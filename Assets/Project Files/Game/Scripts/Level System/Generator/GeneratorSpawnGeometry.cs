using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Shared geometry helpers for generator block spawning.
    /// Used by both <see cref="GeneratorBehavior"/> at runtime and the editor's GeneratorConfigEditor.
    /// </summary>
    public static class GeneratorSpawnGeometry
    {
        /// <summary>
        /// Converts a figure-space offset (dx, dy) relative to the figure pivot into a
        /// watched-grid-space offset (dCol, dRow) relative to the spawn cell.
        ///
        /// Watched grid layout (BuildWatchedCells):
        ///   col 0 = flankA side, col 1 = center, col 2 = flankB side
        ///   row 0 = nearest board cell, row 1 = middle, row 2 = furthest (deepest inside pipe)
        ///
        /// Direction mapping derived from BuildWatchedCells + GateDirection.PipeOccupiedOffsets:
        ///   Left  (0): block enters +X  → figX = depth axis (Near→Far),  figY = flank axis (A→B)
        ///   Right (1): block enters −X  → figX = depth reversed,          figY = flank axis (A→B)
        ///   Top   (2): block enters −Y  → figY = depth reversed,          figX = flank axis (A→B)
        ///   Bottom(3): block enters +Y  → figY = depth axis (Near→Far),   figX = flank axis (A→B)
        /// </summary>
        public static void FigureOffsetToGridOffset(
            GateDirection.Type dir, int dx, int dy,
            out int dCol, out int dRow)
        {
            switch (dir)
            {
                case GateDirection.Type.Left:
                    dRow = +dx;
                    dCol = +dy;
                    break;
                case GateDirection.Type.Right:
                    dRow = -dx;
                    dCol = +dy;
                    break;
                case GateDirection.Type.Top:
                    dRow = -dy;
                    dCol = +dx;
                    break;
                case GateDirection.Type.Bottom:
                    dRow = +dy;
                    dCol = +dx;
                    break;
                default:
                    dRow = 0;
                    dCol = 0;
                    break;
            }
        }

        /// <summary>
        /// Converts a watched-grid coordinate (col, row) to a world-space cell coordinate.
        /// Uses <paramref name="watchedCells"/> directly for in-range coordinates and falls back
        /// to extrapolation from <paramref name="borderPosition"/> + flanks for out-of-range cells.
        /// </summary>
        /// <param name="watchedCells">The 9-element watched cell list from <see cref="GeneratorBehavior"/>.</param>
        /// <param name="borderPosition">The generator border position in grid space.</param>
        /// <param name="gateDir">The gate direction data.</param>
        /// <param name="col">Column in watched grid (0..2).</param>
        /// <param name="row">Row in watched grid (0..2).</param>
        /// <param name="worldCell">Output world grid cell.</param>
        /// <returns>True if a valid mapping was found.</returns>
        public static bool TryGridToWorldCell(
            IReadOnlyList<Vector2Int> watchedCells,
            Vector2Int borderPosition,
            GateDirection gateDir,
            int col, int row,
            out Vector2Int worldCell)
        {
            int index = col + row * 3;
            if (watchedCells != null && index >= 0 && index < watchedCells.Count)
            {
                worldCell = watchedCells[index];
                return true;
            }

            // Extrapolate: center column of the closest row, then shift by flank offset.
            if (gateDir == null || gateDir.PipeOccupiedOffsets == null || gateDir.PipeOccupiedOffsets.Length == 0)
            {
                worldCell = default;
                return false;
            }

            GateDirection.GetFlankOffsets(
                gateDir == GateDirection.DIRECTIONS[0] ? GateDirection.Type.Left :
                gateDir == GateDirection.DIRECTIONS[1] ? GateDirection.Type.Right :
                gateDir == GateDirection.DIRECTIONS[2] ? GateDirection.Type.Top :
                GateDirection.Type.Bottom,
                out Vector2Int flankA, out Vector2Int flankB);

            // Clamp row to available pipe offsets (PipeOccupiedOffsets has 3 entries: index 0=nearest…2=furthest).
            int rowClamped = Mathf.Clamp(row, 0, gateDir.PipeOccupiedOffsets.Length - 1);
            Vector2Int centerCell = borderPosition - gateDir.PipeOccupiedOffsets[rowClamped];

            worldCell = col switch
            {
                0 => centerCell + flankA,
                1 => centerCell,
                2 => centerCell + flankB,
                _ => centerCell
            };
            return true;
        }

        /// <summary>
        /// Builds the world-space footprint cells that the next-queued block will occupy
        /// when spawned at <paramref name="spawnCell"/> from a generator with the given direction,
        /// and the additional path-clearance cells that must be empty for the spawn to be allowed.
        /// <para>
        /// <paramref name="footprint"/> contains every cell where <see cref="PointData.IsFilled"/> is true
        /// — these are the cells the block physically occupies (used for visuals).
        /// </para>
        /// <para>
        /// <paramref name="additionalRequireCells"/> is auto-derived from geometry: for every filled
        /// figure cell mapped to watched grid (col, row=R), the in-board cells (col, r) for r=0..R-1
        /// (one row closer to the gate per step, equivalent to subtracting 3 from the watched cell index
        /// per row) that are not themselves part of the footprint. These cells must be clear because the
        /// block sweeps through them as it enters the board from the gate.
        /// </para>
        /// </summary>
        public static bool TryGetSpawnFootprint(
            LevelFigure figure,
            GateDirection.Type dir,
            Vector2Int spawnCell,
            IReadOnlyList<Vector2Int> watchedCells,
            Vector2Int borderPosition,
            GateDirection gateDir,
            out List<Vector2Int> footprint,
            out List<Vector2Int> additionalRequireCells)
        {
            footprint = new List<Vector2Int>();
            additionalRequireCells = new List<Vector2Int>();
            if (figure == null || figure.Points == null) return false;

            Vector2Int pivot = figure.PivotPoint;
            int sizeX = figure.Size.x;
            int sizeY = figure.Size.y;

            // Track which watched-grid (col, row) coords end up filled, so path-clearance cells that
            // overlap the footprint can be skipped.
            var filledGridCoords = new HashSet<Vector2Int>();
            // Maximum row reached per column among filled cells — used to derive the path cells
            // (everything from row 0 up to maxRow-1 in that column must be clear).
            var maxRowPerCol = new Dictionary<int, int>();

            for (int fy = 0; fy < sizeY; fy++)
            {
                for (int fx = 0; fx < sizeX; fx++)
                {
                    int idx = fy * sizeX + fx;
                    if (idx >= figure.Points.Length) continue;

                    if (!figure.Points[idx].IsFilled) continue;

                    FigureOffsetToGridOffset(dir, fx - pivot.x, fy - pivot.y, out int dCol, out int dRow);
                    int col = spawnCell.x + dCol;
                    int row = spawnCell.y + dRow;

                    if (!TryGridToWorldCell(watchedCells, borderPosition, gateDir, col, row, out Vector2Int worldCell))
                        return false;

                    footprint.Add(worldCell);
                    filledGridCoords.Add(new Vector2Int(col, row));
                    if (!maxRowPerCol.TryGetValue(col, out int existing) || row > existing)
                        maxRowPerCol[col] = row;
                }
            }

            if (footprint.Count == 0) return false;

            // For each column with a filled cell at row R > 0, every (col, r) with 0 <= r < R must be
            // clear unless it is itself a footprint cell. This mirrors the block sweeping out of the
            // gate (row 0 is closest to the gate, deeper rows lie further into the board).
            var addedRequire = new HashSet<Vector2Int>();
            foreach (KeyValuePair<int, int> kv in maxRowPerCol)
            {
                int col = kv.Key;
                int maxRow = kv.Value;
                for (int r = 0; r < maxRow; r++)
                {
                    var gridCoord = new Vector2Int(col, r);
                    if (filledGridCoords.Contains(gridCoord)) continue;

                    if (!TryGridToWorldCell(watchedCells, borderPosition, gateDir, col, r, out Vector2Int worldCell))
                        return false;

                    if (addedRequire.Add(worldCell))
                        additionalRequireCells.Add(worldCell);
                }
            }

            return true;
        }
    }
}
