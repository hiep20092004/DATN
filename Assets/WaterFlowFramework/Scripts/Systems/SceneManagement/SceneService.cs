using System;
using WaterFlow.Enums;
using UnityEngine.SceneManagement;

namespace WaterFlow.Framework.Systems.SceneManagement
{
    public abstract class SceneService : ServiceSo
    {
        public abstract GamePlacement GetCurrentGamePlacement();
        public abstract void SwitchScene(GamePlacement newPlacement, bool force = false, Action callback = null);
    }
}