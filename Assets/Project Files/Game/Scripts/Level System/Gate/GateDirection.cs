using System;
using UnityEngine;

namespace WaterFlow.Game
{
    public class GateDirection
    {
        public static readonly GateDirection[] DIRECTIONS = new GateDirection[]
        {
            // Left
            new GateDirection()
            {
                PositionOffset = new Vector2Int(-1, 0),

                CalculateAlignedPosition = (basePos, fig, i) => new Vector2Int(basePos.x - 1, basePos.y + i),

                GetAlignedSize = (fig) => fig.Size.y,
                GetNonAlignedSize = (fig) => fig.Size.x,

                GetMoveOffset = (fig) => new Vector3(-fig.Size.x, 0, 0),
                DirectionNormal = new Vector3(1, 0, 0),
                DirectionScale = 1,
                BorderInnerGroundOffset = new Vector3(0.375f, 0, 0),
                PipeOccupiedOffsets = new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(-2, 0),
                    new Vector2Int(-3, 0)
                },

                PipeRotation = 180,
            },

            // Right
            new GateDirection()
            {
                PositionOffset = new Vector2Int(1, 0),

                CalculateAlignedPosition = (basePos, fig, i) => new Vector2Int(basePos.x + fig.Size.x, basePos.y + i),

                GetAlignedSize = (fig) => fig.Size.y,
                GetNonAlignedSize = (fig) => fig.Size.x,

                GetMoveOffset = (fig) => new Vector3(fig.Size.x, 0, 0),
                DirectionNormal = new Vector3(-1, 0, 0),
                DirectionScale = -1,
                BorderInnerGroundOffset = new Vector3(-0.375f, 0, 0),
                PipeOccupiedOffsets = new[]
                {
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0),
                    new Vector2Int(3, 0)
                },

                PipeRotation = 0,
            },

            // Top
            new GateDirection()
            {
                PositionOffset = new Vector2Int(0, 1),

                CalculateAlignedPosition = (basePos, fig, i) => new Vector2Int(basePos.x + i, basePos.y + fig.Size.y),

                GetAlignedSize = (fig) => fig.Size.x,
                GetNonAlignedSize = (fig) => fig.Size.y,

                GetMoveOffset = (fig) => new Vector3(0, 0, fig.Size.y),
                DirectionNormal = new Vector3(0, 0, -1),
                DirectionScale = 1,
                BorderInnerGroundOffset = new Vector3(0, 0, -0.375f),
                PipeOccupiedOffsets = new[]
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(0, 2),
                    new Vector2Int(0, 3)
                },

                PipeRotation = 180,
            },

            // Bottom
            new GateDirection()
            {
                PositionOffset = new Vector2Int(0, -1),

                CalculateAlignedPosition = (basePos, fig, i) => new Vector2Int(basePos.x + i, basePos.y - 1),

                GetAlignedSize = (fig) => fig.Size.x,
                GetNonAlignedSize = (fig) => fig.Size.y,

                GetMoveOffset = (fig) => new Vector3(0, 0, -fig.Size.y),
                DirectionNormal = new Vector3(0, 0, 1),
                DirectionScale = -1,
                BorderInnerGroundOffset = new Vector3(0, 0, 0.375f),
                PipeOccupiedOffsets = new[]
                {
                    new Vector2Int(0, -1),
                    new Vector2Int(0, -2),
                    new Vector2Int(0, -3)
                },

                PipeRotation = 0,
            }
        };

        public Vector2Int PositionOffset { get; private set; }

        public Func<Vector2Int, LevelFigure, int, Vector2Int> CalculateAlignedPosition { get; private set; }

        public Func<LevelFigure, int> GetAlignedSize { get; private set; }
        public Func<LevelFigure, int> GetNonAlignedSize { get; private set; }
        public Func<LevelFigure, Vector3> GetMoveOffset { get; private set; }

        public Vector3 DirectionNormal { get; private set; }
        public int DirectionScale { get; private set; }
        public Vector3 BorderInnerGroundOffset { get; private set; }
        public Vector2Int[] PipeOccupiedOffsets { get; private set; }
        public int PipeRotation { get; private set; }

        public Vector3 Position => new Vector3(PositionOffset.x, 0, PositionOffset.y);

        public enum Type
        {
            None = -1,
            Left = 0,
            Right = 1,
            Top = 2,
            Bottom = 3
        }

        /// <summary>
        /// Neighbor offsets along the wall, on both sides of a gate/generator on the map edge.
        /// </summary>
        public static void GetFlankOffsets(Type dir, out Vector2Int offsetA, out Vector2Int offsetB)
        {
            switch (dir)
            {
                case Type.Left:
                case Type.Right:
                    offsetA = new Vector2Int(0, -1);
                    offsetB = new Vector2Int(0, 1);
                    break;
                case Type.Top:
                case Type.Bottom:
                    offsetA = new Vector2Int(-1, 0);
                    offsetB = new Vector2Int(1, 0);
                    break;
                default:
                    offsetA = Vector2Int.zero;
                    offsetB = Vector2Int.zero;
                    break;
            }
        }
    }
}