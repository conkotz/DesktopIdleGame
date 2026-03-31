using UnityEngine;

public class SmartDockPanel : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform inventoryRect; // InventoryWindow
    [SerializeField] private RectTransform clampRect;     // WindowsArea (or Canvas root)

    [Header("Layout")]
    [SerializeField] private float gap = 10f;

    private RectTransform _rt;
    private RectTransform _parent;

    private void Awake()
    {
        _rt = transform as RectTransform;
        _parent = _rt ? _rt.parent as RectTransform : null;
    }

    private void LateUpdate()
    {
        if (!_rt || !_parent || !inventoryRect || !clampRect) return;

        float panelW = _rt.rect.width;
        float panelH = _rt.rect.height;

        // Inventory bounds in parent's local space
        var invLocal = GetRectInLocalSpace(inventoryRect, _parent);
        float invLeft = invLocal.xMin;
        float invRight = invLocal.xMax;
        float invCenterY = invLocal.center.y;

        // Clamp bounds in parent's local space
        var clampLocal = GetRectInLocalSpace(clampRect, _parent);
        float clampLeft = clampLocal.xMin;
        float clampRight = clampLocal.xMax;
        float clampBottom = clampLocal.yMin;
        float clampTop = clampLocal.yMax;

        // Try right side, else left side
        float xRight = invRight + gap;
        float xLeft = invLeft - gap - panelW;

        bool fitsRight = (xRight + panelW) <= clampRight;
        bool fitsLeft = xLeft >= clampLeft;

        float x = xRight;
        if (!fitsRight && fitsLeft) x = xLeft;

        // Clamp inside bounds if neither fits perfectly
        x = Mathf.Clamp(x, clampLeft, clampRight - panelW);

        // Vertically center on inventory, clamp to bounds
        float y = invCenterY - panelH * 0.5f;
        y = Mathf.Clamp(y, clampBottom, clampTop - panelH);

        // Since our panel pivot should be (0,0.5) OR (0,0), we'll set pivot-safe anchored position:
        // We'll assume pivot = (0,0.5) (recommended).
        _rt.anchoredPosition = new Vector2(x, invCenterY);
    }

    private static Rect GetRectInLocalSpace(RectTransform rt, RectTransform relativeTo)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        for (int i = 0; i < 4; i++)
            corners[i] = relativeTo.InverseTransformPoint(corners[i]);

        float xMin = corners[0].x;
        float xMax = corners[2].x;
        float yMin = corners[0].y;
        float yMax = corners[2].y;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}