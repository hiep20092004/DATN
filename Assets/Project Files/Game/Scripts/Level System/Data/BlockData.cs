using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class BlockData
    {
        [SerializeField] BlockType type;
        [SerializeField] GameObject prefab;
        
        public BlockType Type => type;
        public GameObject Prefab => prefab;
        
        public LevelBlockBehavior BlockBehavior { get; private set; }
        public LevelFigure Figure { get; private set; }

        public void Init()
        {
            if(prefab == null)
            {
                Debug.LogError($"Prefab for block type {type} is not assigned.");

                return;
            }

            LevelBlockBehavior levelBlockBehavior = prefab.GetComponent<LevelBlockBehavior>();
            if(levelBlockBehavior == null)
            {
                Debug.LogError($"Prefab for block type {type} does not have LevelBlockBehavior component.");
                
                return;
            }

            BlockBehavior = levelBlockBehavior;
            Figure = levelBlockBehavior.Figure;
        }
    }
}