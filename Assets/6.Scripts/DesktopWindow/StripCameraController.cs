using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-200)]
public sealed class StripCameraController : MonoBehaviour, ISaveable
{
    public const float StripHeightPercentNormal = 0.3333f;

    [SerializeField] private Camera stripCamera;

    [Header("Viewport")]
    [Range(0.1f, 1f)]
    public float stripHeightPercent = StripHeightPercentNormal;

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
    [Tooltip("Uses Settings ▸ Hotkeys ▸ Zoom In / Zoom Out (scroll wheel by default). Keys repeat while held; scroll wheel steps once per tick.")]
    [SerializeField] private bool enableKeyboardZoom = true;

    [Tooltip("Ortho half-height change per second while a zoom key is held (world units/s).")]
    [SerializeField] private float orthoZoomSpeed = 7f;

    [Tooltip("Ortho half-height change per scroll-wheel unit (applied to zoom target, then smoothed).")]
    [SerializeField] private float scrollZoomSensitivity = 0.25f;

    [Tooltip("Max ortho target change from one scroll-wheel frame (prevents harsh multi-notch jumps).")]
    [SerializeField] private float maxScrollOrthoDeltaPerFrame = 0.45f;

    [Tooltip("Seconds to ease the camera toward the scroll/key zoom target.")]
    [SerializeField] private float orthoZoomSmoothTime = 0.05f;

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
    private static bool _ignoreSavedZoomOnceOnGameplayEntry;

    /// <summary>
    /// First strip <see cref="Camera.aspect"/> we see during play (after lane bounds exist).
    /// Narrower strips than baseline would otherwise allow much larger lane-fit ortho; we clamp max zoom-out so it never exceeds <c>lane / (2×baselineAspect)</c>.
    /// </summary>
    private static float _sessionLaneZoomBaselineStripAspect = -1f;

    private static bool s_stripLayoutLockedForExpandBackground;

    /// <summary>When expand-background is on, strip position/size cannot be changed (see <see cref="SetExpandBackgroundStripLayoutLocked"/>).</summary>
    public static bool IsStripLayoutLockedForExpandBackground => s_stripLayoutLockedForExpandBackground;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyExpandStripLockFromSettingsAfterSceneLoad()
    {
        if (!Application.isPlaying)
            return;

        SyncStripLockToExpandBackgroundSetting();
    }

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
    private float _targetOrthoSize;
    private float _orthoZoomVelocity;
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
        SyncTargetOrthoFromBase();
    }

    private void OnEnable()
    {
        CacheCamera();
        if (Application.isPlaying)
            SyncStripLockToExpandBackgroundSetting();

        if (Application.isPlaying)
        {
            // Requirement: on game load, zoom always starts at default (prefab baseline).
            // After the player has a session zoom (keyboard / slider), keep it across level changes.
            baseOrthoSize = _sessionOrthoActive ? _sessionBaseOrthoSize : DefaultOrthoBaseline;
            SyncTargetOrthoFromBase();
        }

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
            baseOrthoSize = DefaultOrthoBaseline;

        SyncTargetOrthoFromBase();
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
        SyncTargetOrthoFromBase();
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
        {
            if (s_stripLayoutLockedForExpandBackground &&
                !ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            {
                SyncStripLockToExpandBackgroundSetting();
            }
            else if (s_stripLayoutLockedForExpandBackground)
            {
                EnforceLockedBaselineLayoutIfNeeded();
            }

            ClampInspectorValues();
        }

        if (Application.isPlaying && enableKeyboardZoom)
            ApplyKeyboardOrthoZoom();

        if (updateContinuously || HasChanged())
            Apply(force: false);
    }

    /// <summary>
    /// Zoom via Settings ▸ Hotkeys (scroll wheel up/down by default). Keys repeat while held; scroll wheel steps once per tick.
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

        HotkeyChord zoomIn = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetChord(HotkeyBindId.ZoomIn)
            : HotkeyBindingManager.GetDefaultChord(HotkeyBindId.ZoomIn);
        HotkeyChord zoomOut = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetChord(HotkeyBindId.ZoomOut)
            : HotkeyBindingManager.GetDefaultChord(HotkeyBindId.ZoomOut);

        float scrollY = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                if (scrollY > 0f && zoomIn.IsMouseScrollUp)
                {
                    float delta = Mathf.Clamp(scrollY * scrollZoomSensitivity, 0f, maxScrollOrthoDeltaPerFrame);
                    _targetOrthoSize -= delta;
                }
                else if (scrollY < 0f && zoomOut.IsMouseScrollDown)
                {
                    float delta = Mathf.Clamp(-scrollY * scrollZoomSensitivity, 0f, maxScrollOrthoDeltaPerFrame);
                    _targetOrthoSize += delta;
                }
            }
        }

        float change = orthoZoomSpeed * Time.deltaTime;
        int zoomInput = 0;
        if (!zoomOut.IsMouseScroll && !zoomOut.IsEmpty && HotkeyChord.IsHeld(zoomOut))
            zoomInput++;
        if (!zoomIn.IsMouseScroll && !zoomIn.IsEmpty && HotkeyChord.IsHeld(zoomIn))
            zoomInput--;
        if (zoomInput != 0)
            _targetOrthoSize += change * zoomInput;

        ApplySmoothedOrthoZoom();
    }

    private void ApplySmoothedOrthoZoom()
    {
        ClampOrthoZoomTargets();

        if (Mathf.Approximately(baseOrthoSize, _targetOrthoSize))
            return;

        baseOrthoSize = Mathf.SmoothDamp(
            baseOrthoSize,
            _targetOrthoSize,
            ref _orthoZoomVelocity,
            Mathf.Max(0.01f, orthoZoomSmoothTime));

        ClampInspectorValues();
        Apply(force: true);
    }

    private void SyncTargetOrthoFromBase()
    {
        _targetOrthoSize = baseOrthoSize;
        _orthoZoomVelocity = 0f;
    }

    private void ClampOrthoZoomTargets()
    {
        float hi = GetEffectiveMaxOrthoSize();
        float lo = Mathf.Max(0.01f, minOrthoSize);
        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, lo, hi);
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
        if (s_stripLayoutLockedForExpandBackground)
            return;

        bottomNormalized = value;
        Apply(force: true);
    }

    public void SetLeftNormalized(float value)
    {
        if (s_stripLayoutLockedForExpandBackground)
            return;

        leftNormalized = value;
        Apply(force: true);
    }

    public void SetWidthNormalized(float value)
    {
        if (s_stripLayoutLockedForExpandBackground)
            return;

        widthNormalized = value;
        Apply(force: true);
    }

    public void SetStripHeightPercent(float value)
    {
        if (s_stripLayoutLockedForExpandBackground)
            return;

        stripHeightPercent = value;
        Apply(force: true);
    }

    /// <summary>Snap strip layout to the normal prefab baseline (used when expand-background locks the strip).</summary>
    public void SnapToNormalStripLayoutForExpandLock()
    {
        float preservedOrtho = baseOrthoSize;
        ApplyPrefabLayoutRectSnapshot();
        baseOrthoSize = preservedOrtho;
        Apply(force: true);
    }

    /// <summary>Applies strip lock from <see cref="ToggleSettingId.ExpandStripBackground"/> (on = locked).</summary>
    public static void SyncStripLockToExpandBackgroundSetting()
    {
        SetExpandBackgroundStripLayoutLocked(
            ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground));
    }

    /// <summary>Locks or unlocks strip move/resize; when locking, snaps all strip cameras to the normal layout.</summary>
    public static void SetExpandBackgroundStripLayoutLocked(bool locked)
    {
        bool stateChanged = s_stripLayoutLockedForExpandBackground != locked;
        s_stripLayoutLockedForExpandBackground = locked;

        StripCameraController[] controllers = UnityEngine.Object.FindObjectsByType<StripCameraController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (locked && stateChanged)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                StripCameraController ctrl = controllers[i];
                if (ctrl)
                    ctrl.SnapToNormalStripLayoutForExpandLock();
            }
        }
        else if (!locked && stateChanged)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                StripCameraController ctrl = controllers[i];
                if (ctrl)
                    ctrl.RestorePersistedStripLayoutIfAny();
            }
        }

        SyncStripManipulatorInteractables(!locked);
    }

    private void RestorePersistedStripLayoutIfAny()
    {
        if (!persistStripLayout)
            return;

        TryLoadSavedLayoutQuiet();
        Apply(force: true);
    }

    private static void SyncStripManipulatorInteractables(bool interactable)
    {
        DragStripBar[] dragBars = UnityEngine.Object.FindObjectsByType<DragStripBar>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < dragBars.Length; i++)
        {
            if (dragBars[i])
                dragBars[i].enabled = interactable;
        }

        RightEdgeResizer[] resizers = UnityEngine.Object.FindObjectsByType<RightEdgeResizer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < resizers.Length; i++)
        {
            RightEdgeResizer resizer = resizers[i];
            if (!resizer)
                continue;

            resizer.enabled = interactable;
            resizer.gameObject.SetActive(interactable);
        }
    }

    private void EnforceLockedBaselineLayoutIfNeeded()
    {
        if (!s_stripLayoutLockedForExpandBackground)
            return;

        float expectedBottom = GetLockedExpandBaselineBottomNormalized();
        if (Mathf.Approximately(stripHeightPercent, _prefabStripHeightPercent) &&
            Mathf.Approximately(bottomNormalized, expectedBottom) &&
            Mathf.Approximately(leftNormalized, _prefabLeftNormalized) &&
            Mathf.Approximately(widthNormalized, _prefabWidthNormalized))
        {
            return;
        }

        float preservedOrtho = baseOrthoSize;
        ApplyPrefabLayoutRectSnapshot();
        baseOrthoSize = preservedOrtho;
        Apply(force: true);
    }

    private float GetLockedExpandBaselineBottomNormalized()
    {
        float taskbarBottomMin = GetTaskbarBottomMinNormalized();
        float bottom = Mathf.Max(_prefabBottomNormalized, taskbarBottomMin);
        if (bottom + _prefabStripHeightPercent > 1f)
            bottom = Mathf.Max(taskbarBottomMin, 1f - _prefabStripHeightPercent);
        return bottom;
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

    /// <summary>
    /// Forces the next <see cref="LoadFrom"/> (save apply) to ignore <see cref="SaveData.stripCameraZoomMultiplier"/> and
    /// snap to <see cref="DefaultOrthoBaseline"/> instead. Also clears session zoom so the baseline wins.
    /// </summary>
    public static void IgnoreSavedZoomOnceOnNextGameplayLoad()
    {
        _ignoreSavedZoomOnceOnGameplayEntry = true;
        ClearSessionOrthoZoomState();
    }

    private void CapturePrefabBaselineSnapshotFromSerializedFields()
    {
        _prefabOrthoAtAwake = baseOrthoSize;
        _prefabStripHeightPercent = StripHeightPercentNormal;
        _prefabBottomNormalized = bottomNormalized;
        _prefabLeftNormalized = leftNormalized;
        _prefabWidthNormalized = widthNormalized;
    }

    private void ApplyPrefabBaselineSnapshot()
    {
        ApplyPrefabLayoutRectSnapshot();
        baseOrthoSize = _prefabOrthoAtAwake;
        SyncTargetOrthoFromBase();
    }

    private void ApplyPrefabLayoutRectSnapshot()
    {
        stripHeightPercent = _prefabStripHeightPercent;
        bottomNormalized = _prefabBottomNormalized;
        leftNormalized = _prefabLeftNormalized;
        widthNormalized = _prefabWidthNormalized;
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
        if (s_stripLayoutLockedForExpandBackground ||
            ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            return;

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

    /// <summary>
    /// Strip width / horizontal anchor are intentionally NOT restored from PlayerPrefs.
    ///
    /// Every cold load starts the strip at the prefab full-width baseline (typically <c>widthNormalized = 1</c>,
    /// <c>leftNormalized = 0</c>). The user can still drag the right edge to shrink the strip during a session
    /// (see <see cref="RightEdgeResizer"/>) and that change persists for the running session, but the next
    /// launch returns to full width.
    ///
    /// Why: <see cref="GetEffectiveMaxOrthoSize"/> records the first observed strip aspect of the session as
    /// <see cref="_sessionLaneZoomBaselineStripAspect"/> and bakes the lane-fit zoom-out ceiling from that. If
    /// we restored a narrow saved width, the baseline aspect would be narrow on load and the lane-fit ortho
    /// ceiling (<c>laneW / (2 × baselineAspect)</c>) would explode — making zoom-out behave wildly compared to
    /// the prefab-tuned ceiling. Forcing full-width on load anchors that baseline correctly every time.
    /// </summary>
    private void ApplyRectsFromSaved(SavedStripLayoutV2 s)
    {
        if (s_stripLayoutLockedForExpandBackground ||
            ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            return;

        stripHeightPercent = s.stripHeightPercent;
        bottomNormalized = s.bottomNormalized;
        leftNormalized = _prefabLeftNormalized;
        widthNormalized = _prefabWidthNormalized;

        ClampSavedStripHeightForGameplayLayout();
    }

    private void ClampSavedStripHeightForGameplayLayout()
    {
        if (!Application.isPlaying)
            return;

        if (stripHeightPercent > StripHeightPercentNormal + 0.001f)
            stripHeightPercent = StripHeightPercentNormal;
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

        // When the game window covers the OS taskbar (see MonitorSwitcher.coverEntireMonitorIncludingTaskbar),
        // keep the strip's bottom edge above the taskbar so the EXP bar / BotomGameBar / UI_Frame floor doesn't get
        // hidden behind it. Player can still slide the strip up — only the lowest position is clamped.
        float taskbarBottomMinNormalized = GetTaskbarBottomMinNormalized();
        if (bottomNormalized < taskbarBottomMinNormalized)
            bottomNormalized = taskbarBottomMinNormalized;

        if (bottomNormalized + stripHeightPercent > 1f)
            bottomNormalized = Mathf.Max(taskbarBottomMinNormalized, 1f - stripHeightPercent);

        minOrthoSize = Mathf.Max(0.01f, minOrthoSize);

        float hi = GetEffectiveMaxOrthoSize();

        baseOrthoSize = Mathf.Clamp(Mathf.Max(0.01f, baseOrthoSize), minOrthoSize, hi);
        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, minOrthoSize, hi);
    }

    /// <summary>
    /// Normalized (0..1 of screen height) bottom margin reserved for the OS taskbar. Zero when MonitorSwitcher
    /// hasn't published a value yet (e.g. Editor, no taskbar overlap, taskbar on a side edge).
    /// </summary>
    private static float GetTaskbarBottomMinNormalized()
    {
        int reservedPx = MonitorSwitcher.BottomTaskbarReservedPixels;
        if (reservedPx <= 0)
            return 0f;
        int screenH = Screen.height;
        if (screenH <= 0)
            return 0f;
        return Mathf.Clamp01((float)reservedPx / screenH);
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

        if (Application.isPlaying && persistStripLayout && layoutChanged &&
            !s_stripLayoutLockedForExpandBackground)
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

        if (_ignoreSavedZoomOnceOnGameplayEntry)
        {
            _ignoreSavedZoomOnceOnGameplayEntry = false;
            baseOrthoSize = DefaultOrthoBaseline;
            SyncTargetOrthoFromBase();
            Apply(force: true);
            return;
        }

        if (data.stripCameraZoomMultiplier <= 0.0001f)
            return;

        float baseline = DefaultOrthoBaseline;
        if (baseline < 0.01f)
            return;

        baseOrthoSize = baseline * data.stripCameraZoomMultiplier;
        SyncTargetOrthoFromBase();
        Apply(force: true);
    }
}
