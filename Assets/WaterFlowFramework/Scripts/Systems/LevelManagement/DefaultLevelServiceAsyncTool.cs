using System.IO;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

namespace WaterFlow.Framework.Systems.LevelManagement
{
    [CreateAssetMenu(fileName = "DefaultLevelServiceAsyncTool", menuName = "WaterFlow Services/Level Service/Default Level Service Async Tool")]
    public class DefaultLevelServiceAsyncTool : DefaultLevelServiceAsync
    {
        [SerializeField] protected string path = "";

        [BoxGroup("SERVICES")] [Required] [SerializeField]
        protected Service<SaveObjectServiceAsync> saveObjectServiceAsync = new();

        public void SetFolder(string folder)
        {
            path = folder;
        }

        public override async UniTask<T> GetLevelData<T>(int level, GameMode gameMode, bool force = false,
            bool loop = false, int category = 0)
        {
            return await GetLevel<T>(level, gameMode, category);
        }

        protected override async UniTask<T> GetLevel<T>(int level, GameMode gameMode, int category = 0)
        {
            string mergePath = category == 0 ? $"{path}{level}" : $"{path}{level}.{category}";
            var levelData = await loadObjectServiceAsync.Instance.LoadAsync<T>(mergePath);
            if (levelData == null && category > 0)
            {
                levelData = await GetLevel<T>(level, gameMode, category - 1);
            }

            return levelData;
        }

        public void SaveLevel<T>(T levelData) where T : LevelDataFramework
        {
            if (Directory.Exists(saveObjectServiceAsync.Instance.path) == false) Directory.CreateDirectory(saveObjectServiceAsync.Instance.path);
            if (Directory.Exists(Path.Combine(saveObjectServiceAsync.Instance.path, path)) == false)
                Directory.CreateDirectory(Path.Combine(saveObjectServiceAsync.Instance.path, path));
            saveObjectServiceAsync.Instance.SaveObject(levelData, $"{path}{levelData.level}");
        }
    }
}