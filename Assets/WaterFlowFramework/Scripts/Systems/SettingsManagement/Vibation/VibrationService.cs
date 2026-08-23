using System;

namespace WaterFlow.Framework.Systems.SettingsManagement.Vibation
{
    public abstract class VibrationService : ServiceSo
    {
        public Action onVibrateUpdate;
        public abstract void SetVibrationState(bool state);
        public abstract bool GetVibrationState();

        public abstract void Vibrate(long milliseconds);
    }
}