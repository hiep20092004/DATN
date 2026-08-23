using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.UIElements.UICollectItemEffect
{
    public class CollectEffectSingle : BaseCollectEffect
    {
        public string collectEffectName = "CollectResourceSingleItem";

        public override async UniTask Collect(GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinishStep, Action callback)
        {
            var collectEffectItem = await GameSystem.GetService<PoolingServiceAsync>().CreateAsync<UICollectEffectItem>(collectEffectName,
                PanelManager.Instance.transform);
            collectEffectItem.SetData(0, key, quantity, startPos, endPos, (index) =>
            {
                onFinishStep?.Invoke(0);
                callback?.Invoke();
            });
        }
    }
}