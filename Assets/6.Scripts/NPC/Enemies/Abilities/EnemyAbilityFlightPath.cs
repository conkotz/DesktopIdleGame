using UnityEngine;

/// <summary>
/// Shared hop arc for enemy disengage / pounce movement on X with a stepped Y flight path.
/// </summary>
public static class EnemyAbilityFlightPath
{
    private static readonly float[] HeightFractions = { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.4f, 0.3f, 0.2f, 0.1f, 0f };

    public static float SampleHeightFraction(float normalizedProgress01)
    {
        if (HeightFractions.Length <= 1)
            return 0f;

        float scaled = Mathf.Clamp01(normalizedProgress01) * (HeightFractions.Length - 1);
        int segment = Mathf.Min(Mathf.FloorToInt(scaled), HeightFractions.Length - 2);
        float segmentT = scaled - segment;
        return Mathf.Lerp(HeightFractions[segment], HeightFractions[segment + 1], segmentT);
    }

    public static Vector3 Evaluate(Vector3 start, Vector3 end, float normalizedProgress01, float airHeight)
    {
        float t = Mathf.Clamp01(normalizedProgress01);
        Vector3 pos = Vector3.Lerp(start, end, t);
        float floorY = Mathf.Lerp(start.y, end.y, t);
        pos.y = floorY + Mathf.Max(0f, airHeight) * SampleHeightFraction(t);
        return pos;
    }

    public static float HorizontalDistance(Vector3 start, Vector3 end) =>
        Mathf.Abs(end.x - start.x);
}
