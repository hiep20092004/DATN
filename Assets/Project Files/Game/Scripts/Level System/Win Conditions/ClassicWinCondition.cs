using WaterFlow.Enums;

namespace WaterFlow.Game
{
    public sealed class ClassicWinCondition : IWinCondition
    {
        public GameMode Mode => GameMode.Classic;

        public WinEvalResult Evaluate(LevelRepresentation rep)
        {
            if (rep == null || rep.ActiveBlocks == null)
                return WinEvalResult.Ongoing;

            if (rep.HasPendingBlockSpawn)
                return WinEvalResult.Ongoing;

            foreach (LevelBlockBehavior block in rep.ActiveBlocks)
            {
                if (block && block.CanCollectBlock())
                    return WinEvalResult.Ongoing;
            }

            return WinEvalResult.Win;
        }

        public WinEvalResult OnTimeExpired(LevelRepresentation rep) => WinEvalResult.Lose;
    }
}
