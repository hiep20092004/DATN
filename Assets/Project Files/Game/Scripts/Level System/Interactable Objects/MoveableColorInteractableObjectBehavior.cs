using Lofelt.NiceVibrations;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Color obstacle variant the player can pick up and drag like a block. Keeps the
    /// color-cover mechanic from <see cref="ColorInteractableObjectBehavior"/> and adds
    /// block-style movement (ground raycast follow, board clamp, velocity-driven physics,
    /// grid snap with stall fallback) driven by the shared <see cref="BlockConfig"/> so the
    /// drag feel matches blocks exactly.
    /// </summary>
    public class MoveableColorInteractableObjectBehavior : ColorInteractableObjectBehavior, IClickableObject
    {
        // Path to the finger cell can be blocked by walls/blocks; after this many ticks without
        // progress, fall back to the nearest cell from the current physical position.
        private const int SNAP_STALL_TICK_LIMIT = 5;
        private const float SNAP_STALL_PROGRESS_EPSILON = 0.0025f;

        [Space]
        [SerializeField] float pickedModelYOffset = 0.4f;

        private MovementConfigData configData;
        private Rigidbody objectRigidbody;
        private Camera cachedMainCamera;

        // Resolved in OnCreated: LayerMask.NameToLayer (via GameLayer's static init) is not
        // allowed during MonoBehaviour construction/deserialization, e.g. editor level preview.
        private int groundLayerMask;

        private bool isPicked;
        private bool isSnapping;
        private Vector3 touchOffset;

        // Finger-intent position (board-clamped), refreshed every physics tick. On release we
        // snap toward this instead of the rigidbody position, so a fast flick still lands on
        // the cell the finger lifted over even if the physics body is lagging behind.
        private Vector3 lastDesiredPosition;

        private Vector3 snapTargetPosition;
        private float snapMoveSpeed;
        private float snapPreviousDistanceSq;
        private int snapStallTicks;
        private bool snapRetargeted;

        private float currentModelY;
        private float targetModelY;
        private bool isModelYTransitioning;

        private Vector3 originalLocalPosition;
        private TweenCase shakeTweenCase;

        public bool IsPicked => isPicked;

        public override void OnCreated()
        {
            base.OnCreated();

            originalLocalPosition = transform.localPosition;

            // Editor preview spawns this behavior too; physics setup only matters in play mode.
            if (!Application.isPlaying) return;

            configData = MovementConfigData.From(LevelController.Instance ? LevelController.Instance.BlockConfig : null);
            groundLayerMask = 1 << GameLayer.LAYER_GROUND;

            objectRigidbody = GetComponent<Rigidbody>();
            if (!objectRigidbody)
                objectRigidbody = gameObject.AddComponent<Rigidbody>();
            LevelBlockBehavior.ApplyRigidbodySettings(objectRigidbody);

            // The prefab parents the collider under modelParent; the picked-lift would carry it
            // above block colliders so the drag passes through blocks. Keep physics grounded by
            // pinning the collider to the root (same separation blocks use via colliderParent).
            // Safe here: base.OnCreated already applied defaultY, so the resting pose is kept.
            if (objectCollider)
                objectCollider.transform.SetParent(transform, worldPositionStays: true);

            cachedMainCamera = Camera.main;
        }

        public void OnObjectClicked()
        {
            PickObject();
        }

        public bool CanBeClicked() => !isHide && !isPicked;

        public void OnClickBlocked()
        {
            shakeTweenCase.KillActive();
            transform.localPosition = originalLocalPosition;
            shakeTweenCase = transform.DOShake(0.04f, 0.15f).OnComplete(() =>
            {
                if (gameObject)
                {
                    transform.localPosition = originalLocalPosition;
                }
            });

            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Deny);
            Services.AudioService.PlaySound(AudioId.Booster_Denied);
        }

        private void PickObject()
        {
            if (isPicked || !objectRigidbody) return;

            // A previous release may still be snapping to grid; land it before re-driving the body.
            FinishPendingSnap();

            isPicked = true;
            objectRigidbody.isKinematic = false;

            if (modelParent)
            {
                currentModelY = modelParent.transform.localPosition.y;
                targetModelY = defaultY + pickedModelYOffset;
                isModelYTransitioning = true;
            }

            Vector3 rigidbodyPosition = objectRigidbody.position;
            touchOffset = rigidbodyPosition - GetWorldPosFromMouse();
            touchOffset.y = 0f;
            lastDesiredPosition = rigidbodyPosition;

            Services.AudioService.PlaySound(AudioId.Block_Pick);
            WaterFlow.Core.HapticFeedback.Play(WaterFlow.Core.HapticType.Block_Select);
        }

        private Vector3 GetWorldPosFromMouse()
        {
            if (!cachedMainCamera)
                cachedMainCamera = Camera.main;
            if (!cachedMainCamera)
                return objectRigidbody ? objectRigidbody.position : transform.position;

            Ray ray = cachedMainCamera.ScreenPointToRay(InputController.MousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, configData.RaycastDistance, groundLayerMask))
            {
                return hit.point;
            }

            return objectRigidbody ? objectRigidbody.position : transform.position;
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            UpdateModelYTransition();

            if (isSnapping)
            {
                UpdateSnapMovement();
                return;
            }

            if (!isPicked) return;

            // RaycastController routes pointer-up to the block movement manager only, so the
            // obstacle watches the click action itself to detect release.
            var clickAction = InputController.ClickAction;
            if (clickAction == null || !clickAction.IsPressed())
            {
                BeginSnap();
                return;
            }

            if (!cachedMainCamera)
                cachedMainCamera = Camera.main;
            if (!cachedMainCamera) return;

            Ray ray = cachedMainCamera.ScreenPointToRay(InputController.MousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, configData.RaycastDistance, groundLayerMask))
            {
                UpdateDragMovement(hit.point);
            }
        }

        private void UpdateDragMovement(Vector3 mouseWorldPos)
        {
            if (OwnerLevel == null) return;

            float xClamp = Mathf.Clamp(mouseWorldPos.x, 0f, OwnerLevel.Size.x - 1);
            float zClamp = Mathf.Clamp(mouseWorldPos.z, 0f, OwnerLevel.Size.y - 1);
            mouseWorldPos = new Vector3(xClamp, 0f, zClamp);

            Vector3 direction = mouseWorldPos + touchOffset - objectRigidbody.position;
            direction.y = 0f;

            lastDesiredPosition = objectRigidbody.position + direction;

            // Cover the whole remaining offset this tick when close (no exponential trailing),
            // capped at MovementSpeed when far. Velocity-driven so collisions still resolve.
            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            objectRigidbody.linearVelocity = Vector3.ClampMagnitude(direction / dt, configData.MovementSpeed);
        }

        private void BeginSnap()
        {
            isPicked = false;
            isSnapping = true;

            if (modelParent)
            {
                targetModelY = defaultY;
                isModelYTransitioning = true;
            }

            objectRigidbody.linearVelocity = Vector3.zero;

            int x = Mathf.RoundToInt(lastDesiredPosition.x);
            int z = Mathf.RoundToInt(lastDesiredPosition.z);
            if (OwnerLevel != null)
            {
                x = Mathf.Clamp(x, 0, OwnerLevel.Size.x - 1);
                z = Mathf.Clamp(z, 0, OwnerLevel.Size.y - 1);
            }
            snapTargetPosition = new Vector3(x, 0f, z);

            SetSnapSpeedFor(Vector3.Distance(objectRigidbody.position, snapTargetPosition));
            snapPreviousDistanceSq = float.MaxValue;
            snapStallTicks = 0;
            snapRetargeted = false;

            Services.AudioService.PlaySound(AudioId.Block_Put);
            WaterFlow.Core.HapticFeedback.Play(WaterFlow.Core.HapticType.Block_Select);
        }

        private void UpdateSnapMovement()
        {
            if (!objectRigidbody)
            {
                isSnapping = false;
                return;
            }

            Vector3 currentPosition = objectRigidbody.position;
            Vector3 toTarget = snapTargetPosition - currentPosition;
            toTarget.y = 0f;

            float snapStopDistance = Mathf.Max(configData.MovementSnapStopDistance, 0.01f);
            bool forceComplete = false;

            if (toTarget.sqrMagnitude > snapStopDistance * snapStopDistance)
            {
                if (snapPreviousDistanceSq - toTarget.sqrMagnitude < SNAP_STALL_PROGRESS_EPSILON)
                {
                    snapStallTicks++;
                    if (snapStallTicks >= SNAP_STALL_TICK_LIMIT)
                    {
                        if (snapRetargeted)
                        {
                            forceComplete = true;
                            snapTargetPosition = new Vector3(
                                Mathf.RoundToInt(currentPosition.x), 0f,
                                Mathf.RoundToInt(currentPosition.z));
                        }
                        else
                        {
                            snapRetargeted = true;
                            snapStallTicks = 0;
                            snapPreviousDistanceSq = float.MaxValue;
                            snapTargetPosition = new Vector3(
                                Mathf.RoundToInt(currentPosition.x), 0f,
                                Mathf.RoundToInt(currentPosition.z));
                            SetSnapSpeedFor(Vector3.Distance(currentPosition, snapTargetPosition));
                            return;
                        }
                    }
                }
                else
                {
                    snapStallTicks = 0;
                }
                snapPreviousDistanceSq = toTarget.sqrMagnitude;

                if (!forceComplete)
                {
                    float distance = toTarget.magnitude;
                    float maxStepSpeed = distance / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
                    float effectiveSpeed = Mathf.Min(snapMoveSpeed, maxStepSpeed);
                    objectRigidbody.linearVelocity = (toTarget / distance) * effectiveSpeed;
                    return;
                }
            }

            CompleteSnap();
        }

        // Land the in-flight snap on the nearest cell from the current physical position before a
        // new pick re-drives the body, so the obstacle is never orphaned mid-snap (same fallback
        // BlockMovementManager.FinishPendingSnap uses).
        private void FinishPendingSnap()
        {
            if (!isSnapping || !objectRigidbody) return;

            Vector3 currentPosition = objectRigidbody.position;
            snapTargetPosition = new Vector3(
                Mathf.RoundToInt(currentPosition.x), 0f,
                Mathf.RoundToInt(currentPosition.z));
            CompleteSnap();
        }

        private void CompleteSnap()
        {
            objectRigidbody.position = snapTargetPosition;
            objectRigidbody.linearVelocity = Vector3.zero;
            objectRigidbody.angularVelocity = Vector3.zero;
            objectRigidbody.isKinematic = true;
            Physics.SyncTransforms();

            isSnapping = false;

            // The cover mechanic (OnBlockPicked/OnBlockReleased overlap checks) reads this grid
            // position, so it must follow the obstacle to its new cell.
            position = new Vector2Int(
                Mathf.RoundToInt(snapTargetPosition.x),
                Mathf.RoundToInt(snapTargetPosition.z));
            originalLocalPosition = transform.localPosition;
        }

        private void SetSnapSpeedFor(float distance)
        {
            float snapDuration = Mathf.Max(configData.MovementSnapDuration, 0.001f);
            snapMoveSpeed = Mathf.Min(distance / snapDuration, configData.MovementSpeed);
        }

        private void UpdateModelYTransition()
        {
            if (!isModelYTransitioning || !modelParent) return;

            currentModelY = Mathf.Lerp(currentModelY, targetModelY, configData.ModelYLerpSpeed * Time.fixedDeltaTime);
            if (Mathf.Abs(currentModelY - targetModelY) < 0.001f)
            {
                currentModelY = targetModelY;
                isModelYTransitioning = false;
            }

            Vector3 localPosition = modelParent.transform.localPosition;
            localPosition.y = currentModelY;
            modelParent.transform.localPosition = localPosition;
        }
    }
}
