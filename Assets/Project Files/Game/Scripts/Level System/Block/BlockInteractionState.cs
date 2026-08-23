namespace WaterFlow.Game
{
    /// <summary>
    /// Playability state for booster targeting and interaction gating (separate from fill <see cref="BlockState"/>).
    /// </summary>
    public enum BlockInteractionState
    {
        Idle,
        Hidden,
        Tween,
    }
}
