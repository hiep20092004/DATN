namespace WaterFlow.Game
{
    public interface IColorElement
    {
        public bool IsActive { get; }
        public BlockColor Color { get; }
    }
}