using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class LayerBlockEffectBehavior : BlockEffectBehavior<LayeredBlockEffectData>
    {
        private GameObject effectObject;

        private LayerBlockEffectConfig config;
        private bool isCollected;
        private bool isReset;
        private CustomEasingFunction disappearEasing;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<LayerBlockEffectConfig>();
            BlockType blockType = blockBehavior.BlockConfig.Type;
            BlockTheme blockTheme = blockBehavior.BlockTheme;
            if (config == null || !config.TryGetInnerPrefab(blockTheme, blockType, out GameObject innerPrefab))
            {
                Debug.LogError($"Inner prefab for layer block type {blockType} (theme {blockTheme}) is not assigned.", this);
                DisableEffect();
                return;
            }

            effectObject = Instantiate(innerPrefab, linkedBlock.MeshRenderer.transform);
            effectObject.transform.localPosition = Vector3.zero;
            BlockColorData innerBlockColorData = LevelController.Instance.GetBlockColorData(Data.layeredBlockColor);
            
            MeshRenderer meshRenderer = effectObject.GetComponent<MeshRenderer>();
            meshRenderer.material = innerBlockColorData.Material;
            
            // Apply Skin Color for Block
            linkedBlock.SetInnerColor(innerBlockColorData);
            if (Application.isPlaying)
            {
                disappearEasing = Ease.GetCustomEasingFunction("BlockDisappear");
            }
        }

        public override BlockColor GetOverrideBlockColor()
        {
            return isCollected ? base.GetOverrideBlockColor() : Data.layeredBlockColor;
        }


        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            // The inner layer renderer is parented under the block mesh (not under this effect), so it must
            // follow the resolved visibility too — this keeps it hidden under Ice after a Container clears,
            // and only reveals it once Ice releases.
            if (effectObject)
                effectObject.SetActive(visible);
        }

        public override bool IsDestructible()
        {
            return isCollected;
        }

        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if (loseReason != LoseReason.BlockLayerFailed) return;
            LevelController.Instance.ForceCollectBlock(linkedBlock); 
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            if (isCollected) return;
            isCollected = true;
            ResetBlockColor();
            linkedBlock.ChangeGlassMaterial(linkedBlock.OriginColorConfig.GlassMaterial);
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
            ResetBlockColor();
            if (effectObject)
            {
                Destroy(effectObject);
            }
        }
    }
}