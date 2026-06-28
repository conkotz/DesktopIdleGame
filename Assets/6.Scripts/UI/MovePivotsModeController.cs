using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// When <see cref="ToggleSettingId.MoveWindowPivots"/> is on, closes HUD windows and shows colored semi-transparent rectangle placeholders.
/// </summary>
[DisallowMultipleComponent]
public sealed class MovePivotsModeController : MonoBehaviour
{
    private const int OverlayCanvasSortOrder = 10005;
    private const float OverlayButtonHeight = 40f;
    private const float OverlaySaveButtonWidth = 280f;
    private const float OverlayWideButtonWidth = 340f;
    private const float OverlayVisibilityButtonWidth = 280f;
    private const float OverlayResetButtonWidth = 240f;
    private const float OverlaySaveButtonHeight = 48f;
    private static readonly Color OverlayButtonResetColor = new(0.78f, 0.24f, 0.24f, 0.96f);
    private const float OverlayButtonStackGap = 8f;
    private const float OverlayTopInset = 12f;

    private static readonly Color OverlayButtonDefaultColor = new(0.22f, 0.2f, 0.16f, 0.92f);
    private static readonly Color OverlayButtonSaveColor = new(0.2f, 0.42f, 0.28f, 0.92f);
    private static readonly Color OverlayButtonCancelColor = new(0.48f, 0.22f, 0.22f, 0.92f);

    private static MovePivotsModeController _instance;

    public static bool IsTestViewActive =>
        _instance != null && _instance._testViewActive;

    public static bool IsPivotModeActive =>
        _instance != null && _instance._active;

    public static void NotifyGhostBroughtToFront(WindowPivotGhostUI ghost)
    {
        if (_instance == null || ghost == null)
            return;

        _instance.SyncBringToFrontOverlayOrder();
    }

    public static bool TryGetPivotGhost(string memoryKey, out WindowPivotGhostUI ghost)
    {
        ghost = null;
        if (_instance == null || string.IsNullOrWhiteSpace(memoryKey))
            return false;

        for (int i = 0; i < _instance._ghosts.Count; i++)
        {
            WindowPivotGhostUI candidate = _instance._ghosts[i];
            if (candidate?.Binding == null)
                continue;

            if (!string.Equals(candidate.Binding.MemoryKey, memoryKey, System.StringComparison.Ordinal))
                continue;

            ghost = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Re-applies the ghost placeholder layout onto the real window (e.g. after quest tracker content rebuild in test view).</summary>
    public static bool TryReapplyGhostLayoutForWindow(string memoryKey)
    {
        if (!TryGetPivotGhost(memoryKey, out WindowPivotGhostUI ghost) || ghost?.Binding == null)
            return false;

        ghost.Binding.ApplySnapshot(ghost.GetCurrentSnapshot());
        return true;
    }

    public static void NotifyGhostLayoutEdited(string memoryKey, in UIWindowLayoutPrefs.Snapshot snapshot)
    {
        if (_instance == null || string.IsNullOrWhiteSpace(memoryKey))
            return;

        _instance.RecordGhostSnapshot(memoryKey, snapshot);
    }

    private readonly List<UIWindowLayoutBinding> _bindings = new(8);
    private readonly List<UIWindowLayoutPrefs.Snapshot> _ghostSnapshots = new(8);
    private readonly List<UIWindowLayoutPrefs.Snapshot> _preEditBindingSnapshots = new(8);
    private readonly List<UIWindowLayoutPrefs.Snapshot> _entrySavedPivotSnapshots = new(8);
    private readonly List<bool> _entryHadSavedPivot = new(8);
    private readonly Dictionary<string, Rect[]> _actionBarSlotHintRects = new(1);
    private readonly List<WindowPivotGhostUI> _ghosts = new(8);
    private readonly List<PivotGhostBringToFrontOverlayButton> _bringToFrontOverlayButtons = new(8);
    private readonly List<PivotGhostLabelOverlay> _labelOverlays = new(8);
    private readonly Dictionary<GameObject, bool> _rememberedActive = new(16);

    private RectTransform _overlayRoot;
    private RectTransform _windowsParent;
    private bool _active;
    private bool _testViewActive;
    private bool _restoreMainMenuSettingsOnExit;
    private bool _userFinishedArranging;
    private bool _commitOnExit;
    private Button _testViewButton;
    private Button _alignButton;
    private Button _cancelButton;
    private Button _visibilityButton;
    private Button _resetPivotsButton;
    private TMP_Text _testViewButtonLabel;
    private PivotWindowVisibilityMenu _visibilityMenu;
    private readonly Dictionary<string, bool> _pivotGhostVisibleByKey = new(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (IsBootstrapScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
        {
            UIWindowLayoutBinding.ResetGameLoadLayoutState();
            UIWindowPositionMemory.ForgetAll();
            return;
        }

        if (IsGameplayScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
            EnsureExists();
    }

    private static bool IsBootstrapScene(UnityEngine.SceneManagement.Scene scene) =>
        scene.IsValid() && scene.name.Equals("Bootstrap", System.StringComparison.OrdinalIgnoreCase);

    private static bool IsGameplayScene(UnityEngine.SceneManagement.Scene scene) =>
        scene.IsValid() && scene.name.Equals("GamePlay", System.StringComparison.OrdinalIgnoreCase);

    public static void EnsureExists()
    {
        if (FindWindowsArea() == null)
            return;

        EnsureBindingsInScene();

        MovePivotsModeController existing =
            FindFirstObjectByType<MovePivotsModeController>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        Transform hostParent = FindFullWindowCanvasTransform();
        GameObject host = new GameObject(nameof(MovePivotsModeController));
        if (hostParent)
            host.transform.SetParent(hostParent, false);

        host.AddComponent<MovePivotsModeController>();
    }

    public static void RefreshAllFromSettings()
    {
        EnsureExists();
        MovePivotsModeController controller =
            FindFirstObjectByType<MovePivotsModeController>(FindObjectsInactive.Include);
        controller?.RefreshFromSettings();
    }

    private static void EnsureBindingsInScene()
    {
        Transform windowsArea = FindWindowsArea();
        if (!windowsArea)
            return;

        for (int i = 0; i < UIWindowLayoutBinding.KnownMovePivotWindows.Length; i++)
        {
            (string objectName, string label) = UIWindowLayoutBinding.KnownMovePivotWindows[i];
            Transform t = FindChildRecursive(windowsArea, objectName);
            if (!t)
                continue;

            RectTransform rect = t as RectTransform ?? t.GetComponent<RectTransform>();
            if (rect)
                UIWindowLayoutBinding.EnsureOn(rect, label);
        }

        ProcessingSkillsWindowLayout.EnsureProxyAnchors();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleChanged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        MovePivotsSettingsInstaller.EnsureSettingsRowExists();
        RefreshFromSettings();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleChanged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_instance == this && _active)
            SetActiveInternal(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single)
            return;

        _instance = null;
        _active = false;

        if (IsBootstrapScene(scene))
        {
            UIWindowLayoutBinding.ResetGameLoadLayoutState();
            UIWindowPositionMemory.ForgetAll();
            return;
        }

        if (!IsGameplayScene(scene))
            return;

        EnsureExists();
        RefreshAllFromSettings();
    }

    private void Update()
    {
        if (!_active)
            return;

        if (!_testViewActive)
            EnforceBoundWindowsHidden();

        if (!IsConflictingWindowOpen())
            return;

        GameLog.Add(
            "Move pivots closed — another window was opened.",
            GameLog.CannotMessageColor);
        ToggleSettingsStore.Set(ToggleSettingId.MoveWindowPivots, false);
    }

    private static bool IsConflictingWindowOpen()
    {
        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu != null && menu.IsOpen)
            return true;

        if (MerchantClick.MerchantModeOpen || MerchantClick.IsShopOpen)
            return true;

        if (StorageUI.IsOpen)
            return true;

        if (QuickMenuPanelToggleUI.IsOpen)
            return true;

        return false;
    }

    private void OnToggleChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.MoveWindowPivots)
            RefreshFromSettings();
    }

    public void RefreshFromSettings()
    {
        bool shouldBeActive = ToggleSettingsStore.Get(ToggleSettingId.MoveWindowPivots);
        if (shouldBeActive)
        {
            if (_active && (_ghosts.Count > 0 || _testViewActive))
                return;

            SetActiveInternal(true);
            return;
        }

        if (_active)
            SetActiveInternal(false);
    }

    private void SetActiveInternal(bool active)
    {
        if (!active)
        {
            if (!_active)
                return;

            _active = false;
            if (_userFinishedArranging)
            {
                GameLog.Add("Pivot positions set");
                _userFinishedArranging = false;
            }

            if (_testViewActive)
                ExitTestView(restoreWindowVisibility: false);
            MerchantClick.ForceCloseMerchantMode();

            if (_commitOnExit)
            {
                CommitAllGhostLayoutsAndSave();
                ApplySavedPivotsAsLiveLayout();
                ClearPivotEditSnapshots();
            }
            else
            {
                RestorePreEditBindingSnapshots();
                RestoreEntrySavedPivotSnapshots();
            }

            _commitOnExit = false;
            DestroyGhosts();
            RestoreWindowVisibility(reloadPivotLayoutFromPrefs: false);
            RestoreMainMenuSettingsIfNeeded();
            _visibilityMenu?.SetExpanded(false);
            if (_overlayRoot)
                _overlayRoot.gameObject.SetActive(false);
            return;
        }

        _testViewActive = false;
        _restoreMainMenuSettingsOnExit = false;
        _rememberedActive.Clear();
        _pivotGhostVisibleByKey.Clear();
        DiscoverBindings();

        if (_bindings.Count == 0)
        {
            _active = false;
            Debug.LogWarning("[MovePivotsModeController] No movable windows found under WindowsArea.");
            GameLog.Add("Move pivots could not start — no movable windows were found.", GameLog.CannotMessageColor);
            if (ToggleSettingsStore.Get(ToggleSettingId.MoveWindowPivots))
                ToggleSettingsStore.Set(ToggleSettingId.MoveWindowPivots, false);
            return;
        }

        CaptureEntrySavedPivotSnapshots();
        CapturePreEditBindingSnapshots();
        CaptureGhostSnapshots();
        CloseMenuMerchantAndStorage();
        HideAllBoundWindows();
        EnforceBoundWindowsHidden();
        EnsureOverlay();
        RebuildGhosts();
        InitializePivotVisibilityState();
        RebuildVisibilityMenu();

        if (_ghosts.Count == 0)
        {
            _active = false;
            RestoreWindowVisibility(reloadPivotLayoutFromPrefs: true);
            Debug.LogWarning("[MovePivotsModeController] Failed to create pivot placeholders.");
            GameLog.Add("Move pivots could not start — failed to create placeholders.", GameLog.CannotMessageColor);
            ToggleSettingsStore.Set(ToggleSettingId.MoveWindowPivots, false);
            return;
        }

        _active = true;
        SyncBringToFrontOverlayVisibility();
        UpdateTestViewButtonLabel();

        GameLog.Add("Move pivots active");
    }

    private void CloseMenuMerchantAndStorage()
    {
        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu != null && menu.IsOpen)
        {
            _restoreMainMenuSettingsOnExit = menu.IsSettingsPageOpen;
            menu.Close();
        }
        else
            _restoreMainMenuSettingsOnExit = false;

        MerchantClick.ForceCloseMerchantMode();
        StorageClick.ForceCloseStorageMode();

        if (FurnaceUI.IsOpen)
            FurnaceUI.Instance?.Close();
        if (CookingUI.IsOpen)
            CookingUI.Instance?.Close();

        if (QuickMenuPanelToggleUI.IsOpen)
        {
            GameObject quickMenu = GameObject.Find("QuickMenuPanel");
            if (quickMenu)
                _rememberedActive[quickMenu] = true;

            QuickMenuPanelToggleUI.HideIfOpen();
        }
    }

    private void DiscoverBindings()
    {
        _bindings.Clear();

        Transform windowsArea = FindWindowsArea();
        if (!windowsArea)
            return;

        _windowsParent = windowsArea as RectTransform;
        ProcessingSkillsWindowLayout.EnsureProxyAnchors();

        for (int i = 0; i < UIWindowLayoutBinding.KnownMovePivotWindows.Length; i++)
        {
            (string objectName, string label) = UIWindowLayoutBinding.KnownMovePivotWindows[i];
            Transform t = FindChildRecursive(windowsArea, objectName);
            if (!t)
                continue;

            RectTransform rect = t as RectTransform ?? t.GetComponent<RectTransform>();
            if (!rect)
                continue;

            UIWindowLayoutBinding binding = UIWindowLayoutBinding.EnsureOn(rect, label);
            if (binding != null)
                _bindings.Add(binding);
        }
    }

    private void CaptureEntrySavedPivotSnapshots()
    {
        _entrySavedPivotSnapshots.Clear();
        _entryHadSavedPivot.Clear();

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
            {
                _entrySavedPivotSnapshots.Add(default);
                _entryHadSavedPivot.Add(false);
                continue;
            }

            _entryHadSavedPivot.Add(UIWindowLayoutPrefs.HasSaved(binding.MemoryKey));
            _entrySavedPivotSnapshots.Add(binding.GetSavedPivotSnapshotForEditing());
        }
    }

    private void CapturePreEditBindingSnapshots()
    {
        _preEditBindingSnapshots.Clear();

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
            {
                _preEditBindingSnapshots.Add(default);
                continue;
            }

            string key = binding.MemoryKey;
            if (ProcessingSkillsWindowLayout.TryGetSessionSnapshotForAlign(binding, out UIWindowLayoutPrefs.Snapshot processingSnapshot))
                _preEditBindingSnapshots.Add(processingSnapshot);
            else if (UIWindowSessionLayoutMemory.TryGet(key, out UIWindowLayoutPrefs.Snapshot session))
                _preEditBindingSnapshots.Add(session);
            else if (binding.WindowRect != null)
                _preEditBindingSnapshots.Add(binding.GetCurrentSnapshot());
            else
                _preEditBindingSnapshots.Add(binding.GetSessionLayoutSnapshotForAlign());
        }
    }

    private void CaptureGhostSnapshots()
    {
        _ghostSnapshots.Clear();
        _actionBarSlotHintRects.Clear();

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
                continue;

            _ghostSnapshots.Add(binding.GetSavedPivotSnapshotForEditing());

            if (UIWindowLayoutBinding.IsActionBarWindow(binding.MemoryKey))
                CaptureActionBarSlotHintsFromSnapshot(binding, _ghostSnapshots[_ghostSnapshots.Count - 1]);
        }
    }

    private void CaptureActionBarSlotHintsFromSnapshot(
        UIWindowLayoutBinding binding,
        in UIWindowLayoutPrefs.Snapshot snapshot)
    {
        if (binding?.WindowRect == null || !UIWindowLayoutBinding.IsActionBarWindow(binding.MemoryKey))
            return;

        RectTransform rect = binding.WindowRect;
        UIWindowLayoutPrefs.Snapshot previous = UIWindowLayoutPrefs.Capture(rect);
        UIWindowLayoutPrefs.Apply(rect, snapshot);
        ActionBarUI.SyncWindowOnPivotLayoutApplied(rect);
        _actionBarSlotHintRects[binding.MemoryKey] = ActionBarUI.CapturePivotSlotRects(rect);
        UIWindowLayoutPrefs.Apply(rect, previous);
        ActionBarUI.SyncWindowOnPivotLayoutApplied(rect);
    }

    private UIWindowLayoutPrefs.Snapshot ResolveSessionLayoutSnapshot(UIWindowLayoutBinding binding, int bindingIndex)
    {
        if (binding == null)
            return default;

        if (bindingIndex >= 0 && bindingIndex < _preEditBindingSnapshots.Count)
            return _preEditBindingSnapshots[bindingIndex];

        return binding.GetSessionLayoutSnapshotForAlign();
    }

    private void RestoreEntrySavedPivotSnapshots()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.MemoryKey))
                continue;

            if (i >= _entrySavedPivotSnapshots.Count)
                continue;

            UIWindowLayoutPrefs.Snapshot snapshot = _entrySavedPivotSnapshots[i];
            if (i < _entryHadSavedPivot.Count && _entryHadSavedPivot[i])
                UIWindowLayoutPrefs.Save(binding.MemoryKey, snapshot);
            else
                UIWindowLayoutPrefs.Delete(binding.MemoryKey);
        }

        PlayerPrefs.Save();
    }

    private void ClearPivotEditSnapshots()
    {
        _entrySavedPivotSnapshots.Clear();
        _entryHadSavedPivot.Clear();
        _preEditBindingSnapshots.Clear();
        _ghostSnapshots.Clear();
    }

    private void RecordGhostSnapshot(string memoryKey, in UIWindowLayoutPrefs.Snapshot snapshot)
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || binding.MemoryKey != memoryKey)
                continue;

            while (_ghostSnapshots.Count <= i)
                _ghostSnapshots.Add(default);

            _ghostSnapshots[i] = snapshot;
            return;
        }
    }

    private void RestorePreEditBindingSnapshots()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || i >= _preEditBindingSnapshots.Count)
                continue;

            binding.ApplySnapshot(_preEditBindingSnapshots[i]);
        }
    }

    private void ApplySavedPivotsAsLiveLayout()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding?.WindowRect == null || string.IsNullOrWhiteSpace(binding.MemoryKey))
                continue;

            string key = binding.MemoryKey;
            UIWindowSessionLayoutMemory.ForgetKey(key);
            UIWindowPositionMemory.ForgetKey(key);
            UIWindowSessionLayoutMemory.Capture(binding.WindowRect, key);
            UIWindowPositionMemory.Save(key, binding.WindowRect.anchoredPosition);
        }
    }

    private void AlignGhostsWithCurrentWindowPositions()
    {
        if (_testViewActive)
            ExitTestView(restoreWindowVisibility: true);

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
                continue;

            UIWindowLayoutPrefs.Snapshot snapshot = ResolveSessionLayoutSnapshot(binding, i);

            if (i < _ghosts.Count && _ghosts[i] != null)
                _ghosts[i].ApplyLayoutSnapshot(snapshot);

            RecordGhostSnapshot(binding.MemoryKey, snapshot);

            if (UIWindowLayoutBinding.IsActionBarWindow(binding.MemoryKey))
                CaptureActionBarSlotHintsFromSnapshot(binding, snapshot);
        }

        RefreshAllGhostChrome();
        GameLog.Add("Pivots aligned with current window positions");
    }

    private void ExitWithoutSaving()
    {
        _commitOnExit = false;
        ToggleSettingsStore.Set(ToggleSettingId.MoveWindowPivots, false);
    }

    private void SaveLayoutAndExit()
    {
        _commitOnExit = true;
        _userFinishedArranging = true;
        ToggleSettingsStore.Set(ToggleSettingId.MoveWindowPivots, false);
    }

    private void HideAllBoundWindows()
    {
        for (int i = 0; i < _bindings.Count; i++)
            HideBoundWindow(_bindings[i]);
    }

    private void EnforceBoundWindowsHidden()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || !binding.WindowRect)
                continue;

            if (binding.WindowRect.gameObject.activeSelf)
                HideBoundWindow(binding);
        }
    }

    private void HideBoundWindow(UIWindowLayoutBinding binding)
    {
        if (binding == null || !binding.WindowRect)
            return;

        GameObject go = binding.WindowRect.gameObject;
        if (!_rememberedActive.ContainsKey(go))
            _rememberedActive[go] = go.activeSelf;

        go.SetActive(false);
    }

    private static Transform FindWindowsArea()
    {
        GameObject area = GameObject.Find("WindowsArea");
        if (area)
            return area.transform;

        GameObject canvas = GameObject.Find("FullWindowCanvas");
        if (!canvas)
            canvas = GameObject.FindWithTag("FullWindowCanvas");

        return canvas != null ? canvas.transform.Find("WindowsArea") : null;
    }

    private static Transform FindFullWindowCanvasTransform()
    {
        GameObject canvas = GameObject.Find("FullWindowCanvas");
        if (canvas)
            return canvas.transform;

        canvas = GameObject.FindWithTag("FullWindowCanvas");
        return canvas != null ? canvas.transform : null;
    }

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

    private void RestoreWindowVisibility(bool reloadPivotLayoutFromPrefs)
    {
        foreach (KeyValuePair<GameObject, bool> pair in _rememberedActive)
        {
            if (pair.Key)
                pair.Key.SetActive(pair.Value);
        }

        _rememberedActive.Clear();

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
                continue;

            if (reloadPivotLayoutFromPrefs)
            {
                binding.RestoreSavedLayout();
                continue;
            }

            SyncCornerResizeFromBindingLayout(binding);
        }
    }

    private static void SyncCornerResizeFromBindingLayout(UIWindowLayoutBinding binding)
    {
        if (binding?.WindowRect == null)
            return;

        UIWindowCornerResize resize = binding.WindowRect.GetComponent<UIWindowCornerResize>();
        if (resize != null)
            resize.ApplyLayoutScaleFromSnapshot(binding.WindowRect.localScale);
    }

    private void CommitAllGhostLayoutsAndSave()
    {
        for (int i = 0; i < _ghosts.Count; i++)
        {
            WindowPivotGhostUI ghost = _ghosts[i];
            if (ghost)
                ghost.ApplyToBindingAndSave();
        }

        PlayerPrefs.Save();
    }

    private void RestoreMainMenuSettingsIfNeeded()
    {
        if (!_restoreMainMenuSettingsOnExit)
            return;

        _restoreMainMenuSettingsOnExit = false;
        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        menu?.OpenSettings();
    }

    private void EnsureOverlay()
    {
        if (_overlayRoot)
        {
            _overlayRoot.gameObject.SetActive(true);
            _overlayRoot.SetAsLastSibling();
            EnsureOverlayButtons();
            return;
        }

        Transform canvasTransform = FindFullWindowCanvasTransform();
        if (!canvasTransform)
            return;

        GameObject overlayGo = new GameObject("MovePivotsOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        overlayGo.layer = canvasTransform.gameObject.layer;
        _overlayRoot = overlayGo.GetComponent<RectTransform>();
        _overlayRoot.SetParent(canvasTransform, false);
        _overlayRoot.anchorMin = Vector2.zero;
        _overlayRoot.anchorMax = Vector2.one;
        _overlayRoot.offsetMin = Vector2.zero;
        _overlayRoot.offsetMax = Vector2.zero;

        Canvas overlayCanvas = overlayGo.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = OverlayCanvasSortOrder;

        _overlayRoot.SetAsLastSibling();
        EnsureOverlayButtons();
    }

    private void EnsureOverlayButtons()
    {
        if (!_overlayRoot)
            return;

        if (!_overlayRoot.Find("MovePivotsDoneButton"))
            CreateDoneButton(_overlayRoot);

        Transform testViewTransform = _overlayRoot.Find("MovePivotsTestViewButton");
        if (!testViewTransform)
            CreateTestViewButton(_overlayRoot);
        else if (!_testViewButton)
        {
            _testViewButton = testViewTransform.GetComponent<Button>();
            _testViewButtonLabel = _testViewButton != null
                ? _testViewButton.GetComponentInChildren<TMP_Text>(true)
                : null;
        }

        if (!_overlayRoot.Find("MovePivotsAlignButton"))
            CreateAlignButton(_overlayRoot);
        else if (!_alignButton)
            _alignButton = _overlayRoot.Find("MovePivotsAlignButton")?.GetComponent<Button>();

        if (!_overlayRoot.Find("MovePivotsVisibilityButton"))
            CreateVisibilityButton(_overlayRoot);
        else if (!_visibilityButton)
            _visibilityButton = _overlayRoot.Find("MovePivotsVisibilityButton")?.GetComponent<Button>();

        if (!_overlayRoot.Find("MovePivotsResetButton"))
            CreateResetPivotsButton(_overlayRoot);
        else if (!_resetPivotsButton)
            _resetPivotsButton = _overlayRoot.Find("MovePivotsResetButton")?.GetComponent<Button>();

        if (!_overlayRoot.Find("MovePivotsCancelButton"))
            CreateCancelButton(_overlayRoot);
        else if (!_cancelButton)
            _cancelButton = _overlayRoot.Find("MovePivotsCancelButton")?.GetComponent<Button>();

        ApplyOverlayButtonChrome();
        LayoutOverlayButtons();
        UpdateTestViewButtonLabel();

        DestroyLegacyVisibilityMenuHost();

        if (_visibilityButton != null && _visibilityMenu == null)
            _visibilityMenu = PivotWindowVisibilityMenu.AttachToButton(_visibilityButton, HandlePivotVisibilityChanged);
        else
            _visibilityMenu?.SetToggleButton(_visibilityButton);
    }

    private void DestroyLegacyVisibilityMenuHost()
    {
        if (!_overlayRoot)
            return;

        Transform legacyHost = _overlayRoot.Find("MovePivotsVisibilityMenu");
        if (legacyHost)
            Destroy(legacyHost.gameObject);
    }

    private void ApplyOverlayButtonChrome()
    {
        if (!_overlayRoot)
            return;

        StyleOverlayButton(_overlayRoot.Find("MovePivotsDoneButton"), "Save layout", OverlayButtonSaveColor);
        StyleOverlayButton(_overlayRoot.Find("MovePivotsTestViewButton"), "Test View", OverlayButtonDefaultColor);
        StyleOverlayButton(_overlayRoot.Find("MovePivotsAlignButton"), "Align pivots with current positions", OverlayButtonDefaultColor, 15f);
        StyleOverlayButton(_overlayRoot.Find("MovePivotsVisibilityButton"), "Window visibility toggle", OverlayButtonDefaultColor, 15f);
        StyleOverlayButton(_overlayRoot.Find("MovePivotsResetButton"), "Reset to default pivots", OverlayButtonResetColor, 13f, new Color(0.05f, 0.05f, 0.05f, 1f));
        StyleOverlayButton(_overlayRoot.Find("MovePivotsCancelButton"), "Exit and don't save", OverlayButtonCancelColor, 15f);
    }

    private static void StyleOverlayButton(
        Transform buttonTransform,
        string labelText,
        Color backgroundColor,
        float fontSize = 18f,
        Color? labelColor = null)
    {
        if (!buttonTransform)
            return;

        if (buttonTransform.TryGetComponent(out Image image))
            image.color = backgroundColor;

        TMP_Text label = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        if (label)
        {
            label.text = labelText;
            label.fontSize = fontSize;
            if (labelColor.HasValue)
                label.color = labelColor.Value;
        }
    }

    private void LayoutOverlayButtons()
    {
        if (!_overlayRoot)
            return;

        const float y = -OverlayTopInset;
        float gap = OverlayButtonStackGap;
        float wSave = OverlaySaveButtonWidth;
        float wTest = OverlaySaveButtonWidth;
        float wAlign = OverlayWideButtonWidth;
        float wVisibility = OverlayVisibilityButtonWidth;
        float wReset = OverlayResetButtonWidth;
        float wCancel = OverlaySaveButtonWidth;
        float totalWidth = wSave + wTest + wAlign + wVisibility + wReset + wCancel + gap * 5f;
        float x = -totalWidth * 0.5f;

        PositionOverlayButtonHorizontal(_overlayRoot.Find("MovePivotsDoneButton") as RectTransform, ref x, wSave, y);
        x += gap;
        PositionOverlayButtonHorizontal(_overlayRoot.Find("MovePivotsTestViewButton") as RectTransform, ref x, wTest, y);
        x += gap;
        PositionOverlayButtonHorizontal(_overlayRoot.Find("MovePivotsAlignButton") as RectTransform, ref x, wAlign, y);
        x += gap;
        PositionOverlayButtonHorizontal(_overlayRoot.Find("MovePivotsVisibilityButton") as RectTransform, ref x, wVisibility, y);
        x += gap;
        PositionOverlayButtonHorizontal(_overlayRoot.Find("MovePivotsResetButton") as RectTransform, ref x, wReset, y);
        x += gap;
        PositionOverlayButtonHorizontal(_overlayRoot.Find("MovePivotsCancelButton") as RectTransform, ref x, wCancel, y);
        _visibilityMenu?.SetToggleButton(_visibilityButton);
    }

    private static void PositionOverlayButtonHorizontal(RectTransform buttonRect, ref float x, float width, float y)
    {
        if (!buttonRect)
            return;

        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = new Vector2(x, y);
        buttonRect.sizeDelta = new Vector2(width, OverlaySaveButtonHeight);
        x += width;
    }

    private void CreateDoneButton(RectTransform parent)
    {
        Button button = CreateOverlayButton(
            parent,
            "MovePivotsDoneButton",
            "Save layout",
            new Vector2(0f, -OverlayTopInset),
            OverlaySaveButtonWidth,
            OverlaySaveButtonHeight);
        button.onClick.AddListener(SaveLayoutAndExit);
    }

    private void CreateTestViewButton(RectTransform parent)
    {
        _testViewButton = CreateOverlayButton(
            parent,
            "MovePivotsTestViewButton",
            "Test View",
            new Vector2(0f, -OverlayTopInset),
            OverlaySaveButtonWidth,
            OverlaySaveButtonHeight);
        _testViewButtonLabel = _testViewButton.GetComponentInChildren<TMP_Text>(true);
        _testViewButton.onClick.AddListener(ToggleTestView);
    }

    private void CreateAlignButton(RectTransform parent)
    {
        _alignButton = CreateOverlayButton(
            parent,
            "MovePivotsAlignButton",
            "Align pivots with current positions",
            new Vector2(0f, -OverlayTopInset),
            OverlayWideButtonWidth,
            OverlaySaveButtonHeight,
            15f);
        _alignButton.onClick.AddListener(AlignGhostsWithCurrentWindowPositions);
    }

    private void CreateVisibilityButton(RectTransform parent)
    {
        _visibilityButton = CreateOverlayButton(
            parent,
            "MovePivotsVisibilityButton",
            "Window visibility toggle",
            new Vector2(0f, -OverlayTopInset),
            OverlayVisibilityButtonWidth,
            OverlaySaveButtonHeight,
            15f);
        _visibilityButton.onClick.AddListener(ToggleVisibilityMenu);

        DestroyLegacyVisibilityMenuHost();
        _visibilityMenu = PivotWindowVisibilityMenu.AttachToButton(_visibilityButton, HandlePivotVisibilityChanged);
    }

    private void CreateResetPivotsButton(RectTransform parent)
    {
        _resetPivotsButton = CreateOverlayButton(
            parent,
            "MovePivotsResetButton",
            "Reset to default pivots",
            new Vector2(0f, -OverlayTopInset),
            OverlayResetButtonWidth,
            OverlaySaveButtonHeight,
            13f);
        _resetPivotsButton.onClick.AddListener(ResetAllGhostsToFactoryDefaults);
    }

    private void CreateCancelButton(RectTransform parent)
    {
        _cancelButton = CreateOverlayButton(
            parent,
            "MovePivotsCancelButton",
            "Exit and don't save",
            new Vector2(0f, -OverlayTopInset),
            OverlaySaveButtonWidth,
            OverlaySaveButtonHeight,
            15f);
        _cancelButton.onClick.AddListener(ExitWithoutSaving);
    }

    private static Button CreateOverlayButton(
        RectTransform parent,
        string objectName,
        string labelText,
        Vector2 anchoredPosition,
        float width = 220f,
        float height = 40f,
        float fontSize = 18f)
    {
        GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.layer = parent.gameObject.layer;
        RectTransform rt = buttonGo.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(width, height);

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.22f, 0.2f, 0.16f, 0.92f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.layer = parent.gameObject.layer;
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textGo.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = new Color(0.95f, 0.9f, 0.82f, 1f);
        label.raycastTarget = false;

        return buttonGo.GetComponent<Button>();
    }

    private void ToggleTestView()
    {
        if (_testViewActive)
            ExitTestView(restoreWindowVisibility: true);
        else
            EnterTestView();
    }

    private void EnterTestView()
    {
        if (_testViewActive || _ghosts.Count == 0)
            return;

        _testViewActive = true;

        for (int i = 0; i < _ghosts.Count; i++)
        {
            WindowPivotGhostUI ghost = _ghosts[i];
            if (!ghost)
                continue;

            UIWindowLayoutBinding binding = ghost.Binding;
            if (binding != null && UIWindowLayoutBinding.UsesGhostDuringTestView(binding.MemoryKey))
                continue;

            ghost.gameObject.SetActive(false);
        }

        ApplyAllGhostLayoutsToBindings();
        ShowRealWindowsForTestView();
        ApplyAllGhostLayoutsToBindings();
        SetTestViewInteractionEnabled(false);
        UpdateTestViewButtonLabel();
    }

    private void ExitTestView(bool restoreWindowVisibility)
    {
        if (!_testViewActive)
            return;

        if (restoreWindowVisibility)
            HideTestViewWindows();

        for (int i = 0; i < _ghosts.Count; i++)
        {
            if (_ghosts[i])
                _ghosts[i].gameObject.SetActive(true);
        }

        _testViewActive = false;
        SetTestViewInteractionEnabled(true);
        RefreshAllGhostChrome();
        UpdateTestViewButtonLabel();
    }

    private void RefreshAllGhostChrome()
    {
        for (int i = 0; i < _ghosts.Count; i++)
        {
            if (_ghosts[i])
                _ghosts[i].RefreshPivotChrome();
        }

        RefreshBringToFrontOverlayButtons();
    }

    private void SetTestViewInteractionEnabled(bool enabled)
    {
        for (int i = 0; i < _ghosts.Count; i++)
        {
            if (_ghosts[i])
                _ghosts[i].SetInteractionEnabled(enabled);
        }

        for (int i = 0; i < _bringToFrontOverlayButtons.Count; i++)
        {
            PivotGhostBringToFrontOverlayButton overlayButton = _bringToFrontOverlayButtons[i];
            if (overlayButton)
                overlayButton.SetInteractionEnabled(enabled);
        }

        SyncBringToFrontOverlayVisibility();
        SyncLabelOverlayVisibility();
    }

    private void ApplyAllGhostLayoutsToBindings()
    {
        for (int i = 0; i < _ghosts.Count; i++)
        {
            WindowPivotGhostUI ghost = _ghosts[i];
            UIWindowLayoutBinding binding = ghost != null ? ghost.Binding : null;
            if (!binding)
                continue;

            binding.ApplySnapshot(ghost.GetCurrentSnapshot());
        }
    }

    private void ShowRealWindowsForTestView()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || !binding.WindowRect)
                continue;

            if (!IsPivotGhostVisible(binding.MemoryKey))
                continue;

            if (UIWindowLayoutBinding.UsesGhostDuringTestView(binding.MemoryKey))
                continue;

            if (UIWindowLayoutBinding.IsProcessingSkillsWindow(binding.MemoryKey))
            {
                ProcessingSkillsWindowLayout.ShowTestPreview();
                continue;
            }

            binding.WindowRect.gameObject.SetActive(true);
            binding.WindowRect.SetAsLastSibling();

            if (UIWindowLayoutBinding.IsActionBarWindow(binding.MemoryKey))
                ActionBarUI.SyncWindowOnPivotLayoutApplied(binding.WindowRect);
        }
    }

    private void HideTestViewWindows()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || !binding.WindowRect)
                continue;

            if (UIWindowLayoutBinding.UsesGhostDuringTestView(binding.MemoryKey))
                continue;

            if (UIWindowLayoutBinding.IsProcessingSkillsWindow(binding.MemoryKey))
            {
                ProcessingSkillsWindowLayout.HideTestPreview();
                continue;
            }

            binding.WindowRect.gameObject.SetActive(false);
        }
    }

    private void UpdateTestViewButtonLabel()
    {
        if (_testViewButtonLabel)
            _testViewButtonLabel.text = _testViewActive ? "Stop test view" : "Test View";
    }

    private void RebuildGhosts()
    {
        DestroyGhosts();

        if (!_windowsParent)
            return;

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
                continue;

            UIWindowLayoutPrefs.Snapshot snapshot = i < _ghostSnapshots.Count
                ? _ghostSnapshots[i]
                : binding.GetSavedPivotSnapshotForEditing();

            GameObject ghostGo = new GameObject(
                $"PivotGhost_{binding.MemoryKey}",
                typeof(RectTransform),
                typeof(WindowPivotGhostUI));
            ghostGo.layer = _windowsParent.gameObject.layer;
            ghostGo.SetActive(false);
            ghostGo.transform.SetParent(_windowsParent, false);
            ghostGo.transform.SetAsLastSibling();

            WindowPivotGhostUI ghost = ghostGo.GetComponent<WindowPivotGhostUI>();
            Color fillColor = UIWindowLayoutBinding.GetPivotGhostFillColor(binding.MemoryKey);
            Rect[] slotHints = null;
            if (UIWindowLayoutBinding.IsActionBarWindow(binding.MemoryKey))
                _actionBarSlotHintRects.TryGetValue(binding.MemoryKey, out slotHints);

            ghost.Initialize(binding, _windowsParent, snapshot, fillColor, slotHints);
            ApplyPivotGhostVisibility(binding.MemoryKey, ghost);
            ghostGo.SetActive(IsPivotGhostVisible(binding.MemoryKey));
            _ghosts.Add(ghost);
            EnsureBringToFrontOverlayButton(ghost);
            EnsureLabelOverlay(ghost);
        }

        SyncBringToFrontOverlayOrder();
        SyncBringToFrontOverlayVisibility();
    }

    private void EnsureLabelOverlay(WindowPivotGhostUI ghost)
    {
        if (!_overlayRoot || !ghost)
            return;

        GameObject labelGo = new GameObject(
            $"PivotLabel_{ghost.Binding.MemoryKey}",
            typeof(RectTransform),
            typeof(PivotGhostLabelOverlay));
        labelGo.layer = _overlayRoot.gameObject.layer;

        PivotGhostLabelOverlay labelOverlay = labelGo.GetComponent<PivotGhostLabelOverlay>();
        labelOverlay.Initialize(ghost, _overlayRoot);
        _labelOverlays.Add(labelOverlay);
    }

    private void EnsureBringToFrontOverlayButton(WindowPivotGhostUI ghost)
    {
        if (!_overlayRoot || !ghost)
            return;

        GameObject buttonGo = new GameObject(
            $"PivotBringToFront_{ghost.Binding.MemoryKey}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(PivotGhostBringToFrontOverlayButton));
        buttonGo.layer = _overlayRoot.gameObject.layer;

        PivotGhostBringToFrontOverlayButton overlayButton =
            buttonGo.GetComponent<PivotGhostBringToFrontOverlayButton>();
        overlayButton.Initialize(ghost, _overlayRoot, ghost.GetBringToFrontButtonColor());
        _bringToFrontOverlayButtons.Add(overlayButton);
    }

    private void RefreshBringToFrontOverlayButtons()
    {
        for (int i = 0; i < _bringToFrontOverlayButtons.Count; i++)
        {
            PivotGhostBringToFrontOverlayButton overlayButton = _bringToFrontOverlayButtons[i];
            WindowPivotGhostUI ghost = overlayButton != null ? overlayButton.Ghost : null;
            if (!overlayButton || !ghost)
                continue;

            overlayButton.ApplyColor(ghost.GetBringToFrontButtonColor());
        }

        for (int i = 0; i < _labelOverlays.Count; i++)
        {
            if (_labelOverlays[i])
                _labelOverlays[i].RefreshFromGhost();
        }

        SyncBringToFrontOverlayOrder();
        SyncBringToFrontOverlayVisibility();
        SyncLabelOverlayVisibility();
    }

    private void SyncBringToFrontOverlayOrder()
    {
        _bringToFrontOverlayButtons.Sort((a, b) =>
        {
            if (!a || !b || !a.Ghost || !b.Ghost)
                return 0;

            return b.Ghost.transform.GetSiblingIndex().CompareTo(a.Ghost.transform.GetSiblingIndex());
        });

        for (int i = 0; i < _bringToFrontOverlayButtons.Count; i++)
        {
            if (_bringToFrontOverlayButtons[i])
                _bringToFrontOverlayButtons[i].transform.SetAsLastSibling();
        }

        EnsureOverlayButtonsOnTop();
        SyncLabelOverlayOrder();
    }

    private void SyncLabelOverlayOrder()
    {
        _labelOverlays.Sort((a, b) =>
        {
            if (!a || !b || !a.Ghost || !b.Ghost)
                return 0;

            return b.Ghost.transform.GetSiblingIndex().CompareTo(a.Ghost.transform.GetSiblingIndex());
        });

        for (int i = 0; i < _labelOverlays.Count; i++)
        {
            if (_labelOverlays[i])
                _labelOverlays[i].transform.SetAsLastSibling();
        }

        EnsureOverlayButtonsOnTop();
    }

    private void SyncBringToFrontOverlayVisibility()
    {
        for (int i = 0; i < _bringToFrontOverlayButtons.Count; i++)
        {
            PivotGhostBringToFrontOverlayButton overlayButton = _bringToFrontOverlayButtons[i];
            if (!overlayButton)
                continue;

            WindowPivotGhostUI ghost = overlayButton.Ghost;
            bool show = !_testViewActive && ghost != null && ghost.isActiveAndEnabled;
            overlayButton.gameObject.SetActive(show);
        }
    }

    private void SyncLabelOverlayVisibility()
    {
        for (int i = 0; i < _labelOverlays.Count; i++)
        {
            PivotGhostLabelOverlay labelOverlay = _labelOverlays[i];
            if (!labelOverlay)
                continue;

            WindowPivotGhostUI ghost = labelOverlay.Ghost;
            bool show = !_testViewActive && ghost != null && ghost.isActiveAndEnabled;
            labelOverlay.gameObject.SetActive(show);
        }
    }

    private void EnsureOverlayButtonsOnTop()
    {
        if (!_overlayRoot)
            return;

        Transform done = _overlayRoot.Find("MovePivotsDoneButton");
        Transform testView = _overlayRoot.Find("MovePivotsTestViewButton");
        Transform align = _overlayRoot.Find("MovePivotsAlignButton");
        Transform visibility = _overlayRoot.Find("MovePivotsVisibilityButton");
        Transform reset = _overlayRoot.Find("MovePivotsResetButton");
        Transform cancel = _overlayRoot.Find("MovePivotsCancelButton");
        if (done)
            done.SetAsLastSibling();
        if (testView)
            testView.SetAsLastSibling();
        if (align)
            align.SetAsLastSibling();
        if (visibility)
            visibility.SetAsLastSibling();
        if (reset)
            reset.SetAsLastSibling();
        if (cancel)
            cancel.SetAsLastSibling();
    }

    private void DestroyBringToFrontOverlayButtons()
    {
        DestroyLabelOverlays();

        for (int i = _bringToFrontOverlayButtons.Count - 1; i >= 0; i--)
        {
            if (_bringToFrontOverlayButtons[i])
                Destroy(_bringToFrontOverlayButtons[i].gameObject);
        }

        _bringToFrontOverlayButtons.Clear();
    }

    private void DestroyLabelOverlays()
    {
        for (int i = _labelOverlays.Count - 1; i >= 0; i--)
        {
            if (_labelOverlays[i])
                Destroy(_labelOverlays[i].gameObject);
        }

        _labelOverlays.Clear();
    }

    private void DestroyGhosts()
    {
        DestroyBringToFrontOverlayButtons();

        for (int i = 0; i < _ghosts.Count; i++)
        {
            if (_ghosts[i])
                Destroy(_ghosts[i].gameObject);
        }

        _ghosts.Clear();
        _ghostSnapshots.Clear();
        _actionBarSlotHintRects.Clear();
    }

    private void InitializePivotVisibilityState()
    {
        _pivotGhostVisibleByKey.Clear();
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.MemoryKey))
                continue;

            _pivotGhostVisibleByKey[binding.MemoryKey] = true;
        }
    }

    private void RebuildVisibilityMenu()
    {
        if (_visibilityMenu == null && _overlayRoot)
            _visibilityMenu = PivotWindowVisibilityMenu.AttachToButton(_visibilityButton, HandlePivotVisibilityChanged);

        _visibilityMenu?.SetToggleButton(_visibilityButton);
        _visibilityMenu?.RebuildRows(_bindings, _pivotGhostVisibleByKey);
    }

    private void ToggleVisibilityMenu()
    {
        _visibilityMenu?.ToggleExpanded();
    }

    private void HandlePivotVisibilityChanged(string memoryKey, bool visible)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return;

        _pivotGhostVisibleByKey[memoryKey] = visible;
        SetPivotGhostVisible(memoryKey, visible);
    }

    private bool IsPivotGhostVisible(string memoryKey)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return true;

        return !_pivotGhostVisibleByKey.TryGetValue(memoryKey, out bool visible) || visible;
    }

    private void SetPivotGhostVisible(string memoryKey, bool visible)
    {
        WindowPivotGhostUI ghost = FindGhost(memoryKey);
        if (ghost)
            ghost.gameObject.SetActive(visible);

        _visibilityMenu?.SetRowVisible(memoryKey, visible);
        SyncBringToFrontOverlayVisibility();
        SyncLabelOverlayVisibility();

        if (_testViewActive)
        {
            UIWindowLayoutBinding binding = FindBinding(memoryKey);
            if (binding == null)
                return;

            if (!visible)
            {
                if (UIWindowLayoutBinding.IsProcessingSkillsWindow(memoryKey))
                {
                    ProcessingSkillsWindowLayout.HideTestPreview();
                }
                else if (!UIWindowLayoutBinding.UsesGhostDuringTestView(memoryKey) && binding.WindowRect)
                    binding.WindowRect.gameObject.SetActive(false);
            }
            else
            {
                ApplyAllGhostLayoutsToBindings();
                if (UIWindowLayoutBinding.IsProcessingSkillsWindow(memoryKey))
                    ProcessingSkillsWindowLayout.ShowTestPreview();
                else if (!UIWindowLayoutBinding.UsesGhostDuringTestView(memoryKey) && binding.WindowRect)
                {
                    binding.WindowRect.gameObject.SetActive(true);
                    binding.WindowRect.SetAsLastSibling();
                }
            }
        }
    }

    private void ApplyPivotGhostVisibility(string memoryKey, WindowPivotGhostUI ghost)
    {
        if (!ghost)
            return;

        bool visible = IsPivotGhostVisible(memoryKey);
        ghost.gameObject.SetActive(visible);
    }

    private UIWindowLayoutBinding FindBinding(string memoryKey)
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding != null && binding.MemoryKey == memoryKey)
                return binding;
        }

        return null;
    }

    private WindowPivotGhostUI FindGhost(string memoryKey)
    {
        for (int i = 0; i < _ghosts.Count; i++)
        {
            WindowPivotGhostUI ghost = _ghosts[i];
            if (ghost?.Binding != null && ghost.Binding.MemoryKey == memoryKey)
                return ghost;
        }

        return null;
    }

    private void ResetAllGhostsToFactoryDefaults()
    {
        if (_testViewActive)
            ExitTestView(restoreWindowVisibility: true);

        _visibilityMenu?.SetExpanded(false);

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
                continue;

            if (UIWindowLayoutBinding.IsProcessingSkillsWindow(binding.MemoryKey))
            {
                ProcessingSkillsWindowLayout.ResetProxyToFactoryDefault(binding.WindowRect);
                UIWindowLayoutPrefs.Delete(binding.MemoryKey);
                UIWindowPositionMemory.ForgetKey(binding.MemoryKey);
                UIWindowSessionLayoutMemory.ForgetKey(binding.MemoryKey);
                binding.RecaptureFactoryFromCurrentLayout();
            }
            else
            {
                binding.ResetToFactory();
            }

            UIWindowLayoutPrefs.Snapshot factorySnapshot = binding.GetCurrentSnapshot();
            if (i < _ghostSnapshots.Count)
                _ghostSnapshots[i] = factorySnapshot;

            WindowPivotGhostUI ghost = i < _ghosts.Count ? _ghosts[i] : null;
            if (ghost)
                ghost.ApplyLayoutSnapshot(factorySnapshot);
        }

        ProcessingSkillsWindowLayout.HideTestPreview();
        RefreshAllGhostChrome();
        GameLog.Add("All window pivots reset to default positions");
    }
}
