using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Chain Multi Block Effect Config")]
    public class ChainMultiBlockEffectConfig : BaseBlockEffectConfig
    {
        [SerializeField] private float offsetY = 0.2f;
        [SerializeField] private ChainVisualsData[] chainVisualsDatas;

        public float OffsetY => offsetY;

        public bool TryGetChainVisualsData(BlockType blockType, out ChainVisualsData data)
        {
            if (chainVisualsDatas != null)
            {
                for (int i = 0; i < chainVisualsDatas.Length; i++)
                {
                    if (chainVisualsDatas[i].BlockType == blockType)
                    {
                        data = chainVisualsDatas[i];
                        return true;
                    }
                }
            }

            data = null;
            return false;
        }
    }

    [Serializable]
    public class ChainVisualsData
    {
        [SerializeField] private BlockType blockType;
        [SerializeField] private GameObject visualsPrefab;

        public BlockType BlockType => blockType;
        public GameObject VisualsPrefab => visualsPrefab;
    }
}
