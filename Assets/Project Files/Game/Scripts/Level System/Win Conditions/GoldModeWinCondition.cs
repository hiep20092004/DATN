using WaterFlow.Enums;

namespace WaterFlow.Game
{
    public sealed class GoldModeWinCondition : IWinCondition
    {
        public GameMode Mode => GameMode.GoldMode;

        public WinEvalResult Evaluate(LevelRepresentation rep)
        {
            if (rep == null || rep.ActiveBlocks == null)
                return WinEvalResult.Ongoing;

            bool hasGoldBlock = false;
            foreach (LevelBlockBehavior block in rep.ActiveBlocks)
            {
                if (!block || !IsGoldBlock(block))
                    continue;

                hasGoldBlock = true;
                if (block.CanCollectBlock())
                    return WinEvalResult.Ongoing;
            }

            return hasGoldBlock ? WinEvalResult.Win : WinEvalResult.Ongoing;
        }

        public WinEvalResult OnTimeExpired(LevelRepresentation rep) => WinEvalResult.Win;

        private static bool IsGoldBlock(LevelBlockBehavior block)
            => block.GetActiveBlockColor() == BlockColor.Gold ||
               block.GetSecondaryBlockColor() == BlockColor.Gold;
    }
}
