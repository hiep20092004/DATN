using System.Collections.Generic;
using UnityEngine;

namespace BorderSpawnModule
{
    public struct BorderResolveResult
    {
        public Vector2Int Position;
        public ResolvedBorderRule Rule;
    }

    public static class BorderSpawnResolver
    {
        public static List<BorderResolveResult> Resolve(BorderGrid grid, List<BorderRuleEntry> rules)
        {
            List<BorderResolveResult> results = new List<BorderResolveResult>();
            if (grid == null || rules == null || rules.Count == 0)
                return results;

            List<Vector2Int> borderPositions = grid.BorderPositions;
            for (int i = 0; i < borderPositions.Count; i++)
            {
                Vector2Int pos = borderPositions[i];
                int localX = pos.x - grid.Origin.x;
                int localY = pos.y - grid.Origin.y;

                if (BorderRuleMatcher.TryResolve(
                        localX, localY,
                        grid.Continuity, grid.CellTypes,
                        grid.SizeX, grid.SizeY,
                        rules, out ResolvedBorderRule resolved))
                {
                    results.Add(new BorderResolveResult
                    {
                        Position = pos,
                        Rule = resolved
                    });
                }
            }

            return results;
        }

        public static List<BorderResolveResult> ResolveAtPositions(
            BorderGrid grid,
            List<Vector2Int> positions,
            List<BorderRuleEntry> rules,
            BorderRuleMatcher.MatchContext context)
        {
            List<BorderResolveResult> results = new List<BorderResolveResult>();
            if (grid == null || rules == null || rules.Count == 0 || positions == null || positions.Count == 0)
                return results;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector2Int pos = positions[i];
                int localX = pos.x - grid.Origin.x;
                int localY = pos.y - grid.Origin.y;

                if (BorderRuleMatcher.TryResolve(
                        localX, localY,
                        grid.Continuity, grid.CellTypes,
                        grid.SizeX, grid.SizeY,
                        rules, context, out ResolvedBorderRule resolved))
                {
                    results.Add(new BorderResolveResult
                    {
                        Position = pos,
                        Rule = resolved
                    });
                }
            }

            return results;
        }

        public static List<BorderResolveResult> ResolveObstacles(BorderGrid grid, List<Vector2Int> obstaclePositions, List<BorderRuleEntry> rules)
        {
            return ResolveAtPositions(grid, obstaclePositions, rules, BorderRuleMatcher.MatchContext.ForObstacle());
        }
    }
}
