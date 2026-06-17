using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared strip-vs-full-window coverage for fullscreen-style UI overlays (load fade, helper dim, ability tints, biome mood).
/// </summary>
public static class GameplayScreenOverlayLayout
{
    public const string StripUiCanvasObjectName = "StripUICanvas";
    public const float DefaultViewportBleedPixels = 2f;

    private static readonly List<Action> s_coverageRefreshCallbacks = new List<Action>(8);

    /// <summary>True when overlays should cover the full monitor (expand-background on, or no strip UI canvas).</summary>
    public static bool CoversFullWindow =>
        ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground) || TryResolveStripUiCanvas() == null;

    public static void RegisterCoverageRefresh(Action callback)
    {
        if (callback == null || s_coverageRefreshCallbacks.Contains(callback))
            return;
        s_coverageRefreshCallbacks.Add(callback);
    }

    public static void UnregisterCoverageRefresh(Action callback)
    {
        if (callback == null)
            return;
        s_coverageRefreshCallbacks.Remove(callback);
    }

    public static void RefreshAllRegistered()
    {
        for (int i = s_coverageRefreshCallbacks.Count - 1; i >= 0; i--)
        {
            try
            {
                s_coverageRefreshCallbacks[i]?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public static Canvas TryResolveStripUiCanvas()
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (!string.Equals(t.name, StripUiCanvasObjectName, StringComparison.OrdinalIgnoreCase))
                continue;
            Canvas c = t.GetComponent<Canvas>();
            if (c != null)
                return c;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("UICanvas");
        return tagged != null ? tagged.GetComponent<Canvas>() : null;
    }

    public static Camera TryResolveStripCamera()
    {
        StripCameraController ctrl =
            UnityEngine.Object.FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
        if (ctrl)
        {
            Camera fromController = ctrl.GetComponent<Camera>();
            if (fromController && fromController.isActiveAndEnabled)
                return fromController;
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (!cam || cam.hideFlags != HideFlags.None || !cam.gameObject.scene.IsValid())
                continue;

            if (!string.Equals(cam.gameObject.name, "StripCamera", StringComparison.OrdinalIgnoreCase))
                continue;

            if (cam.isActiveAndEnabled)
                return cam;
        }

        Camera main = Camera.main;
        return main && main.isActiveAndEnabled ? main : null;
    }

    public static bool IsUnderStripUiCanvas(Transform t)
    {
        while (t != null)
        {
            if (string.Equals(t.name, StripUiCanvasObjectName, StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }

    public static void ApplyCoverage(GameObject overlayGo, float viewportBleedPixels = DefaultViewportBleedPixels)
    {
        if (!overlayGo)
            return;

        RectTransform rt = overlayGo.transform as RectTransform;
        if (!rt)
            return;

        if (CoversFullWindow)
            ApplyFullWindowRect(rt);
        else
            ApplyStripViewportRect(overlayGo, viewportBleedPixels);
    }

    public static void ApplyFullWindowRect(RectTransform rt)
    {
        if (!rt)
            return;

        StripUIViewportFollower follower = rt.GetComponent<StripUIViewportFollower>();
        if (follower)
            follower.enabled = false;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void ApplyStripViewportRect(GameObject overlayGo, float viewportBleedPixels = DefaultViewportBleedPixels)
    {
        if (!overlayGo)
            return;

        Camera stripCam = TryResolveStripCamera();
        RectTransform rt = overlayGo.transform as RectTransform;
        if (!stripCam || !rt)
        {
            ApplyFullWindowRect(rt);
            return;
        }

        StripUIViewportFollower follower = overlayGo.GetComponent<StripUIViewportFollower>();
        if (!follower)
            follower = overlayGo.AddComponent<StripUIViewportFollower>();
        follower.enabled = true;
        follower.Bind(stripCam);
        follower.ViewportBleedPixels = Mathf.Max(0f, viewportBleedPixels);
    }

    public static void EnsureNestedOverlayCanvas(GameObject overlayRoot, int sortingOrder)
    {
        if (!overlayRoot)
            return;

        Canvas c = overlayRoot.GetComponent<Canvas>();
        if (!c)
            c = overlayRoot.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = sortingOrder;
        c.pixelPerfect = false;
    }

    public static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (string.Equals(t.name, objectName, StringComparison.Ordinal))
                return t.gameObject;
        }

        return null;
    }

    public static GameObject FindDeepNamedChild(Transform root, string childName)
    {
        if (!root || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.name == childName)
                return c.gameObject;

            GameObject deeper = FindDeepNamedChild(c, childName);
            if (deeper != null)
                return deeper;
        }

        return null;
    }
}
