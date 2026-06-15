using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves horizontal movement/camera clamps for the main lane and optional <see cref="SidePlayArea"/> branches.
/// Default behaviour matches legacy <see cref="WorldBounds"/> when no side area contains the queried world X.
/// </summary>
public static class PlayAreaBounds
{
    private static readonly HashSet<string> EnabledAreaIds =
        new(StringComparer.OrdinalIgnoreCase);

    public static void SetEnabledAreasForMap(IEnumerable<string> areaIds)
    {
        EnabledAreaIds.Clear();
        if (areaIds == null)
            return;

        foreach (string raw in areaIds)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            EnabledAreaIds.Add(raw.Trim());
        }
    }

    public static void ClearEnabledAreas() => EnabledAreaIds.Clear();

    public static bool IsAreaEnabled(SidePlayArea area)
    {
        if (!area)
            return false;
        if (EnabledAreaIds.Count == 0)
            return false;
        return EnabledAreaIds.Contains(area.AreaId);
    }

    public static bool TryGetClampXForWorldX(float worldX, float padding, out float minX, out float maxX)
    {
        if (TryGetEnabledSideAreaContainingWorldX(worldX, out SidePlayArea sideArea))
        {
            sideArea.GetClampX(padding, out minX, out maxX);
            return true;
        }

        if (WorldBounds.Instance != null)
        {
            minX = WorldBounds.Instance.Left + padding;
            maxX = WorldBounds.Instance.Right - padding;
            return true;
        }

        minX = maxX = worldX;
        return false;
    }

    public static bool TryGetCameraClampXForWorldX(float worldX, float halfViewportWidth, out float minX, out float maxX)
    {
        if (TryGetEnabledSideAreaContainingWorldX(worldX, out SidePlayArea sideArea))
        {
            sideArea.GetCameraClampX(halfViewportWidth, out minX, out maxX);
            return true;
        }

        if (WorldBounds.Instance != null)
        {
            minX = WorldBounds.Instance.Left + halfViewportWidth;
            maxX = WorldBounds.Instance.Right - halfViewportWidth;
            if (minX > maxX)
            {
                float mid = (WorldBounds.Instance.Left + WorldBounds.Instance.Right) * 0.5f;
                minX = maxX = mid;
            }

            return true;
        }

        minX = maxX = worldX;
        return false;
    }

    private static bool TryGetEnabledSideAreaContainingWorldX(float worldX, out SidePlayArea area)
    {
        area = null;
        if (EnabledAreaIds.Count == 0)
            return false;

        IReadOnlyDictionary<string, SidePlayArea> all = SidePlayArea.All;
        foreach (KeyValuePair<string, SidePlayArea> kv in all)
        {
            SidePlayArea candidate = kv.Value;
            if (!candidate || !EnabledAreaIds.Contains(candidate.AreaId))
                continue;
            if (!candidate.ContainsWorldX(worldX))
                continue;

            area = candidate;
            return true;
        }

        return false;
    }
}
