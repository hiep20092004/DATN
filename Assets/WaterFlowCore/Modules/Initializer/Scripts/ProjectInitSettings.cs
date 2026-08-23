using UnityEngine;

namespace WaterFlow.Core
{
    public class ProjectInitSettings : ScriptableObject
    {
        [SerializeField] InitModule[] modules;
        public InitModule[] Modules => modules;

        public void Init(Initializer initializer)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                if(modules[i])
                {
                    modules[i].CreateComponent();
                }
            }
        }

        public T GetModule<T>() where T : InitModule
        {
            foreach (var module in modules)
            {
                if (module && module is T initModule)
                {
                    return initModule;
                }
            }

            return null;
        }
    }
}