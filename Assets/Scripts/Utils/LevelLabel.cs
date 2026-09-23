using WaterFlow.Game;

/// <summary>
/// Single place that turns a level number into the label shown to the player,
/// so HUD, win and lose screens can never drift apart.
/// </summary>
public static class LevelLabel
{
    public static string Current()
    {
        return ForLevel(ActiveSession.Current.Save.DisplayLevelIndex + 1);
    }

    public static string ForLevel(int level) => $"Cấp {level}";

    public static string ForCompletedLevel(int completedLevel) => ForLevel(completedLevel);
}
