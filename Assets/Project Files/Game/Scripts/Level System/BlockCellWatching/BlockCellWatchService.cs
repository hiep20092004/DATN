using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Tracks which "watched" cells are occupied by active blocks and notifies the registered
    /// <see cref="IBlockCellWatcher"/>s (lift, generator, …) when their watched region gains its
    /// first occupant or loses its last one.
    ///
    /// Occupancy is always re-derived from the live <see cref="IElementProvider.ActiveBlocks"/>
    /// (see <see cref="ResyncWatcherFromActiveBlocks"/>); the cached dictionaries are just a fast index.
    /// All block events follow the same shape: capture a baseline → resync from live blocks →
    /// dispatch per-block hooks → fire crossing-threshold callbacks.
    /// </summary>
    public sealed class BlockCellWatchService : IBlockCellWatchService, IBlockCellEventNotifier
    {
        private readonly IElementProvider elementProvider;

        // Forward / reverse indices between watchers and the cells they watch.
        private readonly Dictionary<Vector2Int, HashSet<IBlockCellWatcher>> cellToWatchers = new();
        private readonly Dictionary<IBlockCellWatcher, HashSet<Vector2Int>> watcherToCells = new();

        // Cached occupancy: which block sits on a watched cell, and how many cells each watcher has occupied.
        private readonly Dictionary<Vector2Int, LevelBlockBehavior> watchedCellOccupant = new();
        private readonly Dictionary<IBlockCellWatcher, int> watcherOccupiedCount = new();

        // Reverse lookup: which watched cells a block currently occupies.
        private readonly Dictionary<LevelBlockBehavior, HashSet<Vector2Int>> blockToWatchedCells = new();

        // Reusable buffers to avoid per-event allocations on the frequent block-move path.
        private readonly HashSet<IBlockCellWatcher> affectedWatchers = new();
        private readonly List<IBlockCellWatcher> affectedWatchersList = new();
        private readonly List<Vector2Int> blockCellsBuffer = new();
        private readonly List<Vector2Int> vacatedCellsBuffer = new();
        private readonly List<Vector2Int> occupiedCellsBuffer = new();
        private readonly List<Vector2Int> perWatcherVacatedBuffer = new();
        private readonly List<Vector2Int> perWatcherOccupiedBuffer = new();
        private readonly Dictionary<IBlockCellWatcher, int> preOccupancyByWatcher = new();
        private readonly List<KeyValuePair<IBlockCellWatcher, int>> thresholdSnapshot = new();

        public BlockCellWatchService(IElementProvider elementProvider)
        {
            this.elementProvider = elementProvider;
        }

        // ── IBlockCellWatchService ────────────────────────────────────────────────
        public bool IsCellOccupied(Vector2Int cell)
        {
            return watchedCellOccupant.TryGetValue(cell, out var occupant) && occupant != null;
        }

        /// <summary>Number of cells occupied according to the live block positions.</summary>
        public int GetOccupiedCount(IEnumerable<Vector2Int> cells)
        {
            return GetOccupiedCount(cells, null);
        }

        /// <summary>Number of cells occupied according to the cached occupancy index.</summary>
        public int GetRegisteredOccupiedCount(IEnumerable<Vector2Int> cells)
        {
            if (cells == null) return 0;

            int count = 0;
            foreach (Vector2Int cell in cells)
                if (IsCellOccupied(cell)) count++;
            return count;
        }

        public void Register(IBlockCellWatcher watcher)
        {
            if (watcher == null) return;
            if (watcherToCells.ContainsKey(watcher)) return;

            var cells = new HashSet<Vector2Int>();
            IReadOnlyList<Vector2Int> watched = watcher.WatchedCells;
            if (watched != null)
            {
                for (int i = 0; i < watched.Count; i++)
                    cells.Add(watched[i]);
            }

            watcherToCells[watcher] = cells;
            watcherOccupiedCount[watcher] = 0;

            foreach (Vector2Int cell in cells)
            {
                if (!cellToWatchers.TryGetValue(cell, out var set))
                {
                    set = new HashSet<IBlockCellWatcher>();
                    cellToWatchers[cell] = set;
                }
                set.Add(watcher);

                watchedCellOccupant.TryAdd(cell, null);
            }

            InitWatcherFromExistingBlocks(watcher, cells);
        }

        public void Unregister(IBlockCellWatcher watcher)
        {
            if (watcher == null) return;
            if (!watcherToCells.TryGetValue(watcher, out var cells)) return;

            foreach (Vector2Int cell in cells)
            {
                if (!cellToWatchers.TryGetValue(cell, out var set)) continue;

                set.Remove(watcher);
                if (set.Count == 0)
                {
                    cellToWatchers.Remove(cell);
                    watchedCellOccupant.Remove(cell);
                }
            }

            watcherToCells.Remove(watcher);
            watcherOccupiedCount.Remove(watcher);
        }

        // ── IBlockCellEventNotifier ───────────────────────────────────────────────
        public void OnNewBlockSpawn(LevelBlockBehavior block)
        {
            if (!block) return;

            CollectWatchersForCells(block.GetOccupiedCells(), affectedWatchers);
            CaptureBaseline(affectedWatchers);
            ResyncAll(affectedWatchers);
            FireWatcherThresholdCallbacks();
        }

        public void NotifyBlockPicked(LevelBlockBehavior block)
        {
            if (!block) return;

            GatherWatchersOccupiedBy(block, affectedWatchers);
            foreach (IBlockCellWatcher watcher in affectedWatchers)
                if (watcher is IWatchedBlockMoveListener move) move.OnWatchedBlockPicked(block);
        }

        public void NotifyBlockReleased(LevelBlockBehavior block, Vector2Int snapTargetPosition)
        {
            if (!block) return;

            GatherWatchersOccupiedBy(block, affectedWatchers);
            foreach (IBlockCellWatcher watcher in affectedWatchers)
                if (watcher is IWatchedBlockMoveListener move) move.OnWatchedBlockReleased(block, snapTargetPosition);

            GatherWatchedCellsOccupiedBy(block, blockCellsBuffer);

            BuildReleaseDelta(block);
            CollectWatchersForCells(vacatedCellsBuffer, affectedWatchers, clear: false);
            CollectWatchersForCells(occupiedCellsBuffer, affectedWatchers, clear: false);

            CaptureBaseline(affectedWatchers);
            ResyncAll(affectedWatchers);

            DispatchCellsChanged(block);
            FireWatcherThresholdCallbacks();
        }

        public void NotifyBlockDestructed(LevelBlockBehavior block)
        {
            if (!block) return;

            GatherWatchedCellsOccupiedBy(block, blockCellsBuffer);
            if (blockCellsBuffer.Count == 0)
                return;

            CollectWatchersForCells(blockCellsBuffer, affectedWatchers);
            CaptureBaseline(affectedWatchers);

            // Exclude the destructing block: it may still appear in ActiveBlocks for one more frame.
            ResyncAll(affectedWatchers, block);

            DispatchCellsDestructed(block);
            FireWatcherThresholdCallbacks(block);
        }

        public void NotifyBlockWatchOccupancyClearedSilently(LevelBlockBehavior block)
        {
            if (!block) return;

            GatherWatchedCellsOccupiedBy(block, blockCellsBuffer);
            for (int i = 0; i < blockCellsBuffer.Count; i++)
                SilentVacateCellIfOccupiedBy(blockCellsBuffer[i], block);
        }

        public void Clear()
        {
            // Snapshot first: callbacks may unregister watchers and mutate the dictionaries.
            var snapshot = new List<KeyValuePair<IBlockCellWatcher, int>>(watcherOccupiedCount);
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Value > 0 && snapshot[i].Key is IWatchedRegionThresholdListener threshold)
                    threshold.OnAllWatchedCellsVacated();
            }

            cellToWatchers.Clear();
            watcherToCells.Clear();
            watchedCellOccupant.Clear();
            watcherOccupiedCount.Clear();
            blockToWatchedCells.Clear();
        }

        // ── Batch pipeline helpers ────────────────────────────────────────────────
        /// <summary>Snapshots the cached occupancy of each affected watcher as the pre-change baseline.</summary>
        private void CaptureBaseline(HashSet<IBlockCellWatcher> watchers)
        {
            preOccupancyByWatcher.Clear();
            foreach (IBlockCellWatcher watcher in watchers)
            {
                if (watcher == null) continue;
                preOccupancyByWatcher[watcher] = GetRegisteredWatcherOccupiedCount(watcher);
            }
        }

        private void ResyncAll(HashSet<IBlockCellWatcher> watchers, LevelBlockBehavior excludeBlock = null)
        {
            foreach (IBlockCellWatcher watcher in watchers)
                ResyncWatcherFromActiveBlocks(watcher, excludeBlock);
        }

        /// <summary>
        /// Fires <see cref="IWatchedRegionThresholdListener.OnAllWatchedCellsVacated"/> /
        /// <see cref="IWatchedRegionThresholdListener.OnAnyWatchedCellOccupied"/> for watchers whose
        /// occupancy crossed the empty/non-empty threshold since the last <see cref="CaptureBaseline"/>.
        /// </summary>
        private void FireWatcherThresholdCallbacks(LevelBlockBehavior excludeFromLiveCount = null)
        {
            // Snapshot: a callback (e.g. lift TrySpawnLift → OnNewBlockSpawn) may clear preOccupancyByWatcher.
            thresholdSnapshot.Clear();
            foreach (var kvp in preOccupancyByWatcher)
                thresholdSnapshot.Add(kvp);

            for (int i = 0; i < thresholdSnapshot.Count; i++)
            {
                if (thresholdSnapshot[i].Key is not IWatchedRegionThresholdListener threshold)
                    continue;

                int before = thresholdSnapshot[i].Value;
                int afterLive = GetWatcherOccupiedCount(threshold, excludeFromLiveCount);
                int afterCached = GetRegisteredWatcherOccupiedCount(threshold);

                if (before > 0 && afterLive == 0 && afterCached == 0)
                    threshold.OnAllWatchedCellsVacated();
                else if (before == 0 && (afterLive > 0 || afterCached > 0))
                    threshold.OnAnyWatchedCellOccupied();
            }
        }

        /// <summary>Computes which watched cells the released block vacated and which it now occupies.</summary>
        private void BuildReleaseDelta(LevelBlockBehavior block)
        {
            vacatedCellsBuffer.Clear();
            for (int i = 0; i < blockCellsBuffer.Count; i++)
            {
                Vector2Int cell = blockCellsBuffer[i];
                if (!block.IsOverlapsCell(cell))
                    vacatedCellsBuffer.Add(cell);
            }

            occupiedCellsBuffer.Clear();
            foreach (IBlockCellWatcher watcher in affectedWatchers)
            {
                if (watcher == null) continue;
                if (!watcherToCells.TryGetValue(watcher, out HashSet<Vector2Int> watchedSet) || watchedSet == null)
                    continue;

                foreach (Vector2Int cell in watchedSet)
                {
                    if (!block.IsOverlapsCell(cell)) continue;

                    watchedCellOccupant.TryGetValue(cell, out LevelBlockBehavior prev);
                    if (prev != null && prev != block) continue;

                    if (!occupiedCellsBuffer.Contains(cell))
                        occupiedCellsBuffer.Add(cell);
                }
            }
        }

        private void DispatchCellsChanged(LevelBlockBehavior block)
        {
            CopyAffectedWatchers();
            for (int i = 0; i < affectedWatchersList.Count; i++)
            {
                IBlockCellWatcher watcher = affectedWatchersList[i];
                if (watcher is not IWatchedBlockMoveListener move) continue;
                if (!TryGetWatchedCells(watcher, out HashSet<Vector2Int> watchedSet))
                    continue;

                FilterCellsByWatcher(vacatedCellsBuffer, watchedSet, perWatcherVacatedBuffer);
                FilterCellsByWatcher(occupiedCellsBuffer, watchedSet, perWatcherOccupiedBuffer);
                move.OnWatchedBlockCellsChanged(perWatcherVacatedBuffer, perWatcherOccupiedBuffer, block);
            }
        }

        private void DispatchCellsDestructed(LevelBlockBehavior block)
        {
            CopyAffectedWatchers();
            for (int i = 0; i < affectedWatchersList.Count; i++)
            {
                IBlockCellWatcher watcher = affectedWatchersList[i];
                if (watcher is not IWatchedBlockMoveListener move) continue;
                if (!TryGetWatchedCells(watcher, out HashSet<Vector2Int> watchedSet))
                    continue;

                FilterCellsByWatcher(blockCellsBuffer, watchedSet, perWatcherVacatedBuffer);
                move.OnWatchedBlockCellsDestructed(perWatcherVacatedBuffer, block);
            }
        }

        private void CopyAffectedWatchers()
        {
            affectedWatchersList.Clear();
            foreach (IBlockCellWatcher watcher in affectedWatchers)
                if (watcher != null) affectedWatchersList.Add(watcher);
        }

        private bool TryGetWatchedCells(IBlockCellWatcher watcher, out HashSet<Vector2Int> watchedSet)
        {
            watchedSet = null;
            return watcher != null
                && watcherToCells.TryGetValue(watcher, out watchedSet)
                && watchedSet != null;
        }

        private static void FilterCellsByWatcher(
            List<Vector2Int> source, HashSet<Vector2Int> watchedSet, List<Vector2Int> result)
        {
            result.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                if (watchedSet.Contains(source[i]))
                    result.Add(source[i]);
            }
        }

        // ── Occupancy queries / indexing ──────────────────────────────────────────
        private int GetOccupiedCount(IEnumerable<Vector2Int> cells, LevelBlockBehavior excludeBlock)
        {
            if (cells == null) return 0;

            IReadOnlyList<LevelBlockBehavior> blocks = elementProvider?.ActiveBlocks;
            if (blocks == null || blocks.Count == 0)
                return GetRegisteredOccupiedCount(cells);

            int count = 0;
            foreach (Vector2Int cell in cells)
            {
                if (FindActiveBlockOverlappingCell(blocks, cell, excludeBlock) != null)
                    count++;
            }

            return count;
        }

        private int GetWatcherOccupiedCount(IBlockCellWatcher watcher, LevelBlockBehavior excludeBlock = null)
        {
            return watcher == null ? 0 : GetOccupiedCount(watcher.WatchedCells, excludeBlock);
        }

        private int GetRegisteredWatcherOccupiedCount(IBlockCellWatcher watcher)
        {
            return watcher == null ? 0 : GetRegisteredOccupiedCount(watcher.WatchedCells);
        }

        private void CollectWatchersForCells(
            IReadOnlyList<Vector2Int> cells, HashSet<IBlockCellWatcher> result, bool clear = true)
        {
            if (clear) result.Clear();
            if (cells == null) return;
            for (int i = 0; i < cells.Count; i++)
                AddWatchersForCell(cells[i], result);
        }

        private void AddWatchersForCell(Vector2Int cell, HashSet<IBlockCellWatcher> result)
        {
            if (result == null) return;
            if (!cellToWatchers.TryGetValue(cell, out var watchers) || watchers == null) return;
            foreach (IBlockCellWatcher watcher in watchers)
                if (watcher != null) result.Add(watcher);
        }

        private void GatherWatchedCellsOccupiedBy(LevelBlockBehavior block, List<Vector2Int> result)
        {
            result.Clear();
            if (!block) return;
            if (!blockToWatchedCells.TryGetValue(block, out var cells) || cells == null || cells.Count == 0)
                return;
            result.AddRange(cells);
        }

        private void GatherWatchersOccupiedBy(LevelBlockBehavior block, HashSet<IBlockCellWatcher> result)
        {
            result.Clear();
            if (!block) return;
            if (!blockToWatchedCells.TryGetValue(block, out var cells) || cells == null || cells.Count == 0)
                return;

            foreach (Vector2Int cell in cells)
                AddWatchersForCell(cell, result);
        }

        private void InitWatcherFromExistingBlocks(IBlockCellWatcher watcher, HashSet<Vector2Int> cells)
        {
            IReadOnlyList<LevelBlockBehavior> blocks = elementProvider?.ActiveBlocks;
            if (blocks == null) return;

            for (int i = 0; i < blocks.Count; i++)
            {
                LevelBlockBehavior block = blocks[i];
                if (!block) continue;

                Vector2Int[] occupied = block.GetOccupiedCells();
                for (int c = 0; c < occupied.Length; c++)
                {
                    Vector2Int cell = occupied[c];
                    if (cells.Contains(cell))
                        OccupyCellForSingleWatcher(cell, block, watcher);
                }
            }
        }

        private void OccupyCellForSingleWatcher(Vector2Int cell, LevelBlockBehavior block, IBlockCellWatcher watcher)
        {
            if (watcher == null) return;

            watchedCellOccupant[cell] = block;
            AddBlockWatchedCell(block, cell);
            IncrementWatcherOccupied(watcher);
        }

        /// <summary>
        /// Clears the block's occupancy of <paramref name="cell"/> and updates counts. Used when a
        /// replacement block will be registered the same frame.
        /// </summary>
        private void SilentVacateCellIfOccupiedBy(Vector2Int cell, LevelBlockBehavior block)
        {
            if (!watchedCellOccupant.TryGetValue(cell, out var current) || current != block)
                return;

            watchedCellOccupant[cell] = null;
            RemoveBlockWatchedCell(block, cell);

            if (!cellToWatchers.TryGetValue(cell, out var watchers) || watchers == null)
                return;

            foreach (IBlockCellWatcher watcher in watchers)
                DecrementWatcherOccupied(watcher);
        }

        private void AddBlockWatchedCell(LevelBlockBehavior block, Vector2Int cell)
        {
            if (!block) return;
            if (!blockToWatchedCells.TryGetValue(block, out var set) || set == null)
            {
                set = new HashSet<Vector2Int>();
                blockToWatchedCells[block] = set;
            }
            set.Add(cell);
        }

        private void RemoveBlockWatchedCell(LevelBlockBehavior block, Vector2Int cell)
        {
            if (!block) return;
            if (!blockToWatchedCells.TryGetValue(block, out var set) || set == null) return;
            set.Remove(cell);
            if (set.Count == 0)
                blockToWatchedCells.Remove(block);
        }

        private void IncrementWatcherOccupied(IBlockCellWatcher watcher)
        {
            if (!watcherOccupiedCount.TryGetValue(watcher, out int count))
                return;

            watcherOccupiedCount[watcher] = count + 1;
            if (count == 0 && watcher is IWatchedRegionThresholdListener threshold)
                threshold.OnAnyWatchedCellOccupied();
        }

        private void DecrementWatcherOccupied(IBlockCellWatcher watcher)
        {
            if (watcher == null) return;
            if (!watcherOccupiedCount.TryGetValue(watcher, out int count))
                return;

            int next = Mathf.Max(0, count - 1);
            watcherOccupiedCount[watcher] = next;

            // Only fire the "fully vacated" threshold once both caches and live blocks agree it is empty.
            if (count == 1 && next == 0
                && watcher is IWatchedRegionThresholdListener threshold
                && GetWatcherOccupiedCount(watcher) == 0
                && GetRegisteredWatcherOccupiedCount(watcher) == 0)
            {
                threshold.OnAllWatchedCellsVacated();
            }
        }

        /// <summary>
        /// Rebuilds the cached occupancy for a watcher's cells from the live block positions.
        /// Corrects desync when a group Rigidbody snap and its member transforms disagree.
        /// </summary>
        private void ResyncWatcherFromActiveBlocks(IBlockCellWatcher watcher, LevelBlockBehavior excludeBlock = null)
        {
            if (watcher == null) return;
            if (!watcherToCells.TryGetValue(watcher, out HashSet<Vector2Int> cells) || cells == null)
                return;

            foreach (Vector2Int cell in cells)
            {
                if (watchedCellOccupant.TryGetValue(cell, out LevelBlockBehavior prev) && prev)
                    RemoveBlockWatchedCell(prev, cell);
                watchedCellOccupant[cell] = null;
            }

            IReadOnlyList<LevelBlockBehavior> blocks = elementProvider?.ActiveBlocks;
            if (blocks == null)
            {
                watcherOccupiedCount[watcher] = 0;
                return;
            }

            int count = 0;
            foreach (Vector2Int cell in cells)
            {
                LevelBlockBehavior occupant = FindActiveBlockOverlappingCell(blocks, cell, excludeBlock);
                if (!occupant) continue;

                watchedCellOccupant[cell] = occupant;
                AddBlockWatchedCell(occupant, cell);
                count++;
            }

            watcherOccupiedCount[watcher] = count;
        }

        private static LevelBlockBehavior FindActiveBlockOverlappingCell(
            IReadOnlyList<LevelBlockBehavior> blocks, Vector2Int cell, LevelBlockBehavior excludeBlock)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                LevelBlockBehavior block = blocks[i];
                if (!block || block == excludeBlock) continue;
                if (block.IsOverlapsCell(cell))
                    return block;
            }

            return null;
        }
    }
}
