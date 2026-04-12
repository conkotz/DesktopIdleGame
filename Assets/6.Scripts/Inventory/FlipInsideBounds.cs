using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Positions a tooltip panel left or right of a measured anchor so it stays inside <see cref="boundsRect"/>.
/// Width/height can come from laid-out rects (dynamic) or fixed fallbacks — see <see cref="GetMeasuredWidth"/> / <see cref="GetMeasuredHeight"/>.
/// </summary>
public class FlipInsideBounds : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform panel;          // Tooltip root (positioning; usually this object)
    [SerializeField] private RectTransform boundsRect;     // Visible window / safe area (screen bounds)
    [Tooltip("World-space anchor for left/right gap (hovered slot/window). Set at runtime; not used for tooltip width.")]
    [SerializeField] private RectTransform measureRect;
    [Tooltip("Vertical clamp target for Y anchoring (optional). If null, parent is used.")]
    [SerializeField] private RectTransform heightRect;
    [Tooltip("Laid-out tooltip box (e.g. Content with VLG + CSF). Width/height come from here after rebuild. If null, width uses panelWidth / panel.")]
    [SerializeField] private RectTransform tooltipContentRect;

    public enum PreferredSide { Left, Right }

    [Header("Preference (only used when BOTH sides fit)")]
    [SerializeField] private PreferredSide preferredSide = PreferredSide.Right;

    [Header("Layout")]
    [SerializeField] private float gap = 10f;

    [Header("Fixed fallback width (when MeasureRect is not used for width)")]
    [Tooltip("When MeasureRect is null: if > 0, used as fixed width. If 0, width comes from panel.rect after layout rebuild.")]
    [SerializeField] private float panelWidth = 240f;

    [Header("Rarity border strip (optional)")]
    [Tooltip("Child strip Image; auto-found as \"RarityBorder\" under Panel if empty.")]
    [SerializeField] private RectTransform rarityBorderStrip;
    [SerializeField] private float rarityBorderWidth = 6f;
    [SerializeField] private float rarityBorderOutwardOffset = 6f;

    private RectTransform ParentRect => panel ? panel.parent as RectTransform : null;

    public void SetPreferredSide(PreferredSide side) => preferredSide = side;
    public void SetMeasureRect(RectTransform r) => measureRect = r;
    public void SetHeightRect(RectTransform r) => heightRect = r;

    private void Reset()
    {
        panel = transform as RectTransform;
    }

    private void Awake()
    {
        if (!rarityBorderStrip && panel)
        {
            Transform t = panel.Find("RarityBorder");
            if (t)
                rarityBorderStrip = t as RectTransform;
        }
    }

    private void LateUpdate()
    {
        if (!panel || !boundsRect) return;

        var parent = ParentRect;
        if (!parent) return;

        if (!rarityBorderStrip && panel)
        {
            Transform t = panel.Find("RarityBorder");
            if (t)
                rarityBorderStrip = t as RectTransform;
        }

        RebuildLayoutForMeasurement();

        float measuredWidth = GetMeasuredWidth();
        float measuredHeight = GetMeasuredHeight();
        bool useExplicitContentSize = tooltipContentRect != null || heightRect != null;

        // World-space anchor for gap (slot / UI element), not tooltip box size
        var m = measureRect ? measureRect : parent;
        var h = heightRect ? heightRect : parent;

        // 1) Space calc (world) — use measured tooltip width so flip logic matches real size
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

        bool canDockRight = spaceRight >= measuredWidth;
        bool canDockLeft = spaceLeft >= measuredWidth;

        bool dockRight;

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

        // 2) Clamp Y to heightRect in parent space (unchanged)
        GetNormalizedYAnchors(parent, h, out float yMin, out float yMax);

        // 3) Apply dock + size
        if (dockRight)
            DockRight(yMin, yMax, measuredWidth, measuredHeight, useExplicitContentSize);
        else
            DockLeft(yMin, yMax, measuredWidth, measuredHeight, useExplicitContentSize);

        ApplyRarityBorderToInnerEdge(dockRight);
    }

    /// <summary>
    /// Ensures ContentSizeFitter / layout groups have applied before we read rect sizes.
    /// </summary>
    private void RebuildLayoutForMeasurement()
    {
        Canvas.ForceUpdateCanvases();

        if (tooltipContentRect && tooltipContentRect != panel)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipContentRect);
        if (heightRect && heightRect != tooltipContentRect && heightRect != panel)
            LayoutRebuilder.ForceRebuildLayoutImmediate(heightRect);
        if (panel)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Laid-out width: <see cref="tooltipContentRect"/> first, then fixed <see cref="panelWidth"/> if &gt; 0, else <see cref="panel"/>.
    /// </summary>
    private float GetMeasuredWidth()
    {
        if (tooltipContentRect)
            return Mathf.Max(0f, tooltipContentRect.rect.width);

        if (panelWidth > 0f)
            return panelWidth;

        if (panel)
            return Mathf.Max(0f, panel.rect.width);

        return 0f;
    }

    /// <summary>
    /// Laid-out height: <see cref="tooltipContentRect"/> first, then <see cref="heightRect"/>, else <see cref="panel"/>.
    /// </summary>
    private float GetMeasuredHeight()
    {
        if (tooltipContentRect)
            return Mathf.Max(0f, tooltipContentRect.rect.height);

        if (heightRect)
            return Mathf.Max(0f, heightRect.rect.height);

        if (panel)
            return Mathf.Max(0f, panel.rect.height);

        return 0f;
    }

    /// <summary>
    /// Puts the rarity strip on the edge of the tooltip that faces the anchor (hovered cell).
    /// Dock right → strip on the left; dock left → strip on the right.
    /// </summary>
    private void ApplyRarityBorderToInnerEdge(bool dockRight)
    {
        if (!rarityBorderStrip)
            return;

        float w = rarityBorderWidth;
        float o = rarityBorderOutwardOffset;

        if (dockRight)
        {
            rarityBorderStrip.anchorMin = new Vector2(0f, 0f);
            rarityBorderStrip.anchorMax = new Vector2(0f, 1f);
            rarityBorderStrip.pivot = new Vector2(0f, 0.5f);
            rarityBorderStrip.anchoredPosition = new Vector2(-o, 0f);
            rarityBorderStrip.sizeDelta = new Vector2(w, 0f);
        }
        else
        {
            rarityBorderStrip.anchorMin = new Vector2(1f, 0f);
            rarityBorderStrip.anchorMax = new Vector2(1f, 1f);
            rarityBorderStrip.pivot = new Vector2(1f, 0.5f);
            rarityBorderStrip.anchoredPosition = new Vector2(o, 0f);
            rarityBorderStrip.sizeDelta = new Vector2(w, 0f);
        }
    }

    private void DockRight(
        float yMin,
        float yMax,
        float measuredWidth,
        float measuredHeight,
        bool useExplicitContentSize)
    {
        if (useExplicitContentSize)
        {
            float yCenter = (yMin + yMax) * 0.5f;
            panel.anchorMin = new Vector2(1f, yCenter);
            panel.anchorMax = new Vector2(1f, yCenter);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(gap, 0f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, measuredWidth);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, measuredHeight);
        }
        else
        {
            panel.anchorMin = new Vector2(1f, yMin);
            panel.anchorMax = new Vector2(1f, yMax);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, measuredWidth);
            panel.anchoredPosition = new Vector2(gap, 0f);
            panel.offsetMin = new Vector2(panel.offsetMin.x, 0f);
            panel.offsetMax = new Vector2(panel.offsetMax.x, 0f);
        }
    }

    private void DockLeft(
        float yMin,
        float yMax,
        float measuredWidth,
        float measuredHeight,
        bool useExplicitContentSize)
    {
        if (useExplicitContentSize)
        {
            float yCenter = (yMin + yMax) * 0.5f;
            panel.anchorMin = new Vector2(0f, yCenter);
            panel.anchorMax = new Vector2(0f, yCenter);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-gap, 0f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, measuredWidth);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, measuredHeight);
        }
        else
        {
            panel.anchorMin = new Vector2(0f, yMin);
            panel.anchorMax = new Vector2(0f, yMax);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, measuredWidth);
            panel.anchoredPosition = new Vector2(-gap, 0f);
            panel.offsetMin = new Vector2(panel.offsetMin.x, 0f);
            panel.offsetMax = new Vector2(panel.offsetMax.x, 0f);
        }
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
