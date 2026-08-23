using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Helper;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.CollectEffect
{
    [CreateAssetMenu(menuName = "WaterFlow Anims/CollectEffectCurveStream")]
    public class CollectEffectCurveStream : CollectEffectControllerBase
    {
        [SerializeField] private float duration;
        [SerializeField] private float timeGap = 0.15f;
        [SerializeField] private AnimationCurve xCurve, yCurve;
        [SerializeField] private Service<PoolingServiceAsync> poolingService = new();
        [SerializeField] private string collectItemName = "UICollectCurveStreamItem";

        public async UniTaskVoid CreateEffect(GameResourceKey resourceKey, Vector3 startPos, Vector3 endPos, int number, Action onFinishStep = null,
            Action callback = null)
        {
            for (var i = 0; i < number; i++)
            {
                var effectItem = await poolingService.Instance.CreateAsync<UICollectCurveStreamItem>(
                    collectItemName, startPos,
                    PanelManager.Instance.transform, resourceKey);

                effectItem.transform.DOMoveX(endPos.x, duration).SetEase(xCurve);
                effectItem.transform.DOMoveY(endPos.y, duration).SetEase(yCurve)
                        .onComplete =
                    () =>
                    {
                        onFinishStep?.Invoke();
                        poolingService.Instance.ReturnObj(effectItem);
                    };
                await UniTask.Delay(TimeSpan.FromSeconds(timeGap));
            }

            await UniTask.Delay(TimeSpan.FromSeconds(duration));
            //Service<InventoryService>.Get().NotiUpdateResource(gameResource);
            callback?.Invoke();
        }
    }
}