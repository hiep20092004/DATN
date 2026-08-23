using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Editor-only container for level test assets.
    /// Stored under an Editor folder so it is excluded from runtime builds.
    /// </summary>
    public sealed class SpecialLevelsEditorStore : ScriptableObject
    {
        [SerializeField] private LevelData[] testLevels;

        public LevelData[] TestLevels => testLevels;
    }
}
