using System;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Core;
using UnityEngine;
using Ease = DG.Tweening.Ease;
using Tween = DG.Tweening.Tween;

namespace WaterFlow.Game
{
    public abstract class BaseContainerBoxGroupVisual : MonoBehaviour, IBlockGroupVisual, IClickableObject
    {
        [Header("Shared Art")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected SpriteRenderer shadowRenderer;
        [SerializeField] protected SpriteRenderer woodLineRenderer;

        [Header("Clear VFX")]
        [SerializeField] protected GameObject vfxPrefab3x3;
        [SerializeField] protected GameObject vfxPrefab2x2;

#if UNITY_EDITOR
        [Header("Editor Preview")]
        [SerializeField] protected Vector2Int editorPreviewCellCount = new(4, 2);
#endif

        protected float localHeightOffset = 0.6f;
        private float visualAlpha = 1f;
        protected ClearAnimationSettings clearSettings = new();

        private readonly List<SpriteRenderer> woodLineInstances = new();
        private BoxCollider clickCollider;
        private float clickColliderDepth = 0.2f;
        private Sequence clearSequence;
        private Vector3 clearAnimationBaseLocalPosition;
        private bool hasClearAnimationBaseLocalPosition;
        private BaseContainerBoxVisualConfig baseVisualConfig;
        private IGroupClickReceiver clickReceiver;
        
        public virtual void Configure(BaseContainerBoxEffectConfig config, IGroupClickReceiver clickReceiver)
        {
            if (!config)
                return;
            gameObject.SetActive(true);
            baseVisualConfig = config.VisualConfig;
            this.clickReceiver = clickReceiver;
            if (baseVisualConfig == null)
            {
                Debug.LogError($"Require ContainerBoxVisualConfig in {config.GetType().Name}");
                return;
            }
            visualAlpha = baseVisualConfig.ShowFullAlpha ? 1f : 0.3f;
            localHeightOffset = baseVisualConfig.LocalHeightOffset;
            clearSettings = baseVisualConfig.ClearAnimation;
        }
        public virtual void Apply(BlockGroupOccupiedBounds bounds)
        {
            if (!bounds.IsValid || !spriteRenderer)
                return;

            SetPositionFromBounds(bounds);
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = new Vector2(
                bounds.CellCount.x,
                bounds.CellCount.y);
            ApplyShadowVisual(bounds);
            ApplyWoodLineVisual(bounds);
            ApplyClickCollider(bounds);
            ApplySpriteAlpha();
            gameObject.SetActive(true);
        }
        
        public virtual void Hide()
        {
            KillClearSequence();
            gameObject.SetActive(false);
            clickReceiver = null;
        }

        public void Show() => gameObject.SetActive(true);

        protected virtual void Awake()
        {
            if (localHeightOffset <= 0f)
                localHeightOffset = transform.localPosition.y;

            CacheWoodLineTemplateIfNeeded();
            CacheClickColliderIfNeeded();
        }

        public void OnObjectClicked()
        {
            clickReceiver?.OnClicked();
        }

        public bool CanBeClicked()
        {
            return clickReceiver != null && clickReceiver.CanClick();
        }

        public void OnClickBlocked()
        {
            clickReceiver?.OnBlocked();
        }

        protected void SetPositionFromBounds(BlockGroupOccupiedBounds bounds)
        {
            Transform parent = transform.parent;
            float y = localHeightOffset <= 0f ? transform.localPosition.y : localHeightOffset;
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

        protected void ApplyShadowVisual(BlockGroupOccupiedBounds bounds)
        {
            if (!shadowRenderer)
                return;

            shadowRenderer.drawMode = SpriteDrawMode.Sliced;
            Vector2 size = shadowRenderer.size;
            size.x = Mathf.Max(0f, bounds.CellCount.x + baseVisualConfig.ShadowWidthPadding);
            shadowRenderer.size = size;

            Transform t = shadowRenderer.transform;
            Vector3 localPos = t.localPosition;
            localPos.y = (-bounds.CellCount.y * 0.5f) +  baseVisualConfig.ShadowBottomPadding;
            t.localPosition = localPos;
        }

        protected void ApplyWoodLineVisual(BlockGroupOccupiedBounds bounds)
        {
            if (!woodLineRenderer)
                return;

            if (!Application.isPlaying && !baseVisualConfig.ShowFullAlpha)
            {
                woodLineRenderer.enabled = false;
                for (int i = 0; i < woodLineInstances.Count; i++)
                {
                    SpriteRenderer r = woodLineInstances[i];
                    if (!r) continue;
                    r.enabled = false;
                }
                return;
            }

            CacheWoodLineTemplateIfNeeded();

            Vector3 scale = woodLineRenderer.transform.localScale;
            float scaleX = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Abs(scale.x);

            int cellsX = Mathf.Max(1, bounds.CellCount.x);
            int cellsY = Mathf.Max(1, bounds.CellCount.y);

            float targetWidth = baseVisualConfig.LineBaseWidth + baseVisualConfig.LineWidthPerExtraCell * (cellsX - 1);
            int lineCount = Mathf.Max(1, (cellsY * 2) - 1);
            EnsureWoodLineInstances(lineCount);

            float totalHeight = cellsY;
            float pad = Mathf.Max(0f, baseVisualConfig.LineVerticalPadding);
            float minY = (-totalHeight * 0.5f) + pad;
            float maxY = (totalHeight * 0.5f) - pad;
            float range = Mathf.Max(0f, maxY - minY);

            for (int i = 0; i < woodLineInstances.Count; i++)
            {
                SpriteRenderer r = woodLineInstances[i];
                if (!r) continue;

                r.enabled = i < lineCount;
                if (i >= lineCount) continue;

                r.drawMode = SpriteDrawMode.Sliced;
                r.size = new Vector2(targetWidth / scaleX, baseVisualConfig.LineLocalHeight);

                Transform t = r.transform;
                Vector3 lp = t.localPosition;
                float y = (lineCount <= 1) ? 0f : (minY + (range * i / (lineCount - 1)));
                lp.y = y;
                t.localPosition = lp;
            }
        }

        /// <summary>Current sprite alpha: full in play mode, <c>visualAlpha</c> for edit-mode preview.</summary>
        protected float CurrentVisualAlpha => Application.isPlaying ? 1f : visualAlpha;

        protected virtual void ApplySpriteAlpha()
        {
            float alpha = CurrentVisualAlpha;

            if (spriteRenderer)
            {
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }

            if (shadowRenderer)
            {
                Color color = shadowRenderer.color;
                color.a = alpha;
                shadowRenderer.color = color;
            }

            for (int i = 0; i < woodLineInstances.Count; i++)
            {
                SpriteRenderer r = woodLineInstances[i];
                if (!r) continue;
                Color color = r.color;
                color.a = alpha;
                r.color = color;
            }
        }

        protected void ApplyClickCollider(BlockGroupOccupiedBounds bounds)
        {
            CacheClickColliderIfNeeded();
            if (!clickCollider)
                return;

            Vector2Int cellCount = bounds.CellCount;
            clickCollider.center = Vector3.zero;
            clickCollider.size = new Vector3(
                cellCount.x,
                cellCount.y,
                clickColliderDepth);
            clickCollider.enabled = true;
        }

        protected void CacheWoodLineTemplateIfNeeded()
        {
            if (!woodLineRenderer)
                return;

            if (woodLineInstances.Count == 0)
                woodLineInstances.Add(woodLineRenderer);
        }

        protected void CacheClickColliderIfNeeded()
        {
            if (clickCollider)
                return;

            clickCollider = GetComponent<BoxCollider>();
            if (clickCollider)
                clickColliderDepth = clickCollider.size.z;
        }

        private void EnsureWoodLineInstances(int count)
        {
            CacheWoodLineTemplateIfNeeded();
            if (count <= 0)
                count = 1;

            Transform parent = woodLineRenderer.transform.parent;
            while (woodLineInstances.Count < count)
            {
                GameObject clone = Instantiate(woodLineRenderer.gameObject, parent);
                clone.name = $"{woodLineRenderer.gameObject.name}_{woodLineInstances.Count}";
                SpriteRenderer r = clone.GetComponent<SpriteRenderer>();
                woodLineInstances.Add(r);
            }
        }

        // ── Shared clear animation / VFX ───────────────────────────────────────

        public virtual void SpawnClearVfx(BlockGroupOccupiedBounds bounds)
        {
            if (!bounds.IsValid)
                return;

            int cellsX = Mathf.Max(1, bounds.CellCount.x);
            int cellsY = Mathf.Max(1, bounds.CellCount.y);
            int maxDim = Mathf.Max(cellsX, cellsY);

            GameObject prefab;
            int count;
            int chunkCells;

            if (maxDim >= 3)
            {
                prefab = vfxPrefab3x3;
                chunkCells = 3;
                count = Mathf.Max(1, maxDim / 3);
            }
            else
            {
                prefab = vfxPrefab2x2;
                chunkCells = 2;
                count = Mathf.Max(1, Mathf.CeilToInt((cellsX * cellsY) / 4f));
            }

            if (!prefab)
                return;

            Vector3 center = GetSpawnCenterFromBounds(bounds);
            bool spreadOnX = cellsX >= cellsY;
            float cellSizeX = bounds.WorldSize.x / cellsX;
            float cellSizeZ = bounds.WorldSize.z / cellsY;
            SpawnVfxInstances(prefab, count, center, spreadOnX, chunkCells, cellSizeX, cellSizeZ);
        }

        /// <summary>
        /// Plays a short pop then shrink + fade, spawns clear VFX at the pop peak, then invokes <paramref name="onComplete"/>.
        /// </summary>
        public virtual void PlayClearAnimation(BlockGroupOccupiedBounds bounds, Action onComplete)
        {
            KillClearSequence();

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                SpawnClearVfx(bounds);
                onComplete?.Invoke();
                return;
            }

            Transform t = transform;
            Vector3 baseScale = Vector3.one;
            t.localScale = baseScale;
            ApplyClearAnimationStartPosition(t);

            float punch = Mathf.Max(0f, clearSettings.PunchDuration);
            float collapse = Mathf.Max(0.01f, clearSettings.CollapseDuration);
            float adjustedPunchScale = ComputeAdjustedPunchScale(bounds);
            Vector3 punchScale = baseScale * (1f + adjustedPunchScale);
            Vector3 collapseScale = baseScale * Mathf.Max(0f, clearSettings.CollapseScale);

            clearSequence = DOTween.Sequence();

            if (punch > 0f)
                clearSequence.Append(t.DOScale(punchScale, punch).SetEase(clearSettings.PunchEase));

            clearSequence.AppendCallback(() => SpawnClearVfx(bounds));
            clearSequence.Append(t.DOScale(collapseScale, collapse).SetEase(clearSettings.CollapseEase));
            clearSequence.Join(FadeRenderers(0f, collapse));

            clearSequence.OnComplete(() =>
            {
                clearSequence = null;
                t.localScale = baseScale;
                RestoreClearAnimationBaseLocalPosition(t);
                onComplete?.Invoke();
            });

            clearSequence.OnKill(() => clearSequence = null);
        }

        protected void KillClearSequence()
        {
            if (clearSequence == null)
                return;

            clearSequence.Kill();
            clearSequence = null;
            RestoreClearAnimationBaseLocalPosition(transform);
        }

        private void ApplyClearAnimationStartPosition(Transform t)
        {
            clearAnimationBaseLocalPosition = t.localPosition;
            hasClearAnimationBaseLocalPosition = true;

            Vector3 punchLocalPosition = clearAnimationBaseLocalPosition;
            punchLocalPosition.y = clearSettings.PunchLocalY;
            punchLocalPosition.z += clearSettings.PunchLocalZOffset;
            t.localPosition = punchLocalPosition;
        }

        private void RestoreClearAnimationBaseLocalPosition(Transform t)
        {
            if (!hasClearAnimationBaseLocalPosition)
                return;

            t.localPosition = clearAnimationBaseLocalPosition;
            hasClearAnimationBaseLocalPosition = false;
        }

        protected virtual Sequence FadeRenderers(float targetAlpha, float duration)
        {
            Sequence fade = DOTween.Sequence();

            if (spriteRenderer)
                fade.Join(spriteRenderer.DOFade(targetAlpha, duration).SetEase(clearSettings.FadeEase));
            if (shadowRenderer)
                fade.Join(shadowRenderer.DOFade(targetAlpha, duration).SetEase(clearSettings.FadeEase));

            for (int i = 0; i < woodLineInstances.Count; i++)
            {
                SpriteRenderer r = woodLineInstances[i];
                if (r && r.enabled)
                    fade.Join(r.DOFade(targetAlpha, duration).SetEase(clearSettings.FadeEase));
            }

            return fade;
        }

        private float ComputeAdjustedPunchScale(BlockGroupOccupiedBounds bounds)
        {
            int cellsX = Mathf.Max(1, bounds.CellCount.x);
            int cellsY = Mathf.Max(1, bounds.CellCount.y);
            int extraCells = Mathf.Max(0, (cellsX * cellsY) - 1);

            float decayPerExtraCell = Mathf.Max(0f, clearSettings.PunchScaleDecayPerExtraCell);
            float minMultiplier = Mathf.Clamp01(clearSettings.PunchScaleMinMultiplier);
            float sizeMultiplier = 1f / (1f + (extraCells * decayPerExtraCell));
            sizeMultiplier = Mathf.Max(minMultiplier, sizeMultiplier);

            return Mathf.Max(0f, clearSettings.PunchScale) * sizeMultiplier;
        }

        private Vector3 GetSpawnCenterFromBounds(BlockGroupOccupiedBounds bounds)
        {
            float yOffset = clearSettings.VfxHeightOffset > 0f ? clearSettings.VfxHeightOffset : localHeightOffset;

            Transform parent = transform.parent;
            if (parent)
            {
                Vector3 localCenter = parent.InverseTransformPoint(bounds.WorldCenter);
                float y = yOffset <= 0f ? transform.localPosition.y : yOffset;
                return parent.TransformPoint(new Vector3(localCenter.x, y, localCenter.z));
            }

            float worldY = yOffset <= 0f ? transform.position.y : yOffset;
            return new Vector3(bounds.WorldCenter.x, worldY, bounds.WorldCenter.z);
        }

        private void SpawnVfxInstances(
            GameObject prefab,
            int count,
            Vector3 center,
            bool spreadOnX,
            int chunkCells,
            float cellSizeX,
            float cellSizeZ)
        {
            float chunkSpan = chunkCells * (spreadOnX ? cellSizeX : cellSizeZ);
            float totalSpan = count * chunkSpan;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = center;
                if (count > 1)
                {
                    float offset = (i + 0.5f) * chunkSpan - totalSpan * 0.5f;
                    if (spreadOnX)
                        position.x += offset;
                    else
                        position.z += offset;
                }

                SpawnSingleClearVfx(prefab, position);
            }
        }

        private void SpawnSingleClearVfx(GameObject prefab, Vector3 position)
        {
            GameObject vfxInstance = Instantiate(prefab, position, Quaternion.identity);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(vfxInstance, "Spawn Clear VFX");
                ParticleSystem editorPs = vfxInstance.GetComponentInChildren<ParticleSystem>(true);
                if (editorPs)
                    editorPs.Play(true);
                return;
            }
#endif

            ParticleSystem ps = vfxInstance.GetComponentInChildren<ParticleSystem>(true);
            if (ps)
            {
                ps.PlayCase().Disabled += () =>
                {
                    if (vfxInstance)
                        Destroy(vfxInstance);
                };
            }
            else
            {
                Destroy(vfxInstance, 2f);
            }
        }

        // ── Lifecycle (variants extend via override + base call) ───────────────

        protected virtual void OnDisable()
        {
            KillClearSequence();
            transform.localScale = Vector3.one;
        }

        protected virtual void OnDestroy()
        {
            KillClearSequence();
        }

#if UNITY_EDITOR
        protected virtual void Reset()
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (!shadowRenderer)
                shadowRenderer = transform.parent ? transform.parent.GetComponentInChildren<SpriteRenderer>() : null;

            // Keep defaulting to prefab position; runtime config overrides via Configure().
            localHeightOffset = transform.localPosition.y;
        }

        [Sirenix.OdinInspector.Button("Spawn Clear VFX", Sirenix.OdinInspector.ButtonSizes.Medium)]
        private void EditorSpawnClearVfx()
        {
            if (!TryCreateEditorPreviewBounds(out BlockGroupOccupiedBounds bounds))
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] Cannot preview VFX: assign vfx prefabs and set editorPreviewCellCount or sprite size.",
                    this);
                return;
            }

            SpawnClearVfx(bounds);
        }

        [Sirenix.OdinInspector.Button("Test Clear Animation", Sirenix.OdinInspector.ButtonSizes.Medium)]
        [Sirenix.OdinInspector.InfoBox(
            "Enter Play Mode to preview (DOTween tweens don't run in Edit Mode).",
            Sirenix.OdinInspector.InfoMessageType.Info,
            "@!UnityEngine.Application.isPlaying")]
        private void EditorTestClearAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{GetType().Name}] Enter Play Mode to test the clear animation.", this);
                return;
            }

            if (!TryCreateEditorPreviewBounds(out BlockGroupOccupiedBounds bounds))
            {
                Debug.LogWarning($"[{GetType().Name}] Cannot preview: set editorPreviewCellCount or sprite size.", this);
                return;
            }

            Show();
            transform.localScale = Vector3.one;
            ApplySpriteAlpha();
            PlayClearAnimation(bounds, () =>
                Debug.Log($"[{GetType().Name}] Clear animation finished.", this));
        }

        private bool TryCreateEditorPreviewBounds(out BlockGroupOccupiedBounds bounds)
        {
            bounds = default;

            Vector2Int cellCount = editorPreviewCellCount;
            if (spriteRenderer)
            {
                int cellsX = Mathf.Max(1, Mathf.RoundToInt(spriteRenderer.size.x));
                int cellsY = Mathf.Max(1, Mathf.RoundToInt(spriteRenderer.size.y));
                cellCount = new Vector2Int(cellsX, cellsY);
            }

            if (cellCount.x < 1 || cellCount.y < 1)
                return false;

            Vector3 cellSize = new(1f, 0.5f, 1f);
            Vector3 worldSize = new(
                cellCount.x * cellSize.x,
                cellSize.y,
                cellCount.y * cellSize.z);
            var worldBounds = new Bounds(transform.position, worldSize);
            bounds = new BlockGroupOccupiedBounds(
                worldBounds,
                Vector2Int.zero,
                cellCount - Vector2Int.one);
            return true;
        }
#endif

        // ── Static obstacle-overlap helpers ────────────────────────────────────

        /// <summary>
        /// Restores obstacle <see cref="GameObject"/>s disabled by <see cref="SetBoundsOverlapObstaclesActive"/>.
        /// Blocks inside the box are hidden via <see cref="LevelBlockBehavior.SetVisible"/> instead.
        /// </summary>
        public static void RestoreBoundsOverlapObstacles(HashSet<GameObject> disabledObstacles)
        {
            if (disabledObstacles == null || disabledObstacles.Count == 0)
                return;

            foreach (GameObject go in disabledObstacles)
            {
                if (go)
                    go.SetActive(true);
            }

            disabledObstacles.Clear();
        }

        /// <summary>
        /// Disables spawned obstacle <see cref="GameObject"/>s inside <paramref name="bounds"/>.
        /// </summary>
        public static void SetBoundsOverlapObstaclesActive(
            BlockGroupOccupiedBounds bounds,
            IElementProvider elementProvider,
            HashSet<GameObject> disabledObstacles,
            bool hide)
        {
            RestoreBoundsOverlapObstacles(disabledObstacles);

            if (!hide || !bounds.IsValid || elementProvider == null || disabledObstacles == null)
                return;

            IReadOnlyList<ObstacleBehavior> obstacles =
                elementProvider.GetElements<ObstacleBehavior>(ElementType.Obstacle);
            for (int i = 0; i < obstacles.Count; i++)
            {
                ObstacleBehavior obstacle = obstacles[i];
                if (!obstacle || !CellInsideBounds(obstacle.Position, bounds))
                    continue;

                TryDisableObstacleGameObject(obstacle.gameObject, disabledObstacles);
            }
        }

        private static bool CellInsideBounds(Vector2Int cell, BlockGroupOccupiedBounds bounds) =>
            cell.x >= bounds.MinCell.x && cell.x <= bounds.MaxCell.x &&
            cell.y >= bounds.MinCell.y && cell.y <= bounds.MaxCell.y;

        private static void TryDisableObstacleGameObject(GameObject go, HashSet<GameObject> disabledObstacles)
        {
            if (!go || !go.activeSelf)
                return;

            disabledObstacles.Add(go);
            go.SetActive(false);
        }

    }
}
