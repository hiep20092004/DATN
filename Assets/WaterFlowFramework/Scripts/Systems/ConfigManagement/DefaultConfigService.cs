using System;
using System.Collections.Generic;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

namespace WaterFlow.Framework.Systems.ConfigManagement
{
    [CreateAssetMenu(fileName = "DefaultConfigService", menuName = "WaterFlow Services/Config Service")]
    public class DefaultConfigService : ConfigService, IServiceInitialize
    {
        [SerializeField] Service<LoadObjectService> loadObjectService = new();
        [SerializeField] private List<ConfigSo> configsSo;
        private readonly Dictionary<Type, object> configs = new();


        public void Initialize()
        {
            foreach (var configSo in configsSo)
            {
                if (configSo != null)
                    configs.TryAdd(configSo.GetType(), configSo);
            }
        }

        public override T Get<T>() where T : class
        {
            if (configs.TryGetValue(typeof(T), out var config))
            {
                return (T)config;
            }

            config = LoadConfig<T>();
            if (config != null)
            {
                configs.Add(typeof(T), config);
            }

            return (T)config;
        }

        private T LoadConfig<T>() where T : class
        {
            return loadObjectService.Instance.LoadObject<T>(nameof(T));
        }
    }
}