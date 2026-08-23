using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using static System.Linq.Expressions.Expression;

namespace WaterFlow.Core
{
    public class Hierarchy
    {
        private static Type sceneHierarchyWindowType;
        private static Type sceneHierarchyType;
        private static Type treeViewControllerType;
        private static Type treeViewGUIType;

        private static PropertyInfo sceneHierarchyProperty;
        private static FieldInfo hierarchyTreeViewField;
        private static PropertyInfo treeViewGUIProperty;

        private static FieldInfo iconWidthField;
        private static FieldInfo iconSpaceField;

        private EditorWindow window;

        private object sceneHierarchy;
        private object treeViewController;
        private object treeViewGUI;

        private readonly float defaultIconWidth;
        private readonly float defaultSpaceBeforeIcon;

        private static PropertyInfo lastInteractedHierarchyWindow;
        private static Func<object> getLastInteractedHierarchyWindow;
        private static Dictionary<object, Hierarchy> hierarchies = new Dictionary<object, Hierarchy>();

        [InitializeOnLoadMethod]
        private static void PrepareData()
        {
            try
            {
                sceneHierarchyWindowType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                sceneHierarchyProperty = sceneHierarchyWindowType?.GetProperty("sceneHierarchy");

                sceneHierarchyType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchy");
                hierarchyTreeViewField = sceneHierarchyType?.GetField("m_TreeView", BindingFlags.NonPublic | BindingFlags.Instance);

                var assembly = typeof(TreeViewState<int>).Assembly;
                treeViewControllerType = assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController`1");
                if (treeViewControllerType != null)
                {
                    treeViewControllerType = treeViewControllerType.MakeGenericType(typeof(EntityId));
                }
                else
                {
                    treeViewControllerType = assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController");
                }
                
                treeViewGUIProperty = treeViewControllerType?.GetProperty("gui");

                // Note: We'll get k_IconWidth and k_SpaceBetweenIconAndText fields from the actual
                // runtime type of the GUI object in the constructor, not here
                // This is because GameObjectTreeViewGUI inherits from TreeViewGUI<int> and the fields
                // need to be accessed on the actual derived type

                lastInteractedHierarchyWindow = sceneHierarchyWindowType?.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Public | BindingFlags.Static);
                
                if (lastInteractedHierarchyWindow != null)
                {
                    getLastInteractedHierarchyWindow = Lambda<Func<object>>(Property(null, lastInteractedHierarchyWindow)).Compile();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hierarchy] Failed to initialize reflection data: {e.Message}");
            }
        }

        public Hierarchy(EditorWindow window)
        {
            this.window = window;

            try
            {
                if (sceneHierarchyProperty == null || hierarchyTreeViewField == null || treeViewGUIProperty == null)
                {
                    Debug.LogWarning("[Hierarchy] Reflection data not properly initialized");
                    return;
                }

                sceneHierarchy = sceneHierarchyProperty.GetValue(window);
                if (sceneHierarchy == null)
                {
                    Debug.LogWarning("[Hierarchy] Failed to get scene hierarchy from window");
                    return;
                }

                treeViewController = hierarchyTreeViewField.GetValue(sceneHierarchy);
                if (treeViewController == null)
                {
                    Debug.LogWarning("[Hierarchy] Failed to get tree view controller");
                    return;
                }

                treeViewGUI = treeViewGUIProperty.GetValue(treeViewController);
                if (treeViewGUI == null)
                {
                    Debug.LogWarning("[Hierarchy] Failed to get tree view GUI");
                    return;
                }

                // Get the fields from the actual runtime type of the GUI object
                // This is important because GameObjectTreeViewGUI inherits from TreeViewGUI<int>
                // and we need to access the fields on the actual instance type
                Type actualGUIType = treeViewGUI.GetType();
                
                // Look for the fields in the type hierarchy (they're defined in TreeViewGUI<T>)
                iconWidthField = actualGUIType.GetField("k_IconWidth", BindingFlags.Public | BindingFlags.Instance);
                iconSpaceField = actualGUIType.GetField("k_SpaceBetweenIconAndText", BindingFlags.Public | BindingFlags.Instance);

                // If not found directly, search base types
                if (iconWidthField == null || iconSpaceField == null)
                {
                    Type baseType = actualGUIType.BaseType;
                    while (baseType != null && (iconWidthField == null || iconSpaceField == null))
                    {
                        if (iconWidthField == null)
                            iconWidthField = baseType.GetField("k_IconWidth", BindingFlags.Public | BindingFlags.Instance);
                        if (iconSpaceField == null)
                            iconSpaceField = baseType.GetField("k_SpaceBetweenIconAndText", BindingFlags.Public | BindingFlags.Instance);
                        baseType = baseType.BaseType;
                    }
                }

                if (iconWidthField != null && iconSpaceField != null)
                {
                    defaultIconWidth = (float)iconWidthField.GetValue(treeViewGUI);
                    defaultSpaceBeforeIcon = (float)iconSpaceField.GetValue(treeViewGUI);

                    SetIconWidth(0, 18);
                }
                else
                {
                    Debug.LogWarning($"[Hierarchy] Failed to find icon fields on type: {actualGUIType.FullName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hierarchy] Error during initialization: {e.Message}\n{e.StackTrace}");
            }
        }

        private void SetIconWidth(float iconWidth, float spaceBeforeIcon)
        {
            if (treeViewGUI == null || iconWidthField == null || iconSpaceField == null)
                return;

            try
            {
                iconWidthField.SetValue(treeViewGUI, iconWidth);
                iconSpaceField.SetValue(treeViewGUI, spaceBeforeIcon);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hierarchy] Failed to set icon width: {e.Message}");
            }
        }

        private void ResetIconWidth()
        {
            SetIconWidth(defaultIconWidth, defaultSpaceBeforeIcon);
        }

        public void DrawElementGUI(int instanceID, Rect selectionRect)
        {
            if (treeViewGUI == null)
                return;

            // Updated to use EntityIdToObject instead of InstanceIDToObject
            GameObject instanceObject = EditorUtility.EntityIdToObject(instanceID) as GameObject;

            if (!instanceObject) return;

            Texture texture = EditorCustomHierarchy.GetTexture(instanceObject);

            if (texture == null) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                ResetIconWidth();

                return;
            }

            SetIconWidth(0, 18);

            Rect iconRect = new Rect(selectionRect) { width = 16, height = 16 };
            iconRect.y += (iconRect.height - 16) / 2;

            using(new ColorScope(EditorCustomStyles.HIERARCHY_COLOR))
            {
                GUI.DrawTexture(iconRect, texture);
            }
        }

        public static Hierarchy GetLastHierarchy()
        {
            if (getLastInteractedHierarchyWindow == null)
            {
                Debug.LogWarning("[Hierarchy] Hierarchy system not properly initialized");
                return null;
            }

            try
            {
                object lastHierarchyWindow = getLastInteractedHierarchyWindow();

                if (lastHierarchyWindow == null)
                    return null;

                if (!hierarchies.TryGetValue(lastHierarchyWindow, out var hierarchy))
                {
                    hierarchy = new Hierarchy(lastHierarchyWindow as EditorWindow);
                    hierarchies.Add(lastHierarchyWindow, hierarchy);
                }

                return hierarchy;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hierarchy] Error getting last hierarchy: {e.Message}");
                return null;
            }
        }

        public static void ClearHierarchies()
        {
            if (hierarchies.Count == 0) return;

            foreach(Hierarchy hierarchy in hierarchies.Values)
            {
                if(hierarchy != null)
                {
                    hierarchy.ResetIconWidth();
                }
            }

            hierarchies.Clear();
        }
    }
}