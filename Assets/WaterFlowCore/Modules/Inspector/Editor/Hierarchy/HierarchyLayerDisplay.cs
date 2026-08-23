using UnityEditor;
using UnityEngine;

namespace WaterFlow.Core
{
    [InitializeOnLoad]
    public class HierarchyLayerDisplay
    {
        static HierarchyLayerDisplay()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.EntityIdToObject(instanceID) as GameObject;
        
            if (obj == null)
                return;

            // Check if layer is not "Default" (layer 0)
            if (obj.layer != 0)
            {
                string layerName = LayerMask.LayerToName(obj.layer);
            
                // Calculate text size
                GUIContent content = new GUIContent(layerName);
                GUIStyle style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = new Color(0.7f, 0.9f, 1f, 0.9f); 
                style.fontSize = 10;
                style.alignment = TextAnchor.MiddleRight;
            
                Vector2 textSize = style.CalcSize(content);
            
                // Position the label on the right side of the hierarchy item
                Rect labelRect = new Rect(
                    selectionRect.xMax - textSize.x - 5,
                    selectionRect.y,
                    textSize.x,
                    selectionRect.height
                );
            
                GUI.Label(labelRect, layerName, style);
            }
        }
    }
}