using Coffee.UIExtensions;
using DG.Tweening;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Visual effects for the Freeze power-up.
    /// Displays countdown, screen tint, and animated effects.
    /// </summary>
    public class FreezeVisualEffect : MonoBehaviour
    {
        private const float IMPACT_EFFECT_DURATION = 1.0f;

        [SerializeField] private UIParticle FreezeTrailEffect;
        [SerializeField] private UIParticle FreezeImpactEffect;
        [SerializeField] CanvasGroup FreezeUIEffect;

        private DG.Tweening.Tween freezeTrailTween;
        private DG.Tweening.Tween freezeImpactTween;
        private DG.Tweening.Tween freezeUiFadeTween;
        private Vector3 originalPosition;
        private EventBinding<LevelEndedEvent> levelEndedEvent;
        private PowerUpBehavior behavior;

        private bool IsFreezeUiAvailable => FreezeUIEffect;

        private void OnEnable()
        {
            PowerUpController.Used += OnPUUsed;
            levelEndedEvent = new EventBinding<LevelEndedEvent>(OnLevelEnded);
        }

        private void OnDisable()
        {
            PowerUpController.Used -= OnPUUsed;
            if (levelEndedEvent != null)
                EventBus<LevelEndedEvent>.Deregister(levelEndedEvent);
            StopFreezeEffects();
        }

        private void OnPUUsed(PowerUpType powerUpType)
        {
            if (powerUpType != PowerUpType.Freeze) return;
            behavior = PowerUpController.GetPowerUpBehavior(powerUpType);
            if (!behavior) return;
            PlayFreezeEffects();
        }

        private void EnableVisuals()
        {
            if (!IsFreezeUiAvailable)
                return;

            PUTimer timer = behavior.GetTimer();
            if (timer == null)
                return;

            timer.OnCompleted(DisableVisuals);

            KillFreezeUiFadeTween();
            FreezeUIEffect.gameObject.SetActive(true);
            freezeUiFadeTween = FreezeUIEffect.DOFade(1f, 0.5f)
                .From(0f)
                .SetLink(FreezeUIEffect.gameObject);
        }

        private void PlayFreezeEffects()
        {
            StopFreezeTweens();
            if (!FreezeTrailEffect) return;
            originalPosition = PowerUpController.PowerUpPopup.GetPowerUpItemPosition(PowerUpType.Freeze);
            FreezeTrailEffect.rectTransform.position = originalPosition;
            FreezeTrailEffect.gameObject.SetActive(true);
            FreezeTrailEffect.Play();
            
            UIGame uiGame = UIController.GetPage<UIGame>();
            var timerVisualiser = uiGame.TimerVisualiser;
            
            if (!timerVisualiser)
            {
                FreezeTrailEffect.gameObject.SetActive(false);
                FreezeTrailEffect.Stop();
                return;
            }

            freezeTrailTween = FreezeTrailEffect.transform.DOMove(timerVisualiser.transform.position, FreezeTimerPowerUpBehavior.TIMER_DELAY)
                .SetEase(DG.Tweening.Ease.OutCubic)
                .OnComplete(() =>
                {
                    if (FreezeTrailEffect)
                    {
                        FreezeTrailEffect.gameObject.SetActive(false);
                        FreezeTrailEffect.Stop();
                        FreezeTrailEffect.rectTransform.position = originalPosition;
                    }
                    PlayImpactEffect(timerVisualiser);
                });
        }

        private void PlayImpactEffect(TimerVisualiser timerVisualiser)
        {
            if (!FreezeImpactEffect || !timerVisualiser) return;
            FreezeImpactEffect.transform.position = timerVisualiser.transform.position;
            FreezeImpactEffect.gameObject.SetActive(true);
            FreezeImpactEffect.Play();
            EnableVisuals();
            
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);
            freezeImpactTween = DOVirtual.DelayedCall(IMPACT_EFFECT_DURATION, () =>
            {
                if (FreezeImpactEffect)
                {
                    FreezeImpactEffect.Stop();
                    FreezeImpactEffect.gameObject.SetActive(false);
                }
            });
        }

        private void StopFreezeEffects()
        {
            StopFreezeTweens();
            HideFreezeUiImmediate();

            if (FreezeTrailEffect)
            {
                FreezeTrailEffect.Stop();
                FreezeTrailEffect.gameObject.SetActive(false);
            }

            if (FreezeImpactEffect)
            {
                FreezeImpactEffect.Stop();
                FreezeImpactEffect.gameObject.SetActive(false);
            }
        }

        private void OnLevelEnded(LevelEndedEvent _)
        {
            StopFreezeEffects();
        }

        private void KillFreezeUiFadeTween()
        {
            freezeUiFadeTween?.Kill();
            freezeUiFadeTween = null;

            if (IsFreezeUiAvailable)
                FreezeUIEffect.DOKill();
        }

        private void HideFreezeUiImmediate()
        {
            KillFreezeUiFadeTween();

            if (!IsFreezeUiAvailable)
                return;

            FreezeUIEffect.alpha = 0f;
            FreezeUIEffect.gameObject.SetActive(false);
        }

        private void StopFreezeTweens()
        {
            freezeTrailTween?.Kill();
            freezeTrailTween = null;

            freezeImpactTween?.Kill();
            freezeImpactTween = null;
        }
        
        
        private void DisableVisuals()
        {
            if (!this || !IsFreezeUiAvailable)
                return;

            KillFreezeUiFadeTween();
            freezeUiFadeTween = FreezeUIEffect.DOFade(0f, 0.8f)
                .From(1f)
                .SetLink(FreezeUIEffect.gameObject)
                .OnComplete(() =>
                {
                    freezeUiFadeTween = null;
                    if (!this || !IsFreezeUiAvailable)
                        return;

                    FreezeUIEffect.gameObject.SetActive(false);
                });
        }
    }
}