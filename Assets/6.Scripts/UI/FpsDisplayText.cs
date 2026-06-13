using TMPro;
using UnityEngine;

/// <summary>
/// Updates a TMP label with the current in-game frame rate when <see cref="ToggleSettingId.ShowFps"/> is on.
/// Uses a nested overlay canvas on the FPS panel only — never mutates the parent FullWindowCanvas sort order.
/// </summary>
[DisallowMultipleComponent]
public sealed class FpsDisplayText : MonoBehaviour
{
    /// <summary>Above windows (~10000) and tooltips (~10200); below level-load fader (32767 / short.MaxValue).</summary>
    private const int OverlaySortOrder = 32000;

    [SerializeField] private TMP_Text label;
    [Tooltip("Hidden when Show FPS is off. Defaults to parent when named FPSTextPanel, otherwise this object.")]
    [SerializeField] private GameObject visibilityRoot;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField] private string format = "FPS: {0}";

    private float _accumUnscaledTime;
    private int _frameCount;
    private bool _overlayConfigured;

    private void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();

        if (!label)
            label = GetComponentInChildren<TMP_Text>(true);

        if (!visibilityRoot)
        {
            Transform parent = transform.parent;
            visibilityRoot = parent != null &&
                             string.Equals(parent.name, "FPSTextPanel", System.StringComparison.Ordinal)
                ? parent.gameObject
                : gameObject;
        }

        EnsureTopmostOverlayCanvas();
    }

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleSettingsChanged;
        ApplyVisibilityFromSettings();
        EnsureTopmostOverlayCanvas();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;
    }

    private void OnToggleSettingsChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.ShowFps)
            ApplyVisibilityFromSettings();
    }

    public static void RefreshAllFromSettings()
    {
        FpsDisplayText[] list = FindObjectsByType<FpsDisplayText>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i])
                list[i].ApplyVisibilityFromSettings();
        }
    }

    private void EnsureTopmostOverlayCanvas()
    {
        if (_overlayConfigured)
            return;

        GameObject root = visibilityRoot ? visibilityRoot : gameObject;
        GameplayScreenOverlayLayout.EnsureNestedOverlayCanvas(root, OverlaySortOrder);
        _overlayConfigured = true;
    }

    private void ApplyVisibilityFromSettings()
    {
        bool show = ToggleSettingsStore.Get(ToggleSettingId.ShowFps);
        if (visibilityRoot)
            visibilityRoot.SetActive(show);
    }

    private void Update()
    {
        if (!label || !Application.isPlaying)
            return;

        if (!ToggleSettingsStore.Get(ToggleSettingId.ShowFps))
            return;

        _accumUnscaledTime += Time.unscaledDeltaTime;
        _frameCount++;

        if (_accumUnscaledTime < refreshInterval)
            return;

        int fps = Mathf.RoundToInt(_frameCount / _accumUnscaledTime);
        _accumUnscaledTime = 0f;
        _frameCount = 0;

        label.text = string.Format(format, fps);
    }
}
