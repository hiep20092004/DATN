using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Gate Effects/Chain Gate Config")]
    public class ChainGateEffectConfig : BaseGateEffectConfig
    {
        [SerializeField] private float timeDelayDestroyVisual = 0.25f;
        [SerializeField] private ChainVisualByGateDirection[] chainVisuals;

        public float TimeDelayDestroyVisual => timeDelayDestroyVisual;

        public ChainVisualsBehavior GetChainVisual(GateDirection.Type gateDirectionType)
        {
            foreach (var visualByGateDirection in chainVisuals)
            {
                if (visualByGateDirection.GateDirectionType == gateDirectionType)
                {
                    return visualByGateDirection.ChainVisualsBehavior;
                }
            }
            
            Debug.LogError($"Chain Gate Visual for Gate Direction {gateDirectionType} not found");
            return null;
        }
    }
    
    [Serializable]
    public class ChainVisualByGateDirection
    {
        public GateDirection.Type GateDirectionType;
        public ChainVisualsBehavior ChainVisualsBehavior;
    }
}