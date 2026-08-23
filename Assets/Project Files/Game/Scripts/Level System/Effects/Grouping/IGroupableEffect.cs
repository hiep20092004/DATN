namespace WaterFlow.Game
{
    /// <summary>
    /// Implemented by block effects that participate in a shared <see cref="BlockGroup"/>.
    /// The group is formed externally (e.g. by LevelRepresentation) and injected via <see cref="OnGroupFormed"/>.
    /// Enables future effect types (e.g. MovableContainerBox) to reuse the same grouping infrastructure
    /// without duplicating group-lifecycle code.
    /// </summary>
    public interface IGroupableEffect
    {
        /// <summary>Identifier shared by all blocks that belong to the same physical group.</summary>
        int GroupId { get; }

        /// <summary>The block this effect is attached to. Used by <c>LevelRepresentation</c> to build the member list generically.</summary>
        LevelBlockBehavior Owner { get; }

        /// <summary>
        /// The currently active group, or null when the group has been dissolved.
        /// Use <see cref="BlockGroup.OccupiedBounds"/> for shared bounds (visuals, gizmos).
        /// </summary>
        BlockGroup ActiveGroup { get; }

        /// <summary>Called once after all peers in the group have been spawned and the group object is ready.</summary>
        void OnGroupFormed(BlockGroup group, LevelRepresentation level);

        /// <summary>Called when the group is dissolved; the effect should clean up its state and disable itself.</summary>
        void OnGroupDissolved();
    }
}
