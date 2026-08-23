using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using WaterFlow.Enums;
using WaterFlow.Framework.Helper.Converters;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.GameDataManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Framework.Systems.InventoryManagement
{
    [CreateAssetMenu(fileName = "DefaultInventoryService", menuName = "WaterFlow Services/Inventory Service")]
    public class DefaultInventoryService : InventoryService, IServiceInitialize
    {
        protected readonly Dictionary<GameResourceKey, ResourceData> resources = new();
        protected Dictionary<string, Dictionary<GameResourceKey, ResourceData>> pendingResources;

        private const string GAME_RESOURCE_PREFIX_KEY = "Game_Resource";
        private const string PENDING_RESOURCE_KEY = "Pending_Resources";
        [SerializeField] private Service<DataService> dataService = new();


        public void Initialize()
        {
            ClaimAllPendingResource();
        }

        private void ClaimAllPendingResource()
        {
            List<ResourceData> pendingResourceData = dataService.Instance.GetData<List<ResourceData>>(PENDING_RESOURCE_KEY);
            if (pendingResourceData != null)
            {
                foreach (var pendingResource in pendingResourceData)
                {
                    AddResourceData(pendingResource);
                }

                dataService.Instance.DeleteKey(PENDING_RESOURCE_KEY);
            }

            pendingResources = new(StringComparer.Ordinal);
        }

        [NotNull]
        public override ResourceData GetResource(GameResourceKey key)
        {
            if (resources.TryGetValue(key, out var data))
            {
                return data;
            }

            data = dataService.Instance.GetData<ResourceData>($"{GAME_RESOURCE_PREFIX_KEY}_{key}");
            if (data == null)
            {
                data = new ResourceData
                {
                    gameResource = key.gameResource,
                    id = key.id,
                };
            }

            resources.Add(key, data);
            return data;
        }

        public override ResourceData GetPendingResource(string source, GameResourceKey key)
        {
            if (string.IsNullOrEmpty(source))
            {
                ResourceData data = null;
                foreach (var dictionary in pendingResources.Values.ToList())
                {
                    if (dictionary.TryGetValue(key, out var resourceData))
                    {
                        if (data == null)
                        {
                            data = resourceData.Clone();
                        }
                        else
                        {
                            data.Add(resourceData);
                        }
                    }
                }

                return data;
            }
            else
            {
                if (pendingResources.TryGetValue(source, out var dictionary))
                {
                    if (dictionary.TryGetValue(key, out var resourceData))
                    {
                        return resourceData.Clone();
                    }
                }
            }

            return null;
        }

        public override void AddResource(ResourceData unitData, EarnResourceLogData logData)
        {
            AddResourceData(unitData);
            EventBus<EarnResourceEvent>.Raise(new EarnResourceEvent(unitData.Key, unitData.EarnTrackingValue, logData));
        }

        public override void AddPendingResource(string source, ResourceData unitData, EarnResourceLogData logData)
        {
            if (!pendingResources.ContainsKey(source))
            {
                var dictionary = new Dictionary<GameResourceKey, ResourceData>();
                dictionary.Add(unitData.Key, unitData.Clone());
                pendingResources.Add(source, dictionary);
            }
            else if (!pendingResources[source].ContainsKey(unitData.Key))
            {
                pendingResources[source].Add(unitData.Key, unitData.Clone());
            }
            else
            {
                pendingResources[source][unitData.Key].Add(unitData);
            }

            SavePendingResources();
            OnAddPendingResource?.Invoke(unitData);
            EventBus<EarnResourceEvent>.Raise(new EarnResourceEvent(unitData.Key, unitData.EarnTrackingValue, logData));
        }

        public override void AddReward(RewardData rewardData, EarnResourceLogData logData)
        {
            foreach (var resourceData in rewardData.resourceUnits)
            {
                AddResource(resourceData, logData);
            }
        }

        public override void AddPendingReward(string source, RewardData rewardData, EarnResourceLogData logData)
        {
            foreach (var resourceData in rewardData.resourceUnits)
            {
                AddPendingResource(source, resourceData, logData);
            }
        }

        public override void TryBuyResourceByCurrency(CurrencyData currency, Action onSuccess)
        {
            if (!CanReduce(currency))
            {
                PopupToast.Create("Not enough coin!");
                return;
            }
            var logSpend = new SpendResourceLogData
            {
                earnType = "currency",
                earnId = currency.currency.ToString(),
                source = "non_iap"
            };
            SpendCurrency(currency, logSpend);
            onSuccess?.Invoke();
        }


        public override void ClaimPendingResource(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                foreach (var dictionary in pendingResources.Values.ToList())
                {
                    foreach (var resourceData in dictionary.Values)
                    {
                        AddResourceData(resourceData);
                    }
                }

                pendingResources.Clear();
            }
            else
            {
                if (pendingResources.TryGetValue(source, out var dictionary))
                {
                    foreach (var resourceData in dictionary.Values)
                    {
                        AddResourceData(resourceData);
                    }

                    pendingResources.Remove(source);
                }
            }

            SavePendingResources();
        }

        public override void ClaimPendingResource(string source, GameResourceKey key)
        {
            if (string.IsNullOrEmpty(source))
            {
                foreach (var dictionary in pendingResources.Values.ToList())
                {
                    if (dictionary.TryGetValue(key, out var resourceData))
                    {
                        AddResourceData(resourceData);
                        dictionary.Remove(key);
                    }
                }

                foreach (var sourceKey in pendingResources.Keys.ToList())
                {
                    if (pendingResources[sourceKey].Count == 0)
                        pendingResources.Remove(sourceKey);
                }
            }
            else
            {
                if (pendingResources.TryGetValue(source, out var dictionary))
                {
                    if (dictionary.TryGetValue(key, out var resourceData))
                    {
                        AddResourceData(resourceData);
                        dictionary.Remove(key);
                    }

                    if (dictionary.Count == 0)
                        pendingResources.Remove(source);
                }
            }

            SavePendingResources();
        }


        public override bool CanReduce(GameResourceKey key, int quantity)
        {
            var data = GetResource(key);
            return data.CanReduce(quantity);
        }

        public override bool CanReduce(CurrencyData currencyData)
        {
            return CanReduce(currencyData.currency.ToGameResourceKey(), currencyData.value);
        }

        public override void SpendCurrency(CurrencyData currencyData, SpendResourceLogData logData)
        {
            ReduceResourceQuantity(currencyData.currency.ToGameResourceKey(), currencyData.value);
            EventBus<SpendResourceEvent>.Raise(new SpendResourceEvent(currencyData.currency.ToGameResourceKey(), currencyData.value, logData));
        }

        public override void SpendResource(GameResourceKey key, int quantity, SpendResourceLogData logData)
        {
            ReduceResourceQuantity(key, quantity);
            EventBus<SpendResourceEvent>.Raise(new SpendResourceEvent(key, quantity, logData));
        }

        public override void SpendResource(GameResource key, int quantity, SpendResourceLogData logData)
        {
            ReduceResourceQuantity(key.ToGameResourceKey(), quantity);
            EventBus<SpendResourceEvent>.Raise(new SpendResourceEvent(key.ToGameResourceKey(), quantity, logData));
        }


        public override void UpdateResource(GameResourceKey key, int quantity)
        {
            var resourceData = GetResource(key);
            resourceData.quantity = quantity;
            SaveData(resourceData);
        }

        public override void UpdateResource(GameResourceKey key, long seconds)
        {
            var resourceData = GetResource(key);
            resourceData.seconds = seconds;
            SaveData(resourceData);
        }

        public override bool IsExpired(GameResourceKey key)
        {
            var data = GetResource(key);
            return data.IsExpired();
        }


        // Single choke point for a resource becoming owned. All credit paths funnel here
        // (AddResource, both ClaimPendingResource overloads, ClaimAllPendingResource), so a
        // subclass can override this once to intercept a resource type on every path instead
        // of re-implementing each public method. AddPendingResource does NOT call this by design
        // (pending is not yet owned), so overrides only see resources at actual credit time.
        protected virtual void AddResourceData(ResourceData unitData)
        {
            ResourceData data = GetResource(unitData.Key);
            data.Add(unitData);
            SaveData(data);
        }

        private void ReduceResourceQuantity(GameResourceKey key, int quantity)
        {
            ResourceData data = GetResource(key);
            data.Reduce(quantity);
            SaveData(data);
        }

        private void SaveData(ResourceData data)
        {
            dataService.Instance.SetData($"{GAME_RESOURCE_PREFIX_KEY}_{data.Key}", data);
            OnResourceUpdate?.Invoke(data.Key);
        }

        private void SavePendingResources()
        {
            if (pendingResources == null || pendingResources.Count == 0)
            {
                dataService.Instance.DeleteKey(PENDING_RESOURCE_KEY);
                return;
            }

            List<ResourceData> pendingResourceData = new List<ResourceData>();
            foreach (var dic in pendingResources.Values)
            {
                if (dic == null) continue;
                foreach (var resourceData in dic.Values)
                {
                    pendingResourceData.Add(resourceData);
                }
            }

            dataService.Instance.SetData(PENDING_RESOURCE_KEY, pendingResourceData);
        }
    }
}