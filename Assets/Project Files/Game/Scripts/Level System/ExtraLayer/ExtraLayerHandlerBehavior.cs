using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Base for runtime handlers of a level's extra layer (see <see cref="ExtraLayerType"/>).
    /// One handler is instantiated per level that has an extra layer; the concrete type is chosen
    /// data-driven via <see cref="EnvironmentData.GetExtraLayerHandlerPrefab"/> — adding a new
    /// extra-layer type means adding a subclass plus a prefab entry, with no spawn-pipeline edits.
    /// </summary>
    public abstract class ExtraLayerHandlerBehavior : MonoBehaviour
    {
        /// <summary>True once this handler has released its elements into <see cref="LevelRepresentation.ActiveBlocks"/> (or has nothing left to release).</summary>
        public virtual bool HasSpawned => false;

        /// <summary>
        /// Initializes the handler. Returns false when the handler has nothing to do, so the caller
        /// can destroy the instance (mirrors <see cref="GeneratorBehavior.Init"/>'s return contract).
        /// </summary>
        public abstract bool Init(
            LevelRepresentation level,
            ILevelContentProvider contentProvider,
            IBlockCellWatchService watchService,
            IReadOnlyList<LevelElementData> extraLayerElements,
            Func<bool> isLevelStarted);
    }
}
