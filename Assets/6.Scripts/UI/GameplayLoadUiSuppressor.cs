using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// During gameplay level loads: hides full-window menu UI and most strip HUD, but keeps
/// <c>BotomGameBar</c> visible as soon as the load fade lifts (independent of black-hold / prewarm timing).
/// </summary>
public static class GameplayLoadUiSuppressor
{
    private const string FullWindowCanvasName = "FullWindowCanvas";
    private const string LevelLoadFaderName = "LevelLoadFader";
    private const string UiFrameName = "UI_Frame";
    private const string BottomGameBarName = "BotomGameBar";

    private static readonly List<Canvas> s_disabledCanvases = new(4);
    private static readonly List<CanvasGroup> s_hiddenStripGroups = new(16);
    private static readonly List<float> s_hiddenStripAlphas = new(16);
    private static readonly List<CanvasGroup> s_hiddenUiFrameChildGroups = new(32);
    private static readonly List<float> s_hiddenUiFrameChildAlphas = new(32);

    public static void Begin()
    {
        HideFullWindowCanvas();
        HideStripHudKeepingBottomBarAndLoadFader();
    }

    public static void End()
    {
        RestoreFullWindowCanvas();
        RestoreStripHud();
        RestoreUiFrameChildren();
    }

    private static void HideFullWindowCanvas()
    {
        GameObject root = GameplayScreenOverlayLayout.FindSceneObjectByName(FullWindowCanvasName);
        if (!root)
            return;

        Canvas canvas = root.GetComponent<Canvas>();
        if (!canvas || !canvas.enabled)
            return;

        canvas.enabled = false;
        s_disabledCanvases.Add(canvas);
    }

    private static void HideStripHudKeepingBottomBarAndLoadFader()
    {
        Canvas stripCanvas = GameplayScreenOverlayLayout.TryResolveStripUiCanvas();
        if (!stripCanvas)
            return;

        Transform stripRoot = stripCanvas.transform;
        for (int i = 0; i < stripRoot.childCount; i++)
        {
            Transform child = stripRoot.GetChild(i);
            if (!child || child.name == LevelLoadFaderName)
                continue;

            if (string.Equals(child.name, UiFrameName, StringComparison.Ordinal))
            {
                HideUiFrameChildrenExceptBottomBar(child);
                continue;
            }

            HideTransformCanvasGroup(child);
        }
    }

    private static void HideUiFrameChildrenExceptBottomBar(Transform uiFrame)
    {
        for (int i = 0; i < uiFrame.childCount; i++)
        {
            Transform child = uiFrame.GetChild(i);
            if (!child)
                continue;

            if (IsBottomGameBar(child))
            {
                EnsureTransformVisible(child);
                continue;
            }

            HideTransformCanvasGroup(child, s_hiddenUiFrameChildGroups, s_hiddenUiFrameChildAlphas);
        }
    }

    private static bool IsBottomGameBar(Transform t) =>
        t && string.Equals(t.name, BottomGameBarName, StringComparison.OrdinalIgnoreCase);

    private static void EnsureTransformVisible(Transform t)
    {
        if (!t)
            return;

        if (!t.gameObject.activeSelf)
            t.gameObject.SetActive(true);

        CanvasGroup group = t.GetComponent<CanvasGroup>();
        if (!group)
            return;

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    private static void HideTransformCanvasGroup(Transform t)
    {
        HideTransformCanvasGroup(t, s_hiddenStripGroups, s_hiddenStripAlphas);
    }

    private static void HideTransformCanvasGroup(
        Transform t,
        List<CanvasGroup> hiddenGroups,
        List<float> hiddenAlphas)
    {
        if (!t)
            return;

        CanvasGroup group = t.GetComponent<CanvasGroup>();
        if (!group)
            group = t.gameObject.AddComponent<CanvasGroup>();

        hiddenGroups.Add(group);
        hiddenAlphas.Add(group.alpha);
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private static void RestoreFullWindowCanvas()
    {
        for (int i = 0; i < s_disabledCanvases.Count; i++)
        {
            Canvas canvas = s_disabledCanvases[i];
            if (canvas)
                canvas.enabled = true;
        }

        s_disabledCanvases.Clear();
    }

    private static void RestoreStripHud()
    {
        RestoreCanvasGroups(s_hiddenStripGroups, s_hiddenStripAlphas);
    }

    private static void RestoreUiFrameChildren()
    {
        RestoreCanvasGroups(s_hiddenUiFrameChildGroups, s_hiddenUiFrameChildAlphas);
    }

    private static void RestoreCanvasGroups(List<CanvasGroup> groups, List<float> alphas)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            CanvasGroup group = groups[i];
            if (!group)
                continue;

            group.alpha = i < alphas.Count ? alphas[i] : 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        groups.Clear();
        alphas.Clear();
    }
}
