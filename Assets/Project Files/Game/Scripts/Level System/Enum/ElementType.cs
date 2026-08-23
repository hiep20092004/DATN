using BorderSpawnModule;

namespace WaterFlow.Game
{
    public enum ElementType
    {
        Empty = 0,
        InnerTile = 1,
        Border = 2,
        Block = 3,
        Gate = 4,
        Obstacle = 5,
        InteractableObject = 6,
        Generator = 7,
    }
    
    public static class ElementTypeExtensions
    {
        public static BorderCellType ToBorderCellType(this ElementType type)
        {
            switch (type)
            {
                case ElementType.Border:
                case ElementType.Generator:
                    return BorderCellType.Border;
                case ElementType.Gate:
                    return BorderCellType.Gate;
                case ElementType.Obstacle:
                    return BorderCellType.Obstacle;
                case ElementType.InnerTile:
                case ElementType.Block:
                case ElementType.InteractableObject:
                    return BorderCellType.InnerTile;
                default:
                    return BorderCellType.Empty;
            }
        }

        public static bool IsCanSpawnBlock(this ElementType type)
        {
            return type is ElementType.InnerTile or ElementType.Block or ElementType.InteractableObject;
        }
    }
}