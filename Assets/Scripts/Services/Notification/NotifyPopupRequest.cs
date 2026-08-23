using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Framework.UIModule;

namespace WaterFlow.Game
{
    public class NotifyPopupRequest
    {
        public readonly string Key;
        public readonly int Priority;
        public readonly Func<UniTask<View>> OpenAsync;

        public NotifyPopupRequest(Func<UniTask<View>> openAsync, string key = null, int priority = 0)
        {
            OpenAsync = openAsync;
            Key = key;
            Priority = priority;
        }
    }
}
