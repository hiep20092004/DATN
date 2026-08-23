namespace WaterFlow.Game
{
    public interface ISpecialLevelRuleSet
    {
        bool ShouldDeductLives { get; }
        bool ShouldModifyStreak { get; }
        bool ShouldAdvanceProgression { get; }
        bool ShouldGrantNormalWinReward { get; }
        bool ShouldRaiseLevelEndTurnEvent { get; }
    }

    public sealed class DefaultSpecialLevelRuleSet : ISpecialLevelRuleSet
    {
        public bool ShouldDeductLives => false;
        public bool ShouldModifyStreak => false;
        public bool ShouldAdvanceProgression => false;
        public bool ShouldGrantNormalWinReward => false;
        public bool ShouldRaiseLevelEndTurnEvent => false;
    }
}
