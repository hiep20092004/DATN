#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Physics-Settings-style triangular toggle matrix for <see cref="BlockEffectCompatibilityMatrix"/>.
    /// A checked cell means the two block effects may be combined on one block. Editing one cell mirrors
    /// the symmetric pair. Indexed by <c>(int)BlockEffectType</c>; <see cref="BlockEffectType.None"/> is
    /// excluded from the grid.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlockEffectCompatibilityMatrix))]
    public sealed class BlockEffectCompatibilityMatrixDrawer : PropertyDrawer
    {
        private const string CellsField = "cells";
        private const string StrideField = "stride";

        private const float CellSize = 20f;
        private const float CheckboxSize = 16f;
        private const float LabelWidth = 150f;
        private const float HeaderHeight = 110f;
        private const float HelpHeight = 34f;

        private static readonly BlockEffectType[] AllTypes = (BlockEffectType[])Enum.GetValues(typeof(BlockEffectType));

        private static GUIStyle s_rowLabel;
        private static GUIStyle s_colLabel;

        private static int EnumCount => AllTypes.Length;
        private static int DisplayCount => AllTypes.Length - 1; // skip None (index 0)

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return line + HelpHeight + HeaderHeight + DisplayCount * CellSize + EditorGUIUtility.standardVerticalSpacing * 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            EnsureStyles();

            // Neutralize indent: EditorGUI.Toggle (cells) auto-applies indentLevel while GUI.Label (headers,
            // row labels) does not, which shifts the grid relative to its labels inside indented sections
            // (e.g. the Level Editor window). Apply the indent uniformly to the origin instead.
            int prevIndent = EditorGUI.indentLevel;
            float indentPixels = prevIndent * 15f;
            EditorGUI.indentLevel = 0;
            position.x += indentPixels;
            position.width -= indentPixels;

            float line = EditorGUIUtility.singleLineHeight;
            float y = position.y;

            EditorGUI.LabelField(new Rect(position.x, y, position.width, line), label, EditorStyles.boldLabel);
            y += line + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.HelpBox(new Rect(position.x, y, position.width, HelpHeight - 4f),
                "Checked = the two effects CAN be combined on one block (default). Uncheck to forbid the pair.",
                MessageType.None);
            y += HelpHeight;

            SerializedProperty cellsProp = property.FindPropertyRelative(CellsField);
            SerializedProperty strideProp = property.FindPropertyRelative(StrideField);
            EnsureSize(cellsProp, strideProp);
            int n = strideProp.intValue;

            float gridStartX = position.x + LabelWidth;
            float gridStartY = y + HeaderHeight;

            DrawHoverHighlight(position.x, gridStartX, gridStartY);
            DrawColumnHeaders(gridStartX, gridStartY);
            DrawRowsAndCells(cellsProp, n, position.x, gridStartX, gridStartY);

            EditorGUI.indentLevel = prevIndent;
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Physics-Settings-style hover band: highlights the row and column under the mouse (label, header,
        /// or cell). Drawn behind the toggles. Requests a window repaint while hovering so it tracks the mouse.
        /// </summary>
        private static void DrawHoverHighlight(float labelX, float gridStartX, float gridStartY)
        {
            Vector2 mouse = Event.current.mousePosition;

            float gridRight = gridStartX + DisplayCount * CellSize;
            float gridBottom = gridStartY + DisplayCount * CellSize;
            float headerTop = gridStartY - HeaderHeight;

            Rect area = Rect.MinMaxRect(labelX, headerTop, gridRight, gridBottom);
            if (!area.Contains(mouse))
                return;

            Color highlight = new Color(0.6f, 0.6f, 0.6f, 0.18f);

            // Row band (label + this row's cells, which end at the diagonal).
            if (mouse.y >= gridStartY && mouse.y < gridBottom)
            {
                int row = (int)((mouse.y - gridStartY) / CellSize);
                if (row >= 0 && row < DisplayCount)
                {
                    float rowWidth = LabelWidth + row * CellSize;
                    EditorGUI.DrawRect(new Rect(labelX, gridStartY + row * CellSize, rowWidth, CellSize), highlight);
                }
            }

            // Column band (header + this column's cells), only for columns that actually carry cells.
            if (mouse.x >= gridStartX && mouse.x < gridRight)
            {
                int col = (int)((mouse.x - gridStartX) / CellSize);
                if (col >= 0 && col < DisplayCount - 1)
                {
                    float colX = gridStartX + col * CellSize;
                    EditorGUI.DrawRect(new Rect(colX, headerTop, CellSize, gridBottom - headerTop), highlight);
                }
            }

            EditorWindow hovered = EditorWindow.mouseOverWindow;
            if (hovered != null)
                hovered.Repaint();
        }

        private static void DrawColumnHeaders(float gridStartX, float gridStartY)
        {
            // Column c sits above the cells of rows r > c, so only 0..DisplayCount-2 are ever used.
            for (int c = 0; c < DisplayCount - 1; c++)
            {
                float columnCenterX = gridStartX + c * CellSize + CellSize * 0.5f;
                DrawColumnHeader(columnCenterX, gridStartY, AllTypes[c + 1].ToString());
            }
        }

        private static void DrawRowsAndCells(
            SerializedProperty cellsProp, int stride, float labelX, float gridStartX, float gridStartY)
        {
            const float toggleInset = (CellSize - CheckboxSize) * 0.5f;

            for (int r = 0; r < DisplayCount; r++)
            {
                BlockEffectType rowType = AllTypes[r + 1];
                int rowEnum = (int)rowType;
                float rowY = gridStartY + r * CellSize;

                GUI.Label(new Rect(labelX, rowY, LabelWidth - 6f, CellSize), rowType.ToString(), s_rowLabel);

                for (int c = 0; c < r; c++)
                {
                    int colEnum = (int)AllTypes[c + 1];
                    int idx = rowEnum * stride + colEnum;
                    if (idx < 0 || idx >= cellsProp.arraySize)
                        continue;

                    Rect cellRect = new Rect(
                        gridStartX + c * CellSize + toggleInset,
                        rowY + toggleInset,
                        CheckboxSize, CheckboxSize);

                    SerializedProperty cell = cellsProp.GetArrayElementAtIndex(idx);
                    EditorGUI.BeginChangeCheck();
                    bool value = EditorGUI.Toggle(cellRect, cell.boolValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        cell.boolValue = value;
                        int mirror = colEnum * stride + rowEnum;
                        if (mirror >= 0 && mirror < cellsProp.arraySize)
                            cellsProp.GetArrayElementAtIndex(mirror).boolValue = value;
                    }
                }
            }
        }

        /// <summary>
        /// Draws a vertical column header centered on <paramref name="columnCenterX"/>, reading bottom-to-top.
        /// Rotating a rect symmetric about its own center guarantees horizontal alignment with the column.
        /// </summary>
        private static void DrawColumnHeader(float columnCenterX, float headerBottomY, string text)
        {
            float w = HeaderHeight - 4f; // becomes vertical extent after the -90° rotation
            float h = CellSize;          // becomes the column width after rotation
            float cy = headerBottomY - HeaderHeight * 0.5f;
            Rect r = new Rect(columnCenterX - w * 0.5f, cy - h * 0.5f, w, h);

            Matrix4x4 prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(-90f, new Vector2(columnCenterX, cy));
            GUI.Label(r, text, s_colLabel);
            GUI.matrix = prev;
        }

        /// <summary>Resizes the serialized grid to match the current enum, preserving overlap, defaulting new pairs to combinable.</summary>
        private static void EnsureSize(SerializedProperty cellsProp, SerializedProperty strideProp)
        {
            int n = EnumCount;
            if (strideProp.intValue == n && cellsProp.arraySize == n * n)
                return;

            int oldStride = strideProp.intValue;
            int oldSize = cellsProp.arraySize;
            bool[] old = new bool[oldSize];
            for (int i = 0; i < oldSize; i++)
                old[i] = cellsProp.GetArrayElementAtIndex(i).boolValue;

            cellsProp.arraySize = n * n;
            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++)
                {
                    bool value = true; // default: combinable until a designer forbids it
                    if (a < oldStride && b < oldStride)
                    {
                        int oldIdx = a * oldStride + b;
                        if (oldIdx >= 0 && oldIdx < old.Length)
                            value = old[oldIdx];
                    }
                    cellsProp.GetArrayElementAtIndex(a * n + b).boolValue = value;
                }
            }

            strideProp.intValue = n;
        }

        private static void EnsureStyles()
        {
            if (s_rowLabel != null)
                return;

            s_rowLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight
            };
            s_colLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
}
#endif
