using UnityEngine;

namespace WaterFlow.Game
{
    public static class WaterFlowUtilities
    {
        public static int GetBlockDepthAtColliderPosition(GateBehavior gate, LevelBlockBehavior block)
        {
            Vector2Int position = gate.Data.Position - gate.GateDirection.PositionOffset;
            BlockColor flowColor = gate.GetActiveColor();
            Vector2Int step = gate.Data.GateDirectionType switch
            {
                GateDirection.Type.Top => Vector2Int.down,
                GateDirection.Type.Bottom => Vector2Int.up,
                GateDirection.Type.Left => Vector2Int.down,
                GateDirection.Type.Right => Vector2Int.down,
                _ => Vector2Int.zero
            };
            bool checkDiffFlag = false;
            int depth = 0;
            for (int i = 0; i < 3; i++)
            {
                Vector2Int checkPosition = position + step * i;
                if (!block.IsOverlapsCell(checkPosition))
                    break;

                if (block.HasDualVisual())
                {
                    bool isMatch = IsFlowColorCell(block, checkPosition, flowColor);
                    if (checkDiffFlag && !isMatch)
                    {
                        break;
                    }
                    if (isMatch)
                    {
                        checkDiffFlag = true;
                    }
                }

                depth++;
            }

            return depth;
        }

        private static bool IsFlowColorCell(LevelBlockBehavior block, Vector2Int cellPosition, BlockColor flowColor)
        {
            if (flowColor == BlockColor.None)
                return false;

            Vector3 samplePosition = new Vector3(cellPosition.x, block.transform.position.y + 0.5f, cellPosition.y);
            BlockColor cellColor = block.GetColorFromPosition(samplePosition);
            return cellColor == flowColor;
        }
    }
}