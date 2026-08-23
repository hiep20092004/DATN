using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [Serializable]
    public class LevelGateEffectData
    {
        [SerializeField] GateEffectType type;
        [SerializeField] GateEffectBehavior behavior;

        public GateEffectType Type => type;
        public GateEffectBehavior Behavior => behavior;
    }
}