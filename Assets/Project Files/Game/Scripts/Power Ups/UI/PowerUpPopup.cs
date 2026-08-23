using Cysharp.Threading.Tasks;
using DG.Tweening;
using Popup;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Systems.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ease = DG.Tweening.Ease;

namespace WaterFlow.Game
{
    public class PowerUpPopup : MonoBehaviour
    {
        [SerializeField] RectTransform botContainer;
        [SerializeField] RectTransform itemContainer;
        [SerializeField] GameObject itemPrefab;
        [SerializeField] float heightWhenNoBannerAds = 200f;
        
        [Space]
        [SerializeField] GameObject selectionPanelObject;
        [SerializeField] Image selectionIconImage;
        [SerializeField] TextMeshProUGUI selectionDescriptionText;
        [SerializeField] Button selectionCloseButton;
        [SerializeField] TextMeshProUGUI textNoTarget;
        
        [Space]
        [Header("Tutorial Panel")]
        [SerializeField] Button tutorialPanel;
        [SerializeField] Image tutorialIconImage;
        [SerializeField] TextMeshProUGUI tutorialDescriptionText;
        [SerializeField] RectTransform arrow;
        
        private PowerUpItemView[] powerUpItemViews;
        private DG.Tweening.Tween containerTween;
        private CanvasGroup containerCanvasGroup;
        private Vector2 containerInitialAnchoredPosition;
        
        private const float ContainerTweenDuration = 0.25f;
        private const float ContainerSpawnDelay = 1f;
        private NotifyTextPresenter noTargetNotifyPresenter;
        
        protected EventBinding<AddResourceVisualEvent> addBoosterEvent;

        private void Awake()
        {
            selectionPanelObject.gameObject.SetActive(false);
            selectionCloseButton.onClick.AddListener(OnSelectionCloseButtonClicked);
            tutorialPanel.gameObject.SetActive(false);
            
            if (!noTargetNotifyPresenter && textNoTarget)
            {
                noTargetNotifyPresenter = NotifyTextPresenter.GetOrAdd(textNoTarget);
            }
            
            containerCanvasGroup = botContainer.GetComponent<CanvasGroup>();
            if (!containerCanvasGroup)
            {
                containerCanvasGroup = botContainer.gameObject.AddComponent<CanvasGroup>();
            }
        }

        internal void InitializeItemViews()
        {
            var activePowerUps = PowerUpController.ActivePowerUps;
            if (activePowerUps == null || activePowerUps.Length == 0)
            {
                powerUpItemViews = System.Array.Empty<PowerUpItemView>();
                return;
            }

            powerUpItemViews = new PowerUpItemView[activePowerUps.Length];
            for (int i = 0; i < activePowerUps.Length; i++)
                powerUpItemViews[i] = PowerUpItemView.Init(itemPrefab, itemContainer, activePowerUps[i]);
        }

        
        private void Start()
        {
            containerInitialAnchoredPosition = new Vector2(0, heightWhenNoBannerAds);
            PrepareContainerForSpawnTween();
            containerTween?.Kill();
            containerTween = DOVirtual.DelayedCall(ContainerSpawnDelay, () => { containerTween = CreateContainerTween(false); });
        }

        private void Update()
        {
            if (powerUpItemViews == null) return;
            foreach(var uiBehavior in powerUpItemViews)
            {
                if (uiBehavior.Behavior.IsDirty)
                {
                    uiBehavior.UpdateData();
                }
            }
        }
        
        private void OnEnable()
        {
            Services.BoosterService.onUnlockBooster -= OnBoosterUnlocked;
            Services.BoosterService.onUnlockBooster += OnBoosterUnlocked;
            addBoosterEvent = new EventBinding<AddResourceVisualEvent>(OnAddBooster);
        }

        private void OnDisable()
        {
            if (Services.BoosterService)
            {
                Services.BoosterService.onUnlockBooster -= OnBoosterUnlocked;
            }
            EventBus<AddResourceVisualEvent>.Deregister(addBoosterEvent);
            containerTween?.Kill();
            containerTween = null;
        }

        private void OnDestroy()
        {
            selectionCloseButton.onClick.RemoveListener(OnSelectionCloseButtonClicked);
        }
        
        
        private void OnAddBooster(AddResourceVisualEvent eventData)
        {
            foreach (var powerUpItemView in powerUpItemViews)
            {
                powerUpItemView.OnAddBooster(eventData);
            }
        }
        
        private void OnBoosterUnlocked(GameResource resources)
        {
            foreach (var powerUpItemView in powerUpItemViews)
            {
                if (powerUpItemView.Behavior.Config.type == resources)
                {
                    powerUpItemView.UpdateData();
                    break;
                }
            }
        }

        public RectTransform GetItemRectTransform(PowerUpType powerUpType)
        {
            foreach (var powerUpItemView in powerUpItemViews)
            {
                if (powerUpItemView.Behavior.PowerUpConfig.Type == powerUpType)
                {
                    return powerUpItemView.IconRectTransform;
                }
            }
            return null;
        }
        public void OnUnlockNotify(BasePowerUpConfig powerUpConfig)
        {
            if (powerUpConfig == null || powerUpItemViews == null) return;
            ShowUnlockTutorialWhenNotifyQueueDrained(powerUpConfig).Forget();
        }

        private async UniTaskVoid ShowUnlockTutorialWhenNotifyQueueDrained(BasePowerUpConfig powerUpConfig)
        {
            NotifyPopupQueue notifyPopupQueue = NotifyPopupQueue.Instance;
            await UniTask.WaitUntil(
                () => !ObstacleUnlockNotifyPopup.IsFlowPending && notifyPopupQueue.IsIdle,
                cancellationToken: this.GetCancellationTokenOnDestroy());

            if (powerUpItemViews == null) return;
            ShowUnlockTutorial(powerUpConfig);
        }

        private void ShowUnlockTutorial(BasePowerUpConfig powerUpConfig)
        {
            tutorialPanel.gameObject.SetActive(true);
            selectionCloseButton.gameObject.SetActive(false);
            tutorialIconImage.sprite = powerUpConfig.Icon;
            tutorialDescriptionText.text = powerUpConfig.Description;
            // Instantiate power up item view on the tutorial panel arrow position
            foreach (var powerUpItemView in powerUpItemViews)
            {
                if (powerUpItemView.Behavior.PowerUpConfig.Type == powerUpConfig.Type)
                {
                    var item = PowerUpItemView.Init(
                        itemPrefab,
                        tutorialPanel.transform,
                        powerUpItemView.Behavior,
                        _ => OnPowerUpUnlockNotifyClick());
                    item.Activate(false);
                    item.SetLocked(false);
                    item.transform.position = powerUpItemView.transform.position;
                    arrow.position = powerUpItemView.transform.position;
                    var t  = arrow.localPosition;
                    t.y += 200f;
                    arrow.localPosition = t;
                    arrow.DOLocalMoveY(arrow.localPosition.y + 20f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    return;
                }
            }
        }
        private void OnPowerUpUnlockNotifyClick()
        {
            tutorialPanel.gameObject.SetActive(false);
        }
        
        
        public void OnLevelLoaded(int levelNumber, bool isTween = true)
        {
            for (int i = 0; i < powerUpItemViews.Length; i++)
            {
                if(powerUpItemViews[i].Behavior.IsActive())
                {
                    powerUpItemViews[i].Activate(isTween);
                    powerUpItemViews[i].OnLevelLoaded(levelNumber);
                }
                else
                {
                    powerUpItemViews[i].Hide();
                }

                powerUpItemViews[i].OnLevelStarted(levelNumber);
            }
        }
        
        
        public void OnLevelFinished()
        {
            for (int i = 0; i < powerUpItemViews.Length; i++)
            {
                if (powerUpItemViews[i].Behavior.IsActive())
                {
                    powerUpItemViews[i].Behavior.ResetBehavior();
                    powerUpItemViews[i].OnLevelFinished();
                    powerUpItemViews[i].UpdateData();
                }
            }
        }
        
        public void RedrawPanels()
        {
            for (int i = 0; i < powerUpItemViews.Length; i++)
            {
                if (powerUpItemViews[i])
                    powerUpItemViews[i].UpdateData();
            }
        }
        
        
        public void OnPowerUpUnselected(PowerUpBehavior selected)
        {
            selectionPanelObject.SetActive(false);
            containerTween?.Kill();
            containerTween = CreateContainerTween(false);
            UIController.GetPage<UIGame>().PlayFadeInTopButtonAnimation();
            selectionCloseButton.gameObject.SetActive(true);
        }

        public void OnPowerUpSelected(PowerUpBehavior selected)
        {
            var config = selected.PowerUpConfig;

            selectionPanelObject.SetActive(true);
            selectionIconImage.sprite = config.Icon;
            selectionDescriptionText.text = config.Description;
            
            UIController.GetPage<UIGame>().PlayFadeOutTopButtonAnimation();
            containerTween?.Kill();
            containerTween = CreateContainerTween(true);
        }
        
        public void ShowNotifyNotFoundTarget(string notifyWhenNotFoundTarget)
        {
            if (string.IsNullOrEmpty(notifyWhenNotFoundTarget)) return;

            if (!noTargetNotifyPresenter && textNoTarget)
            {
                noTargetNotifyPresenter = NotifyTextPresenter.GetOrAdd(textNoTarget);
            }

            noTargetNotifyPresenter?.Show(notifyWhenNotFoundTarget);
        }

        public void SetNavigationHighlight(PowerUpType type)
        {
            foreach (var view in powerUpItemViews)
            {
                bool isTarget = view.Behavior.PowerUpConfig.Type == type;
                view.SetNavigationHighlight(isTarget);
            }
        }

        public void ClearNavigationHighlights()
        {
            if (powerUpItemViews == null) return;
            foreach (var view in powerUpItemViews)
                view.SetNavigationHighlight(false);
        }

        public Vector3 GetPowerUpItemPosition(PowerUpType type)
        {
            foreach (var powerUpItemView in powerUpItemViews)
            {
                if (powerUpItemView.Behavior.PowerUpConfig.Type == type)
                {
                    return powerUpItemView.transform.position;
                }
            }
            
            return Vector3.zero;
        }
        private DG.Tweening.Tween CreateContainerTween(bool hide)
        {
            var targetAlpha = hide ? 0f : 1f;
            var sequence = DOTween.Sequence();

            if (botContainer)
            {
                botContainer.DOKill();
                var targetPosition = hide
                    ? new Vector2(containerInitialAnchoredPosition.x, -containerInitialAnchoredPosition.y)
                    : containerInitialAnchoredPosition;
                sequence.Join(botContainer.DOAnchorPos(targetPosition, ContainerTweenDuration).SetEase(Ease.OutQuad));
            }

            if (containerCanvasGroup)
            {
                containerCanvasGroup.DOKill();
                sequence.Join(containerCanvasGroup.DOFade(targetAlpha, ContainerTweenDuration).SetEase(Ease.OutQuad));
            }

            return sequence;
        }

        private void PrepareContainerForSpawnTween()
        {
            if (botContainer)
            {
                botContainer.DOKill();
                botContainer.anchoredPosition = new Vector2(containerInitialAnchoredPosition.x, -containerInitialAnchoredPosition.y);
            }

            if (containerCanvasGroup)
            {
                containerCanvasGroup.DOKill();
                containerCanvasGroup.alpha = 0f;
            }
        }


        private void OnSelectionCloseButtonClicked()
        {
            selectionPanelObject.gameObject.SetActive(false);
            PowerUpController.UnselectPowerUp();
        }
    }
}