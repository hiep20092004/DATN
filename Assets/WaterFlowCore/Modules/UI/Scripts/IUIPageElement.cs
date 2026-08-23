namespace WaterFlow.Core
{
    public interface IUIPageElement
    {
        public void Init(UIPage page);
        public void OnPageStateChanged(bool state);
    }
}