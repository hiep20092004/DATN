using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>Every cell inside <c>size</c> must exist exactly once in the elements array. Duplicate, stale or
    /// out-of-range positions make cells unreachable for the editor grid and for runtime spawning.</summary>
    public sealed class GridCoverageRule : ILevelValidationRule
    {
        private const int MAX_REPORTED_CELLS = 5;

        public string Tag => "Grid";

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            if (itemsProperty == null || !itemsProperty.isArray || gridSize.x < 1 || gridSize.y < 1)
                yield break;

            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            List<Vector2Int> duplicates = new List<Vector2Int>();
            List<Vector2Int> outOfRange = new List<Vector2Int>();
            int unreadableElements = 0;

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty positionProperty = itemsProperty.GetArrayElementAtIndex(i)
                    ?.FindPropertyRelative(LevelAssetRepresentation.POSITION_PROPERTY_NAME);
                if (positionProperty == null)
                {
                    unreadableElements++;
                    continue;
                }

                Vector2Int position = positionProperty.vector2IntValue;
                if (!seen.Add(position))
                    duplicates.Add(position);

                if (position.x < 0 || position.y < 0 || position.x >= gridSize.x || position.y >= gridSize.y)
                    outOfRange.Add(position);
            }

            List<Vector2Int> missing = new List<Vector2Int>();
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    if (!seen.Contains(position))
                        missing.Add(position);
                }
            }

            if (unreadableElements > 0)
                yield return $"Grid has {unreadableElements} element(s) without readable position data.";

            if (missing.Count > 0)
                yield return $"Grid is missing {missing.Count} cell(s) of size {gridSize}: {Format(missing)}.";

            if (duplicates.Count > 0)
                yield return $"Grid has {duplicates.Count} duplicated cell position(s): {Format(duplicates)}.";

            if (outOfRange.Count > 0)
                yield return $"Grid has {outOfRange.Count} cell(s) outside size {gridSize}: {Format(outOfRange)}.";
        }

        private static string Format(List<Vector2Int> positions)
        {
            int shown = Mathf.Min(positions.Count, MAX_REPORTED_CELLS);
            string joined = string.Join(", ", positions.GetRange(0, shown));
            return positions.Count > shown ? $"{joined}, …" : joined;
        }
    }
}
