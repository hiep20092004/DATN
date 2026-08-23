using System;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Inspector UI for <see cref="BlockColor"/>: palette swatch from <see cref="LevelDatabase.editorColorData"/>
    /// plus visual picker popup (scroll list), aligned with <see cref="BlockTypeDrawer"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlockColor))]
    public sealed class BlockColorDrawer : PropertyDrawer
    {
        private const float PreviewSide = 22f;
        private const float PreviewPadding = 2f;

        private static readonly BlockColor[] AllBlockColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));

        private static LevelDatabase s_levelDatabase;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Mathf.Max(EditorGUIUtility.singleLineHeight, PreviewSide + PreviewPadding);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            BlockColor current = (BlockColor)property.intValue;

            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            float fieldX = position.x + EditorGUIUtility.labelWidth;
            float fieldWidth = position.width - EditorGUIUtility.labelWidth;
            Rect rowRect = new Rect(fieldX, position.y, fieldWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            if (property.hasMultipleDifferentValues)
            {
                Rect previewOuter = new Rect(rowRect.x + 2f, rowRect.y + (rowRect.height - PreviewSide) * 0.5f, PreviewSide, PreviewSide);
                EditorGUI.DrawRect(previewOuter, new Color(0.35f, 0.35f, 0.35f));
                EditorGUI.LabelField(new Rect(previewOuter.xMax + 6f, rowRect.y, rowRect.width - PreviewSide - 8f, rowRect.height), "—");
            }
            else
            {
                DrawPickerRow(rowRect, current, property);
            }

            EditorGUI.EndProperty();
        }

        private static void DrawPickerRow(Rect rowRect, BlockColor current, SerializedProperty property)
        {
            Color palette = GetPaletteColor(current);

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.popup.Draw(rowRect, false, false, false, false);
                Rect previewOuter = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - PreviewSide) * 0.5f, PreviewSide, PreviewSide);
                DrawSwatch(previewOuter, palette, current == BlockColor.None);
                Rect textRect = new Rect(previewOuter.xMax + 8f, rowRect.y, rowRect.xMax - previewOuter.xMax - 22f, rowRect.height);
                GUI.Label(textRect, current.ToString(), EditorStyles.label);
            }

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                PopupWindow.Show(rowRect, new BlockColorPickerPopup(property));
                Event.current.Use();
            }
        }

        private static Color GetPaletteColor(BlockColor blockColor)
        {
            LevelDatabase db = GetLevelDatabase();
            if (db != null)
                return db.Editor_GetBlockEditorPaletteColor(blockColor);

            if (blockColor == BlockColor.None)
                return new Color(0.38f, 0.38f, 0.38f, 1f);

            return new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        private static LevelDatabase GetLevelDatabase()
        {
            if (s_levelDatabase == null)
                s_levelDatabase = EditorUtils.GetAsset<LevelDatabase>();

            return s_levelDatabase;
        }

        private static void DrawSwatch(Rect outer, Color fill, bool isNone)
        {
            EditorGUI.DrawRect(outer, new Color(0.22f, 0.22f, 0.22f, 1f));

            Rect inner = new Rect(
                outer.x + PreviewPadding,
                outer.y + PreviewPadding,
                outer.width - PreviewPadding * 2f,
                outer.height - PreviewPadding * 2f);

            if (isNone)
            {
                EditorGUI.DrawRect(inner, new Color(0.35f, 0.35f, 0.35f, 1f));
                return;
            }

            EditorGUI.DrawRect(inner, fill);

            float luma = 0.299f * fill.r + 0.587f * fill.g + 0.114f * fill.b;
            if (luma > 0.88f)
            {
                Color edge = new Color(0f, 0f, 0f, 0.45f);
                const float t = 1f;
                EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMin, inner.width, t), edge);
                EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMax - t, inner.width, t), edge);
                EditorGUI.DrawRect(new Rect(inner.xMin, inner.yMin, t, inner.height), edge);
                EditorGUI.DrawRect(new Rect(inner.xMax - t, inner.yMin, t, inner.height), edge);
            }
        }

        private sealed class BlockColorPickerPopup : PopupWindowContent
        {
            private const float RowHeight = 28f;
            private const float RowSwatch = 24f;
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

            public BlockColorPickerPopup(SerializedProperty property)
            {
                serializedObject = property.serializedObject;
                propertyPath = property.propertyPath;
            }

            public override Vector2 GetWindowSize()
            {
                float h = Mathf.Min(520f, 16f + AllBlockColors.Length * RowHeight + 16f);
                return new Vector2(300f, h);
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

                BlockColor selected = (BlockColor)prop.intValue;

                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (BlockColor blockColor in AllBlockColors)
                    DrawSelectableRow(blockColor, selected, prop);

                EditorGUILayout.EndScrollView();
            }

            private void DrawSelectableRow(BlockColor blockColor, BlockColor selected, SerializedProperty prop)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                bool isSelected = blockColor == selected;
                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                Color rowPalette = GetPaletteColor(blockColor);
                Rect swatchRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowSwatch) * 0.5f, RowSwatch, RowSwatch);
                DrawSwatch(swatchRect, rowPalette, blockColor == BlockColor.None);

                Rect labelRect = new Rect(swatchRect.xMax + 10f, rowRect.y, rowRect.width - RowSwatch - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, blockColor.ToString(), isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.intValue = (int)blockColor;
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
