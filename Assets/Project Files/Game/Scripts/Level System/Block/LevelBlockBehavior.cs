using System;
using System.Collections.Generic;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Facade for a level block. Holds the serialized scene references (so prefabs stay single-component)
    /// and the lifecycle/physics/geometry/gate/destruction concerns, and delegates fill, effects, and
    /// visual/material concerns to plain collaborators (<see cref="BlockFillController"/>,
    /// <see cref="BlockEffectController"/>, <see cref="BlockVisualController"/>). Public members keep their
    /// original signatures as thin forwarders so existing callers compile unchanged.
    /// </summary>
    public class LevelBlockBehavior : MonoBehaviour, IClickableObject
    {
        [SerializeField] LevelFigure figure;
        [SerializeField] MeshRenderer meshRenderer;
        [SerializeField] MeshRenderer meshGlass;
        [SerializeField] MeshRenderer outerRenderer;
        [SerializeField] WaterVisualModule waterVisualModule;
        [SerializeField] private WaterMinMaxConfig waterMinMaxConfig;
        [SerializeField] Transform shutterBullHandleStart;
        [SerializeField] Transform shutterBullHandleEnd;
        [SerializeField] Transform colliderParent;

        [Header("Collider")]
        [SerializeField] private List<Collider> pressColliders = new();
        [SerializeField] private List<Collider> blockColliders = new();

        private Rigidbody blockRigidbody;
        /// <summary>Level container parent before a combine group reparented this block; cleared after full restore.</summary>
        private Transform parentBeforeCombineGroup;
        private BlockData blockConfig;
        private BlockColorData originColorConfig;

        // Visual is built in Awake so prefab instances are ready before Init (e.g. generator preview).
        private BlockVisualController visual;
        // Remaining collaborators are constructed in Init.
        private BlockEffectController effectController;
        private BlockFillController fill;

        private static readonly Vector3 DestroyScaleTarget = new(0f, 0.6f, 0f);
        private const float DestroyScaleDuration = 0.25f;

        private CustomEasingFunction disappearEasing;
        private int blockId;
        private TweenCase shakeTweenCase;
        private Vector3 originalPosition;
        private LevelRepresentation ownerRepresentation;
        private BlockTheme blockTheme;
        private readonly HashSet<object> pressColliderSuppressSources = new();

        public bool BlockCollectable { get; set; } = true;
        public LevelFigure Figure => figure;
        public MeshRenderer MeshRenderer => meshRenderer;
        public MeshRenderer MeshGlass => meshGlass;
        public MeshRenderer MeshWater => waterVisualModule != null ? waterVisualModule.WaterRenderer : null;
        public BlockData BlockConfig => blockConfig;
        public BlockColorData OriginColorConfig => originColorConfig;
        public Vector2Int MatrixPosition => new (Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));
        public List<BlockEffectBehavior> Effects => effectController.Effects;
        public Transform ShutterTransformStart => shutterBullHandleStart;
        public Transform ShutterBullHandleEnd => shutterBullHandleEnd;
        /// <summary>
        /// Active water material: from dual visual when dual block, else from water module.
        /// </summary>
        public Material InstanceWaterMaterial => GetActiveWaterMaterial();

        /// <summary>
        /// Block is fully filled when all colors have reached their max points
        /// </summary>
        public bool IsFullFill => fill.IsFullFill;

        /// <summary>True once any water has been filled into the block (before it is reset/collected).</summary>
        public bool HasFillProgress => fill.HasFillProgress;

        public bool CanCollectBlock()
        {
            return this && !IsFullFill && BlockCollectable;
        }

        public LevelController LevelController => LevelController.Instance;
        public LevelRepresentation OwnerRepresentation => ownerRepresentation;
        public Rigidbody BlockRigidbody => blockRigidbody;
        public IReadOnlyList<Collider> PressColliders => pressColliders;
        public IReadOnlyList<Collider> BlockColliders => blockColliders;


        internal void SetPressCollidersSuppressed(bool suppressed, object source)
        {
            if (source == null)
                return;

            if (suppressed)
                pressColliderSuppressSources.Add(source);
            else
                pressColliderSuppressSources.Remove(source);

            ApplyPressColliderEnabledState();
        }

        private void ApplyPressColliderEnabledState()
        {
            bool enable = pressColliderSuppressSources.Count == 0;
            if (pressColliders == null)
                return;

            for (int i = 0; i < pressColliders.Count; i++)
            {
                Collider col = pressColliders[i];
                if (col)
                    col.enabled = enable;
            }
        }

        /// <summary>Applies the same rigidbody defaults used for blocks and combine-group roots (kinematic grid movement).</summary>
        public static void ApplyRigidbodySettings(Rigidbody rb)
        {
            if (!rb) return;
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation |
                             RigidbodyConstraints.FreezePositionY;
        }

        /// <summary>Copies Rigidbody motion/collision settings; falls back to defaults when source is null.</summary>
        public static void CopyRigidbodySettings(Rigidbody source, Rigidbody target)
        {
            if (!target) return;
            if (!source)
            {
                ApplyRigidbodySettings(target);
                return;
            }

            target.mass = source.mass;
            target.linearDamping = source.linearDamping;
            target.angularDamping = source.angularDamping;
            target.useGravity = source.useGravity;
            target.isKinematic = source.isKinematic;
            target.interpolation = source.interpolation;
            target.collisionDetectionMode = source.collisionDetectionMode;
            target.constraints = source.constraints;
        }
        public Transform ModelParentTransform => meshRenderer.transform.parent;

        /// <summary>
        /// Transform the pick "lift" animates: the block model by default, or an effect-supplied
        /// visual (e.g. the container box) when the block's own renderers are hidden.
        /// </summary>
        public Transform ModelLiftTransform
        {
            get
            {
                Transform liftOverride = effectController.GetModelLiftTransform();
                return liftOverride ? liftOverride : ModelParentTransform;
            }
        }

        public event SimpleCallback BlockCollected;
        public event SimpleCallback DestroyBlockCompleted;
        public event Action<LevelBlockBehavior> FullFilledAfterAnimation;
        /// <summary>Fires once when this block's spawn (scale-in) animation has completed.</summary>
        public event Action<LevelBlockBehavior> SpawnAnimationCompleted;
        private bool spawnAnimationCompleted;
        /// <summary>True once the spawn (scale-in) animation has finished (or was skipped/disabled).</summary>
        public bool IsSpawnAnimationCompleted => spawnAnimationCompleted;

        private bool interactionHidden;
        private int interactionTweenCount;
        public BlockInteractionState InteractionState { get; private set; } = BlockInteractionState.Idle;
        /// <summary>Block can be selected as a booster target (visible and not animating).</summary>
        public bool IsBoosterTargetable => InteractionState == BlockInteractionState.Idle;

        public BlockTheme BlockTheme => blockTheme;
        public int BlockId => blockId;
        public BlockState BlockState => fill.State;

        private void Awake()
        {
            EnsureVisualController();
        }

        private BlockVisualController Visual
        {
            get
            {
                EnsureVisualController();
                return visual;
            }
        }

        private void EnsureVisualController()
        {
            if (waterVisualModule == null)
                waterVisualModule = GetComponentInChildren<WaterVisualModule>();
            if (visual != null)
                return;
            visual = new BlockVisualController(meshRenderer, meshGlass, outerRenderer, waterVisualModule, waterMinMaxConfig);
        }

        public void Init(BlockData blockData, int blockId, LevelRepresentation representation)
        {
            originalPosition = transform.localPosition;
            ownerRepresentation = representation;
            blockRigidbody = GetComponent<Rigidbody>();
            if (!blockRigidbody)
                blockRigidbody = gameObject.AddComponent<Rigidbody>();
            ApplyRigidbodySettings(blockRigidbody);
#if UNITY_EDITOR
            if (Validate(blockData)) return;
#endif

            this.blockConfig = blockData;
            this.blockId = blockId;

            float fillingWaterSpeed = LevelController.Instance.LevelGeneralConfig.FillingWaterSpeed;

            EnsureVisualController();
            // Construction order matters: fill depends on visual + effects.
            effectController = new BlockEffectController(this);
            fill = new BlockFillController(this, figure, waterVisualModule, Visual, effectController, fillingWaterSpeed);
            fill.InitMaxPoints();

            if (Application.isPlaying)
            {
                disappearEasing = Ease.GetCustomEasingFunction("BlockDisappear");
            }
            MeshWater.enabled = false;
            blockTheme = LevelController.Instance.BlockTheme;
        }
#if UNITY_EDITOR
        private bool Validate(BlockData blockData)
        {
            if (ModelParentTransform.localPosition != colliderParent.localPosition)
            {
                Debug.LogError($"Block {blockData.Prefab.name} must have colliderParent same position with meshRenderer.parent", this);
                return true;
            }

            return false;
        }
#endif

        /// <summary>
        /// Gets available points for a specific color. For single color blocks, returns total available.
        /// For dual color blocks, returns available points for that specific color.
        /// </summary>
        public int GetAvailablePoint(BlockColor color) => fill.GetAvailablePoint(color);

        /// <summary>
        /// Gets total available points across all colors (for backward compatibility)
        /// </summary>
        public int AvailablePoint => fill.AvailablePoint;

        public void SetVisible(bool isOn, bool includeOuter = true)
        {
            Visual.SetVisible(isOn, includeOuter);

            if (!isOn && includeOuter)
            {
                interactionHidden = true;
                if (interactionTweenCount == 0)
                    InteractionState = BlockInteractionState.Hidden;
                return;
            }

            if (isOn && includeOuter)
            {
                interactionHidden = false;
                if (interactionTweenCount == 0)
                    InteractionState = BlockInteractionState.Idle;
            }
        }

        public void BeginInteractionTween()
        {
            interactionTweenCount++;
            InteractionState = BlockInteractionState.Tween;
        }

        public void EndInteractionTween()
        {
            if (interactionTweenCount <= 0)
                return;

            interactionTweenCount--;
            if (interactionTweenCount > 0)
                return;

            InteractionState = interactionHidden
                ? BlockInteractionState.Hidden
                : BlockInteractionState.Idle;
        }

        public void ChangeBodyMaterial(Material newMaterial) => Visual.ChangeBodyMaterial(newMaterial);

        public void ChangeOuterMaterial(Material newMaterial) => Visual.ChangeOuterMaterial(newMaterial);

        public void RestoreOuterMaterial() => Visual.RestoreOuterMaterial();

        public void ChangeGlassMaterial(Material newMaterial) => Visual.ChangeGlassMaterial(newMaterial);

        public void ChangeWaterMaterial(Material newMaterial) => Visual.ChangeWaterMaterial(newMaterial);

        public void SetKinematicMode(bool isKinematic)
        {
            if (blockRigidbody)
                blockRigidbody.isKinematic = isKinematic;
        }

        public void StopBlockPhysics()
        {
            if (!blockRigidbody) return;
            if (!blockRigidbody.isKinematic)
            {
                blockRigidbody.linearVelocity = Vector3.zero;
                blockRigidbody.angularVelocity = Vector3.zero;
            }
            blockRigidbody.isKinematic = true;
        }

        /// <summary>Caches the current parent before reparenting into a group.</summary>
        internal void CacheParentBeforeGrouping()
        {
            if (parentBeforeCombineGroup) return;
            parentBeforeCombineGroup = transform.parent;
        }

        internal Transform ConsumeParentBeforeGrouping()
        {
            Transform p = parentBeforeCombineGroup;
            parentBeforeCombineGroup = null;
            return p;
        }

        /// <summary>Reparents into a group: drops this block's Rigidbody and uses the shared group body.</summary>
        public void SetGroupRigidbody(Rigidbody groupRb)
        {
            if (!groupRb) return;
            if (blockRigidbody && blockRigidbody.gameObject == gameObject && blockRigidbody != groupRb)
            {
                DestroyImmediate(blockRigidbody);
            }
            blockRigidbody = groupRb;
        }

        /// <summary>Restores an individual kinematic Rigidbody on this block after leaving a group.</summary>
        public void RestoreIndividualRigidbody()
        {
            if (blockRigidbody && blockRigidbody.gameObject == gameObject)
                return;
            Rigidbody sourceRb = blockRigidbody;
            blockRigidbody = gameObject.AddComponent<Rigidbody>();
            CopyRigidbodySettings(sourceRb, blockRigidbody);
        }

        public void SetColor(BlockColorData colorData)
        {
            this.originColorConfig = colorData;
            Visual.ChangeBodyMaterial(colorData.Material);
            Visual.SetInnerColor(colorData);
        }

        public void SetInnerColor(BlockColorData overrideData = null)
        {
            Visual.SetInnerColor(overrideData ?? originColorConfig);
        }

        public void OnObjectClicked()
        {
            if (fill.State != BlockState.Normal) return;
            LevelController.OnObjectPicked(this);
        }

        public bool CanBeClicked() => effectController.CanBeClicked();

        public void OnClickBlocked()
        {
            shakeTweenCase.KillActive();
            transform.localPosition = originalPosition;
            shakeTweenCase = transform.DOShake(0.04f, 0.15f).OnComplete(() =>
            {
                if (gameObject)
                {
                    transform.localPosition = originalPosition;
                }
            });

            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Deny);

            Services.AudioService.PlaySound(effectController.GetDenyClickAudioId());
        }

        private void OnDestroy()
        {
            shakeTweenCase.KillActive();
        }

        public IReadOnlyList<LevelBlockBehavior> GetLinkedBlocks() => effectController.GetLinkedBlocks();

        public bool MoveMultiplyObjects() => effectController.MoveMultiplyObjects();

        public void OnBlockEnteredGate(LevelBlockBehavior levelBlockBehavior, GateBehavior gateBehavior)
            => effectController.OnBlockEnteredGate(levelBlockBehavior, gateBehavior);

        public void OnBlockDestructed(LevelBlockBehavior levelBlockBehavior)
            => effectController.OnBlockDestructed(levelBlockBehavior);

        public void ResetFillProgress() => fill.ResetFillProgress();

        /// <summary>
        /// Fill water of a specific color into the block
        /// </summary>
        public void FillWater(BlockColor color, int collectPoints) => fill.FillWater(color, collectPoints);

        /// <summary>
        /// Update visual fill amount for a specific color
        /// </summary>
        public void FillWaterVisual(BlockColor color, int collectPoints) => fill.FillWaterVisual(color, collectPoints);


        public BlockGateState OnGateEntered(GateBehavior gateBehavior)
        {
            var blockGateState = gateBehavior.CanGoThroughGate(this);
            if (blockGateState != BlockGateState.Enterable)
                return blockGateState;

            return effectController.AllowGateEntered();
        }

        /// <summary>
        /// Gate passability from this block's active effects only; the gate's own
        /// <see cref="GateBehavior.CanGoThroughGate"/> check is the caller's responsibility. Used by
        /// the near-gate hot path after <see cref="LevelEnvironmentSpawner.NearGate"/> already
        /// verified the gate, to avoid re-running CanGoThroughGate via <see cref="OnGateEntered"/>.
        /// </summary>
        public BlockGateState AllowGateEntered() => effectController.AllowGateEntered();

        /// <summary>Called by the spawner when this block's spawn (scale-in) animation finishes or is skipped.</summary>
        public void NotifySpawnAnimationCompleted()
        {
            if (spawnAnimationCompleted) return;
            spawnAnimationCompleted = true;
            SpawnAnimationCompleted?.Invoke(this);
        }

        public void OnMapSpawnCompleted()
        {
            effectController.OnMapSpawnCompleted();
            waterVisualModule.OnMapSpawnCompleted();
            RefreshBlockWaterVisibility();
        }

        private void RefreshBlockWaterVisibility()
        {
            SetVisibleBlockWater(!effectController.AnyHidesWater());
        }

        public BlockColor GetActiveBlockColor() => fill.GetActiveBlockColor();

        public BlockColor GetSecondaryBlockColor() => fill.GetSecondaryBlockColor();

        /// <summary>True when the active color, or (for an active dual effect) the secondary color, matches.</summary>
        public bool MatchesActiveOrSecondaryColor(BlockColor color) => fill.MatchesActiveOrSecondary(color);

        /// <summary>
        /// Sets the secondary color and redistributes max points for dual color block.
        /// Called by DualBlockEffectBehavior when effect is applied.
        /// </summary>
        public void SetSecondaryColor(BlockColor secondaryColor) => fill.SetSecondaryColor(secondaryColor);

        /// <summary>
        /// Clears the secondary color and restores all points to primary color.
        /// </summary>
        public void ClearSecondaryColor() => fill.ClearSecondaryColor();

        /// <summary>
        /// Sets the dual visual reference. Called by DualBlockEffectBehavior.
        /// </summary>
        public void SetDualVisual(DualBlockVisualBehavior visualBehavior) => Visual.SetDualVisual(visualBehavior);

        public bool HasDualVisual() => Visual.HasDualVisual();

        public BlockColor GetColorFromPosition(Vector3 position)
            => Visual.GetColorFromPosition(position, GetActiveBlockColor());

        /// <summary>
        /// Returns the water material currently used for display: dual visual's water for the active color when dual, else single water module.
        /// </summary>
        public Material GetActiveWaterMaterial() => Visual.GetActiveWaterMaterial(GetActiveBlockColor());

        /// <summary>
        /// Apply blocked material to water (single module or both dual modules). Used by BlockerEffectBehavior.
        /// </summary>
        public void ApplyBlockedWaterMaterial(Material blockMaterial) => Visual.ApplyBlockedWaterMaterial(blockMaterial);

        public void SetVisibleBlockWater(bool isActive) => Visual.SetVisibleBlockWater(isActive);

        public bool IsWaterVisible => Visual.IsWaterVisible();

        /// <summary>
        /// Checks if block can match the gate color (primary or secondary).
        /// No allocations.
        /// </summary>
        public BlockGateState CanMatchGateColor(BlockColor gateColor)
        {
            if (fill.MatchesActiveOrSecondary(gateColor))
                return BlockGateState.Enterable;
            if (originColorConfig.Type == gateColor)
            {
                // Switch-layer blocks fail their own way (revive swaps/fills), so they map to a distinct
                // lose reason; plain layered blocks keep the original layer-fail state.
                return HasEffect(BlockEffectType.SwitchLayer)
                    ? BlockGateState.BlockSwitchLayerMissMatchColor
                    : BlockGateState.BlockLayerMissMatchColor;
            }
            return BlockGateState.MissMatchColor;
        }

        public Vector2Int[] GetOccupiedCells() => GetOccupiedCells(MatrixPosition);

        public Vector2Int[] GetOccupiedCells(Vector2Int originPosition)
        {
            Vector2Int[] occupiedCells = new Vector2Int[figure.ActivePoints];

            Vector2Int size = figure.Size;
            Vector2Int currentPosition = originPosition;

            int tempIndex = 0;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int index = x + y * size.x;

                    if (figure.Points[index].IsFilled)
                    {
                        occupiedCells[tempIndex] = new Vector2Int(currentPosition.x + x, currentPosition.y + y);

                        tempIndex++;
                    }
                }
            }

            return occupiedCells;
        }

        public bool IsOverlapsCell(Vector2Int checkCell)
        {
            return IsOverlapsCell(checkCell, MatrixPosition);
        }

        public bool IsOverlapsCell(Vector2Int checkCell, Vector2Int originPosition)
        {
            return figure != null && figure.IsCellOccupiedAt(originPosition, checkCell);
        }


        private void OnDestroyBlockCompleted()
        {
            // Unity's overloaded == handles destroyed objects, but ?. does not — guard explicitly.
            if (this && gameObject)
                gameObject.SetActive(false);
            effectController.DisableAll();
            ownerRepresentation?.EvaluateGameWinLose();
            DestroyBlockCompleted?.Invoke();
        }

        /// <summary>Begins the destruct scale animation. Entry point for the fill collaborator.</summary>
        internal void BeginDestructAnimation() => StartDestructAnimation();

        /// <summary>Raises <see cref="FullFilledAfterAnimation"/>; events can only be invoked from the declaring type.</summary>
        internal void RaiseFullFilledAfterAnimation() => FullFilledAfterAnimation?.Invoke(this);

        /// <summary>Raises <see cref="BlockCollected"/>; events can only be invoked from the declaring type.</summary>
        internal void RaiseBlockCollected() => BlockCollected?.Invoke();

        private void StartDestructAnimation()
        {
            if (!meshRenderer || !meshRenderer.transform )
            {
                OnDestroyBlockCompleted();
                return;
            }

            SimpleCallback onSingleAnimationCompleted = null;
            onSingleAnimationCompleted = () =>
            {
                if (this && gameObject)
                    OnDestroyBlockCompleted();
            };

            PlayDestroyScaleTween(meshRenderer.transform.parent, onSingleAnimationCompleted);
            PlayDestroyScaleTween(colliderParent, null);
        }

        private void PlayDestroyScaleTween(Transform target, SimpleCallback onCompleted)
        {
            if (!target)
                return;

            target.DOScale(DestroyScaleTarget, DestroyScaleDuration)
                .OnComplete(onCompleted)
                .SetCustomEasing(disappearEasing);
        }

        public bool HasEffect(BlockEffectType effectType) => effectController.HasEffect(effectType);
        public bool IsHeldByContainer() => effectController.IsHeldByContainer();
        
        public BlockEffectBehavior GetEffect(BlockEffectType effectType) => effectController.GetEffect(effectType);

        public T GetEffect<T>(BlockEffectType effectType) where T : BlockEffectBehavior
            => effectController.GetEffect<T>(effectType);

        public void ApplyEffect(BlockEffectBehavior effect) => effectController.Add(effect);

        public List<BlockEffectBehavior> GetEffectsByRule(int amount, Func<BlockEffectBehavior, bool> effectRule, bool sortAscending = true)
            => effectController.GetEffectsByRule(amount, effectRule, sortAscending);

        public void DisableEffect(DisableSource source, int amount, Func<BlockEffectBehavior, bool> effectRule, bool sortAscending = true)
            => effectController.DisableEffect(source, amount, effectRule, sortAscending);

        public bool HasActiveEffect() => effectController.HasActiveEffect();


#if UNITY_EDITOR
        [Sirenix.OdinInspector.Button(Sirenix.OdinInspector.ButtonSizes.Medium)]
        private void AutoAssignCollidersByLayer()
        {
            if (GameLayer.LAYER_BLOCK_TARGET < 0 || GameLayer.LAYER_BLOCK < 0)
            {
                Debug.LogError($"[LevelBlockBehavior] Missing layers. BlockTarget={GameLayer.LAYER_BLOCK_TARGET}, Block={GameLayer.LAYER_BLOCK}. Check Project Settings > Tags and Layers.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Auto Assign Colliders By Layer");

            pressColliders ??= new List<Collider>();
            blockColliders ??= new List<Collider>();

            pressColliders.Clear();
            blockColliders.Clear();

            Collider[] colliders = transform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (!col)
                    continue;

                int layer = col.gameObject.layer;
                if (layer == GameLayer.LAYER_BLOCK_TARGET)
                {
                    pressColliders.Add(col);
                }
                else if (layer == GameLayer.LAYER_BLOCK)
                {
                    blockColliders.Add(col);
                }
            }

            ApplyPressColliderEnabledState();

            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        private void OnDrawGizmos()
        {
            if (figure == null)
                return;

            var size = figure.Size;

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    int index = x + y * size.x;

                    if (!figure.Points.IsNullOrEmpty() && figure.Points.IsInRange(index) && figure.Points[index].IsFilled)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(transform.position + new Vector3(x, 0.25f, y), new Vector3(1f, 0.5f, 1f));
                    }
                }
            }
        }
    }
}
