using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
#if UNITY_EDITOR
    /// <summary>
    /// Shared helpers for [SerializeReference] property drawers that display a type-picker dropdown
    /// followed by the default inspector for the current managed reference value.
    /// </summary>
    internal static class SerializeReferenceDrawerUtility
    {
        /// <summary>
        /// Draws a type-picker popup and the default child inspector for a [SerializeReference] property.
        /// Returns the total height consumed.
        /// </summary>
        /// <typeparam name="TBase">The abstract base type.</typeparam>
        /// <param name="position">The full rect allocated for this property.</param>
        /// <param name="property">The SerializeReference SerializedProperty.</param>
        /// <param name="label">The GUIContent label.</param>
        public static void OnGUI<TBase>(Rect position, SerializedProperty property, GUIContent label, Func<Type, bool> typeFilter = null)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeLineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            DrawTypePicker<TBase>(typeLineRect, property, typeFilter);

            if (property.managedReferenceValue != null)
            {
                float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                var childRect = new Rect(position.x, position.y + yOffset, position.width, position.height - yOffset);
                DrawChildren(childRect, property);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>Computes total height for the property.</summary>
        public static float GetPropertyHeight<TBase>(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.managedReferenceValue != null)
            {
                var copy = property.Copy();
                var end = property.GetEndProperty();
                copy.NextVisible(true);
                while (!SerializedProperty.EqualContents(copy, end))
                {
                    height += EditorGUI.GetPropertyHeight(copy, true) + EditorGUIUtility.standardVerticalSpacing;
                    copy.NextVisible(false);
                }
            }

            return height;
        }

        private static void DrawTypePicker<TBase>(Rect lineRect, SerializedProperty property, Func<Type, bool> typeFilter)
        {
            var concreteTypes = GetConcreteTypes<TBase>(typeFilter);
            object currentValue = property.managedReferenceValue;
            Type currentType = currentValue?.GetType();

            int currentIndex = 0;
            var names = new string[concreteTypes.Count + 1];
            names[0] = "(none)";
            for (int i = 0; i < concreteTypes.Count; i++)
            {
                names[i + 1] = ObjectNames.NicifyVariableName(concreteTypes[i].Name);
                if (concreteTypes[i] == currentType)
                    currentIndex = i + 1;
            }

            var labelRect = EditorGUI.PrefixLabel(lineRect, GUIUtility.GetControlID(FocusType.Passive),
                new GUIContent("Type"));
            int newIndex = EditorGUI.Popup(labelRect, currentIndex, names);

            if (newIndex != currentIndex)
            {
                property.managedReferenceValue = newIndex == 0
                    ? null
                    : Activator.CreateInstance(concreteTypes[newIndex - 1]);
            }
        }

        internal static void DrawChildren(Rect startRect, SerializedProperty property)
        {
            var copy = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;

            float y = startRect.y;
            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                float h = EditorGUI.GetPropertyHeight(copy, true);
                var rect = new Rect(startRect.x, y, startRect.width, h);
                EditorGUI.PropertyField(rect, copy, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private static readonly Dictionary<Type, List<Type>> _typeCache = new Dictionary<Type, List<Type>>();

        private static List<Type> GetConcreteTypes<TBase>(Func<Type, bool> typeFilter = null)
        {
            Type baseType = typeof(TBase);
            if (_typeCache.TryGetValue(baseType, out var cached))
                return typeFilter == null ? cached : FilterTypes(cached, typeFilter);

            var result = new List<Type>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<TBase>())
            {
                if (!t.IsAbstract && !t.IsInterface)
                    result.Add(t);
            }
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            _typeCache[baseType] = result;
            return typeFilter == null ? result : FilterTypes(result, typeFilter);
        }

        private static List<Type> FilterTypes(List<Type> source, Func<Type, bool> typeFilter)
        {
            var result = new List<Type>();
            for (int i = 0; i < source.Count; i++)
            {
                Type type = source[i];
                if (typeFilter(type))
                    result.Add(type);
            }

            return result;
        }
    }
#endif
}
