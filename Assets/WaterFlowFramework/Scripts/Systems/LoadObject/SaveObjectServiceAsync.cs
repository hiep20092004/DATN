using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEditor;

namespace WaterFlow.Framework.Systems.LoadObject
{
    public abstract class SaveObjectServiceAsync : ServiceSo
    {
#if UNITY_EDITOR
        [OnValueChanged(nameof(OnPathChanged))]
#endif
        public string path;

        public abstract UniTask<T> SaveObject<T>(T data, string fileName);

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