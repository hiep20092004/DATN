using System;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;
using Ease = DG.Tweening.Ease;
using Tween = DG.Tweening.Tween;

namespace WaterFlow.Game
{
    public enum GeneratorState
    {
        Empty,
        Idle,
        Spawning
    }

    public class GeneratorBehavior : MonoBehaviour, IWatchedBlockMoveListener
    {
        [SerializeField] private GeneratorConfig config;
        [SerializeField] private GameObject generatorVisual;
        [SerializeField] private GameObject blockPreviewContainer;
        [SerializeField] private GeneratorFootprint footprintPrefab;

        [SerializeField] private RectTransform canvasTransform;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private MeshRenderer toggle;

        [SerializeField] private ParticleSystem GeneratorBreakFxUD;
        [SerializeField] private ParticleSystem GeneratorBreakFxLR;

        [SerializeField] private Transform doorLeftTransform;
        [SerializeField] private Transform doorRightTransform;
        
        // ── State machine ────────────────────────────────────────────────────────
        public GeneratorState State { get; private set; } = GeneratorState.Empty;
        public event Action<GeneratorState> StateChanged;

        // ── Core data ────────────────────────────────────────────────────────────
        private BorderData data;
        private GateDirection gateDirection;
        private int count;
        private List<IGeneratorLinkedVisual> linkedVisuals;

        private ILevelContentProvider contentProvider;
        private List<GeneratorBlockEntry> queue;
        private int nextBlockIndex;

        // ── Watched cells ────────────────────────────────────────────────────────
        private readonly List<Vector2Int> watchedCells = new();
        private readonly HashSet<Vector2Int> blockFootprints = new();
        private readonly HashSet<Vector2Int> blockAdditionalRequireCells = new();
        private Tween spawnTravelTween;
        private Tween countTextPopupTween;
        private Tween toggleTween;
        private Tween doorTween;
        private IBlockCellWatchService watchService;
        private IBlockCellEventNotifier watchNotifier;
        private LevelRepresentation levelRepresentation;
        private Func<bool> isLevelStarted;

        private bool isPendingSpawn;

        /// <summary>Prefab local scales for preview/canvas before <see cref="InitGeneratorVisual"/> applies mirror axes on the visual root.</summary>
        private Vector3 blockPreviewAuthoringLocalScale = Vector3.one;
        private Vector3 canvasAuthoringLocalScale = Vector3.one;
        private bool overlayAuthoringScaleCaptured;

        // ── Spawn footprint pool ──────────────────────────────────────────────────
        private readonly List<GeneratorFootprint> spawnFootprintPool = new();
        private int activeFootprintCount;

        // ── Public accessors ─────────────────────────────────────────────────────
        public IReadOnlyList<Vector2Int> WatchedCells => watchedCells;
        public GateDirection GateDirection => gateDirection;
        public BorderData Data => data;

        /// <summary>Un-spawned entries of the queue, in delivery order (i.e. <c>queue[nextBlockIndex..]</c>).</summary>
        public List<GeneratorBlockEntry> GetRemainingQueue()
        {
            if (queue == null || nextBlockIndex >= queue.Count)
                return new List<GeneratorBlockEntry>();

            return queue.GetRange(nextBlockIndex, queue.Count - nextBlockIndex);
        }

        /// <summary>
        /// Returns all unique <see cref="BlockEffectType"/> values present across all entries in the generator queue.
        /// Useful for obstacle-unlock scanning at runtime without re-reading level data assets.
        /// </summary>
        public HashSet<BlockEffectType> GetQueueBlockEffectTypes()
        {
            var result = new HashSet<BlockEffectType>();
            if (queue == null) return result;
            foreach (GeneratorBlockEntry entry in queue)
            {
                if (entry?.BlockEffects == null) continue;
                foreach (BlockEffectData effect in entry.BlockEffects)
                    if (effect != null) result.Add(effect.Type);
            }
            return result;
        }

        private void Awake()
        {
            CaptureOverlayAuthoringLocalScalesIfNeeded();
        }

        private void CaptureOverlayAuthoringLocalScalesIfNeeded()
        {
            if (overlayAuthoringScaleCaptured)
                return;
            if (blockPreviewContainer)
                blockPreviewAuthoringLocalScale = blockPreviewContainer.transform.localScale;
            if (canvasTransform)
                canvasAuthoringLocalScale = canvasTransform.localScale;
            overlayAuthoringScaleCaptured = blockPreviewContainer != null || canvasTransform != null;
        }

        // ── Init ─────────────────────────────────────────────────────────────────
        public bool Init(BorderData borderData, ILevelContentProvider levelContentProvider,
            IBlockCellWatchService watchService,
            LevelRepresentation levelRepresentation = null,
            Func<bool> isLevelStarted = null)
        {
            data = borderData;
            contentProvider = levelContentProvider;
            this.watchService = watchService;
            watchNotifier = watchService as IBlockCellEventNotifier;
            this.levelRepresentation = levelRepresentation;
            this.isLevelStarted = isLevelStarted;

            if (borderData.GateDirectionType == GateDirection.Type.None)
                return false;

            gateDirection = GateDirection.DIRECTIONS[(int)borderData.GateDirectionType];
            queue = (borderData.LevelElementData as GeneratorLevelElementData)?.GeneratorQueue;
            count = queue?.Count ?? 0;
            countText.text = count.ToString();
            nextBlockIndex = 0;
            
            if(Application.isPlaying && count == 0) return false;
            
            BuildWatchedCells(borderData);
            var isSuccess = ValidateGeneratorQueueFootprints(levelRepresentation);
            if(!isSuccess) return false;
            
            InitGeneratorVisual(borderData);
            BuildBlockFootprints();
            

            InitPreview();

            if (!Application.isPlaying)
                return true;

            watchService?.Register(this);
            if (count <= 0)
            {
                SetState(GeneratorState.Empty);
                OnEmpty();
                return true;
            }

            SetState(GeneratorState.Idle);
            RegisterPendingSpawn();
            if (CanSpawn())
                BeginSpawn();
            return true;
        }

        private void InitGeneratorVisual(BorderData borderData)
        {
            CaptureOverlayAuthoringLocalScalesIfNeeded();

            generatorVisual.transform.localPosition =
                gateDirection.Position * config.VisualEdgeOffset;

            // Keep preview + world canvas facing the same way as before the visual is rotated/scaled
            // (only position has been applied so far). Setting .rotation preserves world position of each pivot.
            Quaternion blockPreviewWorldRot = default;
            bool hasBlockPreview = blockPreviewContainer != null;
            if (hasBlockPreview)
                blockPreviewWorldRot = blockPreviewContainer.transform.rotation;

            Quaternion canvasWorldRot = default;
            bool hasCanvas = canvasTransform != null;
            if (hasCanvas)
                canvasWorldRot = canvasTransform.rotation;

            Vector3 visualEuler = borderData.Rotation;
            Vector3 visualScale = generatorVisual.transform.localScale;
            switch (borderData.GateDirectionType)
            {
                case GateDirection.Type.Right:
                    visualScale.x = -1;
                    break;
                case GateDirection.Type.Top:
                    visualEuler.y = 90f;
                    break;
                case GateDirection.Type.Bottom:
                    visualEuler.y = -90f;
                    visualScale.z = -1;
                    break;
            }
            generatorVisual.transform.localRotation = Quaternion.Euler(visualEuler);
            generatorVisual.transform.localScale = visualScale;

            if (hasBlockPreview)
                blockPreviewContainer.transform.rotation = blockPreviewWorldRot;
            if (hasCanvas)
                canvasTransform.rotation = canvasWorldRot;

            // Negative localScale on the visual (Right: x, Bottom: z) reflects children; TMP then reads as a 90° twist.
            // Cancel mirror per axis on overlay roots so lossy scale stays consistent with the reference generator.
            ApplyOverlayMirrorCompensation();
        }

        /// <summary>
        /// Undoes mirror from <see cref="generatorVisual"/> negative localScale axes on preview/canvas children.
        /// Must run after world rotations are restored.
        /// </summary>
        private void ApplyOverlayMirrorCompensation()
        {
            Vector3 p = generatorVisual.transform.localScale;
            var flip = new Vector3(
                p.x < 0f ? -1f : 1f,
                p.y < 0f ? -1f : 1f,
                p.z < 0f ? -1f : 1f);

            if (blockPreviewContainer)
                blockPreviewContainer.transform.localScale = Vector3.Scale(blockPreviewAuthoringLocalScale, flip);
            if (canvasTransform)
                canvasTransform.localScale = Vector3.Scale(canvasAuthoringLocalScale, flip);
        }

        private void OnDisable()
        {
            spawnTravelTween?.Kill();
            spawnTravelTween = null;
            countTextPopupTween?.Kill();
            countTextPopupTween = null;
            toggleTween?.Kill();
            toggleTween = null;
            doorTween?.Kill();
            doorTween = null;
            watchService?.Unregister(this);
            UnregisterPendingSpawn();
            SetFootprintsActive(false);
        }

        private void RegisterPendingSpawn()
        {
            if (isPendingSpawn || levelRepresentation == null)
                return;

            levelRepresentation.RegisterPendingBlockSpawn();
            isPendingSpawn = true;
        }

        private void UnregisterPendingSpawn()
        {
            if (!isPendingSpawn || levelRepresentation == null)
                return;

            levelRepresentation.UnregisterPendingBlockSpawn();
            isPendingSpawn = false;
        }

        // ── Watched-cell building ─────────────────────────────────────────────────
        private void BuildWatchedCells(BorderData borderData)
        {
            watchedCells.Clear();
            if (gateDirection == null || gateDirection.PipeOccupiedOffsets == null) return;

            var seen = new HashSet<Vector2Int>();
            GateDirection.GetFlankOffsets(borderData.GateDirectionType, out var flankA, out var flankB);
            Vector2Int basePos = borderData.Position;
            Vector2Int[] perp = gateDirection.PipeOccupiedOffsets;

            for (int i = 0; i < perp.Length; i++)
            {
                Vector2Int p = basePos - perp[i];
                Add(p + flankA);
                Add(p);
                Add(p + flankB);
            }

            void Add(Vector2Int cell)
            {
                if (seen.Add(cell))
                    watchedCells.Add(cell);
            }
        }

        private bool ValidateGeneratorQueueFootprints(LevelRepresentation level)
        {
            string generatorContext = $"Generator '{name}' at cell {data.Position}";
            bool hasBlockStuck = false;
            for (int i = 0; i < queue.Count; i++)
            {
                if (!TryResolveSpawnFootprint(i, out List<Vector2Int> footprint, out _) || footprint == null ||
                    footprint.Count == 0)
                    continue;

                GeneratorBlockEntry entry = queue[i];

                for (int c = 0; c < footprint.Count; c++)
                {
                    Vector2Int cell = footprint[c];
                    LevelElementData element = level.GetElement(cell);
                    if (element == null)
                    {
                        Debug.LogError(
                            $"[Generator] {generatorContext}: queue[{i}] (BlockType={entry.BlockType}) footprint cell {cell} has no level data (empty slot or out of bounds).");
                        hasBlockStuck = true;
                        continue;
                    }

                    if (!element.Type.IsCanSpawnBlock())
                    {
                        Debug.LogError(
                            $"[Generator] {generatorContext}: queue[{i}] (BlockType={entry.BlockType}) footprint cell {cell} is {element.Type}, which is not an inner-board element type (expect InnerTile, Block, Obstacle, or InteractableObject per {nameof(ElementType)}.{nameof(ElementTypeExtensions.ToBorderCellType)}).");
                        hasBlockStuck = true;
                    }
                }
            }

            return !hasBlockStuck;
        }

        private void BuildBlockFootprints()
        {
            blockFootprints.Clear();
            blockAdditionalRequireCells.Clear();

            if (!TryGetNextSpawnFootprint(out var footprints, out var additionalRequire))
            {
                ClearSpawnFootprintVisuals();
                return;
            }

            foreach (var cell in footprints)
                blockFootprints.Add(cell);

            if (additionalRequire != null)
                foreach (var cell in additionalRequire)
                    blockAdditionalRequireCells.Add(cell);

            BuildSpawnFootprints(footprints);
        }

        private void BuildSpawnFootprints(List<Vector2Int> footprints)
        {
            if (!Application.isPlaying) return;
            if (footprintPrefab == null || levelRepresentation?.LevelTransform == null) return;

            if (footprints.Count == 0)
            {
                SetFootprintsActive(false);
                activeFootprintCount = 0;
                return;
            }

            while (spawnFootprintPool.Count < footprints.Count)
            {
                GeneratorFootprint instance = Instantiate(footprintPrefab, levelRepresentation.LevelTransform);
                instance.gameObject.SetActive(false);
                spawnFootprintPool.Add(instance);
            }

            BlockColorData colorData = null;
            if (queue != null && nextBlockIndex >= 0 && nextBlockIndex < queue.Count)
                colorData = contentProvider?.GetBlockColorData(queue[nextBlockIndex].BlockColor);

            for (int i = 0; i < footprints.Count; i++)
            {
                GeneratorFootprint fp = spawnFootprintPool[i];
                Vector2Int cell = footprints[i];
                fp.transform.localPosition = new Vector3(cell.x, 0.02f, cell.y);
                if (colorData != null) fp.SetColor(colorData);
                fp.gameObject.SetActive(false);
            }

            for (int i = footprints.Count; i < spawnFootprintPool.Count; i++)
                spawnFootprintPool[i].gameObject.SetActive(false);

            activeFootprintCount = footprints.Count;
        }

        private void ClearSpawnFootprintVisuals()
        {
            if (!Application.isPlaying) return;

            for (int i = 0; i < spawnFootprintPool.Count; i++)
            {
                if (spawnFootprintPool[i] != null)
                    spawnFootprintPool[i].gameObject.SetActive(false);
            }

            activeFootprintCount = 0;
        }

        private void SetFootprintsActive(bool active)
        {
            for (int i = 0; i < activeFootprintCount && i < spawnFootprintPool.Count; i++)
                spawnFootprintPool[i].gameObject.SetActive(active);
        }

        private bool CanSpawn()
        {
            if (State != GeneratorState.Idle) return false;
            if (watchService == null) return true;
            return watchService.GetOccupiedCount(blockFootprints) == 0
                && (blockAdditionalRequireCells.Count == 0 || watchService.GetOccupiedCount(blockAdditionalRequireCells) == 0);
        }

        // ── IWatchedBlockMoveListener ─────────────────────────────────────────────
        public void OnWatchedBlockPicked(LevelBlockBehavior block)
        {
            if (queue == null || nextBlockIndex >= queue.Count || activeFootprintCount <= 0)
                return;

            SetFootprintsActive(true);
        }

        public void OnWatchedBlockReleased(LevelBlockBehavior block, Vector2Int snapTargetPosition)
        {
            SetFootprintsActive(false);
        }

        public void OnWatchedBlockCellsChanged(
            IReadOnlyList<Vector2Int> vacatedCells,
            IReadOnlyList<Vector2Int> occupiedCells,
            LevelBlockBehavior block)
        {
            TrySpawnOnVacated(vacatedCells);
        }

        public void OnWatchedBlockCellsDestructed(
            IReadOnlyList<Vector2Int> vacatedCells,
            LevelBlockBehavior block)
        {
            TrySpawnOnVacated(vacatedCells);
        }

        private void TrySpawnOnVacated(IReadOnlyList<Vector2Int> vacatedCells)
        {
            for (int i = 0; i < vacatedCells.Count; i++)
            {
                var cell = vacatedCells[i];
                if (!blockFootprints.Contains(cell) && !blockAdditionalRequireCells.Contains(cell)) continue;
                if (count <= 0)
                {
                    SetState(GeneratorState.Empty);
                    return;
                }

                if (CanSpawn())
                    BeginSpawn();
                return;
            }
        }

        // ── State transitions ─────────────────────────────────────────────────────
        private void SetState(GeneratorState newState)
        {
            if (State == newState) return;
            State = newState;
            StateChanged?.Invoke(newState);
        }


        private bool TryGetNextSpawnFootprint(out List<Vector2Int> footprint, out List<Vector2Int> additionalRequireCells) =>
            TryResolveSpawnFootprint(nextBlockIndex, out footprint, out additionalRequireCells);


        private bool TryResolveSpawnFootprint(int queueIndex, out List<Vector2Int> footprint, out List<Vector2Int> additionalRequireCells)
        {
            footprint = null;
            additionalRequireCells = null;
            if (queue == null || queueIndex < 0 || queueIndex >= queue.Count) return false;
            if (data == null || gateDirection == null)
                return false;

            GeneratorBlockEntry entry = queue[queueIndex];
            BlockSpawnPreset preset = config != null ? config.GetSpawnPreset(entry.BlockType) : null;
            if (preset == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[Generator] No spawn preset for BlockType={entry.BlockType}. Staying idle.");
#endif
                return false;
            }

            Vector2Int spawnCell = preset.GetSpawnCell(data.GateDirectionType);

            BlockData blockData = contentProvider?.GetBlockData(entry.BlockType);
            if (blockData == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[Generator] No BlockData for BlockType={entry.BlockType}.");
#endif
                return false;
            }

            LevelFigure figure = blockData.Figure;
            if (figure == null)
            {
                return false;
            }

            bool ok = GeneratorSpawnGeometry.TryGetSpawnFootprint(
                figure,
                data.GateDirectionType,
                spawnCell,
                watchedCells,
                data.Position,
                gateDirection,
                out footprint,
                out additionalRequireCells);

            if (!ok || footprint == null || footprint.Count == 0)
            {
#if UNITY_EDITOR
                Debug.LogError($"[Generator] Could not resolve spawn footprint for BlockType={entry.BlockType}.");
#endif
                return false;
            }

            return true;
        }

        private void UpdateToggleVisual(bool ready)
        {
            if (toggle == null || config == null) return;

            Material mat = ready ? config.ToggleReadyMaterial : config.ToggleBusyMaterial;
            if (mat != null)
                toggle.sharedMaterial = mat;

            Vector3 targetPos = config.GetToggleLocalPos(ready);
            float duration = config.ToggleAnimDuration;

            toggleTween?.Kill();
            if (duration > 0f)
                toggleTween = toggle.transform.DOLocalMove(targetPos, duration)
                    .SetEase(config.ToggleAnimEase)
                    .SetLink(gameObject);
            else
                toggle.transform.localPosition = targetPos;
        }

        // Local scale Z == DoorOpenLocalZ → doors retracted (block can pass); == DoorClosedLocalZ → doors sealed.
        private void AnimateDoors(bool open)
        {
            if (config == null || (doorLeftTransform == null && doorRightTransform == null)) return;

            float targetZ = open ? config.DoorOpenLocalZ : config.DoorClosedLocalZ;
            float duration = open ? config.DoorOpenDuration : config.DoorCloseDuration;
            Ease ease = open ? config.DoorOpenEase : config.DoorCloseEase;

            doorTween?.Kill();

            if (duration <= 0f)
            {
                SetDoorLocalScaleZ(doorLeftTransform, targetZ);
                SetDoorLocalScaleZ(doorRightTransform, targetZ);
                return;
            }

            Sequence seq = DOTween.Sequence().SetLink(gameObject);
            if (doorLeftTransform != null)
                seq.Join(doorLeftTransform.DOScaleZ(targetZ, duration).SetEase(ease));
            if (doorRightTransform != null)
                seq.Join(doorRightTransform.DOScaleZ(targetZ, duration).SetEase(ease));
            doorTween = seq;
        }

        private static void SetDoorLocalScaleZ(Transform door, float z)
        {
            if (door == null) return;
            Vector3 s = door.localScale;
            s.z = z;
            door.localScale = s;
        }

        private void BeginSpawn()
        {
            if (queue == null || nextBlockIndex >= queue.Count) return;

            var rep = levelRepresentation;
            if (rep == null) return;

            UpdateToggleVisual(true);

            GeneratorBlockEntry entry = queue[nextBlockIndex];
            BlockSpawnPreset preset = config.GetSpawnPreset(entry.BlockType);
            if (preset == null)
            {
                SetState(GeneratorState.Idle);
                return;
            }

            Vector2Int spawnCell = preset.GetSpawnCell(data.GateDirectionType);

            // Resolve pivot's world cell.
            if (!GeneratorSpawnGeometry.TryGridToWorldCell(
                    watchedCells, data.Position, gateDirection,
                    spawnCell.x, spawnCell.y, out Vector2Int pivotWorldCell))
            {
#if UNITY_EDITOR
                Debug.LogError($"[Generator] Cannot resolve spawn world cell. Staying idle.");
#endif
                SetState(GeneratorState.Idle);
                return;
            }

            SetState(GeneratorState.Spawning);
            ClearPreviewContainer();
            AnimateDoors(true);
            Services.AudioService.PlaySound(AudioId.Obstacle_Generator);

            LevelBlockBehavior block = rep.SpawnBlock(
                pivotWorldCell, entry.BlockType, entry.BlockColor,
                entry.BlockEffects, entry.BlockId, false, true);

            if (!block)
            {
                SetState(GeneratorState.Idle);
                return;
            }

            Vector3 finalLocalPos = block.transform.localPosition;

            // Determine start position and scale from the current preview block.
            float previewScale = config != null ? config.PreviewBlockScale : 1f;
            Vector3 startLocalPos;

            Transform previewChild = blockPreviewContainer && blockPreviewContainer.transform.childCount > 0
                ? blockPreviewContainer.transform.GetChild(0)
                : null;

            if (previewChild != null && block.transform.parent)
            {
                startLocalPos = block.transform.parent.InverseTransformPoint(previewChild.position);
                startLocalPos.y = finalLocalPos.y;
            }
            else
            {
                // Fallback: start from the outer pipe cell.
                Vector2Int outerPipeCell = data.Position + gateDirection.PipeOccupiedOffsets[^1];
                Vector2Int pivot = block.Figure.PivotPoint;
                startLocalPos = new Vector3(
                    outerPipeCell.x - pivot.x,
                    0f,
                    outerPipeCell.y - pivot.y);
                previewScale = 1f;
            }

            block.transform.localPosition = startLocalPos;
            block.transform.localScale = Vector3.one * previewScale;

            spawnTravelTween?.Kill();
            spawnTravelTween = DOTween.Sequence()
                .Join(block.transform.DOLocalMove(finalLocalPos, config.SpawnTravelDuration).SetEase(config.SpawnTravelEase))
                .Join(block.transform.DOScale(Vector3.one, config.SpawnTravelDuration).SetEase(config.SpawnTravelEase))
                .OnComplete(() => OnSpawnTravelComplete(block));
        }

        private void OnSpawnTravelComplete(LevelBlockBehavior block)
        {
            // Block has finished travelling from the pipe to its delivery cell; register its
            // real occupancy with the watch service now (it was kept silent during travel).
            watchNotifier?.OnNewBlockSpawn(block);

            AnimateDoors(false);
            UpdateToggleVisual(false);
            block.OnMapSpawnCompleted();
            if (isLevelStarted != null && isLevelStarted())
            {
                List<BlockEffectBehavior> effects = block.Effects;
                foreach (BlockEffectBehavior effect in effects)
                    effect.OnLevelActivated();
            }
            OnBlockDelivered();
            if (count > 0)
                SetState(GeneratorState.Idle);
        }

        public void OnBlockDelivered()
        {
            if (count <= 0)
                return;

            count--;
            countText.text = count.ToString();
            PlayCountTextChangePopup();

            nextBlockIndex++;
            BuildBlockFootprints();

            if (contentProvider != null && queue != null && blockPreviewContainer)
            {
                ClearPreviewContainer();
                if (nextBlockIndex < queue.Count)
                    SpawnPreviewBlock(nextBlockIndex, Vector3.zero);
            }

            if (count == 0)
            {
                SetState(GeneratorState.Empty);
                OnEmpty();
            }
        }

        private void PlayCountTextChangePopup()
        {
            if (countText == null) return;

            RectTransform rt = countText.rectTransform;
            countTextPopupTween?.Kill();
            countTextPopupTween = null;
            rt.localScale = Vector3.one;

            if (config == null
                || config.CountTextPopupDuration <= 0f
                || config.CountTextPunchStrength <= 0f)
                return;

            countTextPopupTween = rt
                .DOPunchScale(Vector3.one * config.CountTextPunchStrength, config.CountTextPopupDuration, 8, 0.45f)
                .SetLink(gameObject);
        }

        private void OnEmpty()
        {
            UnregisterPendingSpawn();

            if (linkedVisuals != null)
            {
                for (int i = 0; i < linkedVisuals.Count; i++)
                    linkedVisuals[i]?.OnGeneratorEmptied();
            }

            PlayBreakFx();
        }

        private void PlayBreakFx()
        {
            Services.AudioService.PlaySound(AudioId.Obstacle_Generator_Break);

            ParticleSystem fxToPlay = GetBreakFxForDirection();

            Sequence seq = DOTween.Sequence().SetLink(gameObject);

            if (fxToPlay && fxToPlay.gameObject)
            {
                fxToPlay.transform.SetParent(null);
            }
            
            if (generatorVisual)
            {
                Transform vt = generatorVisual.transform;
                Vector3 orig = vt.localScale;

                seq.Append(vt.DOScale(new Vector3(orig.x * 1.2f, orig.y, orig.z * 0.9f), 0.1f)
                             .SetEase(Ease.OutQuad))
                   .Append(vt.DOScale(new Vector3(orig.x * 0.9f, orig.y, orig.z * 1.2f), 0.15f)
                             .SetEase(Ease.OutQuad));
            }

            seq.AppendCallback(() =>
            {
                if (generatorVisual)
                    generatorVisual.SetActive(false);

                if (!fxToPlay) return;

                GameObject fxObj = fxToPlay.gameObject;
                fxObj.SetActive(true);
                fxToPlay.Play();
                Destroy(fxObj, 1f);
            });
        }

        private ParticleSystem GetBreakFxForDirection()
        {
            if (data == null)
                return GeneratorBreakFxUD != null ? GeneratorBreakFxUD : GeneratorBreakFxLR;

            bool isUD = data.GateDirectionType == GateDirection.Type.Top
                     || data.GateDirectionType == GateDirection.Type.Bottom;
            return isUD ? GeneratorBreakFxUD : GeneratorBreakFxLR;
        }

        // ── Linked visuals ────────────────────────────────────────────────────────
        public void RegisterLinkedVisual(IGeneratorLinkedVisual visual)
        {
            if (visual == null) return;
            linkedVisuals ??= new List<IGeneratorLinkedVisual>();
            linkedVisuals.Add(visual);
        }

        // ── Preview (editor + play-mode lobby) ───────────────────────────────────
        private void InitPreview()
        {
            if (blockPreviewContainer == null || contentProvider == null || queue == null || queue.Count == 0)
                return;

            ClearPreviewContainer();

            if (Application.isPlaying)
            {
                SpawnPreviewBlock(0, Vector3.zero);
            }
            else
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    Vector3 offset = new Vector3(0f, i * config.EditorPreviewStackSpacing, 0f);
                    SpawnPreviewBlock(i, offset);
                }
            }
        }

        private void SpawnPreviewBlock(int queueIndex, Vector3 stackOffset)
        {
            if (queue == null || queueIndex < 0 || queueIndex >= queue.Count)
                return;

            GeneratorBlockEntry entry = queue[queueIndex];
            BlockData blockData = contentProvider.GetBlockData(entry.BlockType);
            if (blockData?.Prefab == null)
                return;

            float previewScale = config != null ? config.PreviewBlockScale : 1f;
            Vector3 localPosition = ComputePreviewLocalPosition(blockData.Figure, previewScale, stackOffset);

            if (Application.isPlaying)
            {
                SpawnPreviewBlockRuntime(entry, localPosition, previewScale);
                return;
            }

            BlockColorData colorData = contentProvider.GetBlockColorData(entry.BlockColor);
            if (colorData == null)
                return;

            GameObject instance = Instantiate(blockData.Prefab, blockPreviewContainer.transform);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * previewScale;

            ApplyPreviewColor(instance, colorData);
            DisablePreviewWaterVisuals(instance);
            StripPreviewGameplay(instance);
        }

        private static Vector3 ComputePreviewLocalPosition(LevelFigure figure, float previewScale, Vector3 stackOffset)
        {
            if (figure == null || !figure.TryGetFigureFilledBoundsCenterXZ(out Vector2 c))
                return stackOffset;

            // Uniform scale about the block root: parent-space center = localPos + previewScale * c
            return new Vector3(
                stackOffset.x - previewScale * c.x,
                stackOffset.y,
                stackOffset.z - previewScale * c.y);
        }

        /// <summary>
        /// Spawns a preview block at runtime using the same path as a real game block so that
        /// visual appearance — materials, effects, dual-color, etc. — matches exactly.
        /// The block is detached from game-state tracking immediately after creation.
        /// </summary>
        private void SpawnPreviewBlockRuntime(
            GeneratorBlockEntry entry,
            Vector3 localPosition,
            float previewScale)
        {
            var rep = levelRepresentation;
            if (rep == null) return;

            LevelBlockBehavior block = rep.SpawnBlock(
                Vector2Int.zero, entry.BlockType, entry.BlockColor,
                entry.BlockEffects, entry.BlockId, false, true);

            if (!block) return;

            // Detach from game tracking — preview is visual-only
            rep.ActiveBlocks.Remove(block);
            watchNotifier?.NotifyBlockDestructed(block);

            // Notify effects that all blocks have been created so any cross-block setup runs
            foreach (var effect in block.Effects)
                effect.OnCreatedAllBlock();

            DisablePreviewWaterVisuals(block.gameObject);

            // Reparent into the preview container
            block.transform.SetParent(blockPreviewContainer.transform, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = Vector3.one * previewScale;

            // Strip physics and click-handling; preserve all visual child objects
            foreach (var col in block.GetComponentsInChildren<Collider>(true))
                Destroy(col);
            var rb = block.GetComponent<Rigidbody>();
            if (rb) Destroy(rb);
            var levelBlock = block.GetComponent<LevelBlockBehavior>();
            if (levelBlock) Destroy(levelBlock);
        }

        /// <summary>
        /// Hides every water mesh used by blocks (single module, dual modules, nested) so generator preview stays body+glass only.
        /// </summary>
        private static void DisablePreviewWaterVisuals(GameObject root)
        {
            if (!root) return;
            var modules = root.GetComponentsInChildren<WaterVisualModule>(true);
            for (int i = 0; i < modules.Length; i++)
                modules[i].gameObject.SetActive(false);
        }

        private static void StripPreviewGameplay(GameObject instance)
        {
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                DestroyOrImmediate(col);

            var rb = instance.GetComponent<Rigidbody>();
            if (rb != null) DestroyOrImmediate(rb);

            foreach (var effect in instance.GetComponentsInChildren<BlockEffectBehavior>(true))
                DestroyOrImmediate(effect);

            var levelBlock = instance.GetComponent<LevelBlockBehavior>();
            if (levelBlock != null) DestroyOrImmediate(levelBlock);
        }

        private static void ApplyPreviewColor(GameObject instance, BlockColorData colorData)
        {
            var levelBlock = instance.GetComponent<LevelBlockBehavior>();
            if (levelBlock != null)
            {
                levelBlock.SetColor(colorData);
                return;
            }

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (colorData.Material != null)
                    renderer.sharedMaterial = colorData.Material;
            }
        }

        private static void DestroyOrImmediate(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void ClearPreviewContainer()
        {
            if (blockPreviewContainer == null) return;
            Transform t = blockPreviewContainer.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject child = t.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [SerializeField] private bool showDebugWatchedCellGizmos;
        [SerializeField] private bool showDebugWatchedCellIndices = true;
        [SerializeField] private bool showDebugNextSpawnFootprintGizmos;
        private static readonly Color WatchedCellGizmoFillColor = new(1f, 0.85f, 0f, 0.25f);
        private static readonly Color WatchedCellGizmoWireColor = new(1f, 0.5f, 0f, 1f);
        private static readonly Color NextSpawnFootprintGizmoFillColor = new(0f, 0.85f, 1f, 0.3f);
        private static readonly Color NextSpawnFootprintGizmoWireColor = new(0f, 0.45f, 1f, 1f);
        private static readonly Color AdditionalRequireGizmoFillColor = new(1f, 0.2f, 0.35f, 0.28f);
        private static readonly Color AdditionalRequireGizmoWireColor = new(1f, 0.15f, 0.2f, 1f);
        
        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;

            if (showDebugWatchedCellGizmos && watchedCells.Count > 0)
            {
                for (int i = 0; i < watchedCells.Count; i++)
                {
                    Vector2Int cell = watchedCells[i];
                    Vector3 center = new Vector3(cell.x, 0.25f, cell.y);
                    Vector3 size = new Vector3(1f, 0.5f, 1f);

                    Gizmos.color = WatchedCellGizmoFillColor;
                    Gizmos.DrawCube(center, size);
                    Gizmos.color = WatchedCellGizmoWireColor;
                    Gizmos.DrawWireCube(center, size);

                    if (showDebugWatchedCellIndices)
                        DrawEditorLabel(center + Vector3.up * 0.35f, i.ToString(), WatchedCellGizmoWireColor);
                }
            }

            bool drawFootprint = showDebugNextSpawnFootprintGizmos;
            if (drawFootprint)
            {
                if (TryGetNextSpawnFootprint(out List<Vector2Int> spawnFootprint, out List<Vector2Int> additionalRequire))
                {
                    Vector3 footprintSize = new Vector3(1f, 0.5f, 1f);
                    if (spawnFootprint is { Count: > 0 })
                    {
                        for (int i = 0; i < spawnFootprint.Count; i++)
                        {
                            Vector2Int cell = spawnFootprint[i];
                            Vector3 center = new Vector3(cell.x, 0.32f, cell.y);

                            Gizmos.color = NextSpawnFootprintGizmoFillColor;
                            Gizmos.DrawCube(center, footprintSize);
                            Gizmos.color = NextSpawnFootprintGizmoWireColor;
                            Gizmos.DrawWireCube(center, footprintSize);
                        }
                    }

                    if (additionalRequire is { Count: > 0 })
                    {
                        Vector3 additionalSize = new Vector3(0.92f, 0.45f, 0.92f);
                        for (int i = 0; i < additionalRequire.Count; i++)
                        {
                            Vector2Int cell = additionalRequire[i];
                            Vector3 center = new Vector3(cell.x, 0.38f, cell.y);

                            Gizmos.color = AdditionalRequireGizmoFillColor;
                            Gizmos.DrawCube(center, additionalSize);
                            Gizmos.color = AdditionalRequireGizmoWireColor;
                            Gizmos.DrawWireCube(center, additionalSize);
                        }
                    }
                }
            }

            Gizmos.color = previousColor;
        }

        private static void DrawEditorLabel(Vector3 worldPos, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            style.normal.textColor = color;
            UnityEditor.Handles.Label(worldPos, text, style);
        }
#endif
    }
}