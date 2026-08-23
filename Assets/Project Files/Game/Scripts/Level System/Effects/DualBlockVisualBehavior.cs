using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class DualBlockVisualBehavior : MonoBehaviour
    {
        [SerializeField] MeshRenderer meshRenderer1;
        [SerializeField] MeshRenderer meshGlass1;
        [SerializeField] WaterVisualModule waterModule1;
        [SerializeField] private float water1MinValue;
        [SerializeField] private float water1MaxValue;

        [SerializeField] MeshRenderer meshRenderer2;
        [SerializeField] MeshRenderer meshGlass2;
        [SerializeField] WaterVisualModule waterModule2;
        [SerializeField] private float water2MinValue;
        [SerializeField] private float water2MaxValue;

        private BlockColor color1;
        private BlockColor color2;

        public void Init(BlockColor color1, BlockColor color2)
        {
            this.color1 = color1;
            this.color2 = color2;

            BlockColorData colorData1 = LevelController.Instance.GetBlockColorData(color1);
            BlockColorData colorData2 = LevelController.Instance.GetBlockColorData(color2);

            if (Application.isPlaying)
            {
                meshRenderer1.material = colorData1.Material;
                meshGlass1.material = colorData1.GlassMaterial;
                meshRenderer2.material = colorData2.Material;
                meshGlass2.material = colorData2.GlassMaterial;
            }
            else
            {
                meshRenderer1.sharedMaterial = colorData1.Material;
                meshGlass1.sharedMaterial = colorData1.GlassMaterial;
                meshRenderer2.sharedMaterial = colorData2.Material;
                meshGlass2.sharedMaterial = colorData2.GlassMaterial;
            }

            waterModule1?.Init(colorData1, water1MinValue, water1MaxValue);
            waterModule2?.Init(colorData2, water2MinValue, water2MaxValue);
        }

        /// <summary>
        /// Fill water for a specific color with animation (water + bubble per module).
        /// </summary>
        public void FillWater(BlockColor color, float fillPercent, float duration, SimpleCallback onComplete)
        {
            WaterVisualModule module = GetWaterModule(color);
            module?.FillWater(fillPercent, duration, onComplete);
        }

        /// <summary>
        /// Set water fill immediately without animation (for editor preview).
        /// </summary>
        public void SetWaterFillImmediate(BlockColor color, float fillPercent)
        {
            WaterVisualModule module = GetWaterModule(color);
            module?.SetFillImmediate(fillPercent);
        }

        /// <summary>
        /// Get water material for a color (for BlockMovementManager / InstanceWaterMaterial when dual).
        /// </summary>
        public Material GetWaterMaterial(BlockColor color)
        {
            WaterVisualModule module = GetWaterModule(color);
            return module != null ? module.InstanceWaterMaterial : null;
        }

        /// <summary>
        /// Fade off bubble for the given color (e.g. when fill not yet full).
        /// </summary>
        public void BubbleFadeOff()
        {
            waterModule1?.BubbleFadeOff();
            waterModule2?.BubbleFadeOff();
        }

        /// <summary>
        /// Set water material on both modules (e.g. BlockerEffect blocked material).
        /// </summary>
        public void SetWaterMaterial(Material blockMaterial)
        {
            waterModule1?.SetWaterMaterial(blockMaterial);
            waterModule2?.SetWaterMaterial(blockMaterial);
        }
        
        public void SetVisible(bool visible)
        {
            waterModule1?.SetVisible(visible);
            waterModule2?.SetVisible(visible);
        }

        public bool IsVisible()
        {
            return waterModule1?.IsVisible() ?? waterModule2?.IsVisible() ?? false;
        }

        private WaterVisualModule GetWaterModule(BlockColor color)
        {
            if (color == color1) return waterModule1;
            if (color == color2) return waterModule2;
            return null;
        }

        /// <summary>
        /// Get the block color based on click position
        /// </summary>
        public BlockColor GetColorFromClickPosition(Vector3 clickPosition)
        {
            Bounds bounds1 = meshRenderer1.bounds;
            if (bounds1.Contains(clickPosition))
            {
                return color1;
            }

            Bounds bounds2 = meshRenderer2.bounds;
            if (bounds2.Contains(clickPosition))
            {
                return color2;
            }

            return BlockColor.None;
        }
    }
}
