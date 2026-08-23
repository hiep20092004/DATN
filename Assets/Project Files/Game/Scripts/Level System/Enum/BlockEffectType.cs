namespace WaterFlow.Game
{
    // APPEND-ONLY. Values are serialized by their integer (level [SerializeReference] data, the block-effect
    // compatibility matrix keyed by (int)BlockEffectType, block-id logic, editor drawers). Explicit numbers
    // pin each value so reordering the lines is harmless. NEVER renumber, insert in the middle, or delete a
    // value — add new effects at the end with the next free integer.
    public enum BlockEffectType
    {
        None = 0,
        FixedDirection = 1,
        Layered = 2,
        Ice = 3,
        Combines = 4,
        Blocked = 5,
        Bomb = 6,
        Shutter = 7,
        Dual = 8,
        Chain = 9,
        KeyChain = 10,
        KeyColor = 11,
        Ropes = 12,
        Scissor = 13,
        Tnt = 14,
        Hidden = 15,
        TimeCapsule = 16,
        ContainerBox = 17,
        ContainerMoveBox = 18,
        SwitchLayer = 19,
        BreakableLink = 20,
        ContainerColorBox = 21
    }
}