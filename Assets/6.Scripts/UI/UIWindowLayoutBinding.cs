using UnityEngine;

/// <summary>
/// Tracks factory and saved layout for a movable HUD window. Added at runtime by <see cref="MovePivotsModeController"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIWindowLayoutBinding : MonoBehaviour
{
    public static readonly (string WindowObjectName, string DisplayLabel)[] KnownMovePivotWindows =
    {
        ("MainMenuWindow", "Main menu"),
        ("GameActivityWindow", "Activity log"),
        ("TrackerWindow", "Tracker"),
        ("FullDPSWindow", "DPS meter"),
        ("ActionBarWindow", "Action bar"),
        ("QuestTrackerWindow", "Quest tracker"),
        ("ShopWindow", "Shop")
    };

    private static readonly Color[] PivotGhostFillPalette =
    {
        new Color(0.72f, 0.45f, 0.45f, 0.74f),
        new Color(0.45f, 0.58f, 0.72f, 0.74f),
        new Color(0.48f, 0.68f, 0.50f, 0.74f),
        new Color(0.70f, 0.58f, 0.40f, 0.74f),
        new Color(0.62f, 0.48f, 0.70f, 0.74f),
        new Color(0.45f, 0.65f, 0.62f, 0.74f),
        new Color(0.75f, 0.55f, 0.48f, 0.74f),
    };

    [SerializeField] private RectTransform windowRect;
    [SerializeField] private string displayLabel;
    [SerializeField] private string memoryKey;

    private UIWindowLayoutPrefs.Snapshot _factorySnapshot;
    private bool _factoryCaptured;

    public RectTransform WindowRect => windowRect;
    public string DisplayLabel => string.IsNullOrWhiteSpace(displayLabel) ? memoryKey : displayLabel;
    public string MemoryKey => memoryKey;

    public static bool IsKnownPivotWindow(string memoryKey)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return false;

        string key = memoryKey.Trim();
        for (int i = 0; i < KnownMovePivotWindows.Length; i++)
        {
            if (KnownMovePivotWindows[i].WindowObjectName == key)
                return true;
        }

        return false;
    }

    public static int GetKnownWindowPaletteIndex(string memoryKey)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return 0;

        string key = memoryKey.Trim();
        for (int i = 0; i < KnownMovePivotWindows.Length; i++)
        {
            if (KnownMovePivotWindows[i].WindowObjectName == key)
                return i;
        }

        return 0;
    }

    public static Color GetPivotGhostFillColor(string memoryKey)
    {
        int index = GetKnownWindowPaletteIndex(memoryKey);
        return PivotGhostFillPalette[index % PivotGhostFillPalette.Length];
    }

    public static Color GetPivotGhostBorderColor(Color fillColor)
    {
        return new Color(fillColor.r * 0.55f, fillColor.g * 0.55f, fillColor.b * 0.55f, 1f);
    }

    public static Color GetPivotGhostLabelColor(Color fillColor)
    {
        return new Color(fillColor.r * 0.35f, fillColor.g * 0.35f, fillColor.b * 0.35f, 1f);
    }

    public static bool IsQuestTrackerWindow(string memoryKey) =>
        string.Equals(memoryKey, "QuestTrackerWindow", System.StringComparison.Ordinal);

    public static bool IsShopWindow(string memoryKey) =>
        string.Equals(memoryKey, "ShopWindow", System.StringComparison.Ordinal);

    public static bool IsMainMenuWindow(string memoryKey) =>
        string.Equals(memoryKey, "MainMenuWindow", System.StringComparison.Ordinal);

    public static bool IsActionBarWindow(string memoryKey) =>
        string.Equals(memoryKey, "ActionBarWindow", System.StringComparison.Ordinal);

    /// <summary>Main menu and shop never open together in-game — keep ghosts during Test View.</summary>
    public static bool UsesGhostDuringTestView(string memoryKey) =>
        IsMainMenuWindow(memoryKey) || IsShopWindow(memoryKey);

    public static float GetPivotGhostMinimumHeight(string memoryKey)
    {
        if (IsQuestTrackerWindow(memoryKey))
            return QuestTrackerWindowUI.GetPivotPlaceholderHeight();

        if (IsActionBarWindow(memoryKey))
            return ActionBarUI.GetPivotPlaceholderHeight();

        return 180f;
    }

    /// <summary>Quest tracker rows grow downward — pivot ghosts use a top anchor so drag position is the window top.</summary>
    public static UIWindowLayoutPrefs.Snapshot PreparePivotGhostSnapshot(
        UIWindowLayoutBinding binding,
        in UIWindowLayoutPrefs.Snapshot snapshot)
    {
        if (binding == null)
            return snapshot;

        UIWindowLayoutPrefs.Snapshot prepared = snapshot;
        if (!IsQuestTrackerWindow(binding.MemoryKey) && prepared.sizeDelta.y < GetPivotGhostMinimumHeight(binding.MemoryKey))
            prepared.sizeDelta.y = GetPivotGhostMinimumHeight(binding.MemoryKey);
        return prepared;
    }

    /// <summary>Fresh game load / new game — clear session moves and apply saved pivot layouts.</summary>
    public static void RestoreAllPivotLayoutsForGameLoad()
    {
        UIWindowPositionMemory.ForgetAll();
        UIWindowSessionLayoutMemory.ForgetAll();
        RestoreAllKnownWindowLayouts(RestorePivotLayoutOnBinding);
    }

    /// <summary>Map scene reload within the same play session — keep session drag/resize positions.</summary>
    public static void RestoreSessionLayoutsForSceneChange()
    {
        RestoreAllKnownWindowLayouts(binding =>
        {
            if (UIWindowSessionLayoutMemory.TryGet(binding.MemoryKey, out UIWindowLayoutPrefs.Snapshot session))
            {
                binding.ApplySnapshot(session);
                UIWindowPositionMemory.Save(binding.MemoryKey, binding.WindowRect.anchoredPosition);
                return;
            }

            binding.RestorePivotLayout();
        });
    }

    private static void RestoreAllKnownWindowLayouts(System.Action<UIWindowLayoutBinding> applyLayout)
    {
        Transform windowsArea = ResolveWindowsArea();
        if (!windowsArea)
            return;

        for (int i = 0; i < KnownMovePivotWindows.Length; i++)
        {
            (string objectName, string label) = KnownMovePivotWindows[i];
            Transform t = FindChildRecursive(windowsArea, objectName);
            if (!t)
                continue;

            RectTransform rect = t as RectTransform ?? t.GetComponent<RectTransform>();
            if (!rect)
                continue;

            UIWindowLayoutBinding binding = EnsureOn(rect, label);
            applyLayout?.Invoke(binding);
        }
    }

    private static Transform ResolveWindowsArea()
    {
        Transform windowsArea = GameObject.Find("WindowsArea")?.transform;
        if (windowsArea)
            return windowsArea;

        GameObject canvas = GameObject.Find("FullWindowCanvas");
        return canvas != null ? canvas.transform.Find("WindowsArea") : null;
    }

    private static void RestorePivotLayoutOnBinding(UIWindowLayoutBinding binding) =>
        binding?.RestorePivotLayout();

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (!parent || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!child)
                continue;

            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested)
                return nested;
        }

        return null;
    }

    public static UIWindowLayoutBinding EnsureOn(RectTransform rect, string label)
    {
        if (!rect)
            return null;

        UIWindowLayoutBinding binding = rect.GetComponent<UIWindowLayoutBinding>();
        if (!binding)
            binding = rect.gameObject.AddComponent<UIWindowLayoutBinding>();

        binding.windowRect = rect;
        binding.displayLabel = label;
        if (string.IsNullOrWhiteSpace(binding.memoryKey))
            binding.memoryKey = rect.name;

        binding.CaptureFactoryIfNeeded();
        return binding;
    }

    private void Awake()
    {
        if (!windowRect)
            windowRect = transform as RectTransform;

        if (string.IsNullOrWhiteSpace(memoryKey) && windowRect)
            memoryKey = windowRect.name;

        CaptureFactoryIfNeeded();
    }

    public void CaptureFactoryIfNeeded()
    {
        if (_factoryCaptured || !windowRect)
            return;

        _factorySnapshot = UIWindowLayoutPrefs.Capture(windowRect);
        _factoryCaptured = true;
    }

    public void RestoreSavedLayout() => RestorePivotLayout();

    public void RestorePivotLayout()
    {
        if (!windowRect)
            return;

        if (UIWindowLayoutPrefs.HasSaved(memoryKey))
        {
            UIWindowLayoutPrefs.TryLoadAndApply(windowRect, memoryKey);
        }
        else if (_factoryCaptured)
        {
            UIWindowLayoutPrefs.Apply(windowRect, _factorySnapshot);
        }

        if (IsQuestTrackerWindow(memoryKey))
            EnsureQuestTrackerTopAnchoredLayout();

        UIWindowPositionMemory.ForgetKey(memoryKey);
        UIWindowSessionLayoutMemory.ForgetKey(memoryKey);
        UIWindowPositionMemory.Save(memoryKey, windowRect.anchoredPosition);
        UIWindowSessionLayoutMemory.Capture(windowRect, memoryKey);

        UIWindowCornerResize resize = windowRect.GetComponent<UIWindowCornerResize>();
        if (resize != null && UIWindowLayoutPrefs.HasSaved(memoryKey))
            resize.ApplyLayoutScaleFromSnapshot(windowRect.localScale);
        else
            resize?.ForgetPersistedScaleAndResetToBase();

        if (IsActionBarWindow(memoryKey))
            ActionBarUI.SyncWindowOnPivotLayoutApplied(windowRect);
    }

    public void SaveCurrentLayout()
    {
        if (!windowRect)
            return;

        UIWindowLayoutPrefs.Save(windowRect, memoryKey);
        UIWindowPositionMemory.Save(memoryKey, windowRect.anchoredPosition);

        UIWindowCornerResize resize = windowRect.GetComponent<UIWindowCornerResize>();
        resize?.SyncPersistedScaleFromCurrentTransform();
    }

    public void ApplySnapshot(in UIWindowLayoutPrefs.Snapshot snapshot)
    {
        if (!windowRect)
            return;

        UIWindowLayoutPrefs.Apply(windowRect, snapshot);

        if (IsQuestTrackerWindow(memoryKey))
            EnsureQuestTrackerTopAnchoredLayout(windowRect);

        if (IsActionBarWindow(memoryKey))
            ActionBarUI.SyncWindowOnPivotLayoutApplied(windowRect);
    }

    private void EnsureQuestTrackerTopAnchoredLayout()
    {
        EnsureQuestTrackerTopAnchoredLayout(windowRect);
    }

    public static void EnsureQuestTrackerTopAnchoredLayout(RectTransform windowRect)
    {
        if (!windowRect)
            return;

        UIWindowLayoutPrefs.Snapshot trackerSnapshot = UIWindowLayoutPrefs.Capture(windowRect);
        if (trackerSnapshot.anchorMin.y > 0.99f && trackerSnapshot.pivot.y > 0.99f)
            return;

        QuestTrackerWindowUI.ApplyTopAnchoredPivotLayout(
            windowRect,
            Mathf.Max(windowRect.sizeDelta.y, windowRect.rect.height, 1f));
    }

    public UIWindowLayoutPrefs.Snapshot GetCurrentSnapshot() =>
        windowRect ? UIWindowLayoutPrefs.Capture(windowRect) : default;

    public void ResetToFactory()
    {
        if (!windowRect || !_factoryCaptured)
            return;

        UIWindowLayoutPrefs.Apply(windowRect, _factorySnapshot);
        UIWindowLayoutPrefs.Delete(memoryKey);
        UIWindowPositionMemory.ForgetKey(memoryKey);
        UIWindowSessionLayoutMemory.ForgetKey(memoryKey);

        UIWindowCornerResize resize = windowRect.GetComponent<UIWindowCornerResize>();
        resize?.ForgetPersistedScaleAndResetToBase();
    }
}
