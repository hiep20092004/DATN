using UnityEngine;

namespace WaterFlow.Game
{
    [System.Serializable]
        public class RopePositionData
        {
            [SerializeField] BlockType blockType;
            [SerializeField] RopeTransform[] transformDatas;
            
            public BlockType BlockType => blockType;
            public RopeTransform[] TransformDatas => transformDatas;

#if UNITY_EDITOR
            internal void Editor_SetBlockType(BlockType type) => blockType = type;
#endif
        }
}