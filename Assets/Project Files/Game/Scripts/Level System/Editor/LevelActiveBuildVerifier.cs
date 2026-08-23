using System.Text;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Editor tool: dry-run report of which levels would be copied into
    /// <see cref="LevelSystemUtils.ActiveLevelsFolder"/> when running Prepare Build.
    /// </summary>
    public static class LevelActiveBuildVerifier
    {
        [MenuItem("Tools/Level Build/Verify Active Level Plan")]
        public static void VerifyAllDatabases()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[LevelActiveBuildVerifier] No LevelDatabase found.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("=== Active Level Build Plan (dry-run) ===");
            report.AppendLine($"Max cap uses LevelGeneralConfigData.MaxLevel; target folder: {LevelSystemUtils.ActiveLevelsFolder}");
            report.AppendLine();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
                if (!db)
                    continue;

                AppendDatabaseReport(report, db, path);
            }

            string text = report.ToString();
            Debug.Log(text);
            EditorUtility.DisplayDialog("Active Level Build Plan", text, "OK");
        }

        [MenuItem("Tools/Level Build/Verify Active Level Plan", true)]
        private static bool VerifyAllDatabasesValidate()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static void AppendDatabaseReport(StringBuilder report, LevelDatabase db, string assetPath)
        {
            LevelActiveBuildSlotInfo[] plan = db.ComputeActiveBuildSlots();

            report.AppendLine($"--- {db.name} ({assetPath}) ---");
            report.AppendLine($"Slots in plan: {plan.Length} (GetMaxLevel={db.GetMaxLevel()}, AmountOfLevels={db.AmountOfLevels})");
            report.AppendLine("Slot | Variant | Base asset | Source asset (packed) | Target");
            report.AppendLine("-----|---------|------------|----------------------|-------");

            foreach (LevelActiveBuildSlotInfo slot in plan)
            {
                string basePath = slot.BaseLevel ? AssetDatabase.GetAssetPath(slot.BaseLevel) : "(null)";
                string sourcePath = slot.SourceLevel ? AssetDatabase.GetAssetPath(slot.SourceLevel) : "(null)";
                string baseName = slot.BaseLevel ? slot.BaseLevel.name : "?";
                string sourceName = slot.SourceLevel ? slot.SourceLevel.name : "?";

                report.AppendLine(
                    $"{slot.SlotNumber1Based,4} | {slot.GetVariantLabel(),7} | {baseName} | {sourceName} | {slot.TargetAssetPath}");
                if (basePath != sourcePath)
                {
                    report.AppendLine($"       base:   {basePath}");
                    report.AppendLine($"       source: {sourcePath}");
                }
            }

            report.AppendLine();
        }

        /// <summary>
        /// After Prepare Build, checks that each active slot asset exists and matches the current plan source.
        /// </summary>
        [MenuItem("Tools/Level Build/Verify Active Folder Matches Plan")]
        public static void VerifyActiveFolderMatchesPlan()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[LevelActiveBuildVerifier] No LevelDatabase found.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("=== ActiveLevels folder vs build plan ===");
            int mismatchCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
                if (!db)
                    continue;

                LevelActiveBuildSlotInfo[] plan = db.ComputeActiveBuildSlots();
                report.AppendLine($"--- {db.name} ---");

                foreach (LevelActiveBuildSlotInfo slot in plan)
                {
                    LevelData slotAsset = AssetDatabase.LoadAssetAtPath<LevelData>(slot.TargetAssetPath);
                    if (!slotAsset)
                    {
                        report.AppendLine($"MISSING: slot {slot.SlotNumber1Based} -> {slot.TargetAssetPath}");
                        mismatchCount++;
                        continue;
                    }

                    if (!slot.SourceLevel)
                        continue;

                    if (!SlotContentMatchesSource(slotAsset, slot.SourceLevel))
                    {
                        string expected = AssetDatabase.GetAssetPath(slot.SourceLevel);
                        report.AppendLine(
                            $"MISMATCH: slot {slot.SlotNumber1Based} ({slot.TargetAssetPath}) " +
                            $"does not match expected source '{slot.SourceLevel.name}' ({expected})");
                        mismatchCount++;
                    }
                }

                report.AppendLine();
            }

            string text = report.ToString();
            if (mismatchCount == 0)
            {
                text += "All checked slots OK (or folder empty — run Prepare Build first).";
                Debug.Log(text);
                EditorUtility.DisplayDialog("Active Folder Check", text, "OK");
            }
            else
            {
                Debug.LogWarning(text);
                EditorUtility.DisplayDialog("Active Folder Check", text, "OK");
            }
        }

        private static bool SlotContentMatchesSource(LevelData slotAsset, LevelData expectedSource)
        {
            if (!slotAsset || !expectedSource)
                return false;

            string slotJson = EditorJsonUtility.ToJson(slotAsset, false);
            string sourceJson = EditorJsonUtility.ToJson(expectedSource, false);
            return slotJson == sourceJson;
        }
    }
}
