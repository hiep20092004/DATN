namespace WaterFlow.Framework.Systems.UserData
{
    /// <summary>
    /// Protects persisted main progression from accidental rollback while allowing explicit debug tooling.
    /// </summary>
    public static class UserLevelProgressPolicy
    {
        public static bool CanSave(int savedLevel, int newLevel, bool isDebugBuild,
            bool isCheatAvailable)
        {
            return isDebugBuild || isCheatAvailable || newLevel >= savedLevel;
        }
    }
}
