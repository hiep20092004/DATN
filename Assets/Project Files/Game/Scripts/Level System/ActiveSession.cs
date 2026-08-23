using System.Collections.Generic;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    [StaticUnload]
    public class ActiveSession
    {
        private const int RECENT_RANDOM_LEVEL_HISTORY = 5;

        private readonly LevelSave levelSave;

        public LevelSave Save => levelSave;

        public int DisplayLevelIndex => levelSave.DisplayLevelIndex;
        public int LevelIndex => levelSave.RealLevelIndex;
        public int MaxReachedLevelIndex => levelSave.MaxReachedLevelIndex;

        public bool IsPlayingRandomLevel => levelSave.IsPlayingRandomLevel;

        public bool FirstStart => levelSave.FirstStart;
        public bool FirstTimeCompletedLevel => levelSave.CompletedLevelIndex < levelSave.DisplayLevelIndex;

        private LevelData levelData;
        public LevelData LevelData => levelData;

        private static ActiveSession currentSession;

        private ActiveSession()
        {
            levelSave = SaveController.GetSaveObject<LevelSave>();

#if UNITY_EDITOR
            if (LevelDatabase.IsEditorPlayModeLevelOverrideActive)
                return;
#endif

            int storedUserLevel = Services.UserDataService.GetLevel();
            bool levelSaveChanged = levelSave.TryReconcileProgress(storedUserLevel,
                out int reconciledUserLevel);

            if (storedUserLevel != reconciledUserLevel)
                Services.UserDataService.SaveLevel(reconciledUserLevel);

            if (levelSaveChanged)
            {
                Debug.LogWarning(
                    $"[LevelProgress][Recovery] Reconciled gameplay save with user level. " +
                    $"UserLevel={storedUserLevel}, RecoveredLevel={reconciledUserLevel}.");
                SaveController.Save(forceSave: true);
            }
        }

        public static ActiveSession Current
        {
            get
            {
                if (currentSession == null)
                    currentSession = new ActiveSession();

                return currentSession;
            }
        }

        public void OnLevelStarted(LevelData levelData)
        {
            this.levelData = levelData;

            levelSave.FirstStart = false;

            if (levelData)
                levelSave.DisplayLevelType = (int)levelData.Type;
        }

        /// <summary>
        /// Level type for the level the player will play next (remote bundle, local SO, or randomizer mapping).
        /// </summary>
        public LevelType GetDisplayLevelType()
        {
            int realLevelIndex = GetLevelIndex(DisplayLevelIndex);
            return ResolveLevelType(realLevelIndex);
        }

        private static LevelDatabase ResolveLevelDatabase()
        {
            LevelDatabase database = Services.GameplayConfig?.LevelDatabase;
            if (database)
                return database;

            LevelController levelController = LevelController.Instance;
            return levelController != null ? levelController.LevelDatabase : null;
        }

        private static LevelType ResolveLevelType(int realLevelIndex)
        {
            var loader = new LevelLoaderSystem(ResolveLevelDatabase());
            return loader.GetLevelType(realLevelIndex);
        }

        public void OnLevelCompleted()
        {
            levelSave.IsPlayingRandomLevel = false;

            if (FirstTimeCompletedLevel)
            {
                levelSave.CompletedLevelIndex = levelSave.RealLevelIndex;
            }
        }

        public static void SetEditorLevelIndex(int levelIndex)
        {
            if (Application.isPlaying)
            {
                ActiveSession activeSession = Current;
                activeSession.SetLevelIndex(levelIndex);
                ClearRandomLevelSession(activeSession.levelSave, levelIndex);

                return;
            }

#if UNITY_EDITOR
            GlobalSave globalSave = SaveController.GetGlobalSave();

            LevelSave editorLevelSave = globalSave.GetSaveObject<LevelSave>();
            editorLevelSave.DisplayLevelIndex = levelIndex;
            editorLevelSave.RealLevelIndex = levelIndex;
            editorLevelSave.LastPlayerLevelIndex = levelIndex;
            editorLevelSave.IsPlayingRandomLevel = false;
            editorLevelSave.FirstStart = true;
            ClearRandomLevelSession(editorLevelSave, levelIndex);

            if (levelIndex > editorLevelSave.MaxReachedLevelIndex)
            {
                editorLevelSave.MaxReachedLevelIndex = levelIndex;
            }

            SaveController.SaveCustom(globalSave);
#endif
        }

        private static void ClearRandomLevelSession(LevelSave levelSave, int levelIndex)
        {
            levelSave.IsPlayingRandomLevel = false;
            levelSave.RealLevelIndex = levelIndex;
            levelSave.LastPlayerLevelIndex = -1;
        }

        public void SetLevelIndex(int levelIndex)
        {
            if (levelSave.DisplayLevelIndex != levelIndex)
                levelSave.IsPlayingRandomLevel = false;

            levelSave.DisplayLevelIndex = levelIndex;
            levelSave.DisplayLevelType = (int)GetDisplayLevelType();
            Services.UserDataService.SaveLevel(levelIndex + 1);

            levelSave.FirstStart = true;

            if (levelIndex > levelSave.MaxReachedLevelIndex)
            {
                levelSave.MaxReachedLevelIndex = levelIndex;
            }
        }

        public void RerollDisplayLevel()
        {
            levelSave.IsPlayingRandomLevel = false;
            levelSave.FirstStart = true;
            levelSave.DisplayLevelType = (int)GetDisplayLevelType();
        }

        public int GetLevelIndex(int levelIndex)
        {
#if UNITY_EDITOR
            if (LevelDatabase.IsEditorPlayModeLevelOverrideActive)
            {
                ClearRandomLevelSession(levelSave, levelIndex);
                return levelIndex;
            }
#endif

            int realLevelIndex;
            if (levelSave.IsPlayingRandomLevel && levelIndex == levelSave.DisplayLevelIndex && levelSave.RealLevelIndex != -1)
            {
                realLevelIndex = levelSave.RealLevelIndex;
            }
            else
            {
                LevelDatabase levelDatabase = ResolveLevelDatabase();
                if (!levelDatabase)
                    return levelIndex;

                bool forceRandom = false;

                realLevelIndex = levelDatabase
                    .GetRandomLevelIndex(levelIndex, levelSave.RecentRandomLevelIndexes, forceRandom);

                levelSave.RealLevelIndex = realLevelIndex;
                PushRecentLevelIndex(realLevelIndex);

                if (forceRandom || realLevelIndex != levelIndex)
                {
                    levelSave.IsPlayingRandomLevel = true;
                }
            }

            return realLevelIndex;
        }

        /// <summary>
        /// Level number of the asset actually being played (<c>Level 038</c> → 38). Differs from the
        /// display level whenever the randomizer picked the level — endless play past the last
        /// authored level.
        /// </summary>
        public static int ResolveRealLevelNumber(int specialLevelNumber)
        {
            return Current.LevelIndex + 1;
        }

        private void PushRecentLevelIndex(int realLevelIndex)
        {
            List<int> recentLevelIndexes = levelSave.RecentRandomLevelIndexes;
            if (recentLevelIndexes == null)
            {
                recentLevelIndexes = new List<int>();
                levelSave.RecentRandomLevelIndexes = recentLevelIndexes;
            }

            recentLevelIndexes.Remove(realLevelIndex);
            recentLevelIndexes.Add(realLevelIndex);

            while (recentLevelIndexes.Count > RECENT_RANDOM_LEVEL_HISTORY)
            {
                recentLevelIndexes.RemoveAt(0);
            }

            levelSave.LastPlayerLevelIndex = realLevelIndex;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            currentSession = null;
        }
        
        private static void UnloadStatic()
        {
            currentSession = null;
        }
    }
}
