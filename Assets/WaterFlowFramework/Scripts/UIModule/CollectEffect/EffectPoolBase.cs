using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.CollectEffect
{
    public class EffectPoolBase : MonoBehaviour, IPoolingObject
    {
        public float timeLive = 0;

        public virtual void Setup()
        {
        }

        public virtual void OnCreateObj(params object[] args)
        {
            transform.localScale = Vector3.one;
            if (timeLive > 0 && gameObject.activeInHierarchy)
                FrameworkUtils.DelayCall(timeLive, ReturnToPool, this);
        }

        public virtual void OnReturnObj()
        {
        }

        protected virtual void ReturnToPool()
        {
            GameSystem.GetService<PoolingServiceAsync>().ReturnObj(this);
        }
    }
}