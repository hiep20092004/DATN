using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    public struct BlockSplitResult
    {
        public BlockType BlockA;
        public BlockType BlockB;
        public Vector2Int OffsetA;
        public Vector2Int OffsetB;
    }

    public static class BlockSplitter
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private static Dictionary<BlockType, List<Vector2Int>> cachedSignatures;

        /// <summary>
        /// Randomly splits a block into 2 smaller blocks whose shapes match existing BlockTypes.
        /// Returns null if the block cannot be split.
        /// </summary>
        public static BlockSplitResult? RandomSplit(BlockType sourceType, BlocksVisualsData visualsData)
        {
            var allSplits = GetAllValidSplits(sourceType, visualsData);
            if (allSplits.Count == 0) return null;
            return allSplits[Random.Range(0, allSplits.Count)];
        }

        /// <summary>
        /// Gets all valid ways to split a block into 2 smaller connected blocks,
        /// where each resulting piece matches a known BlockType.
        /// OffsetA/OffsetB are the cell-grid offsets of each piece relative to the original figure origin.
        /// </summary>
        public static List<BlockSplitResult> GetAllValidSplits(BlockType sourceType, BlocksVisualsData visualsData)
        {
            var results = new List<BlockSplitResult>();

            BlockData sourceData = visualsData.GetBlockData(sourceType);
            if (sourceData == null) return results;

            LevelFigure figure = sourceData.Prefab.GetComponent<LevelBlockBehavior>().Figure;

            if (figure == null) return results;

            List<Vector2Int> cells = GetFilledCells(figure);
            if (cells.Count < 2) return results;

            EnsureSignatures(visualsData);

            int n = cells.Count;

            // Enumerate subsets via bitmask. Fix bit 0 in group A to avoid (A,B)/(B,A) duplicates.
            for (int mask = 1; mask < (1 << n); mask += 2)
            {
                int complement = ((1 << n) - 1) & ~mask;
                if (complement == 0) continue;

                var groupA = CellsFromMask(cells, mask);
                var groupB = CellsFromMask(cells, complement);

                if (!IsConnected(groupA) || !IsConnected(groupB))
                    continue;

                var matchA = MatchBlockType(groupA);
                if (!matchA.HasValue) continue;

                var matchB = MatchBlockType(groupB);
                if (!matchB.HasValue) continue;

                results.Add(new BlockSplitResult
                {
                    BlockA = matchA.Value.type,
                    BlockB = matchB.Value.type,
                    OffsetA = matchA.Value.offset,
                    OffsetB = matchB.Value.offset
                });
            }

            return results;
        }

        private static void EnsureSignatures(BlocksVisualsData visualsData)
        {
            if (cachedSignatures != null) return;

            cachedSignatures = new Dictionary<BlockType, List<Vector2Int>>();
            foreach (BlockData blockData in visualsData.Blocks)
            {
                if (blockData == null) continue;
                LevelFigure figure = blockData.Prefab.GetComponent<LevelBlockBehavior>().Figure;
                cachedSignatures[blockData.Type] = NormalizeCells(GetFilledCells(figure));
            }
        }

        /// <summary>
        /// Call this if block data changes at runtime (e.g. editor reload).
        /// </summary>
        public static void InvalidateCache()
        {
            cachedSignatures = null;
        }

        private static List<Vector2Int> GetFilledCells(LevelFigure figure)
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < figure.Size.y; y++)
            {
                for (int x = 0; x < figure.Size.x; x++)
                {
                    int index = x + y * figure.Size.x;
                    if (figure.Points[index].IsFilled)
                        cells.Add(new Vector2Int(x, y));
                }
            }
            return cells;
        }

        private static List<Vector2Int> CellsFromMask(List<Vector2Int> allCells, int mask)
        {
            var subset = new List<Vector2Int>();
            for (int i = 0; i < allCells.Count; i++)
            {
                if ((mask & (1 << i)) != 0)
                    subset.Add(allCells[i]);
            }
            return subset;
        }

        private static List<Vector2Int> NormalizeCells(List<Vector2Int> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
            }

            var normalized = new List<Vector2Int>(cells.Count);
            foreach (var c in cells)
                normalized.Add(new Vector2Int(c.x - minX, c.y - minY));

            normalized.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return normalized;
        }

        private static bool IsConnected(List<Vector2Int> cells)
        {
            if (cells.Count <= 1) return true;

            var cellSet = new HashSet<Vector2Int>(cells);
            var visited = new HashSet<Vector2Int> { cells[0] };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cells[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var dir in Directions)
                {
                    var neighbor = current + dir;
                    if (cellSet.Contains(neighbor) && visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return visited.Count == cells.Count;
        }

        private static (BlockType type, Vector2Int offset)? MatchBlockType(List<Vector2Int> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
            }

            var normalized = NormalizeCells(cells);

            foreach (var kvp in cachedSignatures)
            {
                if (CellsEqual(normalized, kvp.Value))
                    return (kvp.Key, new Vector2Int(minX, minY));
            }

            return null;
        }

        private static bool CellsEqual(List<Vector2Int> a, List<Vector2Int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
