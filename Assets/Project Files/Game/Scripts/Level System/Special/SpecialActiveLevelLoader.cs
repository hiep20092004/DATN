using System.Collections.Generic;
using UnityEngine;
#if using_addressable
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace WaterFlow.Game
{
    public static class SpecialActiveLevelLoader
    {
        private static readonly Dictionary<string, SpecialLevelData> Cache = new Dictionary<string, SpecialLevelData>();

#if using_addressable
        private static readonly Dictionary<string, AsyncOperationHandle<SpecialLevelData>> Handles =
            new Dictionary<string, AsyncOperationHandle<SpecialLevelData>>();
#endif

        public static SpecialLevelData Load(SpecialLevelMode mode, int orderIndexZeroBased)
        {
            if (orderIndexZeroBased < 0)
                return null;

            int orderNumber = orderIndexZeroBased + 1;
            string key = BuildKey(mode, orderNumber);
            if (Cache.TryGetValue(key, out SpecialLevelData cached) && cached)
                return cached;

#if using_addressable
            string assetPath = LevelSystemUtils.GetActiveSpecialLevelSlotAssetPath(mode, orderNumber);
            AsyncOperationHandle<SpecialLevelData> handle = Addressables.LoadAssetAsync<SpecialLevelData>(assetPath);
            SpecialLevelData level = handle.WaitForCompletion();

            if (!level)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                string slotName = $"{mode} {orderNumber:D3}";
                handle = Addressables.LoadAssetAsync<SpecialLevelData>(slotName);
                level = handle.WaitForCompletion();
            }

            if (!level)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                return null;
            }

            Cache[key] = level;
            Handles[key] = handle;
            return level;
#else
            Debug.LogError("[SpecialActiveLevelLoader] using_addressable is not defined; cannot load special levels.");
            return null;
#endif
        }

        public static void ReleaseAll()
        {
#if using_addressable
            foreach (AsyncOperationHandle<SpecialLevelData> handle in Handles.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            Handles.Clear();
#endif
            Cache.Clear();
        }

        private static string BuildKey(SpecialLevelMode mode, int orderNumber1Based)
        {
            return $"{mode}_{orderNumber1Based:D3}";
        }
    }
}
