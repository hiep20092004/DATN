using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
#if UNITY_EDITOR
    /// <summary>
    /// Property drawer for [SerializeReference] LevelElementData (the new polymorphic type).
    /// Displays a type-picker dropdown + child fields for the selected concrete type.
    /// </summary>
    [CustomPropertyDrawer(typeof(LevelElementData))]
    public class LevelElementDataReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializeReferenceDrawerUtility.OnGUI<LevelElementData>(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return SerializeReferenceDrawerUtility.GetPropertyHeight<LevelElementData>(property, label);
        }
    }
#endif
}
