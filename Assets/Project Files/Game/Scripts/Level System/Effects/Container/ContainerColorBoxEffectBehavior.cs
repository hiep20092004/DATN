using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Container box whose countdown only advances when a cleared block matches the configured color.
    /// Shares all container behavior with <see cref="ContainerBoxEffectBehavior"/> (immovable, holds blocks
    /// beneath it, blocks direct clicks); the only difference is the color-gated <see cref="CountsTowardClear"/>.
    /// </summary>
    public sealed class ContainerColorBoxEffectBehavior
        : BaseContainerBoxEffectBehavior<ContainerColorBoxEffectConfig>, IGroupClickReceiver
    {
        private TweenCase blockedClickShakeTween;
        private Vector3 blockedClickShakeBaseLocalPosition;

        protected override IGroupClickReceiver GetGroupClickReceiver()
        {
            return this;
        }

        protected override bool CountsTowardClear(LevelBlockBehavior filledBlock, BlockColor filledColor)
        {
            if (EffectData is not ContainerColorBoxBlockEffectData colorData)
                return false;

            return filledColor == colorData.colorCount;
        }

        public bool CanClick()
        {
            return false;
        }

        public void OnClicked()
        {

        }

        public void OnBlocked()
        {
            PlayBlockedClickShake();
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Deny);
            Services.AudioService.PlaySound(AudioId.Click_obs_box);
        }

        private void PlayBlockedClickShake()
        {
            if (!isActiveAndEnabled && !groupVisual)
                return;

            blockedClickShakeTween?.KillActive();
            var groupVisualTransform = groupVisual.transform;
            blockedClickShakeBaseLocalPosition = groupVisualTransform.localPosition;
            groupVisualTransform.localPosition = blockedClickShakeBaseLocalPosition;
            blockedClickShakeTween = groupVisualTransform
                .DOShake(
                    Mathf.Max(0f, config.BlockedClickShakeDuration),
                    Mathf.Max(0f, config.BlockedClickShakeStrength))
                .OnComplete(() =>
                {
                    if (this && groupVisual)
                        groupVisualTransform.localPosition = blockedClickShakeBaseLocalPosition;
                });
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            base.OnDisabled(blockBehavior, disableSource);
            blockedClickShakeBaseLocalPosition = groupVisual.transform.localPosition;
            blockedClickShakeTween?.KillActive();
            blockedClickShakeTween = null;
            groupVisual.transform.localPosition = blockedClickShakeBaseLocalPosition;
        }

        private void OnDestroy()
        {
            blockedClickShakeTween?.KillActive();
            blockedClickShakeTween = null;
        }
    }
}
