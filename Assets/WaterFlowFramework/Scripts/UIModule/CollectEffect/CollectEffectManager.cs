using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.CollectEffect
{
    public class CollectEffectManager : SingletonSimple<CollectEffectManager>
    {
        [SerializeField] private List<CollectEffectControllerBase> collectEffects;
        private readonly Dictionary<Type, CollectEffectControllerBase> collectEffectsDictionary = new();

        public T GetICollectEffectType<T>() where T : CollectEffectControllerBase
        {
            if (collectEffectsDictionary.TryGetValue(typeof(T), out var collectEffectController))
                return (T)collectEffectController;

            var t = FindICollectEffectType<T>();
            collectEffectsDictionary.Add(typeof(T), t);
            return t;
        }

        public T FindICollectEffectType<T>() where T : CollectEffectControllerBase
        {
            foreach (var collectEffectController in collectEffects)
                if (collectEffectController.GetType() == typeof(T))
                    return (T)collectEffectController;
            return default;
        }
    }
}