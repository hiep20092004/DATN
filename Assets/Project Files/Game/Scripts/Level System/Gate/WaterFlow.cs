using System;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class WaterFlow : MonoBehaviour
    {
        private static int _fillAmountID = Shader.PropertyToID("_FillAmountBottom");
        private static int _fillAmountIDEnd = Shader.PropertyToID("_FillAmount");
        private MaterialPropertyBlock materialPropertyBlock;
        
        [SerializeField] private WaterFlowElement[] waterFlowElements = new WaterFlowElement[3];
        
        private int currentDepth;

        public void Init()
        {
            foreach (WaterFlowElement element in waterFlowElements)
            {
                SetRendererFloat(element.meshRenderer, _fillAmountID, element.minFillAmount);
                element.meshRenderer.gameObject.SetActive(false);
            }
        }
        
        public void ShowWaterFlow(int depthIndex, BlockColor color, out float flowDuration)
        {
            flowDuration = 0f;
            if (!waterFlowElements.IsInRange(depthIndex)) return;
            currentDepth = depthIndex;
            WaterFlowElement waterFlowElement = waterFlowElements[depthIndex];
            flowDuration = waterFlowElement.flowDuration;
            var meshRenderer = waterFlowElement.meshRenderer;
            meshRenderer.sharedMaterial = LevelController.Instance.GetBlockColorData(color).FlowMaterial;
            SetRendererFloat(meshRenderer, _fillAmountIDEnd, waterFlowElement.minFillAmount);
            SetRendererFloat(meshRenderer, _fillAmountID, waterFlowElement.minFillAmount);
            meshRenderer.gameObject.SetActive(true);
            DoRendererFloat(meshRenderer,
                _fillAmountID,
                waterFlowElement.minFillAmount,
                waterFlowElement.maxFillAmount,
                waterFlowElement.flowDuration);
        }

        public float GetFlowDuration(int depthIndex)
        {
            if (!waterFlowElements.IsInRange(depthIndex)) return 0f;
            return waterFlowElements[depthIndex].flowDuration;
        }
        
        public void DisableWaterFlow()
        {
            if (!waterFlowElements.IsInRange(currentDepth)) return;
            WaterFlowElement waterFlowElement = waterFlowElements[currentDepth];

            var meshRenderer = waterFlowElement.meshRenderer;

            void DisableObject()
            {
                if (meshRenderer && 
                    meshRenderer.gameObject)
                {
                    meshRenderer.gameObject.SetActive(false);
                }
            }
            DoRendererFloat(meshRenderer,
                    _fillAmountIDEnd,
                    waterFlowElement.minFillAmount,
                    waterFlowElement.maxFillAmount,
                    waterFlowElement.flowDuration)
                .OnComplete(DisableObject);
        }

        public void DisableWaterFlowImmediately()
        {
            if (!waterFlowElements.IsInRange(currentDepth)) return;
            WaterFlowElement waterFlowElement = waterFlowElements[currentDepth];
            var meshRenderer = waterFlowElement.meshRenderer;
            if (meshRenderer && meshRenderer.gameObject)
            {
                meshRenderer.gameObject.SetActive(false);
            }
        }

        private void SetRendererFloat(MeshRenderer meshRenderer, int propertyId, float value)
        {
            if (!meshRenderer) return;

            materialPropertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetFloat(propertyId, value);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);
        }

        private TweenCase DoRendererFloat(
            MeshRenderer meshRenderer,
            int propertyId,
            float startValue,
            float endValue,
            float duration)
        {
            SetRendererFloat(meshRenderer, propertyId, startValue);
            return Tween.DoFloat(startValue,
                endValue,
                duration,
                value => SetRendererFloat(meshRenderer, propertyId, value));
        }
    }
    
    
    [Serializable]
    public class WaterFlowElement
    {
        public MeshRenderer meshRenderer;
        public float minFillAmount;
        public float maxFillAmount;
        public float flowDuration = 0.2f;
    }
}