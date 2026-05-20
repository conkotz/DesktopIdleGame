using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime tinted fullscreen overlays (strip or full window per <see cref="GameplayScreenOverlayLayout"/>).
/// Use for ability channels, cutscenes, etc. Load fade and helper dim keep dedicated paths but share layout.
/// </summary>
public static class GameplayScreenOverlay
{
    public const string FinalSeveranceChannelId = "final_severance_channel";
    public const string BladestormChannelId = "bladestorm_channel";

    /// <summary>Slight red wash during Final Severance channel.</summary>
    public static readonly Color FinalSeveranceChannelTint = new Color(0.42f, 0.04f, 0.04f, 0.38f);

    /// <summary>Muted steel wash during Bladestorm channel.</summary>
    public static readonly Color BladestormChannelTint = new Color(0.55f, 0.58f, 0.65f, 0.32f);

    private const string RootNamePrefix = "GameplayScreenOverlay_";

    private static readonly Dictionary<string, Entry> s_entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

    static GameplayScreenOverlay()
    {
        GameplayScreenOverlayLayout.RegisterCoverageRefresh(RefreshAllVisible);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        s_entries.Clear();
    }

    public struct Spec
    {
        public bool blocksRaycasts;
        public bool raycastTarget;
        /// <summary>Nested canvas sort order when parented under strip UI. Ignored for fullscreen-root overlays.</summary>
        public int stripSortingOrder;
        public float viewportBleedPixels;

        public static Spec DefaultAbilityChannel => new Spec
        {
            blocksRaycasts = false,
            raycastTarget = false,
            stripSortingOrder = 25000,
            viewportBleedPixels = GameplayScreenOverlayLayout.DefaultViewportBleedPixels,
        };
    }

    public static void Show(string overlayId, Color color, Spec spec = default)
    {
        if (string.IsNullOrWhiteSpace(overlayId))
            return;

        if (spec.stripSortingOrder <= 0)
            spec = Spec.DefaultAbilityChannel;

        Entry entry = GetOrCreate(overlayId.Trim(), spec);
        if (!IsEntryAlive(entry))
            return;

        entry.Visible = true;
        entry.Image.color = color;
        entry.Image.enabled = true;
        entry.CanvasGroup.alpha = 1f;
        entry.CanvasGroup.blocksRaycasts = spec.blocksRaycasts;
        entry.Image.raycastTarget = spec.raycastTarget;
        entry.Root.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    public static void Hide(string overlayId)
    {
        if (string.IsNullOrWhiteSpace(overlayId))
            return;

        string key = overlayId.Trim();
        if (!s_entries.TryGetValue(key, out Entry entry))
            return;

        if (!IsEntryAlive(entry))
        {
            s_entries.Remove(key);
            return;
        }

        entry.Visible = false;
        entry.CanvasGroup.alpha = 0f;
        entry.Image.enabled = false;
        entry.CanvasGroup.blocksRaycasts = false;
        entry.Image.raycastTarget = false;
    }

    public static bool IsVisible(string overlayId)
    {
        if (string.IsNullOrWhiteSpace(overlayId))
            return false;
        return s_entries.TryGetValue(overlayId.Trim(), out Entry e) && IsEntryAlive(e) && e.Visible;
    }

    public static void RefreshAllVisible()
    {
        var keys = new List<string>(s_entries.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (!s_entries.TryGetValue(key, out Entry entry))
                continue;

            if (!IsEntryAlive(entry))
            {
                s_entries.Remove(key);
                continue;
            }

            if (!entry.Visible)
                continue;

            bool coversFull = GameplayScreenOverlayLayout.CoversFullWindow;
            if (entry.LastCoversFullWindow != coversFull)
            {
                bool visible = entry.Visible;
                Color color = entry.Image != null ? entry.Image.color : Color.clear;
                Spec spec = entry.Spec;
                DestroyEntry(entry);
                s_entries.Remove(key);
                if (visible)
                    Show(key, color, spec);
                continue;
            }

            GameplayScreenOverlayLayout.ApplyCoverage(entry.Root, entry.Spec.viewportBleedPixels);
            entry.Root.transform.SetAsLastSibling();
        }
    }

    private static Entry GetOrCreate(string overlayId, Spec spec)
    {
        if (s_entries.TryGetValue(overlayId, out Entry existing) && IsEntryAlive(existing))
        {
            existing.Spec = spec;
            if (existing.LastCoversFullWindow != GameplayScreenOverlayLayout.CoversFullWindow)
            {
                DestroyEntry(existing);
                s_entries.Remove(overlayId);
            }
            else
            {
                GameplayScreenOverlayLayout.ApplyCoverage(existing.Root, spec.viewportBleedPixels);
                if (!GameplayScreenOverlayLayout.CoversFullWindow)
                    GameplayScreenOverlayLayout.EnsureNestedOverlayCanvas(existing.Root, spec.stripSortingOrder);
                return existing;
            }
        }
        else if (s_entries.ContainsKey(overlayId))
        {
            s_entries.Remove(overlayId);
        }

        Entry created = CreateEntry(overlayId, spec);
        if (created != null)
            s_entries[overlayId] = created;
        return created;
    }

    private static Entry CreateEntry(string overlayId, Spec spec)
    {
        string rootName = RootNamePrefix + overlayId;
        bool coversFull = GameplayScreenOverlayLayout.CoversFullWindow;
        GameObject root;

        if (coversFull)
        {
            root = new GameObject(rootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            RectTransform rt = (RectTransform)root.transform;
            GameplayScreenOverlayLayout.ApplyFullWindowRect(rt);
        }
        else
        {
            Canvas stripCanvas = GameplayScreenOverlayLayout.TryResolveStripUiCanvas();
            if (stripCanvas == null)
                return null;

            Transform existing = stripCanvas.transform.Find(rootName);
            root = existing
                ? existing.gameObject
                : new GameObject(rootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

            root.transform.SetParent(stripCanvas.transform, false);
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            Canvas legacy = root.GetComponent<Canvas>();
            if (legacy != null && legacy.renderMode == RenderMode.ScreenSpaceCamera)
                UnityEngine.Object.Destroy(legacy);

            GameplayScreenOverlayLayout.EnsureNestedOverlayCanvas(root, spec.stripSortingOrder);
            GameplayScreenOverlayLayout.ApplyCoverage(root, spec.viewportBleedPixels);
        }

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        if (!cg)
            cg = root.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.alpha = 0f;

        Image img = root.GetComponent<Image>();
        if (!img)
            img = root.AddComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false;

        return new Entry
        {
            Root = root,
            CanvasGroup = cg,
            Image = img,
            Spec = spec,
            LastCoversFullWindow = coversFull,
            Visible = false,
        };
    }

    private static void DestroyEntry(Entry entry)
    {
        if (entry?.Root)
            UnityEngine.Object.Destroy(entry.Root);
    }

    /// <summary>Unity "fake null" after scene teardown — do not touch components without this check.</summary>
    private static bool IsEntryAlive(Entry entry) =>
        entry != null && entry.Root && entry.CanvasGroup && entry.Image;

    private sealed class Entry
    {
        public GameObject Root;
        public CanvasGroup CanvasGroup;
        public Image Image;
        public Spec Spec;
        public bool LastCoversFullWindow;
        public bool Visible;
    }
}
