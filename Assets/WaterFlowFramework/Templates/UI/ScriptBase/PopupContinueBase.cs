using System;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Gameplay;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.ConfigManagement;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;

public class PopupContinueBase : Panel
{
    public class Data : UIData
    {
        public Action<string, object[]> onPlayOn;
        public Action onClose;
        public StuckType stuckType;
    }

    protected Data data;
    protected readonly Service<InventoryService> inventoryService = new();

    [SerializeField] protected TMP_Text txtPlayonPrice;
    protected CurrencyData playOnPrice;
    [Required] [SerializeField] protected Config<GamePlayConfig> gameConfig;

    public override void OnSetup()
    {
        base.OnSetup();
        playOnPrice = gameConfig.config.playOnPrice;
        txtPlayonPrice.text = playOnPrice.value.ToString();
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
        data = (Data)uiData;
        SetLayout();
    }

    protected virtual void SetLayout()
    {
    }

    public virtual void PlayOnWithCoinClick()
    {
        if (inventoryService.Instance.CanReduce(playOnPrice))
        {
            var log = new SpendResourceLogData()
            {
                earnType = "booster",
                earnId = "revive",
            };

            inventoryService.Instance.SpendCurrency(playOnPrice, log);

            PlayOn("play_on_coin");
        }
        else
        {
            PopupToast.Create("Not enough coin!");
        }
    }

    protected virtual void PlayOn(string by, object[] objectParams = null)
    {
        Close();
        data?.onPlayOn?.Invoke(by, objectParams);
    }

    public virtual void OnGiveUpClick()
    {
        Close();
        data?.onClose?.Invoke();
    }
}
