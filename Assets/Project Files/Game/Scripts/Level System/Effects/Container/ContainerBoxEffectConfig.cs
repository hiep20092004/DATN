using System;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Container Box Effect Config")]
    public sealed class ContainerBoxEffectConfig : BaseContainerBoxEffectConfig
    {
        [Header("Blocked Click Shake")]
        [SerializeField] private float blockedClickShakeDuration = 0.04f;
        [SerializeField] private float blockedClickShakeStrength = 0.15f;
        
        public float BlockedClickShakeDuration => blockedClickShakeDuration;
        public float BlockedClickShakeStrength => blockedClickShakeStrength;
    }

    [Serializable]
    public class ContainerBoxVisualConfig : BaseContainerBoxVisualConfig
    {
        
    }
}
