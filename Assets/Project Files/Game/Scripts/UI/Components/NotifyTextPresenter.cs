using DG.Tweening;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class NotifyTextPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI targetText;
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private float holdDuration = 0.75f;
        [SerializeField] private float moveDistance = 40f;
        [SerializeField] private Ease fadeInEase = Ease.OutQuad;
        [SerializeField] private Ease fadeOutEase = Ease.InQuad;

        private Tween activeTween;
        private Vector2 initialAnchoredPosition;

        private void Awake()
        {
            if (!targetText)
            {
                targetText = GetComponent<TextMeshProUGUI>();
            }

            if (!targetText) return;

            initialAnchoredPosition = targetText.rectTransform.anchoredPosition;
            HideImmediate();
        }

        private void OnDestroy()
        {
            activeTween?.Kill();
        }

        public void ConfigureText(TextMeshProUGUI textComponent)
        {
            targetText = textComponent;
            if (!targetText) return;

            initialAnchoredPosition = targetText.rectTransform.anchoredPosition;
            HideImmediate();
        }

        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message) || !targetText) return;

            activeTween?.Kill();

            var textTransform = targetText.rectTransform;
            textTransform.DOKill();
            textTransform.anchoredPosition = initialAnchoredPosition;

            targetText.text = message;
            targetText.alpha = 0f;
            targetText.gameObject.SetActive(true);

            activeTween = DOTween.Sequence()
                .Append(targetText.DOFade(1f, fadeDuration).SetEase(fadeInEase))
                .Join(textTransform.DOAnchorPosY(initialAnchoredPosition.y + moveDistance, fadeDuration).SetEase(fadeInEase))
                .AppendInterval(holdDuration)
                .Append(targetText.DOFade(0f, fadeDuration).SetEase(fadeOutEase))
                .Join(textTransform.DOAnchorPosY(initialAnchoredPosition.y, fadeDuration).SetEase(fadeOutEase));
        }

        public void HideImmediate()
        {
            if (!targetText) return;

            activeTween?.Kill();

            targetText.DOKill();
            targetText.rectTransform.DOKill();
            targetText.alpha = 0f;
            targetText.rectTransform.anchoredPosition = initialAnchoredPosition;
        }

        public static NotifyTextPresenter GetOrAdd(TextMeshProUGUI textComponent)
        {
            if (!textComponent) return null;

            var presenter = textComponent.GetComponent<NotifyTextPresenter>();
            if (!presenter)
            {
                presenter = textComponent.gameObject.AddComponent<NotifyTextPresenter>();
            }

            presenter.ConfigureText(textComponent);
            return presenter;
        }
    }
}
