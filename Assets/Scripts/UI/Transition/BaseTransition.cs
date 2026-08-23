using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using UnityEngine;

public abstract class BaseTransition : MonoBehaviour
{
    public static BaseTransition Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public abstract UniTask FadeIn(GamePlacement from, GamePlacement to);

    public abstract UniTask FadeOut();
}
