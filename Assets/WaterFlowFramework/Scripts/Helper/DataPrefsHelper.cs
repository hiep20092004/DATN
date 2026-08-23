using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.GameDataManagement;
using UnityEngine;

namespace WaterFlow.Framework.Helper
{
    public class IntDataPref
    {
        private static readonly Service<DataService> DataService = new();

        private readonly string _name;
        public Action<int> onChanged;

        public IntDataPref(string name)
        {
            _name = name;
        }

        public IntDataPref(string name, int defaultValue)
        {
            if (!DataService.Instance.HasKey(name))
                DataService.Instance.SetInt(name, defaultValue);
            _name = name;
        }

        public int Value
        {
            get => DataService.Instance.GetInt(_name, 0);
            set
            {
                if (value != Value)
                {
                    DataService.Instance.SetInt(_name, value);
                    onChanged?.Invoke(value);
                }
            }
        }

        public bool BoolValue
        {
            get => DataService.Instance.GetInt(_name, 0) != 0;
            set
            {
                if (value == (Value != 0)) return;
                DataService.Instance.SetInt(_name, value ? 1 : 0);
                onChanged?.Invoke(value ? 1 : 0);
            }
        }


        public bool HasKey()
        {
            return DataService.Instance.HasKey(_name);
        }
    }

    public class LongDataPref
    {
        private static readonly Service<DataService> DataService = new();


        private readonly string _name;

        private long _currentValue;
        public Action<long> OnChanged;

        public LongDataPref(string name)
        {
            _name = name;
            _currentValue = 0;
            try
            {
                if (!string.IsNullOrEmpty(DataService.Instance.GetString(_name, "")))
                    _currentValue = long.Parse(DataService.Instance.GetString(_name, ""));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public long Value
        {
            get => _currentValue;
            set
            {
                if (value != Value)
                {
                    _currentValue = value;
                    DataService.Instance.SetString(_name, value.ToString());
                    OnChanged?.Invoke(value);
                }
            }
        }
    }

    public class StringDataPref
    {
        private static readonly Service<DataService> DataService = new();

        private readonly string _name;

        private readonly string _default;

        public StringDataPref(string name, string defaultValue)
        {
            _name = name;
            _default = defaultValue;
        }

        public bool Exist => DataService.Instance.HasKey(_name) &&
                             !string.IsNullOrEmpty(DataService.Instance.GetString(_name, _default));

        public string Value
        {
            get => DataService.Instance.GetString(_name, _default);
            set
            {
                if (value != Value)
                    DataService.Instance.SetString(_name, value);
            }
        }

        public void Clear()
        {
            DataService.Instance.DeleteKey(_name);
        }
    }

    public class ListDataPref<T>
    {
        private static readonly Service<DataService> DataService = new();
        private readonly string _name;
        private readonly List<T> _default;

        public ListDataPref(string name, List<T> defaultValue)
        {
            _name = name;
            _default = defaultValue;
        }


        public bool Exist =>
            DataService.Instance.HasKey(_name) &&
            !string.IsNullOrEmpty(DataService.Instance.GetString(_name,
                string.Empty)); // Check if key exists and the stored string isn't empty


        public List<T> Value
        {
            get
            {
                string storedString = DataService.Instance.GetString(_name, string.Empty);
                if (string.IsNullOrEmpty(storedString))
                {
                    return _default != null ? new List<T>(_default) : new List<T>();
                }
                try
                {
                    return DeserializeList(storedString);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Error deserializing list {_name}: {e}");
                    return _default != null ? new List<T>(_default) : new List<T>();
                }
            }
            set
            {
                if (value == null) value = new List<T>();
                try
                {
                    string newValueString = SerializeList(value);
                    string currentValueString = DataService.Instance.GetString(_name, string.Empty);
                    if (newValueString != currentValueString)
                    {
                        DataService.Instance.SetString(_name, newValueString);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Error serializing list {_name}: {e}");
                }
            }
        }


        public void Clear()
        {
            DataService.Instance.DeleteKey(_name);
        }


        private string SerializeList(List<T> list)
        {
            if (list == null) return "[]";
            return JsonConvert.SerializeObject(list);
        }


        private List<T> DeserializeList(string storedString)
        {
            if (string.IsNullOrEmpty(storedString)) return new List<T>();
            var deserialized = JsonConvert.DeserializeObject<List<T>>(storedString);
            return deserialized ?? new List<T>();
        }


        //List functions
        public void Add(T item)
        {
            var currentList = Value;
            currentList.Add(item);
            Value = currentList;
        }


        public void Remove(T item)
        {
            var currentList = Value;
            currentList.Remove(item);
            Value = currentList;
        }


        public void Insert(int index, T item)
        {
            var currentList = Value;
            currentList.Insert(index, item);
            Value = currentList;
        }


        public void RemoveAt(int index)
        {
            var currentList = Value;
            currentList.RemoveAt(index);
            Value = currentList;
        }


        public int Count => Value.Count;


        public T this[int index]
        {
            get => Value[index];
            set
            {
                var currentList = Value;
                currentList[index] = value;
                Value = currentList;
            }
        }

        public bool Contains(T item)
        {
            return Value.Contains(item);
        }

        public void SetValue(int index, T item)
        {
            var currentList = Value;
            if (index < 0 || index >= currentList.Count)
            {
                Debug.LogWarning($"Index out of range: {index}");
                return;
            }
            currentList[index] = item;
            Value = currentList;
        }
    }

    public class ArrayDataPref<T>
    {
        private static readonly Service<DataService> DataService = new();
        private readonly string _name;
        private readonly T[] _default;

        public Action<T[]> OnChanged;

        public ArrayDataPref(string name, T[] defaultValue = null)
        {
            _name = name;
            _default = defaultValue ?? Array.Empty<T>();
        }

        public bool Exist =>
            DataService.Instance.HasKey(_name) &&
            !string.IsNullOrEmpty(DataService.Instance.GetString(_name, string.Empty));

        public T[] Value
        {
            get
            {
                string storedString = DataService.Instance.GetString(_name, string.Empty);
                if (string.IsNullOrEmpty(storedString))
                    return (T[])_default.Clone();
                try
                {
                    return JsonConvert.DeserializeObject<T[]>(storedString) ?? (T[])_default.Clone();
                }
                catch
                {
                    return (T[])_default.Clone();
                }
            }
            set
            {
                if (value == null) value = Array.Empty<T>();
                string newJson = JsonConvert.SerializeObject(value);
                string currentJson = DataService.Instance.GetString(_name, string.Empty);
                if (newJson != currentJson)
                {
                    DataService.Instance.SetString(_name, newJson);
                    OnChanged?.Invoke(value);
                }
            }
        }

        public void Clear()
        {
            DataService.Instance.DeleteKey(_name);
            OnChanged?.Invoke((T[])_default.Clone());
        }

        public int Length => Value.Length;

        public T this[int index]
        {
            get => Value[index];
            set
            {
                T[] current = Value;
                if (index < 0 || index >= current.Length) return;
                current[index] = value;
                Value = current;
            }
        }
    }

    public class ClassDataPref<T> where T : class, new()
    {
        private static readonly Service<DataService> DataService = new();
        private readonly string _name;
        private readonly T _default;
        private T _cache;

        public Action<T> OnChanged;

        public ClassDataPref(string name, T defaultValue = null)
        {
            _name = name;
            _default = defaultValue ?? new T();

            Load();
        }

        public bool Exist =>
            DataService.Instance.HasKey(_name) &&
            !string.IsNullOrEmpty(DataService.Instance.GetString(_name, string.Empty));

        public T Value
        {
            get => _cache;
            set
            {
                if (value == null) value = new T();
                _cache = value;
                Save();
                OnChanged?.Invoke(_cache);
            }
        }

        public void Clear()
        {
            _cache = new T();
            DataService.Instance.DeleteKey(_name);
            OnChanged?.Invoke(_cache);
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_cache);
                DataService.Instance.SetString(_name, json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Error saving {_name}: {e}");
            }
        }

        private void Load()
        {
            try
            {
                string str = DataService.Instance.GetString(_name, "");
                if (string.IsNullOrEmpty(str))
                {
                    _cache = _default != null ? Clone(_default) : new T();
                    Save();
                }
                else
                {
                    _cache = JsonConvert.DeserializeObject<T>(str);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Error loading {_name}: {e}");
                _cache = new T();
            }
        }

        private T Clone(T source)
        {
            try
            {
                var json = JsonConvert.SerializeObject(source);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return new T();
            }
        }
    }

}