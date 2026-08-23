using WaterFlow.Enums;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Switches navigate-bar tabs with horizontal swipes over the tab content area.
/// Attach to the tab container (an ancestor of the tabs' raycast targets);
/// drags that start on non-draggable children bubble up to this handler, and
/// vertical ScrollRects inside the tabs get a <see cref="ScrollRectSwipeRelay"/>
/// so swipes that start over scrollable lists reach this handler as well.
///
/// With a <see cref="UINavigateBarSlide"/> bar the tabs follow the finger
/// (pager-style preview); the switch decision happens on release: commit when
/// the finger travelled far enough or flicked fast enough, otherwise the tabs
/// slide back. The previewed tab receives OnShow the moment it appears (so its
/// UI is fresh while dragging); a cancelled preview balances it with OnHide.
/// </summary>
public class UINavigateBarSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private UINavigateBarBase navigateBar;
    [Tooltip("Horizontal travel needed to switch on release, as a fraction of screen width.")]
    [SerializeField] private float switchThreshold = 0.2f;
    [Tooltip("Release speed that counts as a flick, in screen widths per second.")]
    [SerializeField] private float flickSpeedThreshold = 1f;

    // Flick speed is measured over roughly this window before release, so a
    // fast finish still counts after a slow start, while holding still kills it.
    private const float FLICK_SAMPLE_WINDOW = 0.12f;
    // A flick must still travel a little, so touch jitter never switches tabs.
    private const float MIN_FLICK_TRAVEL_RATIO = 0.03f;
    // Lock the gesture to one axis after this much travel, so vertical list
    // scrolling never wiggles the horizontal tab preview.
    private const float AXIS_LOCK_TRAVEL_RATIO = 0.012f;
    private const int SAMPLE_COUNT = 16;
    private const int NO_POINTER = int.MinValue;

    private enum GestureAxis { Undecided, Horizontal, Vertical }

    private readonly float[] sampleTimes = new float[SAMPLE_COUNT];
    private readonly float[] sampleX = new float[SAMPLE_COUNT];
    private int sampleHead;
    private int samplesStored;

    private int activePointerId = NO_POINTER;
    private NavigationType startTab;
    private GestureAxis axis;
    private bool previewDriving;
    private UINavigateBarSlide slideBar;

    private void Start()
    {
        AttachScrollRectRelays();
    }

    /// <summary>
    /// ScrollRects consume drags before they can bubble up here (UGUI sends
    /// drags only to the innermost handler), so each gets a relay that mirrors
    /// its drag events to this handler — same idea as NestedScrollRect's
    /// route-to-parent. The relay self-guards: a vertical scroller always
    /// relays, while a horizontal one (page slider) relays only when it cannot
    /// scroll itself (a single page), so swiping a one-page slider still
    /// switches the tab. Call again if a tab spawns new ScrollRects after startup.
    /// </summary>
    public void AttachScrollRectRelays()
    {
        foreach (var scrollRect in GetComponentsInChildren<ScrollRect>(true))
        {
            var relay = scrollRect.GetComponent<ScrollRectSwipeRelay>();
            if (relay == null) relay = scrollRect.gameObject.AddComponent<ScrollRectSwipeRelay>();
            // Re-init instead of skipping: a pooled scroller can come back holding
            // a relay bound to a swipe handler from a previous scene load.
            relay.Init(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (activePointerId != NO_POINTER) return; // another finger owns the gesture
        if (navigateBar == null) return;

        activePointerId = eventData.pointerId;
        startTab = navigateBar.CurrTab;
        axis = GestureAxis.Undecided;
        previewDriving = false;
        slideBar = navigateBar as UINavigateBarSlide;
        sampleHead = 0;
        samplesStored = 0;
        RecordSample(eventData.position.x);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        RecordSample(eventData.position.x);

        Vector2 travel = eventData.position - eventData.pressPosition;
        if (axis == GestureAxis.Undecided)
        {
            float slop = Screen.width * AXIS_LOCK_TRAVEL_RATIO;
            if (Mathf.Abs(travel.x) >= slop || Mathf.Abs(travel.y) >= slop)
            {
                axis = Mathf.Abs(travel.x) >= Mathf.Abs(travel.y)
                    ? GestureAxis.Horizontal
                    : GestureAxis.Vertical;
                if (axis == GestureAxis.Horizontal && slideBar != null)
                    previewDriving = slideBar.BeginSwipePreview();
            }
        }

        if (previewDriving)
            slideBar.UpdateSwipePreview(travel.x);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        activePointerId = NO_POINTER;

        bool commit = ShouldCommit(eventData, out int direction);

        if (previewDriving)
        {
            previewDriving = false;
            // The preview owns the visuals; it finishes the slide on commit or
            // snaps back on cancel. No-ops safely if a scripted SwitchTab
            // already cancelled the preview mid-gesture.
            slideBar.EndSwipePreview(commit);
            return;
        }

        if (commit) StepTab(direction);
    }

    private bool ShouldCommit(PointerEventData eventData, out int direction)
    {
        direction = 0;
        if (navigateBar == null) return false;
        // A popup or scripted tab change mid-gesture invalidates the swipe.
        if (navigateBar.CurrTab != startTab) return false;
        if (IsReleaseOutsideTabArea(eventData)) return false;

        Vector2 travel = eventData.position - eventData.pressPosition;
        if (Mathf.Abs(travel.x) <= Mathf.Abs(travel.y)) return false;

        // Swipe left brings in the tab on the right (matches UINavigateBarSlide direction).
        direction = travel.x < 0 ? 1 : -1;

        float width = Screen.width;
        if (Mathf.Abs(travel.x) >= width * switchThreshold) return true;
        if (Mathf.Abs(travel.x) < width * MIN_FLICK_TRAVEL_RATIO) return false;

        float speed = ReleaseSpeed(eventData.position.x);
        return Mathf.Abs(speed) >= width * flickSpeedThreshold
               && Mathf.Approximately(Mathf.Sign(speed), Mathf.Sign(travel.x));
    }

    // True when the release lands on UI outside this container (e.g. a popup that
    // opened mid-drag), so the gesture is no longer a tab swipe.
    private bool IsReleaseOutsideTabArea(PointerEventData eventData)
    {
        var hit = eventData.pointerCurrentRaycast.gameObject;
        return hit != null && !hit.transform.IsChildOf(transform);
    }

    private void RecordSample(float x)
    {
        sampleTimes[sampleHead] = Time.unscaledTime;
        sampleX[sampleHead] = x;
        sampleHead = (sampleHead + 1) % SAMPLE_COUNT;
        if (samplesStored < SAMPLE_COUNT) samplesStored++;
    }

    // Horizontal speed over the last FLICK_SAMPLE_WINDOW. Holding still before
    // release leaves a stale sample, so the speed collapses toward zero.
    private float ReleaseSpeed(float releaseX)
    {
        if (samplesStored == 0) return 0f;

        float now = Time.unscaledTime;
        float refTime = 0f;
        float refX = releaseX;
        for (int i = 0; i < samplesStored; i++)
        {
            int index = (sampleHead - 1 - i + SAMPLE_COUNT) % SAMPLE_COUNT;
            refTime = sampleTimes[index];
            refX = sampleX[index];
            if (now - refTime >= FLICK_SAMPLE_WINDOW) break;
        }

        float dt = now - refTime;
        if (dt < 0.016f) return 0f;
        return (releaseX - refX) / dt;
    }

    private void StepTab(int direction)
    {
        var types = navigateBar.navigationTypes;
        if (types == null || types.Count == 0) return;

        int current = types.IndexOf(navigateBar.CurrTab);
        if (current < 0) return;

        int next = current + direction;
        if (next < 0 || next >= types.Count) return;

        navigateBar.SwitchTab(types[next]);
    }
}
