using Newtonsoft.Json;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LoadObject
{
    [CreateAssetMenu(menuName = "WaterFlow Services/Load Service/Load Resources", fileName = "Load Resources")]
    public class LoadResources : LoadObjectService
    {
        public override T LoadObject<T>(string assetName) where T : class
        {
            string fullPath = $"{path}{assetName}";
            var obj = Resources.Load(fullPath);
            if (obj == null) return null;
            if (obj is TextAsset textAsset)
            {
                return string.IsNullOrEmpty(textAsset.text) ? null : JsonConvert.DeserializeObject<T>(textAsset.text, Settings);
            }
            else
            {
                return obj as T;
            }
        }
    }
}