using System.Collections.Generic;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class HammerPowerUpBehavior : PowerUpBehavior
    {
        private const string TIMER_UNIQUE_NAME = "hammer";
        private const float RECHECK_INTERVAL = 0.5f;
        private static readonly int PARTICLE_HASH = "Booster_Hammer_Impact".GetHashCode();

        [SerializeField] private GameObject hammerModel;
        private HammerPowerUpConfig hammerConfig;
        private List<LevelBlockBehavior> breakableBlocks = new List<LevelBlockBehavior>();

        // Pooling system
        private Dictionary<LevelBlockBehavior, GameObject> blockVisuals = new ();

        private Queue<GameObject> visualPool = new Queue<GameObject>();
        private float recheckTimer;

        public override void Init()
        {
            hammerConfig = (HammerPowerUpConfig)PowerUpConfig;

            // Pre-create pool objects
            for (int i = 0; i < 10; i++)
            {
                GameObject visual = Object.Instantiate(hammerConfig.HammerTargetVisual);
                visual.SetActive(false);
                visualPool.Enqueue(visual);
            }
            hammerModel.SetActive(false);
            SetPauseGameplayTimerWhenUse(true);
        }

        public override bool Activate()
        {
            return false;
        }

        public override bool IsHasAnyTarget()
        {
            var representation = LevelController.Instance?.LevelRepresentation;
            if (representation == null) return false;

            foreach (var block in representation.ActiveBlocks)
            {
                if (IsBreakable(block))
                    return true;
            }

            return false;
        }

        public override bool OnSelected(out string notifyWhenNotFoundTarget)
        {
            breakableBlocks.Clear();
            blockVisuals.Clear();
            recheckTimer = RECHECK_INTERVAL;

            // find all block breakable
            var allBlocks = LevelController.Instance.LevelRepresentation.ActiveBlocks;
            foreach (var block in allBlocks)
            {
                if (IsBreakable(block))
                {
                    breakableBlocks.Add(block);

                    // add visual target
                    GameObject visual = GetVisualFromPool();
                    Bounds bounds = block.Figure.GetHorizontalCenterBounds();
                    visual.transform.position = block.transform.position + bounds.center;
                    visual.SetActive(true);
                    blockVisuals[block] = visual;
                }
            }

            bool hasTarget = breakableBlocks.Count > 0;
            if (!hasTarget)
            {
                notifyWhenNotFoundTarget = config.GetOutOfTargetText();
                ReleaseAllVisuals();
                breakableBlocks.Clear();
                blockVisuals.Clear();
                ResumeGameplayTimerOnUse();
                IsSelected = false;
                isDirty = true;
                return false;
            }

            base.OnSelected(out notifyWhenNotFoundTarget);
            return true;
        }


        public override void OnDeselected()
        {
            base.OnDeselected();
            ReleaseAllVisuals();
            breakableBlocks.Clear();
            blockVisuals.Clear();
        }

        private void Update()
        {
            if (!IsSelected || IsBusy || breakableBlocks.Count == 0)
            {
                return;
            }

            recheckTimer -= Time.deltaTime;
            if (recheckTimer > 0f)
            {
                return;
            }

            recheckTimer = RECHECK_INTERVAL;
            CleanupInvalidBreakableBlocks();
        }

        private GameObject GetVisualFromPool()
        {
            if (visualPool.Count > 0)
            {
                return visualPool.Dequeue();
            }

            // Create new if pool is empty
            return Instantiate(hammerConfig.HammerTargetVisual);
        }

        private void ReturnVisualToPool(GameObject visual)
        {
            visual.SetActive(false);
            visualPool.Enqueue(visual);
        }

        private bool IsBreakable(LevelBlockBehavior block)
        {
            if(!block) return false;
            if (!block.IsBoosterTargetable) return false;
            if (block.IsFullFill) return false;
            var effects = block.Effects;
            foreach (var effect in effects)
            {
                if (IsAvailableEffect(effect))
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupInvalidBreakableBlocks()
        {
            for (int i = breakableBlocks.Count - 1; i >= 0; i--)
            {
                LevelBlockBehavior block = breakableBlocks[i];
                if (IsBreakable(block))
                {
                    continue;
                }

                breakableBlocks.RemoveAt(i);
                if (blockVisuals.TryGetValue(block, out GameObject visual))
                {
                    ReturnVisualToPool(visual);
                    blockVisuals.Remove(block);
                }
            }
        }

        private void ReleaseAllVisuals()
        {
            foreach (var visual in blockVisuals.Values)
            {
                if (visual)
                {
                    ReturnVisualToPool(visual);
                }
            }
        }

        public override bool ApplyToElement(IClickableObject clickableObject, Vector3 clickPosition)
        {
            CleanupInvalidBreakableBlocks();
            if (clickableObject is not LevelBlockBehavior levelBlockBehavior) return false;
            if (!levelBlockBehavior || levelBlockBehavior.IsFullFill) return false;
            if (!breakableBlocks.Contains(levelBlockBehavior)) return false;

            LevelController.Instance.GameplayTimer.Pause(TIMER_UNIQUE_NAME);
            Vector3 centerPosition = levelBlockBehavior.transform.position +
                                     levelBlockBehavior.Figure.GetHorizontalCenterBounds().center;
            Vector3 particlePosition = centerPosition + new Vector3(0, 1f, 0);
            transform.position = particlePosition;
            
            List<BlockEffectBehavior> effects = levelBlockBehavior.GetEffectsByRule(1, IsAvailableEffect, false);
            if (effects.Count == 0) return false;
            
            RaycastController.Disable("HammerPowerUp");
            IsBusy = true;
            hammerModel.SetActive(true);
            Services.AudioService.PlaySound(AudioId.Booster_PreHammer);
            Tween.DelayedCall(0.86f, () =>
            {
                Services.AudioService.PlaySound(AudioId.Booster_Hammer2);
            });
            Tween.DelayedCall(1.44f, () =>
            {
                global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.Hammer);
                
                ParticleCase particleCase = ParticlesController.PlayParticle(PARTICLE_HASH);
                particleCase.SetPosition(particlePosition);

                foreach (var effect in effects)
                {
                    if (effect.IsActive)
                    {
                        effect.DisableEffect(DisableSource.Hammer);
                    }
                }
                LevelController.Instance.CameraController.StartShake(hammerConfig.ShakeDuration, hammerConfig.ShakeStrength);
                RaycastController.Enable("HammerPowerUp");
                LevelController.Instance.GameplayTimer.Resume(TIMER_UNIQUE_NAME);
                IsBusy = false;
                hammerModel.SetActive(false);
            });
            return true;
        }
        
        private bool IsAvailableEffect(BlockEffectBehavior effect)
        {
            if (!effect || !effect.IsActive) return false;
            if (!effect.IsDestructible()) return false;
            if (!effect.gameObject.activeInHierarchy) return false;

            var typesCanDestroy = hammerConfig.TypesCanDestroy;
            for (int i = 0; i < typesCanDestroy.Length; i++)
            {
                if (typesCanDestroy[i] == effect.Type)
                {
                    return true;
                }
            }

            return false;
        }

    }
}