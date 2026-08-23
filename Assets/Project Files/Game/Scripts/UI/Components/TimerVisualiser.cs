using System;
using DG.Tweening;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class TimerVisualiser : MonoBehaviour
    {
        [SerializeField] TMP_Text timerText;
        [SerializeField] SlicedFilledImage fillImage;

        public RectTransform TimerTargetRect => timerText ? timerText.rectTransform : null;

        [Header("Add Time Effect")]
        [SerializeField] private ParticleSystem addTimeEffect;
        [SerializeField] private TextMeshProUGUI addTimeText;
        
        [Header("Warning Settings")]
        [SerializeField] private int[] warningThresholds = { 30, 20, 10 };
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;

        [Header("Warning Animation")]
        [SerializeField] private float punchScale = 0.3f;
        [SerializeField] private float punchDuration = 0.3f;
        [SerializeField] private int punchVibrato = 5;

        private const float AddTimeMoveDuration = 0.8f;
        private const float AddTimeMoveOffsetY = 45f;
        private const float AddTimeFadeDuration = 1f;

        private GameplayTimer timer;
        private int lastTotalSeconds = int.MaxValue;
        private WarningLevel currentWarningLevel = WarningLevel.None;
        private Tweener colorTween;
        private Tweener punchTween;
        private DOTweenAnimation[] addTimeAnimations;
        private Vector3 addTimeTextInitialLocalPosition;
        private Color addTimeTextInitialColor;
        private bool addTimeVisualCached;
        private Sequence addTimeSequence;
        private Tween addTimeDeactivateTween;

        private enum WarningLevel
        {
            None,
            Warning,    // 30s
            Danger,     // 20s
            Critical    // 10s
        }

        private void Awake()
        {
            CacheAddTimeVisualState();
        }

        public void Show(GameplayTimer timer)
        {
            this.timer = timer;

            gameObject.SetActive(true);

            timer.OnTimeSpanChanged += OnTimeChanged;

            ResetState();
            OnTimeChanged(timer.CurrentTimeSpan);
        }

        private void OnDestroy()
        {
            if (timer != null)
                timer.OnTimeSpanChanged -= OnTimeChanged;

            KillTweens();
        }

        public void Hide()
        {
            gameObject.SetActive(false);

            if (timer != null)
                timer.OnTimeSpanChanged -= OnTimeChanged;

            KillTweens();
        }
        
        public void UpdateTimeMaterial(Material material)
        {
            timerText.fontMaterial = material;
        }

        private void KillTweens()
        {
            colorTween?.Kill();
            punchTween?.Kill();
            KillAddTimeTweens();
        }

        private void KillAddTimeTweens()
        {
            addTimeSequence?.Kill();
            addTimeSequence = null;
            addTimeDeactivateTween?.Kill();
            addTimeDeactivateTween = null;

            if (!addTimeText) return;

            addTimeText.DOKill();
            addTimeText.rectTransform.DOKill();

            if (addTimeAnimations == null) return;
            foreach (DOTweenAnimation animation in addTimeAnimations)
            {
                animation?.DOKill();
            }
        }

        private void ResetState()
        {
            lastTotalSeconds = int.MaxValue;
            currentWarningLevel = WarningLevel.None;
            timerText.color = normalColor;
            timerText.transform.localScale = Vector3.one;
            fillImage.fillAmount = 0;
            KillTweens();
        }

        public void Refresh()
        {
            if (timer == null) return;
            ResetState();
            OnTimeChanged(timer.CurrentTimeSpan);
        }

        public void SetFreezeFillAmount(float t)
        {
            fillImage.fillAmount = t;
        }

        private void OnTimeChanged(TimeSpan timeSpan)
        {
            timerText.text = $"{timeSpan:mm\\:ss}";

            int totalSeconds = (int)timeSpan.TotalSeconds;

            if (totalSeconds == lastTotalSeconds) return;
            if (totalSeconds > lastTotalSeconds) OnAddTime(totalSeconds - lastTotalSeconds);

            bool isFirstSync = lastTotalSeconds == int.MaxValue;
            lastTotalSeconds = totalSeconds;

            CheckWarningThreshold(totalSeconds, isFirstSync);
        }

        private void OnAddTime(int deltaSeconds)
        {
            addTimeEffect?.Play();
            global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.ClickButton);
            PlayPunchAnimation();

            if (!addTimeText) return;

            CacheAddTimeVisualState();

            addTimeText.text = $"+{deltaSeconds}";
            addTimeText.gameObject.SetActive(true);

            KillAddTimeTweens();
            ResetAddTimeVisualState();

            if (addTimeAnimations != null && addTimeAnimations.Length > 0)
                PlayAddTimeDotweenAnimations();
            else
                PlayAddTimeCodeTween();
        }

        private void CacheAddTimeVisualState()
        {
            if (addTimeVisualCached || !addTimeText) return;

            addTimeAnimations = addTimeText.GetComponents<DOTweenAnimation>();
            addTimeTextInitialLocalPosition = addTimeText.rectTransform.localPosition;
            addTimeTextInitialColor = addTimeText.color;
            addTimeVisualCached = true;
        }

        private void ResetAddTimeVisualState()
        {
            addTimeText.rectTransform.localPosition = addTimeTextInitialLocalPosition;
            addTimeText.color = addTimeTextInitialColor;
        }

        private void PlayAddTimeDotweenAnimations()
        {
            float maxDuration = 0f;

            foreach (DOTweenAnimation animation in addTimeAnimations)
            {
                if (!animation || !animation.isActive || !animation.isValid) continue;

                animation.CreateTween();
                maxDuration = Mathf.Max(maxDuration, animation.delay + animation.duration);
            }

            ScheduleAddTimeDeactivate(maxDuration);
        }

        private void PlayAddTimeCodeTween()
        {
            addTimeSequence = DOTween.Sequence();
            addTimeSequence.Join(
                addTimeText.rectTransform
                    .DOLocalMove(new Vector3(0f, AddTimeMoveOffsetY, 0f), AddTimeMoveDuration)
                    .SetRelative()
                    .SetEase(Ease.OutQuad));
            addTimeSequence.Join(
                addTimeText
                    .DOFade(0f, AddTimeFadeDuration)
                    .SetEase(Ease.OutQuad));
            addTimeSequence.OnComplete(DeactivateAddTimeText);
        }

        private void ScheduleAddTimeDeactivate(float delay)
        {
            addTimeDeactivateTween = DOVirtual
                .DelayedCall(delay, DeactivateAddTimeText)
                .SetTarget(addTimeText);
        }

        private void DeactivateAddTimeText()
        {
            if (!addTimeText) return;
            addTimeText.gameObject.SetActive(false);
        }

        private void CheckWarningThreshold(int totalSeconds, bool isFirstSync)
        {
            WarningLevel newLevel = GetWarningLevel(totalSeconds);

            if (newLevel != currentWarningLevel)
            {
                currentWarningLevel = newLevel;
                ApplyWarningEffect(newLevel);
            }

            if (IsExactThreshold(totalSeconds))
            {
                PlayPunchAnimation();

                if (!isFirstSync)
                    Services.AudioService.PlaySound(AudioId.Time_Warning);
            }
        }

        private WarningLevel GetWarningLevel(int totalSeconds)
        {
            if (warningThresholds.Length >= 3 && totalSeconds <= warningThresholds[2])
                return WarningLevel.Critical;
            if (warningThresholds.Length >= 2 && totalSeconds <= warningThresholds[1])
                return WarningLevel.Danger;
            if (warningThresholds.Length >= 1 && totalSeconds <= warningThresholds[0])
                return WarningLevel.Warning;

            return WarningLevel.None;
        }

        private bool IsExactThreshold(int totalSeconds)
        {
            foreach (int threshold in warningThresholds)
            {
                if (totalSeconds == threshold) return true;
            }
            return false;
        }

        private void ApplyWarningEffect(WarningLevel level)
        {
            Color targetColor = level switch
            {
                WarningLevel.Warning => warningColor,
                WarningLevel.Danger => warningColor,
                WarningLevel.Critical => criticalColor,
                _ => normalColor
            };

            colorTween?.Kill();
            colorTween = timerText.DOColor(targetColor, 0.3f).SetEase(Ease.OutSine);

            if (level == WarningLevel.Critical)
            {
                StartCriticalBlink();
            }
        }

        private void PlayPunchAnimation()
        {
            punchTween?.Kill();
            if(!timerText) return;
            timerText.transform.localScale = Vector3.one;
            punchTween = timerText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato)
                .SetEase(Ease.OutElastic);
        }

        private void StartCriticalBlink()
        {
            colorTween?.Kill();
            colorTween = timerText.DOColor(criticalColor, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }
}
