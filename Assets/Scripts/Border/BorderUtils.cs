using UnityEngine;

namespace BorderSpawnModule
{
    public static class BorderUtils
    {
        public static readonly Vector2Int[] BorderNeighborOffsets =
        {
            new (-1, -1), new (0, -1), new (1, -1),
            new (-1, 0),               new (1, 0),
            new (-1, 1),  new (0, 1),  new (1, 1)
        };
    }
}