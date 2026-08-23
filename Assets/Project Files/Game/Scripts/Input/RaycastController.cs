using System;
using System.Collections.Generic;
using System.Linq;
using WaterFlow.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace WaterFlow.Game
{
    [StaticUnload]
    public class RaycastController : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float maxRaycastDistance = 200f;
        [SerializeField] private LayerMask raycastLayerMask = ~0;
        [SerializeField] private bool highlightHitInHierarchy;
        private const int RAYCAST_HIT_BUFFER_SIZE = 12;

        private Camera mainCam;
        private readonly RaycastHit[] raycastHitsBuffer = new RaycastHit[RAYCAST_HIT_BUFFER_SIZE];
        private static bool isActive;
        private static readonly Dictionary<string, int> disableReasons = new Dictionary<string, int>(StringComparer.Ordinal);

        public static event SimpleCallback OnInputActivated;
        public static event SimpleCallback OnObjectTouched;

        /// <summary>
        /// Initializes the controller by caching the main camera and enabling input.
        /// </summary>
        public void Init()
        {
            mainCam = Camera.main;
            isActive = mainCam != null && disableReasons.Count == 0;

            if (!mainCam)
            {
                Debug.LogWarning($"[{nameof(RaycastController)}] Main camera not found. Raycast input disabled.");
            }
        }

        private void Update()
        {
            if (!isActive) return;
            InputAction clickAction = InputController.ClickAction;
            if (clickAction == null) return;

            if (clickAction.WasPressedThisFrame())
            {
                HandlePress();
            }
            else if (clickAction.WasReleasedThisFrame())
            {
                HandleRelease();
            }
        }

        private void OnDestroy()
        {
            UnloadStatic();
        }

        private void HandlePress()
        {
            if (IsPointerOverUI()) return;

            if (!TryGetClickableHit(out RaycastHit hit, out IClickableObject clickableObject)) return;

#if UNITY_EDITOR
            if (highlightHitInHierarchy && hit.collider)
            {
                UnityEditor.Selection.activeGameObject = hit.collider.gameObject;
            }
#endif

            ProcessClickableObject(clickableObject, hit.point);
        }

        private static void ProcessClickableObject(IClickableObject clickableObject, Vector3 hitPoint)
        {
            OnObjectTouched?.Invoke();

            if (PowerUpController.SelectedPowerUp)
            {
                PowerUpController.ApplyToElement(clickableObject, hitPoint);
            }
            else
            {
                HandleDirectClick(clickableObject);
            }
        }

        private static void HandleDirectClick(IClickableObject clickableObject)
        {
            if (clickableObject.CanBeClicked())
            {
                clickableObject.OnObjectClicked();
            }
            else
            {
                clickableObject.OnClickBlocked();
            }
        }

        private static void HandleRelease()
        {
            LevelController.Instance.OnObjectReleased();
        }

        private bool TryGetClickableHit(out RaycastHit hit, out IClickableObject clickableObject)
        {
            if (!mainCam)
            {
                hit = default;
                clickableObject = null;
                return false;
            }

            Ray ray = mainCam.ScreenPointToRay(InputController.MousePosition);
            int hitCount = Physics.RaycastNonAlloc(ray, raycastHitsBuffer, maxRaycastDistance, raycastLayerMask);
            if (hitCount <= 0)
            {
                hit = default;
                clickableObject = null;
                return false;
            }

            RaycastHit bestHit = default;
            IClickableObject bestClickable = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = raycastHitsBuffer[i];
                if (!candidate.collider)
                    continue;

                if (candidate.distance >= bestDistance)
                    continue;

                IClickableObject candidateClickable = candidate.collider.GetComponentInParent<IClickableObject>();
                if (candidateClickable == null)
                    continue;

                bestHit = candidate;
                bestClickable = candidateClickable;
                bestDistance = candidate.distance;
            }

            if (bestClickable == null)
            {
                hit = default;
                clickableObject = null;
                return false;
            }

            hit = bestHit;
            clickableObject = bestClickable;
            return true;
        }

        private static bool IsPointerOverUI()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// Disables raycast input processing.
        /// </summary>
        public static void Disable(string reason)
        {
            if (disableReasons.TryGetValue(reason, out var count))
            {
                disableReasons[reason] = count + 1;
            }
            else
            {
                disableReasons[reason] = 1;
            }

            UpdateActiveState();
        }

        /// <summary>
        /// Enables raycast input processing.
        /// </summary>
        public static void Enable(string reason, bool isShowLog = true)
        {
            if (!disableReasons.TryGetValue(reason, out var count))
            {
                if(isShowLog) Debug.Log($"[RaycastController] Not found Disable \"{reason}\" reason");
                if (disableReasons.Count > 0)
                {
                    string reasons = string.Join(", ", disableReasons.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Debug.Log($"[RaycastController] Current disableReasons ({disableReasons.Count}): {reasons}");
                }
                return;
            }

            if (count <= 1)
            {
                disableReasons.Remove(reason);
            }
            else
            {
                disableReasons[reason] = count - 1;
            }

            UpdateActiveState();
        }

        private static void UpdateActiveState()
        {
            bool shouldBeActive = disableReasons.Count == 0;
            if (isActive == shouldBeActive) return;

            isActive = shouldBeActive;
            if (isActive)
            {
                OnInputActivated?.Invoke();
            }
        }

        private static void UnloadStatic()
        {
            isActive = false;
            disableReasons.Clear();
            OnInputActivated = null;
            OnObjectTouched = null;
        }
    }
}