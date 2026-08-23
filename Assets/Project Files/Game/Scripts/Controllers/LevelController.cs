using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;

namespace WaterFlow.Game
{
    public class LevelController : Singleton<LevelController>, ILevelContentProvider
    {
        [SerializeField] EnvironmentData environmentData;
        [SerializeField] BlocksVisualsData BlocksVisualsSimple;
        [SerializeField] BlocksVisualsData BlocksVisualsNew;
        [SerializeField] LevelDatabase levelDatabase;
        [SerializeField] BlockConfig blockConfig;

        private int currentLevel;
        private bool levelLoaded;
        private LevelRepresentation levelRepresentation;
        private BlockMovementManager movementManager;
        // Last grid cell at which the near-gate auto-collect scan ran for the held block; null when
        // no block is picked. Gate proximity only changes on cell crossings, so we scan on change.
        private Vector2Int? lastGateCheckGridPos;
        private CameraController cameraController;
        private bool isBlockMovementConfigSubscribed;
        private LevelLoaderSystem levelLoader;
        private LevelRepresentation stagingRepresentation;
        private int stagingLevelIndex;
        private LevelRuntimePrecomputedCache runtimePrecomputedCache;
        private GoldBlockCoinManager goldBlockCoinManager;
        private readonly IWinConditionFactory winConditionFactory = new WinConditionFactory();
        private IWinCondition winCondition;
        private GameMode activeGameMode;
        private BlocksVisualsData blocksVisuals;
        private BlockTheme blockTheme;
        
        public IBlockCellWatchService CellWatchService { get; private set; }
        public IBlockCellEventNotifier CellWatchNotifier { get; private set; }

        private const float SWIPE_TRANSITION_DURATION = 0.5f;
        private const float SWIPE_LEVEL_GAP = 20f;

        private Vector2Int? pickedBlockGridPos;
        public LevelRepresentation LevelRepresentation => levelRepresentation;
        public LevelRepresentation StagingRepresentation => stagingRepresentation;
        public LevelRuntimePrecomputedCache RuntimePrecomputedCache => runtimePrecomputedCache;
        public GameplayTimer GameplayTimer { get; private set; }
        public LevelDatabase LevelDatabase => levelDatabase;
        public LevelGeneralConfigData LevelGeneralConfig => levelDatabase.LevelGeneralConfigData;
        public CameraController CameraController => cameraController;
        public bool IsLevelLoaded => levelLoaded;
        public BlockConfig BlockConfig => blockConfig;
        public BlockMovementManager MovementManager => movementManager;
        public BlockTheme BlockTheme => blockTheme;

        /// <summary>Total collectible gold for the current Gold Mode level (0 when not a Gold Mode level). Denominator for gold-mode completion tracking.</summary>
        public int GoldMaxReward => goldBlockCoinManager?.MaxPossibleGold ?? 0;

        public bool LevelStarted { get; private set; }
        
        protected EventBinding<GamePausedEvent> pauseEvent;

        public event SimpleCallback LevelLoaded;

        public void Init(LevelDatabase levelDatabase)
        {
            if (!levelDatabase)
                Debug.LogError("Level database is not set. Please check the Game Data scriptable object.");

            this.levelDatabase = levelDatabase;
            levelDatabase.Init();

            levelLoader = new LevelLoaderSystem(levelDatabase);
            movementManager = new BlockMovementManager(blockConfig);
            movementManager.BlockSnapCompleted = OnBlockSnapCompleted;
            RegisterBlockMovementConfigEvents();

            CacheBlockVisualData();

            GameplayTimer = new GameplayTimer();
            GameplayTimer.OnTimerFinished += OnGameplayTimerFinished;
            GameplayTimer.OnActiveStateChanged += OnGameplayTimerChangeActiveState;
            levelLoaded = false;

            ActiveSession activeSession = ActiveSession.Current;
            int displayedLevelIndex = activeSession.DisplayLevelIndex;

            bool isEditorTestPlay = false;
#if UNITY_EDITOR
            isEditorTestPlay = LevelDatabase.IsEditorPlayModeLevelOverrideActive;
#endif
            if (!isEditorTestPlay)
            {
                Services.SpecialLevelService.TryScheduleForUpcomingLevel(
                    levelDatabase != null ? levelDatabase.SpecialLevelScheduleConfig : null,
                    displayedLevelIndex);
            }

            int levelIndex = activeSession.GetLevelIndex(displayedLevelIndex);
            LoadLevel(levelIndex);
        }

        private void CacheBlockVisualData()
        {
            if (!blocksVisuals)
            {
                blocksVisuals = LoadBLockVisualData();
                if (blocksVisuals)
                {
                    BlockFigureGeometryCache.Rebuild(blocksVisuals);
                }
            } 
        }
        private BlocksVisualsData LoadBLockVisualData()
        {
            if (Application.isPlaying)
            {
                blockTheme = Services.GameplayConfig.GetBlockTheme();
            }
            else
            {
                blockTheme = BlockTheme.New;
            }
            return blockTheme switch
            {
                BlockTheme.New => BlocksVisualsNew,
                BlockTheme.Simple => BlocksVisualsSimple,
                _ => BlocksVisualsNew
            };
        }

        public void OnGameEnd(bool isWin)
        {
            RaycastController.Disable("GameEnd");
            OnObjectReleased();

            // On win, mark the session ended but keep the data readable for end-of-level consumers
            // (win popup, analytics) — it is fully wiped by the next LoadLevel/CommitTransition/Begin.
            // On a loss we leave the session active: a revive may resume play and must keep recording.
            if (isWin)
            {
                LevelRuntimeData.Current.End();
            }

            if (GameplayTimer != null && GameplayTimer.IsActive)
                GameplayTimer.Pause();

            if (levelRepresentation != null)
            {
                List<LevelBlockBehavior> blocks = levelRepresentation.ActiveBlocks;
                foreach (LevelBlockBehavior block in blocks)
                {
                    if (block.HasActiveEffect())
                    {
                        List<BlockEffectBehavior> effects = block.Effects;
                        foreach (BlockEffectBehavior effect in effects)
                            effect.OnGameEnded();
                    }
                }
            }

            if (isWin)
                OnLevelCompleted();
            else
                OnLevelFailed();
        }

        void OnLevelCompleted()
        {
            if (Services.SpecialLevelService.IsActive)
            {
                return;
            }

            ActiveSession activeSession = ActiveSession.Current;
            activeSession.OnLevelCompleted();
        }

        void OnLevelFailed() { }

        public void OnRevived(int seconds, LoseReason loseReason = LoseReason.OutOfTime)
        {
            if (loseReason == LoseReason.OutOfTime)
                GameplayTimer.AdjustTime(seconds);

            levelRepresentation?.ResetCompletionLatch();

            RaycastController.Enable("GameEnd");
            GameplayTimer.Resume();

            List<LevelBlockBehavior> blocks = LevelRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in blocks)
            {
                if (!block) continue;
                List<BlockEffectBehavior> effects = block.Effects;
                foreach (BlockEffectBehavior effect in effects)
                {
                    if (!effect || !effect.IsActive) continue;
                    effect.OnRevived(loseReason, seconds);
                }
            }

            var gates = levelRepresentation.EnvironmentSpawner.Gates;
            foreach (var gate in gates)
            {
                var effects = gate.Effects;
                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    var effect = effects[i];
                    if (!effect || !effect.IsActive) continue;
                    effect.OnRevived(loseReason, seconds);
                }
            }
        }

        public void AddTime(float seconds)
        {
            GameplayTimer.AdjustTime(seconds);
        }

        private void FixedUpdate()
        {
            if (movementManager == null) return;
            MovementUpdate();
        }

        public void LoadLevel(int levelIndex)
        {
            if (IsLevelLoaded)
                UnloadLevel();

            LevelStarted = false;
            currentLevel = levelIndex + 1;

            LevelData levelData = levelLoader.LoadLevel(levelIndex);
#if DEBUG
            LogLevelLoadDebug(levelIndex);
#endif
            LoadCamera();

            levelRepresentation = new LevelRepresentation(levelData, environmentData, cameraController, this);
            CellWatchService = new BlockCellWatchService(levelRepresentation);
            CellWatchNotifier = CellWatchService as IBlockCellEventNotifier;
            levelRepresentation.EnvironmentSpawner?.SetWatchService(CellWatchService);
            runtimePrecomputedCache = new LevelRuntimePrecomputedCache();
            runtimePrecomputedCache.Build(levelRepresentation.LevelElements);
            SubscribeRepresentationEvents(levelRepresentation);
            LevelRepresentation.Spawn();

            SetupWinCondition();
            SetupGoldBlockCoins();

            movementManager.SetLevelRepresentation(LevelRepresentation);

            InitTimer();
            ActiveSession.Current.OnLevelStarted(levelData);
            LevelRuntimeData.Current.Clear();

            levelLoaded = true;
            LevelLoaded?.Invoke();
            RaiseLevelLoaded();
        }

        private void OnDestroy()
        {
            // Leaving the Game scene (e.g. back to Home) — the play session is now out of date.
            LevelRuntimeData.Current.Clear();

            UnregisterBlockMovementConfigEvents();
            EventBus<GamePausedEvent>.Deregister(pauseEvent);
            UnsubscribeRepresentationEvents(levelRepresentation);
            UnsubscribeRepresentationEvents(stagingRepresentation);
            UnloadLevel();
        }

        private void RegisterBlockMovementConfigEvents()
        {
            if (!blockConfig || isBlockMovementConfigSubscribed) return;
            blockConfig.Changed += OnBlockConfigChanged;
            isBlockMovementConfigSubscribed = true;
        }

        private void UnregisterBlockMovementConfigEvents()
        {
            if (!blockConfig || !isBlockMovementConfigSubscribed) return;
            blockConfig.Changed -= OnBlockConfigChanged;
            isBlockMovementConfigSubscribed = false;
        }

        private void OnBlockConfigChanged() => movementManager?.SetConfig(blockConfig);

        public void OnGameActivated()
        {
            GameplayTimer.Start();
            LevelStarted = true;

            // Start a fresh play session (Begin internally clears previous data).
            LevelRuntimeData.Current.Begin(currentLevel, LevelRepresentation?.LevelData);

            SpecialLevelService special = Services.SpecialLevelService;
            bool isSpecial = special.IsActive && special.CurrentLevel != null;
            GameMode gameMode = isSpecial ? special.CurrentLevel.Mode.ToGameMode() : GameMode.Classic;
            // Special modes are numbered within their own sequence; classic uses the main display level.
            int trackedLevel = isSpecial ? special.CurrentOrderIndex + 1 : ActiveSession.Current.DisplayLevelIndex + 1;

            int analyticsLevel = trackedLevel;

            int realLevel = ActiveSession.ResolveRealLevelNumber(trackedLevel);

            EventBus<LevelStartedEvent>.Raise(new LevelStartedEvent()
            {
                gameMode = gameMode,
                level = trackedLevel,
                realLevel = realLevel,
                phase = 0,
            });

            EventBus<LevelStartTurnEvent>.Raise(new LevelStartTurnEvent()
            {
                gameMode = gameMode,
                level = analyticsLevel,
                realLevel = realLevel,
                phase = 0,
            });

            pauseEvent = new EventBinding<GamePausedEvent>(OnGamePausedEvent);
            List<LevelBlockBehavior> blocks = LevelRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in blocks)
            {
                if (block.HasActiveEffect())
                {
                    List<BlockEffectBehavior> effects = block.Effects;
                    foreach (BlockEffectBehavior effect in effects)
                        effect.OnLevelActivated();
                }
            }
        }

        private void OnGamePausedEvent(GamePausedEvent pausedEvent)
        {
            if (pausedEvent.paused)
                GameplayTimer.Pause(pausedEvent.reason);
            else
                GameplayTimer.Resume(pausedEvent.reason);

            PowerUpController.OnGamePausedEvent(pausedEvent.paused);
        }

        private void LoadCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera)
            {
                cameraController = mainCamera.GetComponent<CameraController>();
                if (!cameraController)
                    Debug.LogError("CameraController component not found on Main Camera!");
            }
        }

        /// <summary>
        /// Editor-only sandbox flag: true while a Special "Test" level is being played from the Level Editor.
        /// Power-ups query this to force-unlock and skip consumption. Always false in player builds.
        /// </summary>
        public static bool IsTestModeActive
        {
#if UNITY_EDITOR
            get => LevelDatabase.IsEditorSpecialTestPlay;
#else
            get => false;
#endif
        }

        private static GameMode ResolveActiveGameMode()
        {
#if UNITY_EDITOR
            if (LevelDatabase.IsEditorSpecialTestPlay)
                return GameMode.Test;
#endif
            SpecialLevelService special = Services.SpecialLevelService;
            if (special is { IsActive: true } && special.CurrentLevel)
                return special.CurrentLevel.Mode.ToGameMode();
            return GameMode.Classic;
        }

        private void SetupWinCondition()
        {
            if (levelRepresentation == null)
                return;

            activeGameMode = ResolveActiveGameMode();
            winCondition = winConditionFactory.Create(activeGameMode);
            levelRepresentation.SetWinCondition(winCondition);
        }

        private void SetupGoldBlockCoins()
        {
            goldBlockCoinManager?.Dispose();
            goldBlockCoinManager = null;

            if (activeGameMode != GameMode.GoldMode)
                return;

            SpecialLevelService special = Services.SpecialLevelService;
            if (special?.GoldCoinInBlockPrefab == null)
                return;

            goldBlockCoinManager = new GoldBlockCoinManager(special.GoldCoinInBlockPrefab, levelRepresentation);
            goldBlockCoinManager.SpawnCoinsForGoldBlocks();
        }

        public void UnloadLevel()
        {
            if (!IsLevelLoaded) return;

            // Cancel any staging representation that's still pending
            CancelStaging();

            if (movementManager != null)
            {
                movementManager.ReleaseObject();
                movementManager.SetLevelRepresentation(null);
            }

            GameplayTimer?.Reset();

            if (levelRepresentation != null && !levelRepresentation.SpawnComplete)
            {
                RaycastController.Enable("mapSpawn");
                GameController.Instance.UnblockUI("mapSpawn");
            }

            goldBlockCoinManager?.Dispose();
            goldBlockCoinManager = null;
            winCondition = null;
            activeGameMode = GameMode.Classic;

            UnsubscribeRepresentationEvents(levelRepresentation);
            levelRepresentation?.Cleanup();
            levelRepresentation = null;

            CellWatchNotifier?.Clear();

            levelLoaded = false;
            // NOTE: Do NOT reset LevelLoaded event here. Consumers subscribe for the lifetime
            // of the scene and should unsubscribe themselves if needed.
        }

        #region Representation Events

        private void SubscribeRepresentationEvents(LevelRepresentation rep)
        {
            if (rep == null) return;
            rep.LevelCompleted += OnRepresentationLevelCompleted;
            rep.LevelFailed += OnRepresentationLevelFailed;
            rep.SpawnStarted += OnRepresentationSpawnStarted;
            rep.SpawnCompleted += OnRepresentationSpawnCompleted;
        }

        private void UnsubscribeRepresentationEvents(LevelRepresentation rep)
        {
            if (rep == null) return;
            rep.LevelCompleted -= OnRepresentationLevelCompleted;
            rep.LevelFailed -= OnRepresentationLevelFailed;
            rep.SpawnStarted -= OnRepresentationSpawnStarted;
            rep.SpawnCompleted -= OnRepresentationSpawnCompleted;
        }

        private void OnRepresentationLevelCompleted(LevelRepresentation rep)
        {
            if (rep != levelRepresentation) return;
            _ = GameController.Instance.OnCompleteLevel();
        }

        private void OnRepresentationLevelFailed(LevelRepresentation rep, LoseReason reason, float delay)
        {
            if (rep != levelRepresentation) return;
            GameController.Instance.GameOver(reason, delay);
        }

        private void OnRepresentationSpawnStarted(LevelRepresentation rep)
        {
            if (rep != levelRepresentation) return;
            GameController.Instance.OnLevelSpawnStart();
        }

        private void OnRepresentationSpawnCompleted(LevelRepresentation rep)
        {
            if (rep != levelRepresentation) return;
            rep.SetBlocksInterpolation(RigidbodyInterpolation.Interpolate);
            GameController.Instance.OnLevelSpawnCompleted();

            if (activeGameMode == GameMode.GoldMode)
                goldBlockCoinManager?.TryShowIntroMessage();
        }

        #endregion

        #region Level Transition

        public void PrepareNextLevel(int levelIndex, Vector3 worldOffset)
        {
            stagingRepresentation?.Cleanup();
            stagingLevelIndex = levelIndex;
            LevelStarted = false;

            LevelData levelData = levelLoader.LoadLevel(levelIndex);
            stagingRepresentation = new LevelRepresentation(
                levelData, environmentData, cameraController, this, "[LEVEL_STAGING]");
            runtimePrecomputedCache = new LevelRuntimePrecomputedCache();
            runtimePrecomputedCache.Build(stagingRepresentation.LevelElements);
            SubscribeRepresentationEvents(stagingRepresentation);
            stagingRepresentation.Spawn(false);
            stagingRepresentation.SetWorldOffset(worldOffset);
        }

        public void CommitTransition()
        {
            if (stagingRepresentation == null) return;

            var oldRepresentation = levelRepresentation;

            levelRepresentation = stagingRepresentation;
            stagingRepresentation = null;

            levelRepresentation.SetWorldOffset(Vector3.zero);
            movementManager.SetLevelRepresentation(levelRepresentation);

            currentLevel = stagingLevelIndex + 1;
            LevelStarted = false;

            InitTimer();
            ActiveSession.Current.OnLevelStarted(levelRepresentation.LevelData);

            // New level staged in via swipe transition — clear stale play data before it starts.
            LevelRuntimeData.Current.Clear();

            SetupWinCondition();
            SetupGoldBlockCoins();

            UnsubscribeRepresentationEvents(oldRepresentation);
            oldRepresentation?.Cleanup();

            levelLoaded = true;
            LevelLoaded?.Invoke();
            RaiseLevelLoaded();
        }

        public void CancelStaging()
        {
            if (stagingRepresentation == null) return;
            UnsubscribeRepresentationEvents(stagingRepresentation);
            stagingRepresentation.Cleanup();
            stagingRepresentation = null;
        }

        public async UniTask SwipeToNextLevelAsync(int levelIndex, float duration = SWIPE_TRANSITION_DURATION)
        {
            Bounds currentBounds = levelRepresentation.LevelBounds;
            float offsetX = currentBounds.size.x + SWIPE_LEVEL_GAP;
            Vector3 worldOffset = new Vector3(offsetX, 0, 0);

            PrepareNextLevel(levelIndex, worldOffset);

            await UniTask.Delay(TimeSpan.FromSeconds(duration));

            if (stagingRepresentation == null) return;

            levelRepresentation?.SetBlocksInterpolation(RigidbodyInterpolation.None);
            stagingRepresentation.SetBlocksInterpolation(RigidbodyInterpolation.None);

            Transform oldTransform = levelRepresentation?.LevelTransform;
            Transform newTransform = stagingRepresentation.LevelTransform;

            var completionSource = new UniTaskCompletionSource();
            Sequence slideSequence = DOTween.Sequence();

            if (oldTransform)
                slideSequence.Join(oldTransform.DOMove(-worldOffset, duration).SetEase(DG.Tweening.Ease.InOutCubic));
            if (newTransform)
                slideSequence.Join(newTransform.DOMove(Vector3.zero, duration).SetEase(DG.Tweening.Ease.InOutCubic));

            slideSequence.OnComplete(() => completionSource.TrySetResult());
            slideSequence.OnKill(() => completionSource.TrySetResult());

            _ = RepositionCameraDelayed(stagingRepresentation,
                Mathf.Max(0, duration - cameraController.RepositionDuration));

            await completionSource.Task;

            CommitTransition();
            levelRepresentation?.SetBlocksInterpolation(RigidbodyInterpolation.Interpolate);
        }

        private async UniTask RepositionCameraDelayed(LevelRepresentation rep, float delay)
        {
            if (rep == null) return;
            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            await rep.RepositionCamera();
        }

        #endregion

        #region Movement

        private void MovementUpdate()
        {
            movementManager.FixedUpdate();

            if (!movementManager.IsBlockPicked)
            {
                lastGateCheckGridPos = null;
                return;
            }

            LevelBlockBehavior levelBlockBehavior = movementManager.BlockBehavior;
            if (!levelBlockBehavior) return;

            // Gate proximity only changes when the held block crosses into a new grid cell. While it
            // stays in the same cell (held still or moving sub-cell) the result is unchanged, so we
            // scan on cell change instead of on a fixed timer — at most one scan per cell crossing.
            // Linked blocks move rigidly with the primary, so the primary's cell is a sufficient key.
            Vector2Int currentGridPos = levelBlockBehavior.MatrixPosition;
            if (lastGateCheckGridPos == currentGridPos) return;
            lastGateCheckGridPos = currentGridPos;

            bool IsNearGate(LevelBlockBehavior blockBehavior)
            {
                var nearGate = LevelRepresentation.EnvironmentSpawner.NearGate(blockBehavior);
                if (!nearGate.HasValue) return false;
                var (direction, gateBehavior) = nearGate.Value;
                if (direction == GateDirection.Type.None || !gateBehavior) return false;
                // NearGate already verified gate.CanGoThroughGate == Enterable for this gate; only the
                // block's own effect gate remains, so skip re-running the full OnGateEntered (which
                // would repeat CanGoThroughGate).
                return blockBehavior.AllowGateEntered() == BlockGateState.Enterable;
            }

            // Gate auto-collect: snap in place at the validated cell so the block fills from the gate
            // it actually reached, instead of chasing the finger off the gate on a fast drag.
            if (IsNearGate(levelBlockBehavior))
            {
                OnObjectReleased(snapToCurrentPosition: true);
            }
            else if (levelBlockBehavior.MoveMultiplyObjects())
            {
                var linkedBlocks = levelBlockBehavior.GetLinkedBlocks();
                if (linkedBlocks.IsNullOrEmpty()) return;
                foreach (var linkedBlock in linkedBlocks)
                {
                    if (!IsNearGate(linkedBlock)) continue;
                    OnObjectReleased(snapToCurrentPosition: true);
                    break;
                }
            }
        }

        public void OnObjectPicked(LevelBlockBehavior levelBlockBehavior)
        {
            if (levelBlockBehavior.IsFullFill) return;

            pickedBlockGridPos = levelBlockBehavior.MatrixPosition;
            movementManager.PickObject(levelBlockBehavior);

            if (movementManager.IsBlockPicked)
                NotifyBlockPickedIncludingLinked(levelBlockBehavior);
            Services.AudioService.PlaySound(AudioId.Block_Pick);
            //HapticPatterns.PlayPreset(HapticPatterns.PresetType.SoftImpact);
            global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.Block_Select);
        }

        public void TryToCollectBlock(LevelBlockBehavior levelBlockBehavior)
        {
            if (LevelRepresentation == null || !levelBlockBehavior) return;

            var nearGate = LevelRepresentation.EnvironmentSpawner.NearGate(levelBlockBehavior);
            if (!nearGate.HasValue) return;

            var (direction, gateBehavior) = nearGate.Value;
            if (direction == GateDirection.Type.None || !gateBehavior) return;

            var blockGateState = levelBlockBehavior.OnGateEntered(gateBehavior);
            if (blockGateState != BlockGateState.Enterable) return;

            BlockColor gateColor = gateBehavior.GetActiveColor();
            int availableForColor = levelBlockBehavior.GetAvailablePoint(gateColor);
            int consumePoint = Mathf.Min(gateBehavior.GetActivePoint(), availableForColor);
            if (consumePoint <= 0) return;

            int depth = WaterFlowUtilities.GetBlockDepthAtColliderPosition(gateBehavior, levelBlockBehavior);
            levelBlockBehavior.FillWater(gateColor, consumePoint);
            gateBehavior.RemoveWaterInPipe(consumePoint, gateColor);
            gateBehavior.ShowWaterFlow(gateColor, consumePoint, depth, levelBlockBehavior, null);
            levelBlockBehavior.FillWaterVisual(gateColor, consumePoint);
            OnBlockEntered(levelBlockBehavior, gateBehavior);
        }

        public void ForceCollectBlock(LevelBlockBehavior levelBlockBehavior, BlockColor blockColor = BlockColor.None)
        {
            if (!levelBlockBehavior || levelBlockBehavior.IsFullFill) return;

            BlockColor primaryColor = levelBlockBehavior.GetActiveBlockColor();
            if (blockColor == BlockColor.None || blockColor == primaryColor)
                CollectWaterForBlock(levelBlockBehavior, primaryColor);

            BlockColor secondaryColor = levelBlockBehavior.GetSecondaryBlockColor();
            if (secondaryColor != BlockColor.None && (blockColor == BlockColor.None || blockColor == secondaryColor))
                CollectWaterForBlock(levelBlockBehavior, secondaryColor);
        }

        private void CollectWaterForBlock(LevelBlockBehavior block, BlockColor color)
        {
            int amountNeeded = block.GetAvailablePoint(color);
            if (amountNeeded <= 0) return;

            List<WaterPumpSource> sources = FindWaterSourcesForPump(color, amountNeeded);
            if (sources.Count == 0) return;

            List<WaterPumpSource> mergedSources = MergeSourcesByGate(sources);

            int totalCollected = 0;
            foreach (var source in mergedSources)
                totalCollected += source.Amount;

            if (totalCollected == 0) return;

            block.FillWater(color, totalCollected);
            block.FillWaterVisual(color, totalCollected);

            foreach (var source in mergedSources)
                source.Gate.RemoveWaterInPipe(source.Amount, color);
        }

        private List<WaterPumpSource> MergeSourcesByGate(List<WaterPumpSource> sources)
        {
            var gateToAmount = new Dictionary<GateBehavior, int>();
            foreach (var source in sources)
            {
                if (gateToAmount.ContainsKey(source.Gate))
                    gateToAmount[source.Gate] += source.Amount;
                else
                    gateToAmount[source.Gate] = source.Amount;
            }

            var result = new List<WaterPumpSource>();
            foreach (var kvp in gateToAmount)
                result.Add(new WaterPumpSource(kvp.Key, kvp.Value));

            return result;
        }

        /// <summary>Find gates containing water of the specified color, prioritising lower queue index.</summary>
        private List<WaterPumpSource> FindWaterSourcesForPump(BlockColor color, int amountNeeded)
        {
            var result = new List<WaterPumpSource>();
            var gates = LevelRepresentation.EnvironmentSpawner.Gates;
            var allSources = new List<(GateBehavior gate, int amount, int queueIndex)>();
            var waterInfoBuffer = new List<(int amount, int queueIndex)>();

            foreach (var gate in gates)
            {
                gate.GetWaterInfoForColor(color, waterInfoBuffer);
                foreach (var (amount, queueIndex) in waterInfoBuffer)
                {
                    if (amount > 0)
                        allSources.Add((gate, amount, queueIndex));
                }
            }

            allSources.Sort((a, b) => a.queueIndex.CompareTo(b.queueIndex));

            int collected = 0;
            foreach (var (gate, amount, _) in allSources)
            {
                if (collected >= amountNeeded) break;
                int toTake = Mathf.Min(amount, amountNeeded - collected);
                result.Add(new WaterPumpSource(gate, toTake));
                collected += toTake;
            }

            return result;
        }

        private struct WaterPumpSource
        {
            public GateBehavior Gate;
            public int Amount;

            public WaterPumpSource(GateBehavior gate, int amount)
            {
                Gate = gate;
                Amount = amount;
            }
        }

        public void OnObjectReleased(bool snapToCurrentPosition = false)
        {
            if (movementManager == null) return;
            LevelBlockBehavior pickedBlock = movementManager.BlockBehavior;
            if (!pickedBlock) return;

            bool shouldPlayPutSound = false;
            if (movementManager.IsBlockPicked)
            {
                var snapTargetPosition = movementManager.SnapToClosestPosition(snapToCurrentPosition);

                if (pickedBlockGridPos.HasValue)
                {
                    int manhattan = Mathf.Abs(snapTargetPosition.x - pickedBlockGridPos.Value.x)
                                    + Mathf.Abs(snapTargetPosition.y - pickedBlockGridPos.Value.y);
                    if (manhattan >= 1)
                    {
                        BoosterNavigationController.Instance?.OnBlockMoved();
                        EventBus<BlockMoveEvent>.Raise(new BlockMoveEvent());
                        LevelRuntimeData.Current.RecordMove();
                    }
                    pickedBlockGridPos = null;
                }

                // Cell-watch and effect release fire from OnBlockSnapCompleted after snap finishes.
                shouldPlayPutSound = true;
            }

            movementManager.ReleaseObject();

            if (shouldPlayPutSound)
                Services.AudioService.PlaySound(AudioId.Block_Put);

            global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.Block_Select);
        }

        public void OnBlockDestructed(LevelBlockBehavior destructedBlock)
        {
            // Remove from ActiveBlocks before cell-watch threshold / TrySpawnLift so the
            // destructing block is not counted as still overlapping lift watched cells.
            LevelRepresentation.OnBlockDestructed(destructedBlock);

            CellWatchNotifier?.NotifyBlockDestructed(destructedBlock);
            BoosterNavigationController.Instance?.OnBlockCleared();
            LevelRuntimeData.Current.RecordBlockDestroyed(destructedBlock.BlockId);
            EventBus<BlockDestroyedEvent>.Raise(new BlockDestroyedEvent());

            List<LevelBlockBehavior> activeBlocks = LevelRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in activeBlocks)
                block.OnBlockDestructed(destructedBlock);

            List<GateBehavior> gates = LevelRepresentation.EnvironmentSpawner.Gates;
            foreach (GateBehavior gate in gates)
                gate.OnBlockDestructed(destructedBlock);

            List<InteractableObjectBehavior> interactableObjects =
                LevelRepresentation.EnvironmentSpawner.InteractableObjects;
            for (int i = interactableObjects.Count - 1; i >= 0; i--)
            {
                InteractableObjectBehavior interactable = interactableObjects[i];
                if (interactable) interactable.OnBlockDestructed(destructedBlock);
            }
        }

        private void OnBlockEntered(LevelBlockBehavior collectedBlock, GateBehavior gateBehavior)
        {
            BoosterNavigationController.Instance?.OnBlockCleared();

            List<LevelBlockBehavior> activeBlocks = LevelRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in activeBlocks)
                block.OnBlockEnteredGate(collectedBlock, gateBehavior);

            List<GateBehavior> gates = LevelRepresentation.EnvironmentSpawner.Gates;
            foreach (GateBehavior gate in gates)
                gate.OnBlockEntered(collectedBlock);
        }

        private void NotifyBlockPickedIncludingLinked(LevelBlockBehavior primaryBlock)
        {
            List<InteractableObjectBehavior> interactableObjects =
                LevelRepresentation.EnvironmentSpawner.InteractableObjects;

            ForEachBlockIncludingLinked(primaryBlock, block =>
            {
                NotifyBlockEffectsPicked(block);
                CellWatchNotifier?.NotifyBlockPicked(block);

                foreach (InteractableObjectBehavior interactable in interactableObjects)
                    interactable.OnBlockPicked(block);
            });
        }

        private void NotifyBlockReleasedIncludingLinked(LevelBlockBehavior primaryBlock, Vector2Int snapTargetPosition)
        {
            List<InteractableObjectBehavior> interactableObjects =
                LevelRepresentation.EnvironmentSpawner.InteractableObjects;

            ForEachBlockIncludingLinked(primaryBlock, block =>
            {
                Vector2Int releasedGrid = block.MatrixPosition;
                NotifyBlockEffectsReleased(block, releasedGrid);
                CellWatchNotifier?.NotifyBlockReleased(block, releasedGrid);

                foreach (InteractableObjectBehavior interactable in interactableObjects)
                    interactable.OnBlockReleased(block, releasedGrid);
            });
        }

        private void OnBlockSnapCompleted(LevelBlockBehavior block, Vector2Int snapGridPosition)
        {
            NotifyBlockReleasedIncludingLinked(block, snapGridPosition);
        }

        private static void NotifyBlockEffectsPicked(LevelBlockBehavior block)
        {
            if (!block) return;

            foreach (BlockEffectBehavior effect in block.Effects)
            {
                if (!effect.IsActive) continue;
                effect.OnBlockPicked(block);
            }
        }

        private static void NotifyBlockEffectsReleased(LevelBlockBehavior block, Vector2Int snapTargetPosition)
        {
            if (!block) return;

            foreach (BlockEffectBehavior effect in block.Effects)
            {
                if (!effect.IsActive) continue;
                effect.OnBlockReleased(block, snapTargetPosition);
            }
        }

        private void ForEachBlockIncludingLinked(LevelBlockBehavior primaryBlock, Action<LevelBlockBehavior> action)
        {
            if (!primaryBlock) return;

            action(primaryBlock);

            if (movementManager?.LinkedObjects == null) return;

            foreach (BlockMovementManager.LinkedObjectData linkedObject in movementManager.LinkedObjects)
            {
                if (!linkedObject.Block) continue;
                action(linkedObject.Block);
            }
        }

        #endregion

        #region Timer

        private void InitTimer()
        {
            LevelData levelData = LevelRepresentation.LevelData;
            bool useRandomDuration = !Services.SpecialLevelService.IsActive &&
                                     ActiveSession.Current.IsPlayingRandomLevel;
            float time = useRandomDuration ? levelData.RandomDuration : levelData.Duration;

            GameplayTimer.SetMaxTime(time);
        }

        private void Update()
        {
            GameplayTimer?.Update();

            // Accumulate active play time (timer is paused while the game is paused).
            if (GameplayTimer != null && GameplayTimer.IsActive)
                LevelRuntimeData.Current.Tick(Time.deltaTime);
        }

        private void OnGameplayTimerFinished()
        {
            if (levelRepresentation.IsLevelCompleted()) return;

            if (winCondition != null && levelRepresentation != null)
            {
                WinEvalResult result = winCondition.OnTimeExpired(levelRepresentation);
                if (result == WinEvalResult.Win)
                {
                    _ = GameController.Instance.OnCompleteLevel();
                    return;
                }
            }

            GameController.Instance.GameOver(LoseReason.OutOfTime);
        }

        private void OnGameplayTimerChangeActiveState(bool isActive)
        {
            List<LevelBlockBehavior> blocks = LevelRepresentation.ActiveBlocks;
            foreach (LevelBlockBehavior block in blocks)
            {
                List<BlockEffectBehavior> effects = block.Effects;
                foreach (BlockEffectBehavior effect in effects)
                    effect.OnGameStateChanged(isActive);
            }

            if (isActive)
                RaycastController.Enable("TimerStateChange", false);
        }

        #endregion

        public LevelBlockEffectData GetEffectData(BlockEffectType effectType)
            => levelDatabase.GetEffectData(effectType);

        public LevelGateEffectData GetGateEffectData(GateEffectType effectType)
            => levelDatabase.GetGateEffectData(effectType);

        public LevelInteractableObjectData GetInteractableObject(InteractableObjectType objectType)
            => levelDatabase.GetInteractableObjectData(objectType);

        public BlocksVisualsData GetBlocksVisualsData()
        {
            CacheBlockVisualData();
            return blocksVisuals;
        }

        public BlockColorData GetBlockColorData(BlockColor blockColor)
            => GetBlocksVisualsData().GetColorData(blockColor);

        public BlockData GetBlockData(BlockType blockType)
            => GetBlocksVisualsData().GetBlockData(blockType);

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            LevelRepresentation?.DrawGizmos();
        }

        /// <summary>
        /// Invoke <paramref name="loadCallback"/> immediately if the level is already loaded,
        /// otherwise subscribe it as a one-shot listener on <see cref="LevelLoaded"/>.
        /// </summary>
        public void InvokeOrWait(SimpleCallback loadCallback)
        {
            if (IsLevelLoaded)
            {
                loadCallback?.Invoke();
            }
            else
            {
                // One-shot subscription: auto-remove after firing.
                SimpleCallback wrapper = null;
                wrapper = () =>
                {
                    LevelLoaded -= wrapper;
                    loadCallback?.Invoke();
                };
                LevelLoaded += wrapper;
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private void RaiseLevelLoaded()
        {
            EventBus<LevelLoadedEvent>.Raise(new LevelLoadedEvent { level = currentLevel });
        }

#if DEBUG
        private void LogLevelLoadDebug(int realLevelIndex)
        {
            int displayLevelIndex = ActiveSession.Current.DisplayLevelIndex;
            int loadedLevelNumber = realLevelIndex + 1;
            string source = levelLoader != null ? levelLoader.LastLoadSource : "UNKNOWN";

            Debug.Log(
                $"[LevelLoadDebug] source={source} | displayLevelIndex={displayLevelIndex} | " +
                $"realLevelIndex={realLevelIndex} | loadedLevelNumber={loadedLevelNumber}");
        }
#endif
    }
}
