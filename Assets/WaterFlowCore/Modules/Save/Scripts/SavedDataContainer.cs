using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace WaterFlow.Core
{
    [Serializable]
    public class SavedDataContainer
    {
        // Legacy primary identifier. Historically computed via the unstable
        // string.GetHashCode(). Kept for backwards compatibility – the
        // GlobalSave migration path falls back to it when the modern
        // <see cref="key"/> is missing or doesn’t match.
        [SerializeField] int hash;
        public int Hash => hash;

        // Stable string identifier (e.g. "WaterFlow.Game.LevelSave"). Added in v2 of
        // the save container schema. Marked OptionalField so BinaryFormatter
        // can deserialize older blobs that don’t contain this field.
        [OptionalField(VersionAdded = 2)]
        [SerializeField] string key;
        public string Key => key;

        [SerializeField] string json;
        public string Json => json;

        public bool Restored { get; set; }

        [NonSerialized] ISaveObject saveObject;
        public ISaveObject SaveObject => saveObject;

        public SavedDataContainer(int hash, string key, ISaveObject saveObject)
        {
            this.hash = hash;
            this.key = key;
            this.saveObject = saveObject;
            Restored = true;
        }

        // Allow GlobalSave to update the identifier when migrating an old
        // container to the new (key + stable hash) scheme.
        internal void Reidentify(int newHash, string newKey)
        {
            hash = newHash;
            key = newKey;
        }

        public void Flush()
        {
            if (saveObject != null) saveObject.Flush();
            if (Restored) json = JsonUtility.ToJson(saveObject);
        }

        public void Restore<T>() where T : ISaveObject, new()
        {
            try
            {
                if (!string.IsNullOrEmpty(json))
                    saveObject = JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SavedDataContainer] FromJson<{typeof(T).Name}> failed for key='{key}' hash={hash}: {ex.Message}. Falling back to defaults.");
                saveObject = default;
            }

            if (saveObject == null)
            {
                // JsonUtility returns null when json is null/empty – materialise
                // a fresh default so callers never get a null reference back.
                saveObject = new T();
            }
            Restored = true;
        }

        // Used during recovery to rehydrate an already-constructed instance
        // from JSON without losing its reference identity.
        internal bool TryRestoreOverwrite(ISaveObject target)
        {
            if (target == null || string.IsNullOrEmpty(json)) return false;
            try
            {
                JsonUtility.FromJsonOverwrite(json, target);
                saveObject = target;
                Restored = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SavedDataContainer] FromJsonOverwrite failed for key='{key}' hash={hash}: {ex.Message}");
                return false;
            }
        }
    }
}
