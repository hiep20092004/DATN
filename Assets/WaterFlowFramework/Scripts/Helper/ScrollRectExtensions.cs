using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public static class ScrollRectSnapExtensions
{
    public static void SnapToCenter(this ScrollRect scrollRect, RectTransform target, float duration = 0.35f, float delay = 0f)
    {
        Canvas.ForceUpdateCanvases();

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport;

        // Convert world to local relative to content
        Vector2 viewportCenterLocal = content.InverseTransformPoint(
            viewport.TransformPoint(viewport.rect.center)
        );

        Vector2 targetCenterLocal = content.InverseTransformPoint(
            target.TransformPoint(target.rect.center)
        );

        // offset = vị trí viewportCenter - itemCenter
        Vector2 offset = viewportCenterLocal - targetCenterLocal;

        // Với pivot = (0,1), chỉ cần cộng offset vào anchoredPosition
        Vector2 newPos = content.anchoredPosition + offset;

        // Clamp vertical
        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        float minY = 0;
        float maxY = (contentHeight - viewportHeight);

        // Chỉ clamp nếu content > viewport
        if (contentHeight > viewportHeight)
            newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
        else
            newPos.y = 0;

        // Animate
        content.DOAnchorPos(newPos, duration).SetEase(Ease.OutCubic).SetDelay(delay);
    }
    
}