using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// One filterable mechanic present in a level (e.g. a block effect, a gate effect, a
    /// generator, or an extra layer). Each descriptor owns a stable bit in the <see cref="LevelTagRegistry"/>
    /// mask so AND-matching across the whole level list is a single bitwise op.
    /// </summary>
    public readonly struct LevelTagDescriptor
    {
        public readonly int BitIndex;
        public readonly ObstacleCategory Category;
        public readonly string EffectKey;
        public readonly string DisplayName;

        public ulong Bit => 1UL << BitIndex;

        public LevelTagDescriptor(int bitIndex, ObstacleCategory category, string effectKey, string displayName)
        {
            BitIndex = bitIndex;
            Category = category;
            EffectKey = effectKey;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Editor-only vocabulary of level tags, generated automatically from the effect/obstacle enums
    /// so it never drifts when a new effect is added. Each tag is assigned a stable bit index used by
    /// <see cref="LevelTagScanner"/>; the canonical key matches <see cref="ObstacleUnlockScanner.ComputeEffectKey"/>
    /// so the same level scan that drives obstacle unlocks also drives filtering.
    /// </summary>
    public static class LevelTagRegistry
    {
        // Bit indices are packed into a single ulong; the current vocabulary is ~30 entries.
        // Guarded at build time so a future enum growth past 64 fails loudly instead of silently dropping tags.
        private const int MaxBits = 64;

        private static List<LevelTagDescriptor> all;
        private static Dictionary<string, int> keyToBit;

        public static IReadOnlyList<LevelTagDescriptor> All
        {
            get
            {
                EnsureBuilt();
                return all;
            }
        }

        public static bool TryGetBit(string effectKey, out int bitIndex)
        {
            EnsureBuilt();
            return keyToBit.TryGetValue(effectKey, out bitIndex);
        }

        private static void EnsureBuilt()
        {
            if (all != null)
                return;

            all = new List<LevelTagDescriptor>();
            keyToBit = new Dictionary<string, int>();

            // Order defines bit assignment. Categories are appended in ObstacleCategory order; enums are
            // append-only, so bit indices stay stable across versions.
            foreach (BlockEffectType effect in (BlockEffectType[])System.Enum.GetValues(typeof(BlockEffectType)))
            {
                if (effect == BlockEffectType.None) continue;
                Register(ObstacleCategory.Block,
                    ObstacleUnlockScanner.ComputeEffectKey(ObstacleCategory.Block, effect, GateEffectType.None,
                        InteractableObjectType.None, default),
                    GetBlockDisplayName(effect));
            }

            foreach (GateEffectType effect in (GateEffectType[])System.Enum.GetValues(typeof(GateEffectType)))
            {
                if (effect == GateEffectType.None) continue;
                Register(ObstacleCategory.Gate,
                    ObstacleUnlockScanner.ComputeEffectKey(ObstacleCategory.Gate, BlockEffectType.None, effect,
                        InteractableObjectType.None, default),
                    GetGateDisplayName(effect));
            }

            foreach (InteractableObjectType type in (InteractableObjectType[])System.Enum.GetValues(
                         typeof(InteractableObjectType)))
            {
                if (type == InteractableObjectType.None) continue;
                Register(ObstacleCategory.InteractableObject,
                    ObstacleUnlockScanner.ComputeEffectKey(ObstacleCategory.InteractableObject, BlockEffectType.None,
                        GateEffectType.None, type, default),
                    GetInteractableDisplayName(type));
            }

            Register(ObstacleCategory.Generator,
                ObstacleUnlockScanner.ComputeEffectKey(ObstacleCategory.Generator, BlockEffectType.None,
                    GateEffectType.None, InteractableObjectType.None, default),
                "Printer");

            foreach (ExtraLayerType type in (ExtraLayerType[])System.Enum.GetValues(typeof(ExtraLayerType)))
            {
                Register(ObstacleCategory.ExtraLayer,
                    ObstacleUnlockScanner.ComputeEffectKey(ObstacleCategory.ExtraLayer, BlockEffectType.None,
                        GateEffectType.None, InteractableObjectType.None, type),
                    type.ToString());
            }
        }

        private static void Register(ObstacleCategory category, string effectKey, string displayName)
        {
            if (keyToBit.ContainsKey(effectKey))
                return;

            int bitIndex = all.Count;
            if (bitIndex >= MaxBits)
            {
                Debug.LogError(
                    $"[LevelTagRegistry] Tag vocabulary exceeded {MaxBits} entries; tag '{effectKey}' is ignored. " +
                    "Switch the mask from ulong to a wider representation.");
                return;
            }

            keyToBit[effectKey] = bitIndex;
            all.Add(new LevelTagDescriptor(bitIndex, category, effectKey, displayName));
        }

        // Designer-facing names matching the level editor's tag panel. Falls back to the enum name so a
        // newly added effect still shows up (just with its raw name) without a code change.
        private static string GetBlockDisplayName(BlockEffectType effect)
        {
            switch (effect)
            {
                case BlockEffectType.FixedDirection: return "Vector";
                case BlockEffectType.Layered: return "Stack";
                case BlockEffectType.Blocked: return "Blocker";
                case BlockEffectType.KeyChain: return "Keychain";
                case BlockEffectType.KeyColor: return "Key Color";
                case BlockEffectType.Tnt: return "TNT";
                case BlockEffectType.TimeCapsule: return "Time Capsule";
                case BlockEffectType.SwitchLayer: return "Switch Layer";
                case BlockEffectType.BreakableLink: return "Linked";
                default: return effect.ToString();
            }
        }

        private static string GetGateDisplayName(GateEffectType effect)
        {
            switch (effect)
            {
                case GateEffectType.IceGate: return "Ice Gate";
                case GateEffectType.LockedColor: return "Locked Color";
                case GateEffectType.ChainGate: return "Chained Gate";
                default: return effect.ToString();
            }
        }

        private static string GetInteractableDisplayName(InteractableObjectType type) => type.ToString();
    }

    /// <summary>
    /// Computes and caches a per-level tag bitmask. The expensive part — walking a level's elements — runs
    /// once per level and is cached by asset instance id; matching is then a single <c>(levelMask &amp; selected) == selected</c>
    /// across the whole list. Scans intentionally bypass <see cref="ObstacleUnlockConfig"/> ignore-lists
    /// (those exist for unlock popups) so the filter reflects the level's actual content.
    /// </summary>
    public static class LevelTagScanner
    {
        private static readonly Dictionary<int, ulong> maskCache = new Dictionary<int, ulong>();

        public static ulong GetMask(LevelData level)
        {
            if (level == null)
                return 0UL;

            int id = level.GetInstanceID();
            if (maskCache.TryGetValue(id, out ulong cached))
                return cached;

            ulong mask = ComputeMask(level);
            maskCache[id] = mask;
            return mask;
        }

        public static ulong ComputeMask(LevelData level)
        {
            if (level == null)
                return 0UL;

            ulong mask = 0UL;
            ObstacleUnlockScanner.VisitEffects(level, (category, blockEffect, gateEffect, interactable, extraLayer) =>
            {
                string key = ObstacleUnlockScanner.ComputeEffectKey(category, blockEffect, gateEffect, interactable,
                    extraLayer);
                if (LevelTagRegistry.TryGetBit(key, out int bitIndex))
                    mask |= 1UL << bitIndex;
            }, applyIgnoreList: false);

            return mask;
        }

        /// <summary><c>true</c> when <paramref name="levelMask"/> contains every selected tag (AND match).</summary>
        public static bool Matches(ulong levelMask, ulong selectedMask)
        {
            return (levelMask & selectedMask) == selectedMask;
        }

        public static void Invalidate(LevelData level)
        {
            if (level != null)
                maskCache.Remove(level.GetInstanceID());
        }

        public static void Clear()
        {
            maskCache.Clear();
        }
    }
}
