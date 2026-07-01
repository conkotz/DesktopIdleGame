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

    /// <summary>Smallest strip width the player can resize to (fraction of screen width).</summary>
    public const float MinStripWidthNormalized = 0.52f;

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

    [Header("Zoom settings")]
    [Tooltip("Ortho half-height change per second while a zoom hotkey is held (world units/s).")]
    [SerializeField] private float orthoZoomSpeed = 7f;

    [Tooltip("Ortho half-height change per scroll-wheel notch (applied to zoom target, then smoothed).")]
    [SerializeField] private float scrollZoomSensitivity = 0.25f;

    [Tooltip("Caps ortho change from a single scroll-wheel frame (prevents harsh multi-notch jumps). Not camera Y movement.")]
    [SerializeField] private float maxScrollOrthoDeltaPerFrame = 0.45f;

    [Tooltip("Seconds to ease the camera toward the scroll/key zoom target.")]
    [SerializeField] private float orthoZoomSmoothTime = 0.05f;

    [Tooltip(
        "When zooming out (ortho increases), camera world Y added per ortho unit above the default. " +
        "1 keeps the strip bottom anchored like default zoom.")]
    [SerializeField] private float zoomOutCameraLiftPerOrthoUnit = 1f;

    [Tooltip(
        "When zooming in (ortho decreases), camera world Y removed per ortho unit below the default. " +
        "1 keeps the strip bottom anchored like default zoom.")]
    [SerializeField] private float zoomInCameraDropPerOrthoUnit = 1f;

    [Header("Lane framing")]
    [Tooltip("Strip camera world Y when Lane Vertical Framing Pixels is 0. Context menu: Capture Framing Baseline From Transform.")]
    [SerializeField] private float framingBaselineWorldY;

    [Tooltip(
        "Nudge strip camera height (strip pixels). Positive = camera up = playfield lower in the strip. " +
        "Works in the editor and in play mode. Does not move MainLane.")]
    [SerializeField] private float laneVerticalFramingPixels;

    /// <summary>Screen-pixel offset applied to strip camera Y relative to <see cref="framingBaselineWorldY"/>.</summary>
    public float LaneVerticalFramingPixels => laneVerticalFramingPixels;

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

    /// <summary>Strip viewport rect the player chose this session (survives GamePlay scene reloads; not cold launch).</summary>
    private static bool _sessionStripLayoutActive;
    private static float _sessionStripHeightPercent;
    private static float _sessionBottomNormalized;
    private static float _sessionLeftNormalized;
    private static float _sessionWidthNormalized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionStatics()
    {
        _sessionOrthoActive = false;
        _sessionBaseOrthoSize = 0f;
        _ignoreSavedZoomOnceOnGameplayEntry = false;
        _sessionLaneZoomBaselineStripAspect = -1f;
        ClearSessionStripLayoutState();
    }

    /// <summary>
    /// First strip <see cref="Camera.aspect"/> we see during play (after lane bounds exist).
    /// Narrower strips than baseline would otherwise allow much larger lane-fit ortho; we clamp max zoom-out so it never exceeds <c>lane / (2×baselineAspect)</c>.
    /// </summary>
    private static float _sessionLaneZoomBaselineStripAspect = -1f;

    private static bool s_stripLayoutLockedForExpandBackground;

    /// <summary>Fired when strip viewport rect, screen size, or orthographic zoom changes.</summary>
    public static event Action StripLayoutChanged;

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
    private float _lastLaneVerticalFramingPixels = float.NaN;
    private float _lastZoomOutCameraLiftPerOrthoUnit = float.NaN;
    private float _lastZoomInCameraDropPerOrthoUnit = float.NaN;
    private float _lastFramingBaselineWorldY = float.NaN;

    private GameplayLevelBootstrapper _subscribedGameplayBootstrapper;

    private Coroutine _levelZoomReapplyRoutine;

    public float StripHeightPercent => Mathf.Clamp(stripHeightPercent, 0.1f, 1f);
    public float BottomNormalized => bottomNormalized;
    public float LeftNormalized => leftNormalized;
    public float WidthNormalized => Mathf.Clamp(widthNormalized, MinStripWidthNormalized, 1f);

    /// <summary>Orthographic size captured from the prefab/scene at <see cref="Awake"/> — base for zoom % HUD.</summary>
    public float DefaultOrthoBaseline => _prefabOrthoAtAwake;

    private void Reset()
    {
        CacheCamera();
        CapturePrefabBaselineSnapshotFromSerializedFields();
        if (stripCamera != null)
            framingBaselineWorldY = stripCamera.transform.position.y;
    }

    private void Awake()
    {
        CapturePrefabBaselineSnapshotFromSerializedFields();
        SyncTargetOrthoFromBase();
        CacheCamera();
        EnsureFramingBaselineFromTransform();
    }

    private void EnsureFramingBaselineFromTransform()
    {
        if (!stripCamera)
            return;

        if (Mathf.Approximately(framingBaselineWorldY, 0f) &&
            !Mathf.Approximately(stripCamera.transform.position.y, 0f))
        {
            framingBaselineWorldY = stripCamera.transform.position.y;
        }
    }

    [ContextMenu("Capture Framing Baseline From Transform")]
    private void CaptureFramingBaselineFromTransform()
    {
        CacheCamera();
        if (!stripCamera)
            return;

        framingBaselineWorldY = stripCamera.transform.position.y;
        laneVerticalFramingPixels = 0f;
        Apply(force: true);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
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
            TryApplySessionStripLayoutIfAny();
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
        if (_prefabOrthoAtAwake < 0.01f)
            CapturePrefabBaselineSnapshotFromSerializedFields();
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

        if (Application.isPlaying)
            ApplyInteractiveOrthoZoom();

        if (updateContinuously || HasChanged())
            Apply(force: false);
    }

    /// <summary>
    /// Zoom via scroll wheel or Settings ▸ Hotkeys (scroll wheel up/down by default).
    /// </summary>
    private void ApplyInteractiveOrthoZoom()
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
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!pointerOverUi && IsScrollZoomAllowedAtPointer())
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

    /// <summary>
    /// Scroll zoom only when the cursor is inside the game window and over gameplay viewport empty space
    /// (strip rect, or expand-background sky band above the strip). Matches desktop click-through regions.
    /// </summary>
    private bool IsScrollZoomAllowedAtPointer()
    {
        Vector2 mouse = Input.mousePosition;

        if (mouse.x < 0f || mouse.y < 0f || mouse.x > Screen.width || mouse.y > Screen.height)
            return false;

        if (!stripCamera)
            return false;

        Rect stripRect = stripCamera.pixelRect;
        if (stripRect.width <= 0f || stripRect.height <= 0f)
            return false;

        if (stripRect.Contains(mouse))
            return true;

        if (!ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
            return false;

        return mouse.y > stripRect.yMax &&
               mouse.x >= stripRect.xMin &&
               mouse.x <= stripRect.xMax;
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

        widthNormalized = Mathf.Clamp(value, MinStripWidthNormalized, 1f);
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

        if (_sessionStripLayoutActive)
        {
            TryApplySessionStripLayoutIfAny();
            Apply(force: true);
            return;
        }

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
        ClearSessionStripLayoutState();

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

    public static void ClearSessionStripLayoutState()
    {
        _sessionStripLayoutActive = false;
        _sessionStripHeightPercent = StripHeightPercentNormal;
        _sessionBottomNormalized = 0f;
        _sessionLeftNormalized = 0f;
        _sessionWidthNormalized = 1f;
    }

    private static void CaptureSessionStripLayout(
        float stripHeight,
        float bottom,
        float left,
        float width)
    {
        if (!Application.isPlaying || s_stripLayoutLockedForExpandBackground)
            return;

        _sessionStripLayoutActive = true;
        _sessionStripHeightPercent = stripHeight;
        _sessionBottomNormalized = bottom;
        _sessionLeftNormalized = left;
        _sessionWidthNormalized = width;
    }

    private void TryApplySessionStripLayoutIfAny()
    {
        if (!Application.isPlaying || !_sessionStripLayoutActive || s_stripLayoutLockedForExpandBackground)
            return;

        stripHeightPercent = _sessionStripHeightPercent;
        bottomNormalized = _sessionBottomNormalized;
        leftNormalized = _sessionLeftNormalized;
        widthNormalized = _sessionWidthNormalized;
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
               !Mathf.Approximately(baseOrthoSize, _lastBaseOrthoSize) ||
               !Mathf.Approximately(laneVerticalFramingPixels, _lastLaneVerticalFramingPixels) ||
               !Mathf.Approximately(framingBaselineWorldY, _lastFramingBaselineWorldY) ||
               !Mathf.Approximately(zoomOutCameraLiftPerOrthoUnit, _lastZoomOutCameraLiftPerOrthoUnit) ||
               !Mathf.Approximately(zoomInCameraDropPerOrthoUnit, _lastZoomInCameraDropPerOrthoUnit);
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

        ApplyCameraVerticalPosition();

        RememberCurrentState();
    }

    private void ApplyCameraVerticalPosition()
    {
        if (!stripCamera || !stripCamera.orthographic)
            return;

        float stripPixelHeight = stripCamera.pixelRect.height;
        if (stripPixelHeight <= 0f)
            return;

        float ortho = stripCamera.orthographicSize;
        float worldPerPixel = (2f * ortho) / stripPixelHeight;
        float framingOffset = laneVerticalFramingPixels * worldPerPixel;

        float zoomAnchorOffset = 0f;
        if (Application.isPlaying)
        {
            float dOrtho = ortho - _prefabOrthoAtAwake;
            zoomAnchorOffset = dOrtho >= 0f
                ? dOrtho * zoomOutCameraLiftPerOrthoUnit
                : dOrtho * zoomInCameraDropPerOrthoUnit;
        }

        float targetY = framingBaselineWorldY + framingOffset + zoomAnchorOffset;

        Vector3 p = stripCamera.transform.position;
        if (Mathf.Approximately(p.y, targetY))
            return;

        p.y = targetY;
        stripCamera.transform.position = p;
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
        widthNormalized = Mathf.Clamp(widthNormalized, MinStripWidthNormalized, 1f);

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
            !Mathf.Approximately(laneVerticalFramingPixels, _lastLaneVerticalFramingPixels) ||
            !Mathf.Approximately(framingBaselineWorldY, _lastFramingBaselineWorldY) ||
            !Mathf.Approximately(zoomOutCameraLiftPerOrthoUnit, _lastZoomOutCameraLiftPerOrthoUnit) ||
            !Mathf.Approximately(zoomInCameraDropPerOrthoUnit, _lastZoomInCameraDropPerOrthoUnit) ||
            Screen.width != _lastScreenWidth ||
            Screen.height != _lastScreenHeight;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastStripHeightPercent = stripHeightPercent;
        _lastBottomNormalized = bottomNormalized;
        _lastLeftNormalized = leftNormalized;
        _lastWidthNormalized = widthNormalized;
        _lastBaseOrthoSize = baseOrthoSize;
        _lastLaneVerticalFramingPixels = laneVerticalFramingPixels;
        _lastFramingBaselineWorldY = framingBaselineWorldY;
        _lastZoomOutCameraLiftPerOrthoUnit = zoomOutCameraLiftPerOrthoUnit;
        _lastZoomInCameraDropPerOrthoUnit = zoomInCameraDropPerOrthoUnit;

        if (Application.isPlaying)
        {
            _sessionBaseOrthoSize = baseOrthoSize;
            _sessionOrthoActive = true;
        }

        if (Application.isPlaying && persistStripLayout && layoutChanged &&
            !s_stripLayoutLockedForExpandBackground)
        {
            CaptureSessionStripLayout(stripHeightPercent, bottomNormalized, leftNormalized, widthNormalized);
            SaveLayoutToPrefs(forceImmediate: false);
        }

        if (layoutChanged)
        {
            if (Application.isPlaying)
                SaveManager.Instance?.NotifyStripZoomChangedDebounced();
            StripLayoutChanged?.Invoke();
        }
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
