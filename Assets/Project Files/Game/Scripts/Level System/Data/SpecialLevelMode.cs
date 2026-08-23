using WaterFlow.Enums;

namespace WaterFlow.Game
{
    public enum SpecialLevelMode
    {
        GoldMode = 0,
        RescueColor = 1,
        RescueBlock = 2
    }

    public static class SpecialLevelModeExtensions
    {
        public static GameMode ToGameMode(this SpecialLevelMode mode) => mode switch
        {
            SpecialLevelMode.GoldMode => GameMode.GoldMode,
            SpecialLevelMode.RescueColor => GameMode.RescueColor,
            SpecialLevelMode.RescueBlock => GameMode.RescueBlock,
            _ => GameMode.Classic,
        };
    }
}
