using System;
using DG.Tweening;
using UnityEngine;
using Ease = DG.Tweening.Ease;

namespace WaterFlow.Game
{
    public sealed class LiftGroupVisual : MonoBehaviour, IBlockGroupVisual
    {
        [SerializeField] private SpriteRenderer bounderRenderer;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteRenderer liftBgRenderer;
        [SerializeField] private Transform maskTransform;
        [SerializeField] private SpriteRenderer lineSpriteLeft;
        [SerializeField] private SpriteRenderer lineSpriteRight;
        [SerializeField] private Transform lineTransformLeft;
        [SerializeField] private Transform lineTransformRight;
        [SerializeField] private Transform liftBgTransform;
        
        private LiftConfig config;
        private Tween dissolveTween;
        private Tween spawnFadeTween;
        private Tween openTween;
        private Vector2Int boundsSize;
        private float lineLeftClosedX;
        private float lineRightClosedX;
        
        public void Configure(LiftConfig liftConfig) => config = liftConfig;

        public void Apply(BlockGroupOccupiedBounds bounds)
        {
            if (!bounds.IsValid)
                return;
            boundsSize = bounds.CellCount;
            SetPositionFromBounds(bounds);
            InitLiftSprite();
            InitMaskSize();
            InitLine();
            gameObject.SetActive(true);
        }

        private void InitLiftSprite()
        {
            bounderRenderer.drawMode = SpriteDrawMode.Sliced;
            float widthPad = config ? config.SizeWidthPadding : 0f;
            float heightPad = config ? config.SizeHeightPadding : 0f;
            Vector2 liftSize = new Vector2(
                Mathf.Max(0f, boundsSize.x + widthPad),
                Mathf.Max(0f, boundsSize.y + heightPad));
            bounderRenderer.size = liftSize;
            spriteRenderer.size = new Vector2(boundsSize.x, boundsSize.y);
            liftBgTransform.localScale = new Vector3(boundsSize.x, boundsSize.y, 1f);
        }

        private void InitLine()
        {
            float sizeX = lineSpriteLeft.size.x;
            float offset = sizeX / 2;
            lineSpriteLeft.size = new Vector2(sizeX, boundsSize.y);
            lineSpriteRight.size = new Vector2(sizeX, boundsSize.y);
            lineTransformLeft.localPosition = new Vector3(-offset, 0.01f, 0);
            lineTransformRight.localPosition = new Vector3(offset, 0.01f, 0);
            lineLeftClosedX = -offset;
            lineRightClosedX = offset;
        }

        /// <summary>
        /// Slides the doors open: grows the reveal mask along x from 0 to the bound width while the
        /// two seam lines part outward by half the bound width each. Returns the driving tween (null
        /// when it resolves instantly) so the caller can sequence the block rise / dissolve against it.
        /// </summary>
        public Tween PlayOpenDoor(float duration, Ease ease)
        {
            openTween?.Kill();

            if (!Application.isPlaying || duration <= 0f)
            {
                ApplyOpenProgress(1f);
                return null;
            }

            ApplyOpenProgress(0f);
            openTween = DOVirtual.Float(0f, 1f, duration, ApplyOpenProgress)
                .SetEase(ease)
                .SetLink(gameObject);
            return openTween;
        }

        // t: 0 = fully closed, 1 = fully open.
        private void ApplyOpenProgress(float t)
        {
            float halfWidth = boundsSize.x * 0.5f;

            if (maskTransform)
            {
                Vector3 scale = maskTransform.localScale;
                scale.x = boundsSize.x * t;
                maskTransform.localScale = scale;
            }

            if (lineTransformLeft)
            {
                Vector3 p = lineTransformLeft.localPosition;
                p.x = lineLeftClosedX - halfWidth * t;
                lineTransformLeft.localPosition = p;
            }

            if (lineTransformRight)
            {
                Vector3 p = lineTransformRight.localPosition;
                p.x = lineRightClosedX + halfWidth * t;
                lineTransformRight.localPosition = p;
            }
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Fades the bound in from transparent to opaque.</summary>
        public Tween PlayFadeIn(float duration, Ease ease, float delay = 0f)
        {
            spawnFadeTween?.Kill();

            if (!Application.isPlaying || duration <= 0f)
            {
                SetRenderersAlpha(1f);
                return null;
            }

            SetRenderersAlpha(0f);
            spawnFadeTween = DOVirtual.Float(0f, 1f, duration, SetRenderersAlpha)
                .SetEase(ease)
                .SetDelay(delay)
                .SetLink(gameObject);
            return spawnFadeTween;
        }

        public void StopSpawnFade()
        {
            spawnFadeTween?.Kill();
            spawnFadeTween = null;
            SetRenderersAlpha(1f);
        }

        /// <summary>Fades the bound out, then hides and invokes <paramref name="onComplete"/>.</summary>
        public void PlayDissolve(float duration, Ease ease, Action onComplete)
        {
            StopSpawnFade();
            dissolveTween?.Kill();

            if (!Application.isPlaying || duration <= 0f)
            {
                Hide();
                onComplete?.Invoke();
                return;
            }

            dissolveTween = DOVirtual.Float(1f, 0f, duration, SetRenderersAlpha)
                .SetEase(ease)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    Hide();
                    onComplete?.Invoke();
                });
        }

        private void SetRenderersAlpha(float alpha)
        {
            SpriteRenderer[] renderers = { bounderRenderer, spriteRenderer, lineSpriteLeft, lineSpriteRight, liftBgRenderer };
            foreach (SpriteRenderer renderer in renderers)
            {
                if (!renderer)
                    continue;
                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
        }

        private void InitMaskSize()
        {
            if (!maskTransform)
                return;

            Vector3 scale = maskTransform.localScale;
            scale.x = 0;
            scale.y = boundsSize.y;
            maskTransform.localScale = scale;
        }

        private void SetPositionFromBounds(BlockGroupOccupiedBounds bounds)
        {
            Transform parent = transform.parent;
            float heightOffset = config ? config.VisualHeightOffset : 0.6f;
            float y = heightOffset <= 0f ? transform.localPosition.y : heightOffset;

            if (parent)
            {
                Vector3 localCenter = parent.InverseTransformPoint(bounds.WorldCenter);
                transform.localPosition = new Vector3(localCenter.x, y, localCenter.z);
            }
            else
            {
                transform.position = bounds.WorldCenter + Vector3.up * y;
            }
        }

        private void OnDestroy()
        {
            spawnFadeTween?.Kill();
            spawnFadeTween = null;
            dissolveTween?.Kill();
            dissolveTween = null;
            openTween?.Kill();
            openTween = null;
        }
    }
}
