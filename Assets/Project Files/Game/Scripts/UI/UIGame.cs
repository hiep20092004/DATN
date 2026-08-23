using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Game
{
    public class UIGame : UIPage
    {
        [BoxGroup("References", "References")]
        [SerializeField] RectTransform safeAreaRectTransform;

        [SerializeField] RectTransform itemsOverlayContainer;
        
        [BoxGroup("Top Panel")]
        [SerializeField] RectTransform levelPanelContainer;

        [BoxGroup("Top Panel")]
        [SerializeField] LevelPanel normalLevelPanelPrefab;

        [BoxGroup("Top Panel")]
        [SerializeField] GoldModeLevelPanel goldModeLevelPanelPrefab;
        
        [BoxGroup("Message Box")]
        [SerializeField] MessageBox messageBox;
        
        public Button SettingBtn;
        public Button ReplayBtn;
        public Button SkipSpecialBtn;
        
        private LevelPanelBase levelPanel;
        private RectTransform levelPanelRect;
        private RectTransform settingButtonRect;
        private RectTransform replayButtonRect;
        private CanvasGroup levelPanelCanvasGroup;
        private CanvasGroup settingButtonCanvasGroup;
        private CanvasGroup replayButtonCanvasGroup;
        private Vector2 levelPanelInitialPosition;
        private Vector2 settingButtonInitialPosition;
        private Vector2 replayButtonInitialPosition;
        private bool isTopPanelHidden;
        private DG.Tweening.Tween topPanelSpawnTween;

        private Camera itemOverlaysWorldCamera;
        private Camera itemOverlaysUiCamera;
        private bool itemOverlaysCameraCacheReady;

        private const float TopPanelHideOffset = 220f;
        private const float TopPanelTweenDuration = 0.25f;
        
        public MessageBox MessageBox => messageBox;
        public Transform SafeArea => safeAreaRectTransform;
        public TimerVisualiser TimerVisualiser => levelPanel?.TimeVisualiser;
        public LevelPanelBase LevelPanel => levelPanel;
        public RectTransform ItemOverlays => itemsOverlayContainer;
        
        public void Awake()
        {
            SettingBtn.onClick.AddListener(OnSettingButtonClicked);
            ReplayBtn.onClick.AddListener(OnReplayButtonClicked);
            if (SkipSpecialBtn)
                SkipSpecialBtn.onClick.AddListener(OnSkipSpecialButtonClicked);

            levelPanel = SpawnLevelPanel();
            levelPanel.Init();
            
            levelPanelRect = levelPanel.transform as RectTransform;
            settingButtonRect = SettingBtn.transform as RectTransform;
            replayButtonRect = ReplayBtn.transform as RectTransform;

            levelPanelInitialPosition = levelPanelRect ? levelPanelRect.anchoredPosition : Vector2.zero;
            settingButtonInitialPosition = settingButtonRect ? settingButtonRect.anchoredPosition : Vector2.zero;
            replayButtonInitialPosition = replayButtonRect ? replayButtonRect.anchoredPosition : Vector2.zero;

            levelPanelCanvasGroup = GetOrAddCanvasGroup(levelPanel.gameObject);
            settingButtonCanvasGroup = GetOrAddCanvasGroup(SettingBtn.gameObject);
            replayButtonCanvasGroup = GetOrAddCanvasGroup(ReplayBtn.gameObject);

            RefreshSpecialLevelButtons();
        }

        private void Start()
        {
            PrepareTopPanelForSpawnTweenFromBelow();
            topPanelSpawnTween = DOVirtual.DelayedCall(1f, PlayFadeInTopButtonAnimation);
        }

        private void OnDisable()
        {
            topPanelSpawnTween?.Kill();
            topPanelSpawnTween = null;
        }

        private void OnDestroy()
        {
            SettingBtn.onClick.RemoveListener(OnSettingButtonClicked);
            ReplayBtn.onClick.RemoveListener(OnReplayButtonClicked);
            if (SkipSpecialBtn)
                SkipSpecialBtn.onClick.RemoveListener(OnSkipSpecialButtonClicked);
        }

        public override void Init()
        {
            NotchSaveArea.RegisterRectTransform(safeAreaRectTransform);
            
            messageBox.Init();
            RefreshSpecialLevelButtons();
        }

        public override void PlayHideAnimation()
        {
            if (TimerVisualiser)
                TimerVisualiser.Hide();

            UIController.OnPageClosed(this);
        }

        public override void PlayShowAnimation()
        {
            if (TimerVisualiser)
                TimerVisualiser.Show(LevelController.Instance.GameplayTimer);
            UIController.OnPageOpened(this);
        }


        /// <summary>
        /// Converts a world position into a local point inside ItemOverlays, for effects (e.g. particles) that need
        /// to fly toward or spawn under this UI container. Caches the world/UI cameras used for the conversion
        /// since callers (time capsule, boosters, etc.) invoke this every time an effect reaches the UI.
        /// </summary>
        public Vector3 GetItemOverlaysLocalPoint(Vector3 worldPosition)
        {
            EnsureItemOverlaysCameraCache();
            return FrameworkUtils.WorldPointToLocalRectPoint(worldPosition, itemsOverlayContainer, itemOverlaysWorldCamera, itemOverlaysUiCamera);
        }

        /// <summary>
        /// Same as <see cref="GetItemOverlaysLocalPoint"/> but for a source that is itself a UI element (e.g. the
        /// timer text), not a 3D world object. Uses the UI camera (or none, for Screen Space Overlay) instead of
        /// the gameplay camera, since <paramref name="uiSource"/>.position is already in canvas space.
        /// </summary>
        public Vector3 GetItemOverlaysLocalPointFromUI(Transform uiSource)
        {
            EnsureItemOverlaysCameraCache();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(itemOverlaysUiCamera, uiSource.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(itemsOverlayContainer, screenPoint, itemOverlaysUiCamera, out Vector2 localPoint);
            return localPoint;
        }

        private void EnsureItemOverlaysCameraCache()
        {
            if (itemOverlaysCameraCacheReady)
                return;

            itemOverlaysWorldCamera = Camera.main;
            Canvas mainCanvas = UIController.MainCanvas;
            itemOverlaysUiCamera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
            itemOverlaysCameraCacheReady = true;
        }

        public void RefreshForNextLevel(int levelIndex)
        {
            levelPanel.RefreshForNextLevel(levelIndex);
            RefreshSpecialLevelButtons();
        }

        public void PlayFadeOutTopButtonAnimation()
        {
            if (isTopPanelHidden) return;
            isTopPanelHidden = true;

            TweenTopPanel(
                levelPanelRect, levelPanelCanvasGroup, levelPanelInitialPosition + Vector2.up * TopPanelHideOffset, 0f);
            TweenTopPanel(
                settingButtonRect, settingButtonCanvasGroup, settingButtonInitialPosition + Vector2.up * TopPanelHideOffset, 0f);
            TweenTopPanel(
                replayButtonRect, replayButtonCanvasGroup, replayButtonInitialPosition + Vector2.up * TopPanelHideOffset, 0f);
        }

        public void PlayFadeInTopButtonAnimation()
        {
            if (!isTopPanelHidden) return;
            isTopPanelHidden = false;

            TweenTopPanel(levelPanelRect, levelPanelCanvasGroup, levelPanelInitialPosition, 1f);
            TweenTopPanel(settingButtonRect, settingButtonCanvasGroup, settingButtonInitialPosition, 1f);
            TweenTopPanel(replayButtonRect, replayButtonCanvasGroup, replayButtonInitialPosition, 1f);
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (!canvasGroup)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private static void TweenTopPanel(
            RectTransform rectTransform,
            CanvasGroup canvasGroup,
            Vector2 targetPosition,
            float targetAlpha)
        {
            if (!rectTransform || !canvasGroup) return;

            rectTransform.DOKill();
            canvasGroup.DOKill();

            rectTransform.DOAnchorPos(targetPosition, TopPanelTweenDuration).SetEase(DG.Tweening.Ease.OutQuad);
            canvasGroup.DOFade(targetAlpha, TopPanelTweenDuration).SetEase(DG.Tweening.Ease.OutQuad);
        }

        private void PrepareTopPanelForSpawnTweenFromBelow()
        {
            isTopPanelHidden = true;
            SetTopPanelImmediate(levelPanelRect, levelPanelCanvasGroup, levelPanelInitialPosition + Vector2.up * TopPanelHideOffset, 0f);
            SetTopPanelImmediate(settingButtonRect, settingButtonCanvasGroup, settingButtonInitialPosition + Vector2.up * TopPanelHideOffset, 0f);
            SetTopPanelImmediate(replayButtonRect, replayButtonCanvasGroup, replayButtonInitialPosition + Vector2.up * TopPanelHideOffset, 0f);
        }

        private static void SetTopPanelImmediate(
            RectTransform rectTransform,
            CanvasGroup canvasGroup,
            Vector2 targetPosition,
            float targetAlpha)
        {
            if (!rectTransform || !canvasGroup) return;

            rectTransform.DOKill();
            canvasGroup.DOKill();
            rectTransform.anchoredPosition = targetPosition;
            canvasGroup.alpha = targetAlpha;
        }
        
        private void OnReplayButtonClicked()
        {
            if (!LevelController.Instance.LevelStarted)
            {
                var data = new PopupPreBooster.Data(GamePlacement.Game);
                data.NotResetJuiceStep();
                data.SetGoHome(false);
                Retry();
                return;
            }

            UIData uiData = new UIData();
            uiData.Add("OpenPlacement", OpenPlacement.Retry);
            PanelManager.Instance.OpenPanelByNameAsync<PopupReduceLive>("PopupReduceLive", uiData).Forget();
        }

        private void Retry()
        {
            GameController.Instance.Replay();
        }

        private void OnSettingButtonClicked()
        {
            PanelManager.Instance.OpenForget<PopupSetting>();
        }

        private void OnSkipSpecialButtonClicked()
        {
            Services.SpecialLevelService.Skip();
        }

        private LevelPanelBase SpawnLevelPanel()
        {
            Transform container = levelPanelContainer ? levelPanelContainer : transform;

            SpecialLevelService specialLevelService = Services.SpecialLevelService;
            bool isGoldMode = specialLevelService != null
                && specialLevelService.TryGetActiveOrPendingMode(out SpecialLevelMode mode)
                && mode == SpecialLevelMode.GoldMode;

            LevelPanelBase prefab = isGoldMode
                ? (LevelPanelBase)goldModeLevelPanelPrefab
                : normalLevelPanelPrefab;

            return Instantiate(prefab, container);
        }

        private void RefreshSpecialLevelButtons()
        {
            bool isSpecialActive = Services.SpecialLevelService.IsActive;

            if (SkipSpecialBtn)
                SkipSpecialBtn.gameObject.SetActive(isSpecialActive);

            if (ReplayBtn)
                ReplayBtn.gameObject.SetActive(!isSpecialActive);
        }
    }
}