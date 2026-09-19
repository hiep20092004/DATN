using System;
using Cysharp.Threading.Tasks;
using Lofelt.NiceVibrations;
using WaterFlow.Game;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Systems.AudioManagement;
using WaterFlow.Framework.UIModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PopupSetting : Panel
{
    /// <summary>
    /// One on/off row of the settings popup. The prefab art has no <see cref="Toggle"/>: state is shown by
    /// overlaying a "slash" graphic on the icon, so the row is a plain button plus that overlay.
    /// </summary>
    [Serializable]
    public class SettingRow
    {
        public Button button;

        [Tooltip("Crossed-out overlay, shown while the setting is off.")]
        public GameObject slash;

        public void Refresh(bool isOn)
        {
            if (slash != null)
                slash.SetActive(!isOn);
        }
    }

    public Button CloseBtn;
    public Button HomeBtn;

    [Header("Toggles")]
    public SettingRow MusicRow;
    public SettingRow SoundRow;
    public SettingRow VibrateRow;

    public override void OnSetup()
    {
        base.OnSetup();
        CloseBtn.onClick.AddListener(Close);
        HomeBtn.onClick.AddListener(OnHomeBtnClick);

        // Screen flow: settings opened from gameplay asks "keep playing?" — Close resumes the level,
        // Home leaves it for the main menu. On the main menu itself there is nowhere to go home to.
        bool isHomeScene = Services.TransitionService.GetCurrentGamePlacement() == GamePlacement.Home;
        HomeBtn.gameObject.SetActive(!isHomeScene);

        SetupRow(MusicRow, IsMusicOn, OnMusicToggled);
        SetupRow(SoundRow, IsSoundOn, OnSoundToggled);
        SetupRow(VibrateRow, () => HapticController.hapticsEnabled, OnVibrateToggled);
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
        MusicRow?.button?.onClick.RemoveAllListeners();
        SoundRow?.button?.onClick.RemoveAllListeners();
        VibrateRow?.button?.onClick.RemoveAllListeners();
    }

    private void SetupRow(SettingRow row, Func<bool> getState, Action<bool> setState)
    {
        if (row?.button == null)
        {
            Debug.LogWarning("[PopupSetting] A setting row has no button assigned.");
            return;
        }

        row.Refresh(getState());
        row.button.onClick.AddListener(() =>
        {
            bool newState = !getState();
            setState(newState);
            row.Refresh(newState);
        });
    }

    private static bool IsMusicOn() => Services.AudioService.GetVolume(AudioTracks.Music) != 0f;

    private static bool IsSoundOn() => Services.AudioService.GetVolume(AudioTracks.Sound) != 0f;

    private static void OnMusicToggled(bool isOn)
    {
        Services.AudioService.SetVolume(AudioTracks.Music, isOn ? 1f : 0f);

        // Volume alone would leave a muted track running, so the source is stopped as well.
        if (isOn)
            Services.AudioService.ResumeMusic();
        else
            Services.AudioService.StopMusic();
    }

    private static void OnSoundToggled(bool isOn)
    {
        Services.AudioService.SetVolume(AudioTracks.Sound, isOn ? 1f : 0f);
        if (!isOn)
            Services.AudioService.StopSound();
    }

    private static void OnVibrateToggled(bool isOn)
    {
        HapticController.hapticsEnabled = isOn;
    }

    private void OnHomeBtnClick()
    {
        Close();

        // Gameplay tweens outlive the scene load unless killed, same guard GameController.Replay uses.
        DG.Tweening.DOTween.KillAll();
        SaveController.Save(true);
        Services.TransitionService.SwitchScene(GamePlacement.Home);
    }

}
