using UnityEngine;

public class SingletonSimple<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying) _instance = FindFirstObjectByType<T>();

            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            OnAwake();
        }
        else if (_instance != this)
        {
            Debug.LogWarning("Destroy duplicate singleton: " + typeof(T).ToString().Color("red"));
            Destroy(gameObject);
        }
    }

    protected virtual void OnAwake()
    {
    }
}