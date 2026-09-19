using System;
using System.Collections;
using WaterFlow.Framework.Systems.GameDataManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems.TimeManagement
{
    [CreateAssetMenu(fileName = "DefaultTimeService", menuName = "WaterFlow Services/Time Service")]
    public class DefaultTimeService : TimeService, IServiceInitialize
    {
        [SerializeField] private Service<DataService> dataService = new();

        private DateTime lastFeatTime;
        private float timeFeat;

        private bool UsingLocalTime
        {
            get => dataService.Instance.GetInt("USING_LOCAL_TIME", 0) == 1;
            set => dataService.Instance.SetInt("USING_LOCAL_TIME", value ? 1 : 0);
        }

        public void Initialize()
        {
            ResetTimeBase();
        }

        public bool IsTimeTrusted => true;

        public void SetUsingLocalTime(bool value)
        {
            UsingLocalTime = value;
        }

        public void ForceUpdateNetTime()
        {
            UsingLocalTime = false;
            GetCurrentTime();
        }

        public void SetCheatDateTime(DateTime dateTime)
        {
            lastFeatTime = dateTime;
            timeFeat = Time.realtimeSinceStartup;
            Debug.Log($"[CheatTime] Clock overridden to {dateTime:yyyy-MM-dd HH:mm:ss}");
        }

        public void ResetCheatDateTime()
        {
            ResetTimeBase();
            Debug.Log("[CheatTime] Clock reset to real time");
        }

        public override long GetUnixTimeSeconds(bool force = true)
        {
            var now = GetCurrentTime(force);
            if (now == DateTime.MinValue) return 0;
            return ((DateTimeOffset)now).ToUnixTimeSeconds();
        }

        public override DateTime GetCurrentTime(bool force = true)
        {
            if (UsingLocalTime)
            {
                return DateTime.Now;
            }

            // ServicesManager.Resolve() initializes services in list order, and UserDataService.CheckTimeUser()
            // reads the clock from inside its own Initialize(). Without this guard a service ordered earlier
            // reads lastFeatTime while still default (DateTime.MinValue), which throws on the DateTimeOffset
            // cast in GetUnixTimeSeconds under any positive UTC offset.
            if (lastFeatTime == default)
            {
                ResetTimeBase();
            }

            var dateTime = lastFeatTime.AddSeconds(Time.realtimeSinceStartup - timeFeat);
            return dateTime;
        }

        private void ResetTimeBase()
        {
            lastFeatTime = DateTime.Now;
            timeFeat = Time.realtimeSinceStartup;
        }

        public override int GetDaysPassed(DateTime timeStart, DateTime timeEnd)
        {
            DateTime dateOnlyStart = timeStart.Date;
            DateTime dateOnlyEnd = timeEnd.Date;

            TimeSpan difference = dateOnlyEnd - dateOnlyStart;

            return (int)difference.TotalDays;
        }

        public override IEnumerator DoActionRealtime(long sec, Action<long> action, bool force = true)
        {
            var timeFinihsed = GetUnixTimeSeconds(force) + sec;
            var timeRemain = sec;
            while (timeRemain > 0)
            {
                action?.Invoke(timeRemain);
                yield return new WaitForSecondsRealtime(1);
                timeRemain = timeFinihsed - GetUnixTimeSeconds(force);
            }

            action?.Invoke(timeRemain);
        }
    }
}
