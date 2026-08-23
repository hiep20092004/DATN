using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    public abstract class PoolingContainerService: ServiceSo
    {
        public abstract T CreateObject<T>(Transform container);
        public abstract void CleanContainer(Transform container);
    }
}