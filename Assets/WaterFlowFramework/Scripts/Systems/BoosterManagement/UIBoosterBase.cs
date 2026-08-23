using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Utils;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using WaterFlow.Framework.Templates.UI.ScriptBase;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace WaterFlow.Framework.Systems.BoosterManagement
{
    public class UIBoosterBase : MonoBehaviour
    {
        public GameResource boosterType;
        public GameObject[] lockObj;
        public GameObject[] unlockObj;
        public TMP_Text txtQuantity;
        public TMP_Text txtLevelUnlock;
        public GameObject priceObj;
        public TMP_Text txtPrice;

        [SerializeField] protected readonly Service<BoosterService> boosterService = new();
        protected BoosterData boosterData;
        protected BoosterConfig config;

        private bool inited;
        protected bool unlocked;
        protected EventBinding<AddResourceVisualEvent> addBoosterEvent;
        protected bool usingBooster;

        protected virtual void OnEnable()
        {
            Init();
            UpdateData();
            boosterService.Instance.onUnlockBooster += OnBoosterUnlocked;
            addBoosterEvent = new EventBinding<AddResourceVisualEvent>(OnAddBooster);
            boosterService.Instance.InventoryService.OnResourceUpdate += OnResourceUpdate;
            //boosterData.boosterVisibleData.onUpdate += UpdateData;
        }

        protected virtual void OnDisable()
        {
            boosterService.Instance.onUnlockBooster -= OnBoosterUnlocked;
            EventBus<AddResourceVisualEvent>.Deregister(addBoosterEvent);
            boosterService.Instance.InventoryService.OnResourceUpdate -= OnResourceUpdate;
            addBoosterEvent = null;
        }

        protected virtual void Init()
        {
            if (inited) return;
            inited = true;

            config = boosterService.Instance.GetBoosterConfig(boosterType);
            boosterData = boosterService.Instance.GetBoosterData(boosterType);
            if (txtPrice != null)
                txtPrice.text = config.price.ToString();
            UpdateLockVisual(true);
        }

        private void OnBoosterUnlocked(GameResource resources)
        {
            if (boosterType != resources && resources != GameResource.MAX) return;
            UpdateData();
            UnlockBooster();
        }

        protected virtual void OnAddBooster(AddResourceVisualEvent eventData)
        {
            if (eventData.key.gameResource != this.boosterType) return;
            if (eventData.collectEffect != null)
            {
                eventData.collectEffect.Collect(eventData.key, 1, eventData.position, transform.position, (index) =>
                {
                    if (index == 0)
                    {
                        float defaultScale = transform.localScale.x;
                        transform.DOScale(defaultScale * 1.1f, 0.075f).SetLoops(2, LoopType.Yoyo);
                        GameSystem.GetService<InventoryService>().ClaimPendingResource("", boosterType.ToGameResourceKey());
                        UpdateData();
                    }
                });
            }
            else
            {
                UpdateData();
            }
        }

        protected virtual void OnResourceUpdate(GameResourceKey gameResourceKey)
        {
            if (gameResourceKey.gameResource != this.boosterType) return;
            UpdateData();
        }

        protected virtual void UpdateData()
        {
            txtQuantity.text = boosterData.resourceData.quantity.ToString();
            priceObj.SetActive(boosterData.resourceData.quantity <= 0);
        }

        protected virtual void UpdateLockVisual(bool force = false)
        {
            if (!force && unlocked) return;
            unlocked = boosterData.unlocked;
            if (lockObj != null)
                for (var i = 0; i < lockObj.Length; i++)
                    lockObj[i].SetActive(!unlocked);

            if (unlockObj != null)
                for (var i = 0; i < unlockObj.Length; i++)
                    unlockObj[i].SetActive(unlocked);

            if (!unlocked) txtLevelUnlock.text = $"Lv.{config.levelUnlock}";
        }

        protected virtual void UnlockBooster()
        {
            UpdateLockVisual();
        }


        public virtual void ClickBooster()
        {
            if (usingBooster) return;
            if (boosterService.Instance.CanUseBooster(boosterType))
            {
                UseBooster();
            }
            else
            {
                if (unlocked) OnOutOfBooster();
                else
                {
                    BoosterLockFeedback();
                }
            }
        }

        protected virtual void BoosterLockFeedback()
        {
        }

        public virtual void OnOutOfBooster()
        {
            UIData uiData = new UIData();
            uiData.Add("booster_config", config);
            PanelManager.Instance.OpenPanelByNameAsync<PopupBuyBoosterBase>("PopupBuyBooster", uiData).Forget();
        }

        public virtual void UseBooster()
        {
            usingBooster = true;
        }

        public virtual void OnUseBoosterSuccess()
        {
            usingBooster = false;
            boosterService.Instance.UseBoosterSuccess(boosterType);
        }
    }
}