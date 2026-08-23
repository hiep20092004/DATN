using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;
using Random = UnityEngine.Random;

public class CollectEffectMultipleStar : BaseCollectEffect
{
    public string collectEffectName = "CollectResourceMultipleStar";
    public float radius = 0.65f;

    public override async UniTask Collect(GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinishStep = null,
        Action callback = null)
    {
        await Spawn(key, quantity, startPos, endPos, onFinishStep, callback);
    }

    private async UniTask Spawn(GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinishStep, Action callback)
    {
        for (int i = 0; i < quantity; i++)
        {
            Vector3 ran = Random.insideUnitCircle * radius;
            Vector3 pos = startPos + ran;
            var item = await GameSystem.GetService<PoolingServiceAsync>().CreateAsync<UICollectEffectItemMultiple>(collectEffectName,
                PanelManager.Instance.transform);
            item.SetIntermediaryPos(pos);
            item.SetData(i, key, quantity, startPos, endPos, onFinishStep);
        }

        callback?.Invoke();
    }
}