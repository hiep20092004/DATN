using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    [CustomPropertyDrawer(typeof(InteractableObjectData))]
    public class InteractableObjectDataDrawer : PropertyDrawer
    {
        private static readonly string[] GRINDER_AXIS_OPTIONS = { "Ngang (Horizontal)", "Dọc (Vertical)" };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Always draw Type
            var typeProp = property.FindPropertyRelative("type");
            EditorGUI.PropertyField(line, typeProp, new GUIContent("Interactable Type"));
            line = Next(line);

            // Get selected type
            var effType = (InteractableObjectType)typeProp.enumValueIndex;

            // Draw all fields associated with this type
            if (InteractableObjectData.FIELDS.TryGetValue(effType, out var fieldNames))
            {
                foreach (var fieldName in fieldNames)
                {
                    SerializedProperty sp = property.FindPropertyRelative(fieldName);

                    if (sp == null) continue; // in case mapping has wrong name

                    if (effType == InteractableObjectType.Grinder &&
                        fieldName == LevelAssetRepresentation.INTERACTABLE_GRINDER_CONFIG_PROPERTY_NAME)
                    {
                        line.y += DrawGrinderConfig(line, sp);
                        continue;
                    }

                    float h = EditorGUI.GetPropertyHeight(sp, includeChildren: true);
                    EditorGUI.PropertyField(new Rect(line.x, line.y, line.width, h), sp, includeChildren: true);

                    line.y += h + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// The config is one Vector3Int so it stays a single serialized field, but X/Y/Z say nothing to
        /// a level designer — draw it as axis + per-side tape counts instead. Returns the height used.
        /// </summary>
        private static float DrawGrinderConfig(Rect line, SerializedProperty configProp)
        {
            Vector3Int config = configProp.vector3IntValue;
            bool isVertical = config.x == GrinderLayout.AXIS_VERTICAL;

            EditorGUI.BeginChangeCheck();

            int axisIndex = EditorGUI.Popup(line, "Direction", isVertical ? 1 : 0, GRINDER_AXIS_OPTIONS);
            line = Next(line);

            int positive = Mathf.Max(0, EditorGUI.IntField(line,
                isVertical ? "Tape Up (+)" : "Tape Right (+)", config.y));
            line = Next(line);

            int negative = Mathf.Max(0, EditorGUI.IntField(line,
                isVertical ? "Tape Down (-)" : "Tape Left (-)", config.z));

            if (EditorGUI.EndChangeCheck())
            {
                configProp.vector3IntValue = new Vector3Int(
                    axisIndex == 1 ? GrinderLayout.AXIS_VERTICAL : GrinderLayout.AXIS_HORIZONTAL,
                    positive,
                    negative);
            }

            return GetGrinderConfigHeight();
        }

        private static float GetGrinderConfigHeight() =>
            (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // Type

            SerializedProperty typeProp = property.FindPropertyRelative("type");
            InteractableObjectType effType = (InteractableObjectType)typeProp.enumValueIndex;

            if (InteractableObjectData.FIELDS.TryGetValue(effType, out var fieldNames))
            {
                foreach (var fieldName in fieldNames)
                {
                    var sp = property.FindPropertyRelative(fieldName);
                    if (sp == null) continue;

                    if (effType == InteractableObjectType.Grinder &&
                        fieldName == LevelAssetRepresentation.INTERACTABLE_GRINDER_CONFIG_PROPERTY_NAME)
                    {
                        height += GetGrinderConfigHeight();
                        continue;
                    }

                    height += EditorGUI.GetPropertyHeight(sp, includeChildren: true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }

        private static Rect Next(Rect line)
        {
            return new Rect(line.x, line.y + line.height + EditorGUIUtility.standardVerticalSpacing, line.width, EditorGUIUtility.singleLineHeight);
        }
    }
}
