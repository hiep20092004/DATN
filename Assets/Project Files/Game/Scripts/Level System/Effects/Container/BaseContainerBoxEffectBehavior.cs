using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WaterFlow.Game
{
    public abstract class BaseContainerBoxEffectBehavior<T1>
        : BlockEffectBehavior<ContainerBoxBlockEffectDataBase>, IGroupableEffect where T1 : BaseContainerBoxEffectConfig
    {
        [SerializeField] protected BaseContainerBoxGroupVisual groupVisual;
        [SerializeField] private Transform textVisualRoot;
        [SerializeField] private TextMeshProUGUI turnsText;
        [SerializeField] private TextMeshProUGUI turnsShadowText;

#if UNITY_EDITOR
        [SerializeField]
#endif
        private int remainingStrength;

        protected T1 config;
        private Tweener punchTween;
        private Tweener spawnTween;
        private Tween dissolveDelayTween;
        private bool restoredLinkedVisualState;
        private BlockGroupPhysicsBarrier physicsBarrier;
        private bool isDissolveScheduled;
        private bool isClearAnimating;
        private bool hasPendingClearRegistered;
        private int pendingReleasedSpawnCompletions;
        private readonly HashSet<LevelBlockBehavior> suppressedPressColliderMembers = new();
        private readonly HashSet<GameObject> disabledBoundsOverlapObstacles = new();
        private readonly HashSet<Vector2Int> barrierOccupiedCells = new();
        private LevelRepresentation level;

        public int GroupId => Data.containerBoxID;
        public LevelBlockBehavior Owner => linkedBlock;
        public BlockGroup ActiveGroup { get; private set; }

        public BlockGroupOccupiedBounds GroupOccupiedBounds =>
            ActiveGroup?.OccupiedBounds ?? default;

        private static readonly AudioId[] BreakAudio =
        {
            AudioId.Obstacle_Box_01,
            AudioId.Obstacle_Box_02,
            AudioId.Obstacle_Box_03,
        };

        private static int lastTimePlayAudio = -1;

        protected abstract IGroupClickReceiver GetGroupClickReceiver();
        
        public void OnGroupFormed(BlockGroup group, LevelRepresentation level)
        {
            ActiveGroup = group;
            this.level = level;
            remainingStrength = GetVisualOwnerClearCount(group);
            HideLinkedBlockVisualsForPlayMode();
            SuppressPressCollidersForGroup(group, true);

            if (!IsVisualOwner(group))
            {
                groupVisual?.Hide();
                SetRemainingTextVisible(false);
                return;
            }

            SetRemainingTextVisible(true);
            SetRemainingText(remainingStrength);

            if (groupVisual)
            {
                groupVisual.Configure(config, GetGroupClickReceiver());
                groupVisual.transform.SetParent(group.GroupObject.transform, true);
                groupVisual.transform.localScale = Vector3.one;
            }

            SetupGroupPhysicsBarrier(group);
            PlayGroupVisualSpawnTween();
            UpdateBoundsOverlapElementsVisibility(group.OccupiedBounds);
        }

        public void OnGroupDissolved()
        {
            RestoreBoundsOverlapElementsVisibility();
            StopGroupVisualSpawnTween();
            ReleaseGroupPhysicsBarrier();
            SuppressPressCollidersForGroup(ActiveGroup, false);

            if (ActiveGroup != null)
                ActiveGroup.SetBoundsChangedListener(null);

            groupVisual?.Hide();
            ActiveGroup = null;
            DisableEffect();
        }

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<T1>();
            if (!config)
            {
                Debug.LogError($"Missing config data for {GetType().Name}");
                return;
            }
            restoredLinkedVisualState = false;

            if (!groupVisual)
                groupVisual = GetComponentInChildren<BaseContainerBoxGroupVisual>(true);

            groupVisual?.Hide();
        }

        // The block renderers are hidden and only the visual owner's group visual is on screen, so
        // the pick lift must raise that visual rather than the (invisible) block model.
        public override Transform GetModelLiftTransform()
        {
            if (ActiveGroup != null && groupVisual && IsVisualOwner(ActiveGroup))
                return groupVisual.transform;

            return null;
        }

        public override bool IsDestructible() => false;

        // Members are hidden and their press colliders suppressed; the only interactive surface is the
        // group visual, which routes clicks through ConfigureGroupVisualInteraction. The block itself is
        // never directly clickable.
        public override bool IsClickable() => false;

        public override BlockGateState AllowGateEntered()
        {
            if(!ActiveGroup.IsFinish) return BlockGateState.Blocked;
            return BlockGateState.Enterable;
        }

        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            if (ActiveGroup == null) return;
            if (!IsVisualOwner(ActiveGroup)) return;

            // Members filling never count; CountsTowardClear lets variants narrow this further (e.g. by color).
            if (IsGroupMember(levelBlockBehavior)) return;
            if (!CountsTowardClear(levelBlockBehavior, filledColor)) return;

            remainingStrength--;
            if (remainingStrength <= 0)
            {
                ActiveGroup.FinishGroup();
                PlayBreakAudio();
                SetRemainingText(0);
                PlayRemainingTextPunch();
                ScheduleDissolveSelf(GetClearStartDelayAfterTurnPunch());
                return;
            }

            SetRemainingText(remainingStrength);
            PlayRemainingTextPunch();
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            punchTween?.Kill();
            punchTween = null;
            StopGroupVisualSpawnTween();
            StopRemainingTextPunch();

            ReleaseGroupPhysicsBarrier();
            ActiveGroup?.SetBoundsChangedListener(null);
            groupVisual?.Hide();
            SetRemainingTextVisible(false);
            StopDissolveDelay();
            isDissolveScheduled = false;
            isClearAnimating = false;
            pendingReleasedSpawnCompletions = 0;
            SuppressPressCollidersForGroup(ActiveGroup, false);
            RestoreBoundsOverlapElementsVisibility();
            ActiveGroup = null;
            RestoreLinkedBlockVisualsForPlayMode();
        }

        public override void OnNewEffectAddedToBlock(BlockEffectBehavior effect)
        {
            if (!effect || !effect.IsActive) return;
            if (!Application.isPlaying)
            {
                effect.gameObject.SetActive(false);
                return;
            }
            effect.OnToggleVisual(false, ToggleVisualSource.Container);
        }


        /// <summary>
        /// Extra gate on whether a fully-filled non-member block decrements the counter. Base counts every
        /// non-member fill; a color-gated variant (e.g. "clear blue blocks to break") overrides this to
        /// require <paramref name="filledColor"/> to match the configured one.
        /// </summary>
        protected virtual bool CountsTowardClear(LevelBlockBehavior filledBlock, BlockColor filledColor) => true;

        /// <summary>
        /// Only the visual owner's <see cref="remainingStrength"/> decrements during play (see
        /// <see cref="OnBlockFullFilledAfterAnimationGlobal"/>), so members must read the group's live
        /// count from the owner. Returns null (drop from snapshot) once the box is logically broken.
        /// </summary>
        public override BlockEffectData GetCurrentEffectData()
        {
            int currentStrength = GetLiveGroupRemainingStrength();
            if (currentStrength <= 0)
                return null;

            var clone = (ContainerBoxBlockEffectDataBase)effectData.Clone();
            clone.clearCount = currentStrength;
            return clone;
        }

        private int GetLiveGroupRemainingStrength()
        {
            if (ActiveGroup == null || ActiveGroup.Members.Count == 0)
                return remainingStrength;

            LevelBlockBehavior visualOwner = ActiveGroup.Members[0];
            if (!visualOwner)
                return remainingStrength;

            foreach (BlockEffectBehavior effect in visualOwner.Effects)
            {
                if (effect is BaseContainerBoxEffectBehavior<T1> ownerBox &&
                    ownerBox.IsActive && ownerBox.Type == Type && ownerBox.GroupId == GroupId)
                {
                    return ownerBox.remainingStrength;
                }
            }

            return remainingStrength;
        }

        protected bool IsVisualOwner(BlockGroup group) =>
            group.Members.Count > 0 && group.Members[0] == Owner;

        protected bool IsGroupMember(LevelBlockBehavior block)
        {
            if (ActiveGroup == null || !block)
                return false;

            IReadOnlyList<LevelBlockBehavior> members = ActiveGroup.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == block)
                    return true;
            }

            return false;
        }

        private int GetVisualOwnerClearCount(BlockGroup group)
        {
            if (group == null || group.Members == null || group.Members.Count == 0)
                return Data.clearCount;

            LevelBlockBehavior visualOwner = group.Members[0];
            if (!visualOwner)
                return Data.clearCount;

            foreach (BlockEffectBehavior effect in visualOwner.Effects)
            {
                if (!effect || !effect.IsActive)
                    continue;

                if (effect.EffectData is ContainerBoxBlockEffectDataBase containerBoxEffect &&
                    containerBoxEffect.Type == Type &&
                    containerBoxEffect.containerBoxID == GroupId)
                {
                    return containerBoxEffect.clearCount;
                }
            }

            return Data.clearCount;
        }

        private void SetupGroupPhysicsBarrier(BlockGroup group)
        {
            if (group == null || group.GroupObject == null)
                return;

            physicsBarrier = group.GroupObject.GetComponent<BlockGroupPhysicsBarrier>();
            if (!physicsBarrier)
                physicsBarrier = group.GroupObject.AddComponent<BlockGroupPhysicsBarrier>();

            physicsBarrier.gameObject.layer = GameLayer.LAYER_BLOCK;
            physicsBarrier.Configure(config.CellColliderPrefab);

            if (!config.CellColliderPrefab)
                Debug.LogError($"[{GetType().Name}] CellColliderPrefab not assigned on {config.name}; group gaps will have no collider.", this);

            group.SetBoundsChangedListener(OnGroupBoundsChanged);
            OnGroupBoundsChanged(group.OccupiedBounds);
        }

        private void ReleaseGroupPhysicsBarrier()
        {
            if (physicsBarrier)
                physicsBarrier.Release();

            physicsBarrier = null;
        }

        private void OnGroupBoundsChanged(BlockGroupOccupiedBounds bounds)
        {
            groupVisual?.Apply(bounds);
            UpdateGroupPhysicsBarrier(bounds);
            UpdateBoundsOverlapElementsVisibility(bounds);
        }

        private void UpdateGroupPhysicsBarrier(BlockGroupOccupiedBounds bounds)
        {
            if (!physicsBarrier)
                return;

            BuildBarrierOccupiedCells(bounds, barrierOccupiedCells);
            physicsBarrier.ApplyBounds(bounds, barrierOccupiedCells, GetBarrierCellWorldY(bounds));
        }

        /// <summary>
        /// Collects every cell inside <paramref name="bounds"/> already backed by its own collider, so the
        /// barrier only fills the genuinely empty perimeter gaps.
        /// </summary>
        private void BuildBarrierOccupiedCells(BlockGroupOccupiedBounds bounds, HashSet<Vector2Int> result)
        {
            result.Clear();
            if (!bounds.IsValid)
                return;

            if (ActiveGroup != null)
            {
                IReadOnlyList<LevelBlockBehavior> members = ActiveGroup.Members;
                for (int i = 0; i < members.Count; i++)
                {
                    LevelBlockBehavior member = members[i];
                    if (!member)
                        continue;

                    Vector2Int[] cells = member.GetOccupiedCells();
                    for (int c = 0; c < cells.Length; c++)
                        result.Add(cells[c]);
                }
            }

            // Hidden obstacles lose their collider; the barrier must seal those cells instead.
            if (ShouldHideBoundsOverlapObstacles())
                return;

            IElementProvider elementProvider = linkedBlock?.OwnerRepresentation;
            if (elementProvider == null)
                return;

            IReadOnlyList<ObstacleBehavior> obstacles =
                elementProvider.GetElements<ObstacleBehavior>(ElementType.Obstacle);
            for (int i = 0; i < obstacles.Count; i++)
            {
                ObstacleBehavior obstacle = obstacles[i];
                if (obstacle && IsCellInsideBounds(obstacle.Position, bounds))
                    result.Add(obstacle.Position);
            }
        }

        private static bool IsCellInsideBounds(Vector2Int cell, BlockGroupOccupiedBounds bounds) =>
            cell.x >= bounds.MinCell.x && cell.x <= bounds.MaxCell.x &&
            cell.y >= bounds.MinCell.y && cell.y <= bounds.MaxCell.y;

        // Cell colliders sit at the member blocks' world height so they align with where a block occupies
        // a cell; the box visual rides above them at its own configured offset.
        private float GetBarrierCellWorldY(BlockGroupOccupiedBounds bounds)
        {
            if (ActiveGroup != null)
            {
                IReadOnlyList<LevelBlockBehavior> members = ActiveGroup.Members;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i])
                        return members[i].transform.position.y;
                }
            }

            return bounds.WorldCenter.y;
        }

        private bool ShouldHideBoundsOverlapObstacles()
        {
            if (Application.isPlaying)
                return true;

            return config.VisualConfig.ShowFullAlpha;
        }

        private void UpdateBoundsOverlapElementsVisibility(BlockGroupOccupiedBounds bounds)
        {
            if (!IsVisualOwner(ActiveGroup))
                return;

            IElementProvider elementProvider = linkedBlock?.OwnerRepresentation;
            if (elementProvider == null)
            {
                RestoreBoundsOverlapElementsVisibility();
                return;
            }

            BaseContainerBoxGroupVisual.SetBoundsOverlapObstaclesActive(
                bounds,
                elementProvider,
                disabledBoundsOverlapObstacles,
                ShouldHideBoundsOverlapObstacles());
        }

        private void RestoreBoundsOverlapElementsVisibility() =>
            BaseContainerBoxGroupVisual.RestoreBoundsOverlapObstacles(disabledBoundsOverlapObstacles);

        private void SuppressPressCollidersForGroup(BlockGroup group, bool suppressed)
        {
            if (suppressed)
            {
                if (group == null || group.Members == null)
                    return;

                for (int i = 0; i < group.Members.Count; i++)
                {
                    LevelBlockBehavior member = group.Members[i];
                    if (!member)
                        continue;

                    member.SetPressCollidersSuppressed(true, this);
                    suppressedPressColliderMembers.Add(member);
                }
                return;
            }

            if (suppressedPressColliderMembers.Count == 0)
                return;

            foreach (LevelBlockBehavior member in suppressedPressColliderMembers)
            {
                if (member)
                    member.SetPressCollidersSuppressed(suppressed, this);
            }

            suppressedPressColliderMembers.Clear();
        }

        private void SetRemainingText(int value)
        {
            string text = value.ToString();
            if (turnsText)
                turnsText.text = text;
            if (turnsShadowText)
                turnsShadowText.text = text;
        }

        private void SetRemainingTextVisible(bool visible)
        {
            if (textVisualRoot)
                textVisualRoot.gameObject.SetActive(visible);
        }

        private void PlayRemainingTextPunch()
        {
            if (!textVisualRoot)
                return;

            punchTween?.Kill();
            textVisualRoot.localScale = Vector3.one;
            punchTween = textVisualRoot
                .DOPunchScale(Vector3.one * config.VisualConfig.TurnPunchScale, config.VisualConfig.TurnPunchDuration)
                .SetEase(Ease.OutSine);
        }

        private void StopRemainingTextPunch()
        {
            punchTween?.Kill();
            punchTween = null;

            if (textVisualRoot)
                textVisualRoot.localScale = Vector3.one;
        }

        private float GetClearStartDelayAfterTurnPunch()
        {
            if (!config)
                return 0f;

            return config.VisualConfig.TurnPunchDuration * Mathf.Clamp01(config.VisualConfig.ClearStartAfterTurnPunchNormalized);
        }

        private void PlayGroupVisualSpawnTween()
        {
            EnvironmentData.SpawnTweenConfig tweenConfig =
                linkedBlock?.OwnerRepresentation?.EnvironmentData?.ContainerBoxGroupVisualSpawnTween;
            if (!groupVisual || tweenConfig == null || !Application.isPlaying || !tweenConfig.Enabled)
                return;

            Transform targetTransform = groupVisual.transform;
            spawnTween?.Kill();
            targetTransform.localScale = Vector3.zero;
            spawnTween = targetTransform.DOScale(1f, tweenConfig.Duration)
                .SetEase(tweenConfig.Ease)
                .SetDelay(tweenConfig.Delay);
        }

        private void StopGroupVisualSpawnTween()
        {
            spawnTween?.Kill();
            spawnTween = null;

            if (groupVisual)
                groupVisual.transform.localScale = Vector3.one;
        }

        private void DissolveSelf()
        {
            BlockGroup group = ActiveGroup;
            if (group == null) return;

            if (!IsVisualOwner(group))
            {
                return;
            }

            if (isClearAnimating)
                return;
            isClearAnimating = true;

            // The movable variant can be mid-drag when the counter hits zero; settle the drag first so
            // the clear animation and dissolve never destroy the group object the movement manager is
            // still driving.
            ForceReleaseGroupIfPicked(group);

            StopRemainingTextPunch();
            SetRemainingTextVisible(false);

            if (groupVisual && Application.isPlaying)
                groupVisual.PlayClearAnimation(group.OccupiedBounds, () => DissolveGroupImmediate(group));
            else
            {
                groupVisual?.SpawnClearVfx(group.OccupiedBounds);
                DissolveGroupImmediate(group);
            }
        }

        private static void ForceReleaseGroupIfPicked(BlockGroup group)
        {
            if (!Application.isPlaying)
                return;

            LevelController levelController = LevelController.Instance;
            BlockMovementManager movementManager = levelController ? levelController.MovementManager : null;
            if (movementManager == null || !movementManager.IsBlockPicked)
                return;

            LevelBlockBehavior pickedBlock = movementManager.BlockBehavior;
            if (!pickedBlock)
                return;

            foreach (LevelBlockBehavior member in group.Members)
            {
                if (member != pickedBlock)
                    continue;

                levelController.OnObjectReleased(snapToCurrentPosition: true);
                return;
            }
        }

        private void DissolveGroupImmediate(BlockGroup group)
        {
            if (group == null) return;

            var snapshot = new List<LevelBlockBehavior>(group.Members);

            group.Dissolve();
            foreach (LevelBlockBehavior member in snapshot)
            {
                if (!member) continue;
                foreach (BlockEffectBehavior effect in member.Effects)
                {
                    if (effect is IGroupableEffect groupable && groupable.ActiveGroup == group)
                        groupable.OnGroupDissolved();
                }
            }

            level?.FormCombineGroupsForBlocks(snapshot);

            BeginReleasedBlocksSpawnPending(snapshot);
        }

        private void BeginReleasedBlocksSpawnPending(List<LevelBlockBehavior> members)
        {
            if (members == null || members.Count == 0 || level == null)
                return;

            int releasableCount = 0;
            for (int i = 0; i < members.Count; i++)
            {
                LevelBlockBehavior member = members[i];
                if (member && member.CanCollectBlock())
                    releasableCount++;
            }

            if (releasableCount == 0)
            {
                return;
            }

            pendingReleasedSpawnCompletions = releasableCount;
            RegisterPendingClear();

            for (int i = 0; i < members.Count; i++)
            {
                LevelBlockBehavior member = members[i];
                if (!member || !member.CanCollectBlock())
                    continue;

                level.PlayBlockReleaseSpawnTween(member, NotifyReleasedBlockSpawnComplete);
            }
        }

        private void NotifyReleasedBlockSpawnComplete()
        {
            if (pendingReleasedSpawnCompletions <= 0)
                return;

            pendingReleasedSpawnCompletions--;
            if (pendingReleasedSpawnCompletions > 0)
                return;

            UnregisterPendingClear();
        }

        private void RegisterPendingClear()
        {
            if (hasPendingClearRegistered)
                return;

            hasPendingClearRegistered = true;
            level?.RegisterPendingBlockSpawn();
        }

        private void UnregisterPendingClear()
        {
            if (!hasPendingClearRegistered)
                return;

            hasPendingClearRegistered = false;
            pendingReleasedSpawnCompletions = 0;
            level?.UnregisterPendingBlockSpawn();
        }

        private void ScheduleDissolveSelf(float delay = 0f)
        {
            if (isDissolveScheduled)
                return;

            isDissolveScheduled = true;
            StopDissolveDelay();
            dissolveDelayTween = DOVirtual.DelayedCall(Mathf.Max(0f, delay), () =>
            {
                dissolveDelayTween = null;
                isDissolveScheduled = false;

                if (!isActive || ActiveGroup == null)
                    return;

                DissolveSelf();
            });
        }

        private void StopDissolveDelay()
        {
            dissolveDelayTween?.Kill();
            dissolveDelayTween = null;
        }

        private void PlayBreakAudio()
        {
            if (!Application.isPlaying || Time.frameCount == lastTimePlayAudio)
                return;

            lastTimePlayAudio = Time.frameCount;
            AudioId pickedAudio = BreakAudio[Random.Range(0, BreakAudio.Length)];
            Services.AudioService.PlaySound(pickedAudio);
        }

        private void HideLinkedBlockVisualsForPlayMode()
        {
            if (!Application.isPlaying || !linkedBlock)
                return;

            linkedBlock.SetVisible(false);

            foreach (BlockEffectBehavior effect in linkedBlock.Effects)
            {
                if (!effect || effect == this || !effect.IsActive)
                    continue;
                effect.OnToggleVisual(false, ToggleVisualSource.Container);
            }
        }

        private void RestoreLinkedBlockVisualsForPlayMode()
        {
            if (restoredLinkedVisualState || !Application.isPlaying || !linkedBlock)
                return;

            restoredLinkedVisualState = true;
            linkedBlock.SetVisible(true);

            foreach (BlockEffectBehavior effect in linkedBlock.Effects)
            {
                if (!effect || effect == this)
                    continue;
                effect.OnToggleVisual(true, ToggleVisualSource.Container);
            }
        }
    }
}
