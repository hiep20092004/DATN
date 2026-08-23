using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LoadObject
{
    [CreateAssetMenu(menuName = "WaterFlow Services/Load Service/Load Resources Async",
        fileName = "Load Resources Async")]
    public class LoadResourcesAsync : LoadObjectServiceAsync
    {
        public override async UniTask<T> LoadAsync<T>(string assetPath) where T : class
        {
            var fullPath = string.IsNullOrEmpty(path) ? assetPath : $"{path}/{assetPath}";
            if (fullPath.EndsWith(".json")) fullPath = fullPath.Replace(".json", "");
            var data = await Resources.LoadAsync(fullPath);
            if (data == null)
            {
                Debug.LogError($"[LoadResourcesAsync] Resources.Load failed. assetPath='{assetPath}', fullPath='{fullPath}', type='{typeof(T).Name}'.");
                return null;
            }
            if (data is TextAsset textAsset) return JsonConvert.DeserializeObject<T>(textAsset.text, Settings);

            return data as T;
        }
    }
}