using System.Collections.Generic;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class BlockEffectBehavior : MonoBehaviour
    {
        protected LevelBlockBehavior linkedBlock;

        protected int orderID;
        protected bool isActive;
        protected BlockEffectData effectData;
        protected BlockEffectType type;
        protected int effectSortingOrder;

        protected HashSet<ToggleVisualSource> hiddenVisualSources = new ();
        
        [SerializeField] protected BaseBlockEffectConfig effectConfig;

        public int OrderID => orderID;
        public bool IsActive => isActive;
        public BlockEffectData EffectData => effectData;
        public BlockEffectType Type => type;
        public BaseBlockEffectConfig EffectConfig => effectConfig;
        protected T GetConfig<T>() where T : BaseBlockEffectConfig
        {
            return effectConfig as T;
        }
        
        /// <summary> Set from LevelDatabase LevelBlockEffectData when effect is applied; default 0. </summary>
        public int EffectSortingOrder => effectSortingOrder;

        public void SetOrder(int orderID)
        {
            this.orderID = orderID;
        }

        /// <summary>
        /// Called when the effect is created and attached to a block.
        /// Use this to initialize visuals, timers, or state.
        /// </summary>
        public virtual void OnCreated(LevelBlockBehavior blockBehavior) { }
        public virtual void OnCreatedAllBlock() { }
        /// <summary>
        /// Called when the effect is disabled or removed from a block.
        /// Use this to clean up visuals, unregister from managers, or reset state.
        /// </summary>
        public virtual void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource) {}

        /// <summary>
        /// Called when the game starts.
        /// Use this to start timers or animations that should only run during gameplay.
        /// </summary>
        public virtual void OnLevelActivated() { }

        public virtual void OnMapSpawnCompleted()
        {
            
        }

        /// <summary>
        /// When true, block water must stay hidden after map spawn (e.g. ice, hidden until reveal).
        /// </summary>
        public virtual bool HidesBlockWater => false;
        /// <summary>
        ///  Called when the game is paused.
        ///  </summary>
        public virtual void OnGameStateChanged(bool isActive) { }
        
        /// <summary>
        /// Called when the game ends.
        /// </summary>
        public virtual void OnGameEnded() { }

        /// <summary>
        /// Called when player revived and returned to the level.
        /// </summary>
        public virtual void OnRevived(LoseReason loseReason, int seconds)
        {
            
        }

        /// <summary>
        /// Called when this block is picked up (lifted) for dragging.
        /// </summary>
        public virtual void OnBlockPicked(LevelBlockBehavior levelBlockBehavior) { }

        /// <summary>
        /// Called when this block is released and begins snapping to the grid.
        /// </summary>
        public virtual void OnBlockReleased(LevelBlockBehavior levelBlockBehavior, Vector2Int snapTargetPosition) { }

        public virtual void OnBlockFilled(LevelBlockBehavior levelBlockBehavior) { }
        public virtual void OnBlockFullFilledBeforeAnimationGlobal(LevelBlockBehavior levelBlockBehavior) { }
        public virtual void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor) { }

        public virtual void OnBlockFullBeforeAnimationFilled()
        {
            
        }
        
        /// <summary>
        /// Called when the block is not fully after animation fill completed.
        /// </summary>
        public virtual void OnBlockAfterAnimationFilled()
        {
            
        }
        
        /// <summary>
        /// Called when the block is fully (after animation completed).
        /// Use this to trigger cleanup, effects, or disable the effect.
        /// </summary>
        public virtual void OnBlockFullAfterAnimationFilled()
        {
            
        }


        public virtual BlockGateState AllowGateEntered()
        {
            return BlockGateState.Enterable;
        }

        /// <summary>
        /// Called when any block enters a gate, including this one.
        /// Use this to decrement counters, play sounds, or trigger particles.
        /// </summary>
        public virtual void OnBlockEnteredGate(LevelBlockBehavior levelBlockBehavior, GateBehavior gateBehavior) { }

        public virtual void OverrideMovementDirection(ref Vector3 movementDirection) { }
        
        /// <summary>
        /// Determines whether the block with this attached effect can be clicked.
        /// If this method returns <c>false</c>, the block will not respond to click events and will instead play a shake animation.
        /// Override this method in derived classes to implement custom clickability logic for specific effects.
        /// </summary>
        public virtual bool IsClickable()
        {
            return true;
        }
        
        public virtual AudioId GetOverrideClickAudioId()
        {
            return AudioId.None;
        }


        public virtual bool IsDestructible()
        {
            return true;
        }
        
        /// <summary>
        /// Determines whether the block should move as part of a group.
        /// Return true for effects that link multiple blocks together.
        /// </summary>
        public virtual bool MoveMultiplyObjects()
        {
            return false;
        }

        private LevelBlockBehavior[] linkedBlockBuffer;
        
        public virtual IReadOnlyList<LevelBlockBehavior> GetLinkedBlocks()
        {
            linkedBlockBuffer ??= new LevelBlockBehavior[1];
            linkedBlockBuffer[0] = linkedBlock;
            return linkedBlockBuffer;
        }


        /// <summary>
        /// Transform the pick "lift" (model Y raise while dragging) should animate, when this effect
        /// replaces the block's own model with its own visual. Container effects hide the block
        /// renderers and present a single group visual, so lifting <c>ModelParentTransform</c> is
        /// invisible — they return the group visual transform here. Null = no override (the block's
        /// model is lifted as usual).
        /// </summary>
        public virtual Transform GetModelLiftTransform() => null;

        /// <summary>
        /// Returns the block color to use for logic and visuals.
        /// Override to provide a custom color (e.g., for layered or overridden effects).
        /// </summary>
        public virtual BlockColor GetOverrideBlockColor()
        {
            return linkedBlock.OriginColorConfig.Type;
        }
        
        
        /// <summary>
        /// Called when a block is destructed (after play filled animation) in the level.
        /// Removes the specified block from the ActiveBlocks collection,
        /// ensuring it is no longer tracked as part of the active level state.
        /// </summary>
        /// <param name="block">The block that was destructed.</param>
        public virtual void OnBlockDestructed(LevelBlockBehavior levelBlockBehavior)
        {

        }

        public virtual void OnNewEffectAddedToBlock(BlockEffectBehavior effect)
        {

        }

        public virtual void OnBlockSplit()
        {

        }

        public virtual BlockEffectData GetCurrentEffectData()
        {
            return effectData?.Clone();
        }


        /// <summary>
        /// Reference-counted visibility toggle. Each <see cref="ToggleVisualSource"/> that hides this effect
        /// adds itself to <see cref="hiddenVisualSources"/>; the effect is visible ONLY when no source is
        /// hiding it. This is what makes stacked effects layer correctly: e.g. a block hidden by both Ice and
        /// Container stays hidden when the Container clears, because Ice is still in the set — it only reveals
        /// once Ice releases too.
        ///
        /// Subclasses MUST NOT override this to toggle their visuals from the raw <paramref name="isOn"/>:
        /// that ignores the other sources and breaks layering. Override <see cref="ApplyVisualState"/> instead,
        /// which receives the single resolved visibility. Overriding this method is reserved for effects that
        /// need source-specific behavior (see <c>DualBlockEffectBehavior</c>).
        /// </summary>
        public virtual void OnToggleVisual(bool isOn, ToggleVisualSource source = ToggleVisualSource.Self)
        {
            if (!isActive) return;

            if (isOn)
                hiddenVisualSources.Remove(source);
            else
                hiddenVisualSources.Add(source);

            ApplyVisualState(hiddenVisualSources.Count == 0);
        }

        /// <summary>
        /// Applies the resolved visibility for this effect's visuals. <paramref name="visible"/> is the
        /// aggregate from <see cref="OnToggleVisual"/> (visible only when no source hides the effect).
        /// Override to toggle auxiliary visuals (extra GameObjects, tweens, timers) — always drive them from
        /// <paramref name="visible"/>, never from a per-source flag — and call <c>base.ApplyVisualState</c>.
        /// </summary>
        protected virtual void ApplyVisualState(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public virtual void DisableEffect(DisableSource disableSource = DisableSource.Self)
        {
            isActive = false;
            gameObject.SetActive(false);
            OnDisabled(linkedBlock, disableSource);
        }

        public BlockEffectBehavior ApplyEffect(LevelBlockBehavior blockBehavior, BlockEffectData effectData, int sortingOrder = 0)
        {
            GameObject effectObject = Instantiate(gameObject, blockBehavior.transform, true);
            effectObject.transform.ResetLocal();

            BlockEffectBehavior effectBehavior = effectObject.GetComponent<BlockEffectBehavior>();
            effectBehavior.linkedBlock = blockBehavior;
            effectBehavior.type = effectData.Type;
            effectBehavior.effectData = effectData;
            effectBehavior.effectSortingOrder = sortingOrder;
            effectBehavior.isActive = true;

            blockBehavior.ApplyEffect(effectBehavior);

            return effectBehavior;
        }
    }
}