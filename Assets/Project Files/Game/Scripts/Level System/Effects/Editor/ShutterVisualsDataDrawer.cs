using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomPropertyDrawer(typeof(ShutterVisualsData))]
    public sealed class ShutterVisualsDataDrawer : PropertyDrawer
    {
        private const string BlockTypePropertyName = "blockType";
        private const string VisualsPrefabPropertyName = "visualsPrefab";
        private const string MinValuePropertyName = "minValue";
        private const string MaxValuePropertyName = "maxValue";
        private const string TexturePropertyName = "texture";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 0f;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(BlockTypePropertyName));
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty blockTypeProperty = property.FindPropertyRelative(BlockTypePropertyName);
            SerializedProperty visualsPrefabProperty = property.FindPropertyRelative(VisualsPrefabPropertyName);
            SerializedProperty minValueProperty = property.FindPropertyRelative(MinValuePropertyName);
            SerializedProperty maxValueProperty = property.FindPropertyRelative(MaxValuePropertyName);
            SerializedProperty textureProperty = property.FindPropertyRelative(TexturePropertyName);

            float yPosition = position.y;
            Rect blockTypeRect = new Rect(
                position.x,
                yPosition,
                position.width,
                EditorGUI.GetPropertyHeight(blockTypeProperty));
            yPosition = blockTypeRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            Rect visualsPrefabRect = new Rect(position.x, yPosition, position.width, EditorGUIUtility.singleLineHeight);
            yPosition = visualsPrefabRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            Rect minValueRect = new Rect(position.x, yPosition, position.width, EditorGUIUtility.singleLineHeight);
            yPosition = minValueRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            Rect maxValueRect = new Rect(position.x, yPosition, position.width, EditorGUIUtility.singleLineHeight);
            yPosition = maxValueRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            Rect textureRect = new Rect(position.x, yPosition, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(blockTypeRect, blockTypeProperty);
            EditorGUI.PropertyField(visualsPrefabRect, visualsPrefabProperty);
            EditorGUI.PropertyField(minValueRect, minValueProperty);
            EditorGUI.PropertyField(maxValueRect, maxValueProperty);
            EditorGUI.PropertyField(textureRect, textureProperty);

            EditorGUI.EndProperty();
        }
    }
}
