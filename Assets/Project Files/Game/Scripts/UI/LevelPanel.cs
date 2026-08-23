using System;
using System.Collections.Generic;
using DG.Tweening;
using Popup;
using WaterFlow.Core;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using Ease = DG.Tweening.Ease;
using Tween = DG.Tweening.Tween;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelDifficultyConfig
    {
        [Header("Background")]
        public GameObject background;
        public Color bgColor;
        
        [Header("Spine")]
        public string skinName;
        public string animationName;

        [Header("Particle")]
        public ParticleSystem impactEffect;
        public ParticleSystem transitionEffect;

        [Header("Material")]
        public Material fontMaterial;

        [Header("Sound")]
        public AudioClip levelIntroSound;
        [Min(0f)] public float levelIntroSoundDelay;
    }

    public class LevelPanel : LevelPanelBase
    {
        private const string BlockUIKey = "LevelWarning";

        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private SkeletonGraphic levelAnim;
        [SerializeField] private float scalePunch = 0.2f;
        [SerializeField] private float timePunch = 0.2f;
        [SerializeField] private ParticleGradientColorView levelAddTimeParticle;
        
        [SerializeField]
        private SerializedDictionary<LevelType, LevelDifficultyConfig> difficultyConfigs;

        private LevelDifficultyConfig currentConfig;
        private LevelRepresentation levelRepresentation;
        private LevelType levelType;
        private int currentLevelIndex;

        public override void Init()
        {
            int levelIndex = ActiveSession.Current.DisplayLevelIndex;
            currentLevelIndex = levelIndex;
            levelRepresentation = LevelController.Instance.LevelRepresentation;
            levelType = levelRepresentation.LevelData.Type;

            UpdateLevelText(false);
            UpdateBg(LevelType.Normal);

            PlayLevelAnimation().Forget();
        }

        private async UniTask PlayLevelAnimation()
        {
            levelAnim.gameObject.SetActive(false);

            if (levelType == LevelType.Normal)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.5));
                await TryShowObstacleUnlockPopup();
            }
            else
            {
                if (difficultyConfigs.TryGetValue(levelType, out var config)
                    && !string.IsNullOrEmpty(config.skinName)
                    && !string.IsNullOrEmpty(config.animationName))
                {
                    RaycastController.Disable("LevelPanel");
                    GameController.Instance.BlockUI(BlockUIKey);

                    levelAnim.gameObject.SetActive(true);

                    if (config.levelIntroSound)
                    {
                        DOVirtual.DelayedCall(config.levelIntroSoundDelay, () =>
                        {
                            _ = Services.AudioService.PlaySound(config.levelIntroSound.name, dunking: true);
                        });
                    }

                    SetSkin(config.skinName);
                    TrackEntry entry = levelAnim.GetAnimationState().SetAnimation(0, config.animationName, false);
                    entry.Complete += OnLevelAnimComplete;
                    // RaycastController.Enable and UnblockUI called in OnLevelAnimComplete
                }
                else
                {
                    // No animation configured for this type — skip straight to obstacle notify.
                    RaycastController.Disable("LevelPanel");
                    GameController.Instance.BlockUI(BlockUIKey);
                    RaycastController.Enable("LevelPanel");
                    GameController.Instance.UnblockUI(BlockUIKey);
                    await TryShowObstacleUnlockPopup();
                }
            }
        }

        private void UpdateLevelText(bool updateMat = true)
        {
            if (updateMat && difficultyConfigs.TryGetValue(levelType, out var config) && config.fontMaterial)
            {
                timeVisualiser.UpdateTimeMaterial(config.fontMaterial);
                levelAddTimeParticle.SetColor(config.bgColor);
            }

            levelText.text = LevelLabel.ForLevel(currentLevelIndex + 1);
        }

        private void OnLevelAnimComplete(TrackEntry trackEntry)
        {
            trackEntry.Complete -= OnLevelAnimComplete;
            if (levelAnim && levelAnim.gameObject)
                levelAnim.gameObject.SetActive(false);

            if (difficultyConfigs.TryGetValue(levelType, out var config))
            {
                if (config.impactEffect) config.impactEffect.Play();
                if (config.transitionEffect) config.transitionEffect.Play();
                config.background.transform.DOPunchScale(Vector3.one * scalePunch, timePunch).SetEase(Ease.OutSine);
            }

            UpdateLevelText();
            UpdateBg(levelType);

            HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);
            Services.AudioService.PlaySound(AudioId.Warning_Hit);

            TryShowObstacleUnlockPopup().Forget();

            RaycastController.Enable("LevelPanel");
            GameController.Instance.UnblockUI(BlockUIKey);
        }

        /// <summary>
        /// Check if there are new obstacle effects at this level that haven't been notified yet,
        /// then enqueue <see cref="ObstacleUnlockNotifyPopup"/> for all of them via
        /// <see cref="NotifyPopupQueue"/> (sequential, one popup at a time).
        /// </summary>
        private async UniTask TryShowObstacleUnlockPopup()
        {
            ObstacleUnlockNotifyPopup.BeginFlow();
            try
            {
                // Snapshot the level context so level transitions mid-await don't corrupt data.
                LevelRepresentation capturedRep = levelRepresentation;

                // If the level changed while we were awaiting, abort.
                if (capturedRep != LevelController.Instance.LevelRepresentation) return;

                LevelDatabase levelDatabase = LevelController.Instance.LevelDatabase;
                List<ObstacleUnlockEntry> allEntries =
                    levelDatabase.GetObstacleUnlockEntriesForLevel(capturedRep.LevelData);

                var unnotifiedEntries = new List<ObstacleUnlockEntry>();
                foreach (ObstacleUnlockEntry entry in allEntries)
                {
                    if (!ObstacleUnlockNotifyPopup.HasBeenNotified(entry))
                        unnotifiedEntries.Add(entry);
                }

                if (unnotifiedEntries.Count == 0) return;

                // Wait until the level spawn is complete before showing the popup.
                await UniTask.WaitUntil(() => capturedRep.SpawnComplete,
                    cancellationToken: this.GetCancellationTokenOnDestroy());

                // Safety check again after the wait.
                if (capturedRep != LevelController.Instance.LevelRepresentation) return;

                ObstacleUnlockNotifyPopup.Show(unnotifiedEntries);
            }
            finally
            {
                ObstacleUnlockNotifyPopup.EndFlow();
            }
        }

        public override void OnRefreshForNextLevel(int levelIndex)
        {
            currentLevelIndex = levelIndex;
            levelRepresentation = LevelController.Instance.LevelRepresentation;
            levelType = levelRepresentation.LevelData.Type;
            UpdateLevelText(false);
            UpdateBg(levelType);
        }

        private void SetSkin(string skinName)
        {
            levelAnim.GetAnimationState().ClearTracks();
            levelAnim.initialSkinName = skinName;
            levelAnim.Initialize(true);
        }

        private void UpdateBg(LevelType type)
        {
            foreach (var kvp in difficultyConfigs)
            {
                if (kvp.Value.background)
                    kvp.Value.background.SetActive(kvp.Key == type);
            }
        }
    }
}
