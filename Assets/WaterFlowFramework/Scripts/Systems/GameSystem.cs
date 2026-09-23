using System;
using System.Collections;
using WaterFlow.Core;
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

        private const float SAVE_WAIT_TIMEOUT = 10f;


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

            // Resolving services reads save data (BoosterService -> ActiveSession -> SaveController), so it must
            // not run before SaveInitModule, which fires from GameLoading -> ProjectInitSettings in the Loading
            // scene. Gate on the save flag instead of trusting delayToInit, which is a race.
            float waited = 0f;
            while (!SaveController.IsSaveLoaded)
            {
                if (waited > SAVE_WAIT_TIMEOUT)
                {
                    Debug.LogError(
                        "GameSystem: SaveController was never initialized. The boot scene is missing its " +
                        "GameLoading/Initializer object, so ProjectInitSettings never ran.");
                    break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

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