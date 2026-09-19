using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the environment materials that the imported prefabs are missing, plus the navy wall set.
/// The board floor materials (<c>M_Inner Tile * AB Test</c>) still point at textures that were never
/// committed, so the playfield renders flat white.
/// </summary>
public static partial class WaterFlowClassicSetup
{
    private const string EnvironmentMaterialsFolder = "Assets/Project Files/Game/Materials/Environment";
    private const string EnvironmentPrefabsFolder = "Assets/Project Files/Game/Prefabs/Environment";
    private const string WallPrefabsFolder = "Assets/BorderArt/Prefabs/Ingame/Wall";
    private const string WallTextureFolder = "Assets/BorderArt/Texture";

    private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
    private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

    /// <summary>Wall mesh shapes shipped in BorderArt — the number is part of both the material and texture name.</summary>
    private static readonly int[] WallShapeIds = { 1, 3, 4, 5, 7 };

    [MenuItem("WaterFlow/Setup/Create Environment Materials")]
    public static void CreateEnvironmentMaterials()
    {
        Shader litShader = Shader.Find(UrpLitShaderName);
        if (!litShader)
        {
            Debug.LogError("[WaterFlow Setup] Shader '" + UrpLitShaderName + "' not found — is URP still installed?");
            return;
        }

        EnsureAssetFolder(EnvironmentMaterialsFolder);

        // Board floor. The original textures are gone, so these are flat colours: a dark recess for the
        // ground plate and two near-identical tones so the tile grid reads without stripes.
        Material ground = EnsureMaterial(litShader, "M_Board Ground", new Color32(0x16, 0x23, 0x3A, 0xFF), 0.08f);
        Material tileA = EnsureMaterial(litShader, "M_Board Tile A", new Color32(0x24, 0x38, 0x4F, 0xFF), 0.12f);
        Material tileB = EnsureMaterial(litShader, "M_Board Tile B", new Color32(0x2B, 0x41, 0x5A, 0xFF), 0.12f);

        // Navy wall set. Same shader setup as the shipped walls (Unlit, so the painted gradient is what
        // you see) — only the atlas changes, which keeps the UV islands lined up with Shape_N.fbx.
        Shader unlitShader = Shader.Find(UrpUnlitShaderName);
        if (!unlitShader)
        {
            Debug.LogError("[WaterFlow Setup] Shader '" + UrpUnlitShaderName + "' not found — is URP still installed?");
            return;
        }

        var wallMaterials = new List<Material>(WallShapeIds.Length);
        foreach (int shapeId in WallShapeIds)
        {
            Material wall = EnsureMaterial(unlitShader, NavyWallMaterialName(shapeId), Color.white, 0f);
            AssignWallTexture(wall, shapeId);
            wallMaterials.Add(wall);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int assigned = AssignBoardMaterials(ground, tileA, tileB);

        Debug.Log("[WaterFlow Setup] Environment materials ready in '" + EnvironmentMaterialsFolder + "': " +
                  "3 board + " + wallMaterials.Count + " navy wall. Board prefabs updated: " + assigned + ". " +
                  "Run WaterFlow/Setup/Apply Navy Wall Materials to switch the wall prefabs over.");
    }

    /// <summary>
    /// Swaps the BorderArt wall prefabs over to the navy materials. Kept separate from material creation
    /// because it rewrites art-owned prefabs, which is a look change, not a fix.
    /// </summary>
    [MenuItem("WaterFlow/Setup/Apply Navy Wall Materials")]
    public static void ApplyNavyWallMaterials()
    {
        var replacements = new Dictionary<Material, Material>();
        var missingPaths = new List<string>();

        foreach (int shapeId in WallShapeIds)
        {
            string sourcePath = "Assets/BorderArt/Material/M_New_Wall " + shapeId + ".mat";
            string navyPath = EnvironmentMaterialsFolder + "/" + NavyWallMaterialName(shapeId) + ".mat";

            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            var navy = AssetDatabase.LoadAssetAtPath<Material>(navyPath);

            if (!source)
                missingPaths.Add(sourcePath);

            if (!navy)
                missingPaths.Add(navyPath);

            if (source && navy)
                replacements[source] = navy;
        }

        if (replacements.Count == 0)
        {
            Debug.LogError("[WaterFlow Setup] No wall material pairs resolved — nothing applied. Missing: " +
                           string.Join(" | ", missingPaths) +
                           ". If the navy materials are the missing side, run " +
                           "WaterFlow/Setup/Create Environment Materials first.");
            return;
        }

        if (missingPaths.Count > 0)
        {
            Debug.LogWarning("[WaterFlow Setup] Applying a partial wall set. Missing: " +
                             string.Join(" | ", missingPaths));
        }

        int touchedPrefabs = 0;
        int swappedSlots = 0;

        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { WallPrefabsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                int swapped = SwapRendererMaterials(root, replacements);
                if (swapped == 0)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                touchedPrefabs++;
                swappedSlots += swapped;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[WaterFlow Setup] Navy wall materials applied: " + swappedSlots + " material slot(s) across " +
                  touchedPrefabs + " prefab(s).");
    }

    private static int SwapRendererMaterials(GameObject root, IReadOnlyDictionary<Material, Material> replacements)
    {
        int swapped = 0;

        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] && replacements.TryGetValue(materials[i], out Material replacement))
                {
                    materials[i] = replacement;
                    changed = true;
                    swapped++;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        return swapped;
    }

    private static int AssignBoardMaterials(Material ground, Material tileA, Material tileB)
    {
        int assigned = 0;
        assigned += AssignSingleMaterial("Inner Tile 1", tileA) ? 1 : 0;
        assigned += AssignSingleMaterial("Inner Tile 2", tileB) ? 1 : 0;
        assigned += AssignSingleMaterial("Border Inner Ground", ground) ? 1 : 0;

        return assigned;
    }

    private static bool AssignSingleMaterial(string prefabName, Material material)
    {
        string path = EnvironmentPrefabsFolder + "/" + prefabName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (!root)
        {
            Debug.LogWarning("[WaterFlow Setup] Could not open '" + path + "'.");
            return false;
        }

        try
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[WaterFlow Setup] '" + prefabName + "' has no MeshRenderer.");
                return false;
            }

            foreach (MeshRenderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string NavyWallMaterialName(int shapeId) => "M_Wall Navy " + shapeId;

    private static void AssignWallTexture(Material material, int shapeId)
    {
        string texturePath = WallTextureFolder + "/Shape_" + shapeId + "_Base_color_Navy.png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (!texture)
        {
            Debug.LogWarning("[WaterFlow Setup] Wall texture '" + texturePath + "' missing — " +
                             material.name + " stays untextured.");
            return;
        }

        material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
    }

    /// <summary>AssetDatabase.CreateAsset needs the folder to be a tracked asset, not just a directory.</summary>
    private static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        int lastSlash = folderPath.LastIndexOf('/');
        string parent = folderPath.Substring(0, lastSlash);
        string leaf = folderPath.Substring(lastSlash + 1);

        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>Creates the material, or retunes the existing one so re-running the menu item is safe.</summary>
    private static Material EnsureMaterial(Shader shader, string materialName, Color baseColor, float smoothness)
    {
        string path = EnvironmentMaterialsFolder + "/" + materialName + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = !material;

        if (isNew)
        {
            material = new Material(shader) { name = materialName };
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetColor("_BaseColor", baseColor);

        // URP/Unlit carries none of the shading properties below, and writing them anyway would leave
        // stray keywords on the material.
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

            // These are matte props; highlights on them only read as noise once the water and blocks
            // are moving over the top. URP gates both off the keyword, not the float, so setting the
            // float alone would leave them on.
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        if (isNew)
        {
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        return material;
    }
}
