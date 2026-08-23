using UnityEngine;

namespace WaterFlow.Framework.Systems.ObjectPooling
{
    /// <summary>
    /// Marks a child that lives inside a pooled container purely for layout (e.g. a special-reward
    /// UIAvatarBase) but is NOT managed by the pool. <see cref="PoolingContainer"/> never
    /// deactivates or reuses a child carrying this marker — its active state is owned by the widget
    /// that drives the container.
    /// </summary>
    public sealed class PoolIgnore : MonoBehaviour
    {
    }
}
