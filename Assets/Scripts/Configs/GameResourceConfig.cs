using System;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.ConfigManagement;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "GameResourceConfig", menuName = "Services/Resource/GameResourceConfig")]
public class GameResourceConfig : ConfigSo
{
    [SerializeField] private SerializedDictionary<GameResource, GameResourceData> resourceDatas;

    public Sprite GetSprite(GameResource gameResource)
    {
        var data = GetRewardDataAt(gameResource);
        return data?.GetIcon();
    }
    
    public GameResourceData GetRewardDataAt(GameResource gameResource)
    {
        resourceDatas.TryGetValue(gameResource, out var data);
        return data;
    }

}


[Serializable]
public class GameResourceData
{
    [PreviewField(50, ObjectFieldAlignment.Left)]
    [SerializeField] Sprite icon;

    public Sprite GetIcon() => icon;
}
