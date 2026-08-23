using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class GateEffectBehavior : MonoBehaviour
    {
        protected GateBehavior linkedGate;

        private int orderID;
        private bool isActive;
        protected GateEffectData data;
        private GateEffectType type;

        [SerializeField] protected BaseGateEffectConfig effectConfig;

        public bool IsActive => isActive;
        public GateEffectType Type => type;
        public GateBehavior LinkedGate => linkedGate;
        public BaseGateEffectConfig EffectConfig => effectConfig;
        protected T GetConfig<T>() where T : BaseGateEffectConfig
        {
            return effectConfig as T;
        }
        
        public void SetOrder(int orderID)
        {
            this.orderID = orderID;
        }

        /// <summary>
        /// Called when the effect is created and attached to a gate.
        /// Use this to initialize visuals, set up state, or register with managers.
        /// </summary>
        public abstract void OnCreated(GateBehavior gateBehavior);

        /// <summary>
        /// Called when the effect is disabled or removed from a gate.
        /// Use this to clean up visuals, reset transforms, or unregister from managers.
        /// </summary>
        public virtual void OnDisabled(GateBehavior gateBehavior) { }

        /// <summary>
        /// Determines whether a block can pass through this gate.
        /// Override to implement custom gate-passing logic (e.g., locked doors, color checks).
        /// </summary>
        /// <param name="levelBlockBehavior">The block attempting to pass through the gate.</param>
        /// <returns>True if the block can pass, false otherwise.</returns>
        public virtual BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            return BlockGateState.Enterable;
        }

        public virtual void OnNewEffectAddedToGate(GateEffectBehavior effect)
        {

        }

        public virtual void OnEffectRemovedFromGate(GateEffectBehavior removedEffect)
        {

        }

        public virtual void OnTransferred(GateBehavior fromGate, GateBehavior toGate)
        {

        }
        

        public virtual void OnBlockFullFilledBeforeAnimationGlobal(LevelBlockBehavior levelBlockBehavior) { }
        public virtual void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor) { }
        public virtual bool BlocksGateVisualColorSync => false;

        /// <summary>
        /// Re-serializable data reflecting live state (mirrors <see cref="BlockEffectBehavior.GetCurrentEffectData"/>).
        /// Stateful effects (ice turns, valve open/close, chain keys) override to write their counters back.
        /// </summary>
        public virtual GateEffectData GetCurrentEffectData()
        {
            return data?.Clone();
        }

        /// <summary>
        /// Called when player revived and returned to the level.
        /// </summary>
        public virtual void OnRevived(LoseReason loseReason, int seconds)
        {
            
        }

        protected void DisableEffect()
        {
            isActive = false;
            gameObject.SetActive(false);
            OnDisabled(linkedGate);
        }

        public void TransferTo(GateBehavior targetGate, bool reparent = true)
        {
            if (!targetGate || targetGate == linkedGate)
                return;

            GateBehavior oldGate = linkedGate;
            if (oldGate)
            {
                oldGate.RemoveEffect(this);
            }

            if (reparent)
            {
                transform.SetParent(targetGate.transform, true);
            }

            linkedGate = targetGate;
            targetGate.AttachEffect(this);

            OnTransferred(oldGate, targetGate);
        }

        public GateEffectBehavior ApplyEffect(GateBehavior gateBehavior, GateEffectType type, GateEffectData effectData)
        {
            GameObject effectObject = Instantiate(gameObject, gateBehavior.transform, true);
            effectObject.transform.ResetLocal();

            GateEffectBehavior effectBehavior = effectObject.GetComponent<GateEffectBehavior>();
            effectBehavior.linkedGate = gateBehavior;
            effectBehavior.type = type;
            effectBehavior.data = effectData;
            effectBehavior.isActive = true;

            gateBehavior.ApplyEffect(effectBehavior);

            return effectBehavior;
        }
    }
}