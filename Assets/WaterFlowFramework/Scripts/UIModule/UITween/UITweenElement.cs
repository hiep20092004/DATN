using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
// Aliased instead of `using WaterFlow.Core` — that namespace also declares inspector
// attributes (ShowIf, Button, …) that collide with Odin's.
using HapticType = WaterFlow.Core.HapticType;
using HapticFeedback = WaterFlow.Core.HapticFeedback;

namespace WaterFlow.Framework.UIModule
{
    public class UITweenElement : MonoBehaviour
    {
        public TweenData tweenData;
        public bool playOnAwake = true;

        public bool playHaptic = false;
        [ShowIf("playHaptic")]
        public HapticType hapticTypeOnCompleted = HapticType.None;
        [ShowIf("playHaptic")]
        public float hapticDelayOnStart = 0f;
        [ShowIf("playHaptic")]
        public HapticType hapticTypeOnStart = HapticType.None;

        private void OnEnable()
        {
            if (tweenData.target == null) tweenData.target = transform;
            if (playOnAwake) Play();
            if (playHaptic)
            {
                if (hapticTypeOnStart != HapticType.None)
                {
                    DOVirtual.DelayedCall(hapticDelayOnStart, () =>
                    {
                        HapticFeedback.Play(hapticTypeOnStart);
                    });
                }
                if (hapticTypeOnCompleted != HapticType.None)
                {
                    tweenData.OnCompleted += () =>
                    {
                        HapticFeedback.Play(hapticTypeOnCompleted);
                    };
                }
            }
        }


        public void Play()
        {
            UITween.Play(tweenData).Forget();
        }
    }
}