using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LoadObject
{
    [CreateAssetMenu(fileName = "Save Folder",
        menuName = "WaterFlow Services/Save Service/Save Folder")]
    public class DefaultSaveObjectFolderService : SaveObjectService
    {
        private readonly JsonSerializerSettings settings = new() { TypeNameHandling = TypeNameHandling.Auto };
        [SerializeField] protected string extension = ".json";

        public override void SaveObject<T>(T data, string fileName)
        {
            var json = JsonConvert.SerializeObject(data, settings);
            if (Directory.Exists(path) == false) Directory.CreateDirectory(path);
            var fullPath = $"{path}{fileName}{extension}";
            File.WriteAllText(fullPath, json);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
    }
}