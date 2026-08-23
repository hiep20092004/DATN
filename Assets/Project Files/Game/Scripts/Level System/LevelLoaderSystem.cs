using UnityEngine;

namespace WaterFlow.Game
{
    public enum LoaderMode
    {
        Editor,
        Local
    }

    /// <summary>
    /// Resolves which <see cref="LevelData"/> a slot index maps to. Three sources, in priority order:
    /// the editor database (play-mode authoring), a pending special level, then the baked local levels.
    /// </summary>
    public class LevelLoaderSystem
    {
        public const string SOURCE_SO_EDITOR = "SO_EDITOR";
        public const string SOURCE_LOCAL_SO = "LOCAL_SO";
        public const string SOURCE_SPECIAL_SO = "SPECIAL_SO";

        public const string LoaderModeEditorPrefsKey = "WaterFlow.LevelLoaderSystem.LoaderMode";

        private readonly LevelDatabase _database;

        public string LastLoadSource { get; private set; } = SOURCE_LOCAL_SO;

        public LevelLoaderSystem(LevelDatabase database = null)
        {
            _database = database;
        }

#if UNITY_EDITOR
        public static LoaderMode GetLoaderMode()
        {
            return (LoaderMode)UnityEditor.EditorPrefs.GetInt(LoaderModeEditorPrefsKey, (int)LoaderMode.Editor);
        }

        public static void SetLoaderMode(LoaderMode mode)
        {
            UnityEditor.EditorPrefs.SetInt(LoaderModeEditorPrefsKey, (int)mode);
        }
#endif

        public LevelData LoadLevel(int levelIndex)
        {
#if UNITY_EDITOR
            if (LevelDatabase.EditorPlayModeLevelOverrideSlot == levelIndex &&
                TryLoadFromEditorDatabase(levelIndex, out LevelData overrideLevel, forceEditorDatabase: true))
            {
                EnsureInactiveWhenLoadingMainLevel(overrideLevel);
                return overrideLevel;
            }
#endif

            if (Services.SpecialLevelService.TryConsumePendingLevel(out SpecialLevelData specialLevelData) &&
                specialLevelData)
            {
                LastLoadSource = SOURCE_SPECIAL_SO;
                return specialLevelData;
            }

#if UNITY_EDITOR
            if (TryLoadFromEditorDatabase(levelIndex, out LevelData editorLevel))
            {
                EnsureInactiveWhenLoadingMainLevel(editorLevel);
                return editorLevel;
            }
#endif
            LevelData runtimeLevel = ResolveRuntimeLevelData(levelIndex);
            EnsureInactiveWhenLoadingMainLevel(runtimeLevel);
            return runtimeLevel;
        }

        private static void EnsureInactiveWhenLoadingMainLevel(LevelData levelData)
        {
            if (levelData is SpecialLevelData)
                return;

            Services.SpecialLevelService?.DeactivateForMainLevelLoad();
        }

        public LevelType GetLevelType(int levelIndex)
        {
#if UNITY_EDITOR
            bool forceEditorDatabase = LevelDatabase.EditorPlayModeLevelOverrideSlot == levelIndex;
            if (TryLoadFromEditorDatabase(levelIndex, out LevelData editorLevel, forceEditorDatabase) && editorLevel)
                return editorLevel.Type;
#endif
            // Metadata-only query: LevelDatabase answers from baked infos without loading the
            // LevelData asset (a cold Addressables load here froze Home for seconds).
            if (_database)
                return _database.GetLevelType(levelIndex);

            LevelData levelData = ResolveRuntimeLevelData(levelIndex);
            return levelData ? levelData.Type : LevelType.Normal;
        }

#if UNITY_EDITOR
        private bool TryLoadFromEditorDatabase(int levelIndex, out LevelData levelData, bool forceEditorDatabase = false)
        {
            levelData = null;
            if (!forceEditorDatabase && GetLoaderMode() != LoaderMode.Editor)
                return false;

            if (!_database)
            {
                Debug.LogError("[LevelLoaderSystem] Editor loader mode requires LevelDatabase.");
                return false;
            }

            LastLoadSource = SOURCE_SO_EDITOR;
            levelData = _database.GetLevelDirectly(levelIndex);
            if (levelData is SpecialLevelData editorSpecialLevel)
            {
                Services.SpecialLevelService.Begin(editorSpecialLevel);
                LastLoadSource = SOURCE_SPECIAL_SO;
            }

            return true;
        }
#endif

        private LevelData ResolveRuntimeLevelData(int levelIndex)
        {
            LastLoadSource = SOURCE_LOCAL_SO;
            return LevelActiveLevelLoader.Load(levelIndex);
        }
    }
}
