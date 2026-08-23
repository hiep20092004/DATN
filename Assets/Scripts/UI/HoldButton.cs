using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action onDown;
    public Action onUp;

    [Tooltip("If true, releasing when the pointer leaves the button area. Causes flicker when hiding UI on hold.")]
    [SerializeField] private bool invokeUpOnPointerExit = false;

    private bool _isPressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isPressed)
            return;

        _isPressed = true;
        onDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (invokeUpOnPointerExit)
            Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void Release()
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        onUp?.Invoke();
    }
}
