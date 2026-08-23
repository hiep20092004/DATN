using System;

namespace WaterFlow.Framework.UIModule
{
    public interface UITweenBase
    {
        public event Action OnCompleted;
        public void Play();
        public void Play(float from, float to, float duration);
    }
}