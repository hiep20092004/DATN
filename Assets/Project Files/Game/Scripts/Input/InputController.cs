using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterFlow.Game
{
    [DefaultExecutionOrder(-1)]
    public class InputController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private InputAction mousePositionAction;
        private bool actionsInitialized;

        public static InputActionAsset InputActionsAsset { get; private set; }

        public static Vector2 MousePosition { get; private set; }
        public static InputAction ClickAction { get; private set; }

        private void Awake()
        {
            if (!inputActions)
            {
                Debug.LogWarning($"[{nameof(InputController)}] Input actions asset is not assigned.");
                return;
            }

            InputActionsAsset = inputActions;

            mousePositionAction = inputActions.FindAction("UI/Point", throwIfNotFound: false);
            ClickAction = inputActions.FindAction("UI/Click", throwIfNotFound: false);

            if (mousePositionAction == null || ClickAction == null)
            {
                Debug.LogWarning($"[{nameof(InputController)}] UI/Point or UI/Click action is missing.");
                return;
            }

            mousePositionAction.Enable();
            ClickAction.Enable();

            // Event-driven cursor updates avoid polling every frame.
            mousePositionAction.performed += OnMousePositionChanged;
            mousePositionAction.canceled += OnMousePositionChanged;
            MousePosition = mousePositionAction.ReadValue<Vector2>();
            actionsInitialized = true;
        }

        private void OnDestroy()
        {
            if (!actionsInitialized) return;

            mousePositionAction.performed -= OnMousePositionChanged;
            mousePositionAction.canceled -= OnMousePositionChanged;

            mousePositionAction.Disable();
            ClickAction.Disable();
            MousePosition = default;
            ClickAction = null;
            InputActionsAsset = null;
        }

        private static void OnMousePositionChanged(InputAction.CallbackContext context)
        {
            MousePosition = context.ReadValue<Vector2>();
        }
    }
}