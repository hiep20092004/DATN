using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "Data/Level/Obstacle Unlock Database", fileName = "Obstacle Unlock Database")]
    public class ObstacleUnlockDatabase : ScriptableObject
    {
        [Sirenix.OdinInspector.ListDrawerSettings(
            ListElementLabelName = "EditorLabel",
            ShowFoldout = true,
            DraggableItems = false)]
        [SerializeField] private List<ObstacleUnlockEntry> entries = new List<ObstacleUnlockEntry>();

        public List<ObstacleUnlockEntry> Entries => entries;

        /// <summary>
        /// Returns obstacle unlock entries whose effect appears in <paramref name="levelData"/>'s
        /// elements. Scanning the level data (instead of relying on a hard-coded level number)
        /// keeps the popup correct when the level is loaded from a remote bundle.
        /// </summary>
        public List<ObstacleUnlockEntry> GetEntriesForLevel(LevelData levelData)
        {
            if (entries == null || entries.Count == 0 || !levelData)
                return new List<ObstacleUnlockEntry>(0);

            HashSet<string> keys = new HashSet<string>();
            ObstacleUnlockScanner.CollectEffectKeys(levelData, keys);

            if (keys.Count == 0)
                return new List<ObstacleUnlockEntry>(0);

            List<ObstacleUnlockEntry> result = new List<ObstacleUnlockEntry>();
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (keys.Contains(entry.GetEffectKey()))
                    result.Add(entry);
            }

            return result;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only persistence: scan <paramref name="loadedLevels"/> via
        /// <see cref="ObstacleUnlockScanner"/>, append entries for newly discovered effects,
        /// and sort. Existing entries (including ones no longer found in any level) and their
        /// icon/title/description are preserved.
        /// </summary>
        public void Editor_GenerateObstacleUnlockData(List<LevelData> loadedLevels)
        {
            Dictionary<string, ObstacleUnlockEntry> discoveredTemplates =
                new Dictionary<string, ObstacleUnlockEntry>();

            if (loadedLevels != null)
            {
                foreach (LevelData levelData in loadedLevels)
                {
                    if (!levelData) continue;
                    ObstacleUnlockScanner.CollectTemplates(levelData, discoveredTemplates);
                }
            }

            var mergedEntries = entries != null
                ? entries.Where(e => e != null).ToList()
                : new List<ObstacleUnlockEntry>();

            HashSet<string> existingKeys = new HashSet<string>();
            foreach (var entry in mergedEntries)
                existingKeys.Add(entry.GetEffectKey());

            foreach (var kvp in discoveredTemplates)
            {
                if (existingKeys.Contains(kvp.Key)) continue;
                mergedEntries.Add(kvp.Value);
            }

            mergedEntries = mergedEntries
                .OrderBy(e => e.Category)
                .ThenBy(e => e.GetEffectKey())
                .ToList();

            entries = mergedEntries;
            EditorUtility.SetDirty(this);

            Debug.Log($"[ObstacleUnlockDatabase] Discovered {discoveredTemplates.Count} obstacle effect(s) " +
                      $"across {loadedLevels?.Count ?? 0} level(s).", this);
        }

        public void Editor_SetEntries(List<ObstacleUnlockEntry> newEntries)
        {
            entries = newEntries ?? new List<ObstacleUnlockEntry>();
            EditorUtility.SetDirty(this);
        }

        public void Editor_CopyEntriesFrom(List<ObstacleUnlockEntry> sourceEntries)
        {
            entries = sourceEntries != null
                ? new List<ObstacleUnlockEntry>(sourceEntries)
                : new List<ObstacleUnlockEntry>();

            EditorUtility.SetDirty(this);
        }

        public void Editor_SortByCategory()
        {
            if (entries == null) return;
            entries = entries
                .Where(e => e != null)
                .OrderBy(e => e.Category)
                .ThenBy(e => e.GetEffectKey())
                .ToList();
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
