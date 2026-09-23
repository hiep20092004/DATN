using WaterFlow.Game;
using WaterFlow.Core;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using TMPro;
using UnityEngine.UI;

public class PopupLose : Panel
{
    public Button CloseButton;
    public Button NextButton;
    public TMP_Text LevelTMP;
    public ScalePerCharacter ScaleText;

    public override void OnSetup()
    {
        base.OnSetup();

        CloseButton.onClick.AddListener(OnCloseButtonClick);
        NextButton.onClick.AddListener(OnNextButtonClick);

        LevelTMP.text = LevelLabel.Current().ToUpper();
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
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

    // "Retry the level?" -> Yes.
    private void OnNextButtonClick()
    {
        GameController.Instance.Replay();
    }

    // "Retry the level?" -> No: back to the main menu.
    private void OnCloseButtonClick()
    {
        Close();

        // Gameplay tweens outlive the scene load unless killed, same guard GameController.Replay uses.
        DG.Tweening.DOTween.KillAll();
        SaveController.Save(true);
        Services.TransitionService.SwitchScene(GamePlacement.Home);
    }
}
