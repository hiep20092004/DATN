using System;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Abstract base for polymorphic gate effect data. Use [SerializeReference] when declaring arrays of this type.
    /// </summary>
    [Serializable]
    public abstract class GateEffectData
    {
        public abstract GateEffectType Type { get; }

        public virtual string GetEditorLabel() => Type.ToString();

        /// <summary>Deep-clone this effect instance using JSON round-trip.</summary>
        public virtual GateEffectData Clone()
        {
            string json = UnityEngine.JsonUtility.ToJson(this);
            return (GateEffectData)UnityEngine.JsonUtility.FromJson(json, GetType());
        }

        /// <summary>Factory: create a default instance for the given gate effect type.</summary>
        public static GateEffectData CreateForType(GateEffectType type)
        {
            switch (type)
            {
                case GateEffectType.IceGate:      return new IceGateEffectData();
                case GateEffectType.Valve:        return new ValveGateEffectData();
                case GateEffectType.LockedColor:  return new LockedColorGateEffectData();
                case GateEffectType.MovingLock:   return new MovingLockGateEffectData();
                case GateEffectType.ChainGate:   return new ChainGateEffectData();
                default:
                    UnityEngine.Debug.LogWarning($"[GateEffectData] Unknown GateEffectType '{type}'.");
                    return null;
            }
        }
    }

    [Serializable]
    public sealed class IceGateEffectData : GateEffectData
    {
        [SerializeField] public int iceTurnsAmount;

        public override GateEffectType Type => GateEffectType.IceGate;
        public override string GetEditorLabel() => "Ice " + iceTurnsAmount;
    }

    [Serializable]
    public sealed class ValveGateEffectData : GateEffectData
    {
        [SerializeField] public bool isValveOpened;

        public override GateEffectType Type => GateEffectType.Valve;
        public override string GetEditorLabel() => "Valve: " + (isValveOpened ? "O" : "X");
    }

    [Serializable]
    public sealed class LockedColorGateEffectData : GateEffectData
    {
        [SerializeField] public BlockColor lockColor;

        public override GateEffectType Type => GateEffectType.LockedColor;
        public override string GetEditorLabel() => "Lock: " + lockColor;
    }

    [Serializable]
    public sealed class MovingLockGateEffectData : GateEffectData
    {
        [SerializeField] public bool isClockwise = true;

        public override GateEffectType Type => GateEffectType.MovingLock;
        public override string GetEditorLabel() => "LockMove: " + (isClockwise ? "\u21bb" : "\u21ba");
    }
    
    [Serializable]
    public sealed class ChainGateEffectData : GateEffectData
    {
        [SerializeField] public int keysAmount;

        public override GateEffectType Type => GateEffectType.ChainGate;
        public override string GetEditorLabel() => "chain " + keysAmount;
    }
}
