using System;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement;

namespace WaterFlow.Framework.Systems.BoosterManagement
{
    public abstract class BoosterService : ServiceSo
    {
        public Action<GameResource> onUnlockBooster;
        public abstract bool IsBoosterUnlock(GameResource booster);
        public abstract void UnlockBooster(GameResource booster);
        public abstract BoosterConfig GetBoosterConfig(GameResource booster);
        public abstract BoosterData GetBoosterData(GameResource booster);
        public abstract bool CanUseBooster(GameResource booster);
        public abstract void UseBoosterSuccess(GameResource boosterType);

        public abstract bool BuyBooster(GameResource boosterType);
        
        public virtual InventoryService InventoryService { get;}
    }
}