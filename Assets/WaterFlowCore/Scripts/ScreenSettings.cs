using UnityEngine;

namespace WaterFlow.Core
{
    [RegisterModule("Screen Settings", false)]
    public class ScreenSettings : InitModule
    {
        public override string ModuleName => "Screen Settings";

        [Space]
        [SerializeField] AllowedFrameRates defaultFrameRate = AllowedFrameRates.Rate60;
        [SerializeField] AllowedFrameRates batterySaveFrameRate = AllowedFrameRates.Rate30;

        public override void CreateComponent()
        {
            ApplyFrameRate();
        }

        void ApplyFrameRate()
        {
            Application.targetFrameRate = (int)defaultFrameRate;
        }

        private enum AllowedFrameRates
        {
            Rate30 = 30,
            Rate60 = 60,
            Rate90 = 90,
            Rate120 = 120,
        }
    }
}
