using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class ContainerMoveBoxEffectBehavior : BaseContainerBoxEffectBehavior<ContainerMoveBoxEffectConfig>, IGroupClickReceiver
    {
        public override bool MoveMultiplyObjects()
        {
            return ActiveGroup != null && ActiveGroup.Members.Count > 1;
        }

        /// <summary>Exposes the group members so drag/move logic treats the whole group as linked.</summary>
        public override IReadOnlyList<LevelBlockBehavior> GetLinkedBlocks()
        {
            if (ActiveGroup == null || ActiveGroup.Members.Count == 0)
                return base.GetLinkedBlocks();

            return ActiveGroup.Members;
        }

        protected override IGroupClickReceiver GetGroupClickReceiver()
        {
            return this;
        }

        // ── IGroupVisualClickReceiver ──────────────────────────────────────────
        // Draggable until the box is finishing (clear animation in flight).
        public bool CanClick() =>
            ActiveGroup != null && !ActiveGroup.IsFinish && ActiveGroup.Members.Count > 0;

        public void OnClicked()
        {
            if (ActiveGroup == null || ActiveGroup.Members.Count == 0)
                return;

            // Pick the visual owner directly; MoveMultiplyObjects/GetLinkedBlocks drag the rest of the group.
            LevelBlockBehavior owner = ActiveGroup.Members[0];
            if (owner)
                LevelController.Instance.OnObjectPicked(owner);
        }

        public void OnBlocked()
        {
            
        }

        public override void OnBlockReleased(LevelBlockBehavior levelBlockBehavior, Vector2Int snapTargetPosition)
        {
            if (ActiveGroup != null && IsVisualOwner(ActiveGroup))
                ActiveGroup.RefreshOccupiedBounds();
        }
    }
}
