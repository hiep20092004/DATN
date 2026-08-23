using System;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class LevelColorReplacePopup : PopupWindowContent
    {
        private const float RowHeight = 28f;
        private const float RowSwatch = 24f;
        private const float RowPadding = 4f;

        private static readonly BlockColor[] AllBlockColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));

        private static GUIStyle s_rowLabel;
        private static GUIStyle s_rowLabelBold;

        private readonly BlockColor sourceColor;
        private readonly Func<BlockColor, Color> getEditorColor;
        private readonly Action<BlockColor, BlockColor> replaceColor;
        private Vector2 scroll;

        private LevelColorReplacePopup(
            BlockColor sourceColor,
            Func<BlockColor, Color> getEditorColor,
            Action<BlockColor, BlockColor> replaceColor)
        {
            this.sourceColor = sourceColor;
            this.getEditorColor = getEditorColor;
            this.replaceColor = replaceColor;
        }

        public static void DrawLabel(
            Rect rect,
            BlockColor sourceColor,
            Func<BlockColor, Color> getEditorColor,
            Action<BlockColor, BlockColor> replaceColor)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            GUI.Label(rect, sourceColor.ToString(), EditorStyles.linkLabel);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                PopupWindow.Show(rect, new LevelColorReplacePopup(sourceColor, getEditorColor, replaceColor));
                Event.current.Use();
            }
        }

        public override Vector2 GetWindowSize()
        {
            float h = Mathf.Min(520f, 16f + AllBlockColors.Length * RowHeight + 16f);
            return new Vector2(300f, h);
        }

        public override void OnGUI(Rect rect)
        {
            EnsureRowLabelStyles();

            EditorGUILayout.LabelField($"Replace {sourceColor} with", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (BlockColor blockColor in AllBlockColors)
                DrawSelectableRow(blockColor);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelectableRow(BlockColor blockColor)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));
            bool isSelected = blockColor == sourceColor;
            if (Event.current.type == EventType.Repaint && isSelected)
                EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

            Color rowPalette = getEditorColor != null ? getEditorColor(blockColor) : Color.gray;
            Rect swatchRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowSwatch) * 0.5f, RowSwatch, RowSwatch);
            DrawSwatch(swatchRect, rowPalette, blockColor == BlockColor.None);

            Rect labelRect = new Rect(swatchRect.xMax + 10f, rowRect.y, rowRect.width - RowSwatch - RowPadding - 12f, rowRect.height);
            GUI.Label(labelRect, blockColor.ToString(), isSelected ? s_rowLabelBold : s_rowLabel);

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                replaceColor?.Invoke(sourceColor, blockColor);
                GUI.changed = true;
                editorWindow.Close();
                Event.current.Use();
            }
        }

        private static void DrawSwatch(Rect outer, Color fill, bool isNone)
        {
            const float padding = 2f;
            EditorGUI.DrawRect(outer, new Color(0.22f, 0.22f, 0.22f, 1f));
            Rect inner = new Rect(outer.x + padding, outer.y + padding, outer.width - padding * 2f, outer.height - padding * 2f);
            EditorGUI.DrawRect(inner, isNone ? new Color(0.35f, 0.35f, 0.35f, 1f) : fill);
        }

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
    }
}
