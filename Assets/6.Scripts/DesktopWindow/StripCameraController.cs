using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-200)]
public sealed class StripCameraController : MonoBehaviour, ISaveable
{
    [SerializeField] private Camera stripCamera;

    [Header("Viewport")]
    [Range(0.1f, 1f)]
    public float stripHeightPercent = 0.3f;

    [Tooltip("Bottom of the strip in normalized screen space. 0 = bottom, 1 = top.")]
    [Range(0f, 1f)]
    public float bottomNormalized = 0f;

    [Tooltip("Left edge of the strip in normalized screen space.")]
    [Range(0f, 1f)]
    public float leftNormalized = 0f;

    [Tooltip("Width of the strip in normalized screen space.")]
    [Range(0.1f, 1f)]
    public float widthNormalized = 1f;

    [Header("Orthographic Scaling")]
    [Min(0.01f)]
    public float baseOrthoSize = 4f;

    [Tooltip("Minimum orthographic half-height while playing (furthest zoom in).")]
    [SerializeField] private float minOrthoSize = 2.25f;

    [Tooltip("Fallback max ortho half-height only when lane WorldBounds are unavailable (e.g. loading). While playing with WorldBounds, max zoom-out is computed from lane width.")]
    [SerializeField, FormerlySerializedAs("maxOrthoSize")] private float maxOrthoSizeFallback = 9f;

    [Header("Keyboard zoom")]
    [Tooltip("Uses Settings ▸ Hotkeys ▸ Zoom In / Zoom Out (↑ / ↓ by default). Hold to repeat — speed × Δt each frame.")]
    [SerializeField] private bool enableKeyboardZoom = true;

    [Tooltip("Ortho half-height change per second while Up/Down is held (world units/s).")]
    [SerializeField] private float orthoZoomSpeed = 3f;

    [Header("Behaviour")]
    public bool updateContinuously = false;

    [Header("Persistence")]
    [Tooltip(
        "Save strip viewport rectangle via PlayerPrefs. Ortho zoom is persisted to the save slot and re-applied across level loads.")]
    [SerializeField] private bool persistStripLayout = true;

    private const string StripLayoutLegacyPrefsKey = "DesktopStripLayout.v1";
    private const string StripLayoutPrefsKey = "DesktopStripLayout.v2";
    private const float StripPrefsWriteMinInterval = 0.12f;
    private static float _nextAllowStripPrefsWriteTime = -999f;

    /// <summary>Keyboard zoom survives scene loads within one app session; save slot stores <see cref="SaveData.stripCameraZoomMultiplier"/>.</summary>
    private static bool _sessionOrthoActive;

    private static float _sessionBaseOrthoSize;

    /// <summary>
    /// First strip <see cref="Camera.aspect"/> we see during play (after lane bounds exist).
    /// Narrower strips than baseline would otherwise allow much larger lane-fit ortho; we clamp max zoom-out so it never exceeds <c>lane / (2×baselineAspect)</c>.
    /// </summary>
    private static float _sessionLaneZoomBaselineStripAspect = -1f;

    [Serializable]
    private struct SavedStripLayoutV2
    {
        public float stripHeightPercent;
        public float bottomNormalized;
        public float leftNormalized;
        public float widthNormalized;
    }

    /// <summary>Legacy saved JSON (ortho embedded); ortho ignored when migrating.</summary>
    [Serializable]
    private struct SavedStripLayoutLegacyV1
    {
        public float stripHeightPercent;
        public float bottomNormalized;
        public float leftNormalized;
        public float widthNormalized;
        public float baseOrthoSize;
    }

    private float _prefabOrthoAtAwake;
    private float _prefabStripHeightPercent;
    private float _prefabBottomNormalized;
    private float _prefabLeftNormalized;
    private float _prefabWidthNormalized;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private float _lastStripHeightPercent = float.NaN;
    private float _lastBottomNormalized = float.NaN;
    private float _lastLeftNormalized = float.NaN;
    private float _lastWidthNormalized = float.NaN;
    private float _lastBaseOrthoSize = float.NaN;

    private GameplayLevelBootstrapper _subscribedGameplayBootstrapper;

    private Coroutine _levelZoomReapplyRoutine;

    public float StripHeightPercent => Mathf.Clamp(stripHeightPercent, 0.1f, 1f);
    public float BottomNormalized => bottomNormalized;
    public float LeftNormalized => leftNormalized;
    public float WidthNormalized => Mathf.Clamp(widthNormalized, 0.1f, 1f);

    /// <summary>Orthographic size captured from the prefab/scene at <see cref="Awake"/> — base for zoom % HUD.</summary>
    public float DefaultOrthoBaseline => _prefabOrthoAtAwake;

    private void Awake()
    {
        CapturePrefabBaselineSnapshotFromSerializedFields();
    }

    private void OnEnable()
    {
        CacheCamera();
        if (Application.isPlaying && persistStripLayout)
            TryLoadSavedLayoutQuiet();

        if (Application.isPlaying && _sessionOrthoActive)
            baseOrthoSize = _sessionBaseOrthoSize;

        Apply(force: true);
    }

    private void Start()
    {
        Apply(force: true);

        if (!Application.isPlaying)
            return;

        StartCoroutine(CoBindGameplayBootstrapperForZoom());
    }

    private IEnumerator CoBindGameplayBootstrapperForZoom()
    {
        for (int i = 0; i < 12; i++)
        {
            GameplayLevelBootstrapper b = GameplayLevelBootstrapper.Instance;
            if (b != null)
            {
                if (_subscribedGameplayBootstrapper != null)
                    _subscribedGameplayBootstrapper.OnLevelStarted -= HandleGameplayLevelStartedForZoom;

                _subscribedGameplayBootstrapper = b;
                _subscribedGameplayBootstrapper.OnLevelStarted += HandleGameplayLevelStartedForZoom;
                yield break;
            }

            yield return null;
        }
    }

    private void HandleGameplayLevelStartedForZoom(MapNodeDefinition _)
    {
        if (!Application.isPlaying)
            return;

        if (_levelZoomReapplyRoutine != null)
            StopCoroutine(_levelZoomReapplyRoutine);
        _levelZoomReapplyRoutine = StartCoroutine(CoApplyZoomAfterLevelContentReady());
    }

    private IEnumerator CoApplyZoomAfterLevelContentReady()
    {
        // WorldBounds / lane width often update the frame after GameplayLevelBootstrapper.Start spawns layout.
        yield return null;
        yield return null;

        _levelZoomReapplyRoutine = null;

        if (!this || !isActiveAndEnabled)
            yield break;

        if (_sessionOrthoActive)
            baseOrthoSize = _sessionBaseOrthoSize;
        else
            ApplyZoomFromLastLoadedSaveDataIfAny();

        Apply(force: true);
    }

    private void ApplyZoomFromLastLoadedSaveDataIfAny()
    {
        if (SaveManager.Instance == null ||
            !SaveManager.Instance.TryGetLastLoadedData(out SaveData data) ||
            data.stripCameraZoomMultiplier <= 0.0001f)
            return;

        float baseline = DefaultOrthoBaseline;
        if (baseline < 0.01f)
            return;

        baseOrthoSize = baseline * data.stripCameraZoomMultiplier;
        Apply(force: true);
    }

    private void OnApplicationQuit()
    {
        if (Application.isPlaying && persistStripLayout)
            SaveLayoutToPrefs(forceImmediate: true);
    }

    private void OnDestroy()
    {
        if (_subscribedGameplayBootstrapper != null)
        {
            _subscribedGameplayBootstrapper.OnLevelStarted -= HandleGameplayLevelStartedForZoom;
            _subscribedGameplayBootstrapper = null;
        }

        if (_levelZoomReapplyRoutine != null)
        {
            StopCoroutine(_levelZoomReapplyRoutine);
            _levelZoomReapplyRoutine = null;
        }

        if (Application.isPlaying && persistStripLayout)
            SaveLayoutToPrefs(forceImmediate: true);
    }

    private void OnValidate()
    {
        CacheCamera();
        ClampInspectorValues();
        Apply(force: true);
    }

    private void Update()
    {
        if (Application.isPlaying)
            ClampInspectorValues();

        if (Application.isPlaying && enableKeyboardZoom)
            ApplyKeyboardOrthoZoom();

        if (updateContinuously || HasChanged())
            Apply(force: false);
    }

    /// <summary>
    /// Hold configured keys (defaults: Up = zoom in / smaller ortho, Down = zoom out / larger ortho) — editable in Settings ▸ Hotkeys.
    /// </summary>
    private void ApplyKeyboardOrthoZoom()
    {
        if (HelperGameplayController.BlocksStripGameplay)
            return;

        CacheCamera();

        if (!stripCamera || !stripCamera.orthographic)
            return;

        if (HotkeySettingsRowUI.IsRebinding)
            return;

        KeyCode zoomIn = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.ZoomIn)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.ZoomIn);
        KeyCode zoomOut = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.ZoomOut)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.ZoomOut);

        float change = orthoZoomSpeed * Time.deltaTime;
        int zoomInput = 0;
        if (zoomOut != KeyCode.None && Input.GetKey(zoomOut))
            zoomInput++;
        if (zoomIn != KeyCode.None && Input.GetKey(zoomIn))
            zoomInput--;
        if (zoomInput == 0)
            return;

        baseOrthoSize += change * zoomInput;

        ClampInspectorValues();

        Apply(force: true);
    }

    /// <summary>
    /// Max zoom-out ortho from lane width: visible world width ≈ <c>2 × ortho × aspect</c>, so lane fit is <c>lane / (2×aspect)</c>.
    /// The first-session baseline strip aspect records "full width" at load — we never allow ortho above that baseline lane-fit ceiling,
    /// so narrowing the window cannot unlock an even more distant zoom-out.
    /// </summary>
    private float GetEffectiveMaxOrthoSize()
    {
        if (!Application.isPlaying)
            return maxOrthoSizeFallback;

        CacheCamera();

        if (!stripCamera || !stripCamera.orthographic || !stripCamera.isActiveAndEnabled)
            return Mathf.Max(minOrthoSize, maxOrthoSizeFallback);

        WorldBounds wb = WorldBounds.Instance;
        if (wb == null)
            return Mathf.Max(minOrthoSize, maxOrthoSizeFallback);

        float laneW = wb.Right - wb.Left;
        if (laneW <= 1e-4f)
            return Mathf.Max(minOrthoSize, maxOrthoSizeFallback);

        float aspect = Mathf.Max(0.001f, stripCamera.aspect);

        // One-shot per cold session — "good" max zoom matches full-width-at-load lane fit; narrower window uses min(...) so ortho ceiling does not rise.
        if (_sessionLaneZoomBaselineStripAspect < 1e-4f)
            _sessionLaneZoomBaselineStripAspect = aspect;

        float laneFitAtCurrentAspect = laneW / (2f * aspect);
        float laneFitAtBaselineAspect = laneW / (2f * _sessionLaneZoomBaselineStripAspect);
        float laneBasedMax = Mathf.Min(laneFitAtCurrentAspect, laneFitAtBaselineAspect);

        return Mathf.Max(minOrthoSize, laneBasedMax);
    }

    public void SetBottomNormalized(float value)
    {
        bottomNormalized = value;
        Apply(force: true);
    }

    public void SetLeftNormalized(float value)
    {
        leftNormalized = value;
        Apply(force: true);
    }

    public void SetWidthNormalized(float value)
    {
        widthNormalized = value;
        Apply(force: true);
    }

    public void SetStripHeightPercent(float value)
    {
        stripHeightPercent = value;
        Apply(force: true);
    }

    /// <summary>Deletes persisted strip rectangle prefs, clears session ortho zoom, and snaps all strip cameras back to prefab/script defaults.</summary>
    public static void FactoryResetStoredStripLayoutAcrossApp()
    {
        PlayerPrefs.DeleteKey(StripLayoutPrefsKey);
        PlayerPrefs.DeleteKey(StripLayoutLegacyPrefsKey);
        PlayerPrefs.Save();

        ClearSessionOrthoZoomState();

        StripCameraController[] list = UnityEngine.Object.FindObjectsByType<StripCameraController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < list.Length; i++)
        {
            StripCameraController c = list[i];
            if (!c || !Application.isPlaying)
                continue;

            c.ApplyPrefabBaselineSnapshot();
            if (c.persistStripLayout)
                c.SaveLayoutToPrefs(forceImmediate: true);
        }
    }

    public static void ClearSessionOrthoZoomState()
    {
        _sessionOrthoActive = false;
        _sessionLaneZoomBaselineStripAspect = -1f;
    }

    private void CapturePrefabBaselineSnapshotFromSerializedFields()
    {
        _prefabOrthoAtAwake = baseOrthoSize;
        _prefabStripHeightPercent = stripHeightPercent;
        _prefabBottomNormalized = bottomNormalized;
        _prefabLeftNormalized = leftNormalized;
        _prefabWidthNormalized = widthNormalized;
    }

    private void ApplyPrefabBaselineSnapshot()
    {
        stripHeightPercent = _prefabStripHeightPercent;
        bottomNormalized = _prefabBottomNormalized;
        leftNormalized = _prefabLeftNormalized;
        widthNormalized = _prefabWidthNormalized;
        baseOrthoSize = _prefabOrthoAtAwake;
    }

    private void CacheCamera()
    {
        if (!stripCamera)
            stripCamera = GetComponent<Camera>();
    }

    private bool HasChanged()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            return true;

        return !Mathf.Approximately(stripHeightPercent, _lastStripHeightPercent) ||
               !Mathf.Approximately(bottomNormalized, _lastBottomNormalized) ||
               !Mathf.Approximately(leftNormalized, _lastLeftNormalized) ||
               !Mathf.Approximately(widthNormalized, _lastWidthNormalized) ||
               !Mathf.Approximately(baseOrthoSize, _lastBaseOrthoSize);
    }

    private void Apply(bool force)
    {
        CacheCamera();

        if (!stripCamera)
            return;

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        if (!force && !HasChanged())
            return;

        ClampInspectorValues();

        float height = StripHeightPercent;
        float width = WidthNormalized;
        float x = Mathf.Clamp(leftNormalized, 0f, 1f - width);
        float y = Mathf.Clamp(bottomNormalized, 0f, 1f - height);

        stripCamera.rect = new Rect(x, y, width, height);

        if (stripCamera.orthographic)
            stripCamera.orthographicSize = baseOrthoSize;

        RememberCurrentState();
    }

    private void TryLoadSavedLayoutQuiet()
    {
        if (!PlayerPrefs.HasKey(StripLayoutPrefsKey))
        {
            TryMigrateLegacyStripLayoutPrefsV1Quiet();
            return;
        }

        try
        {
            string json = PlayerPrefs.GetString(StripLayoutPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            SavedStripLayoutV2 s = JsonUtility.FromJson<SavedStripLayoutV2>(json);
            ApplyRectsFromSaved(s);
        }
        catch (Exception)
        {
            // Corrupt pref data — ignored.
        }
    }

    private void TryMigrateLegacyStripLayoutPrefsV1Quiet()
    {
        if (!PlayerPrefs.HasKey(StripLayoutLegacyPrefsKey))
            return;

        try
        {
            string json = PlayerPrefs.GetString(StripLayoutLegacyPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            SavedStripLayoutLegacyV1 legacy = JsonUtility.FromJson<SavedStripLayoutLegacyV1>(json);
            var v2 = new SavedStripLayoutV2
            {
                stripHeightPercent = legacy.stripHeightPercent,
                bottomNormalized = legacy.bottomNormalized,
                leftNormalized = legacy.leftNormalized,
                widthNormalized = legacy.widthNormalized
            };

            ApplyRectsFromSaved(v2);
            PlayerPrefs.DeleteKey(StripLayoutLegacyPrefsKey);
            SaveLayoutToPrefs(forceImmediate: true);
        }
        catch (Exception)
        {
            PlayerPrefs.DeleteKey(StripLayoutLegacyPrefsKey);
        }
    }

    private void ApplyRectsFromSaved(SavedStripLayoutV2 s)
    {
        stripHeightPercent = s.stripHeightPercent;
        bottomNormalized = s.bottomNormalized;
        leftNormalized = s.leftNormalized;
        widthNormalized = s.widthNormalized;
    }

    private void SaveLayoutToPrefs(bool forceImmediate)
    {
        if (!forceImmediate && Time.unscaledTime < _nextAllowStripPrefsWriteTime)
            return;

        _nextAllowStripPrefsWriteTime = Time.unscaledTime + StripPrefsWriteMinInterval;

        var s = new SavedStripLayoutV2
        {
            stripHeightPercent = stripHeightPercent,
            bottomNormalized = bottomNormalized,
            leftNormalized = leftNormalized,
            widthNormalized = widthNormalized
        };

        PlayerPrefs.SetString(StripLayoutPrefsKey, JsonUtility.ToJson(s));
        PlayerPrefs.Save();
    }

    private void ClampInspectorValues()
    {
        stripHeightPercent = Mathf.Clamp(stripHeightPercent, 0.1f, 1f);
        bottomNormalized = Mathf.Clamp01(bottomNormalized);
        leftNormalized = Mathf.Clamp01(leftNormalized);
        widthNormalized = Mathf.Clamp(widthNormalized, 0.1f, 1f);

        minOrthoSize = Mathf.Max(0.01f, minOrthoSize);

        float hi = GetEffectiveMaxOrthoSize();

        baseOrthoSize = Mathf.Clamp(Mathf.Max(0.01f, baseOrthoSize), minOrthoSize, hi);
    }

    private void RememberCurrentState()
    {
        bool layoutChanged =
            !Mathf.Approximately(stripHeightPercent, _lastStripHeightPercent) ||
            !Mathf.Approximately(bottomNormalized, _lastBottomNormalized) ||
            !Mathf.Approximately(leftNormalized, _lastLeftNormalized) ||
            !Mathf.Approximately(widthNormalized, _lastWidthNormalized) ||
            !Mathf.Approximately(baseOrthoSize, _lastBaseOrthoSize) ||
            Screen.width != _lastScreenWidth ||
            Screen.height != _lastScreenHeight;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastStripHeightPercent = stripHeightPercent;
        _lastBottomNormalized = bottomNormalized;
        _lastLeftNormalized = leftNormalized;
        _lastWidthNormalized = widthNormalized;
        _lastBaseOrthoSize = baseOrthoSize;

        if (Application.isPlaying)
        {
            _sessionBaseOrthoSize = baseOrthoSize;
            _sessionOrthoActive = true;
        }

        if (Application.isPlaying && persistStripLayout && layoutChanged)
            SaveLayoutToPrefs(forceImmediate: false);

        if (Application.isPlaying && layoutChanged)
            SaveManager.Instance?.NotifyStripZoomChangedDebounced();
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        float baseline = DefaultOrthoBaseline;
        if (baseline < 0.01f)
            return;

        data.stripCameraZoomMultiplier = Mathf.Max(0.01f, baseOrthoSize) / baseline;
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null)
            return;

        if (data.stripCameraZoomMultiplier <= 0.0001f)
            return;

        float baseline = DefaultOrthoBaseline;
        if (baseline < 0.01f)
            return;

        baseOrthoSize = baseline * data.stripCameraZoomMultiplier;
        Apply(force: true);
    }
}
