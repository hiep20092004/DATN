using System.Collections;
using System.Collections.Generic;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Tracks gameplay state to surface booster hints at the right moment.
    ///
    /// Triggers:
    ///   1. Time milestones – timer at 30 / 20 / 10 seconds → always target Freeze (ignores per-booster cooldown)
    ///   2. No clear  – no block cleared for 30s
    ///   3. Stale map – map unchanged for 15s (no block moved, no block cleared)
    ///
    /// Per-booster 20s cooldown from last navigation highlight for that booster.
    /// Each highlight auto-clears after 3 seconds.
    ///
    /// Priority for triggers 1 & 2:
    ///   Pump → Hammer → Expand → Freeze
    /// </summary>
    public class BoosterNavigationController : MonoBehaviour
    {
        private const float StaleMapThreshold = 15f;
        private const float NoClearThreshold = 30f;
        private const float NavCooldownSeconds = 20f;
        private const float NavHighlightDurationSeconds = 3f;

        private static readonly PowerUpType[] PriorityChain =
        {
            PowerUpType.Pump,
            PowerUpType.Hammer,
            PowerUpType.Expand,
            PowerUpType.Freeze
        };

        private float staleMapTimer;
        private float noClearTimer;
        private readonly Dictionary<PowerUpType, float> lastNavTriggerByType = new Dictionary<PowerUpType, float>();
        private int lastCheckedTimeSecs = int.MaxValue;

        private Coroutine navHighlightClearRoutine;

        private EventBinding<LevelEndedEvent> levelEndedBinding;

        public static BoosterNavigationController Instance { get; private set; }

        // ------------------------------------------------------------------ lifecycle

        public void Init()
        {
            Instance = this;

            levelEndedBinding = new EventBinding<LevelEndedEvent>(OnLevelEnded);

            LevelController.Instance.LevelLoaded += OnLevelLoaded;

            if (LevelController.Instance.IsLevelLoaded)
                OnLevelLoaded();

            PowerUpController.Used += OnBoosterUsed;
        }

        private void OnDestroy()
        {
            EventBus<LevelEndedEvent>.Deregister(levelEndedBinding);

            if (LevelController.Instance)
                LevelController.Instance.LevelLoaded -= OnLevelLoaded;

            PowerUpController.Used -= OnBoosterUsed;

            if (Instance == this)
                Instance = null;
        }

        private void OnLevelLoaded()
        {
            staleMapTimer = 0f;
            noClearTimer = 0f;
            lastNavTriggerByType.Clear();
            lastCheckedTimeSecs = int.MaxValue;

            ClearHighlights();
        }

        private void OnLevelEnded(LevelEndedEvent _)
        {
            ClearHighlights();
        }

        private void OnBoosterUsed(PowerUpType _)
        {
            ClearHighlights();
        }

        // ------------------------------------------------------------------ LevelController hooks

        /// <summary>Called when player moves a block at least one grid cell.</summary>
        public void OnBlockMoved()
        {
            staleMapTimer = 0f;
        }

        /// <summary>Called when a block is cleared (enters gate or is destructed).</summary>
        public void OnBlockCleared()
        {
            staleMapTimer = 0f;
            noClearTimer = 0f;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (!TryGetRunningGameplayTimer(out GameplayTimer timer))
                return;

            float dt = Time.deltaTime;
            staleMapTimer += dt;
            noClearTimer += dt;

            CheckTimeMilestones(timer);
            
            if (ConsumeThreshold(ref noClearTimer, NoClearThreshold))
                TryTriggerPriorityNavigation();
            
            if (ConsumeThreshold(ref staleMapTimer, StaleMapThreshold))
                TryTriggerPriorityNavigation();

        }

        private static bool TryGetRunningGameplayTimer(out GameplayTimer timer)
        {
            timer = null;

            LevelController lc = LevelController.Instance;
            if (!lc || !lc.LevelStarted)
                return false;

            timer = lc.GameplayTimer;
            return timer is { IsActive: true };
        }

        private static bool ConsumeThreshold(ref float accumulator, float threshold)
        {
            if (accumulator < threshold)
                return false;

            accumulator = 0f;
            return true;
        }

        // ------------------------------------------------------------------ milestones

        private void CheckTimeMilestones(GameplayTimer timer)
        {
            int secs = Mathf.FloorToInt(timer.CurrentTime);
            if (secs == lastCheckedTimeSecs)
                return;

            lastCheckedTimeSecs = secs;

            if (IsCountdownMilestone(secs))
            {
                RecordNavTrigger(PowerUpType.Freeze);
                ShowTimedHighlight(PowerUpType.Freeze);
            }
        }

        private static bool IsCountdownMilestone(int wholeSecondsRemaining)
        {
            return wholeSecondsRemaining is 30 or 20 or 10;
        }

        // ------------------------------------------------------------------ priority navigation

        private void TryTriggerPriorityNavigation()
        {
            if (!TryGetPriorityTarget(out PowerUpType target))
                return;

            RecordNavTrigger(target);
            ShowTimedHighlight(target);
        }

        private bool TryGetPriorityTarget(out PowerUpType target)
        {
            foreach (PowerUpType type in PriorityChain)
            {
                if (CanSuggestBooster(type))
                {
                    target = type;
                    return true;
                }
            }

            target = PowerUpType.None;
            return false;
        }

        private bool CanSuggestBooster(PowerUpType type)
        {
            if (PowerUpController.GetPowerUpAmount(type) <= 0)
                return false;

            PowerUpBehavior behavior = PowerUpController.GetPowerUpBehavior(type);
            if (!behavior || !behavior.IsHasAnyTarget())
                return false;

            return IsOffCooldown(type);
        }

        private bool IsOffCooldown(PowerUpType type) =>
            !lastNavTriggerByType.TryGetValue(type, out float t) || Time.time - t >= NavCooldownSeconds;

        private void RecordNavTrigger(PowerUpType type)
        {
            lastNavTriggerByType[type] = Time.time;
        }

        // ------------------------------------------------------------------ highlight UI

        private void ShowTimedHighlight(PowerUpType type)
        {
            StopNavHighlightClearRoutine();
            PowerUpController.PowerUpPopup?.SetNavigationHighlight(type);
            navHighlightClearRoutine = StartCoroutine(ClearHighlightAfterDelayCoroutine());
        }

        private IEnumerator ClearHighlightAfterDelayCoroutine()
        {
            yield return new WaitForSeconds(NavHighlightDurationSeconds);
            navHighlightClearRoutine = null;
            PowerUpController.PowerUpPopup?.ClearNavigationHighlights();
        }

        private void ClearHighlights()
        {
            StopNavHighlightClearRoutine();
            PowerUpController.PowerUpPopup?.ClearNavigationHighlights();
        }

        private void StopNavHighlightClearRoutine()
        {
            if (navHighlightClearRoutine == null)
                return;

            StopCoroutine(navHighlightClearRoutine);
            navHighlightClearRoutine = null;
        }
    }
}
