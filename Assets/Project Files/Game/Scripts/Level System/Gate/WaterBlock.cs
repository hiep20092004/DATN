using UnityEngine;

namespace WaterFlow.Game
{
    public class WaterBlock : MonoBehaviour
    {
        [SerializeField] MeshRenderer graphicsMeshRenderer;
        private BlockColor blockColor;
        private int colorCount;
        
        public BlockColor BlockColor => blockColor;
        public int ColorCount => colorCount;
        
        public void Init(ColorData colorData)
        {
            blockColor = colorData.color;
            colorCount = colorData.colorCount;
            graphicsMeshRenderer.material = LevelController.Instance.GetBlockColorData(colorData.color).WaterInPipeMaterial;
        }
        
        public void OnRemoveColor(int count)
        {
            colorCount -= count;
            if (colorCount < 0) colorCount = 0;
        }
    }
}