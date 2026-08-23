using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Top-of-screen panel shown for gold-mode special levels. Instead of the difficulty
    /// banner used by <see cref="LevelPanel"/>, it surfaces the live reward (gold collected
    /// this play) alongside the remaining time and the level number.
    /// </summary>
    public class GoldModeLevelPanel : LevelPanelBase
    {
        [SerializeField] private AnimatedCountTextView rewardView;

        public override void Init()
        {
            RefreshReward(true);
        }

        public override void OnRefreshForNextLevel(int levelIndex)
        {
            RefreshReward(true);
        }

        public void AnimateReward(int totalGoldAfterCollection, int addedGold = 0)
        {
            if (rewardView)
            {
                rewardView.AnimateAdd(addedGold, totalGoldAfterCollection);
                return;
            }

            Debug.LogWarning($"{nameof(GoldModeLevelPanel)} missing {nameof(rewardView)} reference.", this);
        }

        private void RefreshReward(bool force)
        {
            int gold = LevelRuntimeData.Current.GoldEarned;
            if (!rewardView) return;

            if (!force && gold == rewardView.CurrentValue)
                return;

            rewardView.SetValue(gold);
        }
    }
}
