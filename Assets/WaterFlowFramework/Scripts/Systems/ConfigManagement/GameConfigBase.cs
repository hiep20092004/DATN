using System.Collections.Generic;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;

namespace WaterFlow.Framework.Systems.ConfigManagement
{
    public abstract class GameConfigBase : ConfigSo
    {
        public ResourceData winReward;
        public ResourceData playOnPrice;

        [ShowInInspector] public Dictionary<GameMode, int> totalLevel = new();
    }
}