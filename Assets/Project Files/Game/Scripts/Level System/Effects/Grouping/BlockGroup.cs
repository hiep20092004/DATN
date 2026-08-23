using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WaterFlow.Game
{
    /// <summary>
    /// Manages the lifecycle of a shared-rigidbody group for multiple <see cref="LevelBlockBehavior"/> instances.
    /// Handles creating the group GameObject, reparenting blocks under it, sharing a single Rigidbody,
    /// and cleanly dissolving the group when it is no longer needed.
    /// </summary>
    public sealed class BlockGroup
    {
        private readonly List<LevelBlockBehavior> members = new();
        private BlockGroupVisualHost visualHost;
        private Action<BlockGroupOccupiedBounds> boundsChangedListener;
        private bool isFinish;
        public int GroupId { get; }
        public bool IsFinish => isFinish;
        public GameObject GroupObject { get; private set; }
        public Rigidbody SharedRigidbody { get; private set; }
        public IReadOnlyList<LevelBlockBehavior> Members => members;

        /// <summary>
        /// Bounds enclosing all occupied cells of every member. Refreshed when the group is
        /// created or membership changes. Use for container visuals and debugging.
        /// </summary>
        public BlockGroupOccupiedBounds OccupiedBounds { get; private set; }

        private BlockGroup(int groupId) => GroupId = groupId;

        /// <summary>
        /// Creates a group GameObject with a shared Rigidbody, reparents all <paramref name="blocks"/>
        /// under it and assigns the shared Rigidbody to each block via <see cref="LevelBlockBehavior.SetGroupRigidbody"/>.
        /// </summary>
        public static BlockGroup Create(int groupId, List<LevelBlockBehavior> blocks, Transform parent)
        {
            var group = new BlockGroup(groupId);

            var groupGo = new GameObject($"BlockGroup_id{groupId}");
            if (parent)
                groupGo.transform.SetParent(parent, true);

            group.GroupObject = groupGo;

            Rigidbody groupRb = groupGo.AddComponent<Rigidbody>();
            Rigidbody sourceRb = null;
            foreach (LevelBlockBehavior block in blocks)
            {
                if (block && block.BlockRigidbody)
                {
                    sourceRb = block.BlockRigidbody;
                    break;
                }
            }
            LevelBlockBehavior.CopyRigidbodySettings(sourceRb, groupRb);
            group.SharedRigidbody = groupRb;

            foreach (LevelBlockBehavior block in blocks)
                group.AddMember(block);

            group.visualHost = groupGo.AddComponent<BlockGroupVisualHost>();
            group.RefreshOccupiedBounds();

            return group;
        }

        /// <summary>
        /// Registers a single listener (typically the lead effect's visual) notified after each bounds refresh.
        /// </summary>
        public void SetBoundsChangedListener(Action<BlockGroupOccupiedBounds> listener)
        {
            boundsChangedListener = listener;
        }

        public void FinishGroup()
        {
            isFinish = true;
        }
        
        /// <summary>
        /// Recomputes <see cref="OccupiedBounds"/> from current members and notifies listeners.
        /// </summary>
        public void RefreshOccupiedBounds()
        {
            if (BlockGroupBoundsCalculator.TryCalculate(members, out BlockGroupOccupiedBounds bounds))
                OccupiedBounds = bounds;
            else
                OccupiedBounds = default;

            visualHost?.SetOccupiedBounds(OccupiedBounds);
            boundsChangedListener?.Invoke(OccupiedBounds);
        }

        /// <summary>Adds a single block to the group: caches its parent, reparents it, and assigns the shared Rigidbody.</summary>
        private void AddMember(LevelBlockBehavior block)
        {
            if (!block) return;
            block.CacheParentBeforeGrouping();
            block.transform.SetParent(GroupObject.transform, true);
            block.SetGroupRigidbody(SharedRigidbody);
            members.Add(block);
        }

        /// <summary>
        /// Removes a single block from the group and restores its individual Rigidbody.
        /// Automatically dissolves the group when the last member leaves.
        /// </summary>
        public void RemoveMember(LevelBlockBehavior block)
        {
            if (!block) return;
            members.Remove(block);
            RestoreBlock(block);

            if (members.Count == 0)
                Dissolve();
            else
                RefreshOccupiedBounds();
        }

        /// <summary>
        /// Removes all members, restores each block's individual Rigidbody and original parent,
        /// then destroys the group GameObject.
        /// </summary>
        public void Dissolve()
        {
            boundsChangedListener = null;

            for (int i = members.Count - 1; i >= 0; i--)
            {
                LevelBlockBehavior block = members[i];
                if (block) RestoreBlock(block);
            }
            members.Clear();
            OccupiedBounds = default;
            visualHost = null;

            if (GroupObject)
            {
                Object.Destroy(GroupObject);
                GroupObject = null;
            }
        }

        private void RestoreBlock(LevelBlockBehavior block)
        {
            Transform originalParent = block.ConsumeParentBeforeGrouping();
            if (GroupObject && block.transform.parent == GroupObject.transform)
                block.transform.SetParent(originalParent, true);

            block.RestoreIndividualRigidbody();
        }
    }
}
