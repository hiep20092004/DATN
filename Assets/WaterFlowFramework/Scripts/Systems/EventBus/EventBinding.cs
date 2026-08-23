using System;

namespace WaterFlow.Framework.Systems.EventBus
{
    public interface IEventBinding<T>
    {
        public Action<T> OnEvent { get; set; }
        public Action OnEventNoArgs { get; set; }
    }

    public class EventBinding<T> : IEventBinding<T> where T : IEvent
    {
        private Action<T> onEvent = _ => { };
        private Action onEventNoArgs = () => { };

        public EventBinding(Action<T> onEvent, bool getLastData = false)
        {
            this.onEvent = onEvent;
            EventBus<T>.Register(this, getLastData);
        }

        public EventBinding(Action onEventNoArgs, bool getLastData = false)
        {
            this.onEventNoArgs = onEventNoArgs;
            EventBus<T>.Register(this, getLastData);
        }

        Action<T> IEventBinding<T>.OnEvent
        {
            get => onEvent;
            set => onEvent = value;
        }

        Action IEventBinding<T>.OnEventNoArgs
        {
            get => onEventNoArgs;
            set => onEventNoArgs = value;
        }

        public void Add(Action onEvent)
        {
            onEventNoArgs += onEvent;
        }

        public void Remove(Action onEvent)
        {
            onEventNoArgs -= onEvent;
        }

        public void Add(Action<T> onEvent)
        {
            this.onEvent += onEvent;
        }

        public void Remove(Action<T> onEvent)
        {
            this.onEvent -= onEvent;
        }
    }
}