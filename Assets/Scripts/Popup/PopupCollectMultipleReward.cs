using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using UnityEngine;

public class PopupCollectMultipleReward : Panel
{
    public Transform Container;
    public int MaxItem = 10;
    private Vector3 startPos;
    private Vector3 endPos;
    private GameResourceKey key;
    private Action onFinish;
    private bool playOnAwake = true;
    public static void Show(GameResourceKey key, Vector3 startPos, Vector3 endPos, bool playOnAwake = true, Action onFinish = null)
    {
        UIData uiData = new UIData();
        uiData.Add("GameResourceKey", key);
        uiData.Add("StartPos", startPos);
        uiData.Add("EndPos", endPos);
        uiData.Add("PlayOnAwake", playOnAwake);
        uiData.Add("OnFinish", onFinish);
        PanelManager.Instance.OpenForget<PopupCollectMultipleReward>(uiData);
    }

    public override void OnSetup()
    {
        base.OnSetup();
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
        if (uiData.TryGet("GameResourceKey", out GameResourceKey key))
        {
            this.key = key;
        }
        if (uiData.TryGet("StartPos", out Vector3 startPos))
        {
            this.startPos = startPos;
        }
        if (uiData.TryGet("EndPos", out Vector3 endPos))
        {
            this.endPos = endPos;
        }
        if (uiData.TryGet("PlayOnAwake", out bool playOnAwake))
        {
            this.playOnAwake = playOnAwake;
        }
        if (uiData.TryGet("OnFinish", out Action onFinish))
        {
            this.onFinish = onFinish;
        }
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
        PlayEffect().Forget();
    }

    private async UniTask PlayEffect()
    {
        var ct = this.GetCancellationTokenOnDestroy();
        for (int i = 0; i <= MaxItem; i++)
        {
            if (ct.IsCancellationRequested || Container == null)
            {
                return;
            }
            RewardToUI rewardToUIPrefab = Resources.Load<RewardToUI>("UI/RewardToUI");
            RewardToUI rewardToUI = Instantiate(rewardToUIPrefab, Container);
            rewardToUI.SetData(i, key, startPos, endPos, true, null);
        }

        await UniTask.WaitForSeconds(0.2f, cancellationToken: ct);
        await UniTask.WaitUntil(() => Container == null || Container.childCount == 0, cancellationToken: ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }
        onFinish?.Invoke();
        Close();
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
    }
}
