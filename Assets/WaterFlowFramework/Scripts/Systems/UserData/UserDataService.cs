using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Helper;
using WaterFlow.Framework.Systems.EventBus;
using WaterFlow.Framework.Systems.GameDataManagement;
using WaterFlow.Framework.Systems.TimeManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems.UserData
{
    [CreateAssetMenu(fileName = "UserDataService", menuName = "WaterFlow Services/User Data Service")]
    public class UserDataService : ServiceSo, IServiceInitialize
    {
        private const string UserLevelKey = "UserLevel";
        private const string UserGameModeKey = "UserLevel";

        /// <summary>
        /// Game-side cheat tooling sets this to allow explicit level rollback in release builds.
        /// </summary>
        public static bool CheatEnabled;

#if DEBUG
        private const bool IS_DEBUG_BUILD = true;
#else
        private const bool IS_DEBUG_BUILD = false;
#endif
        private LongDataPref firstTimeOpen;
        private LongDataPref lastTimeOpen;
        private IntDataPref lastDay;
        private IntDataPref sessionToday;
        private IntDataPref sessionTotal;
        private int userDay;
        private int levelPlayToday;
        private Dictionary<GameMode, int> levelByGameMode = new Dictionary<GameMode, int>();


        [BoxGroup("SERVICES")] [SerializeField]
        private Service<DataService> dataService = new();

        private Service<TimeService> timeService = new();

        public void Initialize()
        {
            int currentLevel = GetLevel(GameMode.Classic);
            levelByGameMode.TryAdd(GameMode.Classic, currentLevel);
            levelPlayToday = 0;
            userDay = 0;
            var eventBinding = new EventBinding<LevelEndedEvent>(OnLevelEnded);
            CheckTimeUser();
            
            // Sync level to SDK on startup for remote config keys
        }


        protected virtual void CheckTimeUser()
        {
            firstTimeOpen = new LongDataPref("FirstTimeOpen");
            lastTimeOpen = new LongDataPref("LastTimeOpen");
            lastDay = new IntDataPref("LastDay");
            sessionToday = new IntDataPref("SessionToday");
            sessionTotal = new IntDataPref("SessionTotal");

            if (firstTimeOpen.Value == 0)
            {
                firstTimeOpen.Value = timeService.Instance.GetUnixTimeSeconds(false);
            }

            DateTime now = timeService.Instance.GetCurrentTime(false);
            DateTime firstDay = DateTimeOffset.FromUnixTimeSeconds(firstTimeOpen.Value).ToLocalTime().DateTime;
            userDay = timeService.Instance.GetDaysPassed(firstDay, now);


            if (lastDay.Value != userDay)
            {
                lastDay.Value = userDay;
                levelPlayToday = 0;
                sessionToday.Value = 0;
            }
            else
            {
                sessionToday.Value++;
            }

            sessionTotal.Value++;
            lastTimeOpen.Value = timeService.Instance.GetUnixTimeSeconds(false);
        }

        private void OnLevelEnded(LevelEndedEvent eventData)
        {
            if (!eventData.success) return;

            if (eventData.suppressProgression)
            {
                levelPlayToday++;
                return;
            }

            LevelUp(eventData.gameMode);
        }

        public int GetLevel(GameMode mode = GameMode.Classic)
        {
            if (!levelByGameMode.TryGetValue(mode, out int level))
            {
                level = dataService.Instance.GetInt($"{UserLevelKey}_{mode}", 1);
            }

            return level;
        }


        public void SaveLevel(int _level, GameMode mode = GameMode.Classic)
        {
            int savedLevel = GetLevel(mode);
            if (!UserLevelProgressPolicy.CanSave(savedLevel, _level,
                    IS_DEBUG_BUILD, CheatEnabled))
            {
                Debug.LogWarning(
                    $"[UserData][Level] Ignored level rollback in production. " +
                    $"Mode={mode}, SavedLevel={savedLevel}, NewLevel={_level}.");
                return;
            }

            if (levelByGameMode.ContainsKey(mode))
            {
                levelByGameMode[mode] = _level;
            }
            else
            {
                levelByGameMode.Add(mode, _level);
            }
            dataService.Instance.SetInt($"{UserLevelKey}_{mode}", _level);
            
            // Sync level to SDK for remote config keys (level_start_show_*, by_level_*)
        }

        public void LevelUp(GameMode mode = GameMode.Classic)
        {
            var newLevel = GetLevel(mode) + 1;
            SaveLevel(newLevel, mode);
            levelPlayToday++;
        }

        public int UserDay => userDay;

        public int LevelPlayToday => levelPlayToday;
        public int SessionToday => sessionToday.Value;
        public long FirstTimeOpen => firstTimeOpen.Value;
        public int SessionTotal => sessionTotal.Value;

        /// <summary>
        /// Cheat: set <see cref="UserDay"/> by back-calculating <c>FirstTimeOpen</c> from current time.
        /// </summary>
        public void CheatSetUserDay(int targetUserDay)
        {
            EnsureTimePrefs();

            targetUserDay = Mathf.Max(0, targetUserDay);
            DateTime now = timeService.Instance.GetCurrentTime(false);
            DateTime firstDay = now.Date.AddDays(-targetUserDay);
            firstTimeOpen.Value = new DateTimeOffset(firstDay).ToUnixTimeSeconds();

            userDay = timeService.Instance.GetDaysPassed(firstDay, now);


            if (lastDay.Value != userDay)
            {
                lastDay.Value = userDay;
                levelPlayToday = 0;
                sessionToday.Value = 0;
            }

            Debug.Log($"[UserDataService] Cheat: UserDay={userDay}, FirstTimeOpen={firstTimeOpen.Value} ({firstDay:yyyy-MM-dd})");
        }

        private void EnsureTimePrefs()
        {
            firstTimeOpen ??= new LongDataPref("FirstTimeOpen");
            lastTimeOpen ??= new LongDataPref("LastTimeOpen");
            lastDay ??= new IntDataPref("LastDay");
            sessionToday ??= new IntDataPref("SessionToday");
            sessionTotal ??= new IntDataPref("SessionTotal");
        }
    }
}
