using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Axis-aligned bounds of all occupied grid cells across a <see cref="BlockGroup"/>.
    /// Shared by editor gizmos and runtime visuals (e.g. container box sprite).
    /// </summary>
    public readonly struct BlockGroupOccupiedBounds
    {
        public readonly bool IsValid;
        public readonly Bounds WorldBounds;
        public readonly Vector2Int MinCell;
        public readonly Vector2Int MaxCell;

        public BlockGroupOccupiedBounds(Bounds worldBounds, Vector2Int minCell, Vector2Int maxCell)
        {
            IsValid = true;
            WorldBounds = worldBounds;
            MinCell = minCell;
            MaxCell = maxCell;
        }

        public Vector2Int CellCount => MaxCell - MinCell + Vector2Int.one;
        public Vector3 WorldCenter => WorldBounds.center;
        public Vector3 WorldSize => WorldBounds.size;
    }
}
