using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>Side of <see cref="ConnectedBlocks.BlockA"/> that <see cref="ConnectedBlocks.BlockB"/> sits on.</summary>
    public enum BlockConnectionDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// One adjacency between two blocks, with the orientation a link visual needs to align itself.
    /// Lives next to the grouping infrastructure rather than inside a single effect: it started out
    /// nested in the (now removed) combines effect and is shared by every effect that draws a
    /// connector between neighbouring cells.
    /// </summary>
    public class ConnectedBlocks
    {
        public LevelBlockBehavior BlockA { get; }
        public LevelBlockBehavior BlockB { get; }

        public Vector2Int CellA { get; }
        public Vector2Int CellB { get; }

        public Vector3 PositionA => new Vector3(CellA.x, 0, CellA.y);
        public Vector3 PositionB => new Vector3(CellB.x, 0, CellB.y);

        public BlockConnectionDirection Direction { get; }
        public Quaternion Rotation { get; }

        public ConnectedBlocks(LevelBlockBehavior blockA, LevelBlockBehavior blockB, Vector2Int cellA, Vector2Int cellB)
        {
            BlockA = blockA;
            BlockB = blockB;
            CellA = cellA;
            CellB = cellB;

            // Rotation is baked here so visuals only read it: every connector prefab is authored facing
            // Down, so each direction carries the yaw that turns it to face the neighbour.
            Vector2Int diff = cellB - cellA;
            if (diff == new Vector2Int(0, 1))
            {
                Direction = BlockConnectionDirection.Up;
                Rotation = Quaternion.Euler(0, 180, 0);
            }
            else if (diff == new Vector2Int(0, -1))
            {
                Direction = BlockConnectionDirection.Down;
                Rotation = Quaternion.Euler(0, 0, 0);
            }
            else if (diff == new Vector2Int(1, 0))
            {
                Direction = BlockConnectionDirection.Right;
                Rotation = Quaternion.Euler(0, -90, 0);
            }
            else if (diff == new Vector2Int(-1, 0))
            {
                Direction = BlockConnectionDirection.Left;
                Rotation = Quaternion.Euler(0, 90, 0);
            }
        }
    }
}
