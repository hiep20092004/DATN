using UnityEngine;

namespace WaterFlow.Core
{
    public interface IPool
    {
        public string Name { get; }

        public void Init();

        public GameObject GetPooledObject();

        public void CreatePoolObjects(int count);
        public void ReturnToPoolEverything(bool resetParent = false);

        public void Clear();
    }
}