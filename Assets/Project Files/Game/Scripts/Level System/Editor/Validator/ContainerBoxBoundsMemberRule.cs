using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Ensures every grid cell inside a container box group's occupied bounds (static and movable
    /// variants) belongs only to blocks that share the same
    /// <see cref="ContainerBoxBlockEffectDataBase.containerBoxID"/>. Groups are keyed by
    /// (effect type, box id) to mirror runtime <see cref="LevelRepresentation"/> group formation.
    /// Bounds match runtime <see cref="BlockGroupBoundsCalculator"/> (axis-aligned min/max of member cells).
    /// </summary>
    public sealed class ContainerBoxBoundsMemberRule : ILevelValidationRule
    {
        public string Tag => "Container";

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            if (itemsProperty == null || !itemsProperty.isArray)
                yield break;

            if (!BlockFigureGeometryCache.IsBuilt)
                yield break;

            List<LevelBlockSnapshot> blocks = CollectLevelBlocks(itemsProperty);
            if (blocks.Count == 0)
                yield break;

            var cellToBlockIndices = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < blocks.Count; i++)
            {
                foreach (Vector2Int cell in blocks[i].OccupiedCells)
                {
                    if (!cellToBlockIndices.TryGetValue(cell, out List<int> list))
                        cellToBlockIndices[cell] = list = new List<int>();

                    list.Add(i);
                }
            }

            var groupsByBoxKey = new Dictionary<(BlockEffectType type, int boxId), List<int>>();
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!blocks[i].ContainerBoxId.HasValue)
                    continue;

                (BlockEffectType, int) boxKey = (blocks[i].ContainerType, blocks[i].ContainerBoxId.Value);
                if (!groupsByBoxKey.TryGetValue(boxKey, out List<int> members))
                    groupsByBoxKey[boxKey] = members = new List<int>();

                members.Add(i);
            }

            foreach (KeyValuePair<(BlockEffectType type, int boxId), List<int>> group in groupsByBoxKey)
            {
                (BlockEffectType containerType, int boxId) = group.Key;
                List<int> memberIndices = group.Value;
                if (memberIndices.Count == 0)
                    continue;

                if (!TryGetGroupCellBounds(blocks, memberIndices, out Vector2Int minCell, out Vector2Int maxCell))
                    continue;

                var memberSet = new HashSet<int>(memberIndices);
                var intruderIndices = new HashSet<int>();

                foreach (KeyValuePair<Vector2Int, List<int>> cellEntry in cellToBlockIndices)
                {
                    Vector2Int cell = cellEntry.Key;
                    if (cell.x < minCell.x || cell.x > maxCell.x || cell.y < minCell.y || cell.y > maxCell.y)
                        continue;

                    List<int> occupants = cellEntry.Value;
                    for (int o = 0; o < occupants.Count; o++)
                    {
                        int blockIndex = occupants[o];
                        if (!memberSet.Contains(blockIndex))
                            intruderIndices.Add(blockIndex);
                    }
                }

                if (intruderIndices.Count == 0)
                    continue;

                var intruders = new List<LevelBlockSnapshot>(intruderIndices.Count);
                foreach (int blockIndex in intruderIndices)
                    intruders.Add(blocks[blockIndex]);

                yield return BuildIntruderMessage(containerType, boxId, minCell, maxCell, intruders);
            }
        }

        private static string BuildIntruderMessage(
            BlockEffectType containerType,
            int boxId,
            Vector2Int minCell,
            Vector2Int maxCell,
            List<LevelBlockSnapshot> intruders)
        {
            var sb = new StringBuilder();
            sb.Append(
                $"{containerType} boxId {boxId}: {intruders.Count} block(s) inside group bounds " +
                $"[{minCell.x},{minCell.y}]..[{maxCell.x},{maxCell.y}] are not group members");

            int listed = Mathf.Min(intruders.Count, 3);
            sb.Append(" (e.g. ");
            for (int i = 0; i < listed; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                LevelBlockSnapshot block = intruders[i];
                sb.Append($"blockId {block.BlockId}");
            }

            if (intruders.Count > listed)
                sb.Append(", ...");

            sb.Append(").");
            return sb.ToString();
        }

        private static bool TryGetGroupCellBounds(
            List<LevelBlockSnapshot> blocks,
            List<int> memberIndices,
            out Vector2Int minCell,
            out Vector2Int maxCell)
        {
            minCell = default;
            maxCell = default;
            bool hasAny = false;

            for (int m = 0; m < memberIndices.Count; m++)
            {
                IReadOnlyList<Vector2Int> cells = blocks[memberIndices[m]].OccupiedCells;
                for (int c = 0; c < cells.Count; c++)
                {
                    Vector2Int cell = cells[c];
                    if (!hasAny)
                    {
                        minCell = maxCell = cell;
                        hasAny = true;
                    }
                    else
                    {
                        minCell = Vector2Int.Min(minCell, cell);
                        maxCell = Vector2Int.Max(maxCell, cell);
                    }
                }
            }

            return hasAny;
        }

        private static List<LevelBlockSnapshot> CollectLevelBlocks(SerializedProperty itemsProperty)
        {
            var blocks = new List<LevelBlockSnapshot>();

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty element = itemsProperty.GetArrayElementAtIndex(i);
                if (LevelAssetRepresentation.GetElementType(element) != ElementType.Block)
                    continue;

                Vector2Int pivot = element
                    .FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME)
                    .vector2IntValue;
                BlockType blockType = (BlockType)element
                    .FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME)
                    .intValue;

                int blockId = element
                    .FindPropertyRelative(LevelAssetRepresentation.BLOCK_ID_PROPERTY_NAME)
                    .intValue;

                ContainerBoxBlockEffectDataBase containerBox = TryGetContainerBoxData(
                    element.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME));

                blocks.Add(new LevelBlockSnapshot(pivot, blockType, blockId, containerBox));
            }

            return blocks;
        }

        private static ContainerBoxBlockEffectDataBase TryGetContainerBoxData(SerializedProperty blockEffectsProperty)
        {
            if (blockEffectsProperty == null || !blockEffectsProperty.isArray)
                return null;

            for (int i = 0; i < blockEffectsProperty.arraySize; i++)
            {
                if (blockEffectsProperty.GetArrayElementAtIndex(i).managedReferenceValue
                    is ContainerBoxBlockEffectDataBase containerBox)
                {
                    return containerBox;
                }
            }

            return null;
        }

        private readonly struct LevelBlockSnapshot
        {
            public LevelBlockSnapshot(
                Vector2Int pivotPosition,
                BlockType blockType,
                int blockId,
                ContainerBoxBlockEffectDataBase containerBox)
            {
                BlockId = blockId;
                ContainerBoxId = containerBox?.containerBoxID;
                ContainerType = containerBox?.Type ?? BlockEffectType.None;
                OccupiedCells = BuildOccupiedCells(pivotPosition, blockType);
            }

            public int BlockId { get; }
            public int? ContainerBoxId { get; }
            public BlockEffectType ContainerType { get; }
            public IReadOnlyList<Vector2Int> OccupiedCells { get; }

            private static Vector2Int[] BuildOccupiedCells(Vector2Int pivotPosition, BlockType blockType)
            {
                if (!BlockFigureGeometryCache.HasCachedFigure(blockType))
                    return new[] { pivotPosition };

                Vector2Int[] offsets = BlockFigureGeometryCache.GetOffsetsRelativeToPivot(blockType);
                var cells = new Vector2Int[offsets.Length + 1];
                cells[0] = pivotPosition;

                for (int i = 0; i < offsets.Length; i++)
                    cells[i + 1] = pivotPosition + offsets[i];

                return cells;
            }
        }
    }
}
