using System;
using UnityEditor;

namespace WaterFlow.Game
{
    /// <summary>
    /// Replaces a <see cref="BlockColor"/> everywhere it appears in a level by walking every serialized
    /// property and rewriting each <see cref="BlockColor"/> enum field (block/gate/generator/effect/obstacle
    /// colors alike). Scope matches <see cref="LevelStatisticsCalculator"/>: base elements plus the Lift
    /// extra layer (which the statistics fold into the base layer's block colors).
    /// </summary>
    public static class LevelColorReplacementUtility
    {
        private static readonly string[] BlockColorEnumNames = Enum.GetNames(typeof(BlockColor));

        public static int ReplaceLevelColor(SerializedProperty baseElementsProperty, BlockColor sourceColor, BlockColor targetColor)
        {
            if (baseElementsProperty == null || sourceColor == targetColor)
                return 0;

            int changedCount = ReplaceInProperty(baseElementsProperty, sourceColor, targetColor);

            SerializedProperty extraLayerProperty =
                LevelStatisticsCalculator.GetExtraLayerElementsForStatistics(baseElementsProperty);
            if (extraLayerProperty != null)
                changedCount += ReplaceInProperty(extraLayerProperty, sourceColor, targetColor);

            return changedCount;
        }

        private static int ReplaceInProperty(SerializedProperty elementsProperty, BlockColor sourceColor, BlockColor targetColor)
        {
            int changedCount = 0;
            SerializedProperty iterator = elementsProperty.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = true;
                if (!IsBlockColorProperty(iterator) || iterator.intValue != (int)sourceColor)
                    continue;

                iterator.intValue = (int)targetColor;
                changedCount++;
            }

            return changedCount;
        }

        private static bool IsBlockColorProperty(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Enum &&
                   HasSameEnumNames(property.enumNames, BlockColorEnumNames);
        }

        private static bool HasSameEnumNames(string[] propertyEnumNames, string[] expectedEnumNames)
        {
            if (propertyEnumNames == null || propertyEnumNames.Length != expectedEnumNames.Length)
                return false;

            for (int i = 0; i < expectedEnumNames.Length; i++)
            {
                if (propertyEnumNames[i] != expectedEnumNames[i])
                    return false;
            }

            return true;
        }
    }
}
