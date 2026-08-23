using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mirrors a ScrollRect's drag events to <see cref="UINavigateBarSwipe"/> so the
/// tab swipe still sees gestures a scroller would otherwise swallow. A vertical
/// scroller always relays; a horizontal one (page slider) relays only when it
/// cannot scroll (single page), so a one-page slider still switches the tab.
/// Added at runtime by <see cref="UINavigateBarSwipe.AttachScrollRectRelays"/>.
/// </summary>
public class ScrollRectSwipeRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float SCROLLABLE_EPSILON = 1f;

    private UINavigateBarSwipe target;
    private ScrollRect scrollRect;
    private bool guardHorizontal;
    private bool forwarding;

    public void Init(UINavigateBarSwipe swipeHandler)
    {
        target = swipeHandler;
        scrollRect = GetComponent<ScrollRect>();
        guardHorizontal = scrollRect != null && scrollRect.horizontal;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Decide once per gesture so OnDrag/OnEndDrag stay consistent.
        forwarding = ShouldForward();
        if (forwarding) target.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (forwarding) target.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (forwarding) target.OnEndDrag(eventData);
    }

    private bool ShouldForward()
    {
        if (target == null) return false;
        if (!guardHorizontal) return true;
        return !CanScrollHorizontally();
    }

    private bool CanScrollHorizontally()
    {
        if (scrollRect == null || scrollRect.content == null) return false;
        RectTransform viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : (RectTransform)scrollRect.transform;
        if (viewport == null) return false;
        return scrollRect.content.rect.width > viewport.rect.width + SCROLLABLE_EPSILON;
    }
}
