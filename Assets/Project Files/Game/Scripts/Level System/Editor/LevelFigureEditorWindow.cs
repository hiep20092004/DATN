using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    public class LevelFigureEditorWindow : EditorWindow
    {
        private readonly string[] TOOLS = { "Edit", "Pivot", "Bounds (hor)", "Bounds (ver)" };
        private const int TOOL_EDIT = 0;
        private const int TOOL_PIVOT = 1;
        private const int TOOL_HORIZONTAL_BOUNDS = 2;
        private const int TOOL_VERTICAL_BOUNDS = 3;

        private SerializedProperty property;
        private SerializedObject serializedObject;

        private const float BackgroundPadding = 2f;

        private Vector2Int size;
        private Texture2D cellTexture;
        private Texture2D pivotTexture;
        private Texture2D boundsTexture;

        private int selectedTool = TOOL_EDIT;

        public static void Open(SerializedProperty property, Texture2D cellTexture, Texture2D pivotTexture, Texture2D boundsTexture)
        {
            LevelFigureEditorWindow window = GetWindow<LevelFigureEditorWindow>(true, "Edit Level Figure", true);
            window.property = property;
            window.serializedObject = property.serializedObject;

            window.size = property.FindPropertyRelative("size").vector2IntValue;
            window.cellTexture = cellTexture;
            window.pivotTexture = pivotTexture;
            window.boundsTexture = boundsTexture;
            window.selectedTool = TOOL_EDIT;

            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (property == null || serializedObject == null)
            {
                Close();

                return;
            }

            serializedObject.Update();

            // Display and edit size variable
            Vector2Int newSize = EditorGUILayout.Vector2IntField("Size", size);
            newSize.x = Mathf.Max(1, newSize.x);
            newSize.y = Mathf.Max(1, newSize.y);
            if (newSize != size)
            {
                Undo.RecordObject(serializedObject.targetObject, "Change Size");
                size = newSize;
                RecalculatePointsArray(size);
            }

            // Draw big preview with clickable elements
            SerializedProperty pointsProperty = property.FindPropertyRelative("points");
            int rows = size.y;
            int cols = size.x;

            float cellSize = Mathf.Min(((270 - BackgroundPadding * 2) / cols), ((270 - BackgroundPadding * 2) / rows));
            Rect previewRect = GUILayoutUtility.GetRect(cols * cellSize + BackgroundPadding * 2, rows * cellSize + BackgroundPadding * 2);
            EditorGUI.DrawRect(previewRect, Color.gray);

            SerializedProperty pivotPointProperty = property.FindPropertyRelative("pivotPoint");
            Vector2Int pivotPoint = pivotPointProperty.vector2IntValue;

            Event e = Event.current;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int index = y * cols + x;
                    if (index < pointsProperty.arraySize)
                    {
                        SerializedProperty arrayElementProperty = pointsProperty.GetArrayElementAtIndex(index);
                        SerializedProperty activeProperty = arrayElementProperty.FindPropertyRelative("isFilled");

                        Rect pointRect = new Rect(previewRect.x + BackgroundPadding + x * cellSize, previewRect.y + BackgroundPadding + (rows - 1 - y) * cellSize, cellSize, cellSize);

                        if (activeProperty.boolValue)
                        {
                            GUI.DrawTexture(pointRect, cellTexture);
                        }

                        if (pivotPoint.x == x && pivotPoint.y == y)
                        {
                            GUI.DrawTexture(pointRect, pivotTexture);
                        }

                        if(selectedTool == TOOL_HORIZONTAL_BOUNDS)
                        {
                            SerializedProperty boundsProperty = arrayElementProperty.FindPropertyRelative("useInHorizontalCenteredBounds");
                            if (boundsProperty.boolValue)
                            {
                                GUI.DrawTexture(pointRect, boundsTexture);
                            }
                        }
                        else if (selectedTool == TOOL_VERTICAL_BOUNDS)
                        {
                            SerializedProperty boundsProperty = arrayElementProperty.FindPropertyRelative("useInVerticalCenteredBounds");
                            if (boundsProperty.boolValue)
                            {
                                GUI.DrawTexture(pointRect, boundsTexture);
                            }
                        }

                        if (e.type == EventType.MouseDown && pointRect.Contains(e.mousePosition))
                        {
                            if(selectedTool == TOOL_EDIT)
                            {
                                activeProperty.boolValue = !activeProperty.boolValue;

                                property.FindPropertyRelative("activePoints").intValue = GetActivePointsCount(pointsProperty);

                                e.Use();
                            }
                            else if(selectedTool == TOOL_PIVOT)
                            {
                                pivotPointProperty.vector2IntValue = new Vector2Int(x, y);

                                e.Use();
                            }
                            else if(selectedTool == TOOL_HORIZONTAL_BOUNDS)
                            {
                                SerializedProperty boundsProperty = arrayElementProperty.FindPropertyRelative("useInHorizontalCenteredBounds");
                                boundsProperty.boolValue = !boundsProperty.boolValue;

                                e.Use();
                            }
                            else if (selectedTool == TOOL_VERTICAL_BOUNDS)
                            {
                                SerializedProperty boundsProperty = arrayElementProperty.FindPropertyRelative("useInVerticalCenteredBounds");
                                boundsProperty.boolValue = !boundsProperty.boolValue;

                                e.Use();
                            }
                        }
                    }
                }
            }

            //draw grid
            Rect gridLineRect = new Rect(previewRect.x + BackgroundPadding, previewRect.y + BackgroundPadding, 2, rows * cellSize);

            for (int x = 0; x <= cols; x++)
            {
                EditorGUI.DrawRect(gridLineRect, Color.white);
                gridLineRect.x += cellSize;
            }

            gridLineRect = new Rect(previewRect.x + BackgroundPadding, previewRect.y + BackgroundPadding, cols * cellSize, 2);

            for (int y = 0; y <= rows; y++)
            {
                EditorGUI.DrawRect(gridLineRect, Color.white);
                gridLineRect.y += cellSize;
            }

            serializedObject.ApplyModifiedProperties();
            SelectedBlockEditorCommitEvents.Raise(serializedObject);

            DrawButtons();

            // Recalculate window size
            float windowHeight = previewRect.height + BackgroundPadding * 2 + 80;
            Vector2 windowSize = new Vector2(270, windowHeight);
            minSize = windowSize;
            maxSize = windowSize;
        }

        private void DrawButtons()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < TOOLS.Length; i++)
            {
                GUI.backgroundColor = (selectedTool == i) ? Color.cyan : Color.white;
                if (GUILayout.Button(TOOLS[i]))
                {
                    selectedTool = i;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            GUI.backgroundColor = Color.white;
        }

        private void RecalculatePointsArray(Vector2Int newSize)
        {
            SerializedProperty pointsProperty = property.FindPropertyRelative("points");
            bool[] oldValues = new bool[pointsProperty.arraySize];
            for (int i = 0; i < pointsProperty.arraySize; i++)
            {
                SerializedProperty activeProperty = pointsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("isFilled");

                oldValues[i] = activeProperty.boolValue;
            }

            bool[] newPoints = new bool[newSize.x * newSize.y];

            for (int y = 0; y < Mathf.Min(size.y, newSize.y); y++)
            {
                for (int x = 0; x < Mathf.Min(size.x, newSize.x); x++)
                {
                    int oldIndex = y * size.x + x;
                    int newIndex = y * newSize.x + x;
                    if (oldIndex < pointsProperty.arraySize && newIndex < newPoints.Length)
                    {
                        newPoints[newIndex] = oldValues[oldIndex];
                    }
                }
            }

            pointsProperty.arraySize = newPoints.Length;
            for (int i = 0; i < newPoints.Length; i++)
            {
                pointsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("isFilled").boolValue = newPoints[i];
            }

            property.FindPropertyRelative("size").vector2IntValue = newSize;
            property.FindPropertyRelative("activePoints").intValue = GetActivePointsCount(pointsProperty);
        }

        private int GetActivePointsCount(SerializedProperty arrayProperty)
        {
            int count = 0;

            int arraySize = arrayProperty.arraySize;
            for (int i = 0; i < arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).FindPropertyRelative("isFilled").boolValue)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnDestroy()
        {
            serializedObject?.Dispose();
        }
    }
}