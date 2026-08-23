using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Ease = DG.Tweening.Ease;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Game
{
    public class PowerUpUnlockNotifyPopup : NotifyPopupBase
    {
        [SerializeField] private List<TextMeshProUGUI> textTitles;
        [SerializeField] private TextMeshProUGUI textInfo;
        [SerializeField] private Image mainIcon;
        [SerializeField] private DOTweenAnimation fadeTween;

        private BoosterConfig boosterConfig;
        private BasePowerUpConfig config;
        private bool mainIconStateCached;
        private Vector3 mainIconInitialLocalScale;
        private Vector2 mainIconInitialAnchoredPosition;
        private Sequence flySequence;
        private RectTransform impactTarget;
        private Vector3 impactTargetInitialScale;

        private const float AnticipationDuration = 0.16f;
        private const float AnticipationScale = 1.18f;
        private const float AnticipationLift = 26f;
        private const float FlyDuration = 0.5f;
        private const float FlyArcRatio = 0.22f;
        private const float FlyTiltAngle = -16f;
        private const float TargetPunchScale = 1.35f;
        private const float TargetPunchUpDuration = 0.09f;
        private const float TargetPunchDownDuration = 0.24f;

        public static void Show(BoosterConfig boosterConfig)
        {
            UIData uiData = new UIData();
            uiData.Add("config", boosterConfig);
            NotifyPopupQueue.Instance.EnqueuePanel<PowerUpUnlockNotifyPopup>(
                uiData, dedupeKey: $"PU_{boosterConfig.type}");
        }

        // OnEnable / OnDisable inherited from NotifyPopupBase

        protected new void OnDisable()
        {
            base.OnDisable();
            flySequence?.Kill();
            flySequence = null;
            if (mainIcon)
                mainIcon.rectTransform.DOKill();
            RestoreImpactTarget();
        }

        public override void Open(UIData uiData)
        {
            base.Open(uiData);
            if (uiData.TryGet("config", out BoosterConfig cfg))
            {
                this.boosterConfig = cfg;
                this.config = cfg.GetPowerUpConfig();
            }
            UpdateData();
            PlayReveal();
        }

        private void UpdateData()
        {
            foreach (var textTitle in textTitles)
                textTitle.text = $"{config.DisplayName}!";

            textInfo.text = config.Description;

            if (mainIcon)
            {
                var rt = mainIcon.rectTransform;
                mainIcon.sprite = config.Icon;
                mainIcon.gameObject.SetActive(true);
                if (!mainIconStateCached)
                {
                    mainIconInitialLocalScale = rt.localScale;
                    mainIconInitialAnchoredPosition = rt.anchoredPosition;
                    mainIconStateCached = true;
                }
                rt.localScale = mainIconInitialLocalScale;
                rt.anchoredPosition = mainIconInitialAnchoredPosition;
                rt.localRotation = Quaternion.identity;
            }
        }

        protected override async UniTask OnConfirmAsync()
        {
            Services.BoosterService.OnUnlockNotifyPopupConfirmed(boosterConfig);

            var iconRectTransform = PowerUpController.PowerUpPopup?.GetItemRectTransform(config.Type);
            if (iconRectTransform)
            {
                await PlayFlyToBarTween(iconRectTransform);
                int currentLevel = ActiveSession.Current.DisplayLevelIndex + 1;
                if (currentLevel == Services.BoosterService.GetEffectiveLevelUnlock(boosterConfig))
                    PowerUpController.PowerUpPopup?.OnUnlockNotify(config);
            }
        }

        private async UniTask PlayFlyToBarTween(RectTransform targetIcon)
        {
            if (!mainIcon || !targetIcon) return;

            var rt = mainIcon.rectTransform;
            flySequence?.Kill();
            rt.DOKill();
            rt.SetAsLastSibling();
            fadeTween.DOPlay();

            Vector3 startPosition = rt.position;
            Vector3 launchPosition = startPosition + Vector3.up * AnticipationLift;
            Vector3 endPosition = targetIcon.position;
            Vector3 startScale = rt.localScale;
            Vector3 endScale = Vector3.one * GetLocalScaleMatchingRenderedSize(rt, targetIcon);

            Vector3 travel = endPosition - launchPosition;
            Vector3 arc = Vector3.up * (travel.magnitude * FlyArcRatio);
            Vector3 startControl = launchPosition + arc + travel * 0.15f;
            Vector3 endControl = endPosition + arc * 0.5f;

            flySequence = DOTween.Sequence();
            flySequence.Insert(0f, rt.DOMove(launchPosition, AnticipationDuration).SetEase(Ease.OutQuad));
            flySequence.Insert(0f, rt.DOScale(startScale * AnticipationScale, AnticipationDuration).SetEase(Ease.OutQuad));
            flySequence.Insert(0f, rt.DOLocalRotate(new Vector3(0f, 0f, FlyTiltAngle), AnticipationDuration).SetEase(Ease.OutQuad));

            flySequence.Insert(AnticipationDuration,
                rt.DOPath(new[] { endPosition, startControl, endControl }, FlyDuration, PathType.CubicBezier)
                    .SetEase(Ease.InCubic));
            flySequence.Insert(AnticipationDuration + FlyDuration * 0.12f,
                rt.DOScale(endScale, FlyDuration * 0.88f).SetEase(Ease.InOutSine));
            flySequence.Insert(AnticipationDuration,
                rt.DOLocalRotate(Vector3.zero, FlyDuration * 0.6f).SetEase(Ease.OutBack));

            await flySequence.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
            flySequence = null;

            Services.AudioService.PlaySound(AudioId.Booster_Received);
            mainIcon.gameObject.SetActive(false);
            await PlayTargetImpactTween(targetIcon);
        }

        private async UniTask PlayTargetImpactTween(RectTransform targetIcon)
        {
            RestoreImpactTarget();

            impactTarget = targetIcon;
            impactTargetInitialScale = targetIcon.localScale;
            targetIcon.DOKill();

            var impact = DOTween.Sequence();
            impact.Append(targetIcon
                .DOScale(impactTargetInitialScale * TargetPunchScale, TargetPunchUpDuration)
                .SetEase(Ease.OutQuad));
            impact.Append(targetIcon
                .DOScale(impactTargetInitialScale, TargetPunchDownDuration)
                .SetEase(Ease.OutBack));

            await impact.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            targetIcon.localScale = impactTargetInitialScale;
            impactTarget = null;
        }

        private void RestoreImpactTarget()
        {
            if (impactTarget)
            {
                impactTarget.DOKill();
                impactTarget.localScale = impactTargetInitialScale;
            }
            impactTarget = null;
        }

        /// <summary>
        /// Source and target icons live under different rect sizes, aspect-fit modes and canvas
        /// scales, so copying <c>localScale</c> never lines them up. Solve for the local scale
        /// that makes the flown icon render at the target's on-screen size instead.
        /// </summary>
        private static float GetLocalScaleMatchingRenderedSize(RectTransform source, RectTransform target)
        {
            Vector2 sourceSize = GetRenderedSize(source);
            Vector2 targetSize = GetRenderedSize(target);
            if (sourceSize.x <= 0f || sourceSize.y <= 0f) return source.localScale.x;

            Vector3 sourceParentScale = source.parent ? source.parent.lossyScale : Vector3.one;
            if (Mathf.Approximately(sourceParentScale.x, 0f) || Mathf.Approximately(sourceParentScale.y, 0f))
                return source.localScale.x;

            Vector3 targetScale = target.lossyScale;
            float scaleX = targetSize.x * targetScale.x / (sourceSize.x * sourceParentScale.x);
            float scaleY = targetSize.y * targetScale.y / (sourceSize.y * sourceParentScale.y);
            return Mathf.Min(scaleX, scaleY);
        }

        private static Vector2 GetRenderedSize(RectTransform rectTransform)
        {
            Rect rect = rectTransform.rect;
            var image = rectTransform.GetComponent<Image>();
            if (!image || !image.preserveAspect || !image.sprite || rect.width <= 0f || rect.height <= 0f)
                return rect.size;

            Rect spriteRect = image.sprite.rect;
            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = rect.width / rect.height;
            return spriteAspect > rectAspect
                ? new Vector2(rect.width, rect.width / spriteAspect)
                : new Vector2(rect.height * spriteAspect, rect.height);
        }
    }
}
