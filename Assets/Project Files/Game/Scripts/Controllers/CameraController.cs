using DG.Tweening;
using UnityEngine;

namespace WaterFlow.Game
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] CameraControllerConfig config;

        Camera _camera;
        private Vector3 direction;
        private Tween _repositionSequence;
        private Tween _orthographicSizeTween;
        private Tween _shakeTween;

        float _horizontalPadding;
        
        // Debug cache
        Vector3 _debugTargetPosition;
        Vector3 _debugLevelSize;
        
        // Cache last reposition parameters
        Vector3 _lastTargetPosition;
        Vector3 _lastLevelSize;
        bool _hasRepositioned;

        public float RepositionDuration => config != null ? config.repositionDuration : 0f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            EnsureConfig();
        }

        private void OnEnable()
        {
            EnsureConfig();
            if (config != null)
            {
                config.Changed += OnConfigChanged;
            }
        }

        private void OnDisable()
        {
            if (config != null)
            {
                config.Changed -= OnConfigChanged;
            }
        }

        private void EnsureConfig()
        {
            if (config != null) return;
            config = Resources.Load<CameraControllerConfig>("CameraControllerConfig");
        }

        private void OnConfigChanged()
        {
            if (_hasRepositioned)
            {
                ReapplyLastReposition();
            }
        }

        public void Reposition(Bounds levelBounds)
        {
            Reposition(levelBounds.center, levelBounds.size);
        }
        
        public void Reposition(Vector3 targetPosition, Vector3 levelSize)
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
            EnsureConfig();
            UpdateHorizontalPadding(levelSize);
            // Cache parameters for re-applying
            _lastTargetPosition = targetPosition;
            _lastLevelSize = levelSize;
            _hasRepositioned = true;
            
            direction = CalculateViewDirection();
            Vector3 effectiveTarget = targetPosition + Vector3.forward * (config != null ? config.targetZOffset : 0f);

            _debugTargetPosition = effectiveTarget;
            _debugLevelSize = levelSize;

            Vector3 targetCameraPosition;
            float targetOrthographicSize = _camera.orthographicSize;

            if (_camera.orthographic)
            {
                float levelWidth = levelSize.x + _horizontalPadding;
                float levelHeight = levelSize.z + GetVerticalPadding();

                float requiredOrthographicSizeByHeight = levelHeight * 0.5f;
                float requiredOrthographicSizeByWidth = levelWidth * 0.5f / _camera.aspect;

                targetOrthographicSize = Mathf.Max(requiredOrthographicSizeByHeight, requiredOrthographicSizeByWidth) + (config != null ? config.orthographicSizeAddition : 0f);
                targetCameraPosition = effectiveTarget + direction * 10f;
            }
            else
            {
                float distance = CalculateRequiredDistance(levelSize);
                targetCameraPosition = effectiveTarget + direction * distance;
            }

            Quaternion targetRotation = Quaternion.LookRotation(effectiveTarget - targetCameraPosition);

            bool animate = config != null && config.animateReposition;
            float repositionDuration = config != null ? config.repositionDuration : 0f;
            if (!Application.isPlaying || !animate || repositionDuration <= 0f)
            {
                _repositionSequence?.Kill();
                _orthographicSizeTween?.Kill();
                transform.position = targetCameraPosition;
                transform.rotation = targetRotation;
                if (_camera.orthographic)
                {
                    _camera.orthographicSize = targetOrthographicSize;
                }
                return;
            }

            _repositionSequence?.Kill();
            _orthographicSizeTween?.Kill();

            Ease ease = config != null ? config.repositionEase : Ease.OutCubic;
            float orthographicResizeDuration = config != null ? config.orthographicResizeDuration : 0f;
            _repositionSequence = DOTween.Sequence()
                .Join(transform.DOMove(targetCameraPosition, repositionDuration).SetEase(ease))
                .Join(transform.DORotateQuaternion(targetRotation, repositionDuration).SetEase(ease));

            if (_camera.orthographic)
            {
                _orthographicSizeTween = DOTween.To(
                        () => _camera.orthographicSize,
                        value => _camera.orthographicSize = value,
                        targetOrthographicSize,
                        orthographicResizeDuration
                    )
                    .SetEase(ease);
            }
        }

        private float GetVerticalPadding()
        {
            if (config == null) return 0f;
            return AdsUtilities.IsShowBanner() ? config.verticalPadding + config.bannerAdsPadding : config.verticalPadding;
        }
        public void StartShake(float duration, float strength)
        {
            _shakeTween?.Kill();
            _shakeTween = transform.DOShakePosition(duration, strength, 15, 50f).SetEase(Ease.InOutBack);
        }

        public Vector3 GetDirectionTowardCamera()
        {
            return direction;
        }
        
        public void ReapplyLastReposition()
        {
            if (!_hasRepositioned)
            {
                Debug.LogWarning("CameraController: No previous reposition to reapply. Call Reposition() first.");
                return;
            }
            
            Reposition(_lastTargetPosition, _lastLevelSize);
        }

        private void UpdateHorizontalPadding(Vector3 levelSize)
        {
            if (config == null)
            {
                _horizontalPadding = 0f;
                return;
            }
            _horizontalPadding = config.GetHorizontalPadding(levelSize.x);
        }
        Vector3 CalculateViewDirection()
        {
            // Pitch: rotate around X axis (look down)
            // Yaw: rotate around Y axis
            float pitch = config != null ? config.pitchAngle : 0f;
            float yaw = config != null ? config.yawAngle : 0f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Forward points toward target, so camera goes backward
            return rotation * Vector3.back;
        }

        float CalculateRequiredDistance(Vector3 levelSize)
        {
            float baseDistance = config != null ? config.baseDistance : 0f;
            float levelWidth = levelSize.x + _horizontalPadding;
            float levelHeight = levelSize.z + GetVerticalPadding();

            float vFovRad = _camera.fieldOfView * Mathf.Deg2Rad;
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * _camera.aspect);

            float distByHeight = (levelHeight * 0.5f) / Mathf.Tan(vFovRad * 0.5f);
            float distByWidth = (levelWidth * 0.5f) / Mathf.Tan(hFovRad * 0.5f);

            return Mathf.Max(distByHeight, distByWidth, baseDistance);
        }

        void OnDrawGizmos()
        {
            EnsureConfig();
            if (config == null || !config.drawGizmos) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_debugTargetPosition, 0.25f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                _debugTargetPosition,
                new Vector3(_debugLevelSize.x, 0.1f, _debugLevelSize.z)
            );

            // Camera direction
            Gizmos.color = Color.red;
            Vector3 dir = CalculateViewDirection();
            Gizmos.DrawLine(
                _debugTargetPosition,
                _debugTargetPosition + dir * 3f
            );
        }
    }
}