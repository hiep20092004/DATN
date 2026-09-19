using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Abstract base for polymorphic block effect data. Use [SerializeReference] when declaring arrays of this type.
    /// </summary>
    [Serializable]
    public abstract class BlockEffectData
    {
        public abstract BlockEffectType Type { get; }

        public virtual string GetEditorLabel() => Type.ToString();

        /// <summary>Deep-clone this effect instance using JSON round-trip (supports [SerializeReference]).</summary>
        public virtual BlockEffectData Clone()
        {
            // Concrete type must be JSON-round-trippable via JsonUtility.
            string json = JsonUtility.ToJson(this);
            return (BlockEffectData) JsonUtility.FromJson(json, GetType());
        }

        /// <summary>Factory: create a default instance for the given effect type.</summary>
        public static BlockEffectData CreateForType(BlockEffectType type)
        {
            switch (type)
            {
                case BlockEffectType.Ice:           return new IceBlockEffectData();
                case BlockEffectType.Bomb:          return new BombBlockEffectData();
                case BlockEffectType.Layered:       return new LayeredBlockEffectData();
                case BlockEffectType.SwitchLayer:   return new SwitchLayerBlockEffectData();
                case BlockEffectType.FixedDirection: return new FixedDirectionBlockEffectData();
                case BlockEffectType.Dual:          return new DualBlockEffectData();
                case BlockEffectType.Chain:         return new ChainBlockEffectData();
                case BlockEffectType.KeyChain:      return new KeyChainBlockEffectData();
                case BlockEffectType.KeyColor:      return new KeyColorBlockEffectData();
                case BlockEffectType.Tnt:           return new TntBlockEffectData();
                case BlockEffectType.Blocked:       return new BlockedBlockEffectData();
                case BlockEffectType.TimeCapsule:   return new TimeCapsuleBlockEffectData();
                case BlockEffectType.BreakableLink: return new BreakableLinkBlockEffectData();
                default:
                    UnityEngine.Debug.LogWarning($"[BlockEffectData] Unknown BlockEffectType '{type}'.");
                    return null;
            }
        }
    }

    [Serializable]
    public sealed class IceBlockEffectData : BlockEffectData
    {
        [SerializeField] public int iceTurnsAmount;

        public override BlockEffectType Type => BlockEffectType.Ice;
        public override string GetEditorLabel() => "Ice " + iceTurnsAmount;
    }

    [Serializable]
    public sealed class BombBlockEffectData : BlockEffectData
    {
        [SerializeField] public int bombDuration;

        public override BlockEffectType Type => BlockEffectType.Bomb;
        public override string GetEditorLabel() => "bomb " + bombDuration;
    }

    [Serializable]
    public sealed class LayeredBlockEffectData : BlockEffectData
    {
        [SerializeField] public BlockColor layeredBlockColor;

        public override BlockEffectType Type => BlockEffectType.Layered;
        public override string GetEditorLabel() => "layer:" + layeredBlockColor;
    }

    /// <summary>
    /// Switch-layer variant of <see cref="LayeredBlockEffectData"/>: identical payload (a single layered
    /// color), but its two layers swap on every board clear.
    /// Needs its own type so level data routes it to the switch behavior instead of the plain layer one.
    /// </summary>
    [Serializable]
    public sealed class SwitchLayerBlockEffectData : BlockEffectData
    {
        [SerializeField] public BlockColor layeredBlockColor;

        public override BlockEffectType Type => BlockEffectType.SwitchLayer;
        public override string GetEditorLabel() => "switch:" + layeredBlockColor;
    }

    [Serializable]
    public sealed class FixedDirectionBlockEffectData : BlockEffectData
    {
        [SerializeField] public bool horizontalDirection;

        public override BlockEffectType Type => BlockEffectType.FixedDirection;
        public override string GetEditorLabel() => horizontalDirection ? "\u2194" : "\u2195";
    }

    [Serializable]
    public sealed class DualBlockEffectData : BlockEffectData
    {
        [SerializeField] public BlockColor secondDualColor;

        public override BlockEffectType Type => BlockEffectType.Dual;
        public override string GetEditorLabel() => "dual: " + secondDualColor;
    }

    [Serializable]
    public sealed class ChainBlockEffectData : BlockEffectData
    {
        [SerializeField] public int keysAmount;

        public override BlockEffectType Type => BlockEffectType.Chain;
        public override string GetEditorLabel() => "chain " + keysAmount;
    }

    [Serializable]
    public sealed class KeyChainBlockEffectData : BlockEffectData
    {
        public override BlockEffectType Type => BlockEffectType.KeyChain;
        public override string GetEditorLabel() => "key";
    }

    [Serializable]
    public sealed class KeyColorBlockEffectData : BlockEffectData
    {
        [SerializeField] public BlockColor keyColor;

        public override BlockEffectType Type => BlockEffectType.KeyColor;
        public override string GetEditorLabel() => "key:" + keyColor;
    }

    [Serializable]
    public sealed class TntBlockEffectData : BlockEffectData
    {
        [SerializeField] public int tntTurn;

        public override BlockEffectType Type => BlockEffectType.Tnt;
        public override string GetEditorLabel() => "tnt:" + tntTurn;
    }

    [Serializable]
    public sealed class BlockedBlockEffectData : BlockEffectData
    {
        public override BlockEffectType Type => BlockEffectType.Blocked;
        public override string GetEditorLabel() => "blocker";
    }

    [Serializable]
    public sealed class TimeCapsuleBlockEffectData : BlockEffectData
    {
        [SerializeField] public int timeBonus;

        public override BlockEffectType Type => BlockEffectType.TimeCapsule;
        public override string GetEditorLabel() => "time +" + timeBonus;
    }
    
    /// <summary>
    /// One breakable-link declaration: two adjacent blocks link when BOTH declare an entry with the
    /// same <see cref="linkId"/> AND the same <see cref="count"/>. <see cref="count"/> is the link's
    /// starting clear counter (decremented once per board clear; the link breaks at 0).
    /// </summary>
    [Serializable]
    public sealed class BreakableLinkEntry
    {
        [SerializeField] public int linkId;
        [SerializeField] public int count;
    }

    /// <summary>
    /// Linked blocks move as one rigid group, but each link between two adjacent
    /// blocks has its own counter and breaks when it hits 0. A block with any alive link cannot be
    /// filled at a gate. Duplicate <see cref="BreakableLinkEntry.linkId"/> within one list → first wins.
    /// </summary>
    [Serializable]
    public sealed class BreakableLinkBlockEffectData : BlockEffectData
    {
        [SerializeField] public List<BreakableLinkEntry> links = new();

        public override BlockEffectType Type => BlockEffectType.BreakableLink;
        public override string GetEditorLabel()
            => links == null || links.Count == 0 ? "link:-" : $"link x{links.Count}";
    }
}
