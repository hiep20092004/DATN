using System;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Framework.Systems.InventoryManagement
{
    public abstract class InventoryService : ServiceSo
    {
        public Action<GameResourceKey> OnResourceUpdate { get; set; }
        public Action<ResourceData> OnAddPendingResource { get; set; }

        public abstract ResourceData GetResource(GameResourceKey key);
        public abstract ResourceData GetPendingResource(string source, GameResourceKey key);

        public abstract void UpdateResource(GameResourceKey key, int quantity);
        public abstract void UpdateResource(GameResourceKey key, long seconds);

        public abstract void AddResource(ResourceData unitData, EarnResourceLogData logData);

        public abstract void AddPendingResource(string source, ResourceData unitData, EarnResourceLogData logData);

        public abstract void AddReward(RewardData rewardData, EarnResourceLogData logData);
        public abstract void AddPendingReward(string source, RewardData rewardData, EarnResourceLogData logData);

        public abstract void TryBuyResourceByCurrency(CurrencyData currency, Action onSuccess);

        public abstract void ClaimPendingResource(string source);
        public abstract void ClaimPendingResource(string source, GameResourceKey key);

        public abstract bool CanReduce(GameResourceKey key, int quantity);

        public abstract bool CanReduce(CurrencyData currencyData);

        public abstract void SpendCurrency(CurrencyData currencyData, SpendResourceLogData logData);
        public abstract void SpendResource(GameResourceKey key, int quantity, SpendResourceLogData logData);
        public abstract void SpendResource(GameResource key, int quantity, SpendResourceLogData logData);

        public abstract bool IsExpired(GameResourceKey key);
        //public abstract void NotiUpdateResource(GameResource resource = GameResource.MAX);
    }


    public class EarnResourceLogData
    {
        public string spendType;
        public string spendId;
        public bool isFirstBuy = false;
        public string source = "non_iap";

        public EarnResourceLogData()
        {
        }

        public EarnResourceLogData(string spendType, string spendId, string source = "non_iap", bool isFirstBuy = false)
        {
            this.spendType = spendType;
            this.spendId = spendId;
            this.isFirstBuy = isFirstBuy;
            this.source = source;
        }
    }

    public class SpendResourceLogData
    {
        public string earnType;
        public string earnId;
        public string source = "non_iap";

        public SpendResourceLogData()
        {
        }

        public SpendResourceLogData(string earnType, string earnId, string source = "non_iap")
        {
            this.earnType = earnType;
            this.earnId = earnId;
            this.source = source;
        }
    }
}