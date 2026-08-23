using WaterFlow.Enums;

namespace WaterFlow.Game
{
    public interface IWinConditionFactory
    {
        IWinCondition Create(GameMode mode);
    }
}
