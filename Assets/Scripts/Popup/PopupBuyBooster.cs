using WaterFlow.Game;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using System;
using System.Collections.Generic;
using WaterFlow.Framework.UIModule.UIElements.UICollectItemEffect;
using WaterFlow.Framework.Systems.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupBuyBooster : Panel
{
    [ReadOnly]
    [SerializeField] protected BoosterConfig boosterConfig;

    [SerializeField] protected Button CloseButton;
    [SerializeField] protected Button BuyButton;
    [SerializeField] protected TMP_Text HeaderText;
    [SerializeField] protected TMP_Text DescriptionText;
    [SerializeField] protected TMP_Text PriceText;
    [SerializeField] protected TMP_Text ValueText;
    [SerializeField] protected Image MainIcon;
    [SerializeField] protected TextMeshProUGUI ValueCoinText;
    
    public override void OnSetup()
    {
        base.OnSetup();
        CloseButton.onClick.AddListener(OnCloseClick);
        BuyButton.onClick.AddListener(OnBuyClick);
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);

        if (uiData.TryGet("BoosterConfig", out BoosterConfig boosterConfig))
        {
            this.boosterConfig = boosterConfig;
        }
        
        InitPopup();
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
        CloseButton.onClick.RemoveListener(OnCloseClick);
        BuyButton.onClick.RemoveListener(OnBuyClick);
    }

    private void OnCloseClick()
    {
        Close();
    }

    private void OnBuyClick()
    {
        Services.InventoryService.TryBuyResourceByCurrency(boosterConfig.price, () =>
        {
            OnBuyComplete(boosterConfig.AmountPerBuy);
        });
    }

    private void OnBuyComplete(int amount)
    {
        Services.InventoryService.AddResource(new ResourceData(boosterConfig.type, amount), new EarnResourceLogData()
        {
            spendType = "buy_booster",
            spendId = boosterConfig.type.ToString(),
            source = "non_iap"
        });
        EventBus<AddResourceVisualEvent>.Raise(new AddResourceVisualEvent()
        {
            key = new GameResourceKey()
            {
                gameResource = boosterConfig.type
            },
            position = MainIcon.transform.position,
            collectEffect = new CollectEffectMultiple()
        });
        Close();
    }

    protected virtual void InitPopup()
    {
        HeaderText.text = boosterConfig.GetHeader();
        DescriptionText.text = boosterConfig.GetDescription();
        PriceText.text = boosterConfig.price.value.ToString();
        MainIcon.sprite = boosterConfig.GetIcon();
        UpdateButtonValueText();
    }

    private void UpdateButtonValueText()
    {
        ValueCoinText.text = $"Buy x{boosterConfig.AmountPerBuy}";
    }
}
