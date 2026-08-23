using UnityEngine;

namespace WaterFlow.Core
{
    public sealed class EditorHapticWrapper : BaseHapticWrapper
    {
        public override void Init()
        {
            Log("Module is Initialized!");
        }

        public override void Play(float duration = 0.3f, float intensity = 1.0f)
        {
            Log($"Play method invoked (Duration: {duration}; Intensity: {intensity})!");
        }

        public override void Play(string patternID)
        {
            Log($"Play method invoked (PatternID: {patternID})!");
        }

        public override void RegisterPattern(HapticPattern pattern)
        {
            Log($"Pattern with ID: {pattern.ID} is registered!");
        }
    }
}
