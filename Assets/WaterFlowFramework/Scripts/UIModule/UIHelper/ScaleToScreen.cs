using UnityEngine;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[RequireComponent(typeof(RectTransform))]
public class ScaleToScreen : MonoBehaviour
{
    [Header("Need to capture in design mode at context menu 'Capture Current As Design Pose' to get design scale and anchored position after arrange position")]
    [Header("If not capture, will use current scale and anchored position")]

    private RectTransform rect;
    private RectTransform parent;

    [Header("Design Size")]
    [SerializeField] private float designW = 1440f;
    [SerializeField] private float designH = 2404f;

    [Header("Design Transform")]
    [SerializeField] private Vector3 designScale = Vector3.one;
    [SerializeField] private Vector2 designAnchoredPosition;

    [Header("Options")]
    [SerializeField] private bool updateRuntime = false;

#if UNITY_EDITOR
    [Header("Turn off editDesignPose to update runtime scale and anchored position")]
    [SerializeField] private bool editDesignPose = false;
#endif

    private void Awake()
    {
        Init();
    }

    private void Start()
    {
        ApplyScale();
    }

    private void OnEnable()
    {
        Init();
        ApplyScale();
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyScale();
    }

#if UNITY_EDITOR
    private void Update()
    {
        Init();

        if (rect == null || parent == null)
            return;

        if (!Application.isPlaying)
        {
            if (editDesignPose)
            {
                CaptureCurrentAsDesignPose();
            }
            else
            {
                ApplyScale();
            }

            return;
        }

        if (updateRuntime)
            ApplyScale();
    }

    [ContextMenu("Capture Current As Design Pose")]
    private void CaptureCurrentAsDesignPose()
    {
        Init();

        if (rect == null || parent == null)
            return;

        float finalScale = GetFinalScale();

        if (Mathf.Approximately(finalScale, 0f))
            return;

        designScale = rect.localScale / finalScale;
        designAnchoredPosition = rect.anchoredPosition / finalScale;
    }

    [ContextMenu("Apply Scale")]
    private void ApplyScaleContext()
    {
        ApplyScale();
    }
#endif

    private void Init()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (parent == null && transform.parent != null)
            parent = transform.parent.GetComponent<RectTransform>();
    }

    private float GetFinalScale()
    {
        float screenW = parent.rect.width;
        float screenH = parent.rect.height;

        if (screenW <= 0f || screenH <= 0f || designW <= 0f || designH <= 0f)
            return 1f;

        float scaleW = screenW / designW;
        float scaleH = screenH / designH;

        // Envelope Parent / Cover
        return Mathf.Max(scaleW, scaleH);
    }

    private void ApplyScale()
    {
        Init();

        if (rect == null || parent == null)
            return;

        float finalScale = GetFinalScale();

        rect.localScale = designScale * finalScale;
        rect.anchoredPosition = designAnchoredPosition * finalScale;
    }
}