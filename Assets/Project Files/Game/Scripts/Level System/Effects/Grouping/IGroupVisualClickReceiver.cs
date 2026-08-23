namespace WaterFlow.Game
{
    public interface IGroupClickReceiver
    {
        bool CanClick();
        void OnClicked();
        void OnBlocked();
    }
}
