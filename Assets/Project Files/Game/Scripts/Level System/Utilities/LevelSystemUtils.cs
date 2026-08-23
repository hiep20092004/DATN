namespace WaterFlow.Game
{
    public static class LevelSystemUtils
    {
        public const string LevelSystemDataFolder = "Assets/Project Files/Level System";
        public const string SourceLevelsFolder = LevelSystemDataFolder + "/Editor/Levels";
        public const string SpecialLevelsSourceFolder = LevelSystemDataFolder + "/Editor/SpecialLevels";
        public const string LegacySourceLevelsFolder = LevelSystemDataFolder + "/Levels";

        /// <summary>Generated active level <see cref="LevelData"/> assets included in player / Addressables builds.</summary>
        public const string ActiveLevelsFolder = LevelSystemDataFolder + "/ActiveLevels";
        public const string SpecialActiveLevelsFolder = ActiveLevelsFolder + "/Special";

        public const string LevelDataAssetExtension = ".asset";

        public static string GetActiveLevelSlotAssetPath(int levelNumber1Based)
        {
            return $"{ActiveLevelsFolder}/Level {levelNumber1Based:D3}{LevelDataAssetExtension}";
        }

        public static string GetSourceLevelAssetPath(int levelNumber1Based)
        {
            return $"{SourceLevelsFolder}/Level {levelNumber1Based:D3}{LevelDataAssetExtension}";
        }

        public static string GetActiveSpecialLevelSlotAssetPath(SpecialLevelMode mode, int slotNumber1Based)
        {
            return $"{SpecialActiveLevelsFolder}/{mode} {slotNumber1Based:D3}{LevelDataAssetExtension}";
        }
    }
}
