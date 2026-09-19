using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if using_addressable
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif
using UnityEngine;

namespace WaterFlow.Game
{
    public static class LevelAddressableBuilder
    {
        private const string MainGroupName = "LevelActive";
        private const string ActiveLevelsFolder = LevelSystemUtils.ActiveLevelsFolder;
        private const string SlotAssetPrefix = "Level ";

        #region Menu Items

        [MenuItem("Tools/Level Build/1. Prepare Build")]
        public static void ProcessPreBuild()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[LevelActiveBuilder] No LevelDatabase found.");
                return;
            }

            EnsureActiveLevelsFolder();

            var processedPaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
                if (db == null)
                    continue;

                LevelActiveBuildSlotInfo[] plan = db.ComputeActiveBuildSlots();

                var keptSlotPaths = new HashSet<string>();

                foreach (LevelActiveBuildSlotInfo slot in plan)
                {
                    LevelData slotAsset = CopyToActiveSlotAsset(slot.SourceLevel, slot.SlotNumber1Based);
                    if (slotAsset == null)
                        continue;

                    string slotPath = AssetDatabase.GetAssetPath(slotAsset);
                    keptSlotPaths.Add(slotPath);
                }

                RemoveStaleActiveSlotAssets(keptSlotPaths);

                BakeRuntimeLevelInfos(db, plan);

                Debug.Log(
                    $"[LevelActiveBuilder] Prepared {keptSlotPaths.Count} active LevelData asset(s) in '{ActiveLevelsFolder}' for '{db.name}'.");

                EditorUtility.SetDirty(db);
                processedPaths.Add(path);
            }

            if (processedPaths.Count > 0)
            {
                AssetDatabase.SaveAssets();
                ImportGeneratedActiveLevelAssets();

#if using_addressable
                RegisterAddressableEntries();
#endif
            }
        }

#if using_addressable
        [MenuItem("Tools/Level Build/2. Build Addressables")]
        public static void BuildAddressables()
        {
            ProcessPreBuild();

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (!string.IsNullOrEmpty(result.Error))
                Debug.LogError($"[LevelActiveBuilder] Build failed: {result.Error}");
            else
                Debug.Log($"[LevelActiveBuilder] Build succeeded. Output: {result.OutputPath}");
        }
#endif

        [MenuItem("Tools/Level Build/Cleanup All")]
        private static void MenuCleanup()
        {
            if (!EditorUtility.DisplayDialog(
                    "Cleanup Active Levels",
                    "Delete all generated active level assets and Addressable entries?",
                    "OK", "Cancel"))
                return;

            CleanupActiveLevelAssets();
#if using_addressable
            CleanupAddressableEntries();
#endif
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LevelActiveBuilder] Cleanup complete.");
        }

        [MenuItem("Tools/Level Build/Migrate Source Levels To Editor Folder")]
        private static void MigrateSourceLevelsToEditorFolder()
        {
            const string legacyLevelsFolder = LevelSystemUtils.LegacySourceLevelsFolder;
            const string sourceLevelsFolder = LevelSystemUtils.SourceLevelsFolder;

            if (!AssetDatabase.IsValidFolder(legacyLevelsFolder))
            {
                Debug.LogWarning($"[LevelActiveBuilder] Legacy source levels folder not found: {legacyLevelsFolder}");
                return;
            }

            if (AssetDatabase.IsValidFolder(sourceLevelsFolder))
            {
                if (!IsAssetFolderEmpty(sourceLevelsFolder))
                {
                    Debug.LogWarning(
                        $"[LevelActiveBuilder] Target source levels folder already exists: {sourceLevelsFolder}. " +
                        "Merge or remove it before migrating.");
                    return;
                }

                AssetDatabase.DeleteAsset(sourceLevelsFolder);
            }

            EnsureAssetFolder(Path.GetDirectoryName(sourceLevelsFolder).Replace('\\', '/'));

            string error = AssetDatabase.MoveAsset(legacyLevelsFolder, sourceLevelsFolder);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[LevelActiveBuilder] Failed to migrate source levels: {error}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[LevelActiveBuilder] Migrated source levels to editor-only folder: {sourceLevelsFolder}");
        }

        #endregion

        #region Addressable

#if using_addressable
        internal static void LogErrorIfAddressablesNotBuiltWithPlayer()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null &&
                settings.BuildAddressablesWithPlayerBuild ==
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer)
            {
                Debug.LogError(
                    "[LevelActiveBuilder] Addressables \"Build Addressables on Player Build\" is disabled. " +
                    "Run Tools/Level Build/Build Addressables after Prepare Build, or enable that option in Addressable Asset Settings.");
            }
        }

        private static void RegisterAddressableEntries()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[LevelActiveBuilder] Addressable Asset Settings not found.");
                return;
            }

            RegisterAddressableFolder(settings, MainGroupName, ActiveLevelsFolder);
            ImportGeneratedActiveLevelAssets();
        }

        private static void CleanupAddressableEntries()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            CleanupAddressableGroupEntries(settings, MainGroupName);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static void RegisterAddressableFolder(AddressableAssetSettings settings, string groupName, string folderPath)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName)
                                          ?? settings.CreateGroup(groupName, false, false, true,
                                              null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), folderPath);
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"[LevelActiveBuilder] Folder not found: {fullPath}");
                return;
            }

            var existingEntries = new List<AddressableAssetEntry>(group.entries);
            foreach (AddressableAssetEntry entry in existingEntries)
                settings.RemoveAssetEntry(entry.guid);

            string folderGuid = AssetDatabase.AssetPathToGUID(folderPath);
            if (string.IsNullOrEmpty(folderGuid))
            {
                Debug.LogError($"[LevelActiveBuilder] Folder GUID not found for: {folderPath}");
                return;
            }

            AddressableAssetEntry folderEntry = settings.CreateOrMoveEntry(folderGuid, group);
            folderEntry.address = folderPath;

            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.SaveAssetIfDirty(group);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"[LevelActiveBuilder] Registered folder entry '{folderPath}' in group '{groupName}'.");
        }

        private static void CleanupAddressableGroupEntries(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
                return;

            var entries = new List<AddressableAssetEntry>(group.entries);
            foreach (AddressableAssetEntry entry in entries)
                settings.RemoveAssetEntry(entry.guid);

            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(group);
        }
#endif

        #endregion

        #region Helpers

        private static void EnsureActiveLevelsFolder()
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), ActiveLevelsFolder);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            assetFolderPath = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            string parent = Path.GetDirectoryName(assetFolderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(assetFolderPath);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static bool IsAssetFolderEmpty(string assetFolderPath)
        {
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), assetFolderPath);
            return Directory.Exists(fullPath) &&
                   Directory.GetFiles(fullPath).Length == 0 &&
                   Directory.GetDirectories(fullPath).Length == 0;
        }

        internal static void ImportGeneratedActiveLevelAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string fullFolderPath = Path.Combine(Application.dataPath.Replace("Assets", ""), ActiveLevelsFolder);
            if (!Directory.Exists(fullFolderPath))
                return;

            foreach (string file in Directory.GetFiles(fullFolderPath, "*" + LevelSystemUtils.LevelDataAssetExtension, SearchOption.AllDirectories))
            {
                string relativePath = file.Replace('\\', '/')
                    .Replace(Application.dataPath.Replace('\\', '/') + "/", "Assets/");
                string assetPath = relativePath;
                AssetDatabase.ImportAsset(assetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log("[LevelActiveBuilder] Active level assets imported (synchronous).");
        }

        /// <summary>
        /// Bakes per-level <see cref="LevelRuntimeInfo"/> (type, randomizer eligibility) into the
        /// database so runtime queries never synchronously load LevelData from Addressables.
        /// Indexed by slot number so it stays aligned with the active slot assets built above.
        /// </summary>
        private static void BakeRuntimeLevelInfos(LevelDatabase database, LevelActiveBuildSlotInfo[] plan)
        {
            int maxSlotNumber = 0;
            foreach (LevelActiveBuildSlotInfo slot in plan)
            {
                if (slot.SourceLevel && slot.SlotNumber1Based > maxSlotNumber)
                    maxSlotNumber = slot.SlotNumber1Based;
            }

            var infos = new LevelRuntimeInfo[maxSlotNumber];
            foreach (LevelActiveBuildSlotInfo slot in plan)
            {
                if (slot.SourceLevel)
                    infos[slot.SlotNumber1Based - 1] =
                        new LevelRuntimeInfo(slot.SourceLevel.Type, slot.SourceLevel.UseInRandomizer);
            }

            database.Editor_SetRuntimeLevelInfos(infos);
        }

        private static LevelData CopyToActiveSlotAsset(LevelData source, int levelNumber1Based)
        {
            if (!source)
                return null;

            string assetPath = LevelSystemUtils.GetActiveLevelSlotAssetPath(levelNumber1Based);
            LevelData slotAsset = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
            if (slotAsset == null)
            {
                slotAsset = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(slotAsset, assetPath);
            }

            EditorUtility.CopySerialized(source, slotAsset);
            slotAsset.name = SlotAssetPrefix + levelNumber1Based.ToString("D3");
            EditorUtility.SetDirty(slotAsset);
            return slotAsset;
        }

        private static void RemoveStaleActiveSlotAssets(HashSet<string> keptAssetPaths)
        {
            string fullFolderPath = Path.Combine(Application.dataPath.Replace("Assets", ""), ActiveLevelsFolder);
            if (!Directory.Exists(fullFolderPath))
                return;

            foreach (string file in Directory.GetFiles(fullFolderPath, "*" + LevelSystemUtils.LevelDataAssetExtension))
            {
                string fileName = Path.GetFileName(file);
                string assetPath = $"{ActiveLevelsFolder}/{fileName}";
                if (keptAssetPaths.Contains(assetPath))
                    continue;

                if (AssetDatabase.DeleteAsset(assetPath))
                    continue;

                File.Delete(file);
                string metaPath = file + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
            }
        }

        private static void CleanupActiveLevelAssets()
        {
            string fullFolderPath = Path.Combine(Application.dataPath.Replace("Assets", ""), ActiveLevelsFolder);
            if (!Directory.Exists(fullFolderPath))
                return;

            foreach (string file in Directory.GetFiles(fullFolderPath))
            {
                string fileName = Path.GetFileName(file);
                string assetPath = $"{ActiveLevelsFolder}/{fileName}";
                if (AssetDatabase.DeleteAsset(assetPath))
                    continue;

                File.Delete(file);
            }
        }

        #endregion
    }

#if using_addressable && UNITY_2021_2_OR_NEWER
    internal sealed class LevelJsonBuildPlayerProcessor : BuildPlayerProcessor
    {
        public override int callbackOrder => -3000;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            LevelAddressableBuilder.LogErrorIfAddressablesNotBuiltWithPlayer();
            Debug.Log(
                "[LevelActiveBuilder] BuildPlayerProcessor (early): preparing active LevelData assets + addressable entries.");
            LevelAddressableBuilder.ProcessPreBuild();
        }
    }
#endif
}
