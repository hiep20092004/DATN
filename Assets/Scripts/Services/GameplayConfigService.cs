using System;
using System.Collections.Generic;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "GameplayConfigService", menuName = "Services/InGame/GameplayConfigService")]
    public class GameplayConfigService : ServiceSo, IServiceInitialize, IServiceWaitingRemoteConfig
    {
        [SerializeField] LevelDatabase levelDatabase;


        private BlockTheme remoteBlockTheme = BlockTheme.New;
        private BlockTheme currentBlockTheme = BlockTheme.New;

        // Albedo that represents the New theme, captured once before any runtime clearing.
        private Texture resolvedAlbedo;
        private bool albedoResolved;

        public List<RewardByLevelType> winRewards = new ();
        public LevelDatabase LevelDatabase => levelDatabase;

        public void ChangeBlockTheme(BlockTheme theme)
        {
            currentBlockTheme = theme;
        }

        public void Initialize()
        {
        }

        public void OnRemoteConfigReady()
        {
            remoteBlockTheme = FetchRemoteBlockTheme();
            currentBlockTheme = remoteBlockTheme;
        }
        
        public ResourceData GetWinReward(LevelType levelType)
        {
            var data = winRewards.Find(x => x.levelType == levelType) ?? winRewards[0];
            return data.reward.Clone();
        }
        
        public BlockTheme GetBlockTheme() => currentBlockTheme;


        private BlockTheme FetchRemoteBlockTheme() => BlockTheme.New;
        
        [Serializable]
        public class RewardByLevelType
        {
            public LevelType levelType;
            public ResourceData reward;
        }

    }
}