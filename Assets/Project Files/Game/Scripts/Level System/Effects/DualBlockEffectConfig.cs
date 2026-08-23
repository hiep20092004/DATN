using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Dual Block Effect Config")]
    public class DualBlockEffectConfig : BaseBlockEffectConfig
    {
        [SerializeField] private DualVisualsData[] dualVisualsDatas;
        [SerializeField] private DualVisualsData[] newDualVisualsDatas;
        
        public bool TryGetDualVisualsData(BlockTheme blockTheme, BlockType blockType, out DualVisualsData data)
        {
            if (blockTheme == BlockTheme.New)
            {
                if (newDualVisualsDatas != null)
                {
                    for (int i = 0; i < newDualVisualsDatas.Length; i++)
                    {
                        if (newDualVisualsDatas[i].BlockType == blockType)
                        {
                            data = newDualVisualsDatas[i];
                            return true;
                        }
                    }
                }
                data = null;
                return false; 
            }
            
            if (dualVisualsDatas != null)
            {
                for (int i = 0; i < dualVisualsDatas.Length; i++)
                {
                    if (dualVisualsDatas[i].BlockType == blockType)
                    {
                        data = dualVisualsDatas[i];
                        return true;
                    }
                }
            }

            data = null;
            return false;
        }
    }

    [Serializable]
    public class DualVisualsData
    {
        [SerializeField] private BlockType blockType;
        [SerializeField] private GameObject visualsPrefab;

        public BlockType BlockType => blockType;
        public GameObject VisualsPrefab => visualsPrefab;
    }
}
