using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public sealed class StripUIViewportFollower : MonoBehaviour
{
    [SerializeField] private Camera stripCamera;
    [SerializeField] private RectTransform targetRect;

    private Rect _lastRect = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);

    /// <summary>
    /// Rect whose anchors track <see cref="Camera.rect"/> — may differ from <see cref="Component.transform"/> when this component lives on a manager object.
    /// </summary>
    public RectTransform ViewportAlignedRect
    {
        get
        {
            if (!targetRect)
                targetRect = transform as RectTransform;
            return targetRect;
        }
    }

    private void OnEnable()
    {
        CacheTarget();
        Apply(force: true);
    }

    private void OnValidate()
    {
        CacheTarget();
        Apply(force: true);
    }

    private void LateUpdate()
    {
        Apply(force: false);
    }

    private void CacheTarget()
    {
        if (!targetRect)
            targetRect = transform as RectTransform;
    }

    private void Apply(bool force)
    {
        CacheTarget();

        if (!stripCamera || !targetRect)
            return;

        Rect rect = stripCamera.rect;
        if (!force && Approximately(rect, _lastRect))
            return;

        targetRect.anchorMin = new Vector2(rect.xMin, rect.yMin);
        targetRect.anchorMax = new Vector2(rect.xMax, rect.yMax);
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;

        _lastRect = rect;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }
}
