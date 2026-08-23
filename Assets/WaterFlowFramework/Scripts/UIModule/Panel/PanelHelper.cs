using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WaterFlow.Framework.UIModule
{
    public static class PanelHelper
    {
        public static async UniTaskVoid CreateInstancePanel<T>(this T panel, UIData uiData = null, Transform container = null) where T : Panel
        {
            panel = await PanelManager.Instance.OpenPanelAsync<T>(uiData, container);
        }
        
        public static async UniTaskVoid CreateInstancePanelByName<T>(this T panel, string panelName, UIData uiData = null, Transform container = null) where T : Panel
        {
            panel = await PanelManager.Instance.OpenPanelByNameAsync<T>(panelName, uiData, container);
        }
    }
}