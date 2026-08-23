using System;
using UnityEngine;

namespace BorderSpawnModule
{
    public enum BorderRuleType
    {
        Fixed = 0,
        Rotated = 1,
        MirrorX = 2,
        MirrorY = 3,
        MirrorXY = 4
    }

    public enum NeighborCondition
    {
        DontCare = 0,
        This = 1,
        NotThis = 2,
        InnerTile = 3,
        Gate = 4,
        Obstacle = 5,
    }

    [Serializable]
    public class BorderRuleEntry
    {
        [SerializeField] string ruleName = "New Rule";
        [SerializeField] BorderRuleType ruleType;
        [SerializeField] NeighborCondition[] neighbors = new NeighborCondition[9];
        [SerializeField] GameObject prefab;
        [SerializeField] Vector3 positionOffset;
        [SerializeField] Vector3 rotationOffset;

        public string RuleName => ruleName;
        public BorderRuleType RuleType => ruleType;
        public NeighborCondition[] Neighbors => neighbors;
        public GameObject Prefab => prefab;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffset => rotationOffset;
    }

    public struct ResolvedBorderRule
    {
        public string RuleName;
        public GameObject Prefab;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Vector3 PositionOffset;
    }
}
