using DG.Tweening;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class ContainerMoveBoxEffectVisual : BaseContainerBoxGroupVisual
    {
        [Header("Inner")]
        [SerializeField] private SpriteRenderer innerRenderer;
        [Tooltip("Inner size per axis = bounds cell count + this offset. e.g. 1x1 + (-0.18) = 0.82, 2x2 = 1.82.")]
        [SerializeField] private Vector2 innerSizeOffset = new(-0.18f, -0.18f);

        public override void Apply(BlockGroupOccupiedBounds bounds)
        {
            base.Apply(bounds);
            ApplyInnerVisual(bounds);
        }

        protected override void ApplySpriteAlpha()
        {
            base.ApplySpriteAlpha();

            if (!innerRenderer)
                return;

            Color color = innerRenderer.color;
            color.a = CurrentVisualAlpha;
            innerRenderer.color = color;
        }

        protected override Sequence FadeRenderers(float targetAlpha, float duration)
        {
            Sequence fade = base.FadeRenderers(targetAlpha, duration);

            if (innerRenderer)
                fade.Join(innerRenderer.DOFade(targetAlpha, duration).SetEase(clearSettings.FadeEase));

            return fade;
        }

        private void ApplyInnerVisual(BlockGroupOccupiedBounds bounds)
        {
            if (!innerRenderer || !bounds.IsValid)
                return;

            innerRenderer.size = new Vector2(
                Mathf.Max(0f, bounds.CellCount.x + innerSizeOffset.x),
                Mathf.Max(0f, bounds.CellCount.y + innerSizeOffset.y));
        }
    }
}
