using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WaterFlow.Framework.UIModule
{
    public interface IPanelAsyncManager
    {
        public void OpenForget<T>(UIData uiData = null, Transform container = null) where T : View;

        public UniTask<T> OpenPanelByNameAsync<T>(string panelName, UIData uiData = null, Transform container = null)
            where T : View;

        public UniTask<T> OpenPanelAsync<T>(UIData uiData = null, Transform container = null) where T : View;

        public UniTask ClosePanelAsync<T>(bool immediately = false, bool waitCloseCompleted = false) where T : View;

        public void ReleasePanel(View panelClosed);
    }
}