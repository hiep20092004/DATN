using System;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Maps a base <see cref="LevelData"/> (slot in <see cref="LevelDatabase.levels"/>) to alternate layouts and build selection.
    /// </summary>
    [Serializable]
    public class LevelVariantEntry
    {
        [SerializeField] LevelData baseLevel;
        [SerializeField] LevelData[] variants = Array.Empty<LevelData>();
        /// <summary>-1 = ship base level; 0+ = ship <see cref="variants"/>[i].</summary>
        [SerializeField] int activeVariantIndex = -1;

        public LevelData BaseLevel => baseLevel;
        public LevelData[] Variants => variants;
        public int ActiveVariantIndex => activeVariantIndex;

        public void SetBaseLevel(LevelData value) => baseLevel = value;
        public void SetVariants(LevelData[] value) => variants = value ?? Array.Empty<LevelData>();
        public void SetActiveVariantIndex(int value) => activeVariantIndex = value;
    }
}