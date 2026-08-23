namespace WaterFlow.Game
{
    /// <summary>
    /// Optional visual layer for <see cref="IGroupableEffect"/> implementations.
    /// Applies layout from <see cref="BlockGroupOccupiedBounds"/>; does not compute bounds.
    /// </summary>
    public interface IBlockGroupVisual
    {
        void Apply(BlockGroupOccupiedBounds bounds);
        void Hide();
    }
}
