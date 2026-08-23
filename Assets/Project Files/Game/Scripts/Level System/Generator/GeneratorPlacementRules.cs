using System;
using UnityEngine;

namespace WaterFlow.Game
{
    public static class GeneratorPlacementRules
    {
        /// <summary>
        /// Axis probe order matches <c>GetGateDirection</c> for gates (Right, Left, Top, Bottom).
        /// Picks the first direction where: behind (<c>pos + offset</c>) is Empty or outside the grid;
        /// in front (<c>pos - offset</c>) is <see cref="ElementTypeExtensions.IsCanSpawnBlock"/>; flanks are Border.
        /// </summary>
        private static readonly GateDirection.Type[] GeneratorDirectionProbeOrder =
        {
            GateDirection.Type.Right,
            GateDirection.Type.Left,
            GateDirection.Type.Top,
            GateDirection.Type.Bottom,
        };

        public static bool TryGetGeneratorGateDirection(Vector2Int position, Vector2Int gridSize,
            Func<Vector2Int, ElementType?> getCellType, out GateDirection.Type dir)
        {
            for (int i = 0; i < GeneratorDirectionProbeOrder.Length; i++)
            {
                GateDirection.Type candidate = GeneratorDirectionProbeOrder[i];
                Vector2Int offset = GateDirection.DIRECTIONS[(int)candidate].PositionOffset;
                Vector2Int behind = position + offset;
                Vector2Int front = position - offset;

                if (!IsBehindEmptyOrVoid(behind, gridSize, getCellType))
                    continue;
                if (!IsFrontSpawnableBlock(front, gridSize, getCellType))
                    continue;
                if (!AreFlankingCellsBorder(position, candidate, getCellType))
                    continue;

                dir = candidate;
                return true;
            }

            dir = GateDirection.Type.None;
            return false;
        }

        private static bool IsBehindEmptyOrVoid(Vector2Int behind, Vector2Int gridSize,
            Func<Vector2Int, ElementType?> getCellType)
        {
            if (behind.x < 0 || behind.y < 0 || behind.x >= gridSize.x || behind.y >= gridSize.y)
                return true;

            return getCellType(behind) == ElementType.Empty;
        }

        private static bool IsFrontSpawnableBlock(Vector2Int front, Vector2Int gridSize,
            Func<Vector2Int, ElementType?> getCellType)
        {
            if (front.x < 0 || front.y < 0 || front.x >= gridSize.x || front.y >= gridSize.y)
                return false;

            ElementType? t = getCellType(front);
            return t.HasValue && t.Value.IsCanSpawnBlock();
        }

        public static bool TryGetEdgeGateDirection(Vector2Int position, Vector2Int gridSize,
            out GateDirection.Type dir)
        {
            if (position.x == 0)
            {
                dir = GateDirection.Type.Left;
                return true;
            }

            if (position.x == gridSize.x - 1)
            {
                dir = GateDirection.Type.Right;
                return true;
            }

            if (position.y == gridSize.y - 1)
            {
                dir = GateDirection.Type.Top;
                return true;
            }

            if (position.y == 0)
            {
                dir = GateDirection.Type.Bottom;
                return true;
            }

            dir = GateDirection.Type.None;
            return false;
        }

        public static bool AreFlankingCellsBorder(Vector2Int generatorPosition, GateDirection.Type gateDir,
            Func<Vector2Int, ElementType?> getCellType)
        {
            if (gateDir == GateDirection.Type.None)
                return false;

            GateDirection.GetFlankOffsets(gateDir, out Vector2Int oa, out Vector2Int ob);
            ElementType? tA = getCellType(generatorPosition + oa);
            ElementType? tB = getCellType(generatorPosition + ob);
            return tA == ElementType.Border && tB == ElementType.Border;
        }
    }
}
