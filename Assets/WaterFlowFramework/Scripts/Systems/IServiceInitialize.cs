namespace WaterFlow.Framework.Systems
{
    public interface IServiceInitialize
    {
        public void Initialize();
    }
    
    public interface IServiceActionOnFocus
    {
        public void OnApplicationFocus(bool focus);
    }
}