using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    [CreateAssetMenu(fileName = "PoolingContainer", menuName = "WaterFlow Services/Pooling/Pooling Container")]
    public class PoolingContainer : PoolingContainerService
    {
        public override T CreateObject<T>(Transform container)
        {
            Transform template = null;
            for (var i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                // Persistent non-pooled children (marked with PoolIgnore, e.g. a special-reward
                // UIAvatarBase) are neither reused nor cloned as the template.
                if (child.GetComponent<PoolIgnore>() != null)
                {
                    continue;
                }

                // First poolable child is the clone template; kept as the source, never reused.
                if (template == null)
                {
                    template = child;
                    continue;
                }

                if (!child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                    child.transform.SetAsLastSibling();
                    return child.GetComponent<T>();
                }
            }

            var source = template != null ? template : container.GetChild(0);
            var obj = Object.Instantiate(source.gameObject, container);
            obj.SetActive(true);
            return obj.GetComponent<T>();
        }

        public override void CleanContainer(Transform container)
        {
            for (var i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                // Leave pool-ignored children (e.g. a special-reward UIAvatarBase) untouched; their
                // active state is owned by the driving widget, not the pool.
                if (child.GetComponent<PoolIgnore>() != null) continue;
                child.gameObject.SetActive(false);
            }
        }
    }
}