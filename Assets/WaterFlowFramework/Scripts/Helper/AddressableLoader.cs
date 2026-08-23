#if using_addressable
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace WaterFlow.Framework.Helper
{
    public class AddressableLoader<T> where T : UnityEngine.Object
    {
        private Dictionary<string, AsyncOperationHandle<T>> loadedAssets = new Dictionary<string, AsyncOperationHandle<T>>();
        protected T Asset;
        protected async void Init(string address)
        {
            this.Asset = await LoadAssetAsync(address);
        }
        public async UniTask<T> LoadAssetAsync(string address)
        {
            if (loadedAssets.TryGetValue(address, out var result))
            {
                if(result.IsDone) return result.Result;
                await result.Task;
                if (result.Status == AsyncOperationStatus.Succeeded)
                {
                    return result.Result;
                }
                else
                {
                    loadedAssets.Remove(address);
                }
            }
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            loadedAssets.Add(address, handle);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }
            else
            {
                loadedAssets.Remove(address);
                Debug.LogError($"Failed to load asset: {address}");
                return null;
            }
        }

        public void ReleaseAsset(string address)
        {
            if (loadedAssets.ContainsKey(address))
            {
                Addressables.Release(loadedAssets[address]);
                loadedAssets.Remove(address);
            }
        }

        public void ReleaseAll()
        {
            foreach (var handle in loadedAssets.Values)
            {
                Addressables.Release(handle);
            }
            loadedAssets.Clear();
        }
    }
}
#endif