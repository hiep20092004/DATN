using UnityEngine;
using System;
using Lofelt.NiceVibrations;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "BlockConfig", menuName = "Data/Level/Block Config", order = 120)]
    public class BlockConfig : ScriptableObject
    {
        [Header("Fill Water Haptics")]
        [SerializeField] private HapticClip fillWaterShortHapticClip;
        [SerializeField] private HapticClip fillWaterMediumHapticClip;
        [SerializeField] private HapticClip fillWaterLongHapticClip;
        [SerializeField] private HapticClip fillWaterSuperLongHapticClip;
        
        [Header("Drag Movement")]
        [Tooltip("Raycast distance from camera to ground when dragging block.")]
        [Min(1f)]
        [SerializeField] private float raycastDistance = 100f;

        [Tooltip("Base movement speed applied to rigidbody velocity while dragging.")]
        [Min(0.01f)]
        [SerializeField] private float movementSpeed = 47f;

        [Tooltip("Direction magnitude threshold above which a position boost is applied for extra responsiveness.")]
        [Min(0f)]
        [SerializeField] private float movementBoostThreshold = 0.32f;

        [Tooltip("Position nudge multiplier applied when direction exceeds boost threshold.")]
        [Min(0f)]
        [SerializeField] private float movementBoostFactor = 0.0015f;

        [Header("Snap")]
        [Tooltip("Duration for snap tween to nearest grid position on release.")]
        [Min(0.01f)]
        [SerializeField] private float movementSnapDuration = 0.15f;

        [Tooltip("Distance threshold below which snapping is considered complete.")]
        [Min(0f)]
        [SerializeField] private float movementSnapStopDistance = 0.02f;

        [Header("Model Height")]
        [Tooltip("Y position for model parent while dragging (visual lift).")]
        [SerializeField] private float pickedModelY = 0.4f;

        [Tooltip("Y position for model parent after release.")]
        [SerializeField] private float releasedModelY = 0f;

        [Tooltip("Lerp speed for smooth model Y transition between picked and released states.")]
        [Min(0.1f)]
        [SerializeField] private float modelYLerpSpeed = 15f;

        [Tooltip("Follow speed of the visual model toward the physics body while dragging (rendered every frame). Higher = snappier, lower = more trailing.")]
        [Min(0.1f)]
        [SerializeField] private float visualFollowSpeed = 25f;

        [Header("Water Tilt")]
        [Tooltip("Water sine frequency while block is moving.")]
        [Min(0f)]
        [SerializeField] private float waterFrequencyOnRotate = 3f;

        [Tooltip("Water sine amplitude while block is moving.")]
        [Min(0f)]
        [SerializeField] private float waterAmplitudeOnRotate = 0.03f;

        [Tooltip("Maximum water rotation angle when moving to the right.")]
        [SerializeField] private float waterRotateToRightMax = 182.5f;

        [Tooltip("Maximum water rotation angle when moving to the left.")]
        [SerializeField] private float waterRotateToLeftMax = 172.5f;

        [Tooltip("Rotation recovery speed for water tilt.")]
        [Min(0f)]
        [SerializeField] private float waterRotationSpeed = 100f;

        [Tooltip("Minimum actual speed to consider the block as moving.")]
        [Min(0f)]
        [SerializeField] private float movementActiveSpeedThreshold = 0.5f;

        [Tooltip("Horizontal movement threshold to detect left/right direction.")]
        [Min(0f)]
        [SerializeField] private float horizontalDirectionThreshold = 0.1f;

        [Tooltip("Interpolation speed when updating water amplitude.")]
        [Min(0f)]
        [SerializeField] private float waterAmplitudeLerpSpeed = 0.5f;

        [Tooltip("Interpolation speed when updating water frequency.")]
        [Min(0f)]
        [SerializeField] private float waterFrequencyLerpSpeed = 5f;

        public HapticClip FillWaterShort => fillWaterShortHapticClip;
        public HapticClip FillWaterLong => fillWaterLongHapticClip;
        public HapticClip FillWaterMedium => fillWaterMediumHapticClip;
        public HapticClip FillWaterSuperLong => fillWaterSuperLongHapticClip;

        public float RaycastDistance => raycastDistance;
        public float MovementSpeed => movementSpeed;
        public float MovementBoostThreshold => movementBoostThreshold;
        public float MovementBoostFactor => movementBoostFactor;
        public float MovementSnapDuration => movementSnapDuration;
        public float MovementSnapStopDistance => movementSnapStopDistance;
        public float PickedModelY => pickedModelY;
        public float ReleasedModelY => releasedModelY;
        public float ModelYLerpSpeed => modelYLerpSpeed;
        public float VisualFollowSpeed => visualFollowSpeed;

        public float WaterFrequencyOnRotate => waterFrequencyOnRotate;
        public float WaterAmplitudeOnRotate => waterAmplitudeOnRotate;
        public float WaterRotateToRightMax => waterRotateToRightMax;
        public float WaterRotateToLeftMax => waterRotateToLeftMax;
        public float WaterRotationSpeed => waterRotationSpeed;
        public float MovementActiveSpeedThreshold => movementActiveSpeedThreshold;
        public float HorizontalDirectionThreshold => horizontalDirectionThreshold;
        public float WaterAmplitudeLerpSpeed => waterAmplitudeLerpSpeed;
        public float WaterFrequencyLerpSpeed => waterFrequencyLerpSpeed;

        public event Action Changed;

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            Changed?.Invoke();
        }
    }
}
