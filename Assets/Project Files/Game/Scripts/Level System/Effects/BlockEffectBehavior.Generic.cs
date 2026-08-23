using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class BlockEffectBehavior<T> : BlockEffectBehavior
        where T : BlockEffectData
    {
        protected T Data => effectData as T;

        protected bool ValidateDataOrDisable()
        {
            if (Data != null)
                return true;

            Debug.LogError(
                $"[{GetType().Name}] effectData is not {typeof(T).Name}. Got: {effectData?.GetType().Name ?? "null"}",
                this);
            DisableEffect();
            return false;
        }
    }
}
