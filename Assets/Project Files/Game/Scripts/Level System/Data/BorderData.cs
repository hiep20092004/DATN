using UnityEngine;

namespace WaterFlow.Game
{
    public class BorderData
    {
        public ElementType Type { get; private set; }
        public LevelElementData LevelElementData { get; private set; }
        public Vector2Int Position { get; private set; }
        public bool IsExtendable { get; private set; }
        public Vector3 Rotation { get; private set; }

        public GateDirection.Type GateDirectionType { get; private set; }

        public BorderData(LevelElementData levelElementData)
        {
            LevelElementData = levelElementData;

            Type = levelElementData.Type;
            Position = levelElementData.Position;
            IsExtendable = levelElementData is BorderLevelElementData border && border.IsExtendable;
        }

        public void SetGateDirection(GateDirection.Type gateDirection)
        {
            GateDirectionType = gateDirection;
            Rotation = CalculateGateRotation(gateDirection);
        }
        private Vector3 CalculateGateRotation(GateDirection.Type gateDirection)
        {
            return gateDirection is GateDirection.Type.Top or GateDirection.Type.Bottom ? new Vector3(0f, 90f, 0f) : Vector3.zero;
        }
        
        public void InitRotation(Vector3 rotation)
        {
            Rotation = rotation;
        }

        public bool CanSpawnWall()
        {
            return Type is ElementType.Border or ElementType.Generator;
        }
        
    }
}
