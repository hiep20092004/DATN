using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Rope Block Effect Config")]
    public class RopeEffectConfig : BaseBlockEffectConfig
    {
        [Tooltip("Rope visual prefab instantiated once per anchor. Consumed by both runtime and the Rope Config window preview.")]
        [SerializeField] GameObject ropePrefab;

        [Space]
        [Tooltip("One entry per BlockType. Use the Rope Config window ('Sync All Block Types') to keep this list aligned with the enum.")]
        [SerializeField] RopePositionData[] ropePositionDatas = Array.Empty<RopePositionData>();

        public GameObject RopePrefab => ropePrefab;

        public RopePositionData GetPositionData(BlockType blockType)
        {
            if (TryGetPositionData(blockType, out RopePositionData data))
                return data;

            Debug.LogError($"Rope position data not found for block type: {blockType} in {name}.", this);
            return null;
        }

        public bool TryGetPositionData(BlockType blockType, out RopePositionData data)
        {
            for (int i = 0; i < ropePositionDatas.Length; i++)
            {
                if (ropePositionDatas[i].BlockType == blockType)
                {
                    data = ropePositionDatas[i];
                    return true;
                }
            }

            data = null;
            return false;
        }

#if UNITY_EDITOR
        public void Editor_SyncAllBlockTypes()
        {
            Dictionary<BlockType, RopePositionData> existing = new();
            if (ropePositionDatas != null)
            {
                foreach (RopePositionData data in ropePositionDatas)
                {
                    if (data != null)
                        existing.TryAdd(data.BlockType, data);
                }
            }

            BlockType[] allBlockTypes = (BlockType[])Enum.GetValues(typeof(BlockType));
            RopePositionData[] result = new RopePositionData[allBlockTypes.Length];

            for (int i = 0; i < allBlockTypes.Length; i++)
            {
                if (existing.TryGetValue(allBlockTypes[i], out RopePositionData data))
                {
                    result[i] = data;
                }
                else
                {
                    RopePositionData newData = new RopePositionData();
                    newData.Editor_SetBlockType(allBlockTypes[i]);
                    result[i] = newData;
                }
            }

            ropePositionDatas = result;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
