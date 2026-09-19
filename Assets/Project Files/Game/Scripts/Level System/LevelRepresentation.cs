using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WaterFlow.Game
{
    public class LevelRepresentation : IElementProvider
    {
        public LevelData LevelData { get; private set; }
        public Vector2Int Size => LevelData.Size;
        public LevelElementData[] LevelElements { get; private set; }

        public Transform LevelTransform { get; private set; }
        public EnvironmentData EnvironmentData { get; private set; }
        public LevelEnvironmentSpawner EnvironmentSpawner { get; private set; }
        public bool SpawnComplete { get; private set; }
        public bool HasPendingBlockSpawn => pendingBlockSpawnCount > 0;

        public LevelElementData[,] LevelMatrix { get; private set; }
        public List<LevelBlockBehavior> ActiveBlocks { get; private set; }
        IReadOnlyList<LevelBlockBehavior> IElementProvider.ActiveBlocks => ActiveBlocks;

        public IReadOnlyList<T> GetElements<T>(ElementType elementType) where T : class
        {
            switch (elementType)
            {
                case ElementType.Block:
                    if (typeof(T) == typeof(LevelBlockBehavior))
                    {
                        IReadOnlyList<LevelBlockBehavior> blocks = ActiveBlocks == null
                            ? Array.Empty<LevelBlockBehavior>()
                            : ActiveBlocks;
                        return (IReadOnlyList<T>)(object)blocks;
                    }
                    break;
                case ElementType.Gate:
                    if (typeof(T) == typeof(GateBehavior) && EnvironmentSpawner != null)
                        return (IReadOnlyList<T>)(object)EnvironmentSpawner.Gates;
                    break;
                case ElementType.Generator:
                    if (typeof(T) == typeof(GeneratorBehavior) && EnvironmentSpawner != null)
                        return (IReadOnlyList<T>)(object)EnvironmentSpawner.Generators;
                    break;
                case ElementType.InteractableObject:
                    if (typeof(T) == typeof(InteractableObjectBehavior) && EnvironmentSpawner != null)
                        return (IReadOnlyList<T>)(object)EnvironmentSpawner.InteractableObjects;
                    break;
                case ElementType.Obstacle:
                    if (typeof(T) == typeof(ObstacleBehavior) && EnvironmentSpawner != null)
                        return (IReadOnlyList<T>)(object)EnvironmentSpawner.Obstacles;
                    break;
            }

            return Array.Empty<T>();
        }

        public Bounds LevelBounds => EnvironmentSpawner.GetBounds();

        public event Action<LevelRepresentation> LevelCompleted;
        public event Action<LevelRepresentation, LoseReason, float> LevelFailed;
        public event Action<LevelRepresentation> SpawnStarted;
        public event Action<LevelRepresentation> SpawnCompleted;

        private ILevelContentProvider contentProvider;
        private CameraController cameraController;
        private Tween spawnDelayedCall;
        private IWinCondition winCondition;
        private bool levelCompletedFired;
        private int pendingBlockSpawnCount;

        public LevelRepresentation(LevelData level, EnvironmentData environmentData,
            CameraController cameraController, ILevelContentProvider contentProvider,
            string levelObjectName = "[LEVEL]", LevelElementData[] elementsOverride = null)
        {
            this.contentProvider = contentProvider;
            EnvironmentData = environmentData;
            LevelData = level;
            this.cameraController = cameraController;

            GameObject levelObject = new GameObject(levelObjectName);
            LevelTransform = levelObject.transform;

            Vector2Int levelSize = Size;
            LevelElementData[] levelElements = elementsOverride ?? level.Elements;
            LevelElements = new LevelElementData[levelElements?.Length ?? 0];
            LevelMatrix = new LevelElementData[levelSize.x, levelSize.y];

            if (levelElements != null)
            {
                for (int i = 0; i < levelElements.Length; i++)
                {
                    LevelElementData element = levelElements[i]?.Clone();
                    LevelElements[i] = element;
                    if (element == null) continue;
                    LevelMatrix[element.Position.x, element.Position.y] = element;
                }
            }

            EnvironmentSpawner = new LevelEnvironmentSpawner(this, contentProvider, null);
        }

        public async UniTask RepositionCamera()
        {
            // In Edit Mode there is no running PlayerLoop, so UniTask.Yield() may never resume.
            // We still yield a frame in Play Mode to ensure bounds/transforms are settled.
            if (!Application.isPlaying)
            {
                if (cameraController != null)
                    cameraController.Reposition(LevelBounds);
                return;
            }

            await UniTask.Yield();

            // After yielding a frame, a level transition may have cleaned up this representation
            // and destroyed the camera. Unity's overloaded == detects the destroyed CameraController
            // that the ?. operator would silently skip past (throwing MissingReferenceException inside).
            if (LevelTransform == null || cameraController == null)
                return;
            cameraController.Reposition(LevelBounds);
        }

        public void Spawn(bool repositionCamera = true)
        {
            SpawnComplete = false;
            ActiveBlocks = new List<LevelBlockBehavior>();
            SpawnStarted?.Invoke(this);
            EnvironmentSpawner.SpawnGroundAndObstacles();
            EnvironmentSpawner.SpawnGates();
            SpawnBlocks();
            EnvironmentSpawner.SpawnGenerators();
            EnvironmentSpawner.SpawnInteractiveObjects();
            EnvironmentSpawner.SpawnBorders(true);
            FormGroupableEffects();
            EnvironmentSpawner.SpawnExtraLayer();

            if (repositionCamera)
                _ = RepositionCamera();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                BreakableLinkBlocks();
            }
#endif
            
            float mapReadyDelay = Mathf.Max(
                EnvironmentData.GetTotalAnimationTime() + 0.2f,
                EnvironmentSpawner.GateReadyDelay);

            spawnDelayedCall = DOVirtual.DelayedCall(mapReadyDelay, () =>
            {
                if (LevelTransform == null) return;

                if (Application.isPlaying)
                {
                    BreakableLinkBlocks();
                }

                SpawnComplete = true;
                foreach (LevelBlockBehavior block in ActiveBlocks)
                    block.OnMapSpawnCompleted();

                SpawnCompleted?.Invoke(this);
            }, false);
        }

        public LevelBlockBehavior SpawnBlock(Vector2Int position, BlockType blockType, BlockColor blockColor,
            BlockEffectData[] effects, int blockId, bool allowDelay = true, bool skipSpawnTween = false)
        {
            BlockData blockData = contentProvider.GetBlockData(blockType);
            if (blockData == null) return null;

            GameObject blockPrefab = blockData.Prefab;
            GameObject block = Object.Instantiate(blockPrefab, LevelTransform);
            LevelBlockBehavior blockBehavior = block.GetComponent<LevelBlockBehavior>();
            Vector2Int pivotPoint = blockBehavior.Figure.PivotPoint;

            int x = position.x - pivotPoint.x;
            int y = position.y - pivotPoint.y;
            block.transform.localPosition = new Vector3(x, 0, y);

            blockBehavior.Init(blockData, blockId, this);

            BlockColorData colorData = contentProvider.GetBlockColorData(blockColor);
            blockBehavior.SetColor(colorData);

            if (effects != null)
            {
                var effectsToApply = new List<(BlockEffectData effect, LevelBlockEffectData effectData)>();
                foreach (BlockEffectData effect in effects)
                {
                    if (effect == null || effect.Type == BlockEffectType.None) continue;
                    LevelBlockEffectData effectData = contentProvider.GetEffectData(effect.Type);
                    if (effectData == null)
                    {
                        Debug.LogError("No effect data found for effect type: " + effect.Type);
                        continue;
                    }
                    effectsToApply.Add((effect, effectData));
                }
                foreach (var (effect, effectData) in effectsToApply.OrderBy(x => x.effectData.EffectSortingOrder))
                    effectData.Behavior.ApplyEffect(blockBehavior, effect, effectData.EffectSortingOrder);
            }

            ActiveBlocks.Add(blockBehavior);
            LevelController.Instance.CellWatchNotifier?.OnNewBlockSpawn(blockBehavior);
            if (!skipSpawnTween)
                PlaySpawnTween(block, blockBehavior, allowDelay);
            else
                blockBehavior.NotifySpawnAnimationCompleted();
            return blockBehavior;
        }

        private void PlaySpawnTween(GameObject spawnedObject, LevelBlockBehavior blockBehavior, bool allowDelay = true)
        {
            PlayBlockReleaseSpawnTween(blockBehavior, () => blockBehavior?.NotifySpawnAnimationCompleted(), allowDelay);
        }

        /// <summary>
        /// Scale-in tween when a block becomes playable after container release (no init spawn delay).
        /// </summary>
        public void PlayBlockReleaseSpawnTween(LevelBlockBehavior blockBehavior, Action onComplete, bool allowDelay = false)
        {
            EnvironmentData.SpawnTweenConfig tweenConfig = EnvironmentData?.BlockSpawnTween;
            if (blockBehavior == null || tweenConfig == null || !Application.isPlaying || !tweenConfig.Enabled)
            {
                onComplete?.Invoke();
                return;
            }

            GameObject spawnedObject = blockBehavior.gameObject;
            if (!spawnedObject)
            {
                onComplete?.Invoke();
                return;
            }

            Transform targetTransform = spawnedObject.transform;
            targetTransform.localScale = Vector3.zero;
            blockBehavior.BeginInteractionTween();
            targetTransform.DOScale(1f, tweenConfig.Duration)
                .SetEase(tweenConfig.Ease)
                .SetDelay(allowDelay ? tweenConfig.Delay : 0f)
                .SetLink(spawnedObject)
                .OnComplete(() =>
                {
                    blockBehavior?.EndInteractionTween();
                    onComplete?.Invoke();
                });
        }

        private void SpawnBlocks()
        {
            LevelElementData[] levelElements = LevelElements;
            for (int i = 0; i < levelElements.Length; i++)
            {
                if (levelElements[i] is BlockLevelElementData block)
                {
                    SpawnBlock(block.Position, block.BlockType, block.BlockColor,
                        block.BlockEffects, block.BlockId);
                }
            }

            foreach (var spawnedBlock in ActiveBlocks)
            {
                foreach (var blockEffectBehavior in spawnedBlock.Effects)
                    blockEffectBehavior.OnCreatedAllBlock();
            }
            Physics.SyncTransforms();
        }

        private void BreakableLinkBlocks()
        {
            // Container-held blocks cannot share a group rigidbody yet.
            FormBreakableLinkGroupsForBlocks(ActiveBlocks.Where(block => !block.IsHeldByContainer()));
        }

        /// <summary>
        /// Links adjacent blocks that declare a matching {linkId, count} into breakable rigid groups.
        /// Reused at spawn (free blocks) and on container release.
        /// </summary>
        public void FormBreakableLinkGroupsForBlocks(IEnumerable<LevelBlockBehavior> source)
        {
            var candidates = source
                .Where(block => block.HasEffect(BlockEffectType.BreakableLink))
                .ToList();

            BreakableLinkEffectBehavior.FormLinks(candidates, LevelTransform);
        }

        private void FormGroupableEffects()
        {
            var groupMap = new Dictionary<(BlockEffectType, int), List<IGroupableEffect>>();

            foreach (LevelBlockBehavior block in ActiveBlocks)
            {
                foreach (BlockEffectBehavior effect in block.Effects)
                {
                    if (!effect.IsActive || effect is not IGroupableEffect groupable) continue;

                    var key = (effect.Type, groupable.GroupId);
                    if (!groupMap.TryGetValue(key, out List<IGroupableEffect> list))
                        groupMap[key] = list = new List<IGroupableEffect>();

                    list.Add(groupable);
                }
            }

            foreach (List<IGroupableEffect> members in groupMap.Values)
            {
                if (members.Count < 1) continue;

                var blocks = new List<LevelBlockBehavior>(members.Count);
                foreach (IGroupableEffect m in members)
                    blocks.Add(m.Owner);

                BlockGroup group = BlockGroup.Create(members[0].GroupId, blocks, LevelTransform);

                foreach (IGroupableEffect member in members)
                    member.OnGroupFormed(group, this);
            }
        }

        public void OnBlockDestructed(LevelBlockBehavior block) => ActiveBlocks.Remove(block);

        public void RegisterPendingBlockSpawn()
        {
            pendingBlockSpawnCount++;
        }

        public void UnregisterPendingBlockSpawn()
        {
            if (pendingBlockSpawnCount <= 0)
                return;

            pendingBlockSpawnCount--;
            if (pendingBlockSpawnCount == 0)
                EvaluateGameWinLose();
        }

        public void SetWinCondition(IWinCondition condition)
        {
            winCondition = condition;
            levelCompletedFired = false;
        }

        public void ResetCompletionLatch()
        {
            levelCompletedFired = false;
        }

        public void EvaluateGameWinLose()
        {
            if (levelCompletedFired)
                return;
            EvaluateWinCondition();
            EvaluateLose();
        }

        private void EvaluateWinCondition()
        {
            if (levelCompletedFired || !IsLevelCompleted())
                return;

            levelCompletedFired = true;
            LevelCompleted?.Invoke(this);
        }

        private void EvaluateLose()
        {
            if (levelCompletedFired || HasPendingBlockSpawn) return;
            LoseReason reason = DetermineObstacleLoseReason();
            if (reason != LoseReason.None)
            {
                levelCompletedFired = true;
                LevelFailed?.Invoke(this, reason, 1f);
            }
        }

        private LoseReason DetermineObstacleLoseReason()
        {
            LoseReason firstReason = LoseReason.None;

            foreach (var block in ActiveBlocks)
            {
                if (!block.CanCollectBlock()) continue;
                if (!block.CanBeClicked()) continue;
                
                foreach (GateBehavior gateBehavior in EnvironmentSpawner.Gates)
                {
                    var state = block.OnGateEntered(gateBehavior);
                    if (state == BlockGateState.Enterable)
                        return LoseReason.None;

                    if (firstReason == LoseReason.None)
                    {
                        var loseReason = state.ToLoseReason();
                        if (loseReason != LoseReason.None)
                            firstReason = loseReason;
                    }
                }
            }

            return firstReason;
        }

        public bool IsLevelCompleted()
        {
            if (!SpawnComplete || ActiveBlocks == null || winCondition == null)
                return false;

            return winCondition.Evaluate(this) == WinEvalResult.Win;
        }

        public void Cleanup()
        {
            spawnDelayedCall?.Kill();
            spawnDelayedCall = null;

            if (LevelTransform)
                Object.Destroy(LevelTransform.gameObject);

            ActiveBlocks?.Clear();
            LevelTransform = null;
            winCondition = null;
            levelCompletedFired = false;
            pendingBlockSpawnCount = 0;

            // Release event delegates so subscribers can be GC'd
            LevelCompleted = null;
            LevelFailed = null;
            SpawnStarted = null;
            SpawnCompleted = null;
        }

        public void SetBlocksInterpolation(RigidbodyInterpolation interpolation)
        {
            if (ActiveBlocks == null) return;
            foreach (LevelBlockBehavior block in ActiveBlocks)
            {
                if (!block) continue;
                Rigidbody rb = block.BlockRigidbody;
                if (rb) rb.interpolation = interpolation;
            }
        }

        public void SetWorldOffset(Vector3 offset)
        {
            if (LevelTransform)
                LevelTransform.position = offset;
        }

        public void DrawGizmos()
        {
            if (EnvironmentSpawner == null) return;
            var levelBounds = EnvironmentSpawner.GetBounds();
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(levelBounds.center, levelBounds.size);
        }

        public LevelElementData GetElement(Vector2Int position)
        {
            if (position.x < 0 || position.y < 0 || position.x >= Size.x || position.y >= Size.y)
                return null;
            return LevelMatrix[position.x, position.y];
        }

        /// <summary>
        /// Frees an InteractableObject cell once its runtime object is gone (e.g. a retracted Grinder
        /// tape column). Only <see cref="LevelMatrix"/> is rewritten — <see cref="LevelElements"/>
        /// keeps the authored data, matching <see cref="TryConvertExtendableBorderToInnerTile"/>.
        /// </summary>
        public bool ConvertInteractableCellToInnerTile(Vector2Int position)
        {
            LevelElementData element = GetElement(position);
            if (element is not InteractableObjectLevelElementData) return false;

            var innerTile = new InnerTileLevelElementData();
            innerTile.SetPosition(position);
            innerTile.AssignBlockId(element.BlockId);
            LevelMatrix[position.x, position.y] = innerTile;
            return true;
        }

        public bool TryConvertExtendableBorderToInnerTile(Vector2Int position, out LevelElementData element)
        {
            element = GetElement(position);
            if (element == null) return false;
            if (element is not BorderLevelElementData border || !border.IsExtendable) return false;

            var innerTile = new InnerTileLevelElementData();
            innerTile.SetPosition(position);
            innerTile.AssignBlockId(element.BlockId);
            LevelMatrix[position.x, position.y] = innerTile;
            element = innerTile;
            return true;
        }
    }
}
