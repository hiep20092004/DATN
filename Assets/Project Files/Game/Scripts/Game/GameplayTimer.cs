using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class GameplayTimer
    {
        public float MaxTime { get; private set; }
        public float CurrentTime { get; private set; }
        public TimeSpan CurrentTimeSpan { get; private set; }

        public bool IsActive { get; private set; }

        public event SimpleCallback OnTimerFinished;
        public event SimpleBoolCallback OnActiveStateChanged;

        public delegate void TimeSpanCallback(TimeSpan timespan);
        public event TimeSpanCallback OnTimeSpanChanged;

        private readonly Dictionary<string, int> pauseReasons = new Dictionary<string, int>(StringComparer.Ordinal);

        private TweenCase adjustTweenCase;

        public void Start()
        {
            IsActive = true;
            // Keep pre-start bonuses from AdjustTime; only seed from MaxTime when not initialized yet.
            if (CurrentTime <= 0f)
                CurrentTime = MaxTime;
            CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime);
            OnTimeSpanChanged?.Invoke(CurrentTimeSpan);
        }

        public void Update()
        {
            if (!IsActive) return;

            CurrentTime -= Time.deltaTime;

            if (CurrentTime <= 0)
            {
                IsActive = false;

                CurrentTime = 0;
                CurrentTimeSpan = TimeSpan.Zero;

                OnTimerFinished?.Invoke();
            }
            else
            {
                int prevSeconds = CurrentTimeSpan.Seconds;

                CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime);
                if (CurrentTimeSpan.Seconds != prevSeconds)
                {
                    OnTimeSpanChanged?.Invoke(CurrentTimeSpan);
                }
            }
        }

        public void Pause(string reason = "default")
        {
            if (pauseReasons.TryGetValue(reason, out var count))
            {
                pauseReasons[reason] = count + 1;
            }
            else
            {
                pauseReasons[reason] = 1;
            }

            UpdateActiveState();
        }

        public void Resume(string reason = "default")
        {
            if (pauseReasons.TryGetValue(reason, out var count))
            {
                if (count <= 1)
                {
                    pauseReasons.Remove(reason);
                }
                else
                {
                    pauseReasons[reason] = count - 1;
                }

                UpdateActiveState();
            }
        }

        private void UpdateActiveState(bool forceActive = false)
        {
            bool shouldBeActive = forceActive || pauseReasons.Count == 0;

            if (IsActive == shouldBeActive) return;

            IsActive = shouldBeActive;

            if (IsActive)
            {
                adjustTweenCase.CompleteActive();

                OnActiveStateChanged?.Invoke(true);
            }
            else
            {
                OnActiveStateChanged?.Invoke(false);
            }
        }

        public void AdjustTime(float time)
        {
            CurrentTime += time;
            CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime);
            OnTimeSpanChanged?.Invoke(CurrentTimeSpan);
        }

        public void CheatTime(float time)
        {
            CurrentTime = time;
            CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime);
            OnTimeSpanChanged?.Invoke(CurrentTimeSpan);
        }

        public void SetMaxTime(float maxTime)
        {
            MaxTime = maxTime;
            CurrentTime = MaxTime;
            CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime);
        }

        public void Reset()
        {
            IsActive = false;
            CurrentTime = MaxTime;
            CurrentTimeSpan = TimeSpan.FromSeconds(CurrentTime);
        }
    }
}