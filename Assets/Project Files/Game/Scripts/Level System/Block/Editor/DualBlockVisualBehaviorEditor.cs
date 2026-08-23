#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using WaterFlow.Core;

namespace WaterFlow.Game
{
    [CustomEditor(typeof(DualBlockVisualBehavior))]
    public class DualBlockVisualBehaviorEditor : UnityEditor.Editor
    {
        private SerializedProperty meshRenderer1Prop;
        private SerializedProperty meshGlass1Prop;
        private SerializedProperty waterModule1Prop;
        private SerializedProperty water1MinValueProp;
        private SerializedProperty water1MaxValueProp;

        private SerializedProperty meshRenderer2Prop;
        private SerializedProperty meshGlass2Prop;
        private SerializedProperty waterModule2Prop;
        private SerializedProperty water2MinValueProp;
        private SerializedProperty water2MaxValueProp;

        // Static để giữ giá trị khi chuyển đổi
        private static BlockColor testColor1 = BlockColor.Red;
        private static BlockColor testColor2 = BlockColor.Blue;
        private static float testFillPercent1 = 0.5f;
        private static float testFillPercent2 = 0.5f;

        // Static clipboard cho copy/paste min/max values
        private static float copiedMinValue;
        private static float copiedMaxValue;
        private static bool hasClipboardData = false;

        // Lưu giá trị cũ để detect changes
        private float lastWater1Min;
        private float lastWater1Max;
        private float lastWater2Min;
        private float lastWater2Max;

        private void OnEnable()
        {
            meshRenderer1Prop = serializedObject.FindProperty("meshRenderer1");
            meshGlass1Prop = serializedObject.FindProperty("meshGlass1");
            waterModule1Prop = serializedObject.FindProperty("waterModule1");
            water1MinValueProp = serializedObject.FindProperty("water1MinValue");
            water1MaxValueProp = serializedObject.FindProperty("water1MaxValue");

            meshRenderer2Prop = serializedObject.FindProperty("meshRenderer2");
            meshGlass2Prop = serializedObject.FindProperty("meshGlass2");
            waterModule2Prop = serializedObject.FindProperty("waterModule2");
            water2MinValueProp = serializedObject.FindProperty("water2MinValue");
            water2MaxValueProp = serializedObject.FindProperty("water2MaxValue");

            // Lưu giá trị ban đầu
            lastWater1Min = water1MinValueProp.floatValue;
            lastWater1Max = water1MaxValueProp.floatValue;
            lastWater2Min = water2MinValueProp.floatValue;
            lastWater2Max = water2MaxValueProp.floatValue;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DualBlockVisualBehavior dualBlock = (DualBlockVisualBehavior)target;

            // ========== AUTO SETUP SECTION ==========
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Auto Setup", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Auto Find All References", GUILayout.Height(30)))
            {
                AutoFindReferences();
            }

            EditorGUILayout.Space(10);

            // ========== COLOR TEST SECTION ==========
            EditorGUILayout.LabelField("Color Test (Editor Only)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            {
                // Block 1
                EditorGUILayout.LabelField("Block 1", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    testColor1 = (BlockColor)EditorGUILayout.EnumPopup("Color 1", testColor1);
                    testFillPercent1 = EditorGUILayout.Slider("Fill", testFillPercent1, 0f, 1f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // Block 2
                EditorGUILayout.LabelField("Block 2", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                {
                    testColor2 = (BlockColor)EditorGUILayout.EnumPopup("Color 2", testColor2);
                    testFillPercent2 = EditorGUILayout.Slider("Fill", testFillPercent2, 0f, 1f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Apply buttons
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Apply Colors & Fill", GUILayout.Height(25)))
                    {
                        ApplyTestColors(dualBlock);
                    }
                    
                    if (GUILayout.Button("Reset", GUILayout.Height(25)))
                    {
                        ResetColors(dualBlock);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // ========== BLOCK 1 REFERENCES ==========
            EditorGUILayout.LabelField("Block 1 References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(meshRenderer1Prop);
            EditorGUILayout.PropertyField(meshGlass1Prop);
            EditorGUILayout.PropertyField(waterModule1Prop);
            
            // Water 1 Min/Max with change detection and Copy/Paste buttons
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(water1MinValueProp);
                EditorGUILayout.PropertyField(water1MaxValueProp);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    ApplyWater1Bounds(dualBlock);
                    lastWater1Min = water1MinValueProp.floatValue;
                    lastWater1Max = water1MaxValueProp.floatValue;
                }

                // Copy/Paste buttons for Block 1
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Copy Min/Max", GUILayout.Height(20)))
                    {
                        CopyMinMax(water1MinValueProp.floatValue, water1MaxValueProp.floatValue);
                    }
                    
                    GUI.enabled = hasClipboardData;
                    if (GUILayout.Button($"Paste ({copiedMinValue:F2}/{copiedMaxValue:F2})", GUILayout.Height(20)))
                    {
                        PasteMinMax(water1MinValueProp, water1MaxValueProp);
                        serializedObject.ApplyModifiedProperties();
                        ApplyWater1Bounds(dualBlock);
                    }
                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // ========== BLOCK 2 REFERENCES ==========
            EditorGUILayout.LabelField("Block 2 References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(meshRenderer2Prop);
            EditorGUILayout.PropertyField(meshGlass2Prop);
            EditorGUILayout.PropertyField(waterModule2Prop);
            
            // Water 2 Min/Max with change detection and Copy/Paste buttons
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(water2MinValueProp);
                EditorGUILayout.PropertyField(water2MaxValueProp);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    ApplyWater2Bounds(dualBlock);
                    lastWater2Min = water2MinValueProp.floatValue;
                    lastWater2Max = water2MaxValueProp.floatValue;
                }

                // Copy/Paste buttons for Block 2
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Copy Min/Max", GUILayout.Height(20)))
                    {
                        CopyMinMax(water2MinValueProp.floatValue, water2MaxValueProp.floatValue);
                    }
                    
                    GUI.enabled = hasClipboardData;
                    if (GUILayout.Button($"Paste ({copiedMinValue:F2}/{copiedMaxValue:F2})", GUILayout.Height(20)))
                    {
                        PasteMinMax(water2MinValueProp, water2MaxValueProp);
                        serializedObject.ApplyModifiedProperties();
                        ApplyWater2Bounds(dualBlock);
                    }
                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void CopyMinMax(float minValue, float maxValue)
        {
            copiedMinValue = minValue;
            copiedMaxValue = maxValue;
            hasClipboardData = true;
            Debug.Log($"<color=green>✓ Copied Min/Max:</color> {minValue:F2} / {maxValue:F2}");
        }

        private void PasteMinMax(SerializedProperty minProp, SerializedProperty maxProp)
        {
            if (!hasClipboardData)
            {
                Debug.LogWarning("No clipboard data available!");
                return;
            }

            minProp.floatValue = copiedMinValue;
            maxProp.floatValue = copiedMaxValue;
            Debug.Log($"<color=green>✓ Pasted Min/Max:</color> {copiedMinValue:F2} / {copiedMaxValue:F2}");
        }

        private void ApplyWater1Bounds(DualBlockVisualBehavior dualBlock)
        {
            WaterVisualModule waterModule1 = waterModule1Prop.objectReferenceValue as WaterVisualModule;
            if (waterModule1 == null || waterModule1.WaterRenderer == null) return;

            float minValue = water1MinValueProp.floatValue;
            float maxValue = water1MaxValueProp.floatValue;

            // Apply to material property block in editor
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            waterModule1.WaterRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID, minValue);
            mpb.SetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID, maxValue);
            waterModule1.WaterRenderer.SetPropertyBlock(mpb);

            EditorUtility.SetDirty(waterModule1.WaterRenderer);
            Debug.Log($"<color=cyan>[Water 1 Bounds]</color> Applied: Min={minValue:F2}, Max={maxValue:F2}");
        }

        private void ApplyWater2Bounds(DualBlockVisualBehavior dualBlock)
        {
            WaterVisualModule waterModule2 = waterModule2Prop.objectReferenceValue as WaterVisualModule;
            if (waterModule2 == null || waterModule2.WaterRenderer == null) return;

            float minValue = water2MinValueProp.floatValue;
            float maxValue = water2MaxValueProp.floatValue;

            // Apply to material property block in editor
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            waterModule2.WaterRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID, minValue);
            mpb.SetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID, maxValue);
            waterModule2.WaterRenderer.SetPropertyBlock(mpb);

            EditorUtility.SetDirty(waterModule2.WaterRenderer);
            Debug.Log($"<color=cyan>[Water 2 Bounds]</color> Applied: Min={minValue:F2}, Max={maxValue:F2}");
        }

        private void AutoFindReferences()
        {
            DualBlockVisualBehavior dualBlock = (DualBlockVisualBehavior)target;
            bool foundAny = false;

            Transform rootTransform = dualBlock.transform;

            // Tìm part1 và part2
            Transform part1 = FindChildByName(rootTransform, "part1");
            Transform part2 = FindChildByName(rootTransform, "part2");

            if (part1 == null || part2 == null)
            {
                Debug.LogError("<color=red>Cannot find 'part1' or 'part2' in hierarchy!</color>");
                return;
            }

            // ========== AUTO ASSIGN BLOCK 1 (part1) ==========
            if (meshRenderer1Prop.objectReferenceValue == null)
            {
                Transform bllStroke1 = FindChildByName(part1, "BLL_Stroke");
                if (bllStroke1 != null)
                {
                    MeshRenderer renderer = bllStroke1.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        meshRenderer1Prop.objectReferenceValue = renderer;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[Block1 Mesh]</color> Found: {bllStroke1.name}");
                    }
                }
            }

            if (meshGlass1Prop.objectReferenceValue == null)
            {
                Transform bllGlass1 = FindChildByName(part1, "BLL_Glass");
                if (bllGlass1 != null)
                {
                    MeshRenderer renderer = bllGlass1.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        meshGlass1Prop.objectReferenceValue = renderer;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[Block1 Glass]</color> Found: {bllGlass1.name}");
                    }
                }
            }

            if (waterModule1Prop.objectReferenceValue == null)
            {
                Transform bllWater1 = FindChildByName(part1, "BLL_Water");
                if (bllWater1 != null)
                {
                    WaterVisualModule waterModule = bllWater1.GetComponent<WaterVisualModule>();
                    if (waterModule != null)
                    {
                        waterModule1Prop.objectReferenceValue = waterModule;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[Water Module 1]</color> Found: {bllWater1.name}");
                    }
                }
            }

            // ========== AUTO ASSIGN BLOCK 2 (part2) ==========
            if (meshRenderer2Prop.objectReferenceValue == null)
            {
                Transform bllStroke2 = FindChildByName(part2, "BLL_Stroke");
                if (bllStroke2 != null)
                {
                    MeshRenderer renderer = bllStroke2.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        meshRenderer2Prop.objectReferenceValue = renderer;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[Block2 Mesh]</color> Found: {bllStroke2.name}");
                    }
                }
            }

            if (meshGlass2Prop.objectReferenceValue == null)
            {
                Transform bllGlass2 = FindChildByName(part2, "BLL_Glass");
                if (bllGlass2 != null)
                {
                    MeshRenderer renderer = bllGlass2.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        meshGlass2Prop.objectReferenceValue = renderer;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[Block2 Glass]</color> Found: {bllGlass2.name}");
                    }
                }
            }

            if (waterModule2Prop.objectReferenceValue == null)
            {
                Transform bllWater2 = FindChildByName(part2, "BLL_Water");
                if (bllWater2 != null)
                {
                    WaterVisualModule waterModule = bllWater2.GetComponent<WaterVisualModule>();
                    if (waterModule != null)
                    {
                        waterModule2Prop.objectReferenceValue = waterModule;
                        foundAny = true;
                        Debug.Log($"<color=cyan>[Water Module 2]</color> Found: {bllWater2.name}");
                    }
                }
            }

            if (foundAny)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                Debug.Log("<color=green>✓ Auto Find References completed!</color>");
            }
            else
            {
                Debug.LogWarning("No new references found. All references may already be assigned.");
            }
        }

        /// <summary>
        /// Tìm child theo tên chính xác (case-sensitive)
        /// </summary>
        private Transform FindChildByName(Transform parent, string name)
        {
            if (parent == null) return null;

            // Tìm trực tiếp trong children
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
            }

            // Tìm recursive trong children của children
            foreach (Transform child in parent)
            {
                Transform found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void ApplyTestColors(DualBlockVisualBehavior dualBlock)
        {
            if (LevelController.Instance == null)
            {
                Debug.LogError("<color=red>LevelController.Instance is null! Cannot get BlockColorData.</color>");
                return;
            }

            // Get color data
            BlockColorData colorData1 = LevelController.Instance.GetBlockColorData(testColor1);
            BlockColorData colorData2 = LevelController.Instance.GetBlockColorData(testColor2);

            if (colorData1 == null || colorData2 == null)
            {
                Debug.LogError("<color=red>BlockColorData not found for selected colors!</color>");
                return;
            }

            // Apply materials to Block 1
            MeshRenderer mesh1 = meshRenderer1Prop.objectReferenceValue as MeshRenderer;
            MeshRenderer glass1 = meshGlass1Prop.objectReferenceValue as MeshRenderer;
            if (mesh1 != null)
            {
                mesh1.sharedMaterial = colorData1.Material;
                EditorUtility.SetDirty(mesh1);
            }
            if (glass1 != null)
            {
                glass1.sharedMaterial = colorData1.GlassMaterial;
                EditorUtility.SetDirty(glass1);
            }

            // Apply materials to Block 2
            MeshRenderer mesh2 = meshRenderer2Prop.objectReferenceValue as MeshRenderer;
            MeshRenderer glass2 = meshGlass2Prop.objectReferenceValue as MeshRenderer;
            if (mesh2 != null)
            {
                mesh2.sharedMaterial = colorData2.Material;
                EditorUtility.SetDirty(mesh2);
            }
            if (glass2 != null)
            {
                glass2.sharedMaterial = colorData2.GlassMaterial;
                EditorUtility.SetDirty(glass2);
            }

            // Apply water fill
            WaterVisualModule waterModule1 = waterModule1Prop.objectReferenceValue as WaterVisualModule;
            WaterVisualModule waterModule2 = waterModule2Prop.objectReferenceValue as WaterVisualModule;

            if (waterModule1 != null)
            {
                float min1 = water1MinValueProp.floatValue;
                float max1 = water1MaxValueProp.floatValue;
                waterModule1.Init(colorData1, min1, max1);
                waterModule1.SetFillImmediate(testFillPercent1);
                EditorUtility.SetDirty(waterModule1);
            }

            if (waterModule2 != null)
            {
                float min2 = water2MinValueProp.floatValue;
                float max2 = water2MaxValueProp.floatValue;
                waterModule2.Init(colorData2, min2, max2);
                waterModule2.SetFillImmediate(testFillPercent2);
                EditorUtility.SetDirty(waterModule2);
            }

            Debug.Log($"<color=green>✓ Applied colors: {testColor1} & {testColor2} with fill {testFillPercent1:F2} & {testFillPercent2:F2}</color>");
        }

        private void ResetColors(DualBlockVisualBehavior dualBlock)
        {
            // Reset materials
            MeshRenderer mesh1 = meshRenderer1Prop.objectReferenceValue as MeshRenderer;
            MeshRenderer glass1 = meshGlass1Prop.objectReferenceValue as MeshRenderer;
            MeshRenderer mesh2 = meshRenderer2Prop.objectReferenceValue as MeshRenderer;
            MeshRenderer glass2 = meshGlass2Prop.objectReferenceValue as MeshRenderer;

            if (mesh1 != null)
            {
                mesh1.sharedMaterial = null;
                EditorUtility.SetDirty(mesh1);
            }
            if (glass1 != null)
            {
                glass1.sharedMaterial = null;
                EditorUtility.SetDirty(glass1);
            }
            if (mesh2 != null)
            {
                mesh2.sharedMaterial = null;
                EditorUtility.SetDirty(mesh2);
            }
            if (glass2 != null)
            {
                glass2.sharedMaterial = null;
                EditorUtility.SetDirty(glass2);
            }

            // Reset water fill
            WaterVisualModule waterModule1 = waterModule1Prop.objectReferenceValue as WaterVisualModule;
            WaterVisualModule waterModule2 = waterModule2Prop.objectReferenceValue as WaterVisualModule;

            if (waterModule1 != null)
            {
                waterModule1.SetFillImmediate(0f);
                EditorUtility.SetDirty(waterModule1);
            }

            if (waterModule2 != null)
            {
                waterModule2.SetFillImmediate(0f);
                EditorUtility.SetDirty(waterModule2);
            }

            Debug.Log("<color=yellow>Reset all materials and water fills.</color>");
        }
    }
}
#endif