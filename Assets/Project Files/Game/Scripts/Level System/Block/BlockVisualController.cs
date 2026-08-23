using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Owns a block's renderer/material manipulation and the runtime dual-visual reference.
    /// Plain collaborator constructed by <see cref="LevelBlockBehavior"/>; the serialized renderer
    /// references stay on the MonoBehaviour and are passed in, so no prefab change is required.
    /// </summary>
    public class BlockVisualController
    {
        private readonly MeshRenderer meshRenderer;
        private readonly MeshRenderer meshGlass;
        private readonly MeshRenderer outerRenderer;
        private readonly WaterVisualModule waterModule;
        private readonly WaterMinMaxConfig waterMinMaxConfig;

        private Material storedOuterMaterial;
        // Set at runtime by DualBlockEffectBehavior via the facade; null for single-color blocks.
        private DualBlockVisualBehavior dualVisual;

        public BlockVisualController(MeshRenderer meshRenderer, MeshRenderer meshGlass, MeshRenderer outerRenderer,
            WaterVisualModule waterModule, WaterMinMaxConfig waterMinMaxConfig)
        {
            this.meshRenderer = meshRenderer;
            this.meshGlass = meshGlass;
            this.outerRenderer = outerRenderer;
            this.waterModule = waterModule;
            this.waterMinMaxConfig = waterMinMaxConfig;
        }

        public void SetVisible(bool isOn, bool includeOuter = true)
        {
            if (meshRenderer)
                meshRenderer.enabled = isOn;
            if (meshGlass)
                meshGlass.enabled = isOn;
            if (outerRenderer && includeOuter)
                outerRenderer.enabled = isOn;
            waterModule?.SetVisible(isOn);
        }

        public void ChangeBodyMaterial(Material newMaterial)
        {
            if (meshRenderer)
            {
                if (Application.isPlaying)
                    meshRenderer.material = newMaterial;
                else
                    meshRenderer.sharedMaterial = newMaterial;
            }
        }

        public void ChangeOuterMaterial(Material newMaterial)
        {
            if (!outerRenderer) return;
            if (!storedOuterMaterial)
            {
                storedOuterMaterial = Application.isPlaying ? outerRenderer.material : outerRenderer.sharedMaterial;
            }

            if (Application.isPlaying)
                outerRenderer.material = newMaterial;
            else
                outerRenderer.sharedMaterial = newMaterial;
        }

        public void RestoreOuterMaterial()
        {
            if (storedOuterMaterial)
            {
                if (Application.isPlaying)
                    outerRenderer.material = storedOuterMaterial;
                else
                    outerRenderer.sharedMaterial = storedOuterMaterial;
            }
        }

        public void ChangeGlassMaterial(Material newMaterial)
        {
            if (meshGlass)
            {
                if (Application.isPlaying)
                    meshGlass.material = newMaterial;
                else
                    meshGlass.sharedMaterial = newMaterial;
            }
        }

        public void ChangeWaterMaterial(Material newMaterial)
        {
            waterModule?.SetWaterMaterial(newMaterial);
        }

        public void SetInnerColor(BlockColorData colorData)
        {
            ChangeGlassMaterial(colorData.GlassMaterial);
            waterModule?.Init(colorData, waterMinMaxConfig.MinValue, waterMinMaxConfig.MaxValue);
        }

        /// <summary>Active water material: from dual visual for the active color, else from the single water module.</summary>
        public Material GetActiveWaterMaterial(BlockColor activeColor)
        {
            if (dualVisual != null)
                return dualVisual.GetWaterMaterial(activeColor);
            return waterModule != null ? waterModule.InstanceWaterMaterial : null;
        }

        public bool IsWaterVisible()
        {
            return HasDualVisual() ? dualVisual.IsVisible() : waterModule.IsVisible();
        }

        /// <summary>Apply blocked material to water (single module or both dual modules). Used by BlockerEffectBehavior.</summary>
        public void ApplyBlockedWaterMaterial(Material blockMaterial)
        {
            if (dualVisual)
                dualVisual.SetWaterMaterial(blockMaterial);
            else
                waterModule?.SetWaterMaterial(blockMaterial);
        }

        public void SetVisibleBlockWater(bool isActive)
        {
            if (dualVisual)
                dualVisual.SetVisible(isActive);
            else
                waterModule?.SetVisible(isActive);
        }

        public void SetDualVisual(DualBlockVisualBehavior visual)
        {
            dualVisual = visual;
        }

        public bool HasDualVisual()
        {
            return dualVisual;
        }

        public BlockColor GetColorFromPosition(Vector3 position, BlockColor fallbackActiveColor)
        {
            if (dualVisual)
                return dualVisual.GetColorFromClickPosition(position);
            return fallbackActiveColor;
        }

        // --- Dual-water fill: thin wrappers so the fill collaborator never holds dualVisual directly. ---

        public void FillDualWater(BlockColor color, float percent, float duration, SimpleCallback onComplete)
        {
            dualVisual?.FillWater(color, percent, duration, onComplete);
        }

        public void SetDualWaterFillImmediate(BlockColor color, float percent)
        {
            dualVisual?.SetWaterFillImmediate(color, percent);
        }

        /// <summary>Fade off bubbles for the active fill source(s): single module plus dual visual when present.</summary>
        public void BubbleFadeOff()
        {
            waterModule?.BubbleFadeOff();
            if (dualVisual)
                dualVisual.BubbleFadeOff();
        }
    }
}
