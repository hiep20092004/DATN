using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class InteractableObjectData
    {
        /// <summary>Type the level editor sidebar starts on when painting InteractableObject cells.</summary>
        public const InteractableObjectType EditorPaintedType = InteractableObjectType.ColorObstacle;

#if UNITY_EDITOR
        // Mapping from type to the serialized field names we should draw
        public static readonly Dictionary<InteractableObjectType, string[]> FIELDS = new Dictionary<InteractableObjectType, string[]>
        {
            { InteractableObjectType.ColorObstacle, new[] { "obstacleColor" } },
            { InteractableObjectType.MoveableColorObstacle, new[] { "obstacleColor" } },
            { InteractableObjectType.Grinder, new[] { "grinderConfig" } },
        };

        public static readonly InteractableObjectType[] EDITOR_PAINTABLE_TYPES =
        {
            InteractableObjectType.ColorObstacle,
            InteractableObjectType.MoveableColorObstacle,
            InteractableObjectType.Grinder,
        };

        public static bool UsesObstacleColor(InteractableObjectType type) =>
            FIELDS.TryGetValue(type, out string[] fieldNames)
            && Array.IndexOf(fieldNames, nameof(obstacleColor)) >= 0;
#endif

        [SerializeField] InteractableObjectType type;
        public InteractableObjectType Type => type;

        [SerializeField] BlockColor obstacleColor;
        public BlockColor ObstacleColor => obstacleColor;

        [SerializeField] Vector3Int grinderConfig;
        public Vector3Int GrinderConfig => grinderConfig;

        public void SetType(InteractableObjectType value) => type = value;
        public void SetObstacleColor(BlockColor value) => obstacleColor = value;
        public void SetGrinderConfig(Vector3Int value) => grinderConfig = value;
    }
}