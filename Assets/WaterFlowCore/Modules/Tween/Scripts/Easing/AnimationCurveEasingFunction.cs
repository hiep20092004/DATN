using UnityEngine;

namespace WaterFlow.Core
{
    public class AnimationCurveEasingFunction : Ease.IEasingFunction
    {
        private AnimationCurve easingCurve;
        private float totalEasingTime;

        public AnimationCurveEasingFunction(AnimationCurve easingCurve)
        {
            this.easingCurve = easingCurve;

            totalEasingTime = easingCurve.keys[^1].time;
        }

        public float Interpolate(float p)
        {
            return easingCurve.Evaluate(p * totalEasingTime);
        }
    }
}