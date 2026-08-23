using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Enums;
using UnityEngine;
using UnityEngine.UI;

public class UINavigateBarSlide : UINavigateBarBase
{
    private RectTransform[] tabRectTransforms;
    [SerializeField] private float animationDuration = 0.55f;
    // Authorable slide curve: smooth gradual start, then settle into the target.
    // Falls back to animationEase only if the curve is emptied in the inspector.
    [SerializeField] private AnimationCurve slideCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 1.8f),
        new Keyframe(0.5f, 0.88f, 0.5f, 0.5f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private Ease animationEase = Ease.OutExpo;

    [Tooltip("Static full-screen background revealed during edge over-drag. One covers both the first and last tab; forced behind the tabs at startup.")]
    [SerializeField] private RectTransform overDragBackground;
    [Tooltip("Edge over-drag resistance when there is no next/previous tab. 0 = hard stop, higher = more rubber-band.")]
    [SerializeField, Range(0f, 1f)] private float edgeDragDamping = 0.12f;

    private Vector2[] tabOriginalPositions;
    private Vector2 centerPosition;
    private float slideOffset;
    private Canvas parentCanvas;

    // The commit switches currTab immediately; the leftover slide runs as an
    // interruptible "settle" so rapid back-to-back swipes never stall.
    private bool settling;
    private int settleOutIndex = -1;
    private NavigationType settleOutType;
    private Vector2 settleOutTarget;
    private int settleToken;
    public bool IsAnimating => settling;

    private bool previewing;
    private int previewCurrentIndex = -1;
    private int previewNeighborIndex = -1;
    // X the current tab had when the preview began (non-zero when grabbed
    // mid-slide); added to finger travel so it picks up without snapping.
    private float previewBaseX;
    public bool IsSwipePreviewing => previewing;

    private void Start()
    {
        Init();
        SetFirstTab(tabStart);
    }

    public override void Init()
    {
        base.Init();
        tabRectTransforms = new RectTransform[tabs.Count];
        int m = 0;
        foreach (var tab in tabs.Values)
        {
            tabRectTransforms[m] = tab.GetComponent<RectTransform>();
            m++;
        }

        tabOriginalPositions = new Vector2[tabRectTransforms.Length];
        for (int i = 0; i < tabRectTransforms.Length; i++)
        {
            tabOriginalPositions[i] = tabRectTransforms[i].anchoredPosition;
        }

        if (tabRectTransforms.Length > 0)
        {
            centerPosition = new Vector2(0, tabRectTransforms[0].anchoredPosition.y);
            parentCanvas = GetComponentInParent<Canvas>();
            UpdateSlideOffset();
        }

        // Keep the over-drag background behind every tab. It has no UITabBase so
        // base.Init never counts it as a tab; this only fixes its render order.
        if (overDragBackground != null) overDragBackground.SetAsFirstSibling();
    }

    private void UpdateSlideOffset()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        // rootCanvas carries the CanvasScaler factor, so this stays correct
        // across every scaling mode and aspect ratio.
        slideOffset = parentCanvas != null
            ? Screen.width / parentCanvas.rootCanvas.scaleFactor
            : Screen.width;
    }

    private Tweener SetSlideEase(Tweener tween)
    {
        if (slideCurve != null && slideCurve.length >= 2) return tween.SetEase(slideCurve);
        return tween.SetEase(animationEase);
    }

    public override void SetFirstTab(NavigationType tab)
    {
        base.SetFirstTab(tab);
        foreach (var item in items)
        {
            if (item.Key == tab) item.Value.OnSelected();
            else item.Value.OnDeselected();
        }

        highlight.transform.SetParent(items[tab].transform);
        highlight.SetAsFirstSibling();
        // Center on X only; leave Y untouched so the highlight keeps whatever
        // vertical position it inherits from the tab item.
        var highlightPos = highlight.localPosition;
        highlight.localPosition = new Vector3(0f, highlightPos.y, highlightPos.z);
        var rect = items[tab].GetComponent<RectTransform>();
        highlight.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.rect.width * 1.1f);

        tabs[tab].gameObject.SetActive(true);
        tabs[tab].OnShow();
        currTab = tab;
    }

    public void SwitchToHome()
    {
        SwitchTab(NavigationType.Home);
    }

    public bool BeginSwipePreview()
    {
        if (previewing) return false;

        // Finish any in-flight settle so this gesture starts immediately and
        // takes over from the tab's current position instead of snapping back.
        FinishSettle();

        previewCurrentIndex = navigationTypes.IndexOf(currTab);
        if (previewCurrentIndex < 0) return false;

        UpdateSlideOffset();

        var currentRect = tabRectTransforms[previewCurrentIndex];
        currentRect.DOKill();
        previewBaseX = currentRect.anchoredPosition.x;

        previewNeighborIndex = -1;
        previewing = true;
        return true;
    }

    public void UpdateSwipePreview(float screenDx)
    {
        if (!previewing) return;

        float canvasDx = screenDx * (slideOffset / Screen.width);

        if (screenDx != 0f)
        {
            int wantedNeighbor = previewCurrentIndex + (screenDx < 0f ? 1 : -1);
            if (wantedNeighbor < 0 || wantedNeighbor >= navigationTypes.Count) wantedNeighbor = -1;

            if (wantedNeighbor != previewNeighborIndex)
            {
                HidePreviewNeighbor(instant: false);
                previewNeighborIndex = wantedNeighbor;
                if (previewNeighborIndex >= 0)
                {
                    tabRectTransforms[previewNeighborIndex].DOKill();
                    ShowTabInstant(navigationTypes[previewNeighborIndex]);
                }
            }
        }

        // No neighbor at the edge: damp the over-drag (0 = hard stop). The gap it
        // reveals is filled by overDragBackground so it never shows black.
        if (previewNeighborIndex < 0) canvasDx *= edgeDragDamping;

        float x = previewBaseX + canvasDx;
        var currentRect = tabRectTransforms[previewCurrentIndex];
        currentRect.anchoredPosition = new Vector2(x, tabOriginalPositions[previewCurrentIndex].y);

        if (previewNeighborIndex >= 0)
        {
            float side = previewNeighborIndex > previewCurrentIndex ? slideOffset : -slideOffset;
            tabRectTransforms[previewNeighborIndex].anchoredPosition =
                new Vector2(x + side, tabOriginalPositions[previewNeighborIndex].y);
        }
    }

    public void EndSwipePreview(bool commit)
    {
        if (!previewing) return;
        previewing = false;

        int fromIndex = previewCurrentIndex;
        int toIndex = previewNeighborIndex;
        var fromRect = tabRectTransforms[fromIndex];

        if (!commit || toIndex < 0)
        {
            fromRect.DOKill();
            SetSlideEase(fromRect.DOAnchorPos(tabOriginalPositions[fromIndex], animationDuration));

            if (toIndex >= 0)
            {
                float side = toIndex > fromIndex ? slideOffset : -slideOffset;
                Vector2 outTarget = new Vector2(side, tabOriginalPositions[toIndex].y);
                var neighborRect = tabRectTransforms[toIndex];
                neighborRect.DOKill();
                SetSlideEase(neighborRect.DOAnchorPos(outTarget, animationDuration));
                BeginSettle(toIndex, navigationTypes[toIndex], outTarget);
            }
            return;
        }

        var fromType = navigationTypes[fromIndex];
        var toType = navigationTypes[toIndex];

        foreach (var item in items)
        {
            if (item.Key == toType) item.Value.OnSelected();
            else item.Value.OnDeselected();
        }
        highlight.transform.SetParent(items[toType].transform);
        highlight.SetAsFirstSibling();
        highlight.DOLocalMoveX(0f, 0.2f);

        float exitSide = toIndex > fromIndex ? -slideOffset : slideOffset;
        Vector2 exitTarget = new Vector2(exitSide, tabOriginalPositions[fromIndex].y);
        var toRect = tabRectTransforms[toIndex];
        fromRect.DOKill();
        toRect.DOKill();
        SetSlideEase(fromRect.DOAnchorPos(exitTarget, animationDuration));
        SetSlideEase(toRect.DOAnchorPos(tabOriginalPositions[toIndex], animationDuration));

        // Switch now so a follow-up swipe can start before the slide finishes.
        currTab = toType;
        BeginSettle(fromIndex, fromType, exitTarget);
    }

    private void BeginSettle(int outIndex, NavigationType outType, Vector2 outTarget)
    {
        settling = true;
        settleOutIndex = outIndex;
        settleOutType = outType;
        settleOutTarget = outTarget;
        int token = ++settleToken;
        DOVirtual.DelayedCall(animationDuration, () =>
        {
            if (settleToken != token) return;
            tabs[outType].OnHide();
            settling = false;
            settleOutIndex = -1;
        });
    }

    private void FinishSettle()
    {
        if (!settling) return;
        settleToken++;
        settling = false;
        if (settleOutIndex >= 0)
        {
            var rect = tabRectTransforms[settleOutIndex];
            rect.DOKill();
            rect.anchoredPosition = settleOutTarget;
            tabs[settleOutType].OnHide();
        }
        settleOutIndex = -1;
    }

    // OnShow without the fade-in blink: the tab slides in with the finger, so a
    // fade from alpha 0 would flash. Killed the same frame, the fade never renders.
    private void ShowTabInstant(NavigationType type)
    {
        var tab = tabs[type];
        tab.OnShow();
        var canvasGroup = tab.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 1f;
        }
    }

    private void HidePreviewNeighbor(bool instant)
    {
        if (previewNeighborIndex < 0) return;
        var type = navigationTypes[previewNeighborIndex];
        tabRectTransforms[previewNeighborIndex].DOKill();
        tabs[type].OnHide();
        if (instant)
        {
            var canvasGroup = tabs[type].GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.DOKill(true);
        }
        previewNeighborIndex = -1;
    }

    private void CancelSwipePreviewImmediate()
    {
        if (!previewing) return;
        previewing = false;
        HidePreviewNeighbor(instant: true);
        var rect = tabRectTransforms[previewCurrentIndex];
        rect.DOKill();
        rect.anchoredPosition = tabOriginalPositions[previewCurrentIndex];
    }

    public override void SwitchTab(NavigationType type)
    {
        if (previewing) CancelSwipePreviewImmediate();
        FinishSettle();
        if (type == currTab) return;

        var lastTab = currTab;
        UpdateSlideOffset();

        int prevIndex = navigationTypes.IndexOf(currTab);
        int newIndex = navigationTypes.IndexOf(type);

        Vector2 outTarget = Vector2.zero;
        bool hasOutgoing = prevIndex >= 0 && prevIndex < tabRectTransforms.Length;
        if (hasOutgoing)
        {
            bool animateToRight = newIndex < prevIndex;
            outTarget = animateToRight
                ? new Vector2(slideOffset, tabOriginalPositions[prevIndex].y)
                : new Vector2(-slideOffset, tabOriginalPositions[prevIndex].y);

            tabRectTransforms[prevIndex].DOKill();
            SetSlideEase(tabRectTransforms[prevIndex].DOAnchorPos(outTarget, animationDuration));
        }

        foreach (var item in items)
        {
            if (item.Key == type) item.Value.OnSelected();
            else item.Value.OnDeselected();
        }

        highlight.transform.SetParent(items[type].transform);
        highlight.SetAsFirstSibling();
        highlight.DOLocalMoveX(0f, 0.2f);

        if (newIndex >= 0 && newIndex < tabRectTransforms.Length)
        {
            bool comingFromRight = newIndex > prevIndex;
            Vector2 startPosition = comingFromRight
                ? new Vector2(slideOffset, tabOriginalPositions[newIndex].y)
                : new Vector2(-slideOffset, tabOriginalPositions[newIndex].y);

            tabRectTransforms[newIndex].DOKill();
            tabRectTransforms[newIndex].anchoredPosition = startPosition;
            tabs[type].gameObject.SetActive(true);
            tabs[type].OnShow();

            SetSlideEase(tabRectTransforms[newIndex].DOAnchorPos(centerPosition, animationDuration));
        }

        currTab = type;
        if (hasOutgoing) BeginSettle(prevIndex, lastTab, outTarget);
    }
}
