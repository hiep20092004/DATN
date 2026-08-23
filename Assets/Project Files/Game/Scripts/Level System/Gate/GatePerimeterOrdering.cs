using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Runtime utility to compute a stable clockwise perimeter order of gate BlockIds.
    /// Clockwise is defined by decreasing atan2(dy, dx) in a y-up frame.
    /// </summary>
    public static class GatePerimeterOrdering
    {
        private sealed class GateGroup
        {
            public int BlockId;
            public Vector2Int Rep;
        }

        public static bool TryComputeGateBlockIdOrder(
            LevelElementData[] levelElements,
            out List<int> orderedGateBlockIds,
            out string errorMessage)
        {
            orderedGateBlockIds = null;
            errorMessage = null;

            if (levelElements == null || levelElements.Length == 0)
            {
                errorMessage = "Level has no elements.";
                return false;
            }

            List<GateGroup> groups = new List<GateGroup>();
            foreach (IGrouping<int, LevelElementData> g in levelElements
                         .Where(e => e != null && e.Type == ElementType.Gate)
                         .GroupBy(e => e.BlockId))
            {
                List<Vector2Int> positions = g.Select(e => e.Position).ToList();
                if (positions.Count == 0)
                    continue;

                // Pick a deterministic representative cell (min x, then min y).
                Vector2Int rep = positions[0];
                for (int i = 1; i < positions.Count; i++)
                {
                    Vector2Int p = positions[i];
                    if (p.x < rep.x || (p.x == rep.x && p.y < rep.y))
                        rep = p;
                }

                groups.Add(new GateGroup { BlockId = g.Key, Rep = rep });
            }

            if (groups.Count == 0)
            {
                errorMessage = "No Gate elements in this level.";
                return false;
            }

            orderedGateBlockIds = BuildPolarClockwiseOrder(groups);
            if (orderedGateBlockIds.Count != groups.Count)
            {
                // Ensure we never drop any ids even if sorting had edge cases.
                HashSet<int> seen = new HashSet<int>(orderedGateBlockIds);
                foreach (GateGroup g in groups.OrderBy(x => x.Rep.y).ThenBy(x => x.Rep.x))
                {
                    if (seen.Add(g.BlockId))
                        orderedGateBlockIds.Add(g.BlockId);
                }
            }

            return true;
        }

        private static List<int> BuildPolarClockwiseOrder(List<GateGroup> groups)
        {
            if (groups.Count == 1)
                return new List<int> { groups[0].BlockId };

            float cx = 0f, cy = 0f;
            foreach (GateGroup g in groups)
            {
                cx += g.Rep.x;
                cy += g.Rep.y;
            }

            cx /= groups.Count;
            cy /= groups.Count;

            return groups
                .OrderByDescending(g => PolarAngle(g.Rep, cx, cy))
                .ThenBy(g => SquaredDistTo(g.Rep, cx, cy))
                .ThenBy(g => g.BlockId)
                .Select(g => g.BlockId)
                .ToList();
        }

        private static float PolarAngle(Vector2Int rep, float cx, float cy)
        {
            float dx = rep.x - cx;
            float dy = rep.y - cy;
            if (Mathf.Abs(dx) < 1e-6f && Mathf.Abs(dy) < 1e-6f)
                return 0f;

            return Mathf.Atan2(dy, dx);
        }

        private static float SquaredDistTo(Vector2Int rep, float cx, float cy)
        {
            float dx = rep.x - cx;
            float dy = rep.y - cy;
            return dx * dx + dy * dy;
        }
    }
}

