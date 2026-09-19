using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Game;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupWin : Panel
{
    public Button CloseButton;
    public Button NextButton;
    public TMP_Text LevelTMP;
    public SkeletonGraphic CoinAnim;
    public TMP_Text CoinAmount;
    public ScalePerCharacter ScaleText;

    // Both exits finish the level (Next -> next level, Close -> main menu), so the reward must be
    // guarded against a double grant.
    private static bool _hasAwarded;

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

        // The coin flourish is optional — this project ships the win screen without Spine art — so the
        // reward and button wiring below must not depend on it.
        var coinAnimation = CoinAnim ? CoinAnim.GetAnimationState() : null;
        if (coinAnimation != null)
        {
            coinAnimation.SetAnimation(0, "Appear", false).Complete +=
                track => coinAnimation.SetAnimation(0, "Idle", true);
        }

        Services.AudioService.PlaySound(AudioId.Win);

        var coinReward = GameWinFlowService.GetWinReward().quantity;

        int completedLevel = ActiveSession.Current.Save.DisplayLevelIndex;
        string levelTitle = LevelLabel.ForCompletedLevel(completedLevel).ToUpper();

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

        // "Play next level?" -> Yes: the level index was already advanced on win, so reloading the
        // gameplay scene starts the next level.
        CompleteWinFlow(GamePlacement.Game);
    }

    private void OnCloseButtonClick()
    {
        Close();

        // "Play next level?" -> No: back to the main menu.
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
