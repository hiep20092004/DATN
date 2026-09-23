using System;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using UnityEngine;
using Ease = DG.Tweening.Ease;

namespace WaterFlow.Game
{
    public sealed class LiftBehavior : ExtraLayerHandlerBehavior, IWatchedRegionThresholdListener
    {
        [SerializeField] private LiftConfig config;
        [SerializeField] private LiftGroupVisual groupVisual;

        private LevelRepresentation level;
        private ILevelContentProvider contentProvider;
        private IBlockCellWatchService watchService;
        private Func<bool> isLevelStarted;

        private readonly List<BlockLevelElementData> liftBlocks = new();
        private readonly List<LevelBlockBehavior> spawnedBlocks = new();
        private readonly List<Vector2Int> watchedCells = new();
        private Vector3 liftRiseOriginLocal;
        private bool hasLiftRiseOrigin;
        private bool isRegistered;
        private bool hasSpawned;
        private bool isPendingSpawn;
        private int pendingRiseCompletions;

        public IReadOnlyList<Vector2Int> WatchedCells => watchedCells;
        public override bool HasSpawned => hasSpawned;

        public override bool Init(
            LevelRepresentation level,
            ILevelContentProvider contentProvider,
            IBlockCellWatchService watchService,
            IReadOnlyList<LevelElementData> extraLayerElements,
            Func<bool> isLevelStarted)
        {
            this.level = level;
            this.contentProvider = contentProvider;
            this.watchService = watchService;
            this.isLevelStarted = isLevelStarted;

            CollectLiftBlocks(extraLayerElements);
            if (liftBlocks.Count == 0)
                return false;

            BuildWatchedCells();
            if (watchedCells.Count == 0)
                return false;

            if (!groupVisual)
                groupVisual = GetComponentInChildren<LiftGroupVisual>(true);

            if (groupVisual && config &&
                BlockGroupBoundsCalculator.TryCalculate(watchedCells, config.LiftBaseY, out BlockGroupOccupiedBounds bounds))
            {
                groupVisual.Configure(config);
                groupVisual.Apply(bounds);
                CacheLiftRiseOrigin(bounds);
                PlayGroupVisualSpawnTween();
            }

            if (!Application.isPlaying)
                return true;

            if (watchService != null)
            {
                watchService.Register(this);
                isRegistered = true;

                if (watchService.GetOccupiedCount(watchedCells) == 0)
                    TrySpawnLift();
                else
                    RegisterPendingSpawn();
            }

            return true;
        }

        private void CollectLiftBlocks(IReadOnlyList<LevelElementData> extraLayerElements)
        {
            liftBlocks.Clear();
            if (extraLayerElements == null)
                return;

            for (int i = 0; i < extraLayerElements.Count; i++)
            {
                if (extraLayerElements[i] is BlockLevelElementData block)
                    liftBlocks.Add(block);
            }
        }

        private void BuildWatchedCells()
        {
            watchedCells.Clear();

            bool hasAny = false;
            Vector2Int minCell = default;
            Vector2Int maxCell = default;

            foreach (BlockLevelElementData block in liftBlocks)
            {
                LevelFigure figure = contentProvider?.GetBlockData(block.BlockType)?.Figure;
                if (figure?.Points == null)
                    continue;

                // Mirror LevelBlockBehavior.GetOccupiedCells: origin = pivot world cell - pivot.
                Vector2Int origin = block.Position - figure.PivotPoint;
                Vector2Int figureSize = figure.Size;
                PointData[] points = figure.Points;

                for (int y = 0; y < figureSize.y; y++)
                {
                    for (int x = 0; x < figureSize.x; x++)
                    {
                        int index = x + y * figureSize.x;
                        if (index < 0 || index >= points.Length || !points[index].IsFilled)
                            continue;

                        Vector2Int cell = new(origin.x + x, origin.y + y);

                        if (!hasAny)
                        {
                            minCell = maxCell = cell;
                            hasAny = true;
                        }
                        else
                        {
                            minCell = Vector2Int.Min(minCell, cell);
                            maxCell = Vector2Int.Max(maxCell, cell);
                        }
                    }
                }
            }

            if (!hasAny)
                return;

            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                    watchedCells.Add(new Vector2Int(x, y));
            }
        }

        private void TrySpawnLift()
        {
            if (hasSpawned)
                return;

            if (watchService != null && !IsWatchedRegionEmpty())
                return;

            hasSpawned = true;
            Unregister();
            RegisterPendingSpawn();

            if (Application.isPlaying)
                Services.AudioService.PlaySound(AudioId.Obstacle_Lift);

            spawnedBlocks.Clear();
            List<LevelBlockBehavior> spawned = spawnedBlocks;
            foreach (BlockLevelElementData block in liftBlocks)
            {
                LevelBlockBehavior spawnedBlock = level.SpawnBlock(
                    block.Position, block.BlockType, block.BlockColor,
                    block.BlockEffects, block.BlockId, allowDelay: false, skipSpawnTween: true);

                if (spawnedBlock)
                    spawned.Add(spawnedBlock);
            }

            if (spawned.Count == 0)
            {
                UnregisterPendingSpawn();
                DissolveAndDestroy();
                return;
            }

            foreach (LevelBlockBehavior block in spawned)
            {
                foreach (BlockEffectBehavior effect in block.Effects)
                    effect.OnCreatedAllBlock();
            }
            Physics.SyncTransforms();

            bool levelStarted = isLevelStarted != null && isLevelStarted();
            float depth = config ? config.RiseFromDepth : 0f;
            float startScale = config ? config.RiseFromScale : 1f;
            float startDelay = config ? config.RiseBlockStartDelay : 0f;
            float duration = config ? config.RiseDuration : 0f;

            pendingRiseCompletions = spawned.Count;

            bool animatesRise = Application.isPlaying && duration > 0f && (depth > 0f || startScale < 0.999f);
            if (animatesRise)
            {
                for (int i = 0; i < spawned.Count; i++)
                    spawned[i].BeginInteractionTween();
            }

            // Slide the doors open; block rise runs in parallel underneath the growing reveal mask.
            float openDuration = config ? config.DoorOpenDuration : 0f;
            Ease openEase = config ? config.DoorOpenEase : Ease.OutCubic;
            groupVisual?.PlayOpenDoor(openDuration, openEase);

            foreach (LevelBlockBehavior block in spawned)
                AnimateRise(block, depth, startScale, startDelay, duration, levelStarted, animatesRise);

            if (Application.isPlaying)
                DOVirtual.DelayedCall(openDuration, DissolveAndDestroy).SetLink(gameObject);
            else
                DissolveAndDestroy();
        }

        private void CacheLiftRiseOrigin(BlockGroupOccupiedBounds bounds)
        {
            if (!bounds.IsValid)
            {
                hasLiftRiseOrigin = false;
                return;
            }

            // Match block local grid coords (see LevelRepresentation.SpawnBlock).
            liftRiseOriginLocal = new Vector3(
                (bounds.MinCell.x + bounds.MaxCell.x) * 0.5f,
                0f,
                (bounds.MinCell.y + bounds.MaxCell.y) * 0.5f);
            hasLiftRiseOrigin = true;
        }

        private void AnimateRise(
            LevelBlockBehavior block, float depth, float startScaleFactor, float startDelay, float duration,
            bool levelStarted, bool beganInteractionTween)
        {
            void Finalize()
            {
                if (block)
                {
                    if (beganInteractionTween)
                        block.EndInteractionTween();
                    block.OnMapSpawnCompleted();
                    if (levelStarted)
                    {
                        foreach (BlockEffectBehavior effect in block.Effects)
                            effect.OnLevelActivated();
                    }
                }

                NotifyRiseComplete();
            }

            bool animatesScale = startScaleFactor < 0.999f;
            if (!Application.isPlaying || duration <= 0f || (depth <= 0f && !animatesScale))
            {
                Finalize();
                return;
            }

            Vector3 finalLocal = block.transform.localPosition;
            Vector3 finalScale = block.transform.localScale;
            Vector3 startLocal = hasLiftRiseOrigin
                ? new Vector3(liftRiseOriginLocal.x, finalLocal.y - depth, liftRiseOriginLocal.z)
                : finalLocal + Vector3.down * depth;
            Vector3 startScale = finalScale * startScaleFactor;

            block.transform.localPosition = startLocal;
            block.transform.localScale = startScale;

            Tween moveTween = block.transform.DOLocalMove(finalLocal, duration).SetEase(config.RiseEase);
            Sequence riseSequence = DOTween.Sequence()
                .SetLink(block.gameObject)
                .OnComplete(Finalize);

            if (startDelay > 0f)
                riseSequence.AppendInterval(startDelay);

            riseSequence.Append(moveTween);

            if (animatesScale)
                riseSequence.Join(block.transform.DOScale(finalScale, duration).SetEase(config.RiseEase));
        }

        private void NotifyRiseComplete()
        {
            pendingRiseCompletions--;
            if (pendingRiseCompletions > 0)
                return;

            UnregisterPendingSpawn();
        }

        private void PlayGroupVisualSpawnTween()
        {
            EnvironmentData.SpawnTweenConfig tweenConfig = level?.EnvironmentData?.LiftGroupVisualSpawnTween;
            if (!groupVisual || tweenConfig == null || !Application.isPlaying || !tweenConfig.Enabled)
                return;

            groupVisual.PlayFadeIn(tweenConfig.Duration, tweenConfig.Ease, tweenConfig.Delay);
        }

        private void StopGroupVisualSpawnTween()
        {
            if (groupVisual)
                groupVisual.StopSpawnFade();
        }

        private void DissolveAndDestroy()
        {
            StopGroupVisualSpawnTween();

            if (groupVisual)
            {
                groupVisual.PlayDissolve(
                    config ? config.DissolveDuration : 0f,
                    config ? config.DissolveEase : Ease.InBack,
                    () => { if (this) Destroy(gameObject); });
                return;
            }

            Destroy(gameObject);
        }

        private bool IsWatchedRegionEmpty()
        {
            return watchService.GetOccupiedCount(watchedCells) == 0
                && watchService.GetRegisteredOccupiedCount(watchedCells) == 0;
        }

        private void Unregister()
        {
            if (isRegistered && watchService != null)
                watchService.Unregister(this);
            isRegistered = false;
        }

        private void RegisterPendingSpawn()
        {
            if (isPendingSpawn || level == null)
                return;

            level.RegisterPendingBlockSpawn();
            isPendingSpawn = true;
        }

        private void UnregisterPendingSpawn()
        {
            if (!isPendingSpawn || level == null)
                return;

            level.UnregisterPendingBlockSpawn();
            isPendingSpawn = false;
        }

        private void OnDisable()
        {
            StopGroupVisualSpawnTween();
            UnregisterPendingSpawn();
            Unregister();
        }

        // ── IWatchedRegionThresholdListener ───────────────────────────────────────
        public void OnAnyWatchedCellOccupied() { }

        // Primary trigger: fires when the last occupied watched cell becomes empty.
        public void OnAllWatchedCellsVacated()
        {
            TrySpawnLift();
        }
    }
}
