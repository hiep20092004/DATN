using UnityEngine;

namespace WaterFlow.Core
{
    public abstract class BaseHapticWrapper
    {
        public abstract void Init();

        public abstract void Play(float duration = 0.3f, float intensity = 1.0f);
        public abstract void Play(string patternID);

        public abstract void RegisterPattern(HapticPattern pattern);

        protected void Log(string message)
        {
            if (!Haptic.VerboseLogging) return;

            Debug.Log($"[Haptic]: {message}");
        }

        protected void Try(SimpleCallback action, string errorMessage)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Haptic]: {errorMessage}");
                Debug.LogException(e);
            }
        }
    }
}
