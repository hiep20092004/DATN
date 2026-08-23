using UnityEngine;
using Ease = DG.Tweening.Ease;

namespace WaterFlow.Game
{
    /// <summary>
    /// Tunables for <see cref="LiftBehavior"/>: bound geometry/visual and the block rise / bound
    /// dissolve animations. Parallels <see cref="ContainerBoxEffectConfig"/> but is standalone since
    /// Lift is an extra-layer handler, not a block effect.
    /// </summary>
    [CreateAssetMenu(menuName = "WaterFlow/Extra Layer/Lift Config", fileName = "Lift Config")]
    public class LiftConfig : ScriptableObject
    {
        [Header("Geometry")]
        [Tooltip("World Y plane used to compute the lift bound.")]
        [SerializeField] private float liftBaseY = 0f;
        [Tooltip("Local Y offset of the bound visual. If <= 0, uses the visual's prefab local Y.")]
        [SerializeField] private float visualHeightOffset = 0.6f;

        [Header("Visual")]
        [Tooltip("Extra width added on top of the bound width (in cell units).")]
        [SerializeField] private float sizeWidthPadding = 0f;
        [Tooltip("Extra height added on top of the bound height (in cell units).")]
        [SerializeField] private float sizeHeightPadding = 0f;

        [Header("Door Open")]
        [Tooltip("How long the doors take to slide fully open when the lift spawns.")]
        [SerializeField, Min(0f)] private float doorOpenDuration = 0.35f;
        [SerializeField] private Ease doorOpenEase = Ease.OutCubic;

        [Header("Block Rise")]
        [Tooltip("How far below their final position lifted blocks start. Blocks begin at the lift bound center and rise outward to their cells.")]
        [SerializeField, Min(0f)] private float riseFromDepth = 1.5f;
        [Tooltip("Uniform scale blocks start at (relative to final size) while clustered at the lift bound center.")]
        [SerializeField, Range(0f, 1f)] private float riseFromScale = 0.15f;
        [Tooltip("Seconds to wait after lift spawn before blocks begin rising. Door open runs in parallel from spawn.")]
        [SerializeField, Min(0f)] private float riseBlockStartDelay = 0f;
        [SerializeField, Min(0f)] private float riseDuration = 0.35f;
        [SerializeField] private Ease riseEase = Ease.OutBack;

        [Header("Bound Dissolve")]
        [SerializeField, Min(0f)] private float dissolveDuration = 0.25f;
        [SerializeField] private Ease dissolveEase = Ease.InBack;

        public float LiftBaseY => liftBaseY;
        public float VisualHeightOffset => visualHeightOffset;
        public float SizeWidthPadding => sizeWidthPadding;
        public float SizeHeightPadding => sizeHeightPadding;
        public float DoorOpenDuration => doorOpenDuration;
        public Ease DoorOpenEase => doorOpenEase;
        public float RiseFromDepth => riseFromDepth;
        public float RiseFromScale => riseFromScale;
        public float RiseBlockStartDelay => riseBlockStartDelay;
        public float RiseDuration => riseDuration;
        public Ease RiseEase => riseEase;
        public float DissolveDuration => dissolveDuration;
        public Ease DissolveEase => dissolveEase;
    }
}
