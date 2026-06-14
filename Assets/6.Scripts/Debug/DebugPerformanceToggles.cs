using System.Text;
using UnityEngine;

/// <summary>
/// Developer-only runtime toggles for isolating performance hotspots.
/// Attach to a persistent manager (e.g. WorldManager). F6–F12 toggles major UI/world systems.
/// </summary>
[DisallowMultipleComponent]
public sealed class DebugPerformanceToggles : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Optional root for overhead UI. When unset, every UnitOverheadUI instance is toggled instead.")]
    [SerializeField] private GameObject overheadUi;

    [Tooltip("Floating damage/status text canvas. When unset, searches for DamageFloatingFxCanvas (scene root or child).")]
    [SerializeField] private GameObject floatingCombatText;

    [SerializeField] private GameObject actionBarUi;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject activityLog;
    [SerializeField] private GameObject backgroundParallax;
    [SerializeField] private GameObject particlesParent;

    [Header("Options")]
    [SerializeField] private bool useUnitOverheadUiFallback = true;
    [SerializeField] private bool resolveFloatingCombatCanvasFromChild = true;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private int overlayFontSize = 13;
    [Tooltip("When false, this utility disables itself in non-editor release builds.")]
    [SerializeField] private bool activeInReleaseBuilds;

    private float _fps;
    private float _fpsTimer;
    private int _frameCount;
    private GUIStyle _overlayStyle;
    private readonly StringBuilder _overlayBuilder = new(320);

    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (!activeInReleaseBuilds)
        {
            enabled = false;
            showOverlay = false;
        }
#endif
    }

    private void Start()
    {
        TryAutoResolveMissingReferences(logWarnings: true);
    }

    private void Update()
    {
        UpdateFps();
        HandleHotkeys();
    }

    private void OnGUI()
    {
        if (!showOverlay || !enabled)
            return;

        EnsureOverlayStyle();
        _overlayBuilder.Clear();
        _overlayBuilder.AppendLine($"FPS: {_fps:0}");
        _overlayBuilder.AppendLine($"F6  Overhead UI: {ResolveOverheadUiState()}");
        _overlayBuilder.AppendLine($"F7  Floating Combat Text: {ResolveFloatingCombatTextState()}");
        _overlayBuilder.AppendLine($"F8  Action Bar: {FormatState(actionBarUi)}");
        _overlayBuilder.AppendLine($"F9  Stats Panel: {FormatState(statsPanel)}");
        _overlayBuilder.AppendLine($"F10 Activity Log: {FormatState(activityLog)}");
        _overlayBuilder.AppendLine($"F11 Background / Parallax: {FormatState(backgroundParallax)}");
        _overlayBuilder.AppendLine($"F12 Particles: {FormatState(particlesParent)}");

        GUI.Label(new Rect(8f, 8f, 420f, 180f), _overlayBuilder.ToString(), _overlayStyle);
    }

    private void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.F6))
            ToggleOverheadUi();
        else if (Input.GetKeyDown(KeyCode.F7))
            ToggleFloatingCombatText();
        else if (Input.GetKeyDown(KeyCode.F8))
            ToggleTarget(actionBarUi, "Action Bar");
        else if (Input.GetKeyDown(KeyCode.F9))
            ToggleTarget(statsPanel, "Stats Panel");
        else if (Input.GetKeyDown(KeyCode.F10))
            ToggleTarget(activityLog, "Activity Log");
        else if (Input.GetKeyDown(KeyCode.F11))
            ToggleTarget(backgroundParallax, "Background / Parallax");
        else if (Input.GetKeyDown(KeyCode.F12))
            ToggleTarget(particlesParent, "Particles");
    }

    private void TryAutoResolveMissingReferences(bool logWarnings)
    {
        if (floatingCombatText == null && resolveFloatingCombatCanvasFromChild)
            floatingCombatText = ResolveFloatingCombatCanvas();

        if (backgroundParallax == null)
            backgroundParallax = FindSceneRoot("BackgroundVisuals");

        if (particlesParent == null)
        {
            GameObject worldVisuals = FindSceneRoot("WorldVisuals");
            if (worldVisuals != null)
            {
                Transform floorVisuals = worldVisuals.transform.Find("FloorVisuals");
                if (floorVisuals != null)
                    particlesParent = floorVisuals.gameObject;
            }
        }

        if (logWarnings)
        {
            WarnIfMissing(overheadUi, "Overhead UI", useUnitOverheadUiFallback);
            WarnIfMissing(floatingCombatText, "Floating Combat Text", resolveFloatingCombatCanvasFromChild);
            WarnIfMissing(actionBarUi, "Action Bar", false);
            WarnIfMissing(statsPanel, "Stats Panel", false);
            WarnIfMissing(activityLog, "Activity Log", false);
            WarnIfMissing(backgroundParallax, "Background / Parallax", false);
            WarnIfMissing(particlesParent, "Particles", false);
        }
    }

    private static void WarnIfMissing(GameObject target, string label, bool hasFallback)
    {
        if (target != null || hasFallback)
            return;

        Debug.LogWarning($"[DebugPerformanceToggles] {label} reference is missing and no fallback is enabled.");
    }

    private void ToggleOverheadUi()
    {
        if (overheadUi != null)
        {
            ToggleTarget(overheadUi, "Overhead UI");
            return;
        }

        if (!useUnitOverheadUiFallback)
        {
            Debug.LogWarning("[DebugPerformanceToggles] Overhead UI reference is missing.");
            return;
        }

        UnitOverheadUI[] instances = FindObjectsByType<UnitOverheadUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (instances == null || instances.Length == 0)
        {
            Debug.LogWarning("[DebugPerformanceToggles] No UnitOverheadUI instances found to toggle.");
            return;
        }

        bool enable = !AnyUnitOverheadActive(instances);
        for (int i = 0; i < instances.Length; i++)
        {
            UnitOverheadUI instance = instances[i];
            if (instance == null)
                continue;

            instance.gameObject.SetActive(enable);
        }

        Debug.Log(enable ? "Overhead UI Enabled" : "Overhead UI Disabled");
    }

    private void ToggleFloatingCombatText()
    {
        GameObject target = ResolveFloatingCombatTextTarget();
        if (target == null)
        {
            Debug.LogWarning("[DebugPerformanceToggles] Floating Combat Text reference is missing.");
            return;
        }

        bool enable = !target.activeSelf;
        target.SetActive(enable);

        DamagePopupSystem popupSystem = DamagePopupSystem.Instance;
        if (popupSystem != null)
            popupSystem.enabled = enable;

        Debug.Log(enable ? "Floating Combat Text Enabled" : "Floating Combat Text Disabled");
    }

    private static void ToggleTarget(GameObject target, string label)
    {
        if (target == null)
        {
            Debug.LogWarning($"[DebugPerformanceToggles] {label} reference is missing.");
            return;
        }

        bool enable = !target.activeSelf;
        target.SetActive(enable);
        Debug.Log(enable ? $"{label} Enabled" : $"{label} Disabled");
    }

    private GameObject ResolveFloatingCombatTextTarget()
    {
        if (floatingCombatText != null)
            return floatingCombatText;

        if (!resolveFloatingCombatCanvasFromChild)
            return null;

        floatingCombatText = ResolveFloatingCombatCanvas();
        return floatingCombatText;
    }

    /// <summary>
    /// DamagePopupSystem creates DamageFloatingFxCanvas as a scene-root object (unparented), not under WorldManager.
    /// </summary>
    private GameObject ResolveFloatingCombatCanvas()
    {
        GameObject fromChild = FindChildGameObject(transform, "DamageFloatingFxCanvas");
        if (fromChild != null)
            return fromChild;

        return FindSceneRoot("DamageFloatingFxCanvas");
    }

    private string ResolveOverheadUiState()
    {
        if (overheadUi != null)
            return FormatState(overheadUi);

        if (!useUnitOverheadUiFallback)
            return "N/A";

        UnitOverheadUI[] instances = FindObjectsByType<UnitOverheadUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        return AnyUnitOverheadActive(instances) ? "ON" : "OFF";
    }

    private string ResolveFloatingCombatTextState()
    {
        GameObject target = ResolveFloatingCombatTextTarget();
        if (target != null)
            return FormatState(target);

        DamagePopupSystem popupSystem = DamagePopupSystem.Instance;
        if (popupSystem != null)
            return popupSystem.enabled ? "ON" : "OFF";

        return "N/A";
    }

    private static bool AnyUnitOverheadActive(UnitOverheadUI[] instances)
    {
        if (instances == null)
            return false;

        for (int i = 0; i < instances.Length; i++)
        {
            UnitOverheadUI instance = instances[i];
            if (instance != null && instance.gameObject.activeSelf)
                return true;
        }

        return false;
    }

    private static string FormatState(GameObject target) =>
        target == null ? "N/A" : target.activeSelf ? "ON" : "OFF";

    private void UpdateFps()
    {
        _frameCount++;
        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer < 0.5f)
            return;

        _fps = _frameCount / _fpsTimer;
        _frameCount = 0;
        _fpsTimer = 0f;
    }

    private void EnsureOverlayStyle()
    {
        if (_overlayStyle != null)
            return;

        _overlayStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = overlayFontSize,
            richText = false,
            alignment = TextAnchor.UpperLeft
        };
        _overlayStyle.normal.textColor = Color.white;
    }

    private static GameObject FindChildGameObject(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform child = root.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static GameObject FindSceneRoot(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name == objectName)
                return root;
        }

        return null;
    }
}
