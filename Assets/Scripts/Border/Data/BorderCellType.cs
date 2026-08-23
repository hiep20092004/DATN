namespace BorderSpawnModule
{
    public enum BorderCellType
    {
        Empty = 0,
        Border = 1,
        InnerTile = 2,
        Gate = 3,
        Obstacle = 4,
    }
    
    public static class BorderCellTypeExtensions
    {
        public static bool IsContinuityBorder(this BorderCellType type)
        {
            return type is BorderCellType.Border or BorderCellType.Gate;
        }
    }
}
