using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class ColorBalanceRule: ILevelValidationRule
    {
        public ColorBalanceRule()
        {
            
        }

        public string Tag => "Color";

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            if (itemsProperty == null)
                yield break;

            SerializedProperty extraLayerForStats =
                LevelStatisticsCalculator.GetExtraLayerElementsForStatistics(itemsProperty);

            LevelStatistics stats = LevelStatisticsCalculator.Calculate(itemsProperty, extraLayerForStats);
            if (!stats.HasMissingColors())
                yield break;

            yield return "Block/Gate color counts are mismatched (missing colors vs gates).";
        }
    }
}