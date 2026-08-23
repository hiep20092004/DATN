using System;
using System.Collections.Generic;
using UnityEditor;

namespace WaterFlow.Game
{
    internal static class LevelEditorColorCache
    {
        internal static readonly BlockColor[] AllBlockColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));
    }

    public class LevelStatistics
    {
        public Dictionary<BlockColor, int> gateColorCounts = new Dictionary<BlockColor, int>();
        public Dictionary<BlockColor, int> blockColorCounts = new Dictionary<BlockColor, int>();
        public Dictionary<BlockColor, int> missingColorCounts = new Dictionary<BlockColor, int>();
        public int totalGateColors = 0;
        public int totalBlockColors = 0;
    
        public bool HasMissingColors()
        {
            foreach (var kvp in missingColorCounts)
            {
                if (kvp.Value != 0) return true;
            }
            return false;
        }
    }

    public static class LevelStatisticsCalculator
    {
        /// <summary>
        /// Extra-layer blocks included in color stats only for Lift levels (gates stay on base <c>elements</c>).
        /// Matches <see cref="LevelEditorWindow"/> level analysis.
        /// </summary>
        public static SerializedProperty GetExtraLayerElementsForStatistics(SerializedProperty elementsProperty)
        {
            if (elementsProperty?.serializedObject == null)
                return null;

            SerializedObject levelObject = elementsProperty.serializedObject;
            SerializedProperty hasExtraLayer = levelObject.FindProperty("hasExtraLayer");
            SerializedProperty extraLayerType = levelObject.FindProperty("extraLayerType");
            if (hasExtraLayer == null || !hasExtraLayer.boolValue || extraLayerType == null)
                return null;

            if ((ExtraLayerType)extraLayerType.intValue != ExtraLayerType.Lift)
                return null;

            return levelObject.FindProperty("extraLayerElements");
        }

        public static LevelStatistics Calculate(SerializedProperty levelElementsProperty,
            SerializedProperty extraLayerElementsProperty = null)
        {
            LevelStatistics stats = new LevelStatistics();
            if (levelElementsProperty == null)
                return stats;

            foreach (BlockColor color in LevelEditorColorCache.AllBlockColors)
            {
                stats.gateColorCounts[color] = 0;
                stats.blockColorCounts[color] = 0;
                stats.missingColorCounts[color] = 0;
            }

            ProcessElements(stats, levelElementsProperty);

            if (extraLayerElementsProperty != null)
                ProcessExtraLiftLayerBlocks(stats, extraLayerElementsProperty);

            foreach (BlockColor color in LevelEditorColorCache.AllBlockColors)
                stats.missingColorCounts[color] = stats.blockColorCounts[color] - stats.gateColorCounts[color];

            return stats;
        }

        private static void ProcessElements(LevelStatistics stats, SerializedProperty levelElementsProperty)
        {
            for (int i = 0; i < levelElementsProperty.arraySize; i++)
            {
                SerializedProperty element = levelElementsProperty.GetArrayElementAtIndex(i);
                ElementType elementType = LevelAssetRepresentation.GetElementType(element);

                if (elementType == ElementType.Gate)
                {
                    SerializedProperty gateDataArray =
                        element.FindPropertyRelative(LevelAssetRepresentation.GATE_DATA_PROPERTY_NAME);

                    for (int j = 0; j < gateDataArray.arraySize; j++)
                    {
                        SerializedProperty colorData = gateDataArray.GetArrayElementAtIndex(j);
                        BlockColor color = (BlockColor)colorData.FindPropertyRelative("color").intValue;
                        int count = colorData.FindPropertyRelative("colorCount").intValue;

                        if (color == BlockColor.None) continue;
                        stats.gateColorCounts[color] += count;
                        stats.totalGateColors += count;
                    }
                }
                else if (elementType == ElementType.Block)
                {
                    BlockColor color = (BlockColor)element
                        .FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue;
                    if (color == BlockColor.None) continue;

                    BlockType blockType = (BlockType)element
                        .FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue;

                    ApplyBlockAndEffects(
                        stats,
                        color,
                        blockType,
                        element.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME));
                }
                else if (elementType == ElementType.Generator)
                {
                    SerializedProperty generatorQueueProperty =
                        element.FindPropertyRelative(LevelAssetRepresentation.GENERATOR_QUEUE_PROPERTY_NAME);
                    if (generatorQueueProperty == null || !generatorQueueProperty.isArray) continue;

                    for (int j = 0; j < generatorQueueProperty.arraySize; j++)
                    {
                        SerializedProperty entry = generatorQueueProperty.GetArrayElementAtIndex(j);

                        BlockColor entryColor = (BlockColor)entry.FindPropertyRelative("blockColor").intValue;
                        if (entryColor == BlockColor.None) continue;

                        BlockType entryBlockType = (BlockType)entry.FindPropertyRelative("blockType").intValue;

                        ApplyBlockAndEffects(
                            stats,
                            entryColor,
                            entryBlockType,
                            entry.FindPropertyRelative(GeneratorBlockEntry.BlockEffectsPropertyName));
                    }
                }
            }
        }

        // Only counts Block elements from the Lift extra layer — gates and generators on the extra layer are not counted.
        private static void ProcessExtraLiftLayerBlocks(LevelStatistics stats, SerializedProperty extraLayerProperty)
        {
            for (int i = 0; i < extraLayerProperty.arraySize; i++)
            {
                SerializedProperty element = extraLayerProperty.GetArrayElementAtIndex(i);
                if (LevelAssetRepresentation.GetElementType(element) != ElementType.Block)
                    continue;

                BlockColor color = (BlockColor)element
                    .FindPropertyRelative(LevelAssetRepresentation.BLOCK_COLOR_PROPERTY_NAME).intValue;
                if (color == BlockColor.None) continue;

                BlockType blockType = (BlockType)element
                    .FindPropertyRelative(LevelAssetRepresentation.BLOCK_TYPE_PROPERTY_NAME).intValue;

                ApplyBlockAndEffects(
                    stats,
                    color,
                    blockType,
                    element.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME));
            }
        }

        private static void ApplyBlockAndEffects(
            LevelStatistics stats,
            BlockColor baseColor,
            BlockType blockType,
            SerializedProperty effectsArrayProperty)
        {
            int figureSize = BlockFigureGeometryCache.GetBlockPieceCount(blockType);

            stats.blockColorCounts[baseColor] += figureSize;
            stats.totalBlockColors += figureSize;

            if (effectsArrayProperty == null || !effectsArrayProperty.isArray || effectsArrayProperty.arraySize == 0)
                return;

            for (int i = 0; i < effectsArrayProperty.arraySize; i++)
            {
                var blockEffect = effectsArrayProperty.GetArrayElementAtIndex(i).managedReferenceValue as BlockEffectData;
                if (blockEffect == null) continue;
                switch (blockEffect)
                {
                    case LayeredBlockEffectData layered:
                        stats.blockColorCounts[layered.layeredBlockColor] += figureSize;
                        stats.totalBlockColors += figureSize;
                        break;
                    case SwitchLayerBlockEffectData switchLayer:
                        stats.blockColorCounts[switchLayer.layeredBlockColor] += figureSize;
                        stats.totalBlockColors += figureSize;
                        break;
                    case BlockedBlockEffectData _:
                        stats.blockColorCounts[baseColor] -= figureSize;
                        stats.totalBlockColors -= figureSize;
                        break;
                    case DualBlockEffectData dual:
                        int secondColorCount = blockType.GetSecondColorFillAmount();
                        stats.blockColorCounts[baseColor] -= secondColorCount;
                        stats.blockColorCounts[dual.secondDualColor] += secondColorCount;
                        break;
                }
            }
        }
    }
}