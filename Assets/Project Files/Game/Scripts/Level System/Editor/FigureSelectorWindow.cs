using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class FigureSelectorWindow : EditorWindow
    {
        public static FigureSelectorWindow window;
        private LevelFigure[] levelFigures;
        private BlockType[] blockTypes;
        private List<CellTypesHandler.CellType> cellColors;
        private BlockColor selectedBlockColor;
        private Color selectedColor;
        private Action<EditorSelectBlockData> selectFigure;

        private Rect buttonRect;
        private Rect figurePivotRect;
        private Rect figurePointRect;
        private int lines;
        private Vector2 scrollVector;
        
        // Color selection UI
        private Rect colorSelectionRect;
        private const int COLOR_BUTTON_SIZE = 32;
        private const int COLOR_BUTTON_SPACING = 8;
        private const int COLOR_SECTION_HEIGHT = 80;

        public static void CreateWindow(LevelFigure[] levelFigures, BlockType[] blockTypes, 
            List<CellTypesHandler.CellType> cellColors, Action<EditorSelectBlockData> selectFigure)
        {
            if(window)
            {
                window.Close();
                window = null;
            }

            window = (FigureSelectorWindow)EditorWindow.GetWindow(typeof(FigureSelectorWindow));
            window.levelFigures = levelFigures;
            window.blockTypes = blockTypes;
            window.cellColors = cellColors;
            if (cellColors is { Count: > 0 })
            {
                window.selectedBlockColor = (BlockColor)cellColors[0].value;
                window.selectedColor = cellColors[0].color;
            }
            else
            {
                window.selectedBlockColor = BlockColor.None;
                window.selectedColor = Color.white;
            }
            window.selectFigure = selectFigure;

            if (levelFigures.Length > 25)
            {
                window.lines = 3;
            }
            else if ( levelFigures.Length > 10)
            {
                window.lines = 2;
            }
            else
            {
                window.lines = 1;
            }

            window.maxSize = new Vector2(750, 600);
            window.minSize = new Vector2(350, 300);
            window.ShowAuxWindow();
            
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // Color Selection Section
            DrawColorSelectionSection();
            
            // Separator line
            EditorGUILayout.Space(5);
            Rect separatorRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(separatorRect, new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(5);
            
            // Figure Selection Section
            scrollVector = EditorGUILayout.BeginScrollView(scrollVector);
            DrawFigureSelectionSection();
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawColorSelectionSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 12;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("Select Color", titleStyle, GUILayout.Height(20));
            
            EditorGUILayout.Space(5);
            
            // Color buttons
            colorSelectionRect = EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            int colorsPerRow = Mathf.Max(1, (int)((position.width - 40) / (COLOR_BUTTON_SIZE + COLOR_BUTTON_SPACING)));
            int currentColorIndex = 0;
            
            foreach (var cellColor in cellColors)
            {
                if (currentColorIndex > 0 && currentColorIndex % colorsPerRow == 0)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
                
                DrawColorButton(cellColor);
                currentColorIndex++;
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Selected color info
            if (selectedBlockColor != BlockColor.None)
            {
                GUIStyle selectedStyle = new GUIStyle(EditorStyles.label);
                selectedStyle.alignment = TextAnchor.MiddleCenter;
                selectedStyle.normal.textColor = selectedColor;
                selectedStyle.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField($"Selected: {selectedBlockColor}", selectedStyle);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawColorButton(CellTypesHandler.CellType cellColor)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(COLOR_BUTTON_SIZE, COLOR_BUTTON_SIZE);
            
            // Draw the color square
            EditorGUI.DrawRect(buttonRect, cellColor.color);
            
            // Draw border
            Color borderColor = (selectedBlockColor == (BlockColor)cellColor.value) 
                ? Color.white 
                : new Color(0.2f, 0.2f, 0.2f);
            
            float borderWidth = (selectedBlockColor == (BlockColor)cellColor.value) ? 3f : 1f;
            
            // Draw border lines
            EditorGUI.DrawRect(new Rect(buttonRect.x, buttonRect.y, buttonRect.width, borderWidth), borderColor);
            EditorGUI.DrawRect(new Rect(buttonRect.x, buttonRect.yMax - borderWidth, buttonRect.width, borderWidth), borderColor);
            EditorGUI.DrawRect(new Rect(buttonRect.x, buttonRect.y, borderWidth, buttonRect.height), borderColor);
            EditorGUI.DrawRect(new Rect(buttonRect.xMax - borderWidth, buttonRect.y, borderWidth, buttonRect.height), borderColor);
            
            // Selection indicator
            if (selectedBlockColor == (BlockColor)cellColor.value)
            {
                // Draw checkmark or circle in the center
                Rect indicatorRect = new Rect(
                    buttonRect.x + buttonRect.width * 0.25f,
                    buttonRect.y + buttonRect.height * 0.25f,
                    buttonRect.width * 0.5f,
                    buttonRect.height * 0.5f
                );
                
                // Draw white circle with black border
                DrawCircle(indicatorRect, Color.black, 2f);
                DrawCircle(new Rect(indicatorRect.x + 2, indicatorRect.y + 2, indicatorRect.width - 4, indicatorRect.height - 4), Color.white, 0f);
            }
            
            // Handle click
            if (Event.current.type == EventType.MouseDown && buttonRect.Contains(Event.current.mousePosition))
            {
                selectedBlockColor = (BlockColor)cellColor.value;
                selectedColor = cellColor.color;
                Event.current.Use();
                Repaint();
            }
            
            // Tooltip
            GUI.Label(buttonRect, new GUIContent("", cellColor.label));
        }

        private void DrawCircle(Rect rect, Color color, float borderWidth)
        {
            Handles.BeginGUI();
            Handles.color = color;
            
            if (borderWidth > 0)
            {
                Handles.DrawSolidDisc(rect.center, Vector3.forward, rect.width / 2f);
            }
            else
            {
                // Filled circle
                int segments = 32;
                Vector3[] points = new Vector3[segments + 1];
                float radius = rect.width / 2f;
                
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * 360f / segments * Mathf.Deg2Rad;
                    points[i] = rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                
                Handles.DrawAAConvexPolygon(points);
            }
            
            Handles.EndGUI();
        }

        private void DrawFigureSelectionSection()
        {
            EditorGUILayout.BeginVertical();

            if (lines == 1)
            {
                for (int i = 0; i < levelFigures.Length; i++)
                {
                    DrawFigureButton(i);
                }
            }
            else 
            {
                int index = 0;

                do
                {
                    EditorGUILayout.BeginHorizontal();

                    for (int i = 0; i < lines; i++)
                    {
                        if (index + i < levelFigures.Length)
                        {
                            DrawFigureButton(index + i);
                        }
                        else
                        {
                            GUILayout.FlexibleSpace();
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    index += lines;

                } while (index < levelFigures.Length);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFigureButton(int figureIndex)
        {
            buttonRect = EditorGUILayout.BeginHorizontal(GUI.skin.box);

            figurePivotRect = GUILayoutUtility.GetRect(24, 24); // for 3*3 figures

            int index = 0;

            for (int y = levelFigures[figureIndex].Size.y - 1; y >= 0; y--)
            {
                for (int x = 0; x < levelFigures[figureIndex].Size.x; x++)
                {
                    if (levelFigures[figureIndex].Points[index].IsFilled)
                    {
                        figurePointRect = new Rect(figurePivotRect.x + 2 + (x * 8), figurePivotRect.y + 2 + (y * 8), 8, 8);
                        LevelEditorBase.DrawColorRect(figurePointRect, selectedColor);
                    }

                    index++;
                }
            }

            EditorGUILayout.LabelField(blockTypes[figureIndex].ToString());

            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
            {
                EditorSelectBlockData data = new EditorSelectBlockData
                {
                    blockType = blockTypes[figureIndex],
                    blockColor = selectedBlockColor
                };
                selectFigure?.Invoke(data);
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            window = null;
        }
    }
}