using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "Data/Level/Level Data", fileName = "Level Data")]
    public class LevelData : ScriptableObject
    {
        [SerializeField, LevelEditorSetting] Vector2Int size = new Vector2Int(8, 8);
        [SerializeField] float duration = 115;
        [SerializeField] LevelType type;
        [SerializeReference, LevelEditorSetting] LevelElementData[] elements;
        [SerializeField] bool useInRandomizer = true;
        [SerializeField] float randomDuration;

        [SerializeField, LevelEditorSetting] bool hasExtraLayer;
        [SerializeField, LevelEditorSetting] ExtraLayerType extraLayerType;
        [SerializeReference, LevelEditorSetting] LevelElementData[] extraLayerElements;

        public Vector2Int Size => size;
        public float Duration => duration;
        public LevelType Type => type;
        public LevelElementData[] Elements
        {
            get => elements;
            set => elements = value;
        }
        public bool UseInRandomizer => useInRandomizer;
        public bool HasRandomDuration => randomDuration > 0f;
        public float RandomDuration => randomDuration > 0f ? randomDuration : duration;

        public bool HasExtraLayer => hasExtraLayer;
        public ExtraLayerType ExtraLayerType => extraLayerType;
        public LevelElementData[] ExtraLayerElements
        {
            get => extraLayerElements;
            set => extraLayerElements = value;
        }

        public void SetExtraLayerEnabled(bool enabled)
        {
            hasExtraLayer = enabled;

            if (enabled)
                EnsureExtraLayerElements();
        }

        public void InitExtraLayer()
        {
            SetExtraLayerEnabled(true);
        }

        public void RemoveExtraLayer()
        {
            SetExtraLayerEnabled(false);
        }

        public void SetExtraLayerType(ExtraLayerType type)
        {
            extraLayerType = type;
        }

        private void EnsureExtraLayerElements()
        {
            int expectedLength = size.x * size.y;
            if (expectedLength <= 0)
            {
                extraLayerElements = null;
                return;
            }

            bool hasExpectedLayout = extraLayerElements != null && extraLayerElements.Length == expectedLength;
            if (hasExpectedLayout)
            {
                var occupiedPositions = new HashSet<Vector2Int>();
                for (int i = 0; i < extraLayerElements.Length; i++)
                {
                    LevelElementData element = extraLayerElements[i];
                    if (element == null || element.Position.x < 0 || element.Position.y < 0 ||
                        element.Position.x >= size.x || element.Position.y >= size.y ||
                        !occupiedPositions.Add(element.Position))
                    {
                        hasExpectedLayout = false;
                        break;
                    }
                }

                if (hasExpectedLayout)
                    return;
            }

            var existingElements = new Dictionary<Vector2Int, LevelElementData>();
            if (extraLayerElements != null)
            {
                for (int i = 0; i < extraLayerElements.Length; i++)
                {
                    LevelElementData element = extraLayerElements[i];
                    if (element == null)
                        continue;

                    existingElements[element.Position] = element;
                }
            }

            extraLayerElements = new LevelElementData[expectedLength];
            int index = 0;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    if (!existingElements.TryGetValue(position, out LevelElementData element))
                        element = CreateDefaultElement(position);

                    element.SetPosition(position);
                    extraLayerElements[index] = element;
                    index++;
                }
            }
        }

        private LevelElementData CreateDefaultElement(Vector2Int position)
        {
            bool isBorder = position.x == 0 || position.y == 0 ||
                            position.x == size.x - 1 || position.y == size.y - 1;

            LevelElementData element = isBorder
                ? (LevelElementData)new BorderLevelElementData()
                : new InnerTileLevelElementData();

            element.SetPosition(position);
            return element;
        }


        [Button]
        public void Validate()
        {
            AssignElementIds();
            RuntimeEditorUtils.SetDirty(this);
        }
        
        public void AssignElementIds()
        {
            int blockIdCounter = 1;
            int gateIdCounter = -1;
            AssignElementIds(elements, ref blockIdCounter, ref gateIdCounter);

            if (hasExtraLayer)
                AssignElementIds(extraLayerElements, ref blockIdCounter, ref gateIdCounter);
        }

        private void AssignElementIds(LevelElementData[] els, ref int blockIdCounter, ref int gateIdCounter)
        {
            if (els == null || els.Length == 0) return;

            for (int i = 0; i < els.Length; i++)
            {
                LevelElementData element = els[i];
                if (element == null) continue;

                if (element is BlockLevelElementData block)
                {
                    if (block.HasEffect(BlockEffectType.Blocked))
                        element.AssignBlockId(0);
                    else
                    {
                        element.AssignBlockId(blockIdCounter);
                        blockIdCounter++;
                    }
                }
                else if (element.Type == ElementType.Gate)
                {
                    element.AssignBlockId(gateIdCounter);
                    gateIdCounter--;
                }
                else if (element is GeneratorLevelElementData gen)
                {
                    List<GeneratorBlockEntry> generatorQueue = gen.GeneratorQueue;
                    if (generatorQueue != null)
                    {
                        for (int q = 0; q < generatorQueue.Count; q++)
                        {
                            GeneratorBlockEntry entry = generatorQueue[q];
                            if (entry == null) continue;
                            entry.AssignBlockId(blockIdCounter);
                            blockIdCounter++;
                        }
                    }
                }
                else
                {
                    element.AssignBlockId(0);
                }
            }
        }

        private int GetElementIndex(Vector2Int position)
        {
            if (elements == null) return -1;
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] != null && elements[i].Position.Equals(position))
                    return i;
            }
            return -1;
        }

        public int GetTotalBlockEatable()
        {
            int count = 0;
            LevelElementData[] els = elements;
            if (els == null) return 0;

            foreach (LevelElementData element in els)
            {
                if (element == null) continue;
                if (element.BlockId > 0) count++;

                if (element is GeneratorLevelElementData gen && gen.GeneratorQueue != null)
                {
                    foreach (GeneratorBlockEntry entry in gen.GeneratorQueue)
                    {
                        if (entry != null && entry.BlockId > 0) count++;
                    }
                }
            }

            return count;
        }
    }
}
