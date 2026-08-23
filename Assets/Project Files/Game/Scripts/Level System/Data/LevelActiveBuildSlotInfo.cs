using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// One row in the active-level build plan: which source <see cref="LevelData"/> is copied into
    /// <see cref="LevelSystemUtils.ActiveLevelsFolder"/> for Addressables / player builds.
    /// </summary>
    public readonly struct LevelActiveBuildSlotInfo
    {
        public int SlotNumber1Based { get; }
        public LevelData BaseLevel { get; }
        /// <summary>Resolved layout copied into the active slot (variant or base).</summary>
        public LevelData SourceLevel { get; }
        public string TargetAssetPath { get; }
        /// <summary>-1 = base level; 0+ = variant index in <see cref="LevelVariantEntry.Variants"/>.</summary>
        public int ActiveVariantIndex { get; }

        public LevelActiveBuildSlotInfo(
            int slotNumber1Based,
            LevelData baseLevel,
            LevelData sourceLevel,
            string targetAssetPath,
            int activeVariantIndex)
        {
            SlotNumber1Based = slotNumber1Based;
            BaseLevel = baseLevel;
            SourceLevel = sourceLevel;
            TargetAssetPath = targetAssetPath;
            ActiveVariantIndex = activeVariantIndex;
        }

        public string GetVariantLabel()
        {
            if (ActiveVariantIndex < 0)
                return "Base";
            return $"V{ActiveVariantIndex + 1}";
        }
    }
}
