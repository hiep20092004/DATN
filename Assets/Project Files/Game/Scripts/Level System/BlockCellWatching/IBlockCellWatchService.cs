using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    public interface IBlockCellWatchService
    {
        void Register(IBlockCellWatcher watcher);
        void Unregister(IBlockCellWatcher watcher);

        bool IsCellOccupied(Vector2Int cell);

        /// <summary>Occupied-cell count derived from the live block positions (ground truth).</summary>
        int GetOccupiedCount(IEnumerable<Vector2Int> cells);

        /// <summary>
        /// Occupied-cell count from the cached occupancy index. Can differ from
        /// <see cref="GetOccupiedCount"/> while a block reserves a cell it is not physically on yet
        /// (e.g. a generator block mid pipe-travel).
        /// </summary>
        int GetRegisteredOccupiedCount(IEnumerable<Vector2Int> cells);
    }
}
