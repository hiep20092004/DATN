namespace WaterFlow.Enums
{
    public enum GameMode : byte
    {
        Classic
    }

    public enum GameState : byte
    {
        Loading = 0,
        Playing,
        Paused,
        GameOver,
        Tool
    }

    public enum GamePlacement : byte // Must match the order of the Scene Build List
    {
        Loading,
        Home,
        Game
    }

    /// <summary>
    /// Maps <see cref="GamePlacement"/> to names in File → Build Profiles (scene asset names).
    /// Avoid <c>enum.ToString()</c> for scene loads — it allocates on every call.
    /// </summary>
    public static class GamePlacementSceneNames
    {
        public static string ToBuildSceneName(this GamePlacement placement) => placement switch
        {
            GamePlacement.Loading => "Loading",
            GamePlacement.Home => "Home",
            GamePlacement.Game => "Game",
            _ => "Loading",
        };
    }

    public enum NavigationType : byte
    {
        None = 0,
        Home,
        Shop,
        Leaderboard,
    }

    public enum StuckType : byte
    {
        Stuck = 0,
        OutOfMove = 1
    }

    public enum LevelDifficulty : byte
    {
        Normal = 0,
        Hard = 1,
        SuperHard = 2,
    }
}
