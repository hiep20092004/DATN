using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;
using DOTween = DG.Tweening.DOTween;

namespace WaterFlow.Game
{
    public class GameController : Singleton<GameController>
    {
        private const int MAX_GAMEPLAY_FRAME_RATE = 120;

        [SerializeField] UIController uiController;
        [SerializeField] LevelDatabase levelDatabase;
        [SerializeField] private CanvasGroup MainUI;

        private bool isGameActivated;
        private bool isGameFinished;
        private bool isLevelSpawnCompleted;
        private bool pendingGameActivation;
        private readonly Dictionary<string, int> blockUiReasons = new Dictionary<string, int>(StringComparer.Ordinal);

        public bool IsGameActivated => isGameActivated;
        public int RevivedTime;
        public LoseReason CurrentLoseReason { get; private set; } = LoseReason.None;

        protected override void OnAwake()
        {
            base.OnAwake();
            LevelController.Instance.Init(levelDatabase);
            RevivedTime = 0;

            InitControllers();

            isGameActivated = false;
            isGameFinished = false;
            isLevelSpawnCompleted = false;
            pendingGameActivation = false;

            UpdateGameMusic();
        }

        private void InitControllers()
        {
            uiController.Init();

            RaycastController raycastController = gameObject.GetOrAdd<RaycastController>();
            raycastController.Init();
            uiController.InitPages();

            var particlesController = gameObject.GetOrAdd<ParticlesController>();
            particlesController.Init();

            var powerUpController = gameObject.GetOrAdd<PowerUpController>();
            powerUpController.Init();

            var boosterNavController = gameObject.GetOrAdd<BoosterNavigationController>();
            boosterNavController.Init();

            var tutorialController = gameObject.GetOrAdd<TutorialController>();
            tutorialController.Init();

            gameObject.GetOrAdd<NotifyPopupQueue>();
        }

        private static void UpdateGameMusic()
        {
            var audioService = Services.AudioService;
            AudioId targetMusic = ResolveIngameMusic();
            if (audioService.GetCurrentMusic() != targetMusic.ToString())
                audioService.PlayMusic(targetMusic);
        }

        private static AudioId ResolveIngameMusic()
        {
            var representation = LevelController.Instance.LevelRepresentation;
            LevelType levelType = representation?.LevelData ? representation.LevelData.Type : LevelType.Normal;
            switch (levelType)
            {
                case LevelType.Hard:
                    return AudioId.BGM_Ingame_Universal_HardLevel;
                case LevelType.VeryHard:
                    return AudioId.BGM_Ingame_Universal_SuperHardLevel;
                default:
                    return AudioId.BGM_Ingame_Funny;
            }
        }

        private void Start()
        {
            UIController.ShowPage<UIGame>();
            int level = ActiveSession.Current.DisplayLevelIndex + 1;
            PowerUpController.OnLevelLoaded(level);

            if (level >= NOTIFICATION_PERMISSION_LEVEL_THRESHOLD)
                NotificationPermissionPrompt.RequestOnce();
        }

        public async UniTask OnCompleteLevel(bool isForce = false)
        {
            if (isGameFinished || (!LevelController.Instance.LevelStarted && !isForce))
                return;
            isGameFinished = true;

            LevelData completedLevelData = LevelController.Instance.LevelRepresentation.LevelData;
            int completedLevel = ActiveSession.Current.DisplayLevelIndex + 1;
            var type = (int)completedLevelData.Type;

            SpecialLevelService specialLevelService = Services.SpecialLevelService;
            bool isSpecialLevel = specialLevelService is { IsActive: true };

            int endTurnLevel = isSpecialLevel ? specialLevelService.CurrentOrderIndex + 1 : completedLevel;
            int realLevel = ActiveSession.ResolveRealLevelNumber(endTurnLevel);

            EventBus<LevelEndedEvent>.Raise(new LevelEndedEvent()
            {
                gameMode = ResolveGameMode(specialLevelService),
                level = completedLevel,
                realLevel = realLevel,
                success = true,
                suppressProgression = false,
            });

            EventBus<LevelEndTurnEvent>.Raise(new LevelEndTurnEvent()
            {
                gameMode = ResolveGameMode(specialLevelService),
                level = endTurnLevel,
                realLevel = realLevel,
                levelType = type,
                success = true,
                continueTimes = RevivedTime,
            });

            if (isSpecialLevel)
                specialLevelService.FinalizeLevel();

            LevelController.Instance.OnGameEnd(true);
            OnLevelCompleted();
            CollectNewFeatures();

            BlockUI("completeLevelUI");
            // Use cancellation so the await doesn't leak across a Replay call.
            await UniTask.WaitForSeconds(0.25f,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            UnblockUI("completeLevelUI");

            if (isSpecialLevel)
            {
                PanelManager.Instance.OpenForget<PopupPreWin>();
                return;
            }

            PanelManager.Instance.OpenForget<PopupPreWin>();
        }

        // Entering a level at or above this 1-based index counts as the player being engaged
        // enough to be prompted for notifications.
        private const int NOTIFICATION_PERMISSION_LEVEL_THRESHOLD = 7;

        public void GameOver(LoseReason reason, float delayVisual = 0f)
        {
            if (isGameFinished) return;
            isGameFinished = true;
            CurrentLoseReason = reason;

            LevelController.Instance.OnGameEnd(false);

            int failLevel = ActiveSession.Current.DisplayLevelIndex + 1;
            SpecialLevelService specialLevelService = Services.SpecialLevelService;

            EventBus<LevelEndedEvent>.Raise(new LevelEndedEvent()
            {
                gameMode = ResolveGameMode(specialLevelService),
                level = failLevel,
                realLevel = ActiveSession.ResolveRealLevelNumber(failLevel),
                success = false,
                cause = reason.ToString(),
            });

            Tween.DelayedCall(delayVisual, () =>
            {
                Services.AudioService.PlaySound(AudioId.Lose);
                PanelManager.Instance.OpenForget<PopupRevive>();
            });
        }

        public void Revive(int seconds = 60)
        {
            RevivedTime++;
            isGameFinished = false;
            LevelRuntimeData.Current.RecordRevive();

            int reviveLevel = ActiveSession.Current.DisplayLevelIndex + 1;
            EventBus<LevelStartedEvent>.Raise(new LevelStartedEvent()
            {
                gameMode = GameMode.Classic,
                level = reviveLevel,
                realLevel = ActiveSession.ResolveRealLevelNumber(reviveLevel),
                phase = 0,
            });
            LevelController.Instance.OnRevived(seconds, CurrentLoseReason);
            CurrentLoseReason = LoseReason.None;
        }

        public void Replay()
        {
            DOTween.KillAll();
            Services.TransitionService.SwitchScene(GamePlacement.Game);
        }

        public void Unload(bool needSave, SimpleCallback onUnloaded)
        {
            if (needSave) SaveController.Save(true);
            onUnloaded?.Invoke();
        }

        private void OnEnable()
        {
            // Subscribe to block-touch event to trigger first game activation.
            RaycastController.OnObjectTouched -= OnObjectTouched;
            RaycastController.OnObjectTouched += OnObjectTouched;
            ApplyGameplayFrameRate();
        }

        private void ApplyGameplayFrameRate()
        {
            Resolution best = GetHighestRefreshResolution();
            int deviceRefreshRate = Mathf.RoundToInt((float)best.refreshRateRatio.value);

            int currentRefreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            if (deviceRefreshRate > currentRefreshRate)
            {
                Screen.SetResolution(best.width, best.height, Screen.fullScreenMode, best.refreshRateRatio);
            }

            Application.targetFrameRate = deviceRefreshRate > 0
                ? Mathf.Min(deviceRefreshRate, MAX_GAMEPLAY_FRAME_RATE)
                : 60;

#if UNITY_ANDROID
            // Adaptive/LTPO panels treat Unity's per-surface setFrameRate request as a soft hint and
            // fall back to 60Hz when the screen is static. Pinning the window's preferredRefreshRate
            // forces SurfaceFlinger to hold the high-refresh mode regardless of on-screen motion.
            ApplyAndroidPreferredRefreshRate((float)best.refreshRateRatio.value);
#endif

            LogFrameRateInfo(deviceRefreshRate);
        }

#if UNITY_ANDROID
        // Sets WindowManager.LayoutParams.preferredRefreshRate on the activity window. Unlike the
        // per-surface setFrameRate hint, this window attribute persists across pause/resume and is
        // honored more strongly by the system compositor. Must run on the Android UI thread.
        private static void ApplyAndroidPreferredRefreshRate(float hz)
        {
            if (Application.isEditor || hz <= 0f) return;

            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;

                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using var window = activity.Call<AndroidJavaObject>("getWindow");
                        using var layoutParams = window.Call<AndroidJavaObject>("getAttributes");
                        layoutParams.Set<float>("preferredRefreshRate", hz);
                        window.Call("setAttributes", layoutParams);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[FrameRate] preferredRefreshRate apply failed: {e.Message}");
                    }
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FrameRate] preferredRefreshRate setup failed: {e.Message}");
            }
        }
#endif

        // Highest-refresh mode at the active resolution, so we raise refresh without changing render size.
        private static Resolution GetHighestRefreshResolution()
        {
            Resolution best = Screen.currentResolution;
            foreach (var res in Screen.resolutions)
            {
                bool sameSize = res.width == best.width && res.height == best.height;
                if (sameSize && res.refreshRateRatio.value > best.refreshRateRatio.value)
                    best = res;
            }
            return best;
        }

        private static void LogFrameRateInfo(int deviceRefreshRate)
        {
            var cur = Screen.currentResolution;
            Debug.Log(
                $"[FrameRate] target={Application.targetFrameRate} | requestedMode={deviceRefreshRate}Hz | " +
                $"currentMode={cur.width}x{cur.height}@{cur.refreshRateRatio.value:0.##}Hz | " +
                $"vSyncCount={QualitySettings.vSyncCount} | quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} | " +
                $"onDemandInterval={UnityEngine.Rendering.OnDemandRendering.renderFrameInterval} | modes={Screen.resolutions.Length}");
        }

        private void OnDisable()
        {
            RaycastController.OnObjectTouched -= OnObjectTouched;
        }

        private void OnObjectTouched()
        {
            ActivateGame();
            RaycastController.OnObjectTouched -= OnObjectTouched;
        }

        public void OnLevelSpawnStart()
        {
            RaycastController.Disable("mapSpawn");
            BlockUI("mapSpawn");
        }

        public void OnLevelSpawnCompleted()
        {
            isLevelSpawnCompleted = true;
            RaycastController.Enable("mapSpawn");
            UnblockUI("mapSpawn");
            if (pendingGameActivation)
            {
                pendingGameActivation = false;
                DoActivateGame();
            }
        }

        public void ActivateGame()
        {
            if (isGameActivated) return;
            if (!isLevelSpawnCompleted)
            {
                pendingGameActivation = true;
                return;
            }
            DoActivateGame();
        }

        private void DoActivateGame()
        {
            isGameActivated = true;
            LevelController.Instance.OnGameActivated();
        }

        void OnLevelCompleted()
        {
            ActiveSession currentSession = ActiveSession.Current;
            int levelIndex = currentSession.DisplayLevelIndex;

            if (Services.SpecialLevelService is { IsActive: true })
                return;

            int nextMainLevelIndex = levelIndex + 1;
            currentSession.SetLevelIndex(nextMainLevelIndex);

            TryScheduleSpecialLevel(nextMainLevelIndex);
        }

        private void TryScheduleSpecialLevel(int nextMainLevelIndex)
        {
            SpecialLevelScheduleConfig config = levelDatabase != null ? levelDatabase.SpecialLevelScheduleConfig : null;
            if (config == null) return;

            // nextMainLevelIndex is the 0-based index the session was just advanced to,
            // i.e. the level the player is about to play.
            Services.SpecialLevelService.TryScheduleForUpcomingLevel(config, nextMainLevelIndex);
        }

        private void CollectNewFeatures() { }

        private async UniTask ProcessEarlyLevelAutoNext(LevelData completedLevelData)
        {
            GameWinFlowService.GrantWinReward(completedLevelData);
            SaveController.Save(true);

            if (Services.SpecialLevelService is { HasPendingSpecialLevel: true })
            {
                Services.TransitionService.SwitchScene(GamePlacement.Game);
                return;
            }

            ActiveSession activeSession = ActiveSession.Current;
            int displayedLevelIndex = activeSession.DisplayLevelIndex;
            int nextLevelIndex = activeSession.GetLevelIndex(displayedLevelIndex);

            isGameFinished = false;
            isGameActivated = false;
            isLevelSpawnCompleted = false;
            pendingGameActivation = false;
            RevivedTime = 0;
            CurrentLoseReason = LoseReason.None;

            // Clear any pending notify popups from the completed level so they
            // don't appear after transitioning to the next level.
            NotifyPopupQueue.Instance.Clear();

            // Clear UI block counters that should not carry over to the next level.
            blockUiReasons.Clear();
            UpdateUiBlockedState();

            BlockUI("levelTransition");
            await LevelController.Instance.SwipeToNextLevelAsync(nextLevelIndex);
            UnblockUI("levelTransition");

            RaycastController.Enable("GameEnd");

            RaycastController.OnObjectTouched -= OnObjectTouched;
            RaycastController.OnObjectTouched += OnObjectTouched;

            UIGame uiGame = UIController.GetPage<UIGame>();
            uiGame?.RefreshForNextLevel(displayedLevelIndex);

            PowerUpController.OnLevelLoaded(displayedLevelIndex + 1, false);
        }

        public void BlockUI(string reason = "default")
        {
            if (blockUiReasons.TryGetValue(reason, out var count))
                blockUiReasons[reason] = count + 1;
            else
                blockUiReasons[reason] = 1;

            UpdateUiBlockedState();
        }

        public void UnblockUI(string reason = "default")
        {
            if (!blockUiReasons.TryGetValue(reason, out var count)) return;

            if (count <= 1)
                blockUiReasons.Remove(reason);
            else
                blockUiReasons[reason] = count - 1;

            UpdateUiBlockedState();
        }

        private void UpdateUiBlockedState()
        {
            if (!MainUI) return;
            bool shouldBlock = blockUiReasons.Count > 0;
            MainUI.blocksRaycasts = !shouldBlock;
            MainUI.interactable = !shouldBlock;
        }

        private static GameMode ResolveGameMode(SpecialLevelService specialLevelService)
        {
            if (specialLevelService is not { IsActive: true })
                return GameMode.Classic;

            return specialLevelService.CurrentLevel != null
                ? specialLevelService.CurrentLevel.Mode.ToGameMode()
                : GameMode.RescueBlock;
        }
    }
}
