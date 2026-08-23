using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Freeze Timer PowerUp Config", menuName = "Data/PowerUp/Freeze Timer")]
    public class FreezeTimerPowerUpConfig : BasePowerUpConfig
    {
        [LineSpacer("Timer")]
        [SerializeField] float timeFreezeDuration = 10.0f;
        
        public float TimeFreezeDuration => timeFreezeDuration;

        protected override void OnInit()
        {
            
        }
    }
}