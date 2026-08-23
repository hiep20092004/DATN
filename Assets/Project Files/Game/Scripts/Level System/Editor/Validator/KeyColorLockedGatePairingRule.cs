using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Ensures every <see cref="BlockEffectType.KeyColor"/> has enough <see cref="GateEffectType.LockedColor"/>
    /// gates (same <see cref="BlockColor"/> as <c>lockColor</c>), matching runtime pairing used by
    /// <see cref="LockedColorGateEffectBehavior"/> / <see cref="KeyLockDebugVisualizer"/>.
    /// </summary>
    public sealed class KeyColorLockedGatePairingRule : ILevelValidationRule
    {
        public string Tag => "KeyLock";

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            if (itemsProperty == null || !itemsProperty.isArray)
                yield break;

            Dictionary<BlockColor, int> keyCountByColor = new Dictionary<BlockColor, int>();
            Dictionary<BlockColor, int> lockCountByColor = new Dictionary<BlockColor, int>();

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty element = itemsProperty.GetArrayElementAtIndex(i);
                ElementType elementType = LevelAssetRepresentation.GetElementType(element);

                if (elementType == ElementType.Block)
                {
                    AccumulateKeyColorsFromBlockEffects(
                        element.FindPropertyRelative(LevelAssetRepresentation.BLOCK_EFFECTS_PROPERTY_NAME),
                        keyCountByColor);
                }
                else if (elementType == ElementType.Gate)
                {
                    AccumulateLockedColorsFromGateEffects(
                        element.FindPropertyRelative(LevelAssetRepresentation.GATE_EFFECTS_PROPERTY_NAME),
                        lockCountByColor);
                }
                else if (elementType == ElementType.Generator)
                {
                    SerializedProperty queue =
                        element.FindPropertyRelative(LevelAssetRepresentation.GENERATOR_QUEUE_PROPERTY_NAME);
                    if (queue == null || !queue.isArray)
                        continue;

                    for (int q = 0; q < queue.arraySize; q++)
                    {
                        SerializedProperty entry = queue.GetArrayElementAtIndex(q);
                        AccumulateKeyColorsFromBlockEffects(
                            entry.FindPropertyRelative(GeneratorBlockEntry.BlockEffectsPropertyName),
                            keyCountByColor);
                    }
                }
            }

            foreach (KeyValuePair<BlockColor, int> kv in keyCountByColor)
            {
                BlockColor color = kv.Key;
                int keys = kv.Value;
                if (keys <= 0 || color == BlockColor.None)
                    continue;

                lockCountByColor.TryGetValue(color, out int locks);

                if (locks == 0)
                {
                    yield return
                        $"KeyColor effect ({keys}×) for color {color} has no matching LockedColor gate " +
                        $"(LockedColorGateEffect / GateEffectType.LockedColor with lockColor {color}) on any gate.";
                }
                else if (keys > locks)
                {
                    yield return
                        $"KeyColor count ({keys}) exceeds LockedColor gate count ({locks}) for color {color}; " +
                        "each key needs a distinct lock of the same color.";
                }
            }
        }

        private static void AccumulateKeyColorsFromBlockEffects(
            SerializedProperty blockEffectsProperty,
            Dictionary<BlockColor, int> keyCountByColor)
        {
            if (blockEffectsProperty == null || !blockEffectsProperty.isArray)
                return;

            for (int i = 0; i < blockEffectsProperty.arraySize; i++)
            {
                if (blockEffectsProperty.GetArrayElementAtIndex(i).managedReferenceValue is KeyColorBlockEffectData kc)
                {
                    keyCountByColor.TryAdd(kc.keyColor, 0);
                    keyCountByColor[kc.keyColor]++;
                }
            }
        }

        private static void AccumulateLockedColorsFromGateEffects(
            SerializedProperty gateEffectsProperty,
            Dictionary<BlockColor, int> lockCountByColor)
        {
            if (gateEffectsProperty == null || !gateEffectsProperty.isArray)
                return;

            for (int i = 0; i < gateEffectsProperty.arraySize; i++)
            {
                if (gateEffectsProperty.GetArrayElementAtIndex(i).managedReferenceValue is LockedColorGateEffectData lc
                    && lc.lockColor != BlockColor.None)
                {
                    lockCountByColor.TryAdd(lc.lockColor, 0);
                    lockCountByColor[lc.lockColor]++;
                }
            }
        }
    }
}
