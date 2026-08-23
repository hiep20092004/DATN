using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LoadObject
{
    [CreateAssetMenu(menuName = "WaterFlow Services/Load Service/Load Folder", fileName = "Load Folder")]
    public class LoadFolder : LoadObjectService
    {
        [SerializeField] protected string extension = ".json";

        public override T LoadObject<T>(string assetPath) where T : class
        {
            var fullPath = $"{path}{assetPath}{extension}";
            var data = File.ReadAllText(fullPath);
            if (string.IsNullOrEmpty(data)) return null;
            return JsonConvert.DeserializeObject<T>(data, Settings);
        }
    }
}