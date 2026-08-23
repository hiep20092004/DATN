using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;
using Random = UnityEngine.Random;

public class CollectEffectMultiple : BaseCollectEffect
{
    public string collectEffectName = "CollectResourceMultipleItem";
    public int maxCount = 10;
    public float radius = 1.25f;
    public float delayBetweenItems = 0.04f;

    public override async UniTask Collect(GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinishStep = null,
        Action callback = null)
    {
        Spawn(key, quantity, startPos, endPos, onFinishStep, callback).Forget();
    }

    private async UniTaskVoid Spawn(GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinishStep, Action callback)
    {
        int count = Mathf.Min(quantity, maxCount);
        for (int i = 0; i < count; i++)
        {
            Vector3 ran = Random.insideUnitCircle * radius;
            Vector3 pos = startPos + ran;
            var item = await GameSystem.GetService<PoolingServiceAsync>().CreateAsync<UICollectEffectItemMultiple>(collectEffectName,
                PanelManager.Instance.transform);
            item.SetIntermediaryPos(pos);
            item.SetData(i, key, quantity, startPos, endPos, onFinishStep);
            if (key != GameResource.Coin.ToGameResourceKey())
            {
                delayBetweenItems = 0.06f;
            } else {
                delayBetweenItems = 0.04f;
            }
            await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenItems));
        }

        callback?.Invoke();
    }
}