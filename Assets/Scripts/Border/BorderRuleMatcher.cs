using System.Collections.Generic;
using UnityEngine;

namespace BorderSpawnModule
{
    public static class BorderRuleMatcher
    {
        // 3x3 grid layout (row-major, top-to-bottom):
        // [0]TopLeft    [1]Top    [2]TopRight
        // [3]Left       [4]Center [5]Right
        // [6]BottomLeft [7]Bottom [8]BottomRight

        private static readonly int[] OffsetX = { -1, 0, 1, -1, 0, 1, -1, 0, 1 };
        private static readonly int[] OffsetY = {  1, 1, 1,  0, 0, 0, -1,-1,-1 };

        private static readonly int[] Rotate90CW = { 6, 3, 0, 7, 4, 1, 8, 5, 2 };
        private static readonly int[] MirrorXMap = { 2, 1, 0, 5, 4, 3, 8, 7, 6 };
        private static readonly int[] MirrorYMap = { 6, 7, 8, 3, 4, 5, 0, 1, 2 };

        private static readonly NeighborCondition[] _scratchA = new NeighborCondition[9];
        private static readonly NeighborCondition[] _scratchB = new NeighborCondition[9];
        private static readonly bool[] _cellMask = new bool[9];
        private static readonly BorderCellType[] _cellTypes = new BorderCellType[9];

        public readonly struct MatchContext
        {
            public readonly bool ThisUsesContinuityGrid;
            public readonly BorderCellType ThisMatchesCellType;
            public readonly bool TreatObstacleAsInnerTile;

            public MatchContext(bool thisUsesContinuityGrid, BorderCellType thisMatchesCellType, bool treatObstacleAsInnerTile)
            {
                ThisUsesContinuityGrid = thisUsesContinuityGrid;
                ThisMatchesCellType = thisMatchesCellType;
                TreatObstacleAsInnerTile = treatObstacleAsInnerTile;
            }

            public static MatchContext ForBorder()
            {
                return new MatchContext(true, BorderCellType.Border, true);
            }

            public static MatchContext ForObstacle()
            {
                return new MatchContext(false, BorderCellType.Obstacle, false);
            }
        }

        public static bool TryResolve(
            int cx, int cy,
            bool[] continuityGrid, BorderCellType[] cellTypeGrid, int sizeX, int sizeY,
            List<BorderRuleEntry> rules,
            out ResolvedBorderRule result)
        {
            return TryResolve(cx, cy, continuityGrid, cellTypeGrid, sizeX, sizeY, rules, MatchContext.ForBorder(), out result);
        }

        public static bool TryResolve(
            int cx, int cy,
            bool[] continuityGrid, BorderCellType[] cellTypeGrid, int sizeX, int sizeY,
            List<BorderRuleEntry> rules,
            MatchContext context,
            out ResolvedBorderRule result)
        {
            result = default;
            if (rules == null || rules.Count == 0) return false;

            FillCellMask(cx, cy, continuityGrid, cellTypeGrid, sizeX, sizeY, context);

            for (int ri = 0, rc = rules.Count; ri < rc; ri++)
            {
                BorderRuleEntry rule = rules[ri];
                if (!rule.Prefab) continue;

                NeighborCondition[] conditions = rule.Neighbors;
                if (conditions == null || conditions.Length != 9) continue;

                switch (rule.RuleType)
                {
                    case BorderRuleType.Fixed:
                        if (MatchesConditions(conditions, context))
                        {
                            result = BuildResult(rule, 0f, false, false);
                            return true;
                        }
                        break;

                    case BorderRuleType.Rotated:
                        CopyTo(conditions, _scratchA);
                        for (int r = 0; r < 4; r++)
                        {
                            if (MatchesConditions(_scratchA, context))
                            {
                                result = BuildResult(rule, r * 90f, false, false);
                                return true;
                            }
                            ApplyTransformInPlace(_scratchA, _scratchB, Rotate90CW);
                            CopyTo(_scratchB, _scratchA);
                        }
                        break;

                    case BorderRuleType.MirrorX:
                        if (MatchesConditions(conditions, context))
                        {
                            result = BuildResult(rule, 0f, false, false);
                            return true;
                        }
                        ApplyTransformInPlace(conditions, _scratchA, MirrorXMap);
                        if (MatchesConditions(_scratchA, context))
                        {
                            result = BuildResult(rule, 0f, true, false);
                            return true;
                        }
                        break;

                    case BorderRuleType.MirrorY:
                        if (MatchesConditions(conditions, context))
                        {
                            result = BuildResult(rule, 0f, false, false);
                            return true;
                        }
                        ApplyTransformInPlace(conditions, _scratchA, MirrorYMap);
                        if (MatchesConditions(_scratchA, context))
                        {
                            result = BuildResult(rule, 0f, false, true);
                            return true;
                        }
                        break;

                    case BorderRuleType.MirrorXY:
                        if (MatchesConditions(conditions, context))
                        {
                            result = BuildResult(rule, 0f, false, false);
                            return true;
                        }
                        ApplyTransformInPlace(conditions, _scratchA, MirrorXMap);
                        if (MatchesConditions(_scratchA, context))
                        {
                            result = BuildResult(rule, 0f, true, false);
                            return true;
                        }
                        ApplyTransformInPlace(conditions, _scratchA, MirrorYMap);
                        if (MatchesConditions(_scratchA, context))
                        {
                            result = BuildResult(rule, 0f, false, true);
                            return true;
                        }
                        ApplyTransformInPlace(conditions, _scratchB, MirrorXMap);
                        ApplyTransformInPlace(_scratchB, _scratchA, MirrorYMap);
                        if (MatchesConditions(_scratchA, context))
                        {
                            result = BuildResult(rule, 0f, true, true);
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        private static void FillCellMask(int cx, int cy, bool[] continuityGrid, BorderCellType[] cellTypeGrid, int sizeX, int sizeY, MatchContext context)
        {
            for (int i = 0; i < 9; i++)
            {
                if (i == 4)
                {
                    _cellMask[i] = true;
                    _cellTypes[i] = context.ThisMatchesCellType;
                    continue;
                }

                int nx = cx + OffsetX[i];
                int ny = cy + OffsetY[i];
                bool isInBounds = nx >= 0 && ny >= 0 && nx < sizeX && ny < sizeY;
                if (!isInBounds)
                {
                    _cellMask[i] = false;
                    _cellTypes[i] = BorderCellType.Empty;
                    continue;
                }

                int flatIndex = ny * sizeX + nx;
                BorderCellType cellType = cellTypeGrid[flatIndex];

                _cellTypes[i] = cellType;
                _cellMask[i] = context.ThisUsesContinuityGrid ? continuityGrid[flatIndex] : cellType == context.ThisMatchesCellType;
            }
        }

        private static bool MatchesConditions(NeighborCondition[] conditions, MatchContext context)
        {
            for (int i = 0; i < 9; i++)
            {
                if (i == 4) continue;
                NeighborCondition c = conditions[i];
                if (c == NeighborCondition.DontCare) continue;
                if (c == NeighborCondition.This && !_cellMask[i]) return false;
                if (c == NeighborCondition.NotThis && _cellMask[i]) return false;
                if (c == NeighborCondition.InnerTile)
                {
                    BorderCellType type = _cellTypes[i];
                    if (type == BorderCellType.InnerTile) continue;
                    if (context.TreatObstacleAsInnerTile && type == BorderCellType.Obstacle) continue;
                    return false;
                }
                if (c == NeighborCondition.Gate && _cellTypes[i] != BorderCellType.Gate) return false;
                if (c == NeighborCondition.Obstacle && _cellTypes[i] != BorderCellType.Obstacle) return false;
            }
            return true;
        }

        private static void CopyTo(NeighborCondition[] source, NeighborCondition[] dest)
        {
            for (int i = 0; i < 9; i++) dest[i] = source[i];
        }

        private static void ApplyTransformInPlace(NeighborCondition[] source, NeighborCondition[] dest, int[] mapping)
        {
            for (int i = 0; i < 9; i++) dest[i] = source[mapping[i]];
        }

        private static ResolvedBorderRule BuildResult(BorderRuleEntry rule, float rotationY, bool mirrorX, bool mirrorY)
        {
            Vector3 scale = Vector3.one;
            if (mirrorX) scale.x = -1f;
            if (mirrorY) scale.z = -1f;

            return new ResolvedBorderRule
            {
                RuleName = rule.RuleName,
                Prefab = rule.Prefab,
                Rotation = Quaternion.Euler(rule.RotationOffset + Vector3.up * rotationY),
                Scale = scale,
                PositionOffset = rule.PositionOffset
            };
        }
    }
}
