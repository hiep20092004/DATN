using System;
using System.Collections.Generic;
using System.Linq;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Owns the block's live effect list and all effect iteration. The list instance is shared
    /// (returned by <see cref="LevelBlockBehavior.Effects"/>) because external systems read and
    /// mutate it indirectly (group movement, powerups) — it must stay the same reference.
    /// </summary>
    public class BlockEffectController
    {
        private readonly LevelBlockBehavior owner;
        private readonly List<BlockEffectBehavior> effects = new();

        private readonly List<LevelBlockBehavior> linkedBlocksBuffer = new();
        private readonly HashSet<LevelBlockBehavior> linkedBlocksSeen = new();

        public BlockEffectController(LevelBlockBehavior owner)
        {
            this.owner = owner;
        }

        public List<BlockEffectBehavior> Effects => effects;

        public void Add(BlockEffectBehavior effect)
        {
            int effectsCount = effects.Count;

            effects.Add(effect);

            effect.SetOrder(effectsCount);
            effect.OnCreated(owner);

            for (int i = 0; i < effectsCount; i++)
            {
                effects[i].OnNewEffectAddedToBlock(effect);
            }
        }

        public bool CanBeClicked()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                if (!effect.IsClickable())
                    return false;
            }

            return true;
        }

        /// <summary>First effect-supplied deny audio, else the default booster-denied sound.</summary>
        public AudioId GetDenyClickAudioId()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                var clickAudioId = effect.GetOverrideClickAudioId();
                if (clickAudioId != AudioId.None)
                    return clickAudioId;
            }

            return AudioId.Booster_Denied;
        }

        /// <summary>
        /// Aggregates the linked blocks of every active effect, deduped. Returns a shared reused
        /// buffer — callers MUST iterate/copy it before the next GetLinkedBlocks call and MUST NOT
        /// mutate or retain it.
        /// </summary>
        public IReadOnlyList<LevelBlockBehavior> GetLinkedBlocks()
        {
            linkedBlocksBuffer.Clear();
            linkedBlocksSeen.Clear();

            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                var blocksLinked = effect.GetLinkedBlocks();
                if (blocksLinked == null) continue;

                for (int i = 0; i < blocksLinked.Count; i++)
                {
                    LevelBlockBehavior block = blocksLinked[i];
                    if (block && linkedBlocksSeen.Add(block))
                        linkedBlocksBuffer.Add(block);
                }
            }

            return linkedBlocksBuffer;
        }

        public bool MoveMultiplyObjects()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                if (effect.MoveMultiplyObjects())
                    return true;
            }

            return false;
        }

        /// <summary>First active effect that overrides the pick-lift transform, else null (use the block model).</summary>
        public Transform GetModelLiftTransform()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                Transform liftTransform = effect.GetModelLiftTransform();
                if (liftTransform)
                    return liftTransform;
            }

            return null;
        }

        public void OnBlockEnteredGate(LevelBlockBehavior levelBlockBehavior, GateBehavior gateBehavior)
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                effect.OnBlockEnteredGate(levelBlockBehavior, gateBehavior);
            }
        }

        public void OnBlockDestructed(LevelBlockBehavior levelBlockBehavior)
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                effect.OnBlockDestructed(levelBlockBehavior);
            }
        }

        public void OnMapSpawnCompleted()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                effect.OnMapSpawnCompleted();
            }
        }

        /// <summary>Gate-passability gate from this block's active effects (gate's own check is done by the caller).</summary>
        public BlockGateState AllowGateEntered()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                var effectBlockState = effect.AllowGateEntered();
                if (effectBlockState != BlockGateState.Enterable)
                    return effectBlockState;
            }

            return BlockGateState.Enterable;
        }

        public BlockColor GetOverrideBlockColor(BlockColor originType)
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                BlockColor overrideColor = effect.GetOverrideBlockColor();
                if (overrideColor != originType)
                    return overrideColor;
            }

            return originType;
        }

        public bool AnyHidesWater()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                if (effect.HidesBlockWater)
                    return true;
            }

            return false;
        }

        public bool IsHeldByContainer()
        {
            foreach (BlockEffectBehavior effect in effects)
                if (effect.IsActive && effect is IGroupableEffect) return true;
            return false;
        }
        
        public bool HasEffect(BlockEffectType effectType)
        {
            if (effects.IsNullOrEmpty())
                return false;

            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                if (effect.Type == effectType)
                    return true;
            }

            return false;
        }

        public BlockEffectBehavior GetEffect(BlockEffectType effectType)
        {
            if (effects.IsNullOrEmpty())
                return null;

            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                if (effect.Type == effectType)
                    return effect;
            }

            return null;
        }

        public T GetEffect<T>(BlockEffectType effectType) where T : BlockEffectBehavior
        {
            if (effects.IsNullOrEmpty())
                return null;

            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;

                if (effect.Type == effectType)
                    return effect as T;
            }

            return null;
        }

        public List<BlockEffectBehavior> GetEffectsByRule(int amount, Func<BlockEffectBehavior, bool> effectRule, bool sortAscending = true)
        {
            return FindEffects(amount, effectRule, sortAscending);
        }

        public void DisableEffect(DisableSource source, int amount, Func<BlockEffectBehavior, bool> effectRule, bool sortAscending = true)
        {
            var effectsToDisable = FindEffects(amount, effectRule, sortAscending);

            foreach (var effect in effectsToDisable)
            {
                effect.DisableEffect(source);
                UnityEngine.Object.Destroy(effect.gameObject);
            }

            effects.RemoveAll(e => !e.IsActive);
        }

        public void DisableAll()
        {
            if (effects.IsNullOrEmpty())
                return;

            foreach (BlockEffectBehavior effect in effects)
            {
                if (effect.IsActive)
                    effect.OnDisabled(owner, DisableSource.Self);

                UnityEngine.Object.Destroy(effect.gameObject);
            }

            effects.Clear();
        }

        public bool HasActiveEffect()
        {
            if (effects.IsNullOrEmpty())
                return false;

            foreach (BlockEffectBehavior effect in effects)
            {
                if (effect.IsActive)
                    return true;
            }

            return false;
        }

        // --- Fill orchestration helpers (called by BlockFillController) ---

        public void NotifyBlockFilled(LevelBlockBehavior block)
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                effect.OnBlockFilled(block);
            }
        }

        public void NotifyFullBeforeAnimationFilled()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                effect.OnBlockFullBeforeAnimationFilled();
            }
        }

        public void NotifyAfterAnimationFilled()
        {
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                effect.OnBlockAfterAnimationFilled();
            }
        }

        /// <summary>Runs full-fill after-animation callbacks and returns whether all effects allow destruction.</summary>
        public bool NotifyFullAfterAnimationFilledAndIsDestructible()
        {
            bool isDestructible = true;
            foreach (BlockEffectBehavior effect in effects)
            {
                if (!effect.IsActive) continue;
                isDestructible &= effect.IsDestructible();
                effect.OnBlockFullAfterAnimationFilled();
            }

            return isDestructible;
        }

        private List<BlockEffectBehavior> FindEffects(int amount, Func<BlockEffectBehavior, bool> effectRule, bool sortAscending = true)
        {
            if (effects.IsNullOrEmpty())
                return new List<BlockEffectBehavior>();

            var query = effects
                .Where(e => e.IsActive && effectRule(e));
            var ordered = sortAscending
                ? query.OrderBy(e => e.EffectSortingOrder)
                : query.OrderByDescending(e => e.EffectSortingOrder);
            return ordered.Take(amount).ToList();
        }
    }
}
