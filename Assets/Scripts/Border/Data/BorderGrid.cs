using System.Collections.Generic;
using UnityEngine;

namespace BorderSpawnModule
{
    public class BorderGrid
    {
        public BorderCellType[] CellTypes { get; }
        public bool[] Continuity { get; }
        public Vector2Int Origin { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public List<Vector2Int> BorderPositions { get; }

        public BorderGrid(
            BorderCellType[] cellTypes,
            bool[] continuity,
            Vector2Int origin,
            int sizeX, int sizeY,
            List<Vector2Int> borderPositions)
        {
            CellTypes = cellTypes;
            Continuity = continuity;
            Origin = origin;
            SizeX = sizeX;
            SizeY = sizeY;
            BorderPositions = borderPositions;
        }
    }
}
