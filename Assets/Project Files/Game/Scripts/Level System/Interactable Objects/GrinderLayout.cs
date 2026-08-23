using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Grinder geometry decoded from <see cref="InteractableObjectData.GrinderConfig"/>, packed as
    /// (axis, positiveCells, negativeCells) with axis 0 = horizontal and 1 = vertical.
    ///
    /// Only the machine cell is authored in <see cref="LevelData"/>; tape cells are derived from this
    /// value, so nothing has to keep a group of painted cells consistent. Shared by the runtime
    /// behavior, the level editor grid preview and the editor-side GrinderShapeRule validator.
    /// </summary>
    public readonly struct GrinderLayout
    {
        public const int AXIS_HORIZONTAL = 0;
        public const int AXIS_VERTICAL = 1;

        private static readonly Vector2Int HORIZONTAL_STEP = new Vector2Int(1, 0);
        private static readonly Vector2Int VERTICAL_STEP = new Vector2Int(0, 1);

        private GrinderLayout(bool isVertical, int positiveLength, int negativeLength)
        {
            IsVertical = isVertical;
            PositiveLength = positiveLength;
            NegativeLength = negativeLength;
        }

        public static GrinderLayout From(Vector3Int config) => new GrinderLayout(
            config.x == AXIS_VERTICAL,
            Mathf.Max(0, config.y),
            Mathf.Max(0, config.z));

        public bool IsVertical { get; }

        /// <summary>Tape cell count along +<see cref="Axis"/>.</summary>
        public int PositiveLength { get; }

        /// <summary>Tape cell count along -<see cref="Axis"/>.</summary>
        public int NegativeLength { get; }

        /// <summary>(1,0) for a horizontal grinder, (0,1) for a vertical one.</summary>
        public Vector2Int Axis => IsVertical ? VERTICAL_STEP : HORIZONTAL_STEP;

        public int TapeCellCount => PositiveLength + NegativeLength;

        /// <summary>Block clears needed to consume the tape: each clear retracts one column per side.</summary>
        public int RetractStepCount => Mathf.Max(PositiveLength, NegativeLength);

        public bool HasTape => TapeCellCount > 0;

        /// <summary><paramref name="index"/> is 0-based from the machine outwards.</summary>
        public Vector2Int GetTapeCell(Vector2Int corePosition, bool positiveSide, int index)
        {
            Vector2Int offset = Axis * (index + 1);
            return positiveSide ? corePosition + offset : corePosition - offset;
        }

        /// <summary>Appends every tape cell, positive side first, each side ordered from the machine outwards.</summary>
        public void AppendTapeCells(Vector2Int corePosition, List<Vector2Int> results)
        {
            for (int i = 0; i < PositiveLength; i++)
                results.Add(GetTapeCell(corePosition, positiveSide: true, i));

            for (int i = 0; i < NegativeLength; i++)
                results.Add(GetTapeCell(corePosition, positiveSide: false, i));
        }
    }
}
