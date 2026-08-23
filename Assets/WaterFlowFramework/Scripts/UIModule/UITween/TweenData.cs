using System;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Framework.UIModule
{
    [Serializable]
    public class TweenData
    {
        public Transform target;

        [ShowIf("@custom == false")] [OnValueChanged("OnConfigSOValueChanged")]
        public TweenConfigSO configSO;

        public bool custom;

        [ShowIf("@custom")] public TweenConfig config;

        public Action OnCompleted;

        public void SetupData()
        {
            SetDataFromConfigSo();
        }

        private void OnConfigSOValueChanged()
        {
            custom = configSO == null;
            SetDataFromConfigSo();
        }

        private void SetDataFromConfigSo()
        {
            if (!custom && configSO != null) config = configSO.config.Clone();
        }

#if UNITY_EDITOR

        [HorizontalGroup]
        [Button("Import")]
        private void ImportTweenConfigSO()
        {
            if (custom && configSO != null) config = configSO.config.Clone();
        }

        [HorizontalGroup]
        [Button("Export")]
        private void ExportTweenConfigSO()
        {
            if (configSO != null && config != null)
            {
                var configPath = AssetDatabase.GetAssetPath(configSO);
                var newPath = configPath.Replace(".asset", "_copy.asset");
                AssetDatabase.CopyAsset(configPath, newPath);
                //AssetDatabase.SaveAssets();
                var newConfigSO = AssetDatabase.LoadAssetAtPath<TweenConfigSO>(newPath);
                newConfigSO.config = config.Clone();
                configSO = newConfigSO;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
#endif
    }
}