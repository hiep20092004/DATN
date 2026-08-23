using System;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;
using Random = UnityEngine.Random;

public class UICollectEffectItem : MonoBehaviour, IPoolingObject
{
    protected int index = 0;
    protected Vector3 startPosition;
    protected Vector3 targetPosition;

    protected Action<int> onFinish;

    [SerializeField] protected UIResourceItem uiResourceItem;
    [SerializeField] protected AnimationCurve moveXCurve, moveYCurve;
    [SerializeField] protected float duration;
    [SerializeField] protected float speed;
    [SerializeField] protected bool rotate;

    public virtual void SetData(int index, GameResourceKey key, int quantity, Vector3 startPos, Vector3 endPos, Action<int> onFinish)
    {
        this.index = index;
        var resource = key;
        startPosition = startPos;
        targetPosition = endPos;
        this.onFinish = onFinish;
        transform.position = startPosition;
        uiResourceItem.SetData(resource, quantity);
        DOEffect();
    }

    protected virtual void DOEffect()
    {
        transform.DOMoveX(targetPosition.x, duration).SetEase(moveXCurve);
        transform.DOMoveY(targetPosition.y, duration).SetEase(moveYCurve).OnComplete(() =>
        {
            try
            {
                onFinish?.Invoke(index);
                onFinish = null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            GameSystem.GetService<PoolingServiceAsync>().ReturnObj(this);
        });
        if (rotate)
        {
            transform.Rotate(Vector3.forward, Random.Range(-360, 360));
            transform.DORotate(Vector3.zero, duration, RotateMode.FastBeyond360).SetEase(Ease.OutBounce);
        }
    }

    public void Setup()
    {
    }

    public virtual void OnCreateObj(params object[] args)
    {
    }

    public void OnReturnObj()
    {
        onFinish = null;
    }

    private void OnDisable()
    {
        onFinish = null;
    }
}