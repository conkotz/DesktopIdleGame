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
    private const float OverlaySaveButtonHeight = 48f;
    private const float OverlayButtonStackGap = 8f;
    private const float OverlayTopInset = 12f;

    private static MovePivotsModeController _instance;

    public static bool IsTestViewActive =>
        _instance != null && _instance._testViewActive;

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

    private readonly List<UIWindowLayoutBinding> _bindings = new(8);
    private readonly List<UIWindowLayoutPrefs.Snapshot> _ghostSnapshots = new(8);
    private readonly Dictionary<string, Rect[]> _actionBarSlotHintRects = new(1);
    private readonly List<WindowPivotGhostUI> _ghosts = new(8);
    private readonly List<PivotGhostBringToFrontOverlayButton> _bringToFrontOverlayButtons = new(8);
    private readonly Dictionary<GameObject, bool> _rememberedActive = new(16);

    private RectTransform _overlayRoot;
    private RectTransform _windowsParent;
    private bool _active;
    private bool _testViewActive;
    private bool _restoreMainMenuSettingsOnExit;
    private bool _userFinishedArranging;
    private Button _testViewButton;
    private TMP_Text _testViewButtonLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (IsBootstrapScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
        {
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
        UIWindowLayoutBinding.RestoreSessionLayoutsForSceneChange();

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

        if (_instance == this)
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
            _active = false;
            if (_userFinishedArranging)
            {
                GameLog.Add("Pivot positions set");
                _userFinishedArranging = false;
            }

            if (_testViewActive)
                ExitTestView(restoreWindowVisibility: false);
            MerchantClick.ForceCloseMerchantMode();
            CommitAllGhostLayoutsAndSave();
            DestroyGhosts();
            RestoreWindowVisibility();
            RestoreMainMenuSettingsIfNeeded();
            if (_overlayRoot)
                _overlayRoot.gameObject.SetActive(false);
            return;
        }

        _testViewActive = false;
        _restoreMainMenuSettingsOnExit = false;
        _rememberedActive.Clear();
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

        CaptureGhostSnapshots();
        CloseMenuMerchantAndStorage();
        HideAllBoundWindows();
        EnsureOverlay();
        RebuildGhosts();

        if (_ghosts.Count == 0)
        {
            _active = false;
            RestoreWindowVisibility();
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

    private void CaptureGhostSnapshots()
    {
        _ghostSnapshots.Clear();
        _actionBarSlotHintRects.Clear();

        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null)
                continue;

            if (UIWindowLayoutBinding.IsActionBarWindow(binding.MemoryKey) && binding.WindowRect)
            {
                _actionBarSlotHintRects[binding.MemoryKey] =
                    ActionBarUI.CapturePivotSlotRects(binding.WindowRect);
            }

            _ghostSnapshots.Add(binding.GetCurrentSnapshot());
        }
    }

    private void HideAllBoundWindows()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            UIWindowLayoutBinding binding = _bindings[i];
            if (binding == null || !binding.WindowRect)
                continue;

            GameObject go = binding.WindowRect.gameObject;
            if (!_rememberedActive.ContainsKey(go))
                _rememberedActive[go] = go.activeSelf;

            go.SetActive(false);
        }
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

    private void RestoreWindowVisibility()
    {
        foreach (KeyValuePair<GameObject, bool> pair in _rememberedActive)
        {
            if (pair.Key)
                pair.Key.SetActive(pair.Value);
        }

        _rememberedActive.Clear();

        for (int i = 0; i < _bindings.Count; i++)
            _bindings[i]?.RestoreSavedLayout();
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

        ApplySaveLayoutButtonChrome();
        LayoutOverlayButtons();
        UpdateTestViewButtonLabel();
    }

    private void ApplySaveLayoutButtonChrome()
    {
        if (!_overlayRoot)
            return;

        Transform doneTransform = _overlayRoot.Find("MovePivotsDoneButton");
        if (doneTransform is not RectTransform doneRt)
            return;

        doneRt.sizeDelta = new Vector2(OverlaySaveButtonWidth, OverlaySaveButtonHeight);

        TMP_Text label = doneRt.GetComponentInChildren<TMP_Text>(true);
        if (label)
            label.text = "Save layout";
    }

    private void LayoutOverlayButtons()
    {
        if (!_overlayRoot)
            return;

        float doneY = -OverlayTopInset;
        float testViewY = doneY - OverlaySaveButtonHeight - OverlayButtonStackGap;

        Transform doneTransform = _overlayRoot.Find("MovePivotsDoneButton");
        if (doneTransform is RectTransform doneRt)
            doneRt.anchoredPosition = new Vector2(0f, doneY);

        if (_testViewButton)
        {
            RectTransform testRt = _testViewButton.transform as RectTransform;
            if (testRt)
                testRt.anchoredPosition = new Vector2(0f, testViewY);
        }
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
        button.onClick.AddListener(() =>
        {
            _userFinishedArranging = true;
            ToggleSettingsStore.Set(ToggleSettingId.MoveWindowPivots, false);
        });
    }

    private void CreateTestViewButton(RectTransform parent)
    {
        float testViewY = -OverlayTopInset - OverlaySaveButtonHeight - OverlayButtonStackGap;
        _testViewButton = CreateOverlayButton(
            parent,
            "MovePivotsTestViewButton",
            "Test View",
            new Vector2(0f, testViewY));
        _testViewButtonLabel = _testViewButton.GetComponentInChildren<TMP_Text>(true);
        _testViewButton.onClick.AddListener(ToggleTestView);
    }

    private static Button CreateOverlayButton(
        RectTransform parent,
        string objectName,
        string labelText,
        Vector2 anchoredPosition,
        float width = 220f,
        float height = 40f)
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
        label.fontSize = 18f;
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

        ApplyAllGhostLayoutsToBindings();

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

        _testViewActive = true;
        ShowRealWindowsForTestView();
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

            if (UIWindowLayoutBinding.UsesGhostDuringTestView(binding.MemoryKey))
                continue;

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
                : binding.GetCurrentSnapshot();

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
            ghostGo.SetActive(true);
            _ghosts.Add(ghost);
            EnsureBringToFrontOverlayButton(ghost);
        }

        SyncBringToFrontOverlayOrder();
        SyncBringToFrontOverlayVisibility();
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

        SyncBringToFrontOverlayOrder();
        SyncBringToFrontOverlayVisibility();
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

    private void EnsureOverlayButtonsOnTop()
    {
        if (!_overlayRoot)
            return;

        Transform done = _overlayRoot.Find("MovePivotsDoneButton");
        Transform testView = _overlayRoot.Find("MovePivotsTestViewButton");
        if (done)
            done.SetAsLastSibling();
        if (testView)
            testView.SetAsLastSibling();
    }

    private void DestroyBringToFrontOverlayButtons()
    {
        for (int i = _bringToFrontOverlayButtons.Count - 1; i >= 0; i--)
        {
            if (_bringToFrontOverlayButtons[i])
                Destroy(_bringToFrontOverlayButtons[i].gameObject);
        }

        _bringToFrontOverlayButtons.Clear();
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
}
