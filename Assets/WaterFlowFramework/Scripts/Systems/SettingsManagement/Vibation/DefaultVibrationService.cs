using WaterFlow.Framework.Systems.GameDataManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems.SettingsManagement.Vibation
{
    [CreateAssetMenu(fileName = nameof(DefaultVibrationService), menuName = "WaterFlow/Vibration Service")]
    public class DefaultVibrationService : VibrationService
    {
        private const string VibrationKey = "VibrationKey";
        [SerializeField] private Service<DataService> dataService;


        public override void SetVibrationState(bool state)
        {
            dataService.Instance.SetBool(VibrationKey, state);
        }

        public override bool GetVibrationState()
        {
            return dataService.Instance.GetBool(VibrationKey, true);
        }

        public override void Vibrate(long milliseconds)
        {
        }
    }
}