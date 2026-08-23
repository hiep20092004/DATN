using System;
using WaterFlow.Framework.UIModule;

public class LosePanelBase : Panel
{
    public class Data : UIData
    {
        public Action onRetryClick;
    }

    protected Data data;
    protected bool clicked = false;

    public override void Open(UIData uiData)
    {
        base.Open(uiData);
        data = (Data)uiData;
    }

    public virtual void RetryClick()
    {
        Retry();
    }

    protected virtual void Retry()
    {
        if (clicked) return;
        clicked = true;
        Close();
        data?.onRetryClick?.Invoke();
    }
}
