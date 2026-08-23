using System;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using TMPro;
using UnityEngine;
using Tween = WaterFlow.Core.Tween;

namespace WaterFlow.Game
{
    public class BombEffectBehavior : BlockEffectBehavior<BombBlockEffectData>
    {
        [SerializeField] GameObject detonatorEffect;
        [SerializeField] Transform bombVisuals;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] float offsetY = 0.5f;
        
        [SerializeField] ParticleSystem disableParticle;

        [Header("Pop Animation Settings")]
        [SerializeField] private float normalPunchScale = 0.2f;
        [SerializeField] private float criticalPunchScale = 0.4f;
        [SerializeField] private float punchDuration = 0.2f;
        [SerializeField] private int punchVibrato = 5;

        [Header("Critical Warning Settings")]
        [SerializeField] private int criticalThreshold = 10;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float blinkDuration = 0.3f;

        private GameplayTimer timer;
        private bool isFirstUpdate = true;
        private bool isCriticalMode = false;
        private Tweener blinkTween;
        private Tweener textBlinkTween;
        private Tweener punchTween;
        
        const string GAME_END_REASON = "game-ended";
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            Bounds bounds = blockBehavior.Figure.GetHorizontalCenterBounds();

            transform.position = blockBehavior.transform.position + bounds.center + new Vector3(0, offsetY * orderID, 0);
            bombVisuals.SetParent(blockBehavior.ModelParentTransform, true);
            
            timer = new GameplayTimer();
            timer.SetMaxTime(Data.bombDuration);
            timer.OnTimeSpanChanged += UpdateTimerText; 
            timer.OnTimerFinished += OnTimerFinished;

            timerText.text = Data.bombDuration.ToString();
            timerText.color = normalColor;
            isCriticalMode = false;
            
            detonatorEffect?.SetActive(false);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (timer.IsActive)
            {
                Services.AudioService.PlaySound(AudioId.Obstacle_Bomb_out);
            }
            timer.Pause();
            timer = null;
            StopCriticalEffects();
            if (bombVisuals)
            {
                Destroy(bombVisuals.gameObject);
            }
        }

        public override void OnBlockFullBeforeAnimationFilled()
        {
            timer?.Pause();
        }

        
        private void OnTimerFinished()
        {
            StopCriticalEffects();
            
            Services.AudioService.PlaySound(AudioId.Obstacle_Bomb_Explosion);
            bombVisuals.gameObject.SetActive(false);
            if (disableParticle)
            {
                disableParticle.gameObject.SetActive(true);
                disableParticle.PlayCase().Disabled += () =>
                {
                    disableParticle.gameObject.SetActive(false);
                };
            }
            GameController.Instance.GameOver(LoseReason.BombExploded, 0.8f);
        }

        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if (timer == null) return;
            bool hiddenByIce = hiddenVisualSources != null && hiddenVisualSources.Contains(ToggleVisualSource.IceEffect);

            if (loseReason == LoseReason.BombExploded)
            {
                ReEnableAfterExplosion();
            }

            // Always remove the "game-ended" pause so the timer can run again when visuals are restored.
            timer.Resume(GAME_END_REASON);

            // If the bomb is currently hidden by Ice, do NOT:
            // - show bomb visuals
            // - add revive time to this bomb
            // Keep it paused by default until Ice restores visuals.
            if (hiddenByIce)
            {
                // Ensure final state remains hidden and not ticking under Ice.
                OnToggleVisual(false, ToggleVisualSource.IceEffect);
                return;
            }

            AddTime(seconds);
        }

        private void AddTime(int seconds)
        {
            if (timer == null) return;
            timer.AdjustTime(seconds);
            if (timer.CurrentTime > criticalThreshold && isCriticalMode)
            {
                StopCriticalEffects();
            }
            UpdateTimerText(timer.CurrentTimeSpan);
        }
        
        /// <summary>
        /// Re-enable the bomb effect after it was disabled due to explosion (for revive)
        /// </summary>
        private void ReEnableAfterExplosion()
        {
            // Re-enable the effect
            isActive = true;
            gameObject.SetActive(true);
            
            // Re-enable bomb visuals
            if (bombVisuals)
            {
                bombVisuals.gameObject.SetActive(true);
            }
            
            // Hide explosion particle if still active
            if (disableParticle)
            {
                disableParticle.Stop();
                disableParticle.gameObject.SetActive(false);
            }
        }

        private void UpdateTimerText(TimeSpan timespan)
        {
            int totalSeconds = timespan.Seconds + timespan.Minutes * 60;
            timerText.text = $"{totalSeconds}";

            PlayPopAnimation(totalSeconds);

            // Check critical mode (10s)
            if (totalSeconds <= criticalThreshold && !isCriticalMode)
            {
                EnterCriticalMode();
            }
        }

        private void PlayPopAnimation(int totalSeconds)
        {
            punchTween?.Kill();
            timerText.transform.localScale = Vector3.one;

            float scale = isCriticalMode ? criticalPunchScale : normalPunchScale;
            int vibrato = isCriticalMode ? punchVibrato + 3 : punchVibrato;

            punchTween = timerText.transform.DOPunchScale(Vector3.one * scale, punchDuration, vibrato)
                .SetEase(DG.Tweening.Ease.OutElastic);
        }

        private void EnterCriticalMode()
        {
            isCriticalMode = true;

            textBlinkTween?.Kill();
            textBlinkTween = timerText.DOColor(criticalColor, blinkDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(DG.Tweening.Ease.InOutSine);

        }

        private void StopCriticalEffects()
        {
            isCriticalMode = false;
            
            blinkTween?.Kill();
            textBlinkTween?.Kill();
            punchTween?.Kill();

            timerText.color = normalColor;
            timerText.transform.localScale = Vector3.one;

        }

        public override void OnLevelActivated()
        {
            detonatorEffect?.SetActive(true);
            timer?.Start();
        }

        public override void OnGameStateChanged(bool isActive)
        {
            if (timer == null) return;
            if (isActive)
            {
                timer.Resume();
            }
            else
            {
                timer.Pause();
            }
        }

        public override void OnGameEnded()
        {
            timer.Pause(GAME_END_REASON);
        }
        
        
        private void Update()
        {
            if (timer == null) return;
            
            timer.Update();
            if (isFirstUpdate && timer.IsActive)
            {
                isFirstUpdate = false;
                UpdateTimerText(timer.CurrentTimeSpan);
                Services.AudioService.PlaySound(AudioId.Obstacle_Bomb_in);
            }
        }

        private bool bombVisualShown = true;

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            bombVisuals.gameObject.SetActive(visible);
            if (timer != null)
            {
                // Important: avoid stacking Pause()/Resume() counts when visibility doesn't actually change.
                if (bombVisualShown != visible)
                {
                    if (visible)
                        timer.Resume();
                    else
                        timer.Pause();
                }
            }

            bombVisualShown = visible;
        }

        public override BlockEffectData GetCurrentEffectData()
        {
            int duration = timer != null ? timer.CurrentTimeSpan.Seconds : Data.bombDuration;
            return new BombBlockEffectData { bombDuration = duration };
        }

    }
}