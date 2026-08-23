using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Builds a <see cref="LevelData"/> snapshot of a live <see cref="LevelRepresentation"/>: board topology
    /// from <see cref="LevelRepresentation.LevelMatrix"/>, block positions/colors/effects from
    /// <see cref="LevelRepresentation.ActiveBlocks"/>, and remaining gate/generator queues read from their
    /// live behaviors. Used by the runtime "copy current level" cheat; shares its string format with the
    /// Level Editor Window copy/paste tool via <see cref="LevelDataSerializationUtilities"/>.
    /// </summary>
    public static class RuntimeLevelSnapshot
    {
        public static LevelData Capture(LevelRepresentation level)
        {
            LevelData snapshot = ScriptableObject.CreateInstance<LevelData>();
            string json = JsonUtility.ToJson(level.LevelData, false);
            JsonUtility.FromJsonOverwrite(json, snapshot);

            snapshot.Elements = BuildElements(level);
            ApplyExtraLayerState(level, snapshot);
            snapshot.Validate();
            return snapshot;
        }

        private static LevelElementData[] BuildElements(LevelRepresentation level)
        {
            Vector2Int size = level.Size;
            LevelElementData[,] matrix = level.LevelMatrix;

            var gatesByPosition = new Dictionary<Vector2Int, GateBehavior>();
            foreach (GateBehavior gate in level.EnvironmentSpawner.Gates)
                gatesByPosition[gate.Data.Position] = gate;

            var generatorsByPosition = new Dictionary<Vector2Int, GeneratorBehavior>();
            foreach (GeneratorBehavior generator in level.EnvironmentSpawner.Generators)
                generatorsByPosition[generator.Data.Position] = generator;

            // MatrixPosition is the figure's corner cell (SpawnBlock places the transform at
            // position - Figure.PivotPoint); the authored element position is the pivot cell.
            var pivotToBlock = new Dictionary<Vector2Int, LevelBlockBehavior>();
            foreach (LevelBlockBehavior block in level.ActiveBlocks)
                pivotToBlock[block.MatrixPosition + block.Figure.PivotPoint] = block;

            var elements = new List<LevelElementData>(size.x * size.y);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    LevelElementData original = matrix[x, y];
                    if (original == null)
                        continue;

                    var position = new Vector2Int(x, y);

                    // A live block is emitted at whatever cell it currently occupies. Blocks can rest on any
                    // walkable cell (InnerTile, or an InteractableObject they were pushed onto — both are in
                    // ElementType.IsCanSpawnBlock), so this runs before the type switch.
                    if (pivotToBlock.TryGetValue(position, out LevelBlockBehavior block))
                    {
                        elements.Add(BuildBlockElement(block));

                        // A block anchored on an interactable is a runtime-only state (the editor forbids it),
                        // but the elements array allows several entries at one cell and both SpawnBlocks and
                        // SpawnInteractiveObjects iterate that array — so keep the interactable too. Emit it
                        // AFTER the block so LevelMatrix resolves the cell to the interactable, matching the
                        // live board where the interactable is the static tile and the block sits on top.
                        if (original.Type == ElementType.InteractableObject)
                            elements.Add(original.Clone());
                        continue;
                    }

                    switch (original.Type)
                    {
                        case ElementType.Block:
                            // Authored block is gone (collected or moved away); reveal floor underneath.
                            var tile = new InnerTileLevelElementData();
                            tile.SetPosition(position);
                            elements.Add(tile);
                            break;

                        case ElementType.Gate:
                            elements.Add(BuildGateElement(original, position, gatesByPosition));
                            break;

                        case ElementType.Generator:
                            elements.Add(BuildGeneratorElement(original, position, generatorsByPosition));
                            break;

                        default:
                            // Empty, InnerTile, Obstacle, InteractableObject, Border with no block: preserve as authored.
                            elements.Add(original.Clone());
                            break;
                    }
                }
            }

            return elements.ToArray();
        }

        private static BlockLevelElementData BuildBlockElement(LevelBlockBehavior block)
        {
            var element = new BlockLevelElementData();
            element.SetPosition(block.MatrixPosition + block.Figure.PivotPoint);
            element.SetBlockType(block.BlockConfig.Type);
            element.SetBlockColor(block.OriginColorConfig.Type);

            List<BlockEffectBehavior> behaviors = block.Effects;
            var effects = new List<BlockEffectData>(behaviors.Count);
            foreach (BlockEffectBehavior effect in behaviors)
            {
                // Cleared effects (broken container, melted ice, ...) stay in the list with
                // isActive=false — they must not be written back into the snapshot.
                if (!effect.IsActive)
                    continue;

                BlockEffectData currentData = effect.GetCurrentEffectData();
                if (currentData != null)
                    effects.Add(currentData);
            }
            element.SetBlockEffects(effects.ToArray());

            return element;
        }

        private static LevelElementData BuildGateElement(
            LevelElementData original, Vector2Int position, Dictionary<Vector2Int, GateBehavior> gatesByPosition)
        {
            var clone = (GateLevelElementData)original.Clone();
            if (!gatesByPosition.TryGetValue(position, out GateBehavior gate))
                return clone;

            clone.SetGateData(gate.GetRemainingGateData());

            // Rebuild effects from the LIVE gate's list, not the authored one: cleared effects (broken ice,
            // opened lock) are inactive and dropped, counters come back current, and a transferred moving
            // lock is captured on the gate it currently sits on.
            List<GateEffectBehavior> behaviors = gate.Effects;
            var effects = new List<GateEffectData>(behaviors?.Count ?? 0);
            if (behaviors != null)
            {
                foreach (GateEffectBehavior effect in behaviors)
                {
                    if (!effect || !effect.IsActive)
                        continue;

                    GateEffectData currentData = effect.GetCurrentEffectData();
                    if (currentData != null)
                        effects.Add(currentData);
                }
            }
            clone.SetGateEffects(effects.ToArray());

            return clone;
        }

        private static LevelElementData BuildGeneratorElement(
            LevelElementData original, Vector2Int position, Dictionary<Vector2Int, GeneratorBehavior> generatorsByPosition)
        {
            var clone = (GeneratorLevelElementData)original.Clone();
            if (generatorsByPosition.TryGetValue(position, out GeneratorBehavior generator))
            {
                clone.GeneratorQueue.Clear();
                clone.GeneratorQueue.AddRange(generator.GetRemainingQueue());
            }
            return clone;
        }

        /// <summary>
        /// Extra layer elements only matter while still pending in layer 2. Once the handler releases them
        /// (e.g. a lift opens), the blocks migrate into <see cref="LevelRepresentation.ActiveBlocks"/> and are
        /// captured as regular blocks above — and the handler destroys itself. So keep the extra layer ONLY
        /// while the handler is alive AND has not spawned; a null/destroyed handler (released, or Init failed)
        /// means it must be disabled, otherwise replaying the snapshot would duplicate those blocks.
        /// </summary>
        private static void ApplyExtraLayerState(LevelRepresentation level, LevelData snapshot)
        {
            if (!snapshot.HasExtraLayer)
                return;

            // Unity's implicit bool: true only when the handler is alive (non-null and not destroyed).
            ExtraLayerHandlerBehavior handler = level.EnvironmentSpawner.ExtraLayerHandler;
            bool blocksStillPending = handler && !handler.HasSpawned;
            if (!blocksStillPending)
                snapshot.SetExtraLayerEnabled(false);
        }
    }
}
