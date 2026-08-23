using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Lives on the <see cref="BlockGroup"/> GameObject. Holds cached occupied bounds for
    /// visuals and draws group gizmos once (not per member block).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockGroupVisualHost : MonoBehaviour
    {
        [SerializeField] private Color gizmoColor = new(1f, 0.85f, 0.15f, 1f);

        public BlockGroupOccupiedBounds OccupiedBounds { get; private set; }

        public void SetOccupiedBounds(BlockGroupOccupiedBounds bounds)
        {
            OccupiedBounds = bounds;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!OccupiedBounds.IsValid)
                return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(OccupiedBounds.WorldCenter, OccupiedBounds.WorldSize);
        }
#endif
    }
}
