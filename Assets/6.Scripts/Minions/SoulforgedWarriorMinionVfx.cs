using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight procedural VFX for the Soulforged Warrior minion (warcry ring + furious slam shockwave).
/// </summary>
public static class SoulforgedWarriorMinionVfx
{
    private static readonly Color WarcryBaseColor = new Color(0.94f, 0.92f, 0.88f, 0.48f);

    public static void PlayWarcryRing(MonoBehaviour host, Vector3 worldCenter, float maxRadius, float durationSeconds)
    {
        if (!host)
            return;

        maxRadius = Mathf.Max(0.5f, maxRadius);
        durationSeconds = Mathf.Max(0.2f, durationSeconds);

        host.StartCoroutine(CoExpandRing(worldCenter, maxRadius, durationSeconds, WarcryBaseColor, 1f, 0.11f));
        host.StartCoroutine(CoDelayedRing(worldCenter, maxRadius, durationSeconds * 0.9f, WarcryBaseColor, 0.78f, 0.1f, durationSeconds * 0.16f));
        host.StartCoroutine(CoDelayedRing(worldCenter, maxRadius * 0.96f, durationSeconds * 0.82f, WarcryBaseColor, 0.58f, 0.085f, durationSeconds * 0.32f));
        host.StartCoroutine(CoDelayedRing(worldCenter, maxRadius * 0.92f, durationSeconds * 0.74f, WarcryBaseColor, 0.38f, 0.07f, durationSeconds * 0.48f));
    }

    public static void PlayFuriousSlamShockwave(
        MonoBehaviour host,
        Vector3 groundCenter,
        float facingSignX,
        float maxRadius,
        float durationSeconds)
    {
        if (!host)
            return;

        host.StartCoroutine(CoFrontShockwave(groundCenter, facingSignX, maxRadius, durationSeconds));
    }

    private static IEnumerator CoDelayedRing(
        Vector3 center,
        float maxRadius,
        float duration,
        Color baseColor,
        float alphaScale,
        float lineWidth,
        float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        yield return CoExpandRing(center, maxRadius, duration, baseColor, alphaScale, lineWidth);
    }

    private static IEnumerator CoExpandRing(
        Vector3 center,
        float maxRadius,
        float duration,
        Color baseColor,
        float alphaScale,
        float lineWidth)
    {
        maxRadius = Mathf.Max(0.5f, maxRadius);
        duration = Mathf.Max(0.12f, duration);
        alphaScale = Mathf.Clamp01(alphaScale);
        lineWidth = Mathf.Max(0.03f, lineWidth);

        var root = new GameObject("SoulforgedWarriorWarcryRing");
        LineRenderer ring = CreateRingRenderer(root.transform, lineWidth);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!ring)
                yield break;

            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(0.2f, maxRadius, u);
            float alpha = baseColor.a * alphaScale * (1f - u);
            RebuildCircle(ring, center, radius, baseColor, alpha);
            yield return null;
        }

        if (root)
            Object.Destroy(root);
    }

    private static IEnumerator CoFrontShockwave(
        Vector3 groundCenter,
        float facingSignX,
        float maxRadius,
        float duration)
    {
        maxRadius = Mathf.Max(0.5f, maxRadius);
        duration = Mathf.Max(0.08f, duration);
        float sign = Mathf.Approximately(facingSignX, 0f) ? 1f : Mathf.Sign(facingSignX);

        var root = new GameObject("SoulforgedWarriorFuriousSlam");
        LineRenderer leftArc = CreateArcRenderer(root.transform, true);
        LineRenderer rightArc = CreateArcRenderer(root.transform, false);
        Color shockColor = new Color(0.92f, 0.9f, 0.82f, 0.55f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!root)
                yield break;

            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float radius = maxRadius * u;
            float alpha = shockColor.a * (1f - u);
            RebuildFrontArc(leftArc, groundCenter, radius, sign, true, alpha, shockColor);
            RebuildFrontArc(rightArc, groundCenter, radius, sign, false, alpha, shockColor);
            yield return null;
        }

        if (root)
            Object.Destroy(root);
    }

    private static LineRenderer CreateRingRenderer(Transform parent, float lineWidth)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(parent, false);
        LineRenderer lr = ConfigureLineRenderer(go);
        lr.loop = true;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth * 0.75f;
        return lr;
    }

    private static LineRenderer CreateArcRenderer(Transform parent, bool leftSide)
    {
        var go = new GameObject(leftSide ? "ArcLeft" : "ArcRight");
        go.transform.SetParent(parent, false);
        return ConfigureLineRenderer(go);
    }

    private static LineRenderer ConfigureLineRenderer(GameObject go)
    {
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 4;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.05f;
        lr.sortingOrder = 40;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader)
            lr.material = new Material(shader);
        return lr;
    }

    private static void RebuildCircle(LineRenderer lr, Vector3 center, float radius, Color baseColor, float alpha)
    {
        if (!lr)
            return;

        const int segments = 48;
        lr.positionCount = segments + 1;
        Color c = baseColor;
        c.a = alpha;
        lr.startColor = c;
        lr.endColor = c;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(t) * radius;
            float y = center.y + Mathf.Sin(t) * radius;
            lr.SetPosition(i, new Vector3(x, y, center.z));
        }
    }

    private static void RebuildFrontArc(
        LineRenderer lr,
        Vector3 center,
        float radius,
        float facingSignX,
        bool leftSide,
        float alpha,
        Color baseColor)
    {
        if (!lr)
            return;

        const int segments = 14;
        lr.positionCount = segments + 1;
        Color c = baseColor;
        c.a = alpha;
        lr.startColor = c;
        lr.endColor = c;

        float startAngle = leftSide ? 110f : -110f;
        float endAngle = leftSide ? 250f : 110f;
        if (facingSignX < 0f)
        {
            startAngle = -startAngle;
            endAngle = -endAngle;
        }

        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            float deg = Mathf.Lerp(startAngle, endAngle, u) * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(deg) * radius;
            float y = center.y + Mathf.Sin(deg) * radius * 0.28f;
            lr.SetPosition(i, new Vector3(x, y, center.z));
        }
    }
}
