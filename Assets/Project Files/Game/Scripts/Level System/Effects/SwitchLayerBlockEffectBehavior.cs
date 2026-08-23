using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Layered block whose two colors trade places every time ANY block on the board is cleared:
    /// the inner (fill/matchable) color flows out to become the outer body color and the outer color
    /// retreats inside. Shares the two-stage peel of <see cref="LayerBlockEffectBehavior"/>; the only
    /// added rule is the per-clear swap, driven from <see cref="OnBlockFullFilledAfterAnimationGlobal"/>.
    /// </summary>
    public sealed class SwitchLayerBlockEffectBehavior : BlockEffectBehavior<SwitchLayerBlockEffectData>
    {
        private GameObject effectObject;
        private MeshRenderer effectMeshRenderer;

        private bool isCollected;
        private bool isReset;
        private CustomEasingFunction disappearEasing;

        private BlockColorData activeColor;
        private BlockColorData outerColor;

        private TweenCase switchMoveTween;

        // Layer-swap "juice": the inner overlay dips into the block then springs back out, and the two layers'
        // materials are swapped at the hidden low point. Tuned short so it never blocks the next move.
        private const float SWITCH_DIP_OFFSET = 0.4f;
        private const float SWITCH_DIP_DOWN_DURATION = 0.12f;
        private const float SWITCH_DIP_UP_DURATION = 0.22f;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            LayerBlockEffectConfig config = GetConfig<LayerBlockEffectConfig>();
            BlockType blockType = blockBehavior.BlockConfig.Type;
            BlockTheme blockTheme = blockBehavior.BlockTheme;
            if (config == null || !config.TryGetInnerPrefab(blockTheme, blockType, out GameObject innerPrefab))
            {
                Debug.LogError($"Inner prefab for switch layer block type {blockType} (theme {blockTheme}) is not assigned.", this);
                DisableEffect();
                return;
            }

            // Outer = the block's spawn color; inner/active = the configured layered color.
            outerColor = blockBehavior.OriginColorConfig;
            activeColor = LevelController.Instance.GetBlockColorData(Data.layeredBlockColor);

            effectObject = Instantiate(innerPrefab, linkedBlock.MeshRenderer.transform);
            effectObject.transform.localPosition = Vector3.zero;
            effectMeshRenderer = effectObject.GetComponent<MeshRenderer>();

            ApplyLayerColors();

            if (Application.isPlaying)
            {
                disappearEasing = Ease.GetCustomEasingFunction("BlockDisappear");
            }
        }

        public override BlockColor GetOverrideBlockColor()
        {
            // After collecting the inner layer the block lives on as a plain block of whatever color is
            // currently outer — which may differ from the spawn (origin) color after swaps, so it cannot
            // fall back to base/origin like the non-switching layer block does.
            return isCollected ? outerColor.Type : activeColor.Type;
        }

        /// <summary>
        /// Any block clearing on the board flips this block's two layers: inner becomes outer and outer
        /// becomes inner. Only allowed while the inner block has no fill progress — a partially filled
        /// block keeps its active color so painted water is never silently discarded.
        /// </summary>
        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            if (isCollected || !isActive) return;
            if (!gameObject.activeInHierarchy) return;
            if (linkedBlock.HasFillProgress) return;

            (activeColor, outerColor) = (outerColor, activeColor);

            if (!Application.isPlaying)
            {
                ApplyLayerColors();
                return;
            }

            PlaySwitchSwapAnimation();
        }

        /// <summary>
        /// Juicy layer swap: the inner overlay dips into the block then springs back out, and the swapped
        /// materials are applied at the hidden low point so the two layers appear to trade places. The real
        /// shared materials are used (not a color-only tween) so emission and every other property stay correct.
        /// </summary>
        private void PlaySwitchSwapAnimation()
        {
            StopSwitchSwapAnimation();

            Transform innerTransform = effectObject.transform;
            switchMoveTween = innerTransform.DOLocalMoveY(-SWITCH_DIP_OFFSET, SWITCH_DIP_DOWN_DURATION)
                .SetEasing(Ease.Type.QuadIn)
                .OnComplete(() =>
                {
                    if (!effectObject) return;
                    ApplyLayerColors();
                    switchMoveTween = innerTransform.DOLocalMoveY(0f, SWITCH_DIP_UP_DURATION)
                        .SetEasing(Ease.Type.BackOut);
                });
        }

        private void StopSwitchSwapAnimation()
        {
            switchMoveTween.KillActive();
            if (effectObject)
                effectObject.transform.localPosition = Vector3.zero;
        }

        /// <summary>Paints the overlay/glass/water with the active (inner) color and the block body with the outer color.</summary>
        private void ApplyLayerColors()
        {
            if (effectMeshRenderer)
                effectMeshRenderer.material = activeColor.Material;

            linkedBlock.ChangeBodyMaterial(outerColor.Material);
            linkedBlock.SetInnerColor(activeColor);
        }

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            // The inner overlay is parented under the block mesh (not under this effect), so it must follow the
            // resolved visibility too — keeps it hidden under Ice/Container and reveals only once they release.
            if (effectObject)
                effectObject.SetActive(visible);
        }

        public override bool IsDestructible()
        {
            return isCollected;
        }

        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            // Same recovery as the plain layer block: fill/collect the current inner block on revive.
            if (loseReason != LoseReason.BlockSwitchLayerFailed) return;
            LevelController.Instance.ForceCollectBlock(linkedBlock);
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            if (isCollected) return;
            isCollected = true;
            StopSwitchSwapAnimation();
            ResetBlockColor();
            // Rebind the block's permanent color to the current outer color. After swaps the outer may differ
            // from the spawn color, and once this effect disables (below), gate-match/fill logic falls back to
            // the block's origin color — so the origin itself must become the outer color, otherwise a gate of
            // the old spawn color could still fill this now plain single-color block.
            linkedBlock.SetColor(outerColor);
            effectObject.transform.DOScale(new Vector3(0f, 0.6f, 0f), 0.25f)
                .OnComplete(() =>
                {
                    if (!effectObject) return;
                    effectObject.SetActive(false);
                    DisableEffect();
                })
                .SetCustomEasing(disappearEasing);

            Transform meshWaterTransform = linkedBlock.MeshWater.transform;
            Vector3 originalScale = meshWaterTransform.localScale;
            meshWaterTransform.DOScale(new Vector3(0f, 0.6f, 0f), 0.25f)
                .OnComplete(() =>
                {
                    if (meshWaterTransform)
                    {
                        meshWaterTransform.localScale = originalScale;
                    }
                })
                .SetCustomEasing(disappearEasing);
        }

        private void ResetBlockColor()
        {
            if (isReset) return;
            isReset = true;
            LevelBlockBehavior blockBehavior = linkedBlock;
            blockBehavior.ResetFillProgress();
            blockBehavior.SetInnerColor();
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            StopSwitchSwapAnimation();
            ResetBlockColor();
            if (effectObject)
            {
                Destroy(effectObject);
            }
        }
    }
}
