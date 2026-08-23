using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Computes <see cref="BlockGroupOccupiedBounds"/> from block occupied cells.
    /// Cell layout matches <see cref="LevelBlockBehavior"/> gizmo cells.
    /// </summary>
    public static class BlockGroupBoundsCalculator
    {
        private static readonly Vector3 CellCenterOffset = new(0f, 0.25f, 0f);
        private static readonly Vector3 CellSize = new(1f, 0.5f, 1f);

        public static bool TryCalculate(
            IReadOnlyList<LevelBlockBehavior> members,
            out BlockGroupOccupiedBounds bounds)
        {
            bounds = default;
            if (members == null || members.Count == 0)
                return false;

            bool hasAny = false;
            Bounds worldBounds = default;
            Vector2Int minCell = default;
            Vector2Int maxCell = default;

            foreach (LevelBlockBehavior member in members)
            {
                if (!member) continue;

                Vector2Int[] cells = member.GetOccupiedCells();
                float y = member.transform.position.y;

                for (int i = 0; i < cells.Length; i++)
                    Encapsulate(cells[i], y, ref hasAny, ref worldBounds, ref minCell, ref maxCell);
            }

            if (!hasAny)
                return false;

            bounds = new BlockGroupOccupiedBounds(worldBounds, minCell, maxCell);
            return true;
        }

        /// <summary>
        /// Computes bounds from raw grid cells at a fixed world height. Used when the blocks are not
        /// yet spawned (e.g. an extra-layer Lift drawing its bound before lifting its blocks in).
        /// </summary>
        public static bool TryCalculate(
            IReadOnlyList<Vector2Int> cells,
            float worldY,
            out BlockGroupOccupiedBounds bounds)
        {
            bounds = default;
            if (cells == null || cells.Count == 0)
                return false;

            bool hasAny = false;
            Bounds worldBounds = default;
            Vector2Int minCell = default;
            Vector2Int maxCell = default;

            for (int i = 0; i < cells.Count; i++)
                Encapsulate(cells[i], worldY, ref hasAny, ref worldBounds, ref minCell, ref maxCell);

            if (!hasAny)
                return false;

            bounds = new BlockGroupOccupiedBounds(worldBounds, minCell, maxCell);
            return true;
        }

        private static void Encapsulate(
            Vector2Int cell, float y, ref bool hasAny, ref Bounds worldBounds,
            ref Vector2Int minCell, ref Vector2Int maxCell)
        {
            var cellBounds = new Bounds(
                new Vector3(cell.x, y, cell.y) + CellCenterOffset,
                CellSize);

            if (!hasAny)
            {
                worldBounds = cellBounds;
                minCell = maxCell = cell;
                hasAny = true;
            }
            else
            {
                worldBounds.Encapsulate(cellBounds);
                minCell = Vector2Int.Min(minCell, cell);
                maxCell = Vector2Int.Max(maxCell, cell);
            }
        }
    }
}
