using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "CameraControllerConfig", menuName = "WaterFlow/Configs/Camera Controller Config", order = 0)]
    public class CameraControllerConfig : ScriptableObject
    {
        [Header("View Angle")]
        [Tooltip("Camera pitch angle (0 = horizontal, 90 = top-down)")]
        [Range(10f, 85f)]
        public float pitchAngle = 77f;

        [Tooltip("Yaw rotation around Y axis")]
        public float yawAngle = 0f;

        [Header("Distance")]
        public float baseDistance = 14f;
        public float orthographicSizeAddition = 1.6f;

        [Header("Level Padding")]
        public float verticalPadding = 2f;
        [FormerlySerializedAs("bannerAdsPadding")]
        public float bottomReservedPadding = 1.5f;

        [Header("Target Offset")]
        [Tooltip("Offset applied to the camera target position on Z axis.")]
        public float targetZOffset = 0f;

        [Header("Horizontal Padding (auto by level width)")]
        [Tooltip("If level width > wideThreshold -> widePadding")]
        public float wideThreshold = 9f;
        public float widePadding = 0f;

        [Tooltip("Else if level width > mediumThreshold -> mediumPadding")]
        public float mediumThreshold = 5f;
        public float mediumPadding = 0f;

        [Tooltip("Else -> defaultPadding")]
        public float defaultPadding = 0f;

        [Header("Debug Gizmos")]
        public bool drawGizmos = false;

        [Header("Reposition Tween")]
        public bool animateReposition = true;
        public float repositionDuration = 0.35f;
        public Ease repositionEase = Ease.OutCubic;
        public float orthographicResizeDuration = 0.25f;

        [NonSerialized] public Action Changed;

        public float GetHorizontalPadding(float levelWidth)
        {
            if (levelWidth > wideThreshold) return widePadding;
            if (levelWidth > mediumThreshold) return mediumPadding;
            return defaultPadding;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Changed?.Invoke();
        }
#endif
    }
}
