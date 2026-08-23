using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Hammer PowerUp Config", menuName = "Data/PowerUp/Hammer")]
    public class HammerPowerUpConfig : BasePowerUpConfig
    {
        [LineSpacer("Specific")] 
        [SerializeField] GameObject hammerTargetVisual;
        [SerializeField] BlockEffectType[] typesCanDestroy;
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeStrength = 0.5f;
        
        public GameObject HammerTargetVisual => hammerTargetVisual;
        public BlockEffectType[] TypesCanDestroy => typesCanDestroy;
        public float ShakeDuration => shakeDuration;
        public float ShakeStrength => shakeStrength;
        
        protected override void OnInit()
        {
            
        }
    }
}