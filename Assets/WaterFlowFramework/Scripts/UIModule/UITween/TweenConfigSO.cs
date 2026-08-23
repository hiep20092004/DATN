using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace WaterFlow.Framework.UIModule
{
    [CreateAssetMenu(menuName = "WaterFlow/UI Tween/Tween Config")]
    public class TweenConfigSO : ScriptableObject
    {
        public TweenConfig config;
    }

    [Serializable]
    public class TweenConfig
    {
        public UITweenType tweenType;

        [ShowIf(
            "@tweenType == UITweenType.Fade || tweenType == UITweenType.FadeGroup || tweenType == UITweenType.Scale")]
        public float from;

        [ShowIf(
            "@tweenType == UITweenType.Fade || tweenType == UITweenType.FadeGroup || tweenType == UITweenType.Scale")]
        public float to = 1;

        [ShowIf(
            "@tweenType == UITweenType.Move || tweenType == UITweenType.LocalMove || tweenType == UITweenType.RectLocalMove")]
        public Vector3 mFrom;

        [ShowIf(
            "@tweenType == UITweenType.Move || tweenType == UITweenType.LocalMove || tweenType == UITweenType.RectLocalMove")]
        public Vector3 mTo;

        public float duration;
        public float delay;
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        public TweenConfig Clone()
        {
            return new TweenConfig
            {
                tweenType = tweenType,
                from = from,
                to = to,
                duration = duration,
                delay = delay,
                mTo = mTo,
                mFrom = mFrom,
                curve = curve
            };
        }
    }
}