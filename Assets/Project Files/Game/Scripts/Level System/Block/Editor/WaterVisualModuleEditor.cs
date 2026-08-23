#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(WaterVisualModule))]
    public class WaterVisualModuleEditor : UnityEditor.Editor
    {
        private SerializedProperty waterRendererProp;
        private SerializedProperty bubbleEffectProp;
        private SerializedProperty fillMinValueProp;
        private SerializedProperty fillMaxValueProp;

        // Static để giữ giá trị khi chuyển đổi giữa các WaterVisualModule
        private static GameObject _bubbleEffectPrefab;

        private void OnEnable()
        {
            waterRendererProp = serializedObject.FindProperty("waterRenderer");
            bubbleEffectProp = serializedObject.FindProperty("bubbleEffect");
            fillMinValueProp = serializedObject.FindProperty("fillMinValue");
            fillMaxValueProp = serializedObject.FindProperty("fillMaxValue");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            
            // Bubble Effect Prefab field (static)
            EditorGUILayout.LabelField("Auto Setup Configuration", EditorStyles.boldLabel);
            _bubbleEffectPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Bubble Effect Prefab", 
                _bubbleEffectPrefab, 
                typeof(GameObject), 
                false
            );

            EditorGUILayout.Space(3);

            // Auto Setup Button
            GUI.enabled = _bubbleEffectPrefab != null || bubbleEffectProp.objectReferenceValue != null;
            if (GUILayout.Button("Auto Setup", GUILayout.Height(30)))
            {
                AutoSetup();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            // Draw default properties
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(waterRendererProp);
            EditorGUILayout.PropertyField(bubbleEffectProp);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Fill Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(fillMinValueProp);
            EditorGUILayout.PropertyField(fillMaxValueProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoSetup()
        {
            WaterVisualModule module = (WaterVisualModule)target;
            bool foundAny = false;

            // 1. Find WaterRenderer in children
            if (waterRendererProp.objectReferenceValue == null)
            {
                MeshRenderer[] renderers = module.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in renderers)
                {
                    // Tìm renderer có tên chứa "water" (case-insensitive)
                    if (renderer.gameObject.name.ToLower().Contains("water"))
                    {
                        waterRendererProp.objectReferenceValue = renderer;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[WaterRenderer]</color> Found: {renderer.gameObject.name}");
                        break;
                    }
                }

                // Nếu không tìm thấy theo tên, lấy MeshRenderer đầu tiên
                if (waterRendererProp.objectReferenceValue == null && renderers.Length > 0)
                {
                    waterRendererProp.objectReferenceValue = renderers[0];
                    foundAny = true;
                    Debug.Log($"<color=cyan>[WaterRenderer]</color> Found (first): {renderers[0].gameObject.name}");
                }
            }
            else
            {
                Debug.Log($"<color=yellow>[WaterRenderer]</color> Already assigned: {waterRendererProp.objectReferenceValue.name}");
            }

            // 2. Setup BubbleEffect
            BubbleEffect existingBubble = module.GetComponentInChildren<BubbleEffect>(true);
            
            if (existingBubble != null)
            {
                // Đã có BubbleEffect, chỉ cần gán reference
                bubbleEffectProp.objectReferenceValue = existingBubble;
                Debug.Log($"<color=cyan>[BubbleEffect]</color> Found existing: {existingBubble.gameObject.name}");
                foundAny = true;
                
                // Setup Mesh cho BubbleEffect đã tồn tại
                SetupBubbleMesh(existingBubble);
            }
            else if (_bubbleEffectPrefab != null)
            {
                // Instantiate Bubble Effect Prefab
                GameObject bubbleInstance = (GameObject)PrefabUtility.InstantiatePrefab(_bubbleEffectPrefab, module.transform);
                bubbleInstance.name = "BubbleEffect";
                
                BubbleEffect bubbleEffect = bubbleInstance.GetComponent<BubbleEffect>();
                if (bubbleEffect != null)
                {
                    bubbleEffectProp.objectReferenceValue = bubbleEffect;
                    Debug.Log($"<color=green>[BubbleEffect]</color> Instantiated from prefab: {bubbleInstance.name}");
                    foundAny = true;

                    // 3. Setup Mesh cho BubbleEffect
                    SetupBubbleMesh(bubbleEffect);
                }
                else
                {
                    Debug.LogError($"<color=red>[BubbleEffect]</color> Prefab doesn't have BubbleEffect component!");
                    DestroyImmediate(bubbleInstance);
                }

                // Mark scene as dirty
                if (!Application.isPlaying)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        SceneManager.GetActiveScene()
                    );
                }
            }
            else
            {
                Debug.LogWarning("<color=yellow>[BubbleEffect]</color> No existing BubbleEffect found and no prefab assigned.");
            }

            // Apply changes
            if (foundAny)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                Debug.Log("<color=green>✓ Auto Setup completed!</color>");
            }
            else
            {
                Debug.LogWarning("No references found or created. Make sure child objects exist or assign a Bubble Effect Prefab.");
            }
        }

        private void SetupBubbleMesh(BubbleEffect bubbleEffect)
        {
            MeshRenderer waterRenderer = waterRendererProp.objectReferenceValue as MeshRenderer;
            if (waterRenderer == null)
            {
                Debug.LogWarning("<color=yellow>[Bubble Mesh]</color> WaterRenderer not found, cannot setup mesh.");
                return;
            }

            MeshFilter waterMeshFilter = waterRenderer.GetComponent<MeshFilter>();
            if (waterMeshFilter == null || waterMeshFilter.sharedMesh == null)
            {
                Debug.LogWarning("<color=yellow>[Bubble Mesh]</color> Water MeshFilter or Mesh not found.");
                return;
            }

            MeshFilter bubbleMeshFilter = bubbleEffect.GetComponent<MeshFilter>();
            if (bubbleMeshFilter == null)
            {
                bubbleMeshFilter = bubbleEffect.gameObject.AddComponent<MeshFilter>();
                Debug.Log("<color=cyan>[Bubble Mesh]</color> Added MeshFilter to BubbleEffect.");
            }

            // Assign the same mesh as water
            bubbleMeshFilter.sharedMesh = waterMeshFilter.sharedMesh;
            Debug.Log($"<color=green>[Bubble Mesh]</color> Setup completed with mesh: {waterMeshFilter.sharedMesh.name}");

            EditorUtility.SetDirty(bubbleEffect.gameObject);
        }
    }
}
#endif