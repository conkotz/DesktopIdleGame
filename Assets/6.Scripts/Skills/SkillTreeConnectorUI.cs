using UnityEngine;

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

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    public void SetPositions(SkillTreeNodeUI from, SkillTreeNodeUI to)
    {
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

        float angle = Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg;
        Vector2 pos = start + seg * 0.5f;
        float len = segLen;
        if (pixelSnap)
        {
            pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            len = Mathf.Max(1f, Mathf.Round(segLen));
        }
        RectTransform.anchoredPosition = pos;
        RectTransform.sizeDelta = new Vector2(len, RectTransform.sizeDelta.y);
        RectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static float GetEdgeDistance(float halfW, float halfH, Vector2 dir)
    {
        float dx = Mathf.Abs(dir.x);
        float dy = Mathf.Abs(dir.y);
        float tx = dx > 0.0001f ? halfW / dx : float.PositiveInfinity;
        float ty = dy > 0.0001f ? halfH / dy : float.PositiveInfinity;
        return Mathf.Min(tx, ty);
    }
}
