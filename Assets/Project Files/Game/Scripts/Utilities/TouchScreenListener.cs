using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class TouchScreenListener : MonoBehaviour
{
    public UnityEvent onTouchOutside;
    public RectTransform ignoreArea;

    private Canvas _ignoreCanvas;

    private void Awake()
    {
        if (ignoreArea != null)
        {
            _ignoreCanvas = ignoreArea.GetComponentInParent<Canvas>();
        }
    }

    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (ignoreArea == null) return;

        Vector2 touchPos = pointer.position.ReadValue();

        Camera cam = null;
        if (_ignoreCanvas != null &&
            _ignoreCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = _ignoreCanvas.worldCamera != null
                ? _ignoreCanvas.worldCamera
                : Camera.main;
        }

        bool isInside = RectTransformUtility.RectangleContainsScreenPoint(ignoreArea, touchPos, cam);

        if (!isInside)
        {
            OnTouchScreen();
        }
    }

    public void OnTouchScreen()
    {
        onTouchOutside.Invoke();
    }
}