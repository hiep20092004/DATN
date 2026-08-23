using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a fullscreen UI Image that uses the "UI/Iris Hole" shader.
/// Animates the circular hole from a radius that fully covers the overlay
/// down to a target rect (or a fixed radius), then invokes a callback.
///
/// All distances passed to the shader are converted into the overlay's local
/// mesh space so the math matches the shader's <c>worldPosition</c>
/// (which is actually <c>v.vertex</c> in object space) regardless of canvas
/// render mode (Overlay / Screen-Space Camera / World Space).
/// </summary>
[DisallowMultipleComponent]
public class UIIrisWipeController : MonoBehaviour
{
    private static readonly int CenterId   = Shader.PropertyToID("_Center");
    private static readonly int RadiusId   = Shader.PropertyToID("_Radius");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    [Header("Refs")]
    [Tooltip("Fullscreen Image dùng material UI/Iris Hole.")]
    [SerializeField] private Image overlay;

    [Header("Animation")]
    [Tooltip("Radius cuối (đơn vị overlay local). 0 = tự tính theo target rect.")]
    [SerializeField] private float endRadius = 0f;
    [Tooltip("Đệm thêm vào endRadius khi auto-compute từ target rect.")]
    [SerializeField] private float endPadding = 32f;
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private Ease  ease = Ease.InOutCubic;
    [Tooltip("Độ mờ mép vòng tròn (đơn vị overlay local).")]
    [SerializeField] private float softness = 8f;
    [Tooltip("Tween chạy được khi Time.timeScale = 0 (popup pause game).")]
    [SerializeField] private bool ignoreTimeScale = true;

    private Material _materialInstance;
    private Tween    _activeTween;

    public Image Overlay => overlay;
    public bool IsPlaying => _activeTween != null && _activeTween.IsActive() && _activeTween.IsPlaying();

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Stop();
        if (_materialInstance != null)
        {
            Destroy(_materialInstance);
            _materialInstance = null;
        }
    }

    /// <summary>
    /// Play the shrink animation centered on a UI rect. Returns the tween so
    /// callers can chain (e.g. <c>.OnComplete(...)</c>).
    /// </summary>
    public Tween Play(RectTransform target, System.Action onComplete = null)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return null;
        }

        Vector3 centerWorld = target.position;
        float resolvedEnd = endRadius > 0f
            ? endRadius
            : ComputeEndRadiusLocal(target) + endPadding;

        return Play(centerWorld, resolvedEnd, onComplete);
    }

    /// <summary>
    /// Play the shrink animation centered on a world position.
    /// </summary>
    public Tween Play(Vector3 worldCenter, float endRadiusOverride, System.Action onComplete = null)
    {
        Stop();

        Material mat = GetMaterial();
        RectTransform overlayRT = overlay != null ? overlay.rectTransform : null;
        if (mat == null || overlayRT == null)
        {
            onComplete?.Invoke();
            return null;
        }

        Vector2 centerLocal = overlayRT.InverseTransformPoint(worldCenter);
        float startRadius = ComputeStartRadiusLocal(overlayRT, centerLocal);

        mat.SetVector(CenterId, new Vector4(centerLocal.x, centerLocal.y, 0, 0));
        mat.SetFloat(SoftnessId, softness);
        mat.SetFloat(RadiusId, startRadius);

        _activeTween = DOTween.To(
                () => mat.GetFloat(RadiusId),
                v  => mat.SetFloat(RadiusId, v),
                endRadiusOverride,
                duration
            )
            .SetEase(ease)
            .SetUpdate(ignoreTimeScale)
            .OnComplete(() => onComplete?.Invoke());

        return _activeTween;
    }

    /// <summary>
    /// Stop any running tween. Material values stay where they are.
    /// </summary>
    public void Stop()
    {
        if (_activeTween != null)
        {
            _activeTween.Kill();
            _activeTween = null;
        }
    }

    /// <summary>
    /// Force the overlay to fully covered state (no hole) at given world center.
    /// Useful to "reset" the effect before <see cref="Play"/>.
    /// </summary>
    public void SetCovered(Vector3 worldCenter)
    {
        Material mat = GetMaterial();
        RectTransform overlayRT = overlay != null ? overlay.rectTransform : null;
        if (mat == null || overlayRT == null) return;

        Vector2 centerLocal = overlayRT.InverseTransformPoint(worldCenter);
        mat.SetVector(CenterId, new Vector4(centerLocal.x, centerLocal.y, 0, 0));
        mat.SetFloat(SoftnessId, softness);
        mat.SetFloat(RadiusId, ComputeStartRadiusLocal(overlayRT, centerLocal));
    }

    private Material GetMaterial()
    {
        if (overlay == null) return null;
        if (_materialInstance == null)
        {
            _materialInstance = new Material(overlay.material);
            overlay.material = _materialInstance;
        }
        return _materialInstance;
    }

    /// <summary>
    /// Distance (overlay local space) from center to the farthest overlay corner.
    /// Guarantees the hole fully hides the overlay at start.
    /// </summary>
    private static float ComputeStartRadiusLocal(RectTransform overlayRT, Vector2 centerLocal)
    {
        Rect rect = overlayRT.rect;
        Vector2 c0 = new Vector2(rect.xMin, rect.yMin);
        Vector2 c1 = new Vector2(rect.xMin, rect.yMax);
        Vector2 c2 = new Vector2(rect.xMax, rect.yMin);
        Vector2 c3 = new Vector2(rect.xMax, rect.yMax);

        float maxDist = Vector2.Distance(centerLocal, c0);
        maxDist = Mathf.Max(maxDist, Vector2.Distance(centerLocal, c1));
        maxDist = Mathf.Max(maxDist, Vector2.Distance(centerLocal, c2));
        maxDist = Mathf.Max(maxDist, Vector2.Distance(centerLocal, c3));
        return maxDist + 16f;
    }

    /// <summary>
    /// Half diagonal of <paramref name="target"/> expressed in overlay local space.
    /// </summary>
    private float ComputeEndRadiusLocal(RectTransform target)
    {
        RectTransform overlayRT = overlay != null ? overlay.rectTransform : null;
        if (overlayRT == null || target == null) return 100f;

        Vector3[] worldCorners = new Vector3[4];
        target.GetWorldCorners(worldCorners);

        Vector2 c0 = overlayRT.InverseTransformPoint(worldCorners[0]);
        Vector2 c2 = overlayRT.InverseTransformPoint(worldCorners[2]);
        return Vector2.Distance(c0, c2) * 0.5f;
    }
}
