using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Game
{
    /// <summary>
    /// Sizes a UI <see cref="Image"/> so it always fully covers its parent rect (no letterbox/pillarbox
    /// bars) while preserving the source sprite's aspect ratio. This is "cover"/"envelope" behaviour:
    /// the image is scaled to the larger of the two fit axes and the overflow is cropped by the parent.
    ///
    /// Driven by the sprite's real pixel dimensions and re-applied on every rect/resolution change, so a
    /// single portrait splash fills any device aspect without manual per-resolution tuning.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class BackgroundScreenFitter : MonoBehaviour
    {
        [Tooltip("Image whose sprite aspect drives the fit. Defaults to the Image on this GameObject.")]
        [SerializeField] private Image targetImage;

        [Tooltip("Used only when the sprite is missing (e.g. loaded later). Width / Height of the artwork.")]
        [SerializeField] private Vector2 fallbackAspectSize = new Vector2(1229f, 2048f);

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            if (targetImage == null)
                targetImage = GetComponent<Image>();

            // AspectRatioFitter would fight us by overwriting sizeDelta every frame, so disable it.
            var fitter = GetComponent<AspectRatioFitter>();
            if (fitter != null)
                fitter.enabled = false;
        }

        private void OnEnable() => Fit();

        // Stretch anchors keep our rect tied to the parent's size, so this fires on every screen
        // resize / orientation flip / Canvas scaler update — that is what re-runs the fit.
        private void OnRectTransformDimensionsChange() => Fit();

        /// <summary>Re-run the fit. Call after assigning the sprite at runtime.</summary>
        public void Fit()
        {
            if (rectTransform == null)
                rectTransform = (RectTransform)transform;

            if (!(rectTransform.parent is RectTransform parent))
                return;

            Vector2 parentSize = parent.rect.size;
            if (parentSize.x <= 0f || parentSize.y <= 0f)
                return;

            float imageAspect = GetImageAspect();
            if (imageAspect <= 0f)
                return;

            // Full-stretch anchors so the rect tracks the parent (centered anchors would freeze our
            // size and stop OnRectTransformDimensionsChange from firing on resize -> stale fit, bars).
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;

            float parentAspect = parentSize.x / parentSize.y;
            Vector2 coverSize = parentAspect > imageAspect
                ? new Vector2(parentSize.x, parentSize.x / imageAspect)  // wider screen: match width, overflow height
                : new Vector2(parentSize.y * imageAspect, parentSize.y); // taller screen: match height, overflow width

            // With stretch anchors the rect = parentSize + sizeDelta, so offset by the difference.
            Vector2 sizeDelta = coverSize - parentSize;
            if (rectTransform.sizeDelta != sizeDelta)
                rectTransform.sizeDelta = sizeDelta;
        }

        private float GetImageAspect()
        {
            if (targetImage != null && targetImage.sprite != null)
            {
                Rect spriteRect = targetImage.sprite.rect;
                if (spriteRect.height > 0f)
                    return spriteRect.width / spriteRect.height;
            }

            return fallbackAspectSize.y > 0f ? fallbackAspectSize.x / fallbackAspectSize.y : 0f;
        }
    }
}
