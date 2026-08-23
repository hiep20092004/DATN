#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Editor-only validation for <see cref="SpecialLevelData"/> assets referenced by <see cref="LevelDatabase"/>.
    /// Checks the common consecutive-naming rule per mode and dispatches per-mode rule sets.
    /// </summary>
    public static class SpecialLevelValidator
    {
        public static bool Validate(SpecialLevelData[] specialLevels, Object context = null)
        {
            if (specialLevels == null || specialLevels.Length == 0)
                return true;

            bool allValid = true;
            Dictionary<SpecialLevelMode, List<SpecialLevelData>> perMode =
                new Dictionary<SpecialLevelMode, List<SpecialLevelData>>();

            for (int i = 0; i < specialLevels.Length; i++)
            {
                SpecialLevelData level = specialLevels[i];
                if (!level)
                {
                    Debug.LogError($"[SpecialLevelValidator] specialLevels[{i}] is null/missing.", context);
                    allValid = false;
                    continue;
                }

                if (!perMode.TryGetValue(level.Mode, out List<SpecialLevelData> list))
                {
                    list = new List<SpecialLevelData>();
                    perMode[level.Mode] = list;
                }

                list.Add(level);
            }

            foreach (KeyValuePair<SpecialLevelMode, List<SpecialLevelData>> pair in perMode)
            {
                if (!ValidateConsecutive(pair.Key, pair.Value))
                    allValid = false;

                if (!ValidateModeRules(pair.Key, pair.Value))
                    allValid = false;
            }

            return allValid;
        }

        /// <summary>Common rule: assets for a mode must be named consecutively "{Mode} 001..N" with no gaps.</summary>
        private static bool ValidateConsecutive(SpecialLevelMode mode, List<SpecialLevelData> levels)
        {
            if (levels == null || levels.Count == 0)
                return true;

            bool valid = true;
            for (int i = 0; i < levels.Count; i++)
            {
                int expectedOrderNumber = i + 1;
                string expectedAssetName = $"{mode} {expectedOrderNumber:D3}";
                SpecialLevelData level = levels[i];
                if (level.name != expectedAssetName)
                {
                    Debug.LogError(
                        $"[SpecialLevelValidator] Special level naming gap for mode '{mode}': expected '{expectedAssetName}' " +
                        $"but found '{level.name}' at order #{expectedOrderNumber}. Special levels must be consecutive 001→N per mode.",
                        level);
                    valid = false;
                }
            }

            return valid;
        }

        /// <summary>Per-mode rule hook. Extend each branch with mode-specific checks (config sanity, target counts, etc.).</summary>
        private static bool ValidateModeRules(SpecialLevelMode mode, List<SpecialLevelData> levels)
        {
            switch (mode)
            {
                case SpecialLevelMode.GoldMode:
                    return ValidateGoldMode(levels);
                case SpecialLevelMode.RescueColor:
                    return ValidateRescueColorMode(levels);
                case SpecialLevelMode.RescueBlock:
                    return ValidateRescueBlockMode(levels);
                default:
                    return true;
            }
        }

        private static bool ValidateGoldMode(List<SpecialLevelData> levels)
        {
            // Placeholder for GoldMode-specific rules.
            return true;
        }

        private static bool ValidateRescueColorMode(List<SpecialLevelData> levels)
        {
            bool valid = true;
            for (int i = 0; i < levels.Count; i++)
            {
                SpecialLevelData level = levels[i];
                RescueColorConfig config = level.RescueColorConfig;
                if (config == null || config.targets == null || config.targets.Count == 0)
                {
                    Debug.LogError(
                        $"[SpecialLevelValidator] RescueColor level '{level.name}' has no rescue targets configured.",
                        level);
                    valid = false;
                    continue;
                }

                for (int t = 0; t < config.targets.Count; t++)
                {
                    RescueColorTarget target = config.targets[t];
                    if (target == null || target.requiredCount <= 0 || target.color == BlockColor.None)
                    {
                        Debug.LogError(
                            $"[SpecialLevelValidator] RescueColor level '{level.name}' has invalid target[{t}]: color={target?.color}, requiredCount={target?.requiredCount}.",
                            level);
                        valid = false;
                    }
                }
            }

            return valid;
        }

        private static bool ValidateRescueBlockMode(List<SpecialLevelData> levels)
        {
            bool valid = true;
            for (int i = 0; i < levels.Count; i++)
            {
                SpecialLevelData level = levels[i];
                RescueBlockConfig config = level.RescueBlockConfig;
                if (config == null || config.markerEffect == BlockEffectType.None)
                {
                    Debug.LogError(
                        $"[SpecialLevelValidator] RescueBlock level '{level.name}' has no marker effect configured.",
                        level);
                    valid = false;
                }
            }

            return valid;
        }
    }
}
#endif
