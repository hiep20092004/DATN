using System;
using System.Collections.Generic;
using System.Reflection;
using WaterFlow.Core;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomPropertyDrawer(typeof(LevelFigure), false)]
    public class LevelFigureDrawer : PropertyDrawer
    {
        private const float BackgroundPadding = 2f;
        private const float PreviewWidth = 70f;
        private const float PreviewHeight = 70f;

        private static List<FieldInfo> levelFigureFields;
        private static Texture2D cellTexture;
        private static Texture2D pivotTexture;
        private static Texture2D boundsTexture;

        static LevelFigureDrawer()
        {
            Type type = typeof(LevelFigure);
            levelFigureFields = new List<FieldInfo>();

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<LevelEditorSetting>() == null)
                {
                    levelFigureFields.Add(field);
                }
            }

            CreateTextures();
        }

        private static void CreateTextures()
        {
            if(cellTexture == null)
                cellTexture = CreateTexture(Color.red, Color.black);

            if (pivotTexture == null)
                pivotTexture = CreateTexture(Color.clear, Color.blue);

            if(boundsTexture == null)
                boundsTexture = CreateOutlineTexture(Color.yellow);
        }

        public static void EnsureTextures()
        {
            CreateTextures();
        }

        public static Vector2 GetPreviewDimensions(Vector2Int size, float previewWidth = PreviewWidth, float previewHeight = PreviewHeight)
        {
            int cols = Mathf.Max(1, size.x);
            int rows = Mathf.Max(1, size.y);
            float cellSize = Mathf.Min((previewWidth - BackgroundPadding * 2) / cols, (previewHeight - BackgroundPadding * 2) / rows);
            return new Vector2(cols * cellSize + BackgroundPadding * 2, rows * cellSize + BackgroundPadding * 2);
        }

        public static void DrawFigurePreview(Rect rect, LevelFigure figure)
        {
            if (figure == null)
                return;

            EnsureTextures();

            Vector2Int size = figure.Size;
            Vector2Int pivot = figure.PivotPoint;
            PointData[] points = figure.Points;
            int rows = Mathf.Max(1, size.y);
            int cols = Mathf.Max(1, size.x);

            float cellSize = Mathf.Min((PreviewWidth - BackgroundPadding * 2) / cols, (PreviewHeight - BackgroundPadding * 2) / rows);
            float gridWidth = cols * cellSize + BackgroundPadding * 2;
            float gridHeight = rows * cellSize + BackgroundPadding * 2;

            Rect backgroundRect = new Rect(rect.x, rect.y, gridWidth, gridHeight);
            EditorGUI.DrawRect(backgroundRect, Color.gray);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int index = y * cols + x;
                    Rect pointRect = new Rect(
                        rect.x + BackgroundPadding + x * cellSize,
                        rect.y + BackgroundPadding + (rows - 1 - y) * cellSize,
                        cellSize,
                        cellSize);

                    if (points != null && index < points.Length && points[index] != null && points[index].IsFilled)
                        GUI.DrawTexture(pointRect, cellTexture);

                    if (pivot.x == x && pivot.y == y)
                        GUI.DrawTexture(pointRect, pivotTexture);
                }
            }
        }

        private static Texture2D CreateTexture(Color mainColor, Color outlineColor)
        {
            Texture2D tex = new Texture2D(9, 9);

            for (int x = 0; x < tex.width; x++)
            {
                for (int y = 0; y < tex.height; y++)
                {
                    tex.SetPixel(x, y, mainColor);
                }
            }

            for (int x = 0; x < tex.width; x++)
            {
                tex.SetPixel(x, 0, outlineColor);
                tex.SetPixel(x, tex.height - 1, outlineColor);
            }

            for (int y = 0; y < tex.height; y++)
            {
                tex.SetPixel(0, y, outlineColor);
                tex.SetPixel(tex.width - 1, y, outlineColor);
            }

            tex.filterMode = FilterMode.Point;
            tex.Apply();

            return tex;
        }

        private static Texture2D CreateOutlineTexture(Color outlineColor)
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);

            // Set all pixels to transparent
            Color32 transparent = new Color(0, 0, 0, 0);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            // Draw outline
            for (int i = 0; i < size; i++)
            {
                if (i % 2 == 0) 
                {
                    tex.SetPixel(i, 0, outlineColor);               // Top
                    tex.SetPixel(i, size - 1, outlineColor);        // Bottom
                    tex.SetPixel(0, i, outlineColor);               // Left
                    tex.SetPixel(size - 1, i, outlineColor);        // Right
                }
            }

            tex.filterMode = FilterMode.Point;
            tex.Apply();

            return tex;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (cellTexture == null || pivotTexture == null)
                CreateTextures();

            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty pivotPointProperty = property.FindPropertyRelative("pivotPoint");
            SerializedProperty sizeProperty = property.FindPropertyRelative("size");

            // Calculate rects
            Rect leftRect = new Rect(position.x, position.y, position.width - PreviewWidth, position.height);
            Rect rightRect = new Rect(position.x + position.width - PreviewWidth, position.y, PreviewWidth, PreviewHeight);

            // Draw fields found with reflection on the left
            float yOffset = 0;

            using(new EditorGUI.DisabledScope(true))
            {
                EditorGUI.Vector2IntField(new Rect(leftRect.x, leftRect.y + yOffset, leftRect.width, EditorGUIUtility.singleLineHeight), "Size", sizeProperty.vector2IntValue);

                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.Vector2IntField(new Rect(leftRect.x, leftRect.y + yOffset, leftRect.width, EditorGUIUtility.singleLineHeight), "Pivot", pivotPointProperty.vector2IntValue);

                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            foreach (FieldInfo field in levelFigureFields)
            {
                SerializedProperty fieldProperty = property.FindPropertyRelative(field.Name);
                if (fieldProperty != null)
                {
                    float propertyHeight = EditorGUI.GetPropertyHeight(fieldProperty, true);
                    EditorGUI.PropertyField(new Rect(leftRect.x, leftRect.y + yOffset, leftRect.width, propertyHeight), fieldProperty, true);
                    yOffset += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            // Draw points matrix preview on the right
            SerializedProperty pointsProperty = property.FindPropertyRelative("points");
            SerializedProperty activePointsCountProperty = property.FindPropertyRelative("activePoints");

            if(activePointsCountProperty.intValue == -1)
                activePointsCountProperty.intValue = GetActivePointsCount(pointsProperty);

            int rows = sizeProperty.vector2IntValue.y;
            int cols = sizeProperty.vector2IntValue.x;

            float cellSize = Mathf.Min((PreviewWidth - BackgroundPadding * 2) / cols, (PreviewHeight - BackgroundPadding * 2) / rows);

            Rect backgroundRect = new Rect(rightRect.x, rightRect.y, cols * cellSize + BackgroundPadding * 2, rows * cellSize + BackgroundPadding * 2);
            EditorGUI.DrawRect(backgroundRect, Color.gray);

            Vector2Int pivotPoint = pivotPointProperty.vector2IntValue;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int index = y * cols + x;
                    if (index < pointsProperty.arraySize)
                    {
                        Rect pointRect = new Rect(rightRect.x + BackgroundPadding + x * cellSize, rightRect.y + BackgroundPadding + (rows - 1 - y) * cellSize, cellSize, cellSize);

                        if (pointsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("isFilled").boolValue)
                            GUI.DrawTexture(pointRect, cellTexture);

                        if (pivotPoint.x == x && pivotPoint.y == y)
                            GUI.DrawTexture(pointRect, pivotTexture);
                    }
                }
            }

            // Draw Edit button
            Rect buttonRect = new Rect(rightRect.x, rightRect.y + PreviewHeight + BackgroundPadding, PreviewWidth, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(buttonRect, "Edit"))
            {
                SerializedObject serializedObject = new SerializedObject(property.serializedObject.targetObject);
                SerializedProperty tempProperty = serializedObject.FindProperty(property.propertyPath);

                // Open custom editor window
                LevelFigureEditorWindow.Open(tempProperty, cellTexture, pivotTexture, boundsTexture);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalPropertiesHeight = 0;
            foreach (FieldInfo field in levelFigureFields)
            {
                SerializedProperty fieldProperty = property.FindPropertyRelative(field.Name);
                if (fieldProperty != null)
                {
                    totalPropertiesHeight += EditorGUI.GetPropertyHeight(fieldProperty, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            float previewHeightWithButton = PreviewHeight + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return Mathf.Max(totalPropertiesHeight, previewHeightWithButton);
        }

        private int GetActivePointsCount(SerializedProperty arrayProperty)
        {
            int count = 0;

            int arraySize = arrayProperty.arraySize;
            for (int i = 0; i < arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue)
                {
                    count++;
                }
            }

            return count;
        }
    }
}