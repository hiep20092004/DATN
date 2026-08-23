using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    public abstract class PoolingService : PoolingServiceBase
    {
        public abstract T Create<T>(string objectName, Vector3 position, Transform parent = null, params object[] args)
            where T : IPoolingObject;

        public abstract T Create<T>(string objectName, Transform parent = null, params object[] args) where T : IPoolingObject;
        public abstract T CreateObject<T>(Component prefab, Transform container, params object[] args) where T : IPoolingObject;
        public abstract void ReturnObj(IPoolingObject obj, bool keep = true);
    }
}