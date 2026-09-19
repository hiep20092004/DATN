using System.Collections.Generic;

namespace WaterFlow.Game
{
    /// <summary>
    /// Single source of truth for scanning a <see cref="LevelData"/> and reporting every
    /// obstacle effect it contains (Block / Gate / Interactable / Generator + generator queue,
    /// plus the ExtraLayer type and any obstacle effects nested inside the extra layer).
    /// Both runtime popup lookup and editor unlock-data generation go through here so the
    /// iteration rules and ignore-lists stay in one place.
    /// </summary>
    public static class ObstacleUnlockScanner
    {
        private const ExtraLayerType DefaultExtraLayer = default;

        public delegate void EffectVisitor(
            ObstacleCategory category,
            BlockEffectType blockEffect,
            GateEffectType gateEffect,
            InteractableObjectType interactable,
            ExtraLayerType extraLayer);

        /// <summary>
        /// Invokes <paramref name="visitor"/> once for every obstacle effect found in
        /// <paramref name="levelData"/>. When <paramref name="applyIgnoreList"/> is true (default,
        /// used by unlock popups/data) entries listed in <see cref="ObstacleUnlockConfig"/> are filtered out;
        /// pass false to report the level's raw content (used by the editor tag filter). The same effect may
        /// be visited multiple times if it appears across elements; deduplication is the caller's choice.
        /// </summary>
        public static void VisitEffects(LevelData levelData, EffectVisitor visitor, bool applyIgnoreList = true)
        {
            if (visitor == null) return;
            if (levelData == null) return;

            VisitElements(levelData.Elements, visitor, applyIgnoreList);

            if (levelData.HasExtraLayer)
            {
                if (!applyIgnoreList || !ObstacleUnlockConfig.IgnoredExtraLayerTypes.Contains(levelData.ExtraLayerType))
                {
                    visitor(ObstacleCategory.ExtraLayer, BlockEffectType.None, GateEffectType.None,
                        InteractableObjectType.None, levelData.ExtraLayerType);
                }

                VisitElements(levelData.ExtraLayerElements, visitor, applyIgnoreList);
            }
        }

        private static void VisitElements(IEnumerable<LevelElementData> elements, EffectVisitor visitor,
            bool applyIgnoreList)
        {
            if (elements == null) return;

            foreach (LevelElementData element in elements)
            {
                if (element == null) continue;

                VisitBlockEffects(element, visitor, applyIgnoreList);
                VisitGateEffects(element, visitor, applyIgnoreList);
                VisitInteractable(element, visitor, applyIgnoreList);
                VisitGenerator(element, visitor, applyIgnoreList);
            }
        }

        /// <summary>
        /// Convenience wrapper: collects canonical effect keys for every obstacle in
        /// <paramref name="levelData"/>.
        /// </summary>
        public static void CollectEffectKeys(LevelData levelData, HashSet<string> keys)
        {
            if (keys == null) return;
            VisitEffects(levelData, (cat, b, g, i, e) => keys.Add(ComputeEffectKey(cat, b, g, i, e)));
        }

        /// <summary>
        /// Canonical effect key for any (category, effect-fields) tuple. Static so scanners can derive
        /// the key without allocating. Consumed by the level editor tag filter.
        /// </summary>
        public static string ComputeEffectKey(
            ObstacleCategory category,
            BlockEffectType blockEffectType,
            GateEffectType gateEffectType,
            InteractableObjectType interactableObjectType,
            ExtraLayerType extraLayerType)
        {
            return category switch
            {
                ObstacleCategory.Block => $"Block_{blockEffectType}",
                ObstacleCategory.Gate => $"Gate_{gateEffectType}",
                ObstacleCategory.InteractableObject => $"Interactable_{interactableObjectType}",
                ObstacleCategory.Generator => "Generator",
                ObstacleCategory.ExtraLayer => $"ExtraLayer_{extraLayerType}",
                _ => category.ToString()
            };
        }

        private static void VisitBlockEffects(LevelElementData element, EffectVisitor visitor, bool applyIgnoreList)
        {
            if (!(element is BlockLevelElementData block) || block.BlockEffects == null) return;

            foreach (BlockEffectData effect in block.BlockEffects)
            {
                if (effect == null) continue;
                if (effect.Type == BlockEffectType.None) continue;
                if (applyIgnoreList && ObstacleUnlockConfig.IgnoredBlockEffects.Contains(effect.Type)) continue;
                visitor(ObstacleCategory.Block, effect.Type, GateEffectType.None, InteractableObjectType.None,
                    DefaultExtraLayer);
            }
        }

        private static void VisitGateEffects(LevelElementData element, EffectVisitor visitor, bool applyIgnoreList)
        {
            if (!(element is GateLevelElementData gate) || gate.GateEffects == null) return;

            foreach (GateEffectData effect in gate.GateEffects)
            {
                if (effect == null || effect.Type == GateEffectType.None) continue;
                if (applyIgnoreList && ObstacleUnlockConfig.IgnoredGateEffects.Contains(effect.Type)) continue;
                visitor(ObstacleCategory.Gate, BlockEffectType.None, effect.Type, InteractableObjectType.None,
                    DefaultExtraLayer);
            }
        }

        private static void VisitInteractable(LevelElementData element, EffectVisitor visitor, bool applyIgnoreList)
        {
            if (!(element is InteractableObjectLevelElementData io)) return;
            if (io.InteractableObjectData == null) return;

            InteractableObjectType type = io.InteractableObjectData.Type;
            if (type == InteractableObjectType.None) return;
            if (applyIgnoreList && ObstacleUnlockConfig.IgnoredInteractableObjects.Contains(type)) return;

            visitor(ObstacleCategory.InteractableObject, BlockEffectType.None, GateEffectType.None, type,
                DefaultExtraLayer);
        }

        /// <summary>
        /// A Generator level element contributes both the Generator unlock and one
        /// Block_* unlock per unique <see cref="BlockEffectType"/> in its queue.
        /// </summary>
        private static void VisitGenerator(LevelElementData element, EffectVisitor visitor, bool applyIgnoreList)
        {
            if (!(element is GeneratorLevelElementData generator)) return;

            visitor(ObstacleCategory.Generator, BlockEffectType.None, GateEffectType.None, InteractableObjectType.None,
                DefaultExtraLayer);

            if (generator.GeneratorQueue == null) return;

            foreach (GeneratorBlockEntry queueEntry in generator.GeneratorQueue)
            {
                if (queueEntry?.BlockEffects == null) continue;

                foreach (BlockEffectData effect in queueEntry.BlockEffects)
                {
                    if (effect == null) continue;
                    if (effect.Type == BlockEffectType.None) continue;
                    if (applyIgnoreList && ObstacleUnlockConfig.IgnoredBlockEffects.Contains(effect.Type)) continue;
                    visitor(ObstacleCategory.Block, effect.Type, GateEffectType.None, InteractableObjectType.None,
                        DefaultExtraLayer);
                }
            }
        }
    }
}
