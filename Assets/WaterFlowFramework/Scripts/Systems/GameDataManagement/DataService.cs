using Newtonsoft.Json;

namespace WaterFlow.Framework.Systems.GameDataManagement
{
    public abstract class DataService : ServiceSo
    {
        public abstract bool HasKey(string key);

        public abstract void DeleteKey(string key);
        public abstract void SetInt(string key, int value);

        public abstract int GetInt(string key, int defaultValue);

        public abstract void SetFloat(string key, float value);

        public abstract float GetFloat(string key, float defaultValue);

        public abstract void SetString(string key, string value);

        public abstract string GetString(string key, string defaultValue);

        public abstract bool GetBool(string key, bool defaultValue);
        public abstract void SetBool(string key, bool value);

        public abstract T GetData<T>(string key);
        public abstract T GetData<T>(string key, JsonConverter converter);
        public abstract void SetData<T>(string key, T data);
        public abstract void SetData<T>(string key, T data, JsonConverter converter);

        public abstract void SaveData();
    }
}