using DG.Tweening;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.ObjectPooling;
using UnityEngine;

public class PopupToast : Panel
{
    private static PopupToast popupToast;
    private readonly Service<PoolingContainerService> poolingContainer = new();
    public UIToastItem toastItemPrefab;
    private static bool isCreatingPopupToast;
    private string content;
    private string param;
    private float duration;

    public static void Create(string content, string param = null, float duration = 1.25f)
    {
        if (isCreatingPopupToast) return;
        if (popupToast == null || !popupToast.gameObject.activeInHierarchy)
        {
            isCreatingPopupToast = true;
            UIData uIData = new UIData();
            uIData.Add("content", content);
            uIData.Add("param", param);
            uIData.Add("duration", duration);
            PanelManager.Instance.OpenPanelAsync<PopupToast>(OnCreatedPopupToast, uIData).Forget();
        }
        else
        {
            popupToast.transform.SetAsLastSibling();
            popupToast.AddToast(content, param);
        }
    }

    private static void OnCreatedPopupToast(PopupToast toast)
    {
        popupToast = toast;
        isCreatingPopupToast = false;
    }

    public Transform container;

    public override void Open(UIData uIData)
    {
        base.Open(uIData);
        content = uIData.Get<string>("content");
        duration = uIData.Get<float>("duration");
        param = uIData.Get<string>("param");
        AddToast(content, param);
    }


    public void AddToast(string content, string param = null)
    {
        UIToastItem toast = Instantiate(toastItemPrefab, container);
        toast.SetData(content, param);
        toast.gameObject.SetActive(true);
        DOVirtual.DelayedCall(duration, () => toast.gameObject.SetActive(false));
        DOTween.Kill("toast_dismiss");
        DOVirtual.DelayedCall(duration + 0.1f, () => Close()).SetId("toast_dismiss");
    }
}