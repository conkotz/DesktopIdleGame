using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared runtime-only state between Level Select list view and World Map view.
/// Keeps the bottom-bar toggle behavior consistent (reopens last used presentation) and
/// provides the HUD preview selection used by <see cref="ActiveMapDisplayUI"/>.
/// </summary>
public static class LevelSelectSharedState
{
    public enum Presentation
    {
        List,
        WorldMap
    }

    public enum NodeListFilter
    {
        All,
        Combat,
        Gathering,
        Other
    }

    public static Presentation LastPresentation { get; set; } = Presentation.List;

    public static string SelectedRegionId { get; set; } = "";
    public static string SelectedNodeId { get; set; } = "";
    public static NodeListFilter Filter { get; set; } = NodeListFilter.All;

    public static string PendingFocusNodeId { get; set; } = "";

    public static MapNodeDefinition HudPreviewSelection { get; set; }

    private static readonly List<GameObject> s_hideRoots = new();

    public static void SetHideRoots(GameObject[] roots)
    {
        s_hideRoots.Clear();
        if (roots == null)
            return;

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject go = roots[i];
            if (!go)
                continue;
            if (!s_hideRoots.Contains(go))
                s_hideRoots.Add(go);
        }
    }

    public static IReadOnlyList<GameObject> HideRoots => s_hideRoots;

    public static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

    public static bool EqualsId(string a, string b)
    {
        a = Norm(a);
        b = Norm(b);
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

