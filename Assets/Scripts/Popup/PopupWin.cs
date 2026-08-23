using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Game;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PopupWin : Panel
{
    [Serializable]
    public class LevelTypeWinConfig
    {
        public Sprite Bg;
        public Sprite HeaderBg;
    }

    public Button CloseButton;
    public Button NextButton;
    public TMP_Text LevelTMP;
    public SkeletonGraphic CoinAnim;
    public TMP_Text CoinAmount;
    public ScalePerCharacter ScaleText;
    public Image Bg;
    public Image HeaderBg;

    // Close and Next both finish the level, so the reward must be guarded against a double grant.
    private static bool _hasAwarded;

    public SerializedDictionary<LevelType, LevelTypeWinConfig> winConfig = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _hasAwarded = false;
    }

    public override void OnSetup()
    {
        base.OnSetup();
        _hasAwarded = false;
        CoinAmount.gameObject.SetActive(false);
        CoinAmount.transform.DOScale(Vector3.one * 2, 0f);

        CloseButton.onClick.AddListener(OnCloseButtonClick);
        NextButton.onClick.AddListener(OnNextButtonClick);

        LevelType levelType = LevelController.Instance.LevelRepresentation.LevelData.Type;

        CoinAnim.GetAnimationState().SetAnimation(0, "Appear", false).Complete += (track) =>
        {
            CoinAnim.GetAnimationState().SetAnimation(0, "Idle", true);
        };
        Services.AudioService.PlaySound(AudioId.Win);

        var coinReward = GameWinFlowService.GetWinReward().quantity;

        int completedLevel = ActiveSession.Current.Save.DisplayLevelIndex;
        string levelTitle = LevelLabel.ForCompletedLevel(completedLevel).ToUpper();

        var levelConfig = winConfig.GetValueOrDefault(levelType);
        if (levelConfig != null)
        {
            Bg.sprite = levelConfig.Bg;
            HeaderBg.sprite = levelConfig.HeaderBg;
        }

        LevelTMP.text = levelTitle;
        CoinAmount.text = coinReward.ToString();
        CloseButton.transform.DOScale(Vector3.zero, 0f);
        NextButton.transform.DOScale(Vector3.zero, 0f);
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
        PlayCoinAmount().Forget();
        ScaleText.Play().Forget();
    }

    private async UniTask PlayCoinAmount()
    {
        await UniTask.WaitForSeconds(0.2f);

        // The popup can be closed during the delay.
        if (this == null || CoinAmount == null || !gameObject.activeInHierarchy)
            return;

        CoinAmount.gameObject.SetActive(true);
        await CoinAmount.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        _ = CloseButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        _ = NextButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
        CloseButton.onClick.RemoveListener(OnCloseButtonClick);
        NextButton.onClick.RemoveListener(OnNextButtonClick);
    }

    private void OnNextButtonClick()
    {
        Close();
        CompleteWinFlow(GamePlacement.Game);
    }

    private void OnCloseButtonClick()
    {
        Close();
        CompleteWinFlow(GamePlacement.Home);
    }

    private static void CompleteWinFlow(GamePlacement gamePlacement)
    {
        if (_hasAwarded) return;
        _hasAwarded = true;

        GameWinFlowService.GrantWinReward();
        GameWinFlowService.SaveAndSwitchScene(gamePlacement);
    }
}
