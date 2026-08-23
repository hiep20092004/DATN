using WaterFlow.Core;
using UnityEngine.SceneManagement;

namespace WaterFlow.Game
{
    public static class SceneLoader
    {
        public static void LoadScene(string levelName)
        {
            Tween.RemoveAll();
            SaveController.Save();
            SceneManager.LoadScene(levelName);
        }
    }
}