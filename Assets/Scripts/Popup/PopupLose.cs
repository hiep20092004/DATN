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

public class PopupLose : Panel
{
    public Button CloseButton;
    public Button NextButton;
    public TMP_Text LevelTMP;
    public SkeletonGraphic CoinAnim;
    public TMP_Text CoinAmount;
    public ScalePerCharacter ScaleText;

    private int _coinReward = 20;

    private bool _hasAwarded = false;

    public SerializedDictionary<LevelType, int> coinByDif;

    public override void OnSetup()
    {
        base.OnSetup();
        CoinAmount.text = "20";
        CoinAmount.gameObject.SetActive(false);
        CoinAmount.transform.DOScale(Vector3.one * 2, 0f);

        CloseButton.onClick.AddListener(OnCloseButtonClick);
        NextButton.onClick.AddListener(OnNextButtonClick);

        LevelTMP.text = LevelLabel.Current().ToUpper();
        
        CoinAnim.GetAnimationState().SetAnimation(0, "Idle", true);

        LevelRepresentation levelRepresentation = LevelController.Instance.LevelRepresentation;
        var levelType = levelRepresentation.LevelData.Type;
        _coinReward = coinByDif[levelType];

        CoinAmount.text = _coinReward.ToString();
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
        PlayCoinAmount().Forget();
        //ScaleText.Play().Forget();
    }

    private async UniTask PlayCoinAmount()
    {
        await UniTask.WaitForSeconds(0.2f);

        // Check if the popup and components still exist after the delay
        if (this == null || CoinAmount == null || !gameObject.activeInHierarchy)
            return;

        CoinAmount.gameObject.SetActive(true);
        await CoinAmount.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
        CloseButton.onClick.RemoveListener(OnCloseButtonClick);
        NextButton.onClick.RemoveListener(OnNextButtonClick);
    }

    private void OnNextButtonClick()
    {
        GameController.Instance.Replay();
    }

    private void OnCloseButtonClick()
    {
        Services.TransitionService.SwitchScene(GamePlacement.Home);
    }
}
