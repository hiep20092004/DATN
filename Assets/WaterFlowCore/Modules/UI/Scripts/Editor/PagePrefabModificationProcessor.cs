using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WaterFlow.Core
{
    // public class PagePrefabModificationProcessor : AssetModificationProcessor
    // {
    //     private const string PREFAB_ENDING = ".prefab";
    //
    //     private static bool busy;
    //
    //     private static string[] OnWillSaveAssets(string[] paths)
    //     {
    //         if (Application.isBatchMode) return paths;
    //         if (busy) return paths;
    //
    //         try
    //         {
    //             busy = true;
    //
    //             foreach (string path in paths)
    //             {
    //                 if (!path.EndsWith(PREFAB_ENDING, StringComparison.OrdinalIgnoreCase))
    //                     continue;
    //
    //                 if (AssetDatabase.LoadMainAssetAtPath(path) == null)
    //                     continue;
    //
    //                 PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
    //                 if (stage != null && string.Equals(stage.assetPath, path, StringComparison.OrdinalIgnoreCase))
    //                     continue;
    //
    //                 GameObject root = null;
    //                 try
    //                 {
    //                     try
    //                     {
    //                         root = PrefabUtility.LoadPrefabContents(path);
    //                     }
    //                     catch (ArgumentException ex)
    //                     {
    //                         Debug.LogWarning($"Could not load prefab contents for path `{path}`: {ex.Message}");
    //                         continue;
    //                     }
    //
    //                     if (root == null) continue;
    //
    //                     UIPage page = root.GetComponent<UIPage>();
    //                     if (page == null) continue;
    //
    //                     if (page.OnPrefabSaving())
    //                     {
    //                         PrefabUtility.SaveAsPrefabAsset(root, path);
    //                     }
    //                 }
    //                 finally
    //                 {
    //                     if (root != null)
    //                         PrefabUtility.UnloadPrefabContents(root);
    //                 }
    //             }
    //         }
    //         finally
    //         {
    //             busy = false;
    //         }
    //
    //         return paths;
    //     }
    // }
}
