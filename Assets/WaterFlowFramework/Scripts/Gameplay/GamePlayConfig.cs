using System;
using System.Collections.Generic;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.ConfigManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Framework.Gameplay
{
    [CreateAssetMenu(menuName = "WaterFlow Configs/Game Play Config", fileName = "GamePlayConfig")]
    public class GamePlayConfig : ConfigSo
    {
        public CurrencyData playOnPrice;
        public List<RewardByLevelDifficulty> winRewards = new List<RewardByLevelDifficulty>();

        public virtual ResourceData GetWinReward(LevelDifficulty levelDifficulty)
        {
            var data = winRewards.Find(x => x.levelDifficulty == levelDifficulty) ?? winRewards[0];
            return data.reward.Clone();
        }

        [Serializable]
        public class RewardByLevelDifficulty
        {
            public LevelDifficulty levelDifficulty;
            public ResourceData reward;
        }
    }
}