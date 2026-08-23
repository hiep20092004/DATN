using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BorderSpawnModule
{
    public class BorderGridBuilder
    {
        private readonly List<CellEntry> _cells = new List<CellEntry>();
        private readonly HashSet<Vector2Int> _borderPositions = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _forcedContinuity = new HashSet<Vector2Int>();

        private struct CellEntry
        {
            public Vector2Int Position;
            public BorderCellType Type;
        }

        public BorderGridBuilder AddCell(Vector2Int position, BorderCellType type)
        {
            _cells.Add(new CellEntry { Position = position, Type = type });
            return this;
        }

        public BorderGridBuilder MarkAsBorder(Vector2Int position)
        {
            _borderPositions.Add(position);
            return this;
        }

        public BorderGridBuilder AddForcedContinuity(Vector2Int position)
        {
            _forcedContinuity.Add(position);
            return this;
        }

        public BorderGrid Build()
        {
            if (_cells.Count == 0 && _borderPositions.Count == 0)
                return null;

            ComputeBounds(out Vector2Int origin, out int sizeX, out int sizeY);

            BorderCellType[] cellTypes = new BorderCellType[sizeX * sizeY];

            for (int i = 0; i < _cells.Count; i++)
            {
                CellEntry entry = _cells[i];
                int index = ToFlatIndex(entry.Position, origin, sizeX);
                if (index >= 0 && index < cellTypes.Length)
                    cellTypes[index] = entry.Type;
            }

            foreach (Vector2Int pos in _borderPositions)
            {
                int index = ToFlatIndex(pos, origin, sizeX);
                if (index >= 0 && index < cellTypes.Length)
                    cellTypes[index] = BorderCellType.Border;
            }

            HashSet<int> forcedIndices = null;
            if (_forcedContinuity.Count > 0)
            {
                forcedIndices = new HashSet<int>();
                foreach (Vector2Int pos in _forcedContinuity)
                    forcedIndices.Add(ToFlatIndex(pos, origin, sizeX));
            }

            bool[] continuity = BuildContinuityGrid(cellTypes, sizeX, sizeY, forcedIndices);

            List<Vector2Int> sortedBorderPositions = _borderPositions
                .OrderBy(p => p.y).ThenBy(p => p.x).ToList();

            return new BorderGrid(cellTypes, continuity, origin, sizeX, sizeY, sortedBorderPositions);
        }

        private void ComputeBounds(out Vector2Int origin, out int sizeX, out int sizeY)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            void Include(Vector2Int p)
            {
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }

            for (int i = 0; i < _cells.Count; i++)
                Include(_cells[i].Position);

            foreach (Vector2Int p in _borderPositions)
                Include(p);

            foreach (Vector2Int p in _forcedContinuity)
                Include(p);

            origin = new Vector2Int(minX, minY);
            sizeX = maxX - minX + 1;
            sizeY = maxY - minY + 1;
        }

        private static int ToFlatIndex(Vector2Int position, Vector2Int origin, int sizeX)
        {
            return (position.y - origin.y) * sizeX + (position.x - origin.x);
        }

        private static bool[] BuildContinuityGrid(BorderCellType[] cellTypes, int sizeX, int sizeY, HashSet<int> forcedIndices)
        {
            bool[] grid = new bool[sizeX * sizeY];

            for (int i = 0; i < grid.Length; i++)
            {
                BorderCellType type = cellTypes[i];
                grid[i] = type.IsContinuityBorder();
            }

            if (forcedIndices != null)
            {
                foreach (int index in forcedIndices)
                {
                    if (index >= 0 && index < grid.Length)
                        grid[index] = true;
                }
            }

            return grid;
        }
    }
}
