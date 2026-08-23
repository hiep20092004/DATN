using WaterFlow.Enums;

namespace WaterFlow.Game
{
    public enum WinEvalResult
    {
        Ongoing,
        Win,
        Lose
    }

    public interface IWinCondition
    {
        GameMode Mode { get; }
        WinEvalResult Evaluate(LevelRepresentation rep);
        WinEvalResult OnTimeExpired(LevelRepresentation rep);
    }
}
