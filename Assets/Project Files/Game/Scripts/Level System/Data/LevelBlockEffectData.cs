using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelBlockEffectData
    {
        [SerializeField] BlockEffectType type;
        [SerializeField] BlockEffectBehavior behavior;
        [SerializeField] int effectSortingOrder;

        public BlockEffectType Type => type;
        public BlockEffectBehavior Behavior => behavior;
        public int EffectSortingOrder => effectSortingOrder;
    }
}