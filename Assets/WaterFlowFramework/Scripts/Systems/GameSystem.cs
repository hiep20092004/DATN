using System;
using System.Collections;
using WaterFlow.Framework.Base.Singleton;
using Sirenix.OdinInspector;
using WaterFlow.Framework.Systems.ConfigManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems
{
    public class GameSystem : SingletonSimple<GameSystem>
    {
        [SerializeField] protected ServicesManager serviceManager;
        [SerializeField] protected Service<ConfigService> configService = new();
        [SerializeField] protected bool autoInit = true;
        [SerializeField] protected float delayToInit = 0.5f;


        protected override void OnAwake()
        {
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void Start()
        {
            if (autoInit)
            {
                StartCoroutine(WaitToInit());
            }
        }

        IEnumerator WaitToInit()
        {
            yield return new WaitForSeconds(delayToInit);
            InitializeServices();
        }

        public virtual void InitializeServices()
        {
            serviceManager.Resolve();
        }

        public static T GetService<T>() where T : class
        {
            return Instance?.serviceManager?.Get<T>();
        }

        public static T GetConfig<T>() where T : class
        {
            return Instance.configService.Instance.Get<T>();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            serviceManager.OnApplicationFocus(hasFocus);
        }
    }
}