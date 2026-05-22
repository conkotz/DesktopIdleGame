using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SkillTreeConnectorLineGraphic : MaskableGraphic
{
    [SerializeField, Min(0f)] private float featherPixels = 1f;

    private Vector2 _start;
    private Vector2 _end;
    private float _thickness = 4f;

    public void SetLine(Vector2 start, Vector2 end, float thickness)
    {
        _start = start;
        _end = end;
        _thickness = Mathf.Max(1f, thickness);
        SetVerticesDirty();
    }

    /// <summary>Disable soft edge fade (recommended under RectMask2D scroll views to avoid flicker).</summary>
    public void SetFeatherPixels(float pixels)
    {
        featherPixels = Mathf.Max(0f, pixels);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Vector2 delta = _end - _start;
        float len = delta.magnitude;
        if (len <= 0.001f)
            return;

        Vector2 dir = delta / len;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float half = _thickness * 0.5f;
        float feather = Mathf.Max(0f, featherPixels);

        Vector2 nCore = perp * half;
        Vector2 nOuter = perp * (half + feather);

        Vector2 sL = _start - nCore;
        Vector2 sR = _start + nCore;
        Vector2 eL = _end - nCore;
        Vector2 eR = _end + nCore;

        Color32 c = color;
        Color32 c0 = new Color32(c.r, c.g, c.b, 0);

        // Core opaque strip.
        AddQuad(vh, sL, sR, eR, eL, c, c, c, c);

        if (feather > 0.001f)
        {
            Vector2 sLo = _start - nOuter;
            Vector2 eLo = _end - nOuter;
            Vector2 sRo = _start + nOuter;
            Vector2 eRo = _end + nOuter;

            // Left fade (transparent outer -> opaque inner).
            AddQuad(vh, sLo, sL, eL, eLo, c0, c, c, c0);
            // Right fade (opaque inner -> transparent outer).
            AddQuad(vh, sR, sRo, eRo, eR, c, c0, c0, c);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
                                Color32 ca, Color32 cb, Color32 cc, Color32 cd)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, ca, Vector2.zero);
        vh.AddVert(b, cb, Vector2.zero);
        vh.AddVert(c, cc, Vector2.zero);
        vh.AddVert(d, cd, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
