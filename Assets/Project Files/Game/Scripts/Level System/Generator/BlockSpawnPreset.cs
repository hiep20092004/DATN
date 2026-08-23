using System;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Per-block spawn cell in the generator 3×3 watched grid (see <see cref="GeneratorBehavior.BuildWatchedCells"/>).
    /// <see cref="Vector2Int.x"/> = column: 0 flankA, 1 center, 2 flankB.
    /// <see cref="Vector2Int.y"/> = row: 0 nearest board, 1 middle, 2 furthest.
    /// Watched cell list index = x + y * 3.
    /// </summary>
    [Serializable]
    public class BlockSpawnPreset
    {
        [SerializeField] private BlockType blockType;

        /// <summary>Indices match <see cref="GateDirection.Type"/>: Left=0, Right=1, Top=2, Bottom=3.</summary>
        [SerializeField] private Vector2Int[] spawnCellPerDirection = new Vector2Int[4];

        private static readonly Vector2Int DefaultCell = new Vector2Int(1, 0);

        public BlockType BlockType => blockType;

        public BlockSpawnPreset()
        {
            EnsureArrayLength();
            ApplyDefaultsToAllDirections();
        }

        public BlockSpawnPreset(BlockType type)
        {
            blockType = type;
            EnsureArrayLength();
            ApplyDefaultsToAllDirections();
        }

        public Vector2Int GetSpawnCell(GateDirection.Type dir)
        {
            EnsureArrayLength();
            int i = DirectionToIndex(dir);
            if (i < 0)
                return DefaultCell;
            return spawnCellPerDirection[i];
        }

        public void SetSpawnCell(GateDirection.Type dir, Vector2Int cell)
        {
            EnsureArrayLength();
            int i = DirectionToIndex(dir);
            if (i < 0)
                return;
            cell.x = Mathf.Clamp(cell.x, 0, 2);
            cell.y = Mathf.Clamp(cell.y, 0, 2);
            spawnCellPerDirection[i] = cell;
        }

        /// <summary>Watched cell index (0–8) for <paramref name="dir"/>.</summary>
        public int GetWatchedCellIndex(GateDirection.Type dir)
        {
            Vector2Int c = GetSpawnCell(dir);
            return c.x + c.y * 3;
        }

        internal void EnsureArrayLength()
        {
            if (spawnCellPerDirection == null || spawnCellPerDirection.Length != 4)
                spawnCellPerDirection = new Vector2Int[4];
        }

        internal void ApplyDefaultsToAllDirections()
        {
            EnsureArrayLength();
            for (int i = 0; i < 4; i++)
                spawnCellPerDirection[i] = DefaultCell;
        }

        private static int DirectionToIndex(GateDirection.Type dir)
        {
            return dir switch
            {
                GateDirection.Type.Left => 0,
                GateDirection.Type.Right => 1,
                GateDirection.Type.Top => 2,
                GateDirection.Type.Bottom => 3,
                _ => -1
            };
        }
    }
}
