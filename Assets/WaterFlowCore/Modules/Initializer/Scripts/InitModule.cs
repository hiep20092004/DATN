using UnityEngine;

namespace WaterFlow.Core
{
    public abstract class InitModule : ScriptableObject
    {
        public abstract string ModuleName { get; }

        public abstract void CreateComponent();
    }
}