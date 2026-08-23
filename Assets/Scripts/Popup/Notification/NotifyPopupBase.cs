using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Game
{
    /// <summary>
    /// Shared base for single-use notify/unlock popups.
    /// Handles the close-button listeners and the isClosing guard so subclasses
    /// only need to implement <see cref="OnConfirmAsync"/>.
    /// </summary>
    public abstract class NotifyPopupBase : Panel
    {
        [SerializeField] protected Button continueBtn;
        [SerializeField] protected Button closeButton;

        [BoxGroup("REVEAL")] [SerializeField] protected ScalePerCharacter titleScaleText;
        [BoxGroup("REVEAL")] [SerializeField] protected List<UITweenElement> revealElements;
        [BoxGroup("REVEAL")] [SerializeField] protected float revealInputLock = 0.9f;
        [BoxGroup("REVEAL")] [SerializeField] protected ParticleSystem burstEffect;
        [BoxGroup("REVEAL")] [SerializeField] protected ParticleSystem loopEffect;
        [BoxGroup("REVEAL")] [SerializeField] protected float effectDelay = 0.5f;
        [BoxGroup("REVEAL")] [SerializeField] protected AudioId revealSound = AudioId.Booster_Unlock;

        protected bool isClosing;
        protected bool isRevealing;

        // Popups that show several entries in a row replay the reveal per entry, but the
        // unlock stinger must land only once per open or it stacks into noise.
        private bool revealSoundPlayed;

        protected void OnEnable()
        {
            isClosing = false;
            revealSoundPlayed = false;
            if (continueBtn) continueBtn.onClick.AddListener(OnClick);
            if (closeButton) closeButton.onClick.AddListener(OnClick);
        }

        protected void OnDisable()
        {
            if (continueBtn) continueBtn.onClick.RemoveListener(OnClick);
            if (closeButton) closeButton.onClick.RemoveListener(OnClick);
        }

        protected void OnClick()
        {
            if (isClosing || isRevealing) return;
            HandleClick();
        }

        /// <summary>
        /// Plays the staggered entry animation: title characters, then every
        /// <see cref="revealElements"/> tween (each carries its own delay), then the
        /// unlock particles. Safe to call again to replay for a following entry.
        /// </summary>
        protected void PlayReveal()
        {
            if (titleScaleText != null)
            {
                if (titleScaleText.canvasGroup != null)
                    titleScaleText.canvasGroup.alpha = 0f;
                titleScaleText.Play().Forget();
            }

            if (revealElements != null)
            {
                foreach (var element in revealElements)
                {
                    if (element != null) element.Play();
                }
            }

            ResetEffect(burstEffect);
            ResetEffect(loopEffect);

            LockInputDuringReveal().Forget();
            PlayEffectsDelayed().Forget();
        }

        private async UniTaskVoid LockInputDuringReveal()
        {
            isRevealing = true;
            await UniTask.WaitForSeconds(revealInputLock, cancellationToken: this.GetCancellationTokenOnDestroy());
            isRevealing = false;
        }

        private async UniTaskVoid PlayEffectsDelayed()
        {
            await UniTask.WaitForSeconds(effectDelay, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (burstEffect != null) burstEffect.Play(true);
            if (loopEffect != null) loopEffect.Play(true);

            if (revealSound != AudioId.None && !revealSoundPlayed)
            {
                revealSoundPlayed = true;
                Services.AudioService.PlaySound(revealSound);
            }
        }

        private static void ResetEffect(ParticleSystem effect)
        {
            if (effect == null) return;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// Override to change click logic (e.g. test-swap in ObstacleUnlockNotifyPopup).
        /// Default behaviour: run <see cref="OnConfirmAsync"/> then close.
        /// </summary>
        protected virtual void HandleClick()
        {
            isClosing = true;
            RunConfirmAndClose().Forget();
        }

        private async UniTaskVoid RunConfirmAndClose()
        {
            await OnConfirmAsync();
            Close();
        }

        /// <summary>
        /// Async confirm logic. Override in subclass.
        /// When this Task completes the popup will automatically close
        /// (unless <see cref="HandleClick"/> was overridden to suppress that).
        /// </summary>
        protected abstract UniTask OnConfirmAsync();

        /// <summary>
        /// Request close without running <see cref="OnConfirmAsync"/> again.
        /// Safe to call from a subclass that manages multi-step flow.
        /// </summary>
        protected void RequestClose()
        {
            if (isClosing) return;
            isClosing = true;
            Close();
        }
    }
}
