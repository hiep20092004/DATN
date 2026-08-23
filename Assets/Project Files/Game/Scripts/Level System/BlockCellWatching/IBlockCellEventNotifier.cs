using UnityEngine;

namespace WaterFlow.Game
{
    public interface IBlockCellEventNotifier
    {
        void OnNewBlockSpawn(LevelBlockBehavior block);
        void NotifyBlockPicked(LevelBlockBehavior block);
        void NotifyBlockReleased(LevelBlockBehavior block, Vector2Int snapTargetPosition);
        void NotifyBlockDestructed(LevelBlockBehavior block);
        /// <summary>
        /// Clears watched-cell occupancy for this block without watcher callbacks (e.g. no generator BeginSpawn).
        /// Use when replacement blocks are spawned the same frame via <see cref="OnNewBlockSpawn"/>.
        /// </summary>
        void NotifyBlockWatchOccupancyClearedSilently(LevelBlockBehavior block);
        void Clear();
    }
}

