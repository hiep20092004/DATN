using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;

namespace WaterFlow.Framework.Systems.LoadObject
{
    public abstract class LoadObjectServiceAsync : ServiceSo
    {
        protected static readonly JsonSerializerSettings Settings = new() { TypeNameHandling = TypeNameHandling.Auto };
#if UNITY_EDITOR
        [OnValueChanged(nameof(OnPathChanged))]
#endif
        public string path;

        public abstract UniTask<T> LoadAsync<T>(string assetName) where T : class;
        

#if UNITY_EDITOR
        private void OnPathChanged()
        {
            string preName = this.name;
            string subName = string.IsNullOrEmpty(path) ? "" : $"[{Regex.Replace(path, @"\W", "-")}]";
            var newName = "";
            if (Regex.IsMatch(preName, @"\[\S*\]"))
            {
                newName = Regex.Replace(preName, @"\[\S*\]", $"{subName}");
            }
            else
            {
                newName = preName + (string.IsNullOrEmpty(subName) ? "" : $" {subName}");
            }

            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(this), newName);
        }
#endif
    }
}