using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
#if UNITY_EDITOR
    /// <summary>
    /// Property drawer for [SerializeReference] GateEffectData (the new polymorphic type).
    /// Shows an icon-or-number preview + visual picker popup (aligned with
    /// <see cref="BlockEffectDataReferenceDrawer"/>) followed by the default inspector for the
    /// selected concrete type.
    /// </summary>
    [CustomPropertyDrawer(typeof(GateEffectData))]
    public class GateEffectDataReferenceDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, GateEffectType> EffectTypeByManagedType = new Dictionary<Type, GateEffectType>();
        private static List<Type> s_orderedConcreteTypes;

        private static void DrawEffectPreview(Rect rect, GateEffectType effectType)
        {
            EffectIconPreviewUtility.DrawEffectPreview(rect, effectType.ToString(), (int)effectType, effectType == GateEffectType.None);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GateEffectType currentType = (property.managedReferenceValue as GateEffectData)?.Type ?? GateEffectType.None;

            EditorGUI.BeginProperty(position, label, property);

            Rect typeLineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            DrawTypePicker(typeLineRect, property, currentType);

            if (property.managedReferenceValue != null)
            {
                float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                Rect childRect = new Rect(position.x, position.y + yOffset, position.width, position.height - yOffset);
                SerializeReferenceDrawerUtility.DrawChildren(childRect, property);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return SerializeReferenceDrawerUtility.GetPropertyHeight<GateEffectData>(property, label);
        }

        private static void DrawTypePicker(Rect lineRect, SerializedProperty property, GateEffectType currentType)
        {
            Rect rowRect = EditorGUI.PrefixLabel(lineRect, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Type"));

            if (property.hasMultipleDifferentValues)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EditorStyles.popup.Draw(rowRect, false, false, false, false);
                    Rect mixedPreview = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - EffectIconPreviewUtility.PreviewSide) * 0.5f, EffectIconPreviewUtility.PreviewSide, EffectIconPreviewUtility.PreviewSide);
                    EditorGUI.DrawRect(mixedPreview, new Color(0.35f, 0.35f, 0.35f, 1f));
                    Rect mixedText = new Rect(mixedPreview.xMax + 8f, rowRect.y, rowRect.xMax - mixedPreview.xMax - 22f, rowRect.height);
                    GUI.Label(mixedText, "—", EditorStyles.label);
                }
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.popup.Draw(rowRect, false, false, false, false);
                Rect previewOuter = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - EffectIconPreviewUtility.PreviewSide) * 0.5f, EffectIconPreviewUtility.PreviewSide, EffectIconPreviewUtility.PreviewSide);
                DrawEffectPreview(previewOuter, currentType);
                Rect textRect = new Rect(previewOuter.xMax + 8f, rowRect.y, rowRect.xMax - previewOuter.xMax - 22f, rowRect.height);
                GUI.Label(textRect, currentType.ToString(), EditorStyles.label);
            }

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                PopupWindow.Show(rowRect, new GateEffectDataPickerPopup(property));
                Event.current.Use();
            }
        }

        private static bool TryGetEffectType(Type managedType, out GateEffectType effectType)
        {
            if (EffectTypeByManagedType.TryGetValue(managedType, out effectType))
                return true;

            if (Activator.CreateInstance(managedType) is not GateEffectData effect)
            {
                effectType = GateEffectType.None;
                return false;
            }

            effectType = effect.Type;
            EffectTypeByManagedType[managedType] = effectType;
            return true;
        }

        /// <summary>Concrete <see cref="GateEffectData"/> types ordered by their <see cref="GateEffectType"/> enum value.</summary>
        private static List<Type> GetOrderedConcreteTypes()
        {
            if (s_orderedConcreteTypes != null)
                return s_orderedConcreteTypes;

            List<Type> result = new List<Type>();
            foreach (Type t in TypeCache.GetTypesDerivedFrom<GateEffectData>())
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;
                result.Add(t);
            }

            result.Sort((a, b) =>
            {
                TryGetEffectType(a, out GateEffectType ea);
                TryGetEffectType(b, out GateEffectType eb);
                int cmp = ((int)ea).CompareTo((int)eb);
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            s_orderedConcreteTypes = result;
            return s_orderedConcreteTypes;
        }

        private sealed class GateEffectDataPickerPopup : PopupWindowContent
        {
            private const float RowHeight = 28f;
            private const float RowPreview = 24f;
            private const float RowPadding = 4f;

            private static GUIStyle s_rowLabel;
            private static GUIStyle s_rowLabelBold;

            private readonly SerializedObject serializedObject;
            private readonly string propertyPath;
            private readonly List<Type> rowTypes;
            private Vector2 scroll;

            private static void EnsureRowLabelStyles()
            {
                if (s_rowLabel != null)
                    return;

                s_rowLabel = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft
                };
                s_rowLabelBold = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold
                };
            }

            public GateEffectDataPickerPopup(SerializedProperty property)
            {
                serializedObject = property.serializedObject;
                propertyPath = property.propertyPath;
                rowTypes = new List<Type>(GetOrderedConcreteTypes());
            }

            public override Vector2 GetWindowSize()
            {
                int rowCount = rowTypes.Count + 1; // +1 for "(none)"
                float h = Mathf.Min(520f, 16f + rowCount * RowHeight + 16f);
                return new Vector2(320f, h);
            }

            public override void OnGUI(Rect rect)
            {
                serializedObject.Update();
                SerializedProperty prop = serializedObject.FindProperty(propertyPath);
                if (prop == null)
                {
                    editorWindow.Close();
                    return;
                }

                Type currentType = prop.managedReferenceValue?.GetType();

                scroll = EditorGUILayout.BeginScrollView(scroll);

                DrawNoneRow(prop, currentType == null);
                for (int i = 0; i < rowTypes.Count; i++)
                {
                    Type t = rowTypes[i];
                    DrawSelectableRow(t, currentType, prop);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawNoneRow(SerializedProperty prop, bool isSelected)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                Rect previewRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowPreview) * 0.5f, RowPreview, RowPreview);
                DrawEffectPreview(previewRect, GateEffectType.None);

                Rect labelRect = new Rect(previewRect.xMax + 10f, rowRect.y, rowRect.width - RowPreview - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, "(none)", isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.managedReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                    SelectedBlockEditorCommitEvents.Raise(serializedObject);
                    GUI.changed = true;
                    editorWindow.Close();
                    Event.current.Use();
                }
            }

            private void DrawSelectableRow(Type type, Type selectedType, SerializedProperty prop)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                bool isSelected = type == selectedType;
                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                TryGetEffectType(type, out GateEffectType effectType);

                Rect previewRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowPreview) * 0.5f, RowPreview, RowPreview);
                DrawEffectPreview(previewRect, effectType);

                Rect labelRect = new Rect(previewRect.xMax + 10f, rowRect.y, rowRect.width - RowPreview - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, effectType.ToString(), isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.managedReferenceValue = Activator.CreateInstance(type);
                    serializedObject.ApplyModifiedProperties();
                    SelectedBlockEditorCommitEvents.Raise(serializedObject);
                    GUI.changed = true;
                    editorWindow.Close();
                    Event.current.Use();
                }
            }
        }
    }
#endif
}
