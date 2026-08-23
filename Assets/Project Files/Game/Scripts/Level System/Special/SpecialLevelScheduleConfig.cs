using System;
using System.Collections.Generic;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class SpecialLevelScheduleEntry
    {
        public int levelUnlock;  // 1-based display level number (e.g. 9 = appears before level 9)
        public SpecialLevelMode mode;
        public int orderIndex;   // 0-based index within this mode's special levels
    }

    /// <summary>
    /// Maps which main level number triggers each special level.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Level/Special Level Schedule Config", fileName = "Special Level Schedule Config")]
    public class SpecialLevelScheduleConfig : ScriptableObject
    {
        [SerializeField] private SpecialLevelScheduleEntry[] entries = Array.Empty<SpecialLevelScheduleEntry>();

        private SpecialLevelScheduleEntry[] ActiveEntries => entries;

        public bool TryGetForNextDisplayLevel(int nextDisplayLevel, out SpecialLevelMode mode, out int orderIndex)
        {
            mode = default;
            orderIndex = -1;

            SpecialLevelScheduleEntry[] active = ActiveEntries;
            if (active == null) return false;

            foreach (SpecialLevelScheduleEntry entry in active)
            {
                if (entry.levelUnlock == nextDisplayLevel)
                {
                    mode = entry.mode;
                    orderIndex = entry.orderIndex;
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void Editor_Rebuild(SpecialLevelData[] specialLevels)
        {
            if (specialLevels == null || specialLevels.Length == 0)
            {
                entries = Array.Empty<SpecialLevelScheduleEntry>();
                UnityEditor.EditorUtility.SetDirty(this);
                return;
            }

            var modeOrderCounters = new Dictionary<SpecialLevelMode, int>();
            var result = new List<SpecialLevelScheduleEntry>();

            foreach (SpecialLevelData specialLevel in specialLevels)
            {
                if (!specialLevel) continue;

                SpecialLevelMode mode = specialLevel.Mode;
                if (!modeOrderCounters.TryGetValue(mode, out int orderIdx))
                    orderIdx = 0;

                if (specialLevel.LevelUnlock > 0)
                {
                    result.Add(new SpecialLevelScheduleEntry
                    {
                        levelUnlock = specialLevel.LevelUnlock,
                        mode = mode,
                        orderIndex = orderIdx,
                    });
                }

                modeOrderCounters[mode] = orderIdx + 1;
            }

            entries = result.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
