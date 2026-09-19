using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class CanvasAutoLayer : MonoBehaviour
{
    [Header("Layer Settings")]
    public string layerName = "Default";
    public int layerIndex = -1;  // Nếu >=0 thì dùng index thay cho tên
    public bool applyToChildren = true;

    [Header("Canvas Sorting Settings")]
    public bool overrideSorting = true;
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    private string primativeLayer;
    private int primativeLayerIndex;
    private string primativeSortingLayerName;
    private int primativeSortingOrder;


    private Canvas _canvas;

    void Awake()
    {
        ApplyLayer();
        ApplySorting();
        // Lưu lại layer và sorting gốc để có thể reset nếu cần
        primativeLayer = layerName;
        primativeLayerIndex = layerIndex;
        primativeSortingLayerName = sortingLayerName;
        primativeSortingOrder = sortingOrder;
    }

    private void ApplyLayer()
    {
        int targetLayer = -1;

        // Quy tắc: nếu layerIndex >= 0 → ưu tiên index
        if (layerIndex >= 0 && layerIndex < 32)
            targetLayer = layerIndex;
        else
            targetLayer = LayerMask.NameToLayer(layerName);

        if (targetLayer < 0)
        {
            Debug.LogWarning($"[CanvasAutoLayer] Layer không hợp lệ: {layerName}/{layerIndex}");
            return;
        }

        if (applyToChildren)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = targetLayer;
        }
        else
        {
            gameObject.layer = targetLayer;
        }
    }

    private void ApplySorting()
    {
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        _canvas.overrideSorting = overrideSorting;
        _canvas.sortingLayerName = sortingLayerName;
        _canvas.sortingOrder = sortingOrder;
    }

    public void SetLayerAndCanvasSorting(string newLayerName, string newSortingLayerName, int newSortingOrder)
    {
        layerName = newLayerName;
        sortingLayerName = newSortingLayerName;
        sortingOrder = newSortingOrder;
        ApplyLayer();
        ApplySorting();
    }

    public void ResetToPrimativeLayerAndSorting()
    {
        layerName = primativeLayer;
        layerIndex = primativeLayerIndex;
        sortingLayerName = primativeSortingLayerName;
        sortingOrder = primativeSortingOrder;
        ApplyLayer();
        ApplySorting();
    }
}
