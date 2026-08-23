using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Identity of anything that watches a fixed set of grid cells. Register the instance with
    /// <see cref="IBlockCellWatchService"/>; it will receive whichever capability callbacks it also
    /// implements (<see cref="IWatchedRegionThresholdListener"/> and/or
    /// <see cref="IWatchedBlockMoveListener"/>). Implement only the capabilities you actually need.
    /// </summary>
    public interface IBlockCellWatcher
    {
        IReadOnlyList<Vector2Int> WatchedCells { get; }
    }

    /// <summary>
    /// Notified when the watched region crosses the empty / non-empty boundary. Implement this when
    /// you only care that the region as a whole became occupied or fully cleared (e.g. the lift).
    /// </summary>
    public interface IWatchedRegionThresholdListener : IBlockCellWatcher
    {
        /// <summary>The region went from fully empty to having at least one occupied cell.</summary>
        void OnAnyWatchedCellOccupied();

        /// <summary>The last occupied watched cell became empty.</summary>
        void OnAllWatchedCellsVacated();
    }

    /// <summary>
    /// Notified about individual block movements over the watched cells (e.g. the generator, which
    /// toggles its footprint preview and spawns when a required cell is vacated). All cell lists are
    /// pre-filtered to this watcher's <see cref="IBlockCellWatcher.WatchedCells"/> set, and the
    /// service's internal state is already updated when these fire.
    /// </summary>
    public interface IWatchedBlockMoveListener : IBlockCellWatcher
    {
        void OnWatchedBlockPicked(LevelBlockBehavior block);

        void OnWatchedBlockReleased(LevelBlockBehavior block, Vector2Int snapTargetPosition);

        void OnWatchedBlockCellsChanged(
            IReadOnlyList<Vector2Int> vacatedWatchedCells,
            IReadOnlyList<Vector2Int> occupiedWatchedCells,
            LevelBlockBehavior block);

        void OnWatchedBlockCellsDestructed(
            IReadOnlyList<Vector2Int> vacatedWatchedCells,
            LevelBlockBehavior block);
    }
}
