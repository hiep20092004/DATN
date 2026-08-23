using System;
using System.Collections.Generic;
using WaterFlow.Core;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelSave : ISaveObject, IRecoverableSaveObject
    {
        public int MaxReachedLevelIndex = 0;
        public int RealLevelIndex = 0;
        public int DisplayLevelIndex = 0;
        public int DisplayLevelType = 0;
        public bool IsPlayingRandomLevel = false;
        public int LastPlayerLevelIndex = -1;
        public List<int> RecentRandomLevelIndexes = new List<int>();
        public int CompletedLevelIndex = -1;
        public bool FirstStart = true;

        // Keys of special levels the player has already entered (format: "{Mode}_{orderIndex}").
        // Persisted so a special level is offered exactly once even when the player is parked
        // on its unlock boundary (e.g. reached the boundary in a previous build, before the
        // special-level feature shipped).

        // Field names that have ever existed on this save object. Used for the
        // content-based recovery fallback so that even if a user’s old save was
        // written under a different stable hash (Unity / scripting backend
        // upgrade between releases), we can still identify the container by
        // matching its JSON payload to this schema.
        //
        // IMPORTANT: keep historical names here even after they are removed
        // from the live class so recovery keeps working across releases. The
        // current build no longer has "LevelTypeIndex", but older user saves
        // do – including it here lets us reclaim those containers.
        private static readonly string[] SignatureFields =
        {
            "MaxReachedLevelIndex",
            "RealLevelIndex",
            "DisplayLevelIndex",
            "LastPlayerLevelIndex",
            "CompletedLevelIndex",
            "IsPlayingRandomLevel",
            "FirstStart",
            "DisplayLevelType",
            "LevelTypeIndex" // historical, removed in v1.x → DisplayLevelType
        };

        public void Flush()
        {
        }

        /// <summary>
        /// Reconciles the gameplay save with the independently persisted user level without allowing
        /// either source to move monotonic main progression backwards.
        /// </summary>
        public bool TryReconcileProgress(int userLevel, out int reconciledUserLevel)
        {
            int userLevelIndex = Math.Max(0, userLevel - 1);
            int reconciledLevelIndex = Math.Max(DisplayLevelIndex,
                Math.Max(MaxReachedLevelIndex, userLevelIndex));

            reconciledUserLevel = reconciledLevelIndex + 1;
            if (reconciledLevelIndex == DisplayLevelIndex &&
                reconciledLevelIndex == MaxReachedLevelIndex)
            {
                return false;
            }

            DisplayLevelIndex = reconciledLevelIndex;
            MaxReachedLevelIndex = reconciledLevelIndex;
            RealLevelIndex = reconciledLevelIndex;
            IsPlayingRandomLevel = false;
            LastPlayerLevelIndex = -1;
            FirstStart = true;
            return true;
        }

        public bool LooksLikeMyData(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            // Require a high overlap with our schema so we don’t accidentally
            // claim another save object’s container. LevelSave has many
            // distinctive field names (MaxReachedLevelIndex, RealLevelIndex,
            // CompletedLevelIndex…) that don’t appear in any other save type
            // in this project, so a 4-of-9 threshold is conservative enough
            // while still tolerating field renames between releases.
            int matched = 0;
            for (int i = 0; i < SignatureFields.Length; i++)
            {
                if (json.IndexOf("\"" + SignatureFields[i] + "\"", StringComparison.Ordinal) >= 0)
                {
                    matched++;
                }
            }

            return matched >= 4;
        }
    }
}
