using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kirurobo;

/// <summary>
/// Windows player: OS click-through when the pointer is not on the gameplay strip and not on interactive UI.
/// <list type="bullet">
/// <item>Strip region: always captures the mouse (world + strip HUD).</item>
/// <item>Outside strip (normal): UI raycasts block click-through; empty areas pass through to the desktop.</item>
/// <item>Expand background: UI and the sky band above the strip capture clicks; black margins pass through to the desktop/taskbar.</item>
/// <item>Scenes without StripCamera use the same UI rules as “outside strip”: only real UI blocks click-through.</item>
/// </list>
/// For "empty" areas outside the strip, do not leave full-screen Images with Raycast Target on; use Raycast Target off or CanvasGroup blocksRaycasts off on purely visual fillers.
/// When click-through is active, optional re-apply of UniWin topmost keeps the game above other windows after desktop clicks (see Topmost section).
/// </summary>
public class DesktopOverlayClickThrough : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UniWindowController uniWin;
    [SerializeField] private Camera stripCamera;
    [SerializeField] private string stripCameraName = "StripCamera";

    [Header("Hit test")]
    [Tooltip("Disables UniWindowController automatic hit test so this script is the only driver for SetClickThrough.")]
    [SerializeField] private bool disableUniWinAutoHitTest = true;

    [Tooltip("Max seconds between UI raycast refreshes when the pointer is still (0 = only on mouse move or button change).")]
    [SerializeField, Min(0f)] private float uiRaycastRefreshInterval = 0.033f;

    [Header("Advanced")]
    [Tooltip("Rare: if enabled, only listed canvases capture the mouse outside the gameplay strip (or when there is no StripCamera, e.g. Bootstrap). When off (default), any UI raycast blocks click-through.")]
    [SerializeField] private bool allowlistOnlyOutsideStrip;

    [Tooltip("Used only when Allowlist Only Outside Strip is on.")]
    [SerializeField] private Canvas[] outsideStripBlockingCanvases;

    [Header("Topmost (Windows player)")]
    [Tooltip("While the cursor is in a click-through region, re-apply UniWin topmost each frame so other apps do not cover the game—only when Settings › Is topmost game window is on.")]
    [SerializeField] private bool reapplyTopmostWhileClickThrough = true;

    [Header("Debug")]
    [Tooltip("Log click-through decisions and UI raycast stack. In the Editor, logs only (no OS click-through). Windows player: turn on for builds or use Development Build + scripting backend to read Player.log.")]
    [SerializeField] private bool debugLogging;

    [Tooltip("If off, logs on left mouse down and when interactive/click-through state changes. If on, logs every frame (very noisy).")]
    [SerializeField] private bool debugEveryFrame;

    [SerializeField, Range(1, 32)]
    private int debugMaxRaycastEntries = 12;

    // Latches interaction while dragging UI so it doesn't "drop" mid-drag.
    private bool _dragLatch;

    private bool _debugPrevInteractive;
    private bool _debugHadSample;

    private StripCameraController _stripController;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(32);
    private PointerEventData _pointerData;

    private Vector2 _lastSampleMousePosition = new Vector2(float.NaN, float.NaN);
    private float _nextUiRaycastRefreshTime;
    private bool _cachedUiRaycastHit;
    private bool _cachedAllowlistHit;

    private Rect _cachedStripScreenRect;
    private int _cachedScreenWidth = -1;
    private int _cachedScreenHeight = -1;

    private void Awake()
    {
        if (!uniWin)
            uniWin = FindFirstObjectByType<UniWindowController>(FindObjectsInactive.Include);
        RebindStripCamera();

        if (disableUniWinAutoHitTest && uniWin)
            uniWin.isHitTestEnabled = false;
    }

    private void Start()
    {
        ApplyWindowTopmostFromSettings();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ToggleSettingsStore.Changed += OnToggleSettingsStoreChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ToggleSettingsStore.Changed -= OnToggleSettingsStoreChanged;
    }

    private void OnToggleSettingsStoreChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.TopMostGameWindow)
            ApplyWindowTopmostFromSettings();
    }

    /// <summary>Syncs UniWin topmost to the Misc settings toggle (PlayerPrefs).</summary>
    private void ApplyWindowTopmostFromSettings()
    {
        if (!uniWin)
            uniWin = FindFirstObjectByType<UniWindowController>(FindObjectsInactive.Include);
        if (!uniWin)
            return;

        uniWin.isTopmost = ToggleSettingsStore.Get(ToggleSettingId.TopMostGameWindow);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        stripCamera = null;
        _stripController = null;
        InvalidateHitTestCache();
        RebindStripCamera();
        ApplyWindowTopmostFromSettings();

        if (IsBootstrapMenuScene(scene))
            ForceMenuInteractive();
    }

    private void RebindStripCamera()
    {
        if (stripCamera)
        {
            CacheStripControllerFromCamera();
            return;
        }

        stripCamera = FindStripCameraByName(stripCameraName);
        CacheStripControllerFromCamera();
    }

    private void CacheStripControllerFromCamera()
    {
        _stripController = stripCamera ? stripCamera.GetComponent<StripCameraController>() : null;
    }

    private static Camera FindStripCameraByName(string cameraName)
    {
        if (string.IsNullOrEmpty(cameraName))
            return null;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam && string.Equals(cam.gameObject.name, cameraName, System.StringComparison.Ordinal))
                return cam;
        }

        return null;
    }

    private void LateUpdate()
    {
#if !(UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        // Inspector field only applies to Windows player; keep referenced so Editor doesn't warn CS0414.
        _ = reapplyTopmostWhileClickThrough;
        if (!debugLogging)
            return;
#else
        if (!uniWin)
            uniWin = FindFirstObjectByType<UniWindowController>(FindObjectsInactive.Include);
        if (!uniWin)
            return;
#endif

        if (!stripCamera)
            RebindStripCamera();

        RefreshHitTestCachesIfNeeded();

        bool pointerInteractive = IsPointerInteractiveFromCache();

        if (Input.GetMouseButtonDown(0))
            _dragLatch = pointerInteractive;

        if (Input.GetMouseButtonUp(0))
            _dragLatch = false;

        bool interactive = pointerInteractive || _dragLatch;
        bool clickThrough = !interactive;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (IsBootstrapMenuSceneActive())
            ForceMenuInteractive();
        else
        {
            uniWin.SetClickThrough(clickThrough);
            if (reapplyTopmostWhileClickThrough && clickThrough && ToggleSettingsStore.Get(ToggleSettingId.TopMostGameWindow))
                uniWin.isTopmost = true;
        }
#endif

        if (!debugLogging)
            return;

        LogDebugState(interactive, clickThrough, pointerInteractive);
    }

    private void InvalidateHitTestCache()
    {
        _lastSampleMousePosition = new Vector2(float.NaN, float.NaN);
        _nextUiRaycastRefreshTime = 0f;
        _cachedScreenWidth = -1;
        _cachedScreenHeight = -1;
    }

    private bool ShouldRefreshHitTestCaches()
    {
        Vector2 mouse = Input.mousePosition;
        if (mouse != _lastSampleMousePosition)
            return true;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0) ||
            Input.GetMouseButtonDown(1) || Input.GetMouseButtonUp(1) ||
            Input.GetMouseButtonDown(2) || Input.GetMouseButtonUp(2))
            return true;

        if (uiRaycastRefreshInterval > 0f && Time.unscaledTime >= _nextUiRaycastRefreshTime)
            return true;

        return Screen.width != _cachedScreenWidth || Screen.height != _cachedScreenHeight;
    }

    private void RefreshHitTestCachesIfNeeded()
    {
        if (!ShouldRefreshHitTestCaches())
            return;

        _lastSampleMousePosition = Input.mousePosition;
        if (uiRaycastRefreshInterval > 0f)
            _nextUiRaycastRefreshTime = Time.unscaledTime + uiRaycastRefreshInterval;

        RefreshStripScreenRectCache();
        RefreshUiRaycastCache();
    }

    private void RefreshStripScreenRectCache()
    {
        _cachedScreenWidth = Screen.width;
        _cachedScreenHeight = Screen.height;

        if (!stripCamera)
        {
            _cachedStripScreenRect = default;
            return;
        }

        if (_stripController)
        {
            float w = _stripController.WidthNormalized * Screen.width;
            float h = _stripController.StripHeightPercent * Screen.height;
            float x = _stripController.LeftNormalized * Screen.width;
            float y = _stripController.BottomNormalized * Screen.height;
            _cachedStripScreenRect = new Rect(x, y, w, h);
            return;
        }

        _cachedStripScreenRect = stripCamera.pixelRect;
    }

    private void RefreshUiRaycastCache()
    {
        if (!TryFillUiRaycastResults())
        {
            _cachedUiRaycastHit = false;
            _cachedAllowlistHit = false;
            return;
        }

        _cachedUiRaycastHit = _raycastResults.Count > 0;
        _cachedAllowlistHit = ComputeAllowlistHitFromResults(_raycastResults);
    }

    private bool IsPointerInsideExpandedSkyBandCached()
    {
        if (!stripCamera || _cachedStripScreenRect.width <= 0f || _cachedStripScreenRect.height <= 0f)
            return false;

        Vector2 mouse = Input.mousePosition;
        if (mouse.y <= _cachedStripScreenRect.yMax)
            return false;

        return mouse.x >= _cachedStripScreenRect.xMin && mouse.x <= _cachedStripScreenRect.xMax;
    }

    private bool IsPointerInsideStripCached()
    {
        if (!stripCamera)
            return false;

        return _cachedStripScreenRect.Contains(Input.mousePosition);
    }

    private bool IsPointerInteractiveFromCache()
    {
        // Gameplay strip: always capture (world + HUD in strip rect).
        if (stripCamera && IsPointerInsideStripCached())
            return true;

        // Expand background: sky above the strip is gameplay; UI always wins; black margins stay click-through.
        if (ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
        {
            if (_cachedUiRaycastHit)
                return true;

            if (IsPointerInsideExpandedSkyBandCached())
                return true;

            return false;
        }

        // Outside strip (normal): only interactive UI blocks desktop click-through.
        if (!allowlistOnlyOutsideStrip)
            return _cachedUiRaycastHit;

        return _cachedAllowlistHit;
    }

    private bool ComputeAllowlistHitFromResults(List<RaycastResult> results)
    {
        if (outsideStripBlockingCanvases == null || outsideStripBlockingCanvases.Length == 0)
            return false;

        if (results == null || results.Count == 0)
            return false;

        for (int i = 0; i < results.Count; i++)
        {
            RaycastResult r = results[i];
            for (int c = 0; c < outsideStripBlockingCanvases.Length; c++)
            {
                Canvas canvas = outsideStripBlockingCanvases[c];
                if (!canvas)
                    continue;

                Transform hitT = r.gameObject.transform;
                if (hitT == canvas.transform || hitT.IsChildOf(canvas.transform))
                    return true;
            }
        }

        return false;
    }

    private bool TryFillUiRaycastResults()
    {
        _raycastResults.Clear();

        if (EventSystem.current == null)
            return false;

        if (_pointerData == null)
            _pointerData = new PointerEventData(EventSystem.current);
        else
            _pointerData.Reset();

        _pointerData.position = Input.mousePosition;
        EventSystem.current.RaycastAll(_pointerData, _raycastResults);
        return true;
    }

    private void LogDebugState(bool interactive, bool clickThrough, bool pointerInteractive)
    {
        bool clickDown = Input.GetMouseButtonDown(0);
        bool stateChanged = !_debugHadSample || interactive != _debugPrevInteractive;
        _debugHadSample = true;
        _debugPrevInteractive = interactive;

        if (!debugEveryFrame && !(clickDown || stateChanged))
            return;

        bool inStrip = stripCamera && IsPointerInsideStripCached();
        bool noStripMode = !stripCamera;

        var sb = new StringBuilder(512);
        sb.Append("[DesktopOverlayClickThrough] ");
#if UNITY_EDITOR
        sb.Append("(Editor log only; OS click-through not applied) ");
#endif
        sb.Append("mouse=").Append(Input.mousePosition);
        sb.Append(" noStripScene=").Append(noStripMode);
        sb.Append(" inStrip=").Append(inStrip);
        sb.Append(" allowlistOnly=").Append(allowlistOnlyOutsideStrip);
        if (allowlistOnlyOutsideStrip)
            sb.Append(" overAllowlisted=").Append(_cachedAllowlistHit);
        sb.Append(" expandBg=").Append(ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground));
        if (ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            sb.Append(" inExpandedSkyBand=").Append(IsPointerInsideExpandedSkyBandCached());
        sb.Append(" uiRaycastHit=").Append(_cachedUiRaycastHit);
        sb.Append(" pointerInteractive=").Append(pointerInteractive);
        sb.Append(" dragLatch=").Append(_dragLatch);
        sb.Append(" interactive=").Append(interactive);
        sb.Append(" clickThrough=").Append(clickThrough);
        sb.Append(" hits=").Append(_raycastResults.Count);
        Debug.Log(sb.ToString());

        int n = Mathf.Min(_raycastResults.Count, debugMaxRaycastEntries);
        for (int i = 0; i < n; i++)
        {
            RaycastResult r = _raycastResults[i];
            Canvas c = r.gameObject.GetComponentInParent<Canvas>();
            Graphic g = r.gameObject.GetComponent<Graphic>();
            string graphicType = g ? g.GetType().Name : "(no Graphic)";
            bool raycast = g && g.raycastTarget;
            Debug.Log(
                $"  [{i}] depth={r.depth} dist={r.distance:F3} '{r.gameObject.name}' " +
                $"graphic={graphicType} raycastTarget={raycast} canvas='{(c ? c.gameObject.name : "?")}' " +
                $"path={GetTransformPath(r.gameObject.transform)}",
                r.gameObject);
        }

        if (_raycastResults.Count > n)
            Debug.Log($"  ... {_raycastResults.Count - n} more (raise Debug Max Raycast Entries)");
    }

    private static bool IsBootstrapMenuScene(Scene scene) =>
        scene.IsValid() && scene.isLoaded &&
        scene.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase);

    private static bool IsBootstrapMenuSceneActive() => IsBootstrapMenuScene(SceneManager.GetActiveScene());

    private void ForceMenuInteractive()
    {
        if (!uniWin)
            uniWin = FindFirstObjectByType<UniWindowController>(FindObjectsInactive.Include);
        if (!uniWin)
            return;

        uniWin.SetClickThrough(false);
        _dragLatch = false;
    }

    private static string GetTransformPath(Transform t, int maxDepth = 8)
    {
        if (!t)
            return "";

        var sb = new StringBuilder(128);
        int d = 0;
        while (t != null && d < maxDepth)
        {
            if (sb.Length > 0)
                sb.Insert(0, '/');
            sb.Insert(0, t.name);
            t = t.parent;
            d++;
        }

        if (t != null)
            sb.Insert(0, ".../");

        return sb.ToString();
    }
}
