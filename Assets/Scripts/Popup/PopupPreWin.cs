using System;
using System.Collections;
using WaterFlow.Game;
using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.Helper;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Utils;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PopupPreWin : Panel
{
    public SkeletonGraphic WinAnim;
    public GameObject Effect;
    public Button skipPreWin;

    //player ref for delay call haptic
    private IntDataPref delayCallHaptic;
    
    [SerializeField] private float skipDelay = 0.75f;
    [SerializeField] private int levelCanSkip = 5;
    
    private bool _isSkipped;
    private bool _canSkip;
    
    
    public override void OnSetup()
    {
        base.OnSetup();
        _isSkipped = false;
        _canSkip = false;
        StartCoroutine(EnableSkipAfterDelay());
        delayCallHaptic = new IntDataPref("PopupPreWin_DelayCallHaptic", 525);

        WinAnim.GetAnimationState().SetAnimation(0, "PreWin_In", false).Complete += (track) =>
        {
            if (_isSkipped) return;
            WinAnim.GetAnimationState().SetAnimation(0, "PreWin_Out", false).Complete += (track) =>
            {
                if (_isSkipped) return;
                Close();
            };
        };
        Services.AudioService.PlaySound(AudioId.Win);
        FrameworkUtils.DelayCall(delayCallHaptic.Value / 1000f, () => global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.Win));
    }

    private void OnEnable()
    {
        skipPreWin.gameObject.SetActive(false);
    }

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
        skipPreWin.onClick.AddListener(Skip);
    }

    public override void OnOpenCompleted()
    {
        base.OnOpenCompleted();
        Effect?.SetActive(true);
    }

    private IEnumerator EnableSkipAfterDelay()
    {
        yield return new WaitForSeconds(skipDelay);
        _canSkip = ActiveSession.Current.DisplayLevelIndex>= levelCanSkip;
        if (_canSkip)
        {
            skipPreWin.gameObject.SetActive(true);
        }
    }

    private void Skip()
    {
        if (!_canSkip || _isSkipped) return;
        _isSkipped = true;

        Services.AudioService.StopSound();
        WinAnim.GetAnimationState().ClearTracks();

        Close();
    }

    public override void Close()
    {
        base.Close();
        PanelManager.Instance.OpenForget<PopupWin>();
        skipPreWin.onClick.RemoveAllListeners();
    }

    protected override void OnCloseCompleted()
    {
        base.OnCloseCompleted();
    }
}