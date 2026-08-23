using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Encapsulates one water mesh + optional BubbleEffect. Reusable for single block and dual block (one module per water).
    /// </summary>
    public class WaterVisualModule : MonoBehaviour
    {
        [SerializeField] MeshRenderer waterRenderer;
        [SerializeField] BubbleEffect bubbleEffect;
        [SerializeField] private float fillMinValue;
        [SerializeField] private float fillMaxValue;

        private Material instanceWaterMaterial;
        private MaterialPropertyBlock waterMPB;

        // Material tweens live in the global TweensHolder, not on this GameObject. Track the active
        // fill tween so it can be killed on destroy — otherwise a scene reload mid-fill leaves the
        // tween running and its onComplete fires into an already-destroyed block (MissingReferenceException).
        private TweenCase fillTweenCase;

        public MeshRenderer WaterRenderer => waterRenderer;
        public Material InstanceWaterMaterial => instanceWaterMaterial;

        /// <summary>
        /// Initialize water material and bounds, and optional bubble effect.
        /// </summary>
        public void Init(BlockColorData colorData)
        {
            if (!waterRenderer) return;

            if (Application.isPlaying)
            {
                waterRenderer.material = colorData.WaterMaterial;
                instanceWaterMaterial = waterRenderer.material;
            }
            else
            {
                waterRenderer.sharedMaterial = colorData.WaterMaterial;
                instanceWaterMaterial = null;
            }

            SetupFillBounds();

            var mesh = waterRenderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null)
                bubbleEffect?.Initialize(instanceWaterMaterial, mesh);
        }

        /// <summary>
        /// Initialize with explicit fill bounds (e.g. for dual block prefabs with custom min/max).
        /// </summary>
        public void Init(BlockColorData colorData, float fillMin, float fillMax)
        {
            fillMinValue = fillMin;
            fillMaxValue = fillMax;
            Init(colorData);
        }

        private void SetupFillBounds()
        {
            if (!waterRenderer) return;

            if (Application.isPlaying && instanceWaterMaterial != null)
            {
                instanceWaterMaterial.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, 0f);
                instanceWaterMaterial.SetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID, fillMinValue);
                instanceWaterMaterial.SetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID, fillMaxValue);
            }
            else
            {
                waterMPB ??= new MaterialPropertyBlock();
                waterRenderer.GetPropertyBlock(waterMPB);
                waterMPB.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, 0f);
                waterMPB.SetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID, fillMinValue);
                waterMPB.SetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID, fillMaxValue);
                waterRenderer.SetPropertyBlock(waterMPB);
            }
        }

        public void OnMapSpawnCompleted()
        {
            waterRenderer.enabled = true;
            bubbleEffect?.gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Fill water with animation and sync bubble. Calls onComplete when water tween finishes.
        /// </summary>
        public void FillWater(float percent, float duration, SimpleCallback onComplete)
        {
            duration = Mathf.Clamp(duration, 0f, duration);
            if (Application.isPlaying && instanceWaterMaterial != null)
            {
                bubbleEffect?.BubbleEffectFill(percent, duration);
                fillTweenCase.KillActive();
                fillTweenCase = instanceWaterMaterial.DoFloat(ShaderId.FILL_AMOUNT_SHADER_ID, percent, duration)
                    .OnComplete(onComplete);
            }
            else
            {
                SetFillImmediate(percent);
                onComplete?.Invoke();
            }
        }

        private void OnDestroy()
        {
            fillTweenCase.KillActive();
        }

        /// <summary>
        /// Set water fill immediately without animation (e.g. editor preview).
        /// </summary>
        public void SetFillImmediate(float percent)
        {
            if (Application.isPlaying && instanceWaterMaterial != null)
            {
                instanceWaterMaterial.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, percent);
            }
            else if (waterRenderer != null)
            {
                waterMPB ??= new MaterialPropertyBlock();
                waterRenderer.GetPropertyBlock(waterMPB);
                waterMPB.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, percent);
                waterRenderer.SetPropertyBlock(waterMPB);
            }
        }

        /// <summary>
        /// Fade off bubble effect (e.g. when fill is not yet full).
        /// </summary>
        public void BubbleFadeOff()
        {
            bubbleEffect?.BubbleFadeOff();
        }

        /// <summary>
        /// Show or hide the water renderer (e.g. when block uses dual visual and disables single water).
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (waterRenderer)
                waterRenderer.enabled = visible;
        }
        
        public bool IsVisible()
        {
            if (waterRenderer && !waterRenderer.enabled)
            {
                return false;
            }
            return waterRenderer.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Set water material from outside (e.g. BlockerEffect blocked material).
        /// </summary>
        public void SetWaterMaterial(Material material)
        {
            if (waterRenderer == null) return;
            if (Application.isPlaying)
            {
                waterRenderer.material = material;
                instanceWaterMaterial = waterRenderer.material;
            }
            else
            {
                waterRenderer.sharedMaterial = material;
                instanceWaterMaterial = null;
            }
        }
    }
}
