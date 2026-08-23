using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class GeneratorFootprint : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;

        [Space]
        [Slider(0.0f, 1.0f)] [SerializeField] private float defaultAlpha = 0.5f;

        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void SetColor(BlockColorData colorData)
        {
            if (colorData == null || !meshRenderer) return;

            Color displayColor = colorData.Color;
            displayColor.a = defaultAlpha;

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ShaderId.COLOR_SHADER_ID, displayColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
