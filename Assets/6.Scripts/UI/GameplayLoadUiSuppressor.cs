using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides full-window UI during strip-constrained level loads (expand background off).
/// </summary>
public static class GameplayLoadUiSuppressor
{
    private const string FullWindowCanvasName = "FullWindowCanvas";
    private const string LevelLoadFaderName = "LevelLoadFader";

    private static readonly List<Canvas> s_disabledCanvases = new(4);
    private static readonly List<CanvasGroup> s_hiddenStripGroups = new(16);
    private static readonly List<float> s_hiddenStripAlphas = new(16);

    public static bool ShouldSuppressForCurrentSettings()
    {
        return !ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground) &&
               !GameplayScreenOverlayLayout.CoversFullWindow;
    }

    public static void Begin()
    {
        if (!ShouldSuppressForCurrentSettings())
            return;

        HideFullWindowCanvas();
        HideStripHudExceptLoadFader();
    }

    public static void End()
    {
        RestoreFullWindowCanvas();
        RestoreStripHud();
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

    private static void HideStripHudExceptLoadFader()
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

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (!group)
                group = child.gameObject.AddComponent<CanvasGroup>();

            s_hiddenStripGroups.Add(group);
            s_hiddenStripAlphas.Add(group.alpha);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
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
        for (int i = 0; i < s_hiddenStripGroups.Count; i++)
        {
            CanvasGroup group = s_hiddenStripGroups[i];
            if (!group)
                continue;

            group.alpha = i < s_hiddenStripAlphas.Count ? s_hiddenStripAlphas[i] : 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        s_hiddenStripGroups.Clear();
        s_hiddenStripAlphas.Clear();
    }
}
