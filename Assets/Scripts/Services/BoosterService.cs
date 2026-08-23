using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.GameDataManagement;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "BoosterService", menuName = "Services/Booster/Service")]
    public class BoosterService : ServiceSo, IServiceInitialize
    {
        [BoxGroup("SERVICES")] [Required] [SerializeField]
        private Service<InventoryService> inventoryService = new();

        [BoxGroup("SERVICES")] [Required] [SerializeField]
        private Service<DataService> dataService = new();

        public List<BoosterConfig> boosterConfigs = new();

        public Action<GameResource> onUnlockBooster;

        private readonly Dictionary<GameResource, BoosterData> boostersData = new();
        private readonly Dictionary<GameResource, BoosterConfig> boosterConfigCache = new();

        /// <summary>
        /// Runtime-only override for remote levelUnlock values.
        /// We never write back to the ScriptableObject to avoid dirtying the asset.
        /// </summary>

        private bool allBoosterUnlocked;

        private const string UnlockBoosterPendingSource = "unlock_booster";

        private static string BoosterNotifyPrefsKey(GameResource type) => $"Booster_Notify_{type}";

        public void Initialize()
        {
            BuildBoosterConfigCache();

            // Use 1-indexed level to match LevelLoadedEvent
            int level = ActiveSession.Current.DisplayLevelIndex + 1;
            allBoosterUnlocked = true;

            foreach (var boosterConfig in boosterConfigs)
            {
                if (!boosterConfig) continue;

                var resourceData = inventoryService.Instance.GetResource(boosterConfig.type.ToGameResourceKey());
                var boosterData = new BoosterData(boosterConfig.type, resourceData)
                {
                    unlocked = dataService.Instance.GetBool($"{boosterConfig.type}_DATA", false),
                };

                if (!boostersData.TryAdd(boosterConfig.type, boosterData))
                {
                    // Already exists (can happen if Initialize is called more than once)
                    boostersData[boosterConfig.type] = boosterData;
                }

                if (!boosterData.unlocked)
                {
                    if (GetLevelUnlock(boosterConfig) <= level)
                        UnlockAndPendingAddBooster(boosterData.boosterType);
                    else
                        allBoosterUnlocked = false;
                }
            }

            new EventBinding<LevelLoadedEvent>(OnLevelLoaded);
        }

        private void OnLevelLoaded(LevelLoadedEvent levelLoadedEvent)
        {
            if (allBoosterUnlocked) return;
            TryUnlockBoostersForLevel(levelLoadedEvent.level);
        }

        /// <summary>
        /// Check all locked boosters against <paramref name="level"/> (1-indexed) and unlock those ready.
        /// </summary>
        private void TryUnlockBoostersForLevel(int level)
        {
            allBoosterUnlocked = true;
            foreach (var kvp in boostersData)
            {
                if (kvp.Value.unlocked) continue;

                BoosterConfig boosterConfig = GetBoosterConfig(kvp.Key);
                if (!boosterConfig)
                {
                    // Config missing — treat as permanently locked to avoid NRE
                    allBoosterUnlocked = false;
                    continue;
                }

                if (GetLevelUnlock(boosterConfig) <= level)
                    UnlockAndPendingAddBooster(kvp.Value.boosterType);
                else
                    allBoosterUnlocked = false;
            }
        }

        // ─── Config helpers ───────────────────────────────────────────────

        public BoosterConfig GetBoosterConfig(GameResource boosterType)
        {
            if (boosterConfigCache.TryGetValue(boosterType, out var config))
                return config;

            BuildBoosterConfigCache();
            return boosterConfigCache.GetValueOrDefault(boosterType);
        }

        public BoosterConfig[] GetAllBoosterConfigs()
        {
            var configs = new List<BoosterConfig>(boosterConfigs.Count);
            foreach (var config in boosterConfigs)
            {
                if (config) configs.Add(config);
            }
            return configs.ToArray();
        }

        private int GetLevelUnlock(BoosterConfig boosterConfig) => boosterConfig.levelUnlock;

        /// <summary>Unlock level (1-indexed) used for gameplay and UI, same source as <see cref="ShouldShowUnlockNotifyPopup"/>.</summary>
        public int GetEffectiveLevelUnlock(BoosterConfig boosterConfig)
        {
            if (!boosterConfig) return 0;
            return GetLevelUnlock(boosterConfig);
        }

        private void BuildBoosterConfigCache()
        {
            boosterConfigCache.Clear();
            foreach (var config in boosterConfigs)
            {
                if (!config) continue;
                if (boosterConfigCache.ContainsKey(config.type))
                {
                    Debug.LogError(
                        $"Duplicate BoosterConfig detected for {config.type}. Latest config will be used.");
                }
                boosterConfigCache[config.type] = config;
            }
        }

        // ─── Data access ──────────────────────────────────────────────────

        public BoosterData GetBoosterData(GameResource boosterType)
        {
            if (boostersData.TryGetValue(boosterType, out var data)) return data;

            var resourceData = inventoryService.Instance.GetResource(boosterType.ToGameResourceKey());
            data = new BoosterData(boosterType, resourceData)
            {
                unlocked = dataService.Instance.GetBool($"{boosterType}_DATA", false),
            };
            boostersData[boosterType] = data;
            return data;
        }

        // ─── Actions ─────────────────────────────────────────────────────

        public void BuyBooster(BoosterConfig boosterConfig, Action onSuccess)
        {
            inventoryService.Instance.TryBuyResourceByCurrency(boosterConfig.price, () =>
            {
                onSuccess?.Invoke();
            });
        }

        public void UnlockAndPendingAddBooster(GameResource boosterType, bool forceUnlock = false)
        {
            dataService.Instance.SetBool($"{boosterType}_DATA", true);

            if (!boostersData.TryGetValue(boosterType, out var boosterData))
            {
                var resourceData = inventoryService.Instance.GetResource(boosterType.ToGameResourceKey());
                boosterData = new BoosterData(boosterType, resourceData);
                boostersData[boosterType] = boosterData;
            }
            boosterData.unlocked = true;

            BoosterConfig config = GetBoosterConfig(boosterType);
            if (!config) return;

            var logData = new EarnResourceLogData
            {
                spendType = "unlock",
                spendId = "unlock",
            };

            if (GetLevelUnlock(config) == 0 || forceUnlock)
                inventoryService.Instance.AddResource(new ResourceData(boosterType, config.defaultValue), logData);
            else
                inventoryService.Instance.AddPendingResource(
                    UnlockBoosterPendingSource, new ResourceData(boosterType, config.defaultValue), logData);

            onUnlockBooster?.Invoke(boosterType);
        }

        /// <summary>Whether to show the unlock notify popup for this booster at <paramref name="level"/> (1-indexed).</summary>
        public bool ShouldShowUnlockNotifyPopup(BoosterConfig config, int level)
        {
            if (!config) return false;
            return GetLevelUnlock(config) <= level
                   && !dataService.Instance.GetBool(BoosterNotifyPrefsKey(config.type), false);
        }

        /// <summary>Mark unlock notify as seen and claim pending resources for this booster only.</summary>
        public void OnUnlockNotifyPopupConfirmed(BoosterConfig config)
        {
            if (!config) return;
            dataService.Instance.SetBool(BoosterNotifyPrefsKey(config.type), true);
            inventoryService.Instance.ClaimPendingResource(
                UnlockBoosterPendingSource, config.type.ToGameResourceKey());
        }

        public bool CanUseBooster(GameResource boosterType)
        {
            var boosterData = GetBoosterData(boosterType);
            if (!boosterData.unlocked)
            {
                Debug.Log($"Booster {boosterType} is not unlocked.");
                Debug.Log($"Booster {boosterData} has {boosterData.resourceData.quantity}.");
                return false;
            }
            return inventoryService.Instance.CanReduce(boosterType.ToGameResourceKey(), 1);
        }

        public bool IsBoosterUnlock(GameResource booster)
        {
            return GetBoosterData(booster).unlocked;
        }
    }

    public class BoosterData
    {
        public GameResource boosterType;
        public bool unlocked;
        public ResourceData resourceData;

        public BoosterData(GameResource boosterType, ResourceData resourceData)
        {
            this.boosterType = boosterType;
            this.resourceData = resourceData;
        }
    }
}
