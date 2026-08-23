using DG.Tweening;
using WaterFlow.Core;
using UnityEngine;
using Ease = DG.Tweening.Ease;
using Tween = DG.Tweening.Tween;

namespace WaterFlow.Game
{
    public class ColorInteractableObjectBehavior : InteractableObjectBehavior
    {
        [SerializeField] ObstacleColorCustomConfig overrideColorData;
        [SerializeField] MeshRenderer meshRenderer;
        [SerializeField] protected GameObject modelParent;
        [SerializeField] protected Collider objectCollider;
        
        [Space] 
        [Slider(0.0f, 1.0f)] [SerializeField] float defaultAlpha = 0.9f;
        [Slider(0.0f, 1.0f)] [SerializeField] float activeAlpha = 0.3f;
        
        [Space]
        [SerializeField] protected float defaultY = 1f;
        [SerializeField] float activeY = 0f;
        [SerializeField] float tweenDuration = 0.2f;
        [SerializeField] Ease tweenEase = Ease.OutQuad;

        private MaterialPropertyBlock propertyBlock;
        
        private Color defaultColor;
        private float currentAlpha;
        private Tween alphaTween;
        private Tween modelMoveTween;
        private LevelBlockBehavior listenedBlock;
        protected bool isHide = false;
        public override void OnCreated()
        {
            Material instanceMaterial = null;
            if (overrideColorData != null &&
                overrideColorData.TryGetOverride(data.ObstacleColor, out ColorOverrideConfig overrideEntry))
                instanceMaterial = overrideEntry.Material;
            meshRenderer.sharedMaterial = instanceMaterial;
            if (meshRenderer.sharedMaterial) defaultColor = meshRenderer.sharedMaterial.color;
            propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ShaderId.COLOR_SHADER_ID, defaultColor);
            meshRenderer.SetPropertyBlock(propertyBlock);

            currentAlpha = defaultAlpha;
            SetAlpha(defaultAlpha);
            SetModelParentY(defaultY);

            ApplyInitialOverlapIfAny();
        }

        private void ApplyInitialOverlapIfAny()
        {
            if (OwnerLevel?.ActiveBlocks == null) return;

            foreach (LevelBlockBehavior block in OwnerLevel.ActiveBlocks)
            {
                if (!block || !block.IsOverlapsCell(position)) continue;

                SetUnderBlockCover(true, immediate: !Application.isPlaying);
                StartListenBlockLifecycle(block);
                return;
            }
        }

        private void SetUnderBlockCover(bool covered, bool immediate = false)
        {
            float targetAlpha = covered ? activeAlpha : defaultAlpha;
            float targetY = covered ? activeY : defaultY;

            if (immediate)
            {
                alphaTween?.Kill();
                alphaTween = null;
                modelMoveTween?.Kill();
                modelMoveTween = null;
                currentAlpha = targetAlpha;
                SetAlpha(targetAlpha);
                SetModelParentY(targetY);
            }
            else
            {
                TweenAlpha(targetAlpha);
                TweenModelParentY(targetY);
            }

            if (objectCollider)
                objectCollider.enabled = !covered;
            isHide = covered;
        }

        public override void OnBlockPicked(LevelBlockBehavior levelBlockBehavior)
        {
            // A dual-color block hides this obstacle when either of its colors matches; a normal
            // block matches on its single active color. MatchesActiveOrSecondaryColor covers both.
            if (isHide || !levelBlockBehavior.MatchesActiveOrSecondaryColor(data.ObstacleColor)) return;
            SetUnderBlockCover(true);
        }

        public override void OnBlockReleased(LevelBlockBehavior levelBlockBehavior, Vector2Int snapTargetPosition)
        {
            if (!isHide || !levelBlockBehavior ) return;
            if (listenedBlock && listenedBlock.IsOverlapsCell(position))
            {
                return;
            }

            if (levelBlockBehavior.IsOverlapsCell(position, snapTargetPosition))
            {
                StartListenBlockLifecycle(levelBlockBehavior);
                return;
            }

            LevelBlockBehavior coveringBlock = FindCoveringBlock();
            if (coveringBlock)
            {
                StartListenBlockLifecycle(coveringBlock);
                return;
            }

            StopListenBlockLifecycle();
            ResetState();

        }

        private LevelBlockBehavior FindCoveringBlock()
        {
            if (OwnerLevel?.ActiveBlocks == null) return null;

            foreach (LevelBlockBehavior block in OwnerLevel.ActiveBlocks)
            {
                if (!block) continue;
                if (block.IsOverlapsCell(position)) return block;
            }

            return null;
        }

        
        private void ResetState()
        {
            SetUnderBlockCover(false, immediate: !Application.isPlaying);
        }

        private void SetAlpha(float alpha)
        {
            Color color = defaultColor;
            color.a = alpha;
            propertyBlock.SetColor(ShaderId.COLOR_SHADER_ID, color);
            if (meshRenderer)
            {
                meshRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void SetModelParentY(float y)
        {
            if (!modelParent) return;

            Vector3 localPosition = modelParent.transform.localPosition;
            localPosition.y = y;
            modelParent.transform.localPosition = localPosition;
        }

        private void TweenAlpha(float targetAlpha)
        {
            alphaTween?.Kill();
            alphaTween = DOTween.To(() => currentAlpha, value =>
                {
                    currentAlpha = value;
                    SetAlpha(value);
                }, targetAlpha, tweenDuration)
                .SetEase(tweenEase);
        }

        private void TweenModelParentY(float targetY)
        {
            if (!modelParent) return;

            modelMoveTween?.Kill();
            modelMoveTween = modelParent.transform
                .DOLocalMoveY(targetY, tweenDuration)
                .SetEase(tweenEase);
        }

        private void OnDisable()
        {
            alphaTween?.Kill();
            modelMoveTween?.Kill();
        }

        private void OnDestroy()
        {
            StopListenBlockLifecycle();
        }

        private void StartListenBlockLifecycle(LevelBlockBehavior levelBlockBehavior)
        {
            if (listenedBlock == levelBlockBehavior)
                return;

            StopListenBlockLifecycle();
            listenedBlock = levelBlockBehavior;
            listenedBlock.DestroyBlockCompleted += OnListenedBlockResolved;
        }

        private void StopListenBlockLifecycle()
        {
            if (!listenedBlock)
                return;

            listenedBlock.DestroyBlockCompleted -= OnListenedBlockResolved;
            listenedBlock = null;
        }

        private void OnListenedBlockResolved()
        {
            StopListenBlockLifecycle();
            ResetState();
        }

    }
}