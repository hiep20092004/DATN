using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WaterFlow.Core;
using UnityEditor;

namespace WaterFlow.Game
{
    public static class LevelEditorUtils
    {
        public static IEnumerable<SerializedProperty> GetLevelEditorProperties(SerializedObject serializedObject)
        {
            Type targetType = serializedObject.targetObject.GetType();

            IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE)
                .Where(x => x.GetCustomAttribute<LevelEditorSetting>() != null);

            foreach (var field in fieldInfos)
            {
                SerializedProperty serializedProperty = serializedObject.FindProperty(field.Name);
                if (serializedProperty != null)
                    yield return serializedProperty;
            }
        }

        public static IEnumerable<SerializedProperty> GetLevelEditorProperties(SerializedProperty serializedProperty)
        {
            if (serializedProperty.propertyType == SerializedPropertyType.Generic)
            {
                Type targetType = serializedProperty.boxedValue.GetType();
                IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE)
                    .Where(x => x.GetCustomAttribute<LevelEditorSetting>() != null);
                foreach (var field in fieldInfos)
                {
                    SerializedProperty subProperty = serializedProperty.FindPropertyRelative(field.Name);
                    if (subProperty != null)
                        yield return subProperty;
                }
            }
        }

        public static IEnumerable<SerializedProperty> GetUnmarkedProperties(SerializedObject serializedObject)
        {
            if(!serializedObject.targetObject)
                yield break;
            Type targetType = serializedObject.targetObject.GetType();

            IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE)
                .Where(x => x.GetCustomAttribute<LevelEditorSetting>() == null);

            foreach (var field in fieldInfos)
            {
                SerializedProperty serializedProperty = serializedObject.FindProperty(field.Name);
                if (serializedProperty != null)
                    yield return serializedProperty;
            }
        }

        public static IEnumerable<SerializedProperty> GetUnmarkedProperties(SerializedProperty serializedProperty)
        {
            if (serializedProperty.propertyType == SerializedPropertyType.Generic)
            {
                Type targetType = serializedProperty.boxedValue.GetType();
                IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE)
                    .Where(x => x.GetCustomAttribute<LevelEditorSetting>() == null);
                foreach (var field in fieldInfos)
                {
                    SerializedProperty subProperty = serializedProperty.FindPropertyRelative(field.Name);
                    if (subProperty != null)
                        yield return subProperty;
                }
            }
        }
    }
}