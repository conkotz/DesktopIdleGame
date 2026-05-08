using UnityEngine;
using UnityEngine.UI;

/// <summary>Endpoints for <see cref="SkillTreeConnectorUI"/> line placement (skill tree nodes, world map nodes, etc.).</summary>
public interface ITreeConnectorEndpoint
{
    RectTransform RectTransform { get; }
    float GetVisualHalfWidth();
    float GetVisualHalfHeight();
}

public class SkillTreeConnectorUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private bool useMeshLineRenderer = true;
    [SerializeField] private SkillTreeConnectorLineGraphic lineGraphic;
    [SerializeField] private Color meshLineFallbackColor = new Color(0.24f, 0.24f, 0.26f, 1f);
    [Header("Line Width")]
    [Tooltip("When enabled, connector width is forced to a fixed pixel value.")]
    [SerializeField] private bool overrideThickness = true;
    [Tooltip("Fixed connector width in pixels when Override Thickness is enabled.")]
    [SerializeField, Min(1f)] private float thicknessPixels = 4f;
    [Tooltip("When snapping to pixels, also snap line thickness and center to half-pixels for odd thickness.")]
    [SerializeField] private bool snapThicknessToPixelGrid = false;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    public void SetUseMeshLineRenderer(bool enabled)
    {
        useMeshLineRenderer = enabled;
        EnsureLineGraphicSetup(allowAddComponent: enabled);
    }

    private void Awake()
    {
        EnsureLineGraphicSetup(allowAddComponent: true);
    }

    private void OnValidate()
    {
        EnsureLineGraphicSetup(allowAddComponent: false);
    }

    public void SetPositions(SkillTreeNodeUI from, SkillTreeNodeUI to)
    {
        SetUseMeshLineRenderer(true);
        SetPositions((ITreeConnectorEndpoint)from, to, trimToNodeEdges: true, pixelSnap: false);
    }

    public void SetPositions(ITreeConnectorEndpoint from, ITreeConnectorEndpoint to)
    {
        SetPositions(from, to, trimToNodeEdges: true, pixelSnap: false);
    }

    public void SetPositions(ITreeConnectorEndpoint from, ITreeConnectorEndpoint to, bool trimToNodeEdges)
    {
        SetPositions(from, to, trimToNodeEdges, pixelSnap: false);
    }

    public void SetPositions(ITreeConnectorEndpoint from, ITreeConnectorEndpoint to, bool trimToNodeEdges, bool pixelSnap)
    {
        if (from == null || to == null)
            return;

        if (useMeshLineRenderer && lineGraphic == null)
            EnsureLineGraphicSetup(allowAddComponent: true);

        // Note: this assumes nodes and connectors share the same anchored space (same canvas/root).
        // Trimming uses the *visible* node box size (Fill), not the root rect.
        Vector2 a = from.RectTransform.anchoredPosition;
        Vector2 b = to.RectTransform.anchoredPosition;
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length <= 0.001f)
            return;

        Vector2 start = a;
        Vector2 end = b;
        if (trimToNodeEdges)
        {
            Vector2 dir = delta / length;
            start = a + dir * GetEdgeDistance(from.GetVisualHalfWidth(), from.GetVisualHalfHeight(), dir);
            end = b - dir * GetEdgeDistance(to.GetVisualHalfWidth(), to.GetVisualHalfHeight(), -dir);
        }
        Vector2 seg = end - start;
        float segLen = seg.magnitude;
        if (segLen <= 0.001f)
            return;

        RectTransform targetRect = (useMeshLineRenderer && lineGraphic != null)
            ? lineGraphic.rectTransform
            : RectTransform;

        float angle = Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg;
        Vector2 pos = start + seg * 0.5f;
        float len = segLen;
        float height = overrideThickness ? Mathf.Max(1f, thicknessPixels) : targetRect.sizeDelta.y;
        if (pixelSnap)
        {
            pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            len = Mathf.Max(1f, Mathf.Round(segLen));
            if (snapThicknessToPixelGrid)
            {
                height = Mathf.Max(1f, Mathf.Round(height));
                // Odd-pixel thickness lines render crisper when centered on half-pixels.
                if (((int)height & 1) == 1)
                    pos += new Vector2(0.5f, 0.5f);
            }
        }

        if (useMeshLineRenderer && lineGraphic != null)
        {
            lineGraphic.color = ResolveMeshLineColor();
            lineGraphic.material = null;
            lineGraphic.enabled = true;

            // Keep connector rect axis-aligned and draw the diagonal in mesh space for cleaner AA edges.
            float padding = Mathf.Max(1f, height * 0.5f + 2f);
            Vector2 min = Vector2.Min(start, end) - Vector2.one * padding;
            Vector2 max = Vector2.Max(start, end) + Vector2.one * padding;
            Vector2 size = max - min;
            Vector2 center = (min + max) * 0.5f;

            targetRect.anchoredPosition = center;
            targetRect.sizeDelta = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            targetRect.localRotation = Quaternion.identity;

            // UI Graphic vertex space is centered on RectTransform pivot (default 0.5,0.5),
            // so convert world-anchored points into connector-local centered coordinates.
            Vector2 localStart = start - center;
            Vector2 localEnd = end - center;
            lineGraphic.SetLine(localStart, localEnd, height);
            return;
        }

        // Mesh mode unavailable -> fail-safe to legacy image so connectors never disappear.
        if (useMeshLineRenderer)
            EnsureLegacyImageVisible(allowAddComponent: true);

        targetRect.anchoredPosition = pos;
        targetRect.sizeDelta = new Vector2(len, height);
        targetRect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static float GetEdgeDistance(float halfW, float halfH, Vector2 dir)
    {
        float dx = Mathf.Abs(dir.x);
        float dy = Mathf.Abs(dir.y);
        float tx = dx > 0.0001f ? halfW / dx : float.PositiveInfinity;
        float ty = dy > 0.0001f ? halfH / dy : float.PositiveInfinity;
        return Mathf.Min(tx, ty);
    }

    private void EnsureLineGraphicSetup(bool allowAddComponent)
    {
        if (!rectTransform)
            rectTransform = transform as RectTransform;

        if (rectTransform == null)
            return;

        Image legacyImage = GetFirstLegacyImage();
        if (!useMeshLineRenderer)
        {
            if (!lineGraphic)
                lineGraphic = GetComponent<SkillTreeConnectorLineGraphic>();
            if (lineGraphic != null)
                lineGraphic.enabled = false;
            EnsureLegacyImageVisible(allowAddComponent);
            return;
        }

        if (!lineGraphic)
            lineGraphic = GetComponentInChildren<SkillTreeConnectorLineGraphic>(true);
        if (!lineGraphic && allowAddComponent)
            lineGraphic = gameObject.AddComponent<SkillTreeConnectorLineGraphic>();

        if (lineGraphic != null)
        {
            lineGraphic.enabled = true;
            lineGraphic.raycastTarget = false;
            lineGraphic.color = ResolveMeshLineColor();
        }

        if (legacyImage != null)
            legacyImage.enabled = false;
    }

    private void EnsureLegacyImageVisible(bool allowAddComponent)
    {
        Image legacyImage = GetFirstLegacyImage();
        if (legacyImage == null && allowAddComponent)
            legacyImage = gameObject.AddComponent<Image>();

        if (legacyImage != null)
        {
            legacyImage.enabled = true;
            legacyImage.raycastTarget = false;
            if (legacyImage.color.a <= 0.001f)
                legacyImage.color = ResolveMeshLineColor();
        }
        if (lineGraphic != null)
            lineGraphic.enabled = false;
    }

    private Color ResolveMeshLineColor()
    {
        Image legacyImage = GetFirstLegacyImage();
        if (legacyImage != null)
        {
            Color c = legacyImage.color;
            if (c.a > 0.001f)
                return c;
        }
        Color fallback = meshLineFallbackColor;
        if (fallback.a <= 0.001f)
            fallback = new Color(0.24f, 0.24f, 0.26f, 1f);
        return fallback;
    }

    private Image GetFirstLegacyImage()
    {
        Image legacyImage = GetComponent<Image>();
        if (legacyImage != null)
            return legacyImage;
        return GetComponentInChildren<Image>(true);
    }
}
