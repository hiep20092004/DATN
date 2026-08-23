using DG.Tweening;
using WaterFlow.Core;
using UnityEngine;
using Ease = DG.Tweening.Ease;
using Tween = WaterFlow.Core.Tween;

namespace WaterFlow.Game
{
    public sealed class HiddenBlockEffectBehavior : BlockEffectBehavior<HiddenBlockEffectData>
    {
        [SerializeField] Material hiddenMaterial;
        [SerializeField] Material hiddenGlassMaterial;

        private Material storedMaterial;
        private Material storedGlassMaterial;
        private Material meshMaterial;
        private Material glassMaterial;
        private Material hiddenGlassMaterialInstance;

        private HiddenBlockEffectConfig config;
        private bool isRevealed;
        private bool suppressRevealUntilFillAnimationComplete;
        private bool releaseHidePendingFill;
        private Tweener revealPunchTween;
        private Tweener hidePunchTween;
        private TweenCase pendingHideTween;

        public override bool HidesBlockWater => IsActive && !isRevealed;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<HiddenBlockEffectConfig>();

            var meshRenderer = blockBehavior.MeshRenderer;
            var meshGlass = blockBehavior.MeshGlass;
            if (Application.isPlaying)
            {
                meshMaterial = meshRenderer.material;
                glassMaterial = meshGlass.material;
            }
            else
            {
                meshMaterial = meshRenderer.sharedMaterial;
                glassMaterial = meshGlass.sharedMaterial;
            }

            blockBehavior.SetVisibleBlockWater(false);

            storedMaterial = meshMaterial;
            storedGlassMaterial = glassMaterial;

            if (hiddenMaterial) meshRenderer.material = hiddenMaterial;
            if (hiddenGlassMaterial) meshGlass.material = hiddenGlassMaterial;

            if (hiddenGlassMaterial && Application.isPlaying)
            {
                hiddenGlassMaterialInstance = meshGlass.material;
                if (config != null && config.TryGetGlassVisualsData(blockBehavior.BlockConfig.Type, out HiddenGlassVisualsData glassData))
                {
                    hiddenGlassMaterialInstance.SetTextureScale(ShaderId.BASE_MAP_SHADER_ID, glassData.Tiling);
                    hiddenGlassMaterialInstance.SetTextureOffset(ShaderId.BASE_MAP_SHADER_ID, glassData.Offset);
                }
            }

            isRevealed = false;
        }

        public override void OnBlockPicked(LevelBlockBehavior blockBehavior)
        {
            if (!isActive || !blockBehavior)
                return;

            suppressRevealUntilFillAnimationComplete = false;
            releaseHidePendingFill = false;
            pendingHideTween.KillActive();
            hidePunchTween?.Kill();
            RevealVisual(blockBehavior);
        }

        public override void OnBlockFilled(LevelBlockBehavior blockBehavior)
        {
            if (!isActive || !blockBehavior)
                return;

            pendingHideTween.KillActive();
            hidePunchTween?.Kill();
            revealPunchTween?.Kill();
            releaseHidePendingFill = false;
            suppressRevealUntilFillAnimationComplete = true;
            RevealVisual(blockBehavior, instant: true);
        }

        private void RevealVisual(LevelBlockBehavior blockBehavior, bool instant = false)
        {
            if (isRevealed)
                return;

            isRevealed = true;

            blockBehavior.ChangeBodyMaterial(storedMaterial);
            blockBehavior.ChangeGlassMaterial(storedGlassMaterial);
            blockBehavior.SetVisibleBlockWater(true);

            Transform model = blockBehavior.ModelParentTransform;
            if (!model)
                return;

            revealPunchTween?.Kill();
            model.localScale = Vector3.one;

            if (instant || config == null)
                return;

            revealPunchTween = model
                .DOPunchScale(
                    Vector3.one * config.RevealPunchStrength,
                    config.RevealPunchDuration,
                    config.RevealPunchVibrato,
                    config.RevealPunchElasticity)
                .SetEase(Ease.OutSine);
        }

        public override void OnBlockAfterAnimationFilled()
        {
            if (!isActive)
                return;

            OnFillAnimationCompleted(applyHiddenVisual: true);
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            if (!isActive)
                return;

            OnFillAnimationCompleted(applyHiddenVisual: false);
        }

        private void OnFillAnimationCompleted(bool applyHiddenVisual)
        {
            suppressRevealUntilFillAnimationComplete = false;
            releaseHidePendingFill = false;
            pendingHideTween.KillActive();
            revealPunchTween?.Kill();

            if (!applyHiddenVisual)
                return;

            ApplyHiddenVisual();
        }

        public override void OnBlockReleased(LevelBlockBehavior blockBehavior, Vector2Int snapTargetPosition)
        {
            if (!isActive || !blockBehavior)
                return;

            if (!isRevealed)
                return;

            releaseHidePendingFill = true;
            pendingHideTween.KillActive();
            revealPunchTween?.Kill();

            if (WillCollectWaterFromGate(blockBehavior))
                return;

            releaseHidePendingFill = false;
            ApplyHiddenVisual();
        }

        private static bool WillCollectWaterFromGate(LevelBlockBehavior blockBehavior)
        {
            if (!blockBehavior || blockBehavior.IsFullFill)
                return false;

            LevelController levelController = blockBehavior.LevelController;
            LevelRepresentation levelRepresentation = levelController?.LevelRepresentation;
            LevelEnvironmentSpawner environmentSpawner = levelRepresentation?.EnvironmentSpawner;
            if (environmentSpawner == null)
                return false;

            var nearGate = environmentSpawner.NearGate(blockBehavior);
            if (!nearGate.HasValue)
                return false;

            var (direction, gateBehavior) = nearGate.Value;
            if (direction == GateDirection.Type.None || !gateBehavior)
                return false;

            if (blockBehavior.OnGateEntered(gateBehavior) != BlockGateState.Enterable)
                return false;

            BlockColor gateColor = gateBehavior.GetActiveColor();
            int availableForColor = blockBehavior.GetAvailablePoint(gateColor);
            int consumePoint = Mathf.Min(gateBehavior.GetActivePoint(), availableForColor);
            return consumePoint > 0;
        }

        private void ApplyHiddenVisual()
        {
            if (!isActive || !linkedBlock)
                return;

            if (!hiddenMaterial || !hiddenGlassMaterial)
                return;

            isRevealed = false;

            linkedBlock.ChangeBodyMaterial(hiddenMaterial);
            linkedBlock.ChangeGlassMaterial(hiddenGlassMaterialInstance != null ? hiddenGlassMaterialInstance : hiddenGlassMaterial);
            linkedBlock.SetVisibleBlockWater(false);

            Transform model = linkedBlock.ModelParentTransform;
            if (!model)
                return;

            model.localScale = Vector3.one;
            hidePunchTween?.Kill();
            hidePunchTween = model
                .DOPunchScale(
                    -Vector3.one * config.HidePunchStrength,
                    config.HidePunchDuration,
                    config.HidePunchVibrato,
                    config.HidePunchElasticity)
                .SetEase(Ease.OutSine);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            pendingHideTween.KillActive();
            revealPunchTween?.Kill();
            hidePunchTween?.Kill();
            isRevealed = false;
            suppressRevealUntilFillAnimationComplete = false;
            releaseHidePendingFill = false;

            if (blockBehavior && blockBehavior.ModelParentTransform)
                blockBehavior.ModelParentTransform.localScale = Vector3.one;

            if (!blockBehavior)
                return;

            blockBehavior.ChangeBodyMaterial(storedMaterial);
            blockBehavior.ChangeGlassMaterial(storedGlassMaterial);
            blockBehavior.SetVisibleBlockWater(true);
            hiddenGlassMaterialInstance = null;
        }
    }
}
