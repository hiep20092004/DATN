namespace WaterFlow.Game
{
    public interface IClickableObject
    {
        public void OnObjectClicked();
        public bool CanBeClicked();
        public void OnClickBlocked();
    }
}