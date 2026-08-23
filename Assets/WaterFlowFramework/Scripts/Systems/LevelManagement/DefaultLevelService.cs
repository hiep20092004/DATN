using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LevelManagement
{
    [CreateAssetMenu(fileName = "DefaultLevelService", menuName = "WaterFlow Services/Level Service/Default Level Service")]
    public class DefaultLevelService : LevelService, IServiceInitialize
    {
        [SerializeField] private string path = "Level";

        [BoxGroup("SERVICES")] [Required] [SerializeField]
        private Service<LoadObjectService> loadObjectService = new();

        [BoxGroup("SERVICES")] [Required] [SerializeField]
        private Service<SaveObjectService> saveObjectService = new();


        [BoxGroup("CONFIGS")] [SerializeField] protected LevelConfig config;

        protected LevelDataFramework levelCache;
        [SerializeField] private List<GameModeLevel> gameModeLevels = new();
        private Dictionary<GameMode, int> levelsByGameMode = new();

        public void Initialize()
        {
            InitLevelModeData();
        }

        private void InitLevelModeData()
        {
            levelsByGameMode.Clear();
            foreach (var gameModeLevel in gameModeLevels)
            {
                levelsByGameMode.Add(gameModeLevel.mode, gameModeLevel.level);
            }
        }
        public override T GetLevelData<T>(int level, GameMode gameMode, bool force = false, bool loop = true, int category = 0)
        {
            if (!force && levelCache != null && levelCache.gameMode == gameMode && levelCache.level == level && levelCache.category == category)
                return levelCache as T;
            if (levelsByGameMode.TryGetValue(gameMode, out var gameModeLevel) && level > gameModeLevel && loop)
            {
                var levelData = GetLevel<T>(gameModeLevel / 2 + level % (gameModeLevel / 2), gameMode, category);
                levelData.level = level;
                return levelData;
            }
            return GetLevel<T>(level, gameMode, category);
        }

        public void SetFolder(string folder)
        {
            path = folder;
        }

        protected override T GetLevel<T>(int level, GameMode gameMode, int category)
        {
            string mergePath = category == 0 ? string.IsNullOrEmpty(path) ? $"{gameMode}/{level}" : $"{path}/{gameMode}/{level}" :
                string.IsNullOrEmpty(path) ? $"{gameMode}/{level}.{category}" : $"{path}/{gameMode}/{level}.{category}";
            var levelData = loadObjectService.Instance.LoadObject<T>(mergePath);
            if (levelData == null && category > 0)
            {
                levelData = GetLevel<T>(level, gameMode, category - 1);
            }

            return levelData;
        }

        public override void SaveLevel<T>(T levelData)
        {
            if (Directory.Exists(saveObjectService.Instance.path) == false) Directory.CreateDirectory(saveObjectService.Instance.path);
            if (Directory.Exists(Path.Combine(saveObjectService.Instance.path, path)) == false)
                Directory.CreateDirectory(Path.Combine(saveObjectService.Instance.path, path));
            saveObjectService.Instance.SaveObject(levelData, $"{path}/{levelData.level}");
        }
        
    }
}