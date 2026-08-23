using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.UIModule.SpriteService;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.UIModule.UIElements.UICollectItemEffect;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.BoosterManagement;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;

namespace WaterFlow.Framework.Templates.UI.ScriptBase
{
    public class PopupBuyBoosterBase : Panel
    {
        [SerializeField] protected TMP_Text txtTile;
        [SerializeField] protected TMP_Text txtCoinPrice;
        [SerializeField] protected TMP_Text txtValue;
        [SerializeField] protected FixedImageRatio icon;
        [SerializeField] protected string iconNamePattern = "ico_{0}";
        protected UIBoosterBase uIBooster;
        protected BoosterConfig boosterConfig;
        protected readonly Service<BoosterService> boosterService = new();
        protected readonly Service<SpriteAtlasService> spriteService = new();

        public override void Open(UIData uiData)
        {
            base.Open(uiData);
            boosterConfig = uiData.Get<BoosterConfig>("booster_config");
            txtCoinPrice.text = $"{boosterConfig.price.value}";
            txtValue.text = $"x{boosterConfig.packValue}";
            icon.SetSprite(spriteService.Instance.GetSprite(string.Format(iconNamePattern, boosterConfig.booster)));
        }

        public virtual void OnBuyWithCoinClick()
        {
            if (boosterService.Instance.BuyBooster(boosterConfig.booster))
            {
                UpdateBoosterVisual();
                Close();
            }
            else
            {
                PopupToast.Create("Not enough coin!");
            }
        }

        protected virtual void UpdateBoosterVisual()
        {
            EventBus<AddResourceVisualEvent>.Raise(new AddResourceVisualEvent()
            {
                key = new GameResourceKey() { gameResource = boosterConfig.booster },
                position = icon.transform.position,
                collectEffect = new CollectEffectSingle()
            });
        }
    }
}
