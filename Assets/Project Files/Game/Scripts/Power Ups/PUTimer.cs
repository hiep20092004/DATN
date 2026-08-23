using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class PUTimer
    {
        public float State => delayTweenCase.State;
        public string Seconds => (duration - (duration * delayTweenCase.State)).ToString("F0");

        private float duration;
        private float startTime;

        private TweenCase delayTweenCase;

        private bool isActive;
        public bool IsActive => isActive;

        public PUTimer(float duration, SimpleCallback onCompleted)
        {
            this.duration = duration;

            startTime = Time.time;

            delayTweenCase = Tween.DelayedCall(duration, onCompleted);

            isActive = true;
        }

        public void Pause()
        {
            delayTweenCase?.Pause();
        }

        public void Resume()
        {
            delayTweenCase?.Resume();
        }

        public void OnCompleted(SimpleCallback onCompleted)
        {
            if(delayTweenCase.IsActive && !delayTweenCase.IsCompleted)
            {
                delayTweenCase.OnComplete(onCompleted);
            }
            else
            {
                onCompleted?.Invoke();
            }
        }

        public void Disable()
        {
            isActive = false;

            delayTweenCase.KillActive();
        }
    }
}