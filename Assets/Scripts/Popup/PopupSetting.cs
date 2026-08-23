using System;
using Cheat;
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
    public Button RateBtn;
    public Button LanguageBtn;
    public Button RestoreBtn;
    public Button HomeBtn;

    public Button PrivacyPolicyBtn;
    public TMP_Text VersionText;

    public Button CheatBtn;

    public override void OnSetup()
    {
        base.OnSetup();
        CloseBtn.onClick.AddListener(Close);
        RateBtn.onClick.AddListener(Rate);
        LanguageBtn.onClick.AddListener(Language);
        RestoreBtn.onClick.AddListener(Restore);
        VersionText.text = "Ver " + Application.version;
        SetupCheatButton();
        HomeBtn.onClick.AddListener(OnHomeBtnClick);
        PrivacyPolicyBtn.onClick.AddListener(OnPrivacyPolicyBtnClick);
        HomeBtn.gameObject.SetActive(Services.TransitionService.GetCurrentGamePlacement() != GamePlacement.Home);
        RestoreBtn.gameObject.SetActive(Services.TransitionService.GetCurrentGamePlacement() == GamePlacement.Home);
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
        RateBtn.onClick.RemoveListener(Rate);
        LanguageBtn.onClick.RemoveListener(Language);
        RestoreBtn.onClick.RemoveListener(Restore);
        if (CheatBtn != null) CheatBtn.onClick.RemoveListener(OpenCheatPanel);
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

        if (Services.SpecialLevelService.IsActive)
        {
            Services.SpecialLevelService.Complete(GamePlacement.Home);
            return;
        }

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

    private void SetupCheatButton()
    {
        if (CheatBtn == null) return;

        // The hidden tap area stays wired for everyone: without remote access the taps only reveal
        // the device id, and hiding it would leave a tester unable to report the id to be allowlisted.
        CheatBtn.onClick.AddListener(OpenCheatPanel);
    }

    private void OpenCheatPanel()
    {
        // Editor: direct open. Device: 8 taps + password, and only on an allowlisted device.
        CheatManager.HandleCheatButtonTap();
    }

    private void Rate()
    {
        Close();
        PanelManager.Instance.OpenForget<PopupRate>();
    }

    private void Language()
    {
        Close();
        PanelManager.Instance.OpenForget<PopupLanguage>();
    }

    private void Restore()
    {
        Services.ShopService.RestorePurchase((items) => { 
            Debug.LogError("Restore success!");
            PopupToast.Create("Restore success!"); 
            });
    }
}
