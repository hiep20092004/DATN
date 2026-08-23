using Cysharp.Threading.Tasks;

namespace WaterFlow.Framework.Systems
{
    public interface IServiceInitializeAsync
    {
        public UniTaskVoid InitializeAsync();
    }
}