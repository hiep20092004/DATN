using System;
using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Abstract base for polymorphic level element data. Use [SerializeReference] when declaring arrays of this type.
    /// </summary>
    [Serializable]
    public abstract class LevelElementData
    {
        [SerializeField, Hide] Vector2Int position;
        [SerializeField, ReadOnly] int blockId;

        public abstract ElementType Type { get; }
        public Vector2Int Position => position;
        public int BlockId => blockId;

        public void SetPosition(Vector2Int newPosition) => position = newPosition;
        public void AssignBlockId(int id) => blockId = id;

        /// <summary>
        /// Deep-clone this element. Uses EditorJsonUtility in editor builds for full [SerializeReference] support,
        /// and JsonUtility in runtime builds (enough for non-editor paths).
        /// </summary>
        public virtual LevelElementData Clone()
        {
#if UNITY_EDITOR
            string json = UnityEditor.EditorJsonUtility.ToJson(this);
            var copy = (LevelElementData)System.Activator.CreateInstance(GetType());
            UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, copy);
            return copy;
#else
            string json = JsonUtility.ToJson(this);
            return (LevelElementData)JsonUtility.FromJson(json, GetType());
#endif
        }

        /// <summary>Factory: create a default instance for the given element type.</summary>
        public static LevelElementData CreateForType(ElementType type)
        {
            switch (type)
            {
                case ElementType.Empty:             return new EmptyLevelElementData();
                case ElementType.InnerTile:         return new InnerTileLevelElementData();
                case ElementType.Obstacle:          return new ObstacleLevelElementData();
                case ElementType.Border:            return new BorderLevelElementData();
                case ElementType.Block:             return new BlockLevelElementData();
                case ElementType.Gate:              return new GateLevelElementData();
                case ElementType.Generator:         return new GeneratorLevelElementData();
                case ElementType.InteractableObject: return new InteractableObjectLevelElementData();
                default:
                    Debug.LogWarning($"[LevelElementData] Unknown ElementType '{type}'.");
                    return new EmptyLevelElementData();
            }
        }
    }

    [Serializable]
    public sealed class EmptyLevelElementData : LevelElementData
    {
        public override ElementType Type => ElementType.Empty;
    }

    [Serializable]
    public sealed class InnerTileLevelElementData : LevelElementData
    {
        public override ElementType Type => ElementType.InnerTile;
    }

    [Serializable]
    public sealed class ObstacleLevelElementData : LevelElementData
    {
        public override ElementType Type => ElementType.Obstacle;
    }

    [Serializable]
    public sealed class BorderLevelElementData : LevelElementData
    {
        [SerializeField] bool isExtendable;

        public override ElementType Type => ElementType.Border;
        public bool IsExtendable => isExtendable;

        public void SetExtendable(bool extendable) => isExtendable = extendable;
    }

    [Serializable]
    public sealed class BlockLevelElementData : LevelElementData
    {
        [SerializeField] BlockType blockType;
        [SerializeField] BlockColor blockColor;
        [SerializeReference] BlockEffectData[] blockEffects = Array.Empty<BlockEffectData>();

        public override ElementType Type => ElementType.Block;
        public BlockType BlockType => blockType;
        public BlockColor BlockColor => blockColor;
        public BlockEffectData[] BlockEffects => blockEffects;

        public void SetBlockType(BlockType value) => blockType = value;
        public void SetBlockColor(BlockColor value) => blockColor = value;
        public void SetBlockEffects(BlockEffectData[] effects) => blockEffects = effects ?? Array.Empty<BlockEffectData>();

        public bool HasEffect(BlockEffectType effectType)
        {
            if (blockEffects == null) return false;
            for (int i = 0; i < blockEffects.Length; i++)
                if (blockEffects[i] != null && blockEffects[i].Type == effectType) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class GateLevelElementData : LevelElementData
    {
        [SerializeField] List<ColorData> gateData = new List<ColorData>();
        [SerializeReference] GateEffectData[] gateEffects = Array.Empty<GateEffectData>();

        public override ElementType Type => ElementType.Gate;
        public List<ColorData> GateData => gateData;
        public GateEffectData[] GateEffects => gateEffects;

        public void SetGateData(List<ColorData> data) => gateData = data ?? new List<ColorData>();
        public void SetGateEffects(GateEffectData[] effects) => gateEffects = effects ?? Array.Empty<GateEffectData>();
    }

    [Serializable]
    public sealed class GeneratorLevelElementData : LevelElementData
    {
        [SerializeField] List<GeneratorBlockEntry> generatorQueue = new List<GeneratorBlockEntry>();

        public override ElementType Type => ElementType.Generator;
        public List<GeneratorBlockEntry> GeneratorQueue => generatorQueue;
    }

    [Serializable]
    public sealed class InteractableObjectLevelElementData : LevelElementData
    {
        [SerializeField] InteractableObjectData interactableObjectData;

        public override ElementType Type => ElementType.InteractableObject;
        public InteractableObjectData InteractableObjectData => interactableObjectData;

        public void SetInteractableObjectData(InteractableObjectData data) => interactableObjectData = data;
    }
}
