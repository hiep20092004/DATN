using DG.Tweening;
using Popup;
using WaterFlow.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using WaterFlow.Enums;
using Ease = DG.Tweening.Ease;
using Tween = DG.Tweening.Tween;

namespace WaterFlow.Game
{
    public class LevelPanel : LevelPanelBase
    {
        [SerializeField] private TextMeshProUGUI levelText;

        private int currentLevelIndex;

        public override void Init()
        {
            currentLevelIndex = ActiveSession.Current.DisplayLevelIndex;
            UpdateLevelText();
        }

        private void UpdateLevelText()
        {
            levelText.text = LevelLabel.ForLevel(currentLevelIndex + 1);
        }

        public override void OnRefreshForNextLevel(int levelIndex)
        {
            currentLevelIndex = levelIndex;
            UpdateLevelText();
        }
    }
}
