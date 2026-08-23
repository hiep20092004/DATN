using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Pump PowerUp Config", menuName = "Data/PowerUp/Pump")]
    public class PumpPowerUpConfig: BasePowerUpConfig
    {
        [LineSpacer("Specific")]
        [SerializeField] BlockEffectType[] typesCanDestroy;
        [SerializeField] private float alphaOnSelect = 0.55f;
        [SerializeField] private float maskFadeDuration = 0.2f;
        

        public BlockEffectType[] TypesCanDestroy => typesCanDestroy;
        public float AlphaOnSelect => alphaOnSelect;
        public float MaskFadeDuration => maskFadeDuration;
        
        protected override void OnInit()
        {
            
        }
    }
}