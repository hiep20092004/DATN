using DG.Tweening;
using UnityEngine;

/// <summary>
/// Short Z-axis swing for locked UI icons (DOTween).
/// </summary>
public class LockSwingTween
{
    private const float DefaultSwingAngle = 12f;
    private const float DefaultSwingDuration = 0.08f;
    private const int DefaultLoopCount = 4;

    public void Play(
        RectTransform target,
        float swingAngle = DefaultSwingAngle,
        float duration = DefaultSwingDuration,
        int loops = DefaultLoopCount)
    {
        if (!target) return;

        target.DOKill();
        target.localRotation = Quaternion.identity;

        target.localRotation = Quaternion.Euler(0f, 0f, -swingAngle);
        target
            .DOLocalRotate(new Vector3(0f, 0f, swingAngle), duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(loops, LoopType.Yoyo)
            .OnComplete(() => target.localRotation = Quaternion.identity);
    }
}
