using System;
using UnityEngine;
using System.Collections.Generic;

namespace WaterFlow.Core
{
    [Serializable]
    public class GlobalSave
    {
        [SerializeField] SavedDataContainer[] saveObjects;
        private List<SavedDataContainer> saveObjectsList;

        [SerializeField] long accountCreatedTime;
        public long AccountCreatedTime => accountCreatedTime;

        [SerializeField] float gameTime;
        public float GameTime => gameTime + (Time - lastFlushTime);

        [SerializeField] DateTime lastExitTime;
        public DateTime LastExitTime => lastExitTime;

        private float lastFlushTime = 0;

        public float Time { get; set; }

        public void Init(float time)
        {
            if (saveObjects == null)
            {
                saveObjectsList = new List<SavedDataContainer>();
                accountCreatedTime = TimeUtils.GetCurrentUnixTime();
            }
            else
            {
                saveObjectsList = new List<SavedDataContainer>(saveObjects);
            }

            for (int i = 0; i < saveObjectsList.Count; i++)
            {
                if (saveObjectsList[i] == null) continue;
                saveObjectsList[i].Restored = false;
            }

            Time = time;
            lastFlushTime = Time;
        }

        public void Flush(bool updateLastExitTime)
        {
            saveObjects = saveObjectsList.ToArray();

            for (int i = 0; i < saveObjectsList.Count; i++)
            {
                SavedDataContainer saveObject = saveObjectsList[i];
                saveObject?.Flush();
            }

            gameTime += Time - lastFlushTime;

            lastFlushTime = Time;

            if (updateLastExitTime) lastExitTime = DateTime.Now;
        }

        // Modern entry point – callers pass both a stable string key and a
        // pre-computed stable hash. The container is located via a 4-tier
        // lookup designed to recover saves written by older builds even when
        // the runtime hash for the same string has drifted across Unity /
        // scripting backend upgrades.
        internal T GetSaveObject<T>(int stableHash, string key, int legacyHash)
            where T : ISaveObject, new()
        {
            SaveDebugLog.Log($"GetSaveObject<{typeof(T).Name}>: key='{key}', stableHash={stableHash}, legacyHash={legacyHash}");
            SavedDataContainer container = FindContainer(stableHash, key, legacyHash);

            if (container == null)
            {
                SaveDebugLog.Warn($"GetSaveObject<{typeof(T).Name}>: Tier1-3 miss. Trying content recovery.");
                container = TryRecoverByContent<T>(stableHash, key);
            }

            if (container == null)
            {
                SaveDebugLog.Warn($"GetSaveObject<{typeof(T).Name}>: no container found. Creating new default (data may reset).");
                container = new SavedDataContainer(stableHash, key, new T());
                saveObjectsList.Add(container);
                return (T)container.SaveObject;
            }

            // Migrate legacy identifiers in-place so the next save persists
            // the new (key + stable-hash) scheme.
            if (container.Hash != stableHash || container.Key != key)
            {
                SaveDebugLog.Warn($"GetSaveObject<{typeof(T).Name}>: migrating identifiers oldHash={container.Hash} oldKey='{container.Key}' → newHash={stableHash} newKey='{key}'");
                container.Reidentify(stableHash, key);
            }

            if (!container.Restored)
            {
                container.Restore<T>();
            }
            return (T)container.SaveObject;
        }

        private SavedDataContainer FindContainer(int stableHash, string key, int legacyHash)
        {
            // Tier 1: exact string key match (works for all v2+ saves).
            if (!string.IsNullOrEmpty(key))
            {
                for (int i = 0; i < saveObjectsList.Count; i++)
                {
                    var c = saveObjectsList[i];
                    if (c != null && c.Key == key)
                    {
                        SaveDebugLog.Log($"FindContainer: Tier1 key match '{key}'");
                        return c;
                    }
                }
            }

            // Tier 2: stable FNV hash match (v2+ saves where key was stripped).
            for (int i = 0; i < saveObjectsList.Count; i++)
            {
                var c = saveObjectsList[i];
                if (c != null && string.IsNullOrEmpty(c.Key) && c.Hash == stableHash)
                {
                    SaveDebugLog.Log($"FindContainer: Tier2 stable-hash match hash={stableHash}");
                    return c;
                }
            }

            // Tier 3: legacy GetHashCode match. Works for users whose runtime
            // still computes the same string.GetHashCode() as when the save
            // was first written (i.e. no Unity / backend upgrade happened).
            if (legacyHash != stableHash)
            {
                for (int i = 0; i < saveObjectsList.Count; i++)
                {
                    var c = saveObjectsList[i];
                    if (c != null && string.IsNullOrEmpty(c.Key) && c.Hash == legacyHash)
                    {
                        SaveDebugLog.Warn($"FindContainer: Tier3 legacy-hash match legacyHash={legacyHash} (stableHash={stableHash})");
                        return c;
                    }
                }
            }

            return null;
        }

        // Tier 4: content-based recovery. Walks every container that hasn’t
        // been adopted yet and asks the candidate type whether the stored JSON
        // looks like its data. Only used as a last resort because it can in
        // theory misclassify if two save types share many field names.
        private SavedDataContainer TryRecoverByContent<T>(int stableHash, string key)
            where T : ISaveObject, new()
        {
            T probe = new T();
            if (probe is not IRecoverableSaveObject recoverable) return null;

            for (int i = 0; i < saveObjectsList.Count; i++)
            {
                var c = saveObjectsList[i];
                if (c == null || c.Restored) continue;            // already taken by another T
                if (string.IsNullOrEmpty(c.Json)) continue;
                if (!string.IsNullOrEmpty(c.Key)) continue;        // belongs to a known v2+ key – don’t steal
                if (!recoverable.LooksLikeMyData(c.Json)) continue;

                if (c.TryRestoreOverwrite(probe))
                {
                    Debug.LogWarning($"[GlobalSave] Content-based recovery matched container hash={c.Hash} -> {typeof(T).FullName} (key='{key}', stableHash={stableHash}). Migrating identifiers.");
                    SaveDebugLog.Warn($"TryRecoverByContent<{typeof(T).Name}>: matched by JSON signature. oldHash={c.Hash} → newHash={stableHash}, key='{key}'");
                    c.Reidentify(stableHash, key);
                    return c;
                }
            }

            return null;
        }

        // ---------- Legacy-style entry points (kept for compatibility) -----

        public T GetSaveObject<T>(int hash) where T : ISaveObject, new()
        {
            // No string key available – treat the supplied hash as both the
            // stable hash and the legacy hash. This preserves existing
            // behaviour for callers that haven’t been migrated yet.
            return GetSaveObject<T>(hash, null, hash);
        }

        public T GetSaveObject<T>(string uniqueName) where T : ISaveObject, new()
        {
            int stable = SaveStableHash.Compute(uniqueName);
            int legacy = uniqueName != null ? uniqueName.GetHashCode() : stable;
            return GetSaveObject<T>(stable, uniqueName, legacy);
        }

        public T GetSaveObject<T>() where T : ISaveObject, new()
        {
            string typeKey = typeof(T).FullName;
            int stable = SaveStableHash.Compute(typeKey);
            // Mirror the historical key string so legacy hashes line up.
            int legacy = typeof(T).ToString().GetHashCode();
            return GetSaveObject<T>(stable, typeKey, legacy);
        }

        public void Info()
        {
            foreach (var container in saveObjectsList)
            {
                if (container == null) continue;
                Debug.Log($"Hash: {container.Hash} | Key: {container.Key} | Object: {container.SaveObject}");
            }
        }
    }
}
