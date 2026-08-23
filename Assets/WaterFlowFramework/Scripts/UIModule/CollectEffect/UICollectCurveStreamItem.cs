using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.CollectEffect
{
    public class UICollectCurveStreamItem : MonoBehaviour, IPoolingObject
    {
        [SerializeField] private UIResourceItem uiResourceItem;
        public Transform Transform => transform;

        public void Setup()
        {
        }

        public void OnCreateObj(params object[] args)
        {
            transform.DOKill();
            uiResourceItem.SetData((GameResourceKey)args[0]);
        }

        public void OnReturnObj()
        {
        }
    }
}