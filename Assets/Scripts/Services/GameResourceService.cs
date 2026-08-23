using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using UnityEngine;

[CreateAssetMenu(fileName = "GameResourceService", menuName = "Services/GameResourceService")]
public class GameResourceService : ServiceSo, IServiceInitializeAsync
{
    [SerializeField] GameResourceConfig config;

    public async UniTaskVoid InitializeAsync()
    {
        
    }

    public Sprite GetSprite(GameResource gameResource)
    {
        return config.GetSprite(gameResource);
    }
}
