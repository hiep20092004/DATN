using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindFirstObjectByType<T>();

                    if (!_instance)
                    {
                        Debug.Log("Create new singleton: " + typeof(T).Name + " in scene");
                        var go = new GameObject { name = typeof(T).Name };
                        _instance = go.AddComponent<T>();
                        //Debug.LogError("[Singleton] There is no instance of " + typeof(T).Name + " in the scene.");
                    }
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (!_instance)
            {
                _instance = this as T;
                OnAwake();
            }
            else
            {
                Debug.LogError("Destroy duplicate singleton: " + typeof(T));
                Destroy(gameObject);
            }
        }

        protected virtual void OnAwake()
        {
            
        }
    }
}