using UnityEngine;
using UnityEngine.EventSystems;

namespace WaterFlow.Core
{
    [DefaultExecutionOrder(-999)]
    public class Initializer : MonoBehaviour
    {
        private static Initializer initializer;

        [SerializeField] ProjectInitSettings initSettings;
        [SerializeField] EventSystem eventSystem;

        public static GameObject GameObject { get; private set; }

        public static ProjectInitSettings InitSettings { get; private set; }

        public void Init()
        {
            if (initializer) return;

            initializer = this;

            InitSettings = initSettings;

            GameObject = gameObject;

#if MODULE_INPUT_SYSTEM
            eventSystem.gameObject.GetOrAdd<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.gameObject.GetOrAdd<StandaloneInputModule>();
#endif


            DontDestroyOnLoad(gameObject);
        }

        public void InitModules()
        {
            initSettings.Init(this);
        }

    }
}