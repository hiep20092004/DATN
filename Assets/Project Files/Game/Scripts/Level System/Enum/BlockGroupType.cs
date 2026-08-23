namespace WaterFlow.Game
{
    public enum BlockGroupType
    {
        None,
        Single,
        Square,
        Double,
        Triple,
        Corner,
        LType,
        LTypeReverse,
        TType,
        Cross,
        ZType,
        ZTypeReverse,
        UType,
        Square3X3,
    }

    public static class BlockGroupTypeExtensions
    {
        public static BlockGroupType ToBlockGroup(this BlockType groupType)
        {
            switch (groupType)
            {
                case BlockType.Single:
                    return BlockGroupType.Single;
                case BlockType.Double_Horizontal:
                case BlockType.Double_Vertical:
                    return BlockGroupType.Double;
                case BlockType.Triple_Horizontal:
                case BlockType.Triple_Vertical:
                    return BlockGroupType.Triple;
                case BlockType.Square:
                    return BlockGroupType.Square;
                case BlockType.Square_3x3:
                    return BlockGroupType.Square3X3;
                case BlockType.Corner_LB:
                case BlockType.Corner_LT:
                case BlockType.Corner_RB:
                case BlockType.Corner_RT:
                    return BlockGroupType.Corner;
                case BlockType.LType_B1:
                case BlockType.LType_L1:
                case BlockType.LType_R1:
                case BlockType.LType_T1:
                    return BlockGroupType.LTypeReverse;
                case BlockType.LType_B2:
                case BlockType.LType_L2:
                case BlockType.LType_R2:
                case BlockType.LType_T2:
                    return BlockGroupType.LType;
                case BlockType.TType_B:
                case BlockType.TType_L:
                case BlockType.TType_R:
                case BlockType.TType_T:
                    return BlockGroupType.TType;
                case BlockType.ZType_Horiz_L:
                case BlockType.ZType_Vert_R:
                    return BlockGroupType.ZType;
                case BlockType.ZType_Horiz_R:
                case BlockType.ZType_Vert_L:
                    return BlockGroupType.ZTypeReverse;
                case BlockType.UType_B:
                case BlockType.UType_L:
                case BlockType.UType_R:
                case BlockType.UType_T:
                    return BlockGroupType.UType;
                case BlockType.Cross:
                    return BlockGroupType.Cross;
                default:
                    return BlockGroupType.None;
            }
        }
    }
}