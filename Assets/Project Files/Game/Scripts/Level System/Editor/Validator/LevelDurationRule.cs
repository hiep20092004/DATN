using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Main-flow level duration must stay inside the recommended solve-time budget. Special levels
    /// (<see cref="SpecialLevelData"/>) have their own pacing and are skipped.
    /// </summary>
    public sealed class LevelDurationRule : ILevelValidationRule
    {
        private const float MIN_RECOMMENDED_DURATION = 60f;
        private const float MAX_RECOMMENDED_DURATION = 300f;
        private const string DURATION_PROPERTY_NAME = "duration";

        public string Tag => "Time";

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            SerializedObject levelObject = itemsProperty?.serializedObject;
            if (levelObject == null)
                yield break;

            if (levelObject.targetObject is SpecialLevelData)
                yield break;

            SerializedProperty durationProperty = levelObject.FindProperty(DURATION_PROPERTY_NAME);
            if (durationProperty == null || durationProperty.propertyType != SerializedPropertyType.Float)
                yield break;

            float duration = durationProperty.floatValue;
            if (duration < MIN_RECOMMENDED_DURATION || duration > MAX_RECOMMENDED_DURATION)
                yield return $"Duration {duration:0}s is outside the recommended " +
                             $"{MIN_RECOMMENDED_DURATION:0}-{MAX_RECOMMENDED_DURATION:0}s range.";
        }
    }
}
