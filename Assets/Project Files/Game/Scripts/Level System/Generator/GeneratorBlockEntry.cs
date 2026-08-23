using System;
using System.Linq;
using WaterFlow.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace WaterFlow.Game
{
    [Serializable]
    public class GeneratorBlockEntry
    {
        public const string BlockEffectsPropertyName = nameof(blockEffects);
        public const string BlockIdPropertyName = nameof(blockId);

        [ReadOnly] [SerializeField] int blockId;
        [SerializeField] BlockType blockType;
        [SerializeField] BlockColor blockColor;
        [FormerlySerializedAs("blockEffectsNew")]
        [SerializeReference] BlockEffectData[] blockEffects;
        
        public BlockType BlockType => blockType;
        public BlockColor BlockColor => blockColor;
        public int BlockId => blockId;
        public BlockEffectData[] BlockEffects
        {
            get => blockEffects;
            set => blockEffects = value;
        }

        public void AssignBlockId(int id)
        {
            blockId = id;
        }

        public void SortBlockEffects()
        {
            if (blockEffects == null || blockEffects.Length == 0) return;
            blockEffects = blockEffects.OrderBy(x => EditorEffectsHelper.GetSortingOrder(x.Type)).ToArray();
        }
    }
}
