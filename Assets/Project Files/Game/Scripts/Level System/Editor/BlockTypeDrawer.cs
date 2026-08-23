using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Inspector UI for <see cref="BlockType"/>: figure preview + visual picker popup (scroll list with
    /// shape thumbnails), aligned with <see cref="FigureSelectorWindow"/> drawing rules.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlockType))]
    public sealed class BlockTypeDrawer : PropertyDrawer
    {
        private const float PreviewSide = 22f;
        private const float PreviewPadding = 2f;
        private static readonly Color PreviewFill = new Color(0.45f, 0.95f, 0.78f, 1f);

        private static readonly BlockType[] AllBlockTypes = (BlockType[])Enum.GetValues(typeof(BlockType));

        private static BlocksVisualsData s_visuals;
        private static readonly Dictionary<BlockType, LevelFigure> s_figureCache = new Dictionary<BlockType, LevelFigure>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Mathf.Max(EditorGUIUtility.singleLineHeight, PreviewSide + PreviewPadding);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            BlockType current = (BlockType)property.intValue;

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

        private static void DrawPickerRow(Rect rowRect, BlockType current, SerializedProperty property)
        {
            LevelFigure figure = GetFigureForType(current);

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.popup.Draw(rowRect, false, false, false, false);
                Rect previewOuter = new Rect(rowRect.x + 4f, rowRect.y + (rowRect.height - PreviewSide) * 0.5f, PreviewSide, PreviewSide);
                DrawFigurePreview(previewOuter, figure, PreviewFill);
                Rect textRect = new Rect(previewOuter.xMax + 8f, rowRect.y, rowRect.xMax - previewOuter.xMax - 22f, rowRect.height);
                GUI.Label(textRect, current.ToString(), EditorStyles.label);
            }

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                PopupWindow.Show(rowRect, new BlockTypePickerPopup(property));
                Event.current.Use();
            }
        }

        private static LevelFigure GetFigureForType(BlockType blockType)
        {
            if (s_figureCache.TryGetValue(blockType, out LevelFigure cached))
                return cached;

            if (s_visuals == null)
                s_visuals = EditorUtils.GetAsset<BlocksVisualsData>();

            LevelFigure figure = null;
            if (s_visuals != null)
            {
                BlockData[] blocks = s_visuals.Blocks;
                if (blocks != null)
                {
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        BlockData bd = blocks[i];
                        if (bd == null || bd.Type != blockType)
                            continue;
                        bd.Init();
                        figure = bd.Figure;
                        break;
                    }
                }
            }

            s_figureCache[blockType] = figure;
            return figure;
        }

        /// <summary>Same cell indexing as <see cref="FigureSelectorWindow.DrawFigureButton"/>.</summary>
        private static void DrawFigurePreview(Rect pivotRect, LevelFigure figure, Color fillColor)
        {
            EditorGUI.DrawRect(pivotRect, new Color(0.22f, 0.22f, 0.22f));

            if (figure == null || figure.Points == null || figure.Points.Length == 0)
                return;

            int w = figure.Size.x;
            int h = figure.Size.y;
            if (w <= 0 || h <= 0)
                return;

            float inner = pivotRect.width - PreviewPadding * 2f;
            float cell = inner / Mathf.Max(w, h);
            int index = 0;

            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = 0; x < w; x++)
                {
                    if (index < figure.Points.Length && figure.Points[index].IsFilled)
                    {
                        Rect cellRect = new Rect(
                            pivotRect.x + PreviewPadding + x * cell,
                            pivotRect.y + PreviewPadding + y * cell,
                            cell,
                            cell);
                        EditorGUI.DrawRect(cellRect, fillColor);
                    }

                    index++;
                }
            }
        }

        private sealed class BlockTypePickerPopup : PopupWindowContent
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

            public BlockTypePickerPopup(SerializedProperty property)
            {
                serializedObject = property.serializedObject;
                propertyPath = property.propertyPath;
            }

            public override Vector2 GetWindowSize()
            {
                float h = Mathf.Min(520f, 16f + AllBlockTypes.Length * RowHeight + 16f);
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

                BlockType selected = (BlockType)prop.intValue;

                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (BlockType blockType in AllBlockTypes)
                {
                    DrawSelectableRow(blockType, selected, prop);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawSelectableRow(BlockType blockType, BlockType selected, SerializedProperty prop)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.ExpandWidth(true));

                bool isSelected = blockType == selected;
                if (Event.current.type == EventType.Repaint && isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));

                Rect previewRect = new Rect(rowRect.x + RowPadding, rowRect.y + (rowRect.height - RowPreview) * 0.5f, RowPreview, RowPreview);
                DrawFigurePreview(previewRect, GetFigureForType(blockType), PreviewFill);

                Rect labelRect = new Rect(previewRect.xMax + 10f, rowRect.y, rowRect.width - RowPreview - RowPadding - 12f, rowRect.height);
                EnsureRowLabelStyles();
                GUI.Label(labelRect, blockType.ToString(), isSelected ? s_rowLabelBold : s_rowLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    prop.intValue = (int)blockType;
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
