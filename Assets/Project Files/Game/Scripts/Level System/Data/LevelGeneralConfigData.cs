using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelGeneralConfigData
    {
        [SerializeField] int maxLevel = 400;
        [SerializeField] float fillingWaterSpeed = 0.25f;
        [SerializeField] float waterPipeSize = 2f;
        
        public int MaxLevel => maxLevel;
        public float FillingWaterSpeed => fillingWaterSpeed;
        public float WaterPipeSize => waterPipeSize;
    }
}