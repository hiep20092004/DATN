using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Lofelt.NiceVibrations;
using Popup;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ease = DG.Tweening.Ease;

namespace WaterFlow.Game
{
    public class PowerUpItemView : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] GameObject defaultState;

        [Space][SerializeField] GameObject amountContainerObject;
        [SerializeField] TextMeshProUGUI amountText;
        [SerializeField] GameObject amountPurchaseObject;

        [Space][SerializeField] GameObject lockStateObject;
        [SerializeField] TextMeshProUGUI lockText;
        [SerializeField] Image lockImage;

        [Space][SerializeField] GameObject timerObject;
        [SerializeField] Image timerBackground;

        [Space][Header("Navigation Hint")]
        [SerializeField] private GameObject navHighlightObject;

        private DG.Tweening.Tween navHighlightTween;

        private PowerUpBehavior behavior;
        private BoosterData boosterData;
        private BoosterConfig boosterConfig;

        private bool isTimerActive;
        private Button button;
        private bool isActive = false;
        private bool isLocked = false;
        private Action<PowerUpItemView> onButtonClickedCallback;

        public bool IsActive => isActive;
        private Coroutine timerCoroutine;
        public PowerUpBehavior Behavior => behavior;
        public RectTransform IconRectTransform => iconImage.rectTransform;

        private int EffectiveLevelUnlock =>
            boosterConfig != null ? Services.BoosterService.GetEffectiveLevelUnlock(boosterConfig) : 0;
        
        public static PowerUpItemView Init(
            GameObject itemPrefab,
            Transform parent,
            PowerUpBehavior powerUpBehavior,
            Action<PowerUpItemView> onButtonClicked = null)
        {
            var itemObject = Instantiate(itemPrefab, parent);
            var itemView = itemObject.GetComponent<PowerUpItemView>();
            itemView.Init(powerUpBehavior);
            itemView.onButtonClickedCallback = onButtonClicked;
            return itemView;
        }


        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonClicked);
        }

        public void Init(PowerUpBehavior powerUpBehavior)
        {
            behavior = powerUpBehavior;
            boosterConfig = powerUpBehavior.Config;
            boosterData = Services.BoosterService.GetBoosterData(powerUpBehavior.Config.type);

            ApplyVisuals();
            UpdateData();
            gameObject.SetActive(false);
            isActive = false;

            Services.InventoryService.OnResourceUpdate += OnResourceUpdate;
        }

        private void OnDestroy()
        {
            if (Services.InventoryService)
                Services.InventoryService.OnResourceUpdate -= OnResourceUpdate;
        }

        private void OnResourceUpdate(GameResourceKey resourceKey)
        {
            // Only redraw if the resource is a booster type
            if (resourceKey.gameResource == boosterConfig.type)
            {
                UpdateData();
            }
        }

        public void OnLevelLoaded(int levelNumber)
        {
            SetLocked(!LevelController.IsTestModeActive && levelNumber < EffectiveLevelUnlock);
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
            lockStateObject.SetActive(locked);
            defaultState.SetActive(!locked);

            if (locked)
                UpdateLockText();
        }


        private void UpdateLockText()
        {
            if (boosterConfig == null) return;
            lockText.text = StringParameterReplacer.ReplaceParameters(ScriptLocalization.level_value, new Dictionary<string, string> {
                    { "value", EffectiveLevelUnlock.ToString() }
            });
        }

        void OnButtonClicked()
        {
            if (isLocked)
            {
                Services.AudioService.PlaySound(AudioId.Booster_Denied);
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.Deny);
                PlayLockDeniedTween();
                return;
            }
            Services.AudioService.PlaySound(AudioId.ButtonClick);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Selection);

            if (LevelController.IsTestModeActive || Services.BoosterService.CanUseBooster(boosterConfig.type))
            {
                UseBooster();
            }
            else
            {
                if (!behavior.IsBusy)
                {
                    PowerUpController.UnselectPowerUp();
                    // Show Purchase Popup

                    UIData uiData = new UIData();
                    uiData.Add("BoosterConfig", Services.BoosterService.GetBoosterConfig(boosterConfig.type));
                    PanelManager.Instance.OpenForget<PopupBuyBooster>(uiData);
                }
            }
            onButtonClickedCallback?.Invoke(this);
        }

        private void PlayLockDeniedTween()
        {
            var message = StringParameterReplacer.ReplaceParameters(ScriptLocalization.unlock_at_lvvalue, new Dictionary<string, string> {
                { "value", EffectiveLevelUnlock.ToString() }
            });
            PopupTooltip.Instance.Show(message);
            
            if (lockImage != null)
                WaterFlow.Game.TweenCommon.LockSwing.Play(lockImage.rectTransform);

            if (lockText != null)
            {
                RectTransform lockTextRectTransform = lockText.rectTransform;
                lockTextRectTransform.DOKill();
                lockTextRectTransform.localScale = Vector3.one;
                lockTextRectTransform
                    .DOScale(1.2f, 0.12f)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(2, LoopType.Yoyo)
                    .OnComplete(() => lockTextRectTransform.localScale = Vector3.one);
            }
        }

        private void UseBooster()
        {
            if (behavior.IsBusy) return;
            GameController.Instance.ActivateGame();
            PowerUpType powerUpType = boosterConfig.GetPowerUpConfig().Type;
            if (behavior.IsSelectTargetType())
            {
                PowerUpController.SelectPowerUp(powerUpType);
            }
            else
            {
                PowerUpController.UsePowerUp(powerUpType);
            }
        }

        public void OnAddBooster(AddResourceVisualEvent eventData)
        {
            if (eventData.key.gameResource != boosterConfig.type) return;
            Services.InventoryService
                .ClaimPendingResource(string.Empty, boosterConfig.type.ToGameResourceKey());
            if (!transform) return;
            float defaultScale = transform.localScale.x;
            transform.DOScale(defaultScale * 1.1f, 0.075f)
                .SetLoops(2, LoopType.Yoyo);
            UpdateData();
        }

        private void ApplyVisuals()
        {
            BasePowerUpConfig powerUpConfig = behavior.PowerUpConfig;
            iconImage.sprite = powerUpConfig.Icon;
            iconImage.color = Color.white;
            timerBackground.fillAmount = 0f;
        }

        public void UpdateData()
        {
            int amount = boosterData.resourceData.quantity;
            if (LevelController.IsTestModeActive || amount > 0)
            {
                amountContainerObject.SetActive(true);
                amountPurchaseObject.SetActive(false);

                // Test sandbox shows unlimited stock so power-ups never look purchasable/locked.
                amountText.text = LevelController.IsTestModeActive ? "∞" : amount.ToString();
            }
            else
            {
                amountContainerObject.SetActive(false);
                amountPurchaseObject.SetActive(true);
            }

            PUTimer timer = behavior.GetTimer();
            if (!isTimerActive)
            {
                if (timer != null)
                {
                    timerCoroutine = StartCoroutine(TimerCoroutine(timer));
                }
            }

            // if (config.VisualiseActiveState)
            //     RedrawBusyVisuals(behavior.IsBusy);

            // if(behavior.IsSelectable())
            //     selectedOutlineObject.SetActive(behavior.IsSelected);

            behavior.OnRedrawn();
        }

        public void Activate(bool isTween = true)
        {
            isActive = true;

            gameObject.SetActive(true);

            if (isTween)
            {
                transform.localScale = Vector3.zero;
                transform.DOScale(1.0f, 0.3f).SetEase(Ease.OutBack);
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            UpdateData();
        }


        public void Hide()
        {
            isActive = false;
            gameObject.SetActive(false);
        }

        public void OnLevelStarted(int levelNumber)
        {
            if (timerCoroutine != null)
                StopCoroutine(timerCoroutine);
        }

        public void SetNavigationHighlight(bool active)
        {
            navHighlightTween?.Kill();

            if (!navHighlightObject) return;
            
            navHighlightObject.transform.localScale = Vector3.one;
            if (active)
            {
                navHighlightTween = navHighlightObject.transform
                    .DOScale(1.15f, 0.4f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        public void OnLevelFinished()
        {
            if (isTimerActive)
            {
                if (timerCoroutine != null)
                {
                    StopCoroutine(timerCoroutine);
                }

                timerObject.SetActive(false);
                iconImage.color = Color.white;

                isTimerActive = false;
            }

            SetNavigationHighlight(false);
        }

        private IEnumerator TimerCoroutine(PUTimer timer)
        {
            isTimerActive = true;

            timerObject.SetActive(true);
            timerBackground.fillAmount = 1.0f;

            iconImage.color = new Color(1, 1, 1, 0.3f);

            while (timer.IsActive)
            {
                yield return null;
                yield return null;

                timerBackground.fillAmount = 1.0f - timer.State;

                behavior.OnTimerTick();

                if (timerBackground.fillAmount <= 0.0f)
                    break;
            }

            timerObject.SetActive(false);
            iconImage.color = Color.white;

            isTimerActive = false;
        }
    }
}