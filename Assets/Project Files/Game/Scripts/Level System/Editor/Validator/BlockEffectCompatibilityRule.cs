using System.Collections.Generic;
using System.Text;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Flags blocks (and generator queue entries) that carry a pair of block effects marked
    /// incompatible in <see cref="LevelDatabase.BlockEffectCompatibility"/>. Authoring-time safety net
    /// for combinations the effect picker cannot prevent (legacy data, or pairs forbidden after the
    /// level was authored). The matrix is the single source of truth shared with the picker filter.
    /// </summary>
    public sealed class BlockEffectCompatibilityRule : ILevelValidationRule
    {
        public string Tag => "Effect";

        private readonly BlockEffectCompatibilityMatrix matrix;

        public BlockEffectCompatibilityRule()
        {
            LevelDatabase database = EditorUtils.GetAsset<LevelDatabase>();
            matrix = database != null ? database.BlockEffectCompatibility : null;
        }

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            // Duplicate-type detection always runs; pairwise incompatibility only when the matrix is configured.
            if (itemsProperty == null || !itemsProperty.isArray)
                yield break;

            var effectTypes = new List<BlockEffectType>(4);

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty element = itemsProperty.GetArrayElementAtIndex(i);
                ElementType elementType = LevelAssetRepresentation.GetElementType(element);

                if (elementType == ElementType.Block)
                {
                    SerializedProperty blockEffects =
                        element.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME);
                    int blockId = GetBlockId(element);

                    string conflict = FindFirstConflict(blockEffects, effectTypes);
                    if (conflict != null)
                        yield return $"Block id {blockId} at [{GetPositionLabel(element)}] {conflict}.";
                }
                else if (elementType == ElementType.Generator)
                {
                    SerializedProperty queue =
                        element.FindPropertyRelative(LevelAssetRepresentation.GENERATOR_QUEUE_PROPERTY_NAME);
                    if (queue == null || !queue.isArray)
                        continue;

                    for (int q = 0; q < queue.arraySize; q++)
                    {
                        SerializedProperty entry = queue.GetArrayElementAtIndex(q);
                        SerializedProperty entryEffects =
                            entry.FindPropertyRelative(GeneratorBlockEntry.BlockEffectsPropertyName);

                        string conflict = FindFirstConflict(entryEffects, effectTypes);
                        if (conflict != null)
                            yield return $"Generator at [{GetPositionLabel(element)}] queue entry #{q} {conflict}.";
                    }
                }
            }
        }

        /// <summary>
        /// Describes the first problem on the block, or null when none: a duplicate effect type (checked
        /// always) takes priority over an incompatible pair (checked only when the matrix is configured).
        /// </summary>
        private string FindFirstConflict(SerializedProperty blockEffects, List<BlockEffectType> scratch)
        {
            if (blockEffects == null || !blockEffects.isArray)
                return null;

            scratch.Clear();
            for (int i = 0; i < blockEffects.arraySize; i++)
            {
                if (blockEffects.GetArrayElementAtIndex(i).managedReferenceValue is BlockEffectData effect)
                    scratch.Add(effect.Type);
            }

            for (int a = 0; a < scratch.Count; a++)
            {
                for (int b = a + 1; b < scratch.Count; b++)
                {
                    if (scratch[a] == scratch[b])
                        return $"has a duplicate effect: {scratch[a]} (an effect type may only appear once)";
                }
            }

            if (matrix != null && matrix.IsConfigured)
            {
                for (int a = 0; a < scratch.Count; a++)
                {
                    for (int b = a + 1; b < scratch.Count; b++)
                    {
                        if (!matrix.AreCompatible(scratch[a], scratch[b]))
                            return $"has incompatible effects: {scratch[a]} + {scratch[b]}";
                    }
                }
            }

            return null;
        }

        private static int GetBlockId(SerializedProperty element)
        {
            SerializedProperty idProp = element.FindPropertyRelative(LevelAssetRepresentation.BLOCK_ID_PROPERTY_NAME);
            return idProp != null ? idProp.intValue : 0;
        }

        private static string GetPositionLabel(SerializedProperty element)
        {
            SerializedProperty posProp = element.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME);
            if (posProp == null)
                return "?";
            Vector2Int pos = posProp.vector2IntValue;
            return $"{pos.x},{pos.y}";
        }
    }
}
