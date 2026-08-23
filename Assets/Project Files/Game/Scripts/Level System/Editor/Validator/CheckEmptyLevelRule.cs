using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>Level must contain at least one playable Block and one Gate.</summary>
    public sealed class CheckEmptyLevelRule : ILevelValidationRule
    {
        public string Tag => "Empty";

        public IEnumerable<string> Validate(SerializedProperty itemsProperty, Vector2Int gridSize)
        {
            if (itemsProperty == null || !itemsProperty.isArray)
                yield break;

            bool hasBlock = false;
            bool hasGate = false;

            for (int i = 0; i < itemsProperty.arraySize; i++)
            {
                SerializedProperty element = itemsProperty.GetArrayElementAtIndex(i);
                ElementType elementType = LevelAssetRepresentation.GetElementType(element);

                if (elementType == ElementType.Block)
                    hasBlock = true;
                else if (elementType == ElementType.Gate)
                    hasGate = true;

                if (hasBlock && hasGate)
                    yield break;
            }
            if (!hasBlock && !hasGate)
                yield return "Level is Empty!";
            else
            {
                if (!hasBlock)
                    yield return "Level has no playable Block!";
                if (!hasGate)
                    yield return "Level has no Gate!";
            }
        }
    }
}
