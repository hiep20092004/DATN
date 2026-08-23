using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
#if UNITY_EDITOR
    /// <summary>
    /// Custom drawer for <see cref="GeneratorBlockEntry"/> that draws the blockEffects slots manually
    /// instead of through Unity's built-in array UI. The default ReorderableList wrapper costs ~9ms per
    /// [SerializeReference] array per IMGUI pass (near-constant, regardless of element count), which made
    /// the generator handle menu lag badly during scene-view repaints (camera zoom/rotate) — a 20-entry
    /// queue paid that cost 20 times per pass. Manual slot drawing is ~0.25ms per slot.
    /// </summary>
    [CustomPropertyDrawer(typeof(GeneratorBlockEntry))]
    public sealed class GeneratorBlockEntryDrawer : PropertyDrawer
    {
        private const float BUTTON_WIDTH = 22f;

        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = Line + Spacing;
            if (!property.isExpanded)
                return height;

            SerializedProperty blockId = property.FindPropertyRelative("blockId");
            SerializedProperty blockType = property.FindPropertyRelative("blockType");
            SerializedProperty blockColor = property.FindPropertyRelative("blockColor");
            SerializedProperty effects = property.FindPropertyRelative(GeneratorBlockEntry.BlockEffectsPropertyName);

            height += EditorGUI.GetPropertyHeight(blockId, true) + Spacing;
            height += EditorGUI.GetPropertyHeight(blockType, true) + Spacing;
            height += EditorGUI.GetPropertyHeight(blockColor, true) + Spacing;

            // Effects header row (label + add button), then one block per slot.
            height += Line + Spacing;
            if (effects != null)
            {
                for (int i = 0; i < effects.arraySize; i++)
                    height += EditorGUI.GetPropertyHeight(effects.GetArrayElementAtIndex(i), true) + Spacing;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect line = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded)
                return;

            SerializedProperty blockId = property.FindPropertyRelative("blockId");
            SerializedProperty blockType = property.FindPropertyRelative("blockType");
            SerializedProperty blockColor = property.FindPropertyRelative("blockColor");
            SerializedProperty effects = property.FindPropertyRelative(GeneratorBlockEntry.BlockEffectsPropertyName);

            EditorGUI.indentLevel++;
            float y = position.y + Line + Spacing;

            y = DrawField(position, y, blockId);
            y = DrawField(position, y, blockType);
            y = DrawField(position, y, blockColor);

            // Effects header: label + add button.
            Rect headerRect = new Rect(position.x, y, position.width, Line);
            Rect addRect = new Rect(headerRect.xMax - BUTTON_WIDTH, y, BUTTON_WIDTH, Line);
            EditorGUI.LabelField(headerRect, "Block Effects (" + (effects != null ? effects.arraySize : 0) + ")");
            if (effects != null && GUI.Button(addRect, "+"))
            {
                effects.arraySize++;
                effects.GetArrayElementAtIndex(effects.arraySize - 1).managedReferenceValue = null;
                GUI.changed = true;
            }
            y += Line + Spacing;

            if (effects != null)
            {
                int removeIndex = -1;
                for (int i = 0; i < effects.arraySize; i++)
                {
                    SerializedProperty slot = effects.GetArrayElementAtIndex(i);
                    float slotHeight = EditorGUI.GetPropertyHeight(slot, true);
                    Rect slotRect = new Rect(position.x, y, position.width - BUTTON_WIDTH - 2f, slotHeight);
                    EditorGUI.PropertyField(slotRect, slot, new GUIContent("Effect " + i), true);

                    Rect removeRect = new Rect(position.xMax - BUTTON_WIDTH, y, BUTTON_WIDTH, Line);
                    if (GUI.Button(removeRect, "-"))
                        removeIndex = i;

                    y += slotHeight + Spacing;
                }

                if (removeIndex >= 0)
                {
                    effects.DeleteArrayElementAtIndex(removeIndex);
                    GUI.changed = true;
                }
            }

            EditorGUI.indentLevel--;
        }

        private static float DrawField(Rect position, float y, SerializedProperty property)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), property, true);
            return y + height + Spacing;
        }
    }
#endif
}
