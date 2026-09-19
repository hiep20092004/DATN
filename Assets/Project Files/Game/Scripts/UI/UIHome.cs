using WaterFlow.Enums;
using WaterFlow.Core;
using WaterFlow.Framework.UIModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Game
{
    /// <summary>
    /// Home screen page: background, currency bar, reached-level label, settings, and the button that starts a run.
    /// </summary>
    public class UIHome : UIPage
    {
        [BoxGroup("References", "References")]
        [SerializeField] RectTransform safeAreaRectTransform;

        [SerializeField] Image background;
        [SerializeField] TextMeshProUGUI levelText;

        public Button PlayBtn;
        public Button SettingBtn;

        public void Awake()
        {
            PlayBtn.onClick.AddListener(OnPlayButtonClicked);
            SettingBtn.onClick.AddListener(OnSettingButtonClicked);
        }

        public override void Init()
        {
            NotchSaveArea.RegisterRectTransform(safeAreaRectTransform);
        }

        public override void PlayShowAnimation()
        {
            PlayBtn.interactable = true;

            // Refreshed on every show, not in Init: the player returns here after finishing a level,
            // so the progress shown must reflect the save as it stands now.
            levelText.text = LevelLabel.Current();

            UIController.OnPageOpened(this);
        }

        public override void PlayHideAnimation()
        {
            UIController.OnPageClosed(this);
        }

        private void OnPlayButtonClicked()
        {
            // TransitionService already blocks re-entrant switches, but the button has to stop
            // responding too or a double tap reads as an unresponsive screen during the scene load.
            PlayBtn.interactable = false;

            Services.TransitionService.SwitchScene(GamePlacement.Game);
        }

        private void OnSettingButtonClicked()
        {
            PanelManager.Instance.OpenForget<PopupSetting>();
        }
    }
}
