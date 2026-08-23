using System;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Inspector UI for <see cref="BlockEffectType"/>: numeric-enum preview + visual picker popup
    /// (scroll list), aligned with <see cref="BlockTypeDrawer"/> and <see cref="BlockColorDrawer"/>.
    /// The "preview" thumbnail is the underlying integer enum value rendered as text.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlockEffectType))]
    public sealed class BlockEffectTypeDrawer : PropertyDrawer
    {
        internal const float PreviewSide = 22f;
        internal const float PreviewPadding = 2f;

        private static readonly Color PreviewBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color PreviewBackgroundNone = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color PreviewBorder = new Color(0f, 0f, 0f, 0.45f);

        private static readonly BlockEffectType[] AllEffectTypes = (BlockEffectType[])Enum.GetValues(typeof(BlockEffectType));

        private static GUIStyle s_previewNumberStyle;
        private static GUIStyle s_previewNumberStyleSmall;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Mathf.Max(EditorGUIUtility.singleLineHeight, PreviewSide + PreviewPadding);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            BlockEffectType current = (BlockEffectType)property.intValue;

            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            float fieldX = position.x + EditorGUIUtility.labelWidth;
            float fieldWidth = position.width - EditorGUIUtility.labelWidth;
            Rect rowRect = new Rect(fieldX, position.y, fieldWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            if (property.hasMultipleDifferentValues)
            {
                Rect previewOuter = new Rect(rowRect.x + 2f, rowRect.y + (rowRect.height - PreviewSide) * 0.5f, PreviewSide, PreviewSide);
                EditorGUI.DrawRect(previewOuter, PreviewBackgroundNone);
                EditorGUI.LabelField(new Rect(previewOuter.xMax + 6f, rowRect.y, rowRect.width - PreviewSide - 8f, rowRect.height), "—");
            }
            else
            {
                DrawPickerRow(rowRect, current, property);
            }

            EditorGUI.EndProperty();
        }

        private static void DrawPickerRow(Rect rowRect, BlockEffectType current, SerializedProperty property)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.popup.Draw(rowRect, false, false, false, false);
                Rect previewOuter = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - PreviewSide) * 0.5f, PreviewSide, PreviewSide);
                DrawNumberPreview(previewOuter, current);
                Rect textRect = new Rect(previewOuter.xMax + 8f, rowRect.y, rowRect.xMax - previewOuter.xMax - 22f, rowRect.height);
                GUI.Label(textRect, current.ToString(), EditorStyles.label);
            }

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                PopupWindow.Show(rowRect, new BlockEffectTypePickerPopup(property));
                Event.current.Use();
            }
        }

        private static void EnsureNumberStyles()
        {
            if (s_previewNumberStyle != null)
                return;

            s_previewNumberStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) }
            };

            s_previewNumberStyleSmall = new GUIStyle(s_previewNumberStyle)
            {
                fontSize = 10
            };
        }

        internal static void DrawNumberPreview(Rect outer, BlockEffectType effectType)
        {
            bool isNone = effectType == BlockEffectType.None;
            EditorGUI.DrawRect(outer, isNone ? PreviewBackgroundNone : PreviewBackground);

            Rect inner = new Rect(
                outer.x + PreviewPadding,
                outer.y + PreviewPadding,
                outer.width - PreviewPadding * 2f,
                outer.height - PreviewPadding * 2f);

            const float t = 1f;
            EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMin, inner.width, t), PreviewBorder);
            EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMax - t, inner.width, t), PreviewBorder);
            EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMin, t, inner.height), PreviewBorder);
            EditorGUI.DrawRect(new Rect(inner.xMax - t, inner.yMin, t, inner.height), PreviewBorder);

            EnsureNumberStyles();
            string text = ((int)effectType).ToString();
            GUIStyle style = text.Length >= 3 ? s_previewNumberStyleSmall : s_previewNumberStyle;
            GUI.Label(inner, text, style);
        }

        private sealed class BlockEffectTypePickerPopup : PopupWindowContent
        {
            private const float RowHeight = 28f;
            private const float RowPreview = 24f;
            private const float RowPadding = 4f;

            private static GUIStyle s_rowLabel;
            private static GUIStyle s_rowLabelBold;

            private readonly SerializedObject serializedObject;
            private readonly string propertyPath;
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

            public BlockEffectTypePickerPopup(SerializedProperty property)
            {
                serializedObject = property.serializedObject;
                propertyPath = property.propertyPath;
            }

            public override Vector2 GetWindowSize()
            {
                float h = Mathf.Min(520f, 16f + AllEffectTypes.Length * RowHeight + 16f);
                return new Vector2(320f, h);
            }

            public override void OnGUI(Rect rect)
            {
                // The captured SerializedObject can be invalidated while the popup is open (domain reload /
                // recompile / target destroyed). Touching it then throws inside Update(); bail out cleanly.
                if (serializedObject == null || serializedObject.targetObject == null)
                {
                    editorWindow?.Close();
                    return;
                }

                serializedObject.Update();
                SerializedProperty prop = serializedObject.FindProperty(propertyPath);
                if (prop == null)
                {
                    editorWindow?.Close();
                    return;
                }

                BlockEffectType selected = (BlockEffectType)prop.intValue;

                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (BlockEffectType effectType in AllEffectTypes)
                {
                    DrawSelectableRow(effectType, selected, prop);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawSelectableRow(BlockEffectType effectType, BlockEffectType selected, SerializedProperty prop)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                bool isSelected = effectType == selected;
                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                Rect previewRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowPreview) * 0.5f, RowPreview, RowPreview);
                DrawNumberPreview(previewRect, effectType);

                Rect labelRect = new Rect(previewRect.xMax + 10f, rowRect.y, rowRect.width - RowPreview - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, effectType.ToString(), isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.intValue = (int)effectType;
                    serializedObject.ApplyModifiedProperties();
                    SelectedBlockEditorCommitEvents.Raise(serializedObject);
                    GUI.changed = true;
                    editorWindow.Close();
                    Event.current.Use();
                }
            }
        }
    }
}
