using System;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.UIModule.CollectEffect;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinPanelBase : Panel
{
    public class Data : UIData
    {
        public int level;
        public ResourceData reward;
        public Action nextLevel;
        public GameMode gameMode;
    }

    protected bool collected;

    [SerializeField] protected Image icon;
    [SerializeField] protected Transform panel;
    [SerializeField] protected Transform claimBtn;
    [SerializeField] protected float delayToCollect = 2f;
    [SerializeField] protected TMP_Text txtReward;
    protected Data data;

    #region DEFAULT

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
        data = (Data)uiData;
        collected = false;
        txtReward.text = data.reward.quantity.ToString();
    }

    #endregion

    public virtual void OnClaimClick()
    {
        if (collected) return;
        collected = true;
        OnClaimReward(claimBtn);
    }

    protected virtual void OnClaimReward(Transform btn)
    {
        EventBus<AddResourceVisualEvent>.Raise(new AddResourceVisualEvent()
        {
            source = "win",
            key = data.reward.Key,
            position = btn.position,
            visualQuantity = data.reward.quantity,
            collectEffect = new CollectEffectMultiple()
        });
        DOVirtual.DelayedCall(delayToCollect, NextLevel);
    }

    public virtual void NextLevel()
    {
        Close();
        data.nextLevel?.Invoke();
    }
}
