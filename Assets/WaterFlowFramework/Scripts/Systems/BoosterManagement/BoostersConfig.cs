using System;
using System.Collections.Generic;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.ConfigManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;
using UnityEngine.Serialization;

namespace WaterFlow.Framework.Systems.BoosterManagement
{
    [CreateAssetMenu(fileName = "BoostersConfig", menuName = "WaterFlow Configs/BoostersConfig", order = 1)]
    public class BoostersConfig : ConfigSo
    {
        public List<BoosterConfig> configs;
    }

    [Serializable]
    public class BoosterConfig
    {
        public GameResource booster;
        public int levelUnlock;
        public CurrencyData price;
        public int defaultValue;
        public int packValue = 1;
    }
}