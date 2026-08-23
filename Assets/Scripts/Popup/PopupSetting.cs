using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Game;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.UIModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PopupSetting : Panel
{
    public Button CloseBtn;
    public Button HomeBtn;

    public Button PrivacyPolicyBtn;
    public TMP_Text VersionText;


    public override void OnSetup()
    {
        base.OnSetup();
        CloseBtn.onClick.AddListener(Close);
        VersionText.text = "Ver " + Application.version;
        HomeBtn.onClick.AddListener(OnHomeBtnClick);
        PrivacyPolicyBtn.onClick.AddListener(OnPrivacyPolicyBtnClick);
        HomeBtn.gameObject.SetActive(Services.TransitionService.GetCurrentGamePlacement() != GamePlacement.Home);
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
        CloseBtn.onClick.RemoveListener(Close);
        HomeBtn.onClick.RemoveListener(OnHomeBtnClick);
        PrivacyPolicyBtn.onClick.RemoveListener(OnPrivacyPolicyBtnClick);
    }

    private void OnPrivacyPolicyBtnClick()
    {
        Application.OpenURL("https://sites.google.com/view/privacy-policy-sonat-game");
    }

    private void OnHomeBtnClick()
    {
        Close();

        if (!LevelController.Instance.LevelStarted)
        {
            ToHome();
            return;
        }

        ToHome();
    }

    private void ToHome()
    {
        SaveController.Save(true);
        Services.TransitionService.SwitchScene(GamePlacement.Home);
    }

}
