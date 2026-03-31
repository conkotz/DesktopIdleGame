using UnityEngine;

public class FlipInsideBounds : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform panel;          // Tooltip panel (this)
    [SerializeField] private RectTransform boundsRect;     // WindowsArea (visible bounds)
    [SerializeField] private RectTransform measureRect;    // WINDOW (recommended) or slot/anchor
    [SerializeField] private RectTransform heightRect;     // Grid/content used to clamp height

    public enum PreferredSide { Left, Right }

    [Header("Preference (only used when BOTH sides fit)")]
    [SerializeField] private PreferredSide preferredSide = PreferredSide.Right;

    [Header("Layout")]
    [SerializeField] private float gap = 10f;

    // IMPORTANT: use a stable width (your original approach)
    [SerializeField] private float panelWidth = 240f;

    private RectTransform ParentRect => panel ? panel.parent as RectTransform : null;

    public void SetPreferredSide(PreferredSide side) => preferredSide = side;
    public void SetMeasureRect(RectTransform r) => measureRect = r;
    public void SetHeightRect(RectTransform r) => heightRect = r;

    private void Reset()
    {
        panel = transform as RectTransform;
    }

    private void LateUpdate()
    {
        if (!panel || !boundsRect) return;

        var parent = ParentRect;
        if (!parent) return;

        // Fallbacks
        var m = measureRect ? measureRect : parent;
        var h = heightRect ? heightRect : parent;

        // 1) Space calc (world)
        Vector3[] mc = new Vector3[4];
        Vector3[] bc = new Vector3[4];
        m.GetWorldCorners(mc);
        boundsRect.GetWorldCorners(bc);

        float mRight = mc[2].x;
        float mLeft = mc[0].x;

        float boundsRight = bc[2].x;
        float boundsLeft = bc[0].x;

        float spaceRight = boundsRight - mRight - gap;
        float spaceLeft = mLeft - boundsLeft - gap;

        bool canDockRight = spaceRight >= panelWidth;
        bool canDockLeft = spaceLeft >= panelWidth;

        bool dockRight;

        // ✅ ORIGINAL behaviour:
        // - If only one side fits, take it (this is the "flip near edge" behaviour)
        // - If neither fits, take the side with more space
        // - If BOTH fit, use preferredSide
        if (canDockRight && !canDockLeft)
        {
            dockRight = true;
        }
        else if (canDockLeft && !canDockRight)
        {
            dockRight = false;
        }
        else if (canDockRight && canDockLeft)
        {
            dockRight = (preferredSide == PreferredSide.Right);
        }
        else
        {
            dockRight = spaceRight >= spaceLeft;
        }

        // 2) Clamp Y to heightRect in parent space
        GetNormalizedYAnchors(parent, h, out float yMin, out float yMax);

        // 3) Apply
        if (dockRight) DockRight(yMin, yMax);
        else DockLeft(yMin, yMax);
    }

    private void DockRight(float yMin, float yMax)
    {
        panel.anchorMin = new Vector2(1f, yMin);
        panel.anchorMax = new Vector2(1f, yMax);
        panel.pivot = new Vector2(0f, 0.5f);

        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        panel.anchoredPosition = new Vector2(gap, 0f);

        panel.offsetMin = new Vector2(panel.offsetMin.x, 0f);
        panel.offsetMax = new Vector2(panel.offsetMax.x, 0f);
    }

    private void DockLeft(float yMin, float yMax)
    {
        panel.anchorMin = new Vector2(0f, yMin);
        panel.anchorMax = new Vector2(0f, yMax);
        panel.pivot = new Vector2(1f, 0.5f);

        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        panel.anchoredPosition = new Vector2(-gap, 0f);

        panel.offsetMin = new Vector2(panel.offsetMin.x, 0f);
        panel.offsetMax = new Vector2(panel.offsetMax.x, 0f);
    }

    private static void GetNormalizedYAnchors(RectTransform parent, RectTransform target, out float yMin, out float yMax)
    {
        Vector3[] tc = new Vector3[4];
        target.GetWorldCorners(tc);

        float localBottomY = parent.InverseTransformPoint(tc[0]).y;
        float localTopY = parent.InverseTransformPoint(tc[1]).y;

        Rect pr = parent.rect;
        float height = pr.height;

        yMin = Mathf.Clamp01((localBottomY - pr.yMin) / height);
        yMax = Mathf.Clamp01((localTopY - pr.yMin) / height);

        if (yMax < yMin)
        {
            float tmp = yMin;
            yMin = yMax;
            yMax = tmp;
        }
    }
}