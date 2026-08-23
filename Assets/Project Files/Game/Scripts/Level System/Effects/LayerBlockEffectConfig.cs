using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Layer Block Effect Config")]
    public class LayerBlockEffectConfig : BaseBlockEffectConfig
    {
        [SerializeField] private List<LayerBlockPrefabData> layerBlockPrefabs;

#if UNITY_EDITOR
        [Sirenix.OdinInspector.Button("Auto Generate Layer Prefabs")]
        private void AutoGenerateLayerPrefabs()
        {
            layerBlockPrefabs ??= new List<LayerBlockPrefabData>();

            List<LayerBlockPrefabData> existing = new(layerBlockPrefabs);
            layerBlockPrefabs.Clear();

            foreach (BlockGroupType blockType in Enum.GetValues(typeof(BlockGroupType)))
            {
                if(blockType == BlockGroupType.None) continue;
                layerBlockPrefabs.Add(existing.Exists(x => x.BlockGroupType == blockType)
                    ? existing.Find(x => x.BlockGroupType == blockType)
                    : new LayerBlockPrefabData(blockType));
            }

            RuntimeEditorUtils.SetDirty(this);
            Debug.Log("[LayerBlockEffect] Auto generated layer prefabs");
        }
#endif
        
        public bool TryGetInnerPrefab(BlockTheme blockTheme, BlockType blockType, out GameObject innerPrefab)
        {
            if (layerBlockPrefabs != null)
            {
                BlockGroupType blockGroupType = blockType.ToBlockGroup();
                foreach (LayerBlockPrefabData layerBlockPrefabData in layerBlockPrefabs)
                {
                    if (blockGroupType == layerBlockPrefabData.BlockGroupType)
                    {
                        innerPrefab = layerBlockPrefabData.GetLayerPrefab(blockTheme);
                        return innerPrefab;
                    }
                }
            }

            innerPrefab = null;
            return false;
        }
    }

    [Serializable]
    public class LayerBlockPrefabData
    {
        [SerializeField] private BlockGroupType blockGroupType;
        [SerializeField] private GameObject newLayerPrefab;
        [SerializeField] private GameObject simpleLayerPrefab;

        public BlockGroupType BlockGroupType => blockGroupType;

        public LayerBlockPrefabData(BlockGroupType blockGroupType)
        {
            this.blockGroupType = blockGroupType;
        }
        
        public GameObject GetLayerPrefab(BlockTheme theme)
        {
            if (theme == BlockTheme.New)
            {
                return newLayerPrefab;
            }
            if (theme == BlockTheme.Simple)
            {
                return simpleLayerPrefab;
            }
            return null;
        }
    }
}
