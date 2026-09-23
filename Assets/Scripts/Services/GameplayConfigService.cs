using System;
using System.Collections.Generic;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "GameplayConfigService", menuName = "Services/InGame/GameplayConfigService")]
    public class GameplayConfigService : ServiceSo, IServiceInitialize
    {
        [SerializeField] LevelDatabase levelDatabase;


        private BlockTheme currentBlockTheme = BlockTheme.Simple;

        public List<RewardByLevelType> winRewards = new ();
        public LevelDatabase LevelDatabase => levelDatabase;

        public void ChangeBlockTheme(BlockTheme theme)
        {
            currentBlockTheme = BlockTheme.Simple;
        }

        public void Initialize()
        {
            currentBlockTheme = BlockTheme.Simple;
        }
        
        public ResourceData GetWinReward(LevelType levelType)
        {
            var data = winRewards.Find(x => x.levelType == levelType) ?? winRewards[0];
            return data.reward.Clone();
        }
        
        public BlockTheme GetBlockTheme() => currentBlockTheme;
        
        [Serializable]
        public class RewardByLevelType
        {
            public LevelType levelType;
            public ResourceData reward;
        }

    }
}
