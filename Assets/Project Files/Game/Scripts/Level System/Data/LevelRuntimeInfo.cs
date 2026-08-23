using System;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Lightweight per-level facts (type, randomizer eligibility) baked at build time so runtime
    /// queries (Home play-button visual, randomizer mapping) never need to load the full
    /// <see cref="LevelData"/> asset from Addressables.
    /// </summary>
    [Serializable]
    public struct LevelRuntimeInfo
    {
        [SerializeField] LevelType type;
        [SerializeField] bool useInRandomizer;

        public LevelType Type => type;
        public bool UseInRandomizer => useInRandomizer;

        public LevelRuntimeInfo(LevelType type, bool useInRandomizer)
        {
            this.type = type;
            this.useInRandomizer = useInRandomizer;
        }
    }
}
