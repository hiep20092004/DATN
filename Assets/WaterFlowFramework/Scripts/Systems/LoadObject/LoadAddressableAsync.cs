using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
#if using_addressable
using UnityEngine.AddressableAssets;
#endif

namespace WaterFlow.Framework.Systems.LoadObject
{
    [CreateAssetMenu(menuName = "WaterFlow Services/Load Service/Load Addressable Async",
        fileName = "Load Addressable Async")]
    public class LoadAddressableAsync : LoadObjectServiceAsync
    {
        private readonly TimeoutController timeoutController = new();
        [SerializeField] private float timeout = 3;
        [SerializeField] private LoadObjectServiceAsync fallback;
        [SerializeField] private string externPath = ".prefab";
        //private Dictionary<string, AsyncOperationHandle> cache = new();

        public override async UniTask<T> LoadAsync<T>(string assetName) where T : class
        {
#if using_addressable
            try
            {
                string fullPath = $"{path}{assetName}{externPath}";
                return await Addressables.LoadAssetAsync<T>(fullPath).WithCancellation(timeoutController.Timeout(TimeSpan.FromSeconds(timeout)));

                // if (cache.TryGetValue(assetName, out var handle))
                // {
                //     return handle.Result as T;
                // }
                //
                // string fullPath = $"{path}{assetName}{externPath}";
                // handle = Addressables.LoadAssetAsync<T>(fullPath);
                // //.WithCancellation(timeoutController.Timeout(TimeSpan.FromSeconds(timeout)))
                // await handle.Task;
                // cache.Add(assetName, handle);
                // return handle.Result as T;
            }
            catch (OperationCanceledException e)
            {
                if (fallback != null)
                {
                    return await fallback.LoadAsync<T>(assetName);
                }
            }
#endif
            return null;
        }
    }
}