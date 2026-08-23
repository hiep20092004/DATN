using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterFlow.Framework.Systems.EventBus
{
    public static class EventBus<T> where T : IEvent
    {
        private static readonly HashSet<IEventBinding<T>> bindings = new();
        public static T lastEvent;

        public static void Register(IEventBinding<T> binding, bool getLastData = false)
        {
            if (getLastData && lastEvent != null) binding.OnEvent?.Invoke(lastEvent);
            bindings.Add(binding);
        }

        public static void Deregister(EventBinding<T> binding)
        {
            try
            {
                bindings.Remove(binding);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void Raise(T @event)
        {
            var snapshot = new HashSet<IEventBinding<T>>(bindings);

            foreach (var binding in snapshot)
            {
                if (binding != null && bindings.Contains(binding))
                {
                    binding.OnEvent?.Invoke(@event);
                    binding.OnEventNoArgs?.Invoke();
                }
            }

            lastEvent = @event;
        }

        public static bool ContainsBinding(IEventBinding<T> binding)
        {
            return bindings.Contains(binding);
        }

        public static void Clear()
        {
            Debug.Log($"Clearing {typeof(T).Name} bindings");
            bindings.Clear();
        }
    }
}