using DG.Tweening;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game.Common
{
    public class MessageInfoView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Dynamic Positioning")]
        [SerializeField] private float offsetFromAnchor = 20f;
        [SerializeField] private float screenPadding = 20f;
        [SerializeField] private Vector3 arrowOffsetYWhenDown = Vector3.zero;
        [SerializeField] private Vector3 arrowOffsetYWhenUp = Vector3.zero;

        [Header("Sizing")]
        [SerializeField] private Vector2 textPadding = new Vector2(40f, 20f);
        [SerializeField] private float maxTextWidth = 700f;

        [Header("Animation")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.15f;

        [Header("Test / Runtime")]
        [SerializeField] private RectTransform testAnchor;
        [SerializeField] private RectTransform boundsRect;

        private RectTransform rectTransform;
        private RectTransform parentRect;
        private Tween currentTween;
        private bool isOpened;

        public enum ArrowDirection { Up, Down }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void ShowAtAnchor(RectTransform anchor, string message, RectTransform boundsRect = null,
            ArrowDirection? forceDirection = null)
        {
            if (!anchor) return;

#if UNITY_EDITOR
            testAnchor = anchor;
            this.boundsRect = boundsRect;
#endif

            if (messageText != null)
                messageText.text = message;

            gameObject.SetActive(true);

            ApplySizing(message);
            Canvas.ForceUpdateCanvases();
            PositionAtAnchor(anchor, boundsRect, forceDirection);
            Show();
        }

        private void ApplySizing(string message)
        {
            if (rectTransform == null || messageText == null) return;

            // Make sure the text stays inside the bubble with consistent padding.
            var textRt = messageText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.offsetMin = new Vector2(textPadding.x * 0.5f, textPadding.y * 0.5f);
            textRt.offsetMax = new Vector2(-textPadding.x * 0.5f, -textPadding.y * 0.5f);

            // Size the bubble to the TMP preferred size (clamped by maxTextWidth) + padding.
            // This avoids needing LayoutGroup/ContentSizeFitter and ignores the arrow (IgnoreLayout).
            Vector2 preferred = messageText.GetPreferredValues(message, maxTextWidth, Mathf.Infinity);
            float w = Mathf.Ceil(preferred.x + textPadding.x);
            float h = Mathf.Ceil(preferred.y + textPadding.y);
            rectTransform.sizeDelta = new Vector2(w, h);
        }

        public void Show()
        {
            currentTween?.Kill();
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (rectTransform != null)
                rectTransform.localScale = Vector3.zero;

            var seq = DOTween.Sequence();
            if (canvasGroup != null)
                seq.Append(canvasGroup.DOFade(1f, showDuration));
            else
                seq.AppendInterval(showDuration);

            if (rectTransform != null)
                seq.Join(rectTransform.DOScale(1f, showDuration).SetEase(Ease.OutBack));

            currentTween = seq;
            isOpened = true;
        }

        public void Hide()
        {
            currentTween?.Kill();

            var seq = DOTween.Sequence();
            if (canvasGroup != null)
                seq.Append(canvasGroup.DOFade(0f, hideDuration));
            else
                seq.AppendInterval(hideDuration);

            if (rectTransform != null)
                seq.Join(rectTransform.DOScale(0.8f, hideDuration).SetEase(Ease.InQuad));

            seq.OnComplete(() =>
            {
                isOpened = false;
                gameObject.SetActive(false);
            });
            currentTween = seq;
        }

        public bool IsOpened => isOpened;

        public void RePosition()
        {
            if (testAnchor == null)
            {
                Debug.LogWarning("[MessageInfoView] testAnchor is not assigned.");
                return;
            }

            if (parentRect == null)
                parentRect = transform.parent as RectTransform;

            Canvas.ForceUpdateCanvases();
            PositionAtAnchor(testAnchor, boundsRect);
        }

        private static Camera GetRenderCamera(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            var root = canvas.rootCanvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }

        private ArrowDirection PickDirection(float anchorTopY, float anchorBottomY,
            float bubbleHalfHeight, float minY, float maxY)
        {
            float centerDown = anchorTopY + offsetFromAnchor + bubbleHalfHeight;
            float centerUp = anchorBottomY - offsetFromAnchor - bubbleHalfHeight;

            bool downFits = centerDown >= minY && centerDown <= maxY;
            bool upFits = centerUp >= minY && centerUp <= maxY;

            if (downFits) return ArrowDirection.Down;
            if (upFits) return ArrowDirection.Up;

            float downOverflow = Mathf.Max(0f, centerDown - maxY);
            float upOverflow = Mathf.Max(0f, minY - centerUp);
            return downOverflow <= upOverflow ? ArrowDirection.Down : ArrowDirection.Up;
        }

        private static float ClampToRange(float value, float min, float max)
        {
            if (min > max)
                return (min + max) * 0.5f;
            return Mathf.Clamp(value, min, max);
        }

        private void PositionAtAnchor(RectTransform anchor, RectTransform boundsRect = null,
            ArrowDirection? forceDirection = null)
        {
            if (parentRect == null)
                parentRect = transform.parent as RectTransform;

            if (parentRect == null || rectTransform == null)
            {
                Debug.LogWarning("[MessageInfoView] No RectTransform parent found; cannot position dynamically.");
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var anchorCam = GetRenderCamera(anchor);
            var parentCam = GetRenderCamera(parentRect);

            Vector3[] anchorCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);

            Vector3 anchorCenter = (anchorCorners[0] + anchorCorners[2]) / 2f;
            Vector3 anchorTopWorld = (anchorCorners[1] + anchorCorners[2]) / 2f;
            Vector3 anchorBottomWorld = (anchorCorners[0] + anchorCorners[3]) / 2f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, RectTransformUtility.WorldToScreenPoint(anchorCam, anchorCenter),
                parentCam, out Vector2 anchorLocalPos);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, RectTransformUtility.WorldToScreenPoint(anchorCam, anchorTopWorld),
                parentCam, out Vector2 anchorTopLocal);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, RectTransformUtility.WorldToScreenPoint(anchorCam, anchorBottomWorld),
                parentCam, out Vector2 anchorBottomLocal);

            float bubbleHalfWidth = rectTransform.rect.width / 2f;
            float bubbleHalfHeight = rectTransform.rect.height / 2f;

            float minX, maxX, minY, maxY;
            if (boundsRect != null)
            {
                var boundsCam = GetRenderCamera(boundsRect);
                Vector3[] bc = new Vector3[4];
                boundsRect.GetWorldCorners(bc);

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, RectTransformUtility.WorldToScreenPoint(boundsCam, bc[0]),
                    parentCam, out Vector2 bMin);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, RectTransformUtility.WorldToScreenPoint(boundsCam, bc[2]),
                    parentCam, out Vector2 bMax);

                minX = bMin.x + bubbleHalfWidth + screenPadding;
                maxX = bMax.x - bubbleHalfWidth - screenPadding;
                minY = bMin.y + bubbleHalfHeight + screenPadding;
                maxY = bMax.y - bubbleHalfHeight - screenPadding;
            }
            else
            {
                minX = -parentRect.rect.width / 2f + bubbleHalfWidth + screenPadding;
                maxX = parentRect.rect.width / 2f - bubbleHalfWidth - screenPadding;
                minY = -parentRect.rect.height / 2f + bubbleHalfHeight + screenPadding;
                maxY = parentRect.rect.height / 2f - bubbleHalfHeight - screenPadding;
            }

            ArrowDirection direction = forceDirection
                                      ?? PickDirection(anchorTopLocal.y, anchorBottomLocal.y, bubbleHalfHeight, minY, maxY);

            float bubbleY;
            if (direction == ArrowDirection.Down)
                bubbleY = anchorTopLocal.y + offsetFromAnchor + bubbleHalfHeight;
            else
                bubbleY = anchorBottomLocal.y - offsetFromAnchor - bubbleHalfHeight;

            float bubbleX = ClampToRange(anchorLocalPos.x, minX, maxX);
            bubbleY = ClampToRange(bubbleY, minY, maxY);

            rectTransform.anchoredPosition = new Vector2(bubbleX, bubbleY);

            if (arrow == null) return;

            float arrowLocalX = Mathf.Clamp(anchorLocalPos.x - bubbleX, -bubbleHalfWidth, bubbleHalfWidth);

            if (direction == ArrowDirection.Down)
            {
                arrow.anchorMin = new Vector2(0.5f, 0f);
                arrow.anchorMax = new Vector2(0.5f, 0f);
                arrow.pivot = new Vector2(0.5f, 1f);
                arrow.anchoredPosition = new Vector2(arrowLocalX, arrowOffsetYWhenDown.y);
                arrow.localRotation = Quaternion.identity;
            }
            else
            {
                arrow.anchorMin = new Vector2(0.5f, 1f);
                arrow.anchorMax = new Vector2(0.5f, 1f);
                arrow.pivot = new Vector2(0.5f, 0f);
                arrow.anchoredPosition = new Vector2(arrowLocalX, arrowOffsetYWhenUp.y);
                arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }
        }
    }
}

