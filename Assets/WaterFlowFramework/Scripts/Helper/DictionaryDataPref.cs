using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.GameDataManagement;
using UnityEngine;

namespace WaterFlow.Framework.Helper
{
    // Caches the deserialized dictionary in _current so in-place mutation (e.g. Value[key]--) persists
    // once written back through the Value setter, mirroring ClassDataPref's caching pattern.
    public class DictionaryDataPref<TKey, TValue>
    {
        private static readonly Service<DataService> DataService = new();
        private readonly string _name;
        private Dictionary<TKey, TValue> _current;

        public DictionaryDataPref(string name, Dictionary<TKey, TValue> defaultValue = null)
        {
            _name = name;
            try
            {
                string str = DataService.Instance.GetString(_name, string.Empty);
                _current = string.IsNullOrEmpty(str)
                    ? (defaultValue ?? new Dictionary<TKey, TValue>())
                    : JsonConvert.DeserializeObject<Dictionary<TKey, TValue>>(str) ?? new Dictionary<TKey, TValue>();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading {_name}: {e}");
                _current = new Dictionary<TKey, TValue>();
            }
        }

        public Dictionary<TKey, TValue> Value
        {
            get => _current;
            set
            {
                _current = value ?? new Dictionary<TKey, TValue>();
                try
                {
                    DataService.Instance.SetString(_name, JsonConvert.SerializeObject(_current));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error saving {_name}: {e}");
                }
            }
        }

        public bool ContainsKey(TKey key) => _current.ContainsKey(key);

        public TValue GetValueOrDefault(TKey key) => _current.TryGetValue(key, out var value) ? value : default;

        public void Add(TKey key, TValue value)
        {
            _current[key] = value;
            Value = _current;
        }

        public void Remove(TKey key)
        {
            _current.Remove(key);
            Value = _current;
        }

        public void Clear()
        {
            _current = new Dictionary<TKey, TValue>();
            DataService.Instance.DeleteKey(_name);
        }
    }
}
