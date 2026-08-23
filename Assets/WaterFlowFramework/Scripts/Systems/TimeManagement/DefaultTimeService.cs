using System;
using System.Collections;
using System.Globalization;
using System.Net;
using WaterFlow.Framework.Systems.GameDataManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems.TimeManagement
{
    [CreateAssetMenu(fileName = "DefaultTimeService", menuName = "WaterFlow Services/Time Service")]
    public class DefaultTimeService : TimeService, IServiceInitialize
    {
        [SerializeField] private Service<DataService> dataService = new();

        //private static bool usingLocalTime = true;
        private bool getNetTimeSuccess;
        private DateTime lastFeatTime;
        private float timeFeat;

        private bool UsingLocalTime
        {
            get => dataService.Instance.GetInt("USING_LOCAL_TIME", 0) == 1;
            set => dataService.Instance.SetInt("USING_LOCAL_TIME", value ? 1 : 0);
        }

        public void Initialize()
        {
            getNetTimeSuccess = false;
        }

        /// <summary>
        /// Giờ hiện tại có đáng tin để làm mốc nghiệp vụ (mở/đóng mùa, hết hạn event) hay không.
        /// False khi chưa lấy được giờ từ net — lúc đó GetCurrentTime fallback về DateTime.Now, tức giờ máy
        /// và có thể bị người chơi chỉnh. True khi đang chủ động dùng giờ local (cheat/QA).
        /// </summary>
        public bool IsTimeTrusted => getNetTimeSuccess || UsingLocalTime;

        public void SetUsingLocalTime(bool value)
        {
            UsingLocalTime = value;
        }

        public void ForceUpdateNetTime()
        {
            getNetTimeSuccess = false;
            UsingLocalTime = false;
            GetCurrentTime();
        }

        public void SetCheatDateTime(DateTime dateTime)
        {
            lastFeatTime = dateTime;
            timeFeat = Time.realtimeSinceStartup;
            getNetTimeSuccess = true;
            Debug.Log($"[CheatTime] Clock overridden to {dateTime:yyyy-MM-dd HH:mm:ss}");
        }

        public void ResetCheatDateTime()
        {
            getNetTimeSuccess = false;
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
            if (!getNetTimeSuccess)
            {
                lastFeatTime = LoadNetTime(force);
                if (force && Application.internetReachability != NetworkReachability.NotReachable)
                {
                    getNetTimeSuccess = true;
                    timeFeat = Time.realtimeSinceStartup;
                }

                return lastFeatTime;
            }
            
            if (UsingLocalTime)
            {
                return DateTime.Now;
            }

            var dateTime = lastFeatTime.AddSeconds(Time.realtimeSinceStartup - timeFeat);
            return dateTime;
        }
        
        public override int GetDaysPassed(DateTime timeStart, DateTime timeEnd)
        {
            DateTime dateOnlyStart = timeStart.Date;
            DateTime dateOnlyEnd = timeEnd.Date;

            TimeSpan difference = dateOnlyEnd - dateOnlyStart;

            return (int)difference.TotalDays;
        }

        private DateTime LoadNetTime(bool force)
        {
            if (!UsingLocalTime)
                try
                {
                    using (var response = WebRequest.Create("https://www.google.com").GetResponse())
                        //string todaysDates =  response.Headers["date"];
                    {
                        return DateTime.ParseExact(response.Headers["date"],
                            "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                            CultureInfo.InvariantCulture.DateTimeFormat,
                            DateTimeStyles.AssumeUniversal);
                    }
                }
                catch (WebException)
                {
                    if (force)
                        return DateTime.Now; //In case something goes wrong. 
                    return DateTime.Now;
                }

            return DateTime.Now;
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