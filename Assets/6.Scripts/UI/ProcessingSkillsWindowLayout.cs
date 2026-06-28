using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared layout proxy under <c>WindowsArea</c> for all processing-skill panels (furnace, cooking, future crafting UIs).
/// Side popouts are children of each panel and follow when layout is applied.
/// </summary>
public static class ProcessingSkillsWindowLayout
{
    public const string ProcessingWindowKey = "ProcessingWindow";

    private const string LegacyFurnaceWindowKey = "FurnaceWindow";
    private const string LegacyCookingWindowKey = "CookingWindow";

    private static readonly Vector2 PanelDesignSize = new(360f, 460f);
    private static readonly Vector2 TopLeftPivot = new(0f, 1f);
    private static readonly Vector2 CenterAnchor = new(0.5f, 0.5f);

    public static bool IsProcessingSkillsWindow(string memoryKey) =>
        string.Equals(memoryKey, ProcessingWindowKey, System.StringComparison.Ordinal)
        || string.Equals(memoryKey, LegacyFurnaceWindowKey, System.StringComparison.Ordinal)
        || string.Equals(memoryKey, LegacyCookingWindowKey, System.StringComparison.Ordinal);

    public static void EnsureProxyAnchors()
    {
        Transform windowsArea = ResolveWindowsArea();
        if (!windowsArea)
            return;

        DestroyLegacyProxy(windowsArea, LegacyFurnaceWindowKey);
        DestroyLegacyProxy(windowsArea, LegacyCookingWindowKey);
        EnsureProxyAnchor(windowsArea);
        MigrateLegacySavedLayouts();
    }

    public static Vector2 GetProxySizeInWindowsArea() => MeasureDefaultPanelFootprintInWindowsArea();

    public static UIWindowLayoutPrefs.Snapshot PrepareGhostSnapshot(in UIWindowLayoutPrefs.Snapshot snapshot)
    {
        Vector2 size = GetProxySizeInWindowsArea();
        UIWindowLayoutPrefs.Snapshot prepared = snapshot;
        prepared.anchoredPosition = ConvertToTopLeftAnchoredPosition(
            prepared.anchoredPosition,
            prepared.pivot,
            prepared.sizeDelta,
            prepared.localScale);
        prepared.anchorMin = CenterAnchor;
        prepared.anchorMax = CenterAnchor;
        prepared.pivot = TopLeftPivot;
        prepared.sizeDelta = size;
        prepared.localScale = Vector3.one;
        return prepared;
    }

    public static void ApplyLayoutToOpenPanel(RectTransform panel, Canvas panelCanvas)
    {
        if (!panel || !panelCanvas)
            return;

        if (!TryGetProxy(out RectTransform proxy))
            return;

        if (UIWindowSessionLayoutMemory.TryGet(ProcessingWindowKey, out UIWindowLayoutPrefs.Snapshot session))
            UIWindowLayoutPrefs.Apply(proxy, PrepareGhostSnapshot(session));
        else if (UIWindowLayoutPrefs.HasSaved(ProcessingWindowKey))
        {
            if (UIWindowLayoutPrefs.TryLoad(ProcessingWindowKey, out UIWindowLayoutPrefs.Snapshot saved))
                UIWindowLayoutPrefs.Apply(proxy, PrepareGhostSnapshot(saved));
        }
        else
            ResetProxyToFactoryDefault(proxy);

        ApplyProxyLayoutToPanel(proxy, panel, panelCanvas);
    }

    public static void RecordSessionFromPanel(RectTransform panel, Canvas panelCanvas)
    {
        if (!panel || !panelCanvas || !panel.gameObject.activeInHierarchy)
            return;

        if (!TryGetProxy(out RectTransform proxy))
            return;

        ApplyPanelLayoutToProxy(panel, panelCanvas, proxy);
        UIWindowSessionLayoutMemory.Capture(proxy, ProcessingWindowKey);
    }

    public static void SyncProxyPivotFromPanel(RectTransform panel, Canvas panelCanvas)
    {
        if (!panel || !panelCanvas || !panel.gameObject.activeInHierarchy)
            return;

        if (!TryGetProxy(out RectTransform proxy))
            return;

        ApplyPanelLayoutToProxy(panel, panelCanvas, proxy);
        UIWindowLayoutPrefs.Save(proxy, ProcessingWindowKey);
    }

    public static bool TryGetSessionSnapshotForAlign(UIWindowLayoutBinding binding, out UIWindowLayoutPrefs.Snapshot snapshot)
    {
        snapshot = default;
        if (binding == null || !IsProcessingSkillsWindow(binding.MemoryKey))
            return false;

        if (FurnaceUI.IsOpen && FurnaceUI.Instance != null
            && TryCaptureOpenPanelSnapshot(FurnaceUI.Instance.GetLayoutPanel(), FurnaceUI.Instance.GetLayoutCanvas(), out snapshot))
        {
            return true;
        }

        if (CookingUI.IsOpen && CookingUI.Instance != null
            && TryCaptureOpenPanelSnapshot(CookingUI.Instance.GetLayoutPanel(), CookingUI.Instance.GetLayoutCanvas(), out snapshot))
        {
            return true;
        }

        return false;
    }

    public static void ShowTestPreview()
    {
        FurnaceUI ui = FurnaceUI.EnsureInstance();
        ui.ShowLayoutPreview();
        SyncProxyFromVisiblePanel(ui.GetLayoutPanel(), ui.GetLayoutCanvas());
    }

    public static void HideTestPreview()
    {
        FurnaceUI.Instance?.HideLayoutPreview();
        CookingUI.Instance?.HideLayoutPreview();
    }

    public static void EnsureProcessingDragBackdrop(RectTransform panelRoot, Canvas panelCanvas)
    {
        if (!panelRoot || !panelCanvas)
            return;

        Transform existing = panelRoot.Find("ProcessingDragBackdrop");
        if (existing != null)
        {
            if (existing.TryGetComponent(out ProcessingSkillsPanelDrag drag))
                drag.Initialize(panelRoot, panelCanvas);
            existing.SetAsFirstSibling();
            return;
        }

        GameObject dragGo = new GameObject(
            "ProcessingDragBackdrop",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ProcessingSkillsPanelDrag));
        dragGo.layer = panelRoot.gameObject.layer;
        dragGo.transform.SetParent(panelRoot, false);
        dragGo.transform.SetAsFirstSibling();

        RectTransform dragRt = dragGo.GetComponent<RectTransform>();
        dragRt.anchorMin = Vector2.zero;
        dragRt.anchorMax = Vector2.one;
        dragRt.offsetMin = Vector2.zero;
        dragRt.offsetMax = Vector2.zero;

        Image image = dragGo.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        ProcessingSkillsPanelDrag panelDrag = dragGo.GetComponent<ProcessingSkillsPanelDrag>();
        panelDrag.Initialize(panelRoot, panelCanvas);
        UIDragWindowRaycastFilter.EnsureOn(dragGo, panelRoot);
    }

    private static void DestroyLegacyProxy(Transform windowsArea, string legacyKey)
    {
        Transform legacy = windowsArea.Find(legacyKey);
        if (legacy)
            Object.Destroy(legacy.gameObject);
    }

    private static void MigrateLegacySavedLayouts()
    {
        if (UIWindowLayoutPrefs.HasSaved(ProcessingWindowKey))
            return;

        if (UIWindowLayoutPrefs.TryLoad(LegacyFurnaceWindowKey, out UIWindowLayoutPrefs.Snapshot furnaceLayout))
        {
            UIWindowLayoutPrefs.Save(ProcessingWindowKey, PrepareGhostSnapshot(furnaceLayout));
            UIWindowLayoutPrefs.Delete(LegacyFurnaceWindowKey);
            return;
        }

        if (UIWindowLayoutPrefs.TryLoad(LegacyCookingWindowKey, out UIWindowLayoutPrefs.Snapshot cookingLayout))
        {
            UIWindowLayoutPrefs.Save(ProcessingWindowKey, PrepareGhostSnapshot(cookingLayout));
            UIWindowLayoutPrefs.Delete(LegacyCookingWindowKey);
        }
    }

    private static void EnsureProxyAnchor(Transform windowsArea)
    {
        Transform existing = windowsArea.Find(ProcessingWindowKey);
        RectTransform rect;
        if (existing)
        {
            rect = existing as RectTransform ?? existing.GetComponent<RectTransform>();
        }
        else
        {
            GameObject go = new GameObject(ProcessingWindowKey, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = windowsArea.gameObject.layer;
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(windowsArea, false);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
        }

        UIWindowLayoutBinding binding = UIWindowLayoutBinding.EnsureOn(rect, "Processing");
        NormalizeProxyLayout(rect, binding);
        rect.gameObject.SetActive(false);
    }

    private static void NormalizeProxyLayout(RectTransform proxy, UIWindowLayoutBinding binding)
    {
        if (!proxy || binding == null)
            return;

        UIWindowLayoutPrefs.Snapshot snapshot;
        if (UIWindowLayoutPrefs.TryLoad(ProcessingWindowKey, out UIWindowLayoutPrefs.Snapshot saved))
            snapshot = saved;
        else if (UIWindowSessionLayoutMemory.TryGet(ProcessingWindowKey, out UIWindowLayoutPrefs.Snapshot session))
            snapshot = session;
        else
            snapshot = UIWindowLayoutPrefs.Capture(proxy);

        UIWindowLayoutPrefs.Apply(proxy, PrepareGhostSnapshot(snapshot));
        binding.RecaptureFactoryFromCurrentLayout();
    }

    public static void ResetProxyToFactoryDefault(RectTransform proxy)
    {
        if (!proxy)
            return;

        Vector2 size = GetProxySizeInWindowsArea();
        proxy.anchorMin = CenterAnchor;
        proxy.anchorMax = CenterAnchor;
        proxy.pivot = TopLeftPivot;
        proxy.sizeDelta = size;
        proxy.anchoredPosition = new Vector2(-size.x * 0.5f, size.y * 0.5f);
        proxy.localScale = Vector3.one;
    }

    private static void SyncProxyFromVisiblePanel(RectTransform panel, Canvas panelCanvas)
    {
        if (!panel || !panelCanvas || !panel.gameObject.activeInHierarchy)
            return;

        if (!TryGetProxy(out RectTransform proxy))
            return;

        ApplyPanelLayoutToProxy(panel, panelCanvas, proxy);

        UIWindowLayoutBinding binding = proxy.GetComponent<UIWindowLayoutBinding>();
        binding?.RecaptureFactoryFromCurrentLayout();
    }

    private static bool TryGetProxy(out RectTransform proxy)
    {
        proxy = null;
        Transform windowsArea = ResolveWindowsArea();
        if (!windowsArea)
            return false;

        Transform t = windowsArea.Find(ProcessingWindowKey);
        if (!t)
            return false;

        proxy = t as RectTransform ?? t.GetComponent<RectTransform>();
        return proxy != null;
    }

    private static Transform ResolveWindowsArea()
    {
        GameObject area = GameObject.Find("WindowsArea");
        if (area)
            return area.transform;

        GameObject canvas = GameObject.Find("FullWindowCanvas");
        if (!canvas)
            canvas = GameObject.FindWithTag("FullWindowCanvas");

        return canvas != null ? canvas.transform.Find("WindowsArea") : null;
    }

    private static Vector2 MeasureDefaultPanelFootprintInWindowsArea()
    {
        RectTransform windowsArea = ResolveWindowsArea() as RectTransform;
        if (!windowsArea)
            return PanelDesignSize;

        FurnaceUI furnace = FurnaceUI.EnsureInstance();
        RectTransform panel = furnace.GetLayoutPanel();
        Canvas panelCanvas = furnace.GetLayoutCanvas();
        if (!panel || !panelCanvas)
            return PanelDesignSize;

        bool wasActive = panel.gameObject.activeInHierarchy;
        UIWindowLayoutPrefs.Snapshot saved = UIWindowLayoutPrefs.Capture(panel);

        try
        {
            panel.gameObject.SetActive(true);
            panel.anchorMin = CenterAnchor;
            panel.anchorMax = CenterAnchor;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = PanelDesignSize;
            panel.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();

            return MeasureRectFootprintInParent(panel, panelCanvas, windowsArea);
        }
        finally
        {
            UIWindowLayoutPrefs.Apply(panel, saved);
            if (!wasActive)
                panel.gameObject.SetActive(false);
        }
    }

    private static Vector2 MeasureRectFootprintInParent(
        RectTransform rect,
        Canvas sourceCanvas,
        RectTransform targetParent)
    {
        if (!rect || !sourceCanvas || !targetParent)
            return PanelDesignSize;

        Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
        Camera targetCamera = ResolveEventCamera(targetParent);

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector2 screenTopLeft = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[1]);
        Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[2]);
        Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[0]);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screenTopLeft, targetCamera, out Vector2 topLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screenTopRight, targetCamera, out Vector2 topRight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screenBottomLeft, targetCamera, out Vector2 bottomLeft);

        return new Vector2(Mathf.Abs(topRight.x - topLeft.x), Mathf.Abs(topLeft.y - bottomLeft.y));
    }

    private static Vector2 ConvertToTopLeftAnchoredPosition(
        Vector2 anchoredPosition,
        Vector2 pivot,
        Vector2 sizeDelta,
        Vector3 localScale)
    {
        float topLeftX = anchoredPosition.x - pivot.x * sizeDelta.x * localScale.x;
        float topLeftY = anchoredPosition.y + (1f - pivot.y) * sizeDelta.y * localScale.y;
        return new Vector2(topLeftX, topLeftY);
    }

    private static void ApplyProxyLayoutToPanel(RectTransform proxy, RectTransform panel, Canvas panelCanvas)
    {
        if (!proxy || !panel || !panelCanvas)
            return;

        if (!TryGetScreenFootprint(proxy, null, out Vector2 screenTopLeft, out Vector2 screenTopRight, out Vector2 screenBottomLeft))
            return;

        Camera panelCamera = panelCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : panelCanvas.worldCamera;
        RectTransform canvasRect = panelCanvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenTopLeft, panelCamera, out Vector2 localTopLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenTopRight, panelCamera, out Vector2 localTopRight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenBottomLeft, panelCamera, out Vector2 localBottomLeft);

        float width = Mathf.Abs(localTopRight.x - localTopLeft.x);
        float height = Mathf.Abs(localTopLeft.y - localBottomLeft.y);

        panel.anchorMin = CenterAnchor;
        panel.anchorMax = CenterAnchor;
        panel.pivot = TopLeftPivot;
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = localTopLeft;
        panel.localScale = proxy.localScale;
    }

    private static void ApplyPanelLayoutToProxy(RectTransform panel, Canvas panelCanvas, RectTransform proxy)
    {
        if (!proxy || !panel || !panelCanvas)
            return;

        if (!TryGetScreenFootprint(panel, panelCanvas, out Vector2 screenTopLeft, out Vector2 screenTopRight, out Vector2 screenBottomLeft))
            return;

        RectTransform proxyParent = proxy.parent as RectTransform;
        if (!proxyParent)
            return;

        Camera targetCamera = ResolveEventCamera(proxyParent);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(proxyParent, screenTopLeft, targetCamera, out Vector2 localTopLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(proxyParent, screenTopRight, targetCamera, out Vector2 localTopRight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(proxyParent, screenBottomLeft, targetCamera, out Vector2 localBottomLeft);

        float width = Mathf.Abs(localTopRight.x - localTopLeft.x);
        float height = Mathf.Abs(localTopLeft.y - localBottomLeft.y);

        proxy.anchorMin = CenterAnchor;
        proxy.anchorMax = CenterAnchor;
        proxy.pivot = TopLeftPivot;
        proxy.sizeDelta = new Vector2(width, height);
        proxy.anchoredPosition = localTopLeft;
        proxy.localScale = panel.localScale;
    }

    private static bool TryGetScreenFootprint(
        RectTransform rect,
        Canvas sourceCanvas,
        out Vector2 screenTopLeft,
        out Vector2 screenTopRight,
        out Vector2 screenBottomLeft)
    {
        screenTopLeft = default;
        screenTopRight = default;
        screenBottomLeft = default;

        if (!rect)
            return false;

        Camera sourceCamera = null;
        if (sourceCanvas != null)
            sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        screenBottomLeft = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[0]);
        screenTopLeft = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[1]);
        screenTopRight = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[2]);
        return true;
    }

    private static Camera ResolveEventCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private static bool TryCaptureOpenPanelSnapshot(RectTransform panel, Canvas panelCanvas, out UIWindowLayoutPrefs.Snapshot snapshot)
    {
        snapshot = default;
        if (!panel || !panelCanvas || !panel.gameObject.activeInHierarchy)
            return false;

        if (!TryGetProxy(out RectTransform proxy))
            return false;

        ApplyPanelLayoutToProxy(panel, panelCanvas, proxy);
        snapshot = PrepareGhostSnapshot(UIWindowLayoutPrefs.Capture(proxy));
        return true;
    }
}
