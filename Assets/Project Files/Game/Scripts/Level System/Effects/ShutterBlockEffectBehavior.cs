using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;
using Tween = DG.Tweening.Tween;

namespace WaterFlow.Game
{
    public class ShutterBlockEffectBehavior : BlockEffectBehavior<ShutterBlockEffectData>
    {

        [SerializeField] GameObject shutterBullHandle;
        [SerializeField] Material shutterVisualsMaterial;
        
        private bool isOpen = false;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private GameObject shutterVisuals;
        private GameObject shutterPullHandleVisual;
        private Transform shutterBullHandleStart;
        private Transform shutterBullHandleEnd;
        private Material tempMaterial;
        private ShutterVisualsData visualsData;
        private ShutterBlockEffectConfig config;
        private Tween shutterTween;
        private float currentFillAmount;
        
        public bool IsOpen => isOpen;
        private static int lastTimePlayAudio = -1; // For preventing audio overlap

        private void SetupPullHandle(LevelBlockBehavior blockBehavior)
        {
            shutterBullHandleStart = blockBehavior.ShutterTransformStart;
            shutterBullHandleEnd = blockBehavior.ShutterBullHandleEnd;
            
            if (!shutterBullHandle ||!shutterBullHandleStart || !shutterBullHandleEnd)
                return;

            shutterPullHandleVisual = Instantiate(shutterBullHandle, blockBehavior.ModelParentTransform);
            shutterPullHandleVisual.transform.localPosition = isOpen ? shutterBullHandleStart.localPosition : shutterBullHandleEnd.localPosition;
            shutterPullHandleVisual.transform.localRotation = isOpen ? shutterBullHandleStart.localRotation : shutterBullHandleEnd.localRotation;
        }
            
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<ShutterBlockEffectConfig>();
            BlockType blockType = blockBehavior.BlockConfig.Type;
            if (config == null || !config.TryGetShutterVisualsData(blockType, out visualsData))
            {
                Debug.LogError($"Shutter visuals data for block type {blockType} not found.", this);
                DisableEffect();
                return;
            }
            
            isOpen = Data.shutterIsOpen;
            currentFillAmount = GetFillAmount();

            shutterVisuals = Instantiate(visualsData.VisualsPrefab, blockBehavior.ModelParentTransform);
            //TODO: Fix material shader override
            Vector3 pos = shutterVisuals.transform.position;
            pos.y += 0.12f;
            shutterVisuals.transform.position = pos;
            meshRenderer = shutterVisuals.GetComponent<MeshRenderer>();
            
            SetupPullHandle(blockBehavior);
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                tempMaterial = new Material(shutterVisualsMaterial);
                tempMaterial.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, currentFillAmount);
              //  tempMaterial.SetColor(COLOR_SHADER_NAME_ID, blockBehavior.OriginColorConfig.Color);
               if(visualsData.Texture!=null) tempMaterial.SetTexture(ShaderId.MAIN_TEXTURE_ID, visualsData.Texture);
                tempMaterial.SetFloat(ShaderId.FLOAT_FILL_START_ID, visualsData.MinValue);
                tempMaterial.SetFloat(ShaderId.FLOAT_FILL_END_ID, visualsData.MaxValue);
                meshRenderer.sharedMaterial = tempMaterial;
                return;
            }
#endif
            meshRenderer.sharedMaterial = shutterVisualsMaterial;
            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, currentFillAmount);
            // propertyBlock.SetColor(COLOR_SHADER_NAME_ID, blockBehavior.OriginColorConfig.Color);
            if(visualsData.Texture!=null) propertyBlock.SetTexture(ShaderId.MAIN_TEXTURE_ID, visualsData.Texture);
            propertyBlock.SetFloat(ShaderId.FLOAT_FILL_START_ID, visualsData.MinValue);
            propertyBlock.SetFloat(ShaderId.FLOAT_FILL_END_ID, visualsData.MaxValue);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);

            if (!visible)
                shutterTween?.Kill();

            if (shutterVisuals)
                shutterVisuals.SetActive(visible);

            if (shutterPullHandleVisual)
                shutterPullHandleVisual.SetActive(visible);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            shutterTween?.Kill();
            
            if (shutterVisuals)
            {
                Destroy(shutterVisuals);
            }

            if (shutterBullHandle)
            {
                Destroy(shutterPullHandleVisual);
            }
        }

        public override void OnBlockFullFilledBeforeAnimationGlobal(LevelBlockBehavior levelBlockBehavior)
        {
            if (levelBlockBehavior == linkedBlock)
            {
                return;
            }
            ChangeState();
        }

        private float GetFillAmount()
        {
            return isOpen ? 0 : 1;
        }
        
        public override BlockGateState AllowGateEntered()
        {
            return isOpen ? BlockGateState.Enterable : BlockGateState.Shuttered;
        }

        public override BlockEffectData GetCurrentEffectData()
        {
            return new ShutterBlockEffectData { shutterIsOpen = isOpen };
        }

        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if(loseReason != LoseReason.ShutterFailed)
                return;
            ChangeState();
        }

        private void ChangeState()
        {
            if (hiddenVisualSources.Count > 0)
                return;

            isOpen = !isOpen;

            float targetFillAmount = GetFillAmount();
            shutterTween?.Kill();

            if (config.TransitionDuration <= 0f)
            {
                ApplyShutterVisualProgress(targetFillAmount);
            }
            else
            {
                shutterTween = DOTween.To(
                        () => currentFillAmount,
                        ApplyShutterVisualProgress,
                        targetFillAmount,
                        config.TransitionDuration)
                    .SetEase(DG.Tweening.Ease.Linear);
            }

            if (Time.frameCount != lastTimePlayAudio)
            {
                lastTimePlayAudio = Time.frameCount;
                Services.AudioService.PlaySound(AudioId.Obstacle_Shutter);
            }
        }

        private void ApplyShutterVisualProgress(float fillAmount)
        {
            currentFillAmount = fillAmount;

            if (meshRenderer is not null && propertyBlock != null)
            {
                propertyBlock.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, fillAmount);
                meshRenderer.SetPropertyBlock(propertyBlock);
            }

            if (!shutterPullHandleVisual || !shutterBullHandleStart || !shutterBullHandleEnd)
            {
                return;
            }

            float openProgress = 1f - fillAmount;
            Transform handleTransform = shutterPullHandleVisual.transform;
            handleTransform.localPosition = Vector3.Lerp(
                shutterBullHandleEnd.localPosition,
                shutterBullHandleStart.localPosition,
                openProgress);
            handleTransform.localRotation = Quaternion.Lerp(
                shutterBullHandleEnd.localRotation,
                shutterBullHandleStart.localRotation,
                openProgress);
        }
    }
}