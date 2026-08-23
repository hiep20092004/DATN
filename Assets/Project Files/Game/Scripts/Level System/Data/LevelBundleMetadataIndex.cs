using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Per-bundle index of <see cref="LevelRuntimeInfo"/> shipped inside a remote level AssetBundle.
    /// When a remote bundle overrides levels, this index overrides the metadata baked into
    /// <see cref="LevelDatabase"/> so type/randomizer queries stay consistent with the remote content.
    /// Built by the Build Asset Bundle tool; one asset per bundle.
    /// </summary>
    public class LevelBundleMetadataIndex : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField] int levelNumber;
            [SerializeField] LevelRuntimeInfo info;

            public int LevelNumber => levelNumber;
            public LevelRuntimeInfo Info => info;

            public Entry(int levelNumber, LevelRuntimeInfo info)
            {
                this.levelNumber = levelNumber;
                this.info = info;
            }
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public bool TryGet(int levelNumber1Based, out LevelRuntimeInfo info)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && entry.LevelNumber == levelNumber1Based)
                {
                    info = entry.Info;
                    return true;
                }
            }

            info = default;
            return false;
        }

#if UNITY_EDITOR
        public void Editor_SetEntries(List<Entry> newEntries)
        {
            entries = newEntries ?? new List<Entry>();
        }
#endif
    }
}
