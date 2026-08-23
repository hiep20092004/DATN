namespace WaterFlow.Framework.Systems.ConfigManagement
{
    public abstract class ConfigService: ServiceSo
    {
        public abstract T Get<T>() where T : class;
    }
}