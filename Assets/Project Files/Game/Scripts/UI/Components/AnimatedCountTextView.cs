using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Reusable count-up text animator with optional "+delta" popup and particle feedback.
    /// Drop on a UI object, assign <see cref="valueText"/> (auto-resolved from the same GameObject),
    /// and optionally wire add-delta text / particles.
    /// </summary>
    public class AnimatedCountTextView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Add Delta Effect")]
        [SerializeField] private TextMeshProUGUI addDeltaText;
        [SerializeField] private ParticleSystem addDeltaParticle;
        [SerializeField] private ParticleSystem completeParticle;
        [SerializeField] private bool playHapticOnAdd = true;

        [Header("Count Animation")]
        [SerializeField] private float scaleUpTo = 1.3f;
        [SerializeField] private float scaleUpDuration = 0.3f;
        [SerializeField] private float countDelay = 0.3f;
        [SerializeField] private float countDurationPerStep = 0.15f;
        [SerializeField] private float countMinDuration = 0.3f;
        [SerializeField] private float countMaxDuration = 2f;

        [Header("Punch Back")]
        [SerializeField] private float punchBackDelay = 0.1f;
        [SerializeField] private float scaleBackDuration = 0.2f;
        [SerializeField] private float punchAmount = 0.15f;
        [SerializeField] private float punchDuration = 0.35f;

        private const float AddDeltaMoveDuration = 0.8f;
        private const float AddDeltaMoveOffsetY = 45f;
        private const float AddDeltaFadeDuration = 1f;

        private int currentValue = -1;
        private Vector3 valueOrigScale = Vector3.one;
        private bool valueScaleCached;
        private Tween activeCountTween;
        private Sequence addDeltaSequence;
        private Tween addDeltaDeactivateTween;
        private DOTweenAnimation[] addDeltaAnimations;
        private Vector3 addDeltaInitialLocalPosition;
        private Color addDeltaInitialColor;
        private bool addDeltaVisualCached;

        public int CurrentValue => currentValue;

        private void Awake()
        {
            if (!valueText)
                valueText = GetComponent<TextMeshProUGUI>();

            CacheValueScale();
            CacheAddDeltaVisualState();
        }

        private void OnDestroy()
        {
            StopAnimation();
        }

        public void Configure(TextMeshProUGUI textComponent)
        {
            valueText = textComponent;
            valueScaleCached = false;
            CacheValueScale();
        }

        public void SetValue(int value, bool immediate = true)
        {
            if (immediate)
                StopAnimation();

            currentValue = value;
            if (!valueText) return;

            valueText.text = FormatValue(value);
        }

        /// <summary>Count from <paramref name="fromValue"/> (or current) to <paramref name="toValue"/>.</summary>
        public void AnimateTo(int toValue, int fromValue = int.MinValue, Action onComplete = null)
        {
            if (!valueText)
            {
                currentValue = toValue;
                onComplete?.Invoke();
                return;
            }

            int from = fromValue == int.MinValue ? currentValue : fromValue;
            if (from < 0)
                from = 0;

            StopCountTween();
            CacheValueScale();

            if (from == toValue)
            {
                currentValue = toValue;
                valueText.text = FormatValue(toValue);
                PunchBackScale(onComplete);
                return;
            }

            currentValue = from;
            valueText.text = FormatValue(from);

            valueText.transform.DOScale(valueOrigScale * scaleUpTo, scaleUpDuration)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);

            int counter = from;
            float duration = Mathf.Clamp(
                Mathf.Abs(toValue - from) * countDurationPerStep,
                countMinDuration,
                countMaxDuration);

            activeCountTween = DOTween.To(() => counter, x =>
                {
                    counter = x;
                    // Sync so a mid-animation re-trigger continues from the on-screen value
                    // instead of snapping back to the original start.
                    currentValue = x;
                    valueText.text = FormatValue(counter);
                }, toValue, duration)
                .SetDelay(countDelay)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    currentValue = toValue;
                    PunchBackScale(onComplete);
                });
        }

        /// <summary>Show "+delta" feedback, then count the main value up to <paramref name="toValue"/>.</summary>
        public void AnimateAdd(int addedAmount, int toValue, Action onComplete = null)
        {
            if (addedAmount > 0)
                PlayAddDelta(addedAmount);

            AnimateTo(toValue, onComplete: onComplete);
        }

        public void StopAnimation()
        {
            StopCountTween();
            KillAddDeltaTweens();

            if (valueText)
            {
                valueText.transform.DOKill();
                if (valueScaleCached)
                    valueText.transform.localScale = valueOrigScale;
            }
        }

        public static AnimatedCountTextView GetOrAdd(TextMeshProUGUI textComponent)
        {
            if (!textComponent) return null;

            var view = textComponent.GetComponent<AnimatedCountTextView>();
            if (!view)
                view = textComponent.gameObject.AddComponent<AnimatedCountTextView>();

            view.Configure(textComponent);
            return view;
        }

        private static string FormatValue(int value) => value.ToString();

        private void CacheValueScale()
        {
            if (valueScaleCached || !valueText) return;

            valueOrigScale = valueText.transform.localScale;
            valueScaleCached = true;
        }

        private void StopCountTween()
        {
            activeCountTween?.Kill();
            activeCountTween = null;

            if (valueText)
                valueText.transform.DOKill();
        }

        private void PunchBackScale(Action onComplete = null)
        {
            if (!valueText)
            {
                onComplete?.Invoke();
                return;
            }

            DOVirtual.DelayedCall(punchBackDelay, () =>
            {
                if (!valueText) return;

                Sequence seq = DOTween.Sequence().SetLink(gameObject);
                seq.Append(valueText.transform.DOScale(valueOrigScale, scaleBackDuration).SetEase(Ease.Linear));
                seq.AppendCallback(() =>
                {
                    if (completeParticle) completeParticle.Play();
                });
                seq.Append(valueText.transform.DOPunchScale(
                    new Vector3(-punchAmount, -punchAmount, 0f),
                    punchDuration,
                    10,
                    1f));
                seq.AppendCallback(() => onComplete?.Invoke());
            }).SetLink(gameObject);
        }

        private void PlayAddDelta(int delta)
        {
            if (addDeltaParticle)
                addDeltaParticle.Play();

            if (playHapticOnAdd)
                WaterFlow.Core.HapticFeedback.Play(WaterFlow.Core.HapticType.ClickButton);

            if (!addDeltaText) return;

            CacheAddDeltaVisualState();

            addDeltaText.text = $"+{delta}";
            addDeltaText.gameObject.SetActive(true);

            KillAddDeltaTweens();
            ResetAddDeltaVisualState();

            if (addDeltaAnimations != null && addDeltaAnimations.Length > 0)
                PlayAddDeltaDotweenAnimations();
            else
                PlayAddDeltaCodeTween();
        }

        private void CacheAddDeltaVisualState()
        {
            if (addDeltaVisualCached || !addDeltaText) return;

            addDeltaAnimations = addDeltaText.GetComponents<DOTweenAnimation>();
            addDeltaInitialLocalPosition = addDeltaText.rectTransform.localPosition;
            addDeltaInitialColor = addDeltaText.color;
            addDeltaVisualCached = true;
        }

        private void ResetAddDeltaVisualState()
        {
            if (!addDeltaText) return;

            addDeltaText.rectTransform.localPosition = addDeltaInitialLocalPosition;
            addDeltaText.color = addDeltaInitialColor;
        }

        private void PlayAddDeltaDotweenAnimations()
        {
            float maxDuration = 0f;

            foreach (DOTweenAnimation animation in addDeltaAnimations)
            {
                if (!animation || !animation.isActive || !animation.isValid) continue;

                animation.CreateTween();
                maxDuration = Mathf.Max(maxDuration, animation.delay + animation.duration);
            }

            ScheduleAddDeltaDeactivate(maxDuration);
        }

        private void PlayAddDeltaCodeTween()
        {
            addDeltaSequence = DOTween.Sequence().SetLink(gameObject);
            addDeltaSequence.Join(
                addDeltaText.rectTransform
                    .DOLocalMove(new Vector3(0f, AddDeltaMoveOffsetY, 0f), AddDeltaMoveDuration)
                    .SetRelative()
                    .SetEase(Ease.OutQuad));
            addDeltaSequence.Join(
                addDeltaText
                    .DOFade(0f, AddDeltaFadeDuration)
                    .SetEase(Ease.OutQuad));
            addDeltaSequence.OnComplete(DeactivateAddDeltaText);
        }

        private void ScheduleAddDeltaDeactivate(float delay)
        {
            addDeltaDeactivateTween = DOVirtual
                .DelayedCall(delay, DeactivateAddDeltaText)
                .SetTarget(addDeltaText)
                .SetLink(gameObject);
        }

        private void DeactivateAddDeltaText()
        {
            if (!addDeltaText) return;
            addDeltaText.gameObject.SetActive(false);
        }

        private void KillAddDeltaTweens()
        {
            addDeltaSequence?.Kill();
            addDeltaSequence = null;
            addDeltaDeactivateTween?.Kill();
            addDeltaDeactivateTween = null;

            if (!addDeltaText) return;

            addDeltaText.DOKill();
            addDeltaText.rectTransform.DOKill();

            if (addDeltaAnimations == null) return;
            foreach (DOTweenAnimation animation in addDeltaAnimations)
                animation?.DOKill();
        }
    }
}
