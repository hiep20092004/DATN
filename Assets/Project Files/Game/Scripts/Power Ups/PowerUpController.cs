using System;
using System.Collections.Generic;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement;
using UnityEngine;

namespace WaterFlow.Game
{
    [StaticUnload]
    public class PowerUpController : MonoBehaviour
    {
        private static PowerUpController instance;

        [DrawReference]
        [SerializeField] PowerUpPopup powerUpPopup;

        public static PowerUpBehavior[] ActivePowerUps { get; private set; }
        public static PowerUpPopup PowerUpPopup { get; private set; }
        public static PowerUpBehavior SelectedPowerUp { get; private set; }
        
        public static event PowerUpCallback Used;

        private static Dictionary<PowerUpType, PowerUpBehavior> powerUpsLink;
        private EventBinding<LevelEndedEvent> levelEndedEvent;
        private Transform behaviorsContainer;

        public void Init()
        {
            instance = this;

            behaviorsContainer = new GameObject("[POWER UPS]").transform;
            behaviorsContainer.gameObject.isStatic = true;

            var powerUpConfigs = Services.BoosterService.GetAllBoosterConfigs();
            ActivePowerUps = new PowerUpBehavior[powerUpConfigs.Length];
            powerUpsLink = new Dictionary<PowerUpType, PowerUpBehavior>();

            for (int i = 0; i < ActivePowerUps.Length; i++)
            {
                BoosterConfig boosterConfig = powerUpConfigs[i];
                BasePowerUpConfig config = powerUpConfigs[i].GetPowerUpConfig();

                // Initialize power ups
                config.Init();

                // Spawn behavior object 
                GameObject powerUpBehaviorObject = Instantiate(config.BehaviorPrefab, behaviorsContainer);
                powerUpBehaviorObject.transform.ResetLocal();

                var powerUpBehavior = powerUpBehaviorObject.GetComponent<PowerUpBehavior>();
                powerUpBehavior.InjectConfig(boosterConfig);

                ActivePowerUps[i] = powerUpBehavior;

                // Add power up to dictionary
                powerUpsLink.Add(config.Type, ActivePowerUps[i]);
            }

            InitBehaviors();
            
            levelEndedEvent = new EventBinding<LevelEndedEvent>(OnLevelEnded);
        }

        private void OnDestroy()
        {
            EventBus<LevelEndedEvent>.Deregister(levelEndedEvent);
        }

        private void InitBehaviors()
        {
            foreach (PowerUpBehavior powerUp in ActivePowerUps)
            {
                powerUp.gameObject.SetActive(true);
                powerUp.Init();
            }
            PowerUpPopup = powerUpPopup;
            powerUpPopup?.InitializeItemViews();
        }

        public static void OnLevelLoaded(int level, bool isTween = true)
        {
            PowerUpPopup?.OnLevelLoaded(level, isTween);

            foreach (var powerUp in ActivePowerUps)
            {
                powerUp.OnLevelLoaded(level);
            }
        }

        private void OnLevelEnded(LevelEndedEvent levelEvent)
        {
            CleanupAfterLevelEnded();
        }

        private void CleanupAfterLevelEnded()
        {
            UnselectPowerUp();
            PowerUpPopup?.OnLevelFinished();
            foreach (var powerUp in ActivePowerUps)
            {
                powerUp.OnLevelEnded();
            }
        }

        public static bool SelectPowerUp(PowerUpType powerUpType)
        {
            if (!powerUpsLink.TryGetValue(powerUpType, out var powerUpBehavior))
            {
                Debug.LogWarning($"[Power Ups]: Power up with type {powerUpType} isn't registered.");
                return false;
            }
            
            if (SelectedPowerUp == powerUpBehavior)
            {
                if (SelectedPowerUp.IsSelectTargetType())
                    ResumeAllPUTimers();

                SelectedPowerUp.OnDeselected();

                PowerUpPopup?.OnPowerUpUnselected(SelectedPowerUp);

                SelectedPowerUp = null;

                return false;
            }

            if (powerUpBehavior.IsBusy) return false;
            if (SelectedPowerUp)
            {
                if (SelectedPowerUp.IsSelectTargetType())
                    ResumeAllPUTimers();

                SelectedPowerUp.OnDeselected();

                PowerUpPopup?.OnPowerUpUnselected(SelectedPowerUp);

                SelectedPowerUp = null;
            }

            bool hasTarget = powerUpBehavior.OnSelected(out string notifyWhenNotFoundTarget);

            if (!hasTarget)
            {
                PowerUpPopup?.ShowNotifyNotFoundTarget(notifyWhenNotFoundTarget);
                return false;
            }

            if (powerUpBehavior.IsSelectTargetType())
                PauseAllPUTimers();
            
            PowerUpPopup?.OnPowerUpSelected(powerUpBehavior);
            SelectedPowerUp = powerUpBehavior;
            
            return true;
        }

        public static void ApplyToElement(IClickableObject clickableObject, Vector3 clickPosition)
        {
            if (!SelectedPowerUp) return;
            if (SelectedPowerUp.ApplyToElement(clickableObject, clickPosition))
            {
                var settings = SelectedPowerUp.Config;
            
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.SoftImpact);
            
                // Consume booster via InventoryService
                ConsumeBooster(settings.type);
            
                PowerUpPopup?.RedrawPanels();
            
                Used?.Invoke(settings.GetPowerUpConfig().Type);
                
                PowerUpPopup?.OnPowerUpUnselected(SelectedPowerUp);

                if (SelectedPowerUp.IsSelectTargetType())
                    ResumeAllPUTimers();

                SelectedPowerUp.OnDeselected();
                SelectedPowerUp = null;
            }
            else
            {
                Services.AudioService.PlaySound(AudioId.Booster_Denied);
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.Deny);
            }
        }
        
        private static InventoryService InventoryService => GameSystem.GetService<InventoryService>();

        /// <summary>Looks up the config for a registered booster by its <see cref="GameResource"/> type (e.g. for analytics that need booster-specific config values).</summary>
        public static BasePowerUpConfig GetPowerUpConfig(GameResource boosterType)
        {
            foreach (var powerUp in ActivePowerUps)
            {
                if (powerUp.Config.type == boosterType)
                    return powerUp.PowerUpConfig;
            }
            return null;
        }

        private static void ConsumeBooster(GameResource boosterType)
        {
            // Test sandbox (Level Editor test play): power-ups are free, never touch inventory.
            if (LevelController.IsTestModeActive)
                return;

            InventoryService.SpendResource(boosterType.ToGameResourceKey(), 1, new SpendResourceLogData
            {
                earnType = "booster",
                earnId = boosterType.ToString(),
                source = "gameplay"
            });
            LevelRuntimeData.Current.RecordBoosterUsed(boosterType);
            EventBus<UseBoosterEvent>.Raise(new UseBoosterEvent() { booster = boosterType });
        }

        public static void UnselectPowerUp()
        {
            if (SelectedPowerUp)
            {
                if (SelectedPowerUp.IsSelectTargetType())
                    ResumeAllPUTimers();

                SelectedPowerUp.OnDeselected();

                PowerUpPopup?.OnPowerUpUnselected(SelectedPowerUp);

                SelectedPowerUp = null;
            }
        }

        public static bool UsePowerUp(PowerUpType powerUpType)
        {
            if (!powerUpsLink.TryGetValue(powerUpType, out var powerUpBehavior))
            {
                Debug.LogWarning($"[Power Ups]: Power up with type {powerUpType} isn't registered.");
                return false;
            }

            if (powerUpBehavior.IsBusy) return false;
            if (!powerUpBehavior.Activate()) return false;
            var powerUpConfig = powerUpBehavior.Config;
           
            // Consume booster via InventoryService
            ConsumeBooster(powerUpConfig.type);
            
            PowerUpPopup?.RedrawPanels();
            
            Used?.Invoke(powerUpType);
            
            return true;
        }

        public static void OnGamePausedEvent(bool isPaused)
        {
            foreach (var powerUp in ActivePowerUps)
            {
                PUTimer timer = powerUp.GetTimer();
                if(timer!= null)
                {
                    if (isPaused)
                    {
                        timer.Pause();
                    }
                    else
                    {
                        timer.Resume();
                    }
                }
            }
        }

        private static void PauseAllPUTimers()
        {
            if (ActivePowerUps == null) return;
            foreach (var powerUp in ActivePowerUps)
            {
                powerUp.GetTimer()?.Pause();
            }
        }

        private static void ResumeAllPUTimers()
        {
            if (ActivePowerUps == null) return;
            foreach (var powerUp in ActivePowerUps)
            {
                powerUp.GetTimer()?.Resume();
            }
        }
        
        public static int GetPowerUpAmount(PowerUpType powerUpType)
        {
            if (powerUpsLink.TryGetValue(powerUpType, out var powerUpBehavior))
            {
                return InventoryService.GetResource(powerUpBehavior.Config.type.ToGameResourceKey()).quantity;
            }

            return 0;
        }

        public static PowerUpBehavior GetPowerUpBehavior(PowerUpType powerUpType)
        {
            if (powerUpsLink.TryGetValue(powerUpType, out var powerUpBehavior))
            {
                return powerUpBehavior;
            }

#if UNITY_EDITOR
            Debug.LogError($"[Power Ups]: Power up with type {powerUpType} isn't registered.");
#endif

            return null;
        }

        public static void ResetBehaviors()
        {
            for (int i = 0; i < ActivePowerUps.Length; i++)
            {
                ActivePowerUps[i].ResetBehavior();
            }
        }

        private static void UnloadStatic()
        {
            Used = null;

            powerUpsLink = null;

            ActivePowerUps = null;
            PowerUpPopup = null;
            SelectedPowerUp = null;
        }

        public delegate void PowerUpCallback(PowerUpType powerUpType);
    }
}