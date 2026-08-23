using System.Collections.Generic;
using WaterFlow.Enums;

namespace WaterFlow.Game
{
    public sealed class WinConditionFactory : IWinConditionFactory
    {
        private readonly Dictionary<GameMode, IWinCondition> conditions;
        private readonly ClassicWinCondition classicFallback = new();

        public WinConditionFactory()
        {
            conditions = new Dictionary<GameMode, IWinCondition>
            {
                { GameMode.Classic, new ClassicWinCondition() },
                { GameMode.GoldMode, new GoldModeWinCondition() },
            };
        }

        public IWinCondition Create(GameMode mode)
            => conditions.GetValueOrDefault(mode, classicFallback);
    }
}
