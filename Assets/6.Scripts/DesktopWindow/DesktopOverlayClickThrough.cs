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
/// <item>Outside strip: EventSystem raycasts — any graphic with Raycast Target captures the mouse (menus, message bars, etc.).</item>
/// <item>No raycast hit (outside strip, or scenes with no StripCamera e.g. Bootstrap): click-through to the desktop.</item>
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
        RebindStripCamera();
        ApplyWindowTopmostFromSettings();
    }

    private void RebindStripCamera()
    {
        if (stripCamera)
            return;

        var go = GameObject.Find(stripCameraName);
        if (go)
            stripCamera = go.GetComponent<Camera>();
    }

    private void LateUpdate()
    {
#if !(UNITY_STANDALONE_WIN && !UNITY_EDITOR)
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

        bool inStrip = stripCamera && IsPointerInsideStrip();
        bool noStripMode = !stripCamera;
        bool overUi = IsPointerOverUIRaycast();
        bool overAllowlist = allowlistOnlyOutsideStrip && IsPointerOverAllowlistedCanvasOnly();

        bool pointerInteractive = IsPointerInteractiveThisFrame();

        if (Input.GetMouseButtonDown(0))
            _dragLatch = pointerInteractive;

        if (Input.GetMouseButtonUp(0))
            _dragLatch = false;

        bool interactive = pointerInteractive || _dragLatch;
        bool clickThrough = !interactive;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uniWin.SetClickThrough(clickThrough);
        if (reapplyTopmostWhileClickThrough && clickThrough && ToggleSettingsStore.Get(ToggleSettingId.TopMostGameWindow))
            uniWin.isTopmost = true;
#endif

        if (!debugLogging)
            return;

        bool clickDown = Input.GetMouseButtonDown(0);
        bool stateChanged = !_debugHadSample || interactive != _debugPrevInteractive;
        _debugHadSample = true;
        _debugPrevInteractive = interactive;

        if (!debugEveryFrame && !(clickDown || stateChanged))
            return;

        TryGetUiRaycastResults(out List<RaycastResult> raycastResults);
        if (raycastResults == null)
            raycastResults = new List<RaycastResult>();

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
            sb.Append(" overAllowlisted=").Append(overAllowlist);
        sb.Append(" uiRaycastHit=").Append(overUi);
        sb.Append(" pointerInteractive=").Append(pointerInteractive);
        sb.Append(" dragLatch=").Append(_dragLatch);
        sb.Append(" interactive=").Append(interactive);
        sb.Append(" clickThrough=").Append(clickThrough);
        sb.Append(" hits=").Append(raycastResults.Count);
        Debug.Log(sb.ToString());

        int n = Mathf.Min(raycastResults.Count, debugMaxRaycastEntries);
        for (int i = 0; i < n; i++)
        {
            RaycastResult r = raycastResults[i];
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

        if (raycastResults.Count > n)
            Debug.Log($"  ... {raycastResults.Count - n} more (raise Debug Max Raycast Entries)");
    }

    private bool IsPointerInteractiveThisFrame()
    {
        // Gameplay strip: always capture (world + HUD in strip rect).
        if (stripCamera && IsPointerInsideStrip())
            return true;

        // No strip (Bootstrap / menus) or outside strip: UI raycasts only.
        if (!allowlistOnlyOutsideStrip)
            return IsPointerOverUIRaycast();

        return IsPointerOverAllowlistedCanvasOnly();
    }

    private bool IsPointerOverAllowlistedCanvasOnly()
    {
        if (outsideStripBlockingCanvases == null || outsideStripBlockingCanvases.Length == 0)
            return false;

        if (!TryGetUiRaycastResults(out List<RaycastResult> results) || results.Count == 0)
            return false;

        foreach (var r in results)
        {
            foreach (var canvas in outsideStripBlockingCanvases)
            {
                if (!canvas)
                    continue;
                Transform hitT = r.gameObject.transform;
                if (hitT == canvas.transform || hitT.IsChildOf(canvas.transform))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Uses <see cref="StripCameraController"/> normalized rect when present, otherwise <see cref="Camera.pixelRect"/>.
    /// </summary>
    private bool IsPointerInsideStrip()
    {
        if (!stripCamera)
            return false;

        var ctrl = stripCamera.GetComponent<StripCameraController>();
        if (ctrl)
        {
            float w = ctrl.WidthNormalized * Screen.width;
            float h = ctrl.StripHeightPercent * Screen.height;
            float x = ctrl.LeftNormalized * Screen.width;
            float y = ctrl.BottomNormalized * Screen.height;
            return new Rect(x, y, w, h).Contains(Input.mousePosition);
        }

        return stripCamera.pixelRect.Contains(Input.mousePosition);
    }

    private static bool TryGetUiRaycastResults(out List<RaycastResult> results)
    {
        results = null;
        if (EventSystem.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return true;
    }

    private static bool IsPointerOverUIRaycast()
    {
        return TryGetUiRaycastResults(out var results) && results.Count > 0;
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
