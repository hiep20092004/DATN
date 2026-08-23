using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FlowOut.EditorTools
{
    public static class DucMaterialRepairTool
    {
        private const string MATERIAL_FOLDER = "Assets/DUC/Assets/Material";

        private static readonly string[] TextureProperties =
        {
            "_MainTex",
            "_BaseMap",
            "_AlbedoMap",
            "_Albedo",
            "_Texture",
            "_Tex",
            "_SpriteTexture",
        };

        private static readonly string[] ColorProperties =
        {
            "_Color",
            "_BaseColor",
            "_AlbedoColor",
            "_TintColor",
            "_EmissionColor",
        };

        [MenuItem("Tools/DUC/Repair Materials")]
        public static void RepairDucMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
            {
                Debug.LogError($"DUC material folder not found: {MATERIAL_FOLDER}");
                return;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MATERIAL_FOLDER });
            int changedCount = 0;
            int skippedCount = 0;
            int missingShaderCount = 0;
            Dictionary<string, int> remapCounts = new Dictionary<string, int>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string materialGuid in materialGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(materialGuid);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    string oldShaderName = material.shader != null ? material.shader.name : string.Empty;
                    string targetShaderName = ResolveTargetShader(material, path, oldShaderName);
                    if (string.IsNullOrEmpty(targetShaderName) || oldShaderName == targetShaderName)
                    {
                        skippedCount++;
                        continue;
                    }

                    Shader targetShader = Shader.Find(targetShaderName);
                    if (targetShader == null)
                    {
                        missingShaderCount++;
                        Debug.LogWarning($"Missing target shader '{targetShaderName}' for material '{path}' from '{oldShaderName}'.");
                        continue;
                    }

                    Texture mainTexture = FindFirstTexture(material);
                    Color mainColor = FindFirstColor(material);
                    float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
                    bool transparent = ShouldUseTransparentSurface(material.name, oldShaderName, path);

                    material.shader = targetShader;
                    ApplyCommonProperties(material, mainTexture, mainColor, cutoff, transparent);

                    EditorUtility.SetDirty(material);
                    changedCount++;

                    string key = $"{oldShaderName} -> {targetShaderName}";
                    remapCounts.TryGetValue(key, out int currentCount);
                    remapCounts[key] = currentCount + 1;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (KeyValuePair<string, int> remapCount in remapCounts)
            {
                Debug.Log($"DUC material remap: {remapCount.Value} x {remapCount.Key}");
            }

            Debug.Log($"DUC material repair finished. Changed: {changedCount}, skipped: {skippedCount}, missing target shaders: {missingShaderCount}.");
        }

        private static string ResolveTargetShader(Material material, string path, string oldShaderName)
        {
            if (IsKnownGoodTargetShader(oldShaderName))
            {
                return oldShaderName;
            }

            string materialName = material.name;
            string combined = $"{oldShaderName} {materialName} {Path.GetFileName(path)}";

            if (Contains(combined, "SkeletonGraphic"))
            {
                if (Contains(combined, "Tint Black") || Contains(combined, "TintBlack"))
                {
                    if (Contains(combined, "Screen"))
                    {
                        return FirstAvailableShader("Spine/SkeletonGraphic Tint Black Screen", "Spine/SkeletonGraphic Tint Black", "Spine/SkeletonGraphic");
                    }

                    if (Contains(combined, "Multiply"))
                    {
                        return FirstAvailableShader("Spine/SkeletonGraphic Tint Black Multiply", "Spine/SkeletonGraphic Tint Black", "Spine/SkeletonGraphic");
                    }

                    if (Contains(combined, "Additive"))
                    {
                        return FirstAvailableShader("Spine/SkeletonGraphic Tint Black Additive", "Spine/SkeletonGraphic Tint Black", "Spine/SkeletonGraphic");
                    }

                    return FirstAvailableShader("Spine/SkeletonGraphic Tint Black", "Spine/SkeletonGraphic");
                }

                if (Contains(combined, "Screen"))
                {
                    return FirstAvailableShader("Spine/SkeletonGraphic Screen", "Spine/SkeletonGraphic");
                }

                if (Contains(combined, "Multiply"))
                {
                    return FirstAvailableShader("Spine/SkeletonGraphic Multiply", "Spine/SkeletonGraphic");
                }

                if (Contains(combined, "Additive"))
                {
                    return FirstAvailableShader("Spine/SkeletonGraphic Additive", "Spine/SkeletonGraphic");
                }

                return "Spine/SkeletonGraphic";
            }

            if (Contains(combined, "Spine"))
            {
                if (Contains(combined, "Additive"))
                {
                    return FirstAvailableShader("Universal Render Pipeline/Spine/Blend Modes/Skeleton Additive", "Universal Render Pipeline/Spine/Skeleton");
                }

                if (Contains(combined, "Multiply"))
                {
                    return FirstAvailableShader("Universal Render Pipeline/Spine/Blend Modes/Skeleton Multiply", "Universal Render Pipeline/Spine/Skeleton");
                }

                if (Contains(combined, "Screen"))
                {
                    return FirstAvailableShader("Universal Render Pipeline/Spine/Blend Modes/Skeleton Screen", "Universal Render Pipeline/Spine/Skeleton");
                }

                if (Contains(combined, "Sprite"))
                {
                    return FirstAvailableShader("Universal Render Pipeline/Spine/Sprite", "Universal Render Pipeline/Spine/Skeleton");
                }

                return FirstAvailableShader("Universal Render Pipeline/Spine/Skeleton", "Universal Render Pipeline/Spine/Sprite");
            }

            if (Contains(combined, "TextMeshPro") || Contains(combined, "TMP") || Contains(combined, "SDF"))
            {
                return FirstAvailableShader("TextMeshPro/Distance Field", "TextMeshPro/Mobile/Distance Field", "Universal Render Pipeline/Unlit");
            }

            if (Contains(combined, "UI_") || Contains(combined, "UI/") || Contains(combined, "RoundedCorners"))
            {
                return FirstAvailableShader("UI/Default", "Universal Render Pipeline/Unlit");
            }

            if (Contains(combined, "Particle") || Contains(combined, "_ADD") || Contains(combined, " Additive") ||
                Contains(combined, "Spark") || Contains(combined, "Confetti") || Contains(combined, "Glow"))
            {
                return FirstAvailableShader("Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit");
            }

            if (Contains(combined, "Sprite") || Contains(combined, "_AB") || Contains(combined, "alpha") ||
                Contains(combined, "cloud") || Contains(combined, "circle") || Contains(combined, "triangle") ||
                Contains(combined, "snowflake") || Contains(combined, "star"))
            {
                return FirstAvailableShader("Universal Render Pipeline/2D/Sprite-Unlit-Default", "Universal Render Pipeline/Unlit");
            }

            if (Contains(oldShaderName, "Standard") || Contains(oldShaderName, "Diffuse") ||
                Contains(materialName, "_Shd") || Contains(materialName, "Piller") || Contains(materialName, "Valve"))
            {
                return FirstAvailableShader("Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit");
            }

            return FirstAvailableShader("Universal Render Pipeline/Unlit", "Universal Render Pipeline/Lit");
        }

        private static bool IsKnownGoodTargetShader(string shaderName)
        {
            return shaderName == "Universal Render Pipeline/Unlit" ||
                   shaderName == "Universal Render Pipeline/Lit" ||
                   shaderName == "Universal Render Pipeline/Particles/Unlit" ||
                   shaderName == "Universal Render Pipeline/2D/Sprite-Unlit-Default" ||
                   shaderName == "Universal Render Pipeline/Spine/Skeleton" ||
                   shaderName == "Universal Render Pipeline/Spine/Blend Modes/Skeleton Additive" ||
                   shaderName == "Universal Render Pipeline/Spine/Blend Modes/Skeleton Multiply" ||
                   shaderName == "Universal Render Pipeline/Spine/Blend Modes/Skeleton Screen" ||
                   shaderName == "Spine/SkeletonGraphic" ||
                   shaderName == "Spine/SkeletonGraphic Additive" ||
                   shaderName == "Spine/SkeletonGraphic Multiply" ||
                   shaderName == "Spine/SkeletonGraphic Screen" ||
                   shaderName == "Spine/SkeletonGraphic Tint Black" ||
                   shaderName == "Spine/SkeletonGraphic Tint Black Additive" ||
                   shaderName == "Spine/SkeletonGraphic Tint Black Multiply" ||
                   shaderName == "Spine/SkeletonGraphic Tint Black Screen";
        }

        private static void ApplyCommonProperties(Material material, Texture mainTexture, Color mainColor, float cutoff, bool transparent)
        {
            SetTextureIfPresent(material, "_MainTex", mainTexture);
            SetTextureIfPresent(material, "_BaseMap", mainTexture);
            SetTextureIfPresent(material, "_AlbedoMap", mainTexture);

            SetColorIfPresent(material, "_Color", mainColor);
            SetColorIfPresent(material, "_BaseColor", mainColor);
            SetColorIfPresent(material, "_AlbedoColor", mainColor);

            SetFloatIfPresent(material, "_Cutoff", cutoff);
            SetFloatIfPresent(material, "_AlphaCutoff", cutoff);

            if (!transparent)
            {
                return;
            }

            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private static Texture FindFirstTexture(Material material)
        {
            foreach (string propertyName in TextureProperties)
            {
                if (material.HasProperty(propertyName))
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static Color FindFirstColor(Material material)
        {
            foreach (string propertyName in ColorProperties)
            {
                if (material.HasProperty(propertyName))
                {
                    return material.GetColor(propertyName);
                }
            }

            return Color.white;
        }

        private static bool ShouldUseTransparentSurface(string materialName, string shaderName, string path)
        {
            string combined = $"{materialName} {shaderName} {Path.GetFileName(path)}";
            return Contains(combined, "_ADD") ||
                   Contains(combined, "_AB") ||
                   Contains(combined, "Alpha") ||
                   Contains(combined, "Transparent") ||
                   Contains(combined, "Sprite") ||
                   Contains(combined, "Spine") ||
                   Contains(combined, "Particle") ||
                   Contains(combined, "UI") ||
                   Contains(combined, "TMP") ||
                   Contains(combined, "TextMeshPro") ||
                   Contains(combined, "SDF");
        }

        private static string FirstAvailableShader(params string[] shaderNames)
        {
            foreach (string shaderName in shaderNames)
            {
                if (Shader.Find(shaderName) != null)
                {
                    return shaderName;
                }
            }

            return shaderNames.Length > 0 ? shaderNames[shaderNames.Length - 1] : string.Empty;
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool Contains(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
