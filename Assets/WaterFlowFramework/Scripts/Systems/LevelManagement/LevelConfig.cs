using System.Collections.Generic;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.ConfigManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LevelManagement
{
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "WaterFlow Configs/LevelConfig", order = 1)]
    public class LevelConfig: ConfigSo
    {
       // [ShowInInspector] public List<GameModeLevel> totalLevels;
    }

    [System.Serializable]
    public class GameModeLevel
    {
        public GameMode mode;
        public int level;
    }
}