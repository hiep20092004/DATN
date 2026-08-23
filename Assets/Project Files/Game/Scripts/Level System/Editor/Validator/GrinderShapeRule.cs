using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Validates each Grinder machine cell against the tape footprint derived from its
    /// <see cref="InteractableObjectData.GrinderConfig"/>: at least one tape cell, and every tape cell
    /// landing on an empty InnerTile inside the grid. Footprint resolution reuses runtime
    /// <see cref="GrinderLayout"/> so the editor and the game agree.
    /// </summary>
    public sealed class GrinderShapeRule : ILevelValidationRule
    {
        public string Tag => "Grinder";

        /// <summary>
        /// Cells the tape may cover. Tape segments are physical blockers spawned on top of the floor, so the
        /// cell has to be empty floor — a block (or anything structural) underneath is an authoring mistake.
        /// Also used by the level editor grid to flag the same cells red.
        /// </summary>
        public static bool IsTapeCellAllowed(ElementType elementType) =>
            elementType is ElementType.InnerTile;

        /// <summary>
        /// Adds every cell covered by a block to <paramref name="results"/>. Figure blocks author only their
        /// pivot cell, so their remaining pieces stay <see cref="ElementType.InnerTile"/> in the array —
        /// checking the element type alone would miss a tape cell running under a figure. Shared with the
        /// level editor grid so both flag the same cells.
        /// </summary>
        public static void CollectBlockCoveredCells(SerializedProperty itemsProperty, HashSet<Vector2Int> results)
        {
            if (itemsProperty == null || !itemsProperty.isArray)
                return;

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty element = itemsProperty.GetArrayElementAtIndex(i);
                if (LevelAssetRepresentation.GetElementType(element) != ElementType.Block)
                    continue;

                Vector2Int pivot = element
                    .FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME).vector2IntValue;
                results.Add(pivot);

                var blockType = (BlockType)element
                    .FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue;
                if (!BlockFigureGeometryCache.HasCachedFigure(blockType))
                    continue;

                Vector2Int[] offsets = BlockFigureGeometryCache.GetOffsetsRelativeToPivot(blockType);
                for (int o = 0; o < offsets.Length; o++)
                    results.Add(pivot + offsets[o]);
            }
        }

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            if (itemsProperty == null || !itemsProperty.isArray)
                yield break;

            var elementTypeByCell = new Dictionary<Vector2Int, ElementType>();
            var machines = new List<(Vector2Int cell, GrinderLayout layout)>();
            CollectGrinders(itemsProperty, elementTypeByCell, machines);

            if (machines.Count == 0)
                yield break;

            var blockCoveredCells = new HashSet<Vector2Int>();
            CollectBlockCoveredCells(itemsProperty, blockCoveredCells);

            // Two machines whose tapes share a cell would stack colliders there and retract
            // independently, so the cell frees at the wrong time.
            var tapeOwnerByCell = new Dictionary<Vector2Int, Vector2Int>();

            var tapeCells = new List<Vector2Int>();
            foreach ((Vector2Int core, GrinderLayout layout) in machines)
            {
                if (!layout.HasTape)
                {
                    yield return $"Grinder at {core}: no tape configured; " +
                                 "it needs at least 1 cell on one side.";
                    continue;
                }

                tapeCells.Clear();
                layout.AppendTapeCells(core, tapeCells);

                for (int i = 0; i < tapeCells.Count; i++)
                {
                    Vector2Int cell = tapeCells[i];

                    if (cell.x < 0 || cell.y < 0 || cell.x >= gridSize.x || cell.y >= gridSize.y)
                    {
                        yield return $"Grinder at {core}: tape cell {cell} is outside the {gridSize.x}x{gridSize.y} grid.";
                        continue;
                    }

                    if (tapeOwnerByCell.TryGetValue(cell, out Vector2Int otherCore))
                        yield return $"Grinder at {core}: tape cell {cell} overlaps the grinder at {otherCore}.";
                    else
                        tapeOwnerByCell[cell] = core;

                    if (!elementTypeByCell.TryGetValue(cell, out ElementType cellType))
                        continue;

                    if (!IsTapeCellAllowed(cellType))
                    {
                        yield return $"Grinder at {core}: tape cell {cell} sits on a {cellType} cell; " +
                                     "tape cells must be empty InnerTile.";
                    }
                    else if (blockCoveredCells.Contains(cell))
                    {
                        yield return $"Grinder at {core}: tape cell {cell} is covered by a figure block piece; " +
                                     "tape cells must be empty InnerTile.";
                    }
                }
            }
        }

        private static void CollectGrinders(
            SerializedProperty itemsProperty,
            Dictionary<Vector2Int, ElementType> elementTypeByCell,
            List<(Vector2Int cell, GrinderLayout layout)> machines)
        {
            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty element = itemsProperty.GetArrayElementAtIndex(i);
                Vector2Int cell = element
                    .FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME).vector2IntValue;
                ElementType elementType = LevelAssetRepresentation.GetElementType(element);
                elementTypeByCell[cell] = elementType;

                if (elementType != ElementType.InteractableObject)
                    continue;

                SerializedProperty interactableData = element.FindPropertyRelative(
                    LevelAssetRepresentation.INTERACTABLE_OBJECT_DATA_PROPERTY_NAME);
                if (interactableData == null)
                    continue;

                int type = interactableData
                    .FindPropertyRelative(LevelAssetRepresentation.TYPE_PROPERTY_NAME).intValue;
                if (type != (int)InteractableObjectType.Grinder)
                    continue;

                GrinderLayout layout = GrinderLayout.From(interactableData
                    .FindPropertyRelative(LevelAssetRepresentation.INTERACTABLE_GRINDER_CONFIG_PROPERTY_NAME)
                    .vector3IntValue);

                machines.Add((cell, layout));
            }
        }
    }
}
