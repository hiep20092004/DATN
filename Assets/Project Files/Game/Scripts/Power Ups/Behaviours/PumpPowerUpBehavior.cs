using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class PumpPowerUpBehavior : PowerUpBehavior
    {
        private const string TIMER_UNIQUE_NAME = "pump";

        [SerializeField] private PumpVisualEffect pumpVisualEffect;
        [SerializeField] private float selectMoveToCameraDistance = 2f;
        [SerializeField] private SpriteRenderer mask;
        
        
        private PumpPowerUpConfig pumpConfig;
        private List<LevelBlockBehavior> availableBlocks = new List<LevelBlockBehavior>();
        private readonly Dictionary<LevelBlockBehavior, Vector3> originalModelPositions = new Dictionary<LevelBlockBehavior, Vector3>();

        public override void Init()
        {
            pumpConfig = (PumpPowerUpConfig)PowerUpConfig;
            
            // Initialize visual effect
            if (pumpVisualEffect)
            {
                pumpVisualEffect.Init();
            }
            
            SetPauseGameplayTimerWhenUse(true);
        }

        public override bool Activate()
        {
            return false;
        }

        public override bool IsHasAnyTarget()
        {
            var allBlocks = LevelController.Instance?.LevelRepresentation?.ActiveBlocks;
            if (allBlocks == null) return false;

            var typesCanDestroy = pumpConfig.TypesCanDestroy.ToList();
            foreach (var block in allBlocks)
            {
                if (IsPumpTargetBlock(block, typesCanDestroy))
                    return true;
            }

            return false;
        }

        public override bool OnSelected(out string notifyWhenNotFoundTarget)
        {
            availableBlocks.Clear();
            RestoreBlocksPosition();
            CollectPumpTargetBlocks(availableBlocks);

            bool hasTarget = availableBlocks.Count > 0;
            if (!hasTarget)
            {
                notifyWhenNotFoundTarget = config.GetOutOfTargetText();
                ResumeGameplayTimerOnUse();
                IsSelected = false;
                isDirty = true;
                if (mask)
                {
                    mask.DOKill();
                    Color maskColor = mask.color;
                    maskColor.a = 0f;
                    mask.color = maskColor;
                    mask.gameObject.SetActive(false);
                }
                availableBlocks.Clear();
                return false;
            }

            base.OnSelected(out notifyWhenNotFoundTarget);
            if (mask && hasTarget)
            {
                mask.gameObject.SetActive(true);
                Color maskColor = mask.color;
                maskColor.a = 0f;
                mask.color = maskColor;
                mask.DOFade(pumpConfig.AlphaOnSelect, pumpConfig.MaskFadeDuration).SetUpdate(true);
            }
            MoveBlocksTowardCamera();
            return hasTarget;
        }

        public override void OnDeselected()
        {
            base.OnDeselected();
            RestoreBlocksPosition();
            if (mask)
            {
                mask.DOKill();
                Color maskColor = mask.color;
                maskColor.a = 0f;
                mask.color = maskColor;
            }
            availableBlocks.Clear();
        }

        private void CollectPumpTargetBlocks(List<LevelBlockBehavior> into)
        {
            var allBlocks = LevelController.Instance.LevelRepresentation.ActiveBlocks;
            var typesCanDestroy = pumpConfig.TypesCanDestroy.ToList();
            foreach (var block in allBlocks)
            {
                if (IsPumpTargetBlock(block, typesCanDestroy))
                    into.Add(block);
            }
        }

        private bool IsPumpTargetBlock(LevelBlockBehavior block, List<BlockEffectType> typesCanDestroy)
        {
            return IsAvailableBlock(block, typesCanDestroy) && HasEnoughWaterInGates(block);
        }

        public override bool ApplyToElement(IClickableObject clickableObject, Vector3 clickPosition)
        {
            if (clickableObject is not LevelBlockBehavior levelBlockBehavior) return false;
            if (!levelBlockBehavior || levelBlockBehavior.IsFullFill) return false;
            if (!availableBlocks.Contains(levelBlockBehavior)) return false;

            // Check if there's enough water in gates
            if (!HasEnoughWaterInGates(levelBlockBehavior))
            {
                // TODO: Show "not enough water" feedback
                return false;
            }

            LevelController.Instance.GameplayTimer.Pause(TIMER_UNIQUE_NAME);
            RaycastController.Disable("PumpPowerUp"); // Freeze input

            BlockColor blockColor = levelBlockBehavior.GetColorFromPosition(clickPosition);
            // Play sound if configured
            if (pumpConfig.ActivateSound)
            {
                Services.AudioService.PlayAudio(string.Empty, pumpConfig.ActivateSound);
            }

            IsBusy = true;

            // Setup pump visual - spawn at origin (0,0,0) and look towards target block
            if (pumpVisualEffect)
            {
                pumpVisualEffect.Setup(levelBlockBehavior);
                
                // Play appear animation
                pumpVisualEffect.PlayAppearAnimation(() =>
                {
                    LevelController.Instance.ForceCollectBlock(levelBlockBehavior, blockColor); 
                }, () =>
                {
                    RaycastController.Enable("PumpPowerUp"); // Unfreeze input
                    LevelController.Instance.GameplayTimer.Resume(TIMER_UNIQUE_NAME);
                    IsBusy = false;
                    if (pumpVisualEffect)
                    {
                        pumpVisualEffect.PlayDisappearAnimation();
                    }
                });
            }
            else
            {
                // No visual effect, just start pumping
                DOVirtual.DelayedCall(0.2f, () =>
                {
                    LevelController.Instance.ForceCollectBlock(levelBlockBehavior, blockColor);
                });
            }

            // // Wait for fill animation to complete, then disappear and resume
            // float animDuration = CalculateAnimationDuration(levelBlockBehavior);
            // Tween.DelayedCall(animDuration + 0.3f, () =>
            // {
            //     if (pumpVisualEffect)
            //     {
            //         pumpVisualEffect.PlayDisappearAnimation(OnPumpComplete);
            //     }
            //     else
            //     {
            //         OnPumpComplete();
            //     }
            // });

            return true;
        }
        

        /// <summary>
        /// Check if block can be pumped based on its effects
        /// </summary>
        private bool IsAvailableBlock(LevelBlockBehavior block, List<BlockEffectType> availableEffects)
        {
            if (!block) return false;
            if (!block.IsBoosterTargetable) return false;
            if (block.IsFullFill) return false;

            // Check if block has any blocking effects
            var effects = block.Effects;
            if (effects.IsNullOrEmpty()) return true;
            
            foreach (var effect in effects)
            {
                if (!effect.IsActive) continue;

                if (!availableEffects.Contains(effect.Type))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if there's enough water in gates for the block's colors
        /// </summary>
        private bool HasEnoughWaterInGates(LevelBlockBehavior block)
        {
            var gates = LevelController.Instance.LevelRepresentation.EnvironmentSpawner.Gates;

            // Check primary color
            BlockColor primaryColor = block.GetActiveBlockColor();
            int primaryNeeded = block.GetAvailablePoint(primaryColor);
            if (primaryNeeded > 0)
            {
                int primaryAvailable = GetTotalWaterAvailable(gates, primaryColor);
                if (primaryAvailable < primaryNeeded) return false;
            }

            // Check secondary color (for dual blocks)
            BlockColor secondaryColor = block.GetSecondaryBlockColor();
            if (secondaryColor != BlockColor.None)
            {
                int secondaryNeeded = block.GetAvailablePoint(secondaryColor);
                if (secondaryNeeded > 0)
                {
                    int secondaryAvailable = GetTotalWaterAvailable(gates, secondaryColor);
                    if (secondaryAvailable < secondaryNeeded) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get total water available for a color across all gates
        /// </summary>
        private int GetTotalWaterAvailable(List<GateBehavior> gates, BlockColor color)
        {
            int total = 0;
            var waterInfoBuffer = new List<(int amount, int queueIndex)>();
            foreach (var gate in gates)
            {
                gate.GetWaterInfoForColor(color, waterInfoBuffer);
                foreach (var (amount, _) in waterInfoBuffer)
                {
                    total += amount;
                }
            }

            return total;
        }

        
        public override void OnLevelEnded()
        {
            base.OnLevelEnded();
            RestoreBlocksPosition();
            CleanupPumpVisual();
        }
        
        public override void ResetBehavior()
        {
            base.ResetBehavior();
            RestoreBlocksPosition();
            CleanupPumpVisual();
        }

        private void MoveBlocksTowardCamera()
        {
            if(availableBlocks.Count == 0) return;
            
            CameraController cameraController = LevelController.Instance.CameraController;
            if (cameraController == null || Mathf.Approximately(selectMoveToCameraDistance, 0f))
                return;

            Vector3 towardCamera = cameraController.GetDirectionTowardCamera() * selectMoveToCameraDistance;
            for (int i = 0; i < availableBlocks.Count; i++)
            {
                LevelBlockBehavior block = availableBlocks[i];
                if (!block || block.ModelParentTransform == null)
                    continue;

                if (!originalModelPositions.ContainsKey(block))
                {
                    originalModelPositions.Add(block, block.ModelParentTransform.position);
                }

                block.ModelParentTransform.position = originalModelPositions[block] + towardCamera;
            }
        }

        private void RestoreBlocksPosition()
        {
            foreach (KeyValuePair<LevelBlockBehavior, Vector3> entry in originalModelPositions)
            {
                LevelBlockBehavior block = entry.Key;
                if (!block || block.ModelParentTransform == null)
                    continue;

                block.ModelParentTransform.position = entry.Value;
            }

            originalModelPositions.Clear();
        }
        
        /// <summary>
        /// Clean up pump visual resources
        /// </summary>
        private void CleanupPumpVisual()
        {
            if (pumpVisualEffect != null)
            {
                pumpVisualEffect.Cleanup();
            }
        }
    }
}