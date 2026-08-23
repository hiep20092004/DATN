using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using WaterFlow.Game;
using Sirenix.OdinInspector;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[Serializable]
public class ReviveReasonConfig
{
    [BoxGroup("Display", showLabel: false)]
    [HorizontalGroup("Display/Row", width: 64)]
    [PreviewField(60, ObjectFieldAlignment.Left), HideLabel]
    public Sprite icon;

    [VerticalGroup("Display/Row/Text")]
    [LabelWidth(90)]
    public string title;

    [VerticalGroup("Display/Row/Text")]
    [LabelWidth(90)]
    public string descriptionFormat;

    // Optional per-theme icon overrides, mirroring ObstacleUnlockEntry: when the active BlockTheme has an
    // override here the popup uses it, otherwise GetIcon falls back to the default icon above.
    [BoxGroup("Display", showLabel: false)]
    [TableList(AlwaysExpanded = true, ShowIndexLabels = false)]
    public List<ThemeIcon> themeIcons = new List<ThemeIcon>();

    [BoxGroup("Revive Bonus", showLabel: false)]
    [HorizontalGroup("Revive Bonus/Row")]
    [LabelText("Bonus Seconds"), LabelWidth(100)]
    [Tooltip("Extra time granted on revive. OutOfTime uses remote config at runtime.")]
    [MinValue(0)]
    public int continueSeconds = 0;

    [HorizontalGroup("Revive Bonus/Row")]
    [LabelText("Addition Text"), LabelWidth(90)]
    [Tooltip("Shown when Bonus Seconds is 0 (e.g. '+3' for extra turns).")]
    public string additionText = string.Empty;

    /// <summary>Theme-specific icon for the given theme, falling back to the default <see cref="icon"/>.</summary>
    public Sprite GetIcon(BlockTheme theme)
    {
        if (themeIcons != null)
        {
            foreach (ThemeIcon themeIcon in themeIcons)
            {
                if (themeIcon.Theme == theme && themeIcon.Icon)
                    return themeIcon.Icon;
            }
        }
        return icon;
    }

    [Serializable]
    public struct ThemeIcon
    {
        [VerticalGroup("Theme"), HideLabel]
        [SerializeField] private BlockTheme theme;

        [TableColumnWidth(70, resizable: false)]
        [PreviewField(50, ObjectFieldAlignment.Center), HideLabel]
        [SerializeField] private Sprite icon;

        public BlockTheme Theme => theme;
        public Sprite Icon => icon;
    }
}

public class PopupRevive : Panel
{
    public Button CloseButton;
    public Button CoinButton;
    public HoldButton HoldButton;
    public TMP_Text CoinRequiredText;
    public CanvasGroup PopupCanvasGroup;
    [SerializeField] private Image overlayDimImage;
    [SerializeField] private float peekFadeDuration = 0.12f;

    [Header("Dynamic Content")]
    public TMP_Text TitleText;
    public Image IconImage;
    public TMP_Text MoreTimeText;
    public TMP_Text DescriptionText;

    public SerializedDictionary<int, CurrencyData> coinRequiredDict;

    [Header("Revive Config By Reason")]
    public LoseReasonConfig loseReasonConfig;

    [Header("Default Config (Fallback)")]
    public ReviveReasonConfig defaultConfig;

    private ReviveReasonConfig currentConfig;
    private LoseReason currentLoseReason;
    private Color _overlayDimColor = Color.black;
    private bool _overlayRaycastTarget;
    private bool _isPeeking;

#if UNITY_EDITOR
    [Header("Editor Test")]
    [SerializeField] private LoseReason testLoseReason;

    [Button("PreviewLoseReason")]
    private void PreviewLoseReason()
    {
        UpdateContentByLoseReason(testLoseReason);
    }
#endif

    public override void OnSetup()
    {
        base.OnSetup();

        currentLoseReason = GameController.Instance.CurrentLoseReason;

        CloseButton.onClick.AddListener(OnQuitButtonClick);
        CoinButton.onClick.AddListener(OnCoinButtonClick);
        if (HoldButton == null)
            HoldButton = GetComponentInChildren<HoldButton>(true);

        if (overlayDimImage == null)
            overlayDimImage = GetComponent<Image>();

        if (overlayDimImage != null)
        {
            _overlayDimColor = overlayDimImage.color;
            _overlayRaycastTarget = overlayDimImage.raycastTarget;
        }

        if (HoldButton != null)
        {
            HoldButton.onDown += OnHoldButtonDown;
            HoldButton.onUp += OnHoldButtonUp;
        }
        else
            Debug.LogWarning("[PopupRevive] HoldButton is not assigned.");
        int revivedTime = Mathf.Clamp(GameController.Instance.RevivedTime, 0, coinRequiredDict.Count - 1);
        CoinRequiredText.text = coinRequiredDict[revivedTime].value.ToString();

        UpdateContentByLoseReason();
    }



//Run haptic when revive popup open
    private void PlayLoseHaptic()
    {
        global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.Lose);
    }

    private void OnDestroy()
    {
        KillPeekTweens();
        if (HoldButton != null)
        {
            HoldButton.onDown -= OnHoldButtonDown;
            HoldButton.onUp -= OnHoldButtonUp;
        }
    }

    private void OnHoldButtonDown()
    {
        if (_isPeeking)
            return;

        _isPeeking = true;
        KillPeekTweens();
        FadeOutPeekVisuals();
    }

    private void OnHoldButtonUp()
    {
        if (!_isPeeking)
            return;

        _isPeeking = false;
        FadeInPeekVisuals();
    }

    private void FadeOutPeekVisuals()
    {
        if (PopupCanvasGroup != null)
        {
            PopupCanvasGroup.interactable = false;
            PopupCanvasGroup.blocksRaycasts = false;

            if (peekFadeDuration <= 0f)
                PopupCanvasGroup.alpha = 0f;
            else
                PopupCanvasGroup.DOFade(0f, peekFadeDuration).SetEase(Ease.OutQuad);
        }

        if (overlayDimImage != null)
        {
            overlayDimImage.raycastTarget = false;

            if (peekFadeDuration <= 0f)
            {
                var c = overlayDimImage.color;
                c.a = 0f;
                overlayDimImage.color = c;
            }
            else
            {
                overlayDimImage.DOFade(0f, peekFadeDuration).SetEase(Ease.OutQuad);
            }
        }
    }

    private void FadeInPeekVisuals()
    {
        KillPeekTweens();

        var sequence = DOTween.Sequence();

        if (PopupCanvasGroup != null)
        {
            if (peekFadeDuration <= 0f)
                PopupCanvasGroup.alpha = 1f;
            else
                sequence.Join(PopupCanvasGroup.DOFade(1f, peekFadeDuration).SetEase(Ease.OutQuad));
        }

        if (overlayDimImage != null)
        {
            if (peekFadeDuration <= 0f)
                overlayDimImage.color = _overlayDimColor;
            else
                sequence.Join(overlayDimImage.DOFade(_overlayDimColor.a, peekFadeDuration).SetEase(Ease.OutQuad));
        }

        if (sequence.Duration() <= 0f)
        {
            ApplyPeekInteractableState(true);
            return;
        }

        sequence.OnComplete(() => ApplyPeekInteractableState(true));
    }

    private void ApplyPeekInteractableState(bool visible)
    {
        if (PopupCanvasGroup != null)
        {
            PopupCanvasGroup.interactable = visible;
            PopupCanvasGroup.blocksRaycasts = visible;
        }

        if (overlayDimImage != null)
            overlayDimImage.raycastTarget = visible && _overlayRaycastTarget;
    }

    private void KillPeekTweens()
    {
        PopupCanvasGroup?.DOKill();
        overlayDimImage?.DOKill();
    }

    private void RestorePeekVisuals()
    {
        KillPeekTweens();
        _isPeeking = false;

        if (PopupCanvasGroup != null)
        {
            PopupCanvasGroup.alpha = 1f;
            PopupCanvasGroup.interactable = true;
            PopupCanvasGroup.blocksRaycasts = true;
        }

        if (overlayDimImage != null)
        {
            overlayDimImage.color = _overlayDimColor;
            overlayDimImage.raycastTarget = _overlayRaycastTarget;
        }
    }

    private void UpdateContentByLoseReason()
    {
        var reason = currentLoseReason;
        UpdateContentByLoseReason(reason);
    }

    private void UpdateContentByLoseReason(LoseReason reason)
    {
        var config = GetReviveConfig(reason);
        currentConfig = config;
        int seconds = GetContinueSeconds(reason);

        UpdateIcon(config);
        UpdateText();

        if (seconds > 0)
        {
            MoreTimeText.text = $"+{seconds}s";
            MoreTimeText.gameObject.SetActive(true);
        }
        else if (!string.IsNullOrEmpty(config.additionText))
        {
            MoreTimeText.text = config.additionText;
            MoreTimeText.gameObject.SetActive(true);
        }
        else
        {
            MoreTimeText.gameObject.SetActive(false);
        }
    }

    private void UpdateIcon(ReviveReasonConfig config)
    {
        BlockTheme theme = LevelController.Instance.BlockTheme;
        Sprite icon = config.GetIcon(theme);
        IconImage.sprite = icon;
        // IconImage.SetNativeSize();
        IconImage.gameObject.SetActive(icon);
    }

    private void UpdateText()
    {
        int seconds = GetContinueSeconds(currentLoseReason);
        TitleText.text = currentConfig.title;
        DescriptionText.text = currentConfig.descriptionFormat?.Replace("{[value]}", seconds.ToString());
    }

    private ReviveReasonConfig GetReviveConfig(LoseReason reason)
    {
        if (loseReasonConfig != null && loseReasonConfig.entries != null)
        {
            for (int i = 0; i < loseReasonConfig.entries.Count; i++)
            {
                var entry = loseReasonConfig.entries[i];
                if (entry != null && entry.reason == reason && entry.config != null)
                {
                    return entry.config;
                }
            }
        }
        Debug.LogError($"[PopupRevive] Missing LoseReason config for {reason}");
        return defaultConfig ?? new ReviveReasonConfig();
    }

    private int GetContinueSeconds(LoseReason reason)
    {
        return GetReviveConfig(reason).continueSeconds;
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
        PlayLoseHaptic();
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void OnCloseCompleted()
    {
        RestorePeekVisuals();
        base.OnCloseCompleted();
        CloseButton.onClick.RemoveListener(OnQuitButtonClick);
        CoinButton.onClick.RemoveListener(OnCoinButtonClick);
        if (HoldButton != null)
        {
            HoldButton.onDown -= OnHoldButtonDown;
            HoldButton.onUp -= OnHoldButtonUp;
        }
    }

    private void OnQuitButtonClick()
    {
        Close();
        PanelManager.Instance.OpenForget<PopupLose>();
    }

    private void OnCoinButtonClick()
    {
        int revivedTime = Mathf.Clamp(GameController.Instance.RevivedTime, 0, coinRequiredDict.Count - 1);
        CurrencyData currencyData = coinRequiredDict[revivedTime];
        Services.InventoryService.TryBuyResourceByCurrency(currencyData, () =>
        {
            Revive();
            //GameController.Instance.RevivedTime++;
        });
    }

    public void Revive()
    {
        int seconds = GetContinueSeconds(GameController.Instance.CurrentLoseReason);
        GameController.Instance.Revive(seconds);
        if (panelCanvasGroup)
        {
            Close();
        }
    }

    public void Revive(int seconds)
    {
        GameController.Instance.Revive(seconds);
        if (panelCanvasGroup)
        {
            Close();
        }
    }
}
