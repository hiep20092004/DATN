using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Authoring-time pairwise compatibility rules for combining multiple <see cref="BlockEffectData"/>
    /// on a single block. Symmetric grid indexed directly by <c>(int)BlockEffectType</c>:
    /// <c>cells[a * stride + b]</c> == true means effects a and b may coexist on the same block.
    ///
    /// Defaults to permissive: an unconfigured matrix (stride 0) reports every pair compatible, so the
    /// feature is opt-in and never breaks existing levels until a designer authors the matrix on
    /// <see cref="LevelDatabase"/>. Consumed only by editor tooling (effect picker + LevelValidator);
    /// nothing reads it at runtime, but it serializes with the database asset.
    /// </summary>
    [Serializable]
    public sealed class BlockEffectCompatibilityMatrix
    {
        [SerializeField] private bool[] cells = Array.Empty<bool>();
        [SerializeField] private int stride;

        public int Stride => stride;
        public bool IsConfigured => stride > 0 && cells != null && cells.Length >= stride * stride;

        /// <summary>True when effects <paramref name="a"/> and <paramref name="b"/> may share one block.</summary>
        public bool AreCompatible(BlockEffectType a, BlockEffectType b)
        {
            if (a == BlockEffectType.None || b == BlockEffectType.None)
                return true;

            int ai = (int)a;
            int bi = (int)b;
            if (stride <= 0 || cells == null || ai >= stride || bi >= stride)
                return true; // not configured for these indices -> permissive

            int idx = ai * stride + bi;
            if (idx < 0 || idx >= cells.Length)
                return true;

            return cells[idx];
        }

        /// <summary>True when <paramref name="candidate"/> is compatible with every effect already present.</summary>
        public bool CanAddToBlock(BlockEffectType candidate, IReadOnlyList<BlockEffectType> existing)
        {
            if (existing == null)
                return true;

            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] == candidate)
                    continue;
                if (!AreCompatible(candidate, existing[i]))
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR
        public bool Editor_GetCompatible(int a, int b)
        {
            if (stride <= 0 || cells == null)
                return true;
            int idx = a * stride + b;
            return idx >= 0 && idx < cells.Length && cells[idx];
        }

        /// <summary>Sets the pair symmetrically; returns true if a value changed.</summary>
        public bool Editor_SetCompatible(int a, int b, bool value)
        {
            if (stride <= 0 || cells == null)
                return false;

            bool changed = false;
            int ab = a * stride + b;
            int ba = b * stride + a;
            if (ab >= 0 && ab < cells.Length && cells[ab] != value) { cells[ab] = value; changed = true; }
            if (ba >= 0 && ba < cells.Length && cells[ba] != value) { cells[ba] = value; changed = true; }
            return changed;
        }

        /// <summary>
        /// Resizes the grid to <paramref name="count"/> (number of <see cref="BlockEffectType"/> values),
        /// preserving overlapping pairs and defaulting any newly added pair to compatible. Returns true if
        /// the layout changed (caller should mark the owning asset dirty).
        /// </summary>
        public bool Editor_EnsureSize(int count)
        {
            if (count < 0)
                count = 0;

            if (stride == count && cells != null && cells.Length == count * count)
                return false;

            bool[] old = cells;
            int oldStride = stride;
            bool[] rebuilt = new bool[count * count];

            for (int a = 0; a < count; a++)
            {
                for (int b = 0; b < count; b++)
                {
                    bool value = true; // default: combinable until designer forbids it
                    if (old != null && a < oldStride && b < oldStride)
                    {
                        int oldIdx = a * oldStride + b;
                        if (oldIdx >= 0 && oldIdx < old.Length)
                            value = old[oldIdx];
                    }
                    rebuilt[a * count + b] = value;
                }
            }

            cells = rebuilt;
            stride = count;
            return true;
        }
#endif
    }
}
