using System;
using System.Collections.Generic;
using UnityEngine;
//// ChienDM đã thêm class này 
namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "OtherColorDataSO", menuName = "Data/OtherColorDataSO", order = 130)]
    public class ObstacleColorCustomConfig : ScriptableObject
    {
        public ColorOverrideConfig[] colorData;

        [NonSerialized] Dictionary<BlockColor, ColorOverrideConfig> colorOverrideByBlockColor;

        private void OnEnable() => RebuildColorOverrideLookup();

        private void OnValidate() => RebuildColorOverrideLookup();

        void RebuildColorOverrideLookup()
        {
            colorOverrideByBlockColor ??= new Dictionary<BlockColor, ColorOverrideConfig>();
            colorOverrideByBlockColor.Clear();

            if (colorData == null || colorData.Length == 0)
                return;

            for (int i = 0; i < colorData.Length; i++)
            {
                ColorOverrideConfig entry = colorData[i];
                if (entry == null)
                    continue;
                colorOverrideByBlockColor[entry.Type] = entry;
            }
        }

        public bool TryGetOverride(BlockColor blockColor, out ColorOverrideConfig config)
        {
            if (colorOverrideByBlockColor == null)
                RebuildColorOverrideLookup();

            if (colorOverrideByBlockColor == null)
            {
                config = null;
                return false;
            }

            return colorOverrideByBlockColor.TryGetValue(blockColor, out config);
        }
    }
    [Serializable]
    public class ColorOverrideConfig
    {
        [SerializeField] BlockColor type;
        [SerializeField] Sprite sprite;
        [SerializeField] Texture2D texture;
        [SerializeField] Material material;
        
        public BlockColor Type { get { return type; } }
        public Sprite Sprite { get { return sprite; } }
        public Texture2D Texture { get { return texture; } }
        public Material Material { get { return material; } }
        
    }
}
