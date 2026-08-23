using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace WaterFlow.Game
{
    /// <summary>
    /// Manages preview editors for GameObjects to display 3D previews in the editor.
    /// Handles cleanup and caching to prevent memory leaks.
    /// </summary>
    public class PreviewManager
    {
        private Dictionary<int, UnityEditor.Editor> blockPreviews = new Dictionary<int, UnityEditor.Editor>();
        private Dictionary<int, UnityEditor.Editor> materialPreviews = new Dictionary<int, UnityEditor.Editor>();

        #region Block Previews
        
        public void DrawBlockPreview(GameObject prefab, int index, Rect previewRect)
        {
            if (prefab == null) return;

            if (!blockPreviews.ContainsKey(index) || blockPreviews[index] == null)
            {
                blockPreviews[index] = UnityEditor.Editor.CreateEditor(prefab);
            }

            if (blockPreviews[index] != null)
            {
                blockPreviews[index].OnInteractivePreviewGUI(previewRect, GUI.skin.box);
            }
        }

        public void RemoveBlockPreview(int index)
        {
            if (blockPreviews.ContainsKey(index) && blockPreviews[index] != null)
            {
                SafeDestroyEditor(blockPreviews[index]);
                blockPreviews.Remove(index);
            }
        }
        
        #endregion

        #region Material Previews
        
        public void DrawMaterialPreview(Material material, int index, Rect previewRect)
        {
            if (material == null) return;

            Texture preview = AssetPreview.GetAssetPreview(material);
            if (preview != null)
            {
                EditorGUI.DrawPreviewTexture(previewRect, preview);
            }
            else
            {
                // Draw loading indicator
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                
                Rect labelRect = new Rect(
                    previewRect.x, 
                    previewRect.y + previewRect.height / 2 - 10, 
                    previewRect.width, 
                    20
                );
                
                GUIStyle centerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                };
                
                GUI.Label(labelRect, "Loading preview...", centerStyle);
            }
        }

        public void RemoveMaterialPreview(int index)
        {
            if (materialPreviews.ContainsKey(index) && materialPreviews[index] != null)
            {
                SafeDestroyEditor(materialPreviews[index]);
                materialPreviews.Remove(index);
            }
        }
        
        #endregion

        #region Cleanup
        
        public void Cleanup()
        {
            CleanupBlockPreviews();
            CleanupMaterialPreviews();
        }

        private void CleanupBlockPreviews()
        {
            foreach (var preview in blockPreviews.Values.ToArray())
            {
                if (preview != null)
                {
                    SafeDestroyEditor(preview);
                }
            }
            blockPreviews.Clear();
        }

        private void CleanupMaterialPreviews()
        {
            foreach (var preview in materialPreviews.Values.ToArray())
            {
                if (preview != null)
                {
                    SafeDestroyEditor(preview);
                }
            }
            materialPreviews.Clear();
        }

        private void SafeDestroyEditor(UnityEditor.Editor editor)
        {
            if (editor == null) return;

            try
            {
                Object.DestroyImmediate(editor);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to destroy editor preview: {e.Message}");
            }
        }
        
        #endregion

        #region Preview Utilities
        
        public static void DrawColorPreview(Color color, float size = 25f)
        {
            Rect colorRect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size));
            
            // Draw background checkerboard pattern for transparency
            DrawCheckerboard(colorRect);
            
            // Draw color
            EditorGUI.DrawRect(colorRect, color);
            
            // Draw border
            DrawBorder(colorRect, new Color(0, 0, 0, 0.4f));
        }

        public static void DrawMaterialPreviewCompact(Material material, float height = 80f)
        {
            if (material == null)
            {
                GUILayout.Box("No Material", GUILayout.Height(height));
                return;
            }

            Rect previewRect =  GUILayoutUtility.GetRect(height, height, GUILayout.Height(height), GUILayout.ExpandWidth(true));
                    
            Texture preview = AssetPreview.GetAssetPreview(material);
            if (preview != null)
            {
                EditorGUI.DrawPreviewTexture(previewRect, preview);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
            }
            
            DrawBorder(previewRect, new Color(0, 0, 0, 0.3f));
        }

        private static void DrawCheckerboard(Rect rect)
        {
            int checkerSize = 8;
            int checksX = Mathf.CeilToInt(rect.width / checkerSize);
            int checksY = Mathf.CeilToInt(rect.height / checkerSize);

            Color lightGray = new Color(0.8f, 0.8f, 0.8f);
            Color darkGray = new Color(0.6f, 0.6f, 0.6f);

            for (int y = 0; y < checksY; y++)
            {
                for (int x = 0; x < checksX; x++)
                {
                    Rect checkRect = new Rect(
                        rect.x + x * checkerSize,
                        rect.y + y * checkerSize,
                        checkerSize,
                        checkerSize
                    );

                    Color color = (x + y) % 2 == 0 ? lightGray : darkGray;
                    EditorGUI.DrawRect(checkRect, color);
                }
            }
        }

        private static void DrawBorder(Rect rect, Color color, float thickness = 1f)
        {
            // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
        
        #endregion
    }
}