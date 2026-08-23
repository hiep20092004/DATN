using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Blocks Visuals Data", menuName = "Data/Level/Blocks Visuals Data")]
    public class BlocksVisualsData : ScriptableObject
    {
        [SerializeField] BlockData[] blocks;
        [SerializeField] BlockColorData[] colors;
        
        public BlockData[] Blocks => blocks;
        public BlockColorData[] Colors => colors;
        
        private Dictionary<BlockType, BlockData> blockDataDict = new ();
        private Dictionary<BlockColor, BlockColorData> blockColorDict = new ();
        
        public void Init()
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Init();
            }
        }

        public BlockData GetBlockData(BlockType blockType)
        {
            CacheData();
            if(blockDataDict.TryGetValue(blockType, out BlockData blockData))
            {
                return blockData;
            }
#if UNITY_EDITOR
            Debug.LogError($"Block data for {blockType} not found.");
#endif
            return null;
        }

        public BlockColorData GetColorData(BlockColor type)
        {
            CacheData();
            if(blockColorDict.TryGetValue(type, out BlockColorData colorData))
            {
                return colorData;
            }

#if UNITY_EDITOR
            Debug.LogError($"Block color data for {type} not found.");
#endif

            return null;
        }
        
        
        private void CacheData()
        {
            if (blockDataDict.Count == 0)
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (!blockDataDict.ContainsKey(blocks[i].Type))
                    {
                        blockDataDict[blocks[i].Type] = blocks[i];
                    }
#if UNITY_EDITOR
                    else
                    {
                        Debug.LogError($"Duplicate block data for type {blocks[i].Type} found. Skipping.");
                    }
#endif
                    
                }
            }
            if (blockColorDict.Count == 0)
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    if(!blockColorDict.ContainsKey(colors[i].Type))
                    {
                        blockColorDict[colors[i].Type] = colors[i];
                    }
#if UNITY_EDITOR
                    else
                    {
                        Debug.LogError($"Duplicate block color data for type {colors[i].Type} found. Skipping.");
                    }
#endif
                }
            }
        }
    }
}