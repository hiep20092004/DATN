using System;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Per-mode cutoff used by <see cref="LevelJsonAddressableBuilder"/> to delete special level assets
    /// whose order index exceeds <see cref="MaxLevel"/> from <see cref="LevelSystemUtils.SpecialActiveLevelsFolder"/>
    /// before building Addressables.
    /// </summary>
    [Serializable]
    public sealed class SpecialLevelModeMaxConfig
    {
        [SerializeField] private SpecialLevelMode mode = SpecialLevelMode.GoldMode;
        [SerializeField, Tooltip("-1 = no limit. Special level assets with order index > MaxLevel will be removed from the Active folder during build.")]
        private int maxLevel = -1;

        public SpecialLevelMode Mode => mode;
        public int MaxLevel => maxLevel;
    }
}
