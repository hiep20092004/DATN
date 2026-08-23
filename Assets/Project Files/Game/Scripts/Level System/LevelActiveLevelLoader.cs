using System.Collections.Generic;
using UnityEngine;
#if using_addressable
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace WaterFlow.Game
{
    /// <summary>
    /// Loads baked active <see cref="LevelData"/> assets from Addressables
    /// (<see cref="LevelSystemUtils.ActiveLevelsFolder"/>).
    /// </summary>
    public static class LevelActiveLevelLoader
    {
        private static readonly Dictionary<int, LevelData> Cache = new Dictionary<int, LevelData>();

#if using_addressable
        private static readonly Dictionary<int, AsyncOperationHandle<LevelData>> Handles =
            new Dictionary<int, AsyncOperationHandle<LevelData>>();
#endif

        public static LevelData Load(int levelIndex)
        {
            if (levelIndex < 0)
                return null;

            if (Cache.TryGetValue(levelIndex, out LevelData cached) && cached)
                return cached;

#if using_addressable
            int levelNumber = levelIndex + 1;
            string assetPath = LevelSystemUtils.GetActiveLevelSlotAssetPath(levelNumber);

            AsyncOperationHandle<LevelData> handle = Addressables.LoadAssetAsync<LevelData>(assetPath);
            LevelData level = handle.WaitForCompletion();

            if (!level)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                string slotName = $"Level {levelNumber:D3}";
                handle = Addressables.LoadAssetAsync<LevelData>(slotName);
                level = handle.WaitForCompletion();
            }

            if (level)
            {
                Cache[levelIndex] = level;
                Handles[levelIndex] = handle;
                return level;
            }

            if (handle.IsValid())
                Addressables.Release(handle);

            Debug.LogError(
                $"[LevelActiveLevelLoader] Failed to load level index {levelIndex} from Addressables (path={assetPath}).");
            return null;
#else
            Debug.LogError("[LevelActiveLevelLoader] using_addressable is not defined; cannot load active levels.");
            return null;
#endif
        }

        public static void ReleaseAll()
        {
#if using_addressable
            foreach (AsyncOperationHandle<LevelData> handle in Handles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            Handles.Clear();
#endif
            Cache.Clear();
        }
    }
}
