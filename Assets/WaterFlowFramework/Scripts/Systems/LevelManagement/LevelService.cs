using Newtonsoft.Json;
using WaterFlow.Enums;

namespace WaterFlow.Framework.Systems.LevelManagement
{
    public abstract class LevelService : ServiceSo
    {
        public static readonly JsonSerializerSettings Settings = new() { TypeNameHandling = TypeNameHandling.Auto };
        public abstract T GetLevelData<T>(int level, GameMode gameMode, bool force = false,
            bool loop = true, int category = 0) where T : LevelDataFramework;

        protected abstract T GetLevel<T>(int level, GameMode gameMode, int category) where T : LevelDataFramework;
        public abstract void SaveLevel<T>(T levelData) where T : LevelDataFramework;
    }
}