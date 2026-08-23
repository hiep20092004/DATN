using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Game
{
    /// <summary>
    /// Debug visualizer for Key-to-Lock connections in the editor.
    /// Shows lines from keys to their target locked gates with color coding.
    /// </summary>
    public class KeyLockDebugVisualizer : Singleton<KeyLockDebugVisualizer>
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugVisualization = true;
        [SerializeField] private float lineThickness = 2f;
        [SerializeField] private bool onlyShowInPlayMode = false;
        
        [Header("Visual Style")]
        [SerializeField] private bool useKeyColor = true;
        [SerializeField] private Color fallbackLineColor = Color.yellow;
        [SerializeField] private float arrowSize = 0.3f;
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableDebugVisualization) return;
            if (onlyShowInPlayMode && !Application.isPlaying) return;
            
            DrawKeyToLockConnections();
        }

        private void DrawKeyToLockConnections()
        {
            // Find all key effects in the scene
            KeyColorBlockEffectBehavior[] keyEffects = Object.FindObjectsByType<KeyColorBlockEffectBehavior>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            if (keyEffects.Length == 0) return;

            foreach (var keyEffect in keyEffects)
            {
                if(!keyEffect.IsActive)  continue;
                BlockColor keyColor = ((KeyColorBlockEffectData)keyEffect.EffectData).keyColor;
                
                // Find matching locked gate
                LockedColorGateEffectBehavior matchingLock = FindMatchingLockedGate(keyColor);
                
                if (matchingLock)
                {
                    DrawConnectionLine(keyEffect, matchingLock, keyColor);
                }
                else
                {
                    // Draw warning indicator if no matching lock found
                    DrawNoTargetWarning(keyEffect, keyColor);
                }
            }
        }

        private LockedColorGateEffectBehavior FindMatchingLockedGate(BlockColor keyColor)
        {
            LockedColorGateEffectBehavior[] lockedGates = Object.FindObjectsByType<LockedColorGateEffectBehavior>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            foreach (var lockedGate in lockedGates)
            {
                if (lockedGate.Color == keyColor &&
                    lockedGate.IsTargetAvailable)
                {
                    return lockedGate;
                }
            }
            
            return null;
        }

        private void DrawConnectionLine(KeyColorBlockEffectBehavior key, LockedColorGateEffectBehavior lockGate, BlockColor keyColor)
        {
            Vector3 keyPos = key.KeyTransform.position;
            Vector3 lockPos = lockGate.TargetPosition;
            
            // Get color for the line
            Color lineColor = GetColorForKey(keyColor);
            
            // Draw main line
            Handles.color = lineColor;
            Handles.DrawAAPolyLine(lineThickness, keyPos, lockPos);
            
            // Draw arrow at the lock position
            DrawArrowHead(lockPos, (lockPos - keyPos).normalized, lineColor);
            
        }

        private void DrawArrowHead(Vector3 position, Vector3 direction, Color color)
        {
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
            
            Handles.color = color;
            Handles.DrawAAPolyLine(lineThickness, position, position + right * arrowSize);
            Handles.DrawAAPolyLine(lineThickness, position, position + left * arrowSize);
        }

        private void DrawNoTargetWarning(KeyColorBlockEffectBehavior key, BlockColor keyColor)
        {
            Vector3 keyPos = key.KeyTransform.position;
            GUIStyle warningStyle = new GUIStyle
            {
                normal =
                {
                    textColor = Color.red
                },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            Handles.Label(keyPos + Vector3.up * 0.5f, 
                $"⚠ No Lock Found!\nColor: {keyColor}", 
                warningStyle);
        }


        private Color GetColorForKey(BlockColor keyColor)
        {
            if (!useKeyColor) return fallbackLineColor;
            
            if (Application.isPlaying && LevelController.Instance != null)
            {
                return LevelController.Instance.GetBlockColorData(keyColor).Color;
            }
            
            // Fallback colors for editor preview
            return fallbackLineColor;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
#endif
    }

}