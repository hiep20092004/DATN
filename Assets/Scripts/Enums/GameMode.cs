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

    public enum GamePlacement : byte // Must same with Scene Build List
    {
        Loading,
        Home,
        Game
    }

    /// <summary>
    /// Maps <see cref="GamePlacement"/> to names in File → Build Profiles (scene asset names).
    /// Avoid <c>enum.ToString()</c> for load
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

        /// <summary>
        /// Reverse of <see cref="ToBuildSceneName"/>. Needed because the boot scene is loaded by
        /// GameLoading rather than the transition service, so the active scene — not a cached
        /// field — is the only reliable source for "where am I right now".
        /// </summary>
        public static GamePlacement FromBuildSceneName(this string sceneName) => sceneName switch
        {
            "Home" => GamePlacement.Home,
            "Game" => GamePlacement.Game,
            _ => GamePlacement.Loading,
        };
    }

    public enum NavigationType : byte
    {
        None = 0,
        Home,
        Shop,
        Leaderboard,
        Profile_Avatar = 10,
        Profile_Frame = 11,
        Profile_Badge = 19,
        Leaderboard_WeeklyContest = 12,
        Leaderboard_Players = 13,
        Leaderboard_Teams = 14,
        Leaderboard_Players_World = 15,
        Leaderboard_Players_Local = 16,
        Journey = 17,
        CardCollection = 18,

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
