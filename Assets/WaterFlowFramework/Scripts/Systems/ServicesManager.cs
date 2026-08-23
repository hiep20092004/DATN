using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WaterFlow.Framework.Systems
{
    [CreateAssetMenu(fileName = "[WATERFLOW] SERVICE MANAGER", menuName = "WaterFlow Services/WATERFLOW SERVICE MANAGER",
        order = -100)]
    public class ServicesManager : ScriptableObject
    {
        [SerializeField] [Searchable] [ListDrawerSettings(ShowPaging = false)]
        private List<ServiceSo> servicesObject;

        private readonly Dictionary<Type, object> services = new();


        public void Resolve()
        {
            services.Clear();
            foreach (var _service in servicesObject)
            {
#if UNITY_EDITOR
                var service = Instantiate(_service);
#else
                var service = _service;
#endif
                var type = service.GetType();

                if (!services.TryAdd(type, service))
                    Debug.LogError(
                        $"ServiceManager.Register: ServiceInstance of type {type.FullName} already registered");
            }

            foreach (var service in services.Values.ToList())
            {
                if (service is IServiceInitialize serviceInitialize)
                    serviceInitialize.Initialize();
                if (service is IServiceInitializeAsync serviceInitializeAsync)
                    serviceInitializeAsync.InitializeAsync();
            }
        }

        public T Get<T>() where T : class
        {
            if (TryGet<T>(out T result))
            {
                return result;
            }

            return null;
        }

        //
        //     public void Initialize()
        //     {
        //         foreach (var service in servicesObject)
        //         {
        //             var type = service.GetType();
        //
        //             if (!services.TryAdd(type, service))
        //                 Debug.LogError(
        //                     $"ServiceManager.Register: ServiceInstance of type {type.FullName} already registered");
        //         }
        //
        //         foreach (var service in servicesObject)
        //         {
        //             if (service is IServiceInitialize serviceInitialize)
        //                 serviceInitialize.Initialize();
        //             if (service is IServiceInitializeAsync serviceInitializeAsync)
        //                 serviceInitializeAsync.InitializeAsync();
        //         }
        //
        //         return this;
        //     }
        //

        public bool TryGetRaw<T>(out T service) where T : class
        {
            var type = typeof(T);
            foreach (var _service in services.Values)
            {
                if (_service.GetType() == type || _service.GetType().IsSubclassOf(type))
                {
                    service = _service as T;
                    return true;
                }
            }

            service = null;
            return false;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var obj))
            {
                service = obj as T;
                return true;
            }

            if (TryGetDirty(out service)) return true;
            return TryGetRaw(out service);
        }

        //
        //     public T Get<T>() where T : class
        //     {
        //         var type = typeof(T);
        //         if (services.TryGetValue(type, out var obj)) return obj as T;
        //
        //         if (TryGetDirty(out T service)) return service;
        //
        //         throw new ArgumentException($"ServiceManager.Get: ServiceInstance of type {type.FullName} not registered");
        //     }
        //
        private bool TryGetDirty<T>(out T service) where T : class
        {
            foreach (var keyValuePair in services)
                if (typeof(T).IsAssignableFrom(keyValuePair.Key))
                {
                    service = keyValuePair.Value as T;
                    services.Add(typeof(T), service);
                    return true;
                }

            service = null;
            return false;
        }

        public void OnApplicationFocus(bool focus)
        {
            foreach (var service in services.Values)
            {
                if (service is IServiceActionOnFocus serviceActionOnFocus)
                {
                    serviceActionOnFocus.OnApplicationFocus(focus);
                }
            }
        }


        // public ServicesManager Register<T>(T service) where T : class
        // {
        //     var type = typeof(T);
        //
        //     if (!services.TryAdd(type, service))
        //         Debug.LogError($"ServiceManager.Register: ServiceInstance of type {type.FullName} already registered");
        //     if (service is IServiceInitialize serviceInitialize)
        //         serviceInitialize.Initialize();
        //     if (service is IServiceInitializeAsync serviceInitializeAsync)
        //         serviceInitializeAsync.InitializeAsync();
        //     return this;
        // }

        //
        //     public ServicesManager Register(Type type, object service)
        //     {
        //         if (!type.IsInstanceOfType(service))
        //             throw new ArgumentException("Type of service does not match type of service interface",
        //                 nameof(service));
        //
        //         if (!services.TryAdd(type, service))
        //             Debug.LogError($"ServiceManager.Register: ServiceInstance of type {type.FullName} already registered");
        //         if (service is IServiceInitialize serviceInitialize)
        //             serviceInitialize.Initialize();
        //         if (service is IServiceInitializeAsync serviceInitializeAsync)
        //             serviceInitializeAsync.InitializeAsync();
        //         return this;
        //     }
        //
        //
        //     public ServicesManager Register(params object[] servicesToRegister)
        //     {
        //         foreach (var service in servicesToRegister)
        //         {
        //             var type = service.GetType();
        //
        //             if (!services.TryAdd(type, service))
        //                 Debug.LogError(
        //                     $"ServiceManager.Register: ServiceInstance of type {type.FullName} already registered");
        //         }
        //
        //         foreach (var service in servicesToRegister)
        //         {
        //             if (service is IServiceInitialize serviceInitialize)
        //                 serviceInitialize.Initialize();
        //             if (service is IServiceInitializeAsync serviceInitializeAsync)
        //                 serviceInitializeAsync.InitializeAsync();
        //         }
        //
        //         return this;
        //     }
        //
        //     public ServicesManager ForceRegister(Type type, object service)
        //     {
        //         if (services.ContainsKey(type))
        //             services[type] = service;
        //         else
        //             services.Add(type, service);
        //         if (service is IServiceInitialize serviceInitialize)
        //             serviceInitialize.Initialize();
        //         if (service is IServiceInitializeAsync serviceInitializeAsync)
        //             serviceInitializeAsync.InitializeAsync();
        //         return this;
        //     }
    }
}