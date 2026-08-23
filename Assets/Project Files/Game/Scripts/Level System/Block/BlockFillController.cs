using System.Collections.Generic;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Owns the block's fill state and dual-color point tracking, and orchestrates the fill flow.
    /// Holds the single authoritative <see cref="BlockState"/>; the facade and other collaborators
    /// read it through <see cref="State"/>. Reaches effects/visual directly and the owner facade for
    /// representation, events, destruction, and the level controller.
    /// </summary>
    public class BlockFillController
    {
        private readonly LevelBlockBehavior owner;
        private readonly LevelFigure figure;
        private readonly WaterVisualModule waterModule;
        private readonly BlockVisualController visual;
        private readonly BlockEffectController effects;
        private readonly float fillingWaterSpeed;

        // Per-color fill tracking for dual blocks.
        private int primaryColorFillPoint;
        private int secondaryColorFillPoint;
        private int primaryColorMaxPoint;
        private int secondaryColorMaxPoint;

        private BlockColor cachedSecondaryColor = BlockColor.None;
        private bool hasSecondaryColor;
        private BlockState state;

        private BlockColor pendingFillColor;
        private SimpleCallback fillCompleteCallback;

        public BlockFillController(LevelBlockBehavior owner, LevelFigure figure, WaterVisualModule waterModule,
            BlockVisualController visual, BlockEffectController effects, float fillingWaterSpeed)
        {
            this.owner = owner;
            this.figure = figure;
            this.waterModule = waterModule;
            this.visual = visual;
            this.effects = effects;
            this.fillingWaterSpeed = fillingWaterSpeed;
            fillCompleteCallback = () => OnFillWaterComplete(pendingFillColor);
        }

        public BlockState State => state;

        /// <summary>Block is fully filled when all colors have reached their max points.</summary>
        public bool IsFullFill =>
            primaryColorFillPoint >= primaryColorMaxPoint &&
            (!hasSecondaryColor || secondaryColorFillPoint >= secondaryColorMaxPoint);

        /// <summary>True once any water has been filled into the block (before it is reset/collected).</summary>
        public bool HasFillProgress => primaryColorFillPoint > 0 || secondaryColorFillPoint > 0;

        /// <summary>Total available points across all colors (for backward compatibility).</summary>
        public int AvailablePoint => (primaryColorMaxPoint - primaryColorFillPoint) +
                                     (secondaryColorMaxPoint - secondaryColorFillPoint);

        /// <summary>Resets fill points to the primary color total. Call after the figure is known.</summary>
        public void InitMaxPoints()
        {
            state = BlockState.Normal;
            primaryColorMaxPoint = figure.ActivePoints;
            secondaryColorMaxPoint = 0;
        }

        /// <summary>
        /// Available points for a color. Single-color blocks return the total available regardless of color.
        /// </summary>
        public int GetAvailablePoint(BlockColor color)
        {
            if (!hasSecondaryColor)
            {
                return primaryColorMaxPoint - primaryColorFillPoint;
            }

            if (color == owner.OriginColorConfig.Type)
                return primaryColorMaxPoint - primaryColorFillPoint;

            if (color == cachedSecondaryColor)
                return secondaryColorMaxPoint - secondaryColorFillPoint;

            return 0;
        }

        public BlockColor GetActiveBlockColor()
        {
            return effects.GetOverrideBlockColor(owner.OriginColorConfig.Type);
        }

        public BlockColor GetSecondaryBlockColor()
        {
            return hasSecondaryColor ? cachedSecondaryColor : BlockColor.None;
        }

        /// <summary>Matches the gate against the active color or (if dual) the secondary color.</summary>
        public bool MatchesActiveOrSecondary(BlockColor color)
        {
            return GetActiveBlockColor() == color || (hasSecondaryColor && cachedSecondaryColor == color);
        }

        /// <summary>
        /// Sets the secondary color and redistributes max points for a dual color block.
        /// Called by DualBlockEffectBehavior when the effect is applied.
        /// </summary>
        public void SetSecondaryColor(BlockColor secondaryColor)
        {
            cachedSecondaryColor = secondaryColor;
            hasSecondaryColor = secondaryColor != BlockColor.None;

            if (hasSecondaryColor)
            {
                int totalPoints = figure.ActivePoints;
                secondaryColorMaxPoint = owner.BlockConfig.Type.GetSecondColorFillAmount();
                primaryColorMaxPoint = totalPoints - secondaryColorMaxPoint;
            }
        }

        /// <summary>Clears the secondary color and restores all points to the primary color.</summary>
        public void ClearSecondaryColor()
        {
            cachedSecondaryColor = BlockColor.None;
            hasSecondaryColor = false;
            primaryColorMaxPoint = figure.ActivePoints;
            secondaryColorMaxPoint = 0;
            secondaryColorFillPoint = 0;
        }

        public void ResetFillProgress()
        {
            primaryColorFillPoint = 0;
            secondaryColorFillPoint = 0;
            state = BlockState.Normal;
        }

        /// <summary>Fill water of a specific color into the block.</summary>
        public void FillWater(BlockColor color, int collectPoints)
        {
            if (state != BlockState.Normal || collectPoints == 0) return;

            bool isPrimaryColor = color == GetActiveBlockColor();
            bool isSecondaryColor = hasSecondaryColor && (color == cachedSecondaryColor);

            if (!isPrimaryColor && !isSecondaryColor)
                return; // Color doesn't match this block

            state = BlockState.Filling;

            if (isPrimaryColor)
            {
                primaryColorFillPoint = Mathf.Min(primaryColorFillPoint + collectPoints, primaryColorMaxPoint);
            }
            else if (isSecondaryColor)
            {
                secondaryColorFillPoint = Mathf.Min(secondaryColorFillPoint + collectPoints, secondaryColorMaxPoint);
            }

            OnFillBlock();
        }

        /// <summary>Update the visual fill amount for a specific color.</summary>
        public void FillWaterVisual(BlockColor color, int collectPoints)
        {
            if (!owner.gameObject) return;
            PlayFillWaterSound(collectPoints);

            if (!hasSecondaryColor)
            {
                FillWaterVisualSingleColor(color, collectPoints);
            }
            else
            {
                FillWaterVisualDualColor(color, collectPoints);
            }
        }

        private void PlayFillWaterSound(int collectPoints)
        {
            var audioService = Services.AudioService;
            switch (collectPoints)
            {
                case 1:
                    audioService.PlaySound(AudioId.Block_Fill_water_Short);
                    break;
                case 2:
                    audioService.PlaySound(AudioId.Block_Fill_water_Medium);
                    break;
                case 3:
                case 4:
                case 5:
                    audioService.PlaySound(AudioId.Block_Fill_water_Long);
                    break;
                default:
                    audioService.PlaySound(AudioId.Block_Fill_water_SuperLong);
                    Tween.DelayedCall(collectPoints * fillingWaterSpeed,
                        () =>
                        {
                            audioService.StopSound();
                            HapticController.Stop();
                        });
                    break;
            }
        }

        private void FillWaterVisualSingleColor(BlockColor color, int collectPoints)
        {
            float percent = figure.GetWaterFillPercent(primaryColorFillPoint);
            if (Application.isPlaying)
            {
                pendingFillColor = color;
                waterModule?.FillWater(percent, collectPoints * fillingWaterSpeed, fillCompleteCallback);
            }
            else
            {
                waterModule?.SetFillImmediate(percent);
                state = BlockState.Normal;
                if (IsFullFill)
                {
                    state = BlockState.Collected;
                }
            }
        }

        private void FillWaterVisualDualColor(BlockColor color, int collectPoints)
        {
            if (!visual.HasDualVisual())
            {
                Debug.LogWarning("Dual visual not set for dual color block!");
                return;
            }

            bool isPrimaryColor = (color == GetActiveBlockColor());
            int currentFillPoint = isPrimaryColor ? primaryColorFillPoint : secondaryColorFillPoint;
            int maxPoint = isPrimaryColor ? primaryColorMaxPoint : secondaryColorMaxPoint;

            float percent = (float)currentFillPoint / maxPoint;

            if (Application.isPlaying)
            {
                pendingFillColor = color;
                visual.FillDualWater(color, percent, collectPoints * fillingWaterSpeed, fillCompleteCallback);
            }
            else
            {
                visual.SetDualWaterFillImmediate(color, percent);
                state = BlockState.Normal;
                if (IsFullFill)
                {
                    state = BlockState.Collected;
                }
            }
        }

        private void OnFillWaterComplete(BlockColor color)
        {
            if (!IsFullFill)
            {
                state = BlockState.Normal;
                effects.NotifyAfterAnimationFilled();
                visual.BubbleFadeOff();
                owner.OwnerRepresentation.EvaluateGameWinLose();
                return;
            }

            bool isDestructible = effects.NotifyFullAfterAnimationFilledAndIsDestructible();

            owner.RaiseFullFilledAfterAnimation();
            NotifyBlockFulled(color);

            owner.OwnerRepresentation?.EvaluateGameWinLose();
            if (!isDestructible) return;

            Services.AudioService.PlaySound(AudioId.Block_Clear);
            state = BlockState.Collected;
            owner.LevelController.OnBlockDestructed(owner);

            owner.BeginDestructAnimation();
        }

        private void NotifyBlockFulled(BlockColor color)
        {
            List<LevelBlockBehavior> levelBlocks = owner.OwnerRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in levelBlocks)
            {
                List<BlockEffectBehavior> blockEffects = block.Effects;
                foreach (BlockEffectBehavior effect in blockEffects)
                {
                    if (!effect.IsActive) continue;
                    effect.OnBlockFullFilledAfterAnimationGlobal(owner, color);
                }
            }

            List<GateBehavior> levelGates = owner.OwnerRepresentation.EnvironmentSpawner.Gates;
            foreach (GateBehavior gate in levelGates)
            {
                List<GateEffectBehavior> gateEffects = gate.Effects;
                for (int i = gateEffects.Count - 1; i >= 0; i--)
                {
                    GateEffectBehavior effect = gateEffects[i];
                    if (!effect || !effect.IsActive) continue;
                    effect.OnBlockFullFilledAfterAnimationGlobal(owner, color);
                }
            }
        }

        private void OnFillBlock()
        {
            effects.NotifyBlockFilled(owner);

            if (IsFullFill)
            {
                OnBlockFullFilled();
            }
        }

        private void OnBlockFullFilled()
        {
            owner.RaiseBlockCollected();

            effects.NotifyFullBeforeAnimationFilled();

            List<LevelBlockBehavior> levelBlocks = owner.OwnerRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in levelBlocks)
            {
                List<BlockEffectBehavior> blockEffects = block.Effects;
                foreach (BlockEffectBehavior effect in blockEffects)
                {
                    if (!effect.IsActive) continue;
                    effect.OnBlockFullFilledBeforeAnimationGlobal(owner);
                }
            }

            List<GateBehavior> levelGates = owner.OwnerRepresentation.EnvironmentSpawner.Gates;
            foreach (GateBehavior gate in levelGates)
            {
                List<GateEffectBehavior> gateEffects = gate.Effects;
                for (int i = gateEffects.Count - 1; i >= 0; i--)
                {
                    GateEffectBehavior effect = gateEffects[i];
                    if (!effect || !effect.IsActive) continue;
                    effect.OnBlockFullFilledBeforeAnimationGlobal(owner);
                }
            }
        }
    }
}
