using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class ContainerBoxEffectBehavior : BaseContainerBoxEffectBehavior<ContainerBoxEffectConfig>, IGroupClickReceiver
    {
        private TweenCase blockedClickShakeTween;
        private Vector3 blockedClickShakeBaseLocalPosition;
        
        protected override IGroupClickReceiver GetGroupClickReceiver()
        {
            return this;
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
