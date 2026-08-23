using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Expand PowerUp Config", menuName = "Data/PowerUp/Expand")]
    public class ExpandPowerUpConfig: BasePowerUpConfig
    {
        [LineSpacer("Specific")] 
        [SerializeField] GameObject targetVisual;
        
        [SerializeField] private float alphaOnSelect = 0.6f;
        [SerializeField] private float maskFadeDuration = 0.2f;
        [SerializeField] private float drillSpawnOffSet = 2.5f;
        [SerializeField] private float delayExpandBorder = 1.03f;
        [SerializeField] private float animationDuration = 1.8f;
        
        [Space]
        [SerializeField] private float shakeDuration = 0.2f;
        [SerializeField] private float shakeStrength = 0.2f;
        
        public GameObject TargetVisual => targetVisual;
        public float AlphaOnSelect => alphaOnSelect;
        public float MaskFadeDuration => maskFadeDuration;
        public float DrillSpawnOffSet => drillSpawnOffSet;
        public float DelayExpandBorder => delayExpandBorder;
        public float AnimationDuration => animationDuration;
        public float ShakeDuration => shakeDuration;
        public float ShakeStrength => shakeStrength;
        
        protected override void OnInit()
        {
            
        }
    }
}