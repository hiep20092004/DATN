using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Systems;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "SpecialLevelService", menuName = "WaterFlow Services/Special Level Service")]
    public sealed class SpecialLevelService : ServiceSo, IServiceInitialize
    {
        // Each filled gold-block cell grants this many coins (1 cell × multiplier) as soon as the block is cleared.
        public const int GoldBlockRewardMultiplier = 2;

        [SerializeField] GameObject goldCoinInBlockPrefab;

        private int returnMainDisplayLevelIndex = -1;
        private SpecialLevelMode pendingMode;
        private int pendingOrderIndex;
        private bool isFinalized;

        public bool IsActive { get; private set; }
        public bool HasPendingSpecialLevel { get; private set; }
        public SpecialLevelData CurrentLevel { get; private set; }
        public ISpecialLevelRuleSet RuleSet { get; private set; }
        public GameObject GoldCoinInBlockPrefab => goldCoinInBlockPrefab;
        public SpecialLevelMode PendingMode => pendingMode;

        /// <summary>0-based order index of the active/pending special level within its mode (file order). Used as the per-mode level number for tracking.</summary>
        public int CurrentOrderIndex => pendingOrderIndex;

        public void Initialize()
        {
            RuleSet = new DefaultSpecialLevelRuleSet();

            IsActive = false;
            HasPendingSpecialLevel = false;
            CurrentLevel = null;
            pendingMode = default;
            pendingOrderIndex = 0;
            returnMainDisplayLevelIndex = -1;
            isFinalized = false;
        }

        /// <summary>
        /// Single source of truth for "which special mode is the player currently in, or about to play".
        /// An already-begun level (<see cref="IsActive"/>) takes precedence over a not-yet-consumed
        /// pending one. Both <see cref="UIGame"/> (in the Game scene, level already active) and the Home
        /// screen (level still pending) query through here so their decisions can never drift apart.
        /// </summary>
        public bool TryGetActiveOrPendingMode(out SpecialLevelMode mode)
        {
            if (IsActive && CurrentLevel != null)
            {
                mode = CurrentLevel.Mode;
                return true;
            }

            if (HasPendingSpecialLevel)
            {
                mode = pendingMode;
                return true;
            }

            mode = default;
            return false;
        }

        public void Schedule(SpecialLevelMode mode, int orderIndex, int nextMainDisplayLevelIndex)
        {
            pendingMode = mode;
            pendingOrderIndex = Mathf.Max(0, orderIndex);
            returnMainDisplayLevelIndex = Mathf.Max(0, nextMainDisplayLevelIndex);
            HasPendingSpecialLevel = true;
        }

        public void ScheduleReplay()
        {
            if (!IsActive || CurrentLevel == null)
                return;

            Schedule(CurrentLevel.Mode, pendingOrderIndex, returnMainDisplayLevelIndex);
        }

        /// <summary>
        /// Re-evaluates the schedule for the level the player is about to play and schedules a
        /// special level if one is mapped to that boundary and has not been entered yet.
        /// Idempotent: returns the existing pending state if a special level is already active or pending.
        /// Used by Home and by the in-game loader so a special level triggers even when the player is
        /// already standing on its unlock boundary (not only right after completing the previous level).
        /// </summary>
        /// <param name="config">Schedule config (from the active LevelDatabase).</param>
        /// <param name="displayLevelIndex">0-based index of the level about to be played.</param>
        /// <returns>True if a special level is pending after this call.</returns>
        public bool TryScheduleForUpcomingLevel(SpecialLevelScheduleConfig config, int displayLevelIndex)
        {
            if (IsActive || HasPendingSpecialLevel)
                return HasPendingSpecialLevel;

            if (config == null)
                return false;

            int upcomingDisplayLevel = displayLevelIndex + 1; 
            if (!config.TryGetForNextDisplayLevel(upcomingDisplayLevel, out SpecialLevelMode mode, out int orderIndex))
                return false;

            if (IsSpecialLevelCompleted(mode, orderIndex))
                return false;

            Schedule(mode, orderIndex, displayLevelIndex);
            return true;
        }

        public bool TryConsumePendingLevel(out SpecialLevelData specialLevelData)
        {
            specialLevelData = null;
            if (!HasPendingSpecialLevel)
                return false;

            specialLevelData = SpecialActiveLevelLoader.Load(pendingMode, pendingOrderIndex);
            if (!specialLevelData)
            {
                HasPendingSpecialLevel = false;
                return false;
            }

            // NOTE: do NOT mark the level completed here (on entry). If the player kills the app
            // mid-play, the in-memory active/pending state is wiped on the next launch but a
            // persisted "completed" flag would remain — so the level would never be re-offered and
            // could not be replayed. The level is recorded as consumed only when the player actually
            // concludes it (win / skip / fail) in ExitAndReturnToMainFlow.
            Begin(specialLevelData);
            HasPendingSpecialLevel = false;
            return true;
        }

        // ─── Completed-level tracking (persisted in LevelSave) ───────────────

        public bool IsSpecialLevelCompleted(SpecialLevelMode mode, int orderIndex)
        {
            LevelSave save = ActiveSession.Current.Save;
            return save.CompletedSpecialLevels != null &&
                   save.CompletedSpecialLevels.Contains(BuildCompletedKey(mode, orderIndex));
        }

        private void MarkSpecialLevelCompleted(SpecialLevelMode mode, int orderIndex)
        {
            LevelSave save = ActiveSession.Current.Save;
            save.CompletedSpecialLevels ??= new System.Collections.Generic.List<string>();

            string key = BuildCompletedKey(mode, orderIndex);
            if (save.CompletedSpecialLevels.Contains(key))
                return;

            save.CompletedSpecialLevels.Add(key);
            SaveController.Save(true);
        }

        private static string BuildCompletedKey(SpecialLevelMode mode, int orderIndex)
            => $"{mode}_{Mathf.Max(0, orderIndex)}";

        // ─── One-time Gold Mode intro message ────────────────────────────────

        /// <summary>True until the player has seen the one-time Gold Mode intro bubble.</summary>
        public bool ShouldShowGoldModeIntro() => !ActiveSession.Current.Save.HasSeenGoldModeIntro;

        /// <summary>Persists that the Gold Mode intro bubble has been shown so it never appears again.</summary>
        public void MarkGoldModeIntroSeen()
        {
            LevelSave save = ActiveSession.Current.Save;
            if (save.HasSeenGoldModeIntro)
                return;

            save.HasSeenGoldModeIntro = true;
            SaveController.Save(true);
        }

        public void Begin(SpecialLevelData data)
        {
            if (!data)
                return;

            CurrentLevel = data;
            isFinalized = false;
            RuleSet = new DefaultSpecialLevelRuleSet();
            IsActive = true;

            // Editor/direct play calls Begin without Schedule; keep a valid return index.
            if (returnMainDisplayLevelIndex < 0)
                returnMainDisplayLevelIndex = ActiveSession.Current.DisplayLevelIndex;
        }

        /// <summary>
        /// Clears an in-progress special session when a main-line level is about to load.
        /// Does not clear <see cref="HasPendingSpecialLevel"/> or <see cref="returnMainDisplayLevelIndex"/>
        /// (win-popup exit still uses those). Idempotent when not active.
        /// </summary>
        public void DeactivateForMainLevelLoad()
        {
            if (!IsActive)
                return;

            LevelRuntimeData.Current.Clear();
            isFinalized = false;
            IsActive = false;
            CurrentLevel = null;
            RuleSet = new DefaultSpecialLevelRuleSet();
        }

        public void End()
        {
            DeactivateForMainLevelLoad();

            HasPendingSpecialLevel = false;

            // Clear the return target so a later direct Begin() (Editor play) can detect the
            // "no schedule" case via the returnMainDisplayLevelIndex < 0 guard instead of reusing
            // the previous special level's stale return index.
            returnMainDisplayLevelIndex = -1;
        }

        /// <summary>
        /// Finalize the special level at "End Level" — i.e. only when the player actually wins and
        /// receives the reward (called once from GameController.OnCompleteLevel): queue the gold
        /// accumulated during the play into pending inventory (same path as classic win coins),
        /// mark the level completed, then persist — all in the same frame.
        ///
        /// This is the ONLY path that completes a special level. Doing it the moment the level is won
        /// (not when the win popup is dismissed) ensures both the pending coins and the "completed" flag
        /// survive an app kill that happens before the popup is reached/dismissed. Every other exit
        /// (Home via settings, Skip, Fail, app kill before the win) never calls this, so the level stays
        /// un-completed, is re-offered (replayable) on the next launch, and grants no reward.
        /// The <see cref="isFinalized"/> guard keeps it a no-op if invoked twice within the same win.
        /// </summary>
        public void FinalizeLevel()
        {
            if (!IsActive || isFinalized)
                return;
            isFinalized = true;

            GameWinFlowService.GrantSpecialLevelGoldPendingReward(GetGoldReward());

            // Mark completed in the same frame as the pending grant; MarkSpecialLevelCompleted persists, so the
            // pending coin entry and the completed flag are flushed to disk together.
            if (CurrentLevel != null)
                MarkSpecialLevelCompleted(CurrentLevel.Mode, pendingOrderIndex);
            else
                SaveController.Save(true);
        }

        /// <summary>
        /// Total Coin reward for the current Gold Mode play (<see cref="LevelRuntimeData.GoldEarned"/>),
        /// already including per-block <see cref="GoldBlockRewardMultiplier"/> applied at collection time.
        /// Used for the win-popup display; pending grant uses the same value in <see cref="FinalizeLevel"/>.
        /// </summary>
        public int GetGoldReward() => LevelRuntimeData.Current.GoldEarned;

        public bool TryGetReturnMainDisplayLevelIndex(out int displayLevelIndex)
        {
            displayLevelIndex = returnMainDisplayLevelIndex;
            return returnMainDisplayLevelIndex >= 0;
        }

        public void Skip() => ExitAndReturnToMainFlow(GamePlacement.Game);

        public void Complete(GamePlacement gamePlacement = GamePlacement.Game) =>
            ExitAndReturnToMainFlow(gamePlacement);

        public void Fail() => ExitAndReturnToMainFlow(GamePlacement.Game);

        private void ExitAndReturnToMainFlow(GamePlacement gamePlacement)
        {
            int mainDisplayLevelIndex = returnMainDisplayLevelIndex;
            if (mainDisplayLevelIndex < 0)
                mainDisplayLevelIndex = ActiveSession.Current.DisplayLevelIndex;

            // Deliberately do NOT finalize here. A special (Gold Mode) level is only completed when the
            // player actually wins and receives the reward — finalization happens once at End Level in
            // GameController.OnCompleteLevel. Every other exit (Home via settings, Skip, Fail, app kill)
            // must leave the level un-completed so it stays replayable and grants no reward.
            End();
            ActiveSession.Current.SetLevelIndex(mainDisplayLevelIndex);
            SaveController.Save(true);

            GameController gameController = GameController.Instance;
            if (gameController != null)
                gameController.Unload(false, () => Services.TransitionService.SwitchScene(gamePlacement));
            else
                Services.TransitionService.SwitchScene(gamePlacement);
        }
    }
}
