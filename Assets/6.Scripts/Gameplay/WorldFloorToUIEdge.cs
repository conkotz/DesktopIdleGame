using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional UI-edge alignment for the playable lane, plus a shared floor collider reference for spawns and drops.
/// With <see cref="alignWorldContentToUiEdge"/> off (default), <c>MainLane</c> and spawn points are positioned manually in the editor;
/// only <see cref="FloorTopWorldY"/> / <see cref="FloorCollider"/> are used at runtime.
/// <para>
/// When alignment is on, <see cref="worldRoot"/> (e.g. <c>UILaneAlignment</c>) is moved in Y to match a HUD edge.
/// Vertical framing is handled by <see cref="StripCameraController"/> — not this component.
/// </para>
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class WorldFloorToUIEdge : MonoBehaviour
{
    /// <summary>
    /// First-active instance found this frame. Auto-resolved on demand; cleared when the cached instance
    /// is destroyed/disabled. Use this so other systems (e.g. <c>DropManager</c>) can read the canonical
    /// floor top without doing their own raycasts.
    /// </summary>
    public static WorldFloorToUIEdge Active
    {
        get
        {
            if (s_active == null)
                s_active = FindFirstObjectByType<WorldFloorToUIEdge>(FindObjectsInactive.Include);
            return s_active;
        }
    }
    private static WorldFloorToUIEdge s_active;

    /// <summary>Floor collider configured in the inspector (or auto-resolved at startup). May be null in edit mode.</summary>
    public BoxCollider2D FloorCollider => floorCollider;

    /// <summary>
    /// World-space top Y of the configured <see cref="floorCollider"/>. This is the line the lane visually rests on;
    /// it's what player/NPC feet line up against and is the source of truth for item drop landing positions.
    /// Returns <see cref="float.NaN"/> when no floor collider is wired so callers can fall back to their own logic.
    /// </summary>
    public float FloorTopWorldY => floorCollider ? floorCollider.bounds.max.y : float.NaN;

    private enum RectEdge
    {
        Top,
        Bottom
    }

    [Header("UI Source")]
    [SerializeField] private RectTransform sourceRect;
    [SerializeField] private RectEdge sourceEdge = RectEdge.Top;

    /// <summary>e.g. <c>BotomGameBar</c> — used by strip overlays (NPC dialogue) to stay above the same edge as the lane alignment.</summary>
    public RectTransform HudBarRect => sourceRect;

    /// <summary>Strip camera sampled for hud-edge projection (paired with <see cref="HudBarRect"/>).</summary>
    public Camera AlignmentStripCamera => worldCamera;

    [Tooltip("Positive values place the world floor above the selected UI edge in screen pixels.")]
    [SerializeField] private float sourcePixelOffset;

    [Header("Alignment")]
    [Tooltip(
        "When off, MainLane/spawns stay where you place them in the editor. Floor collider is still used for FloorTopWorldY and drops.")]
    [SerializeField] private bool alignWorldContentToUiEdge;

    [Header("World Target")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Transform that is moved in Y to match the UI edge. Prefer a dedicated child (e.g. UILaneAlignment) that wraps lane + spawns — not only a rename of the spawn-point folder.")]
    [SerializeField] private Transform worldRoot;
    [Tooltip("Floor collider used to measure world-space top; usually on the Floor object under the lane.")]
    [SerializeField] private BoxCollider2D floorCollider;
    [SerializeField] private float worldYOffset;

    [Header("Synced floor visuals")]
    [Tooltip("Decorative grass/floor art (e.g. WorldVisuals/FloorVisuals). Y is pinned to the gameplay floor collider each alignment.")]
    [SerializeField] private Transform floorVisualsRoot;

    [Tooltip("World Y offset from floor collider top to floorVisualsRoot (tune if grass art sits above/below the collider line).")]
    [SerializeField] private float floorVisualWorldOffsetFromFloorTop;

    [Header("Orthographic zoom (strip camera)")]
    [Tooltip(
        "Orthographic half-height where the UI↔floor offset was tuned (e.g. StripCamera at 4). " +
        "If feet drift at min/max zoom while this reference is correct elsewhere, set Alignment Y Per Ortho Unit.")]
    [SerializeField] private float orthoAlignmentReferenceHalfHeight = 4f;

    [Tooltip(
        "Extra world Y added to the sampled HUD edge target: (current ortho half-height − reference ortho) × this value. " +
        "Try small values (e.g. −0.015 to −0.03) if sprites float when zoomed out and sink when zoomed in.")]
    [SerializeField] private float alignmentWorldYOffsetPerOrthoUnitVsReference;

    [Header("Stabilization")]
    [Tooltip("Measured world-Y target must move further than this (from the latched value) before the latch updates. Stops layout micro-jitter on maps with busy HUD (layout groups, tutorial UI, gathering bars). Set 0 to disable.")]
    [SerializeField] private float sourceMeasurementLatchWorld = 0.04f;

    [Header("Runtime Followers")]
    [SerializeField] private bool moveRuntimeActorsWithFloor = true;
    [SerializeField] private bool moveItemDropsWithFloor = true;
    [SerializeField] private bool moveCavesWithFloor = true;

    [Tooltip("Gathering props (trees/rocks) should stay on the lane but not be parented under worldRoot — they're nudged here like enemies to avoid hierarchy/collider jitter.")]
    [SerializeField] private bool moveResourceNodesWithFloor = true;

    [Header("Behaviour")]
    [Tooltip("Leave off in play mode to reduce layout churn that shakes the lane (Canvas.ForceUpdateCanvases). Startup and edit-mode previews still rebuild when needed.")]
    [SerializeField] private bool forceCanvasUpdateEveryFrame;

    [SerializeField] private bool updateContinuously = true;

    [Tooltip("Ignore corrections smaller than this (world units). Layout noise caused tiny deltas to pass when this was ~0.")]
    [SerializeField] private float minMoveDelta = 0.025f;

    [Tooltip("Smooth lane target toward UI edge (Hz). Lower = calmer; use ~6–10 on noisy HUD maps if latch alone is not enough.")]
    [SerializeField] private float alignmentSmoothHz = 10f;

    [Tooltip("When Update Continuously is off: re-run alignment when the source rect.layout moves by more than this (anchored position / size delta).")]
    [SerializeField] private float layoutChangeEpsilonUnits = 0.1f;

    [Tooltip("Caps a single-frame Y correction so a bad measurement or stale bounds cannot explode lane position. 0 disables. Typical 1–4 world units.")]
    [SerializeField] private float maxAlignmentStepWorld = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLaneAlignment;

    [Tooltip("Seconds between diagnostic lines in the Console (ignored if Log Every Apply is on).")]
    [SerializeField] private float debugSampleInterval = 0.25f;

    [Tooltip("Logs every LateUpdate Apply (very spammy). Use to catch single-frame spikes.")]
    [SerializeField] private bool debugLogEveryApply;

    [Tooltip("When skipping move due to Min Move Delta, still log if residual |deltaY| exceeds this (shows borderline oscillation).")]
    [SerializeField] private float debugResidualDeltaLogThreshold = 0.012f;

    [Tooltip("Append BotomGameBar anchoredPosition and sizeDelta for layout diagnosis.")]
    [SerializeField] private bool debugIncludeSourceRectLayout;

    private readonly Vector3[] _corners = new Vector3[4];
    private static readonly HashSet<Transform> s_movedFollowersScratch = new();
    private float _smoothTargetFloorTopY = float.NaN;
    private float _floorTopOffsetFromWorldRootY = float.NaN;

    /// <summary>Latched sampled target (world Y for floor top). Updated when measurement moves beyond <see cref="sourceMeasurementLatchWorld"/>.</summary>
    private float _latchedSourceWorldY = float.NaN;

    /// <summary>Fingerprint when Update Continuously is off so alignment still tracks resolution / HUD layout rebuilds.</summary>
    private int _cachedPixelW;
    private int _cachedPixelH;
    private Vector2 _cachedAnchoredPosition;
    private Vector2 _cachedSizeDelta;

    private bool _appliedOncePlaying;
    private float _lastDebugSampleTime = float.NegativeInfinity;
    private float _lastResidualJitterLogTime = float.NegativeInfinity;

    /// <summary>Logged once when floor collider is not under serialized worldRoot (would break incremental alignment).</summary>
    private bool _loggedFloorHierarchyMismatch;
    private Coroutine _startupAlignRoutine;
    private bool _subscribedStripLayout;

    private void OnEnable()
    {
        s_active = this;
        CacheReferences();
        ResetLatchAndSmooth();
        SnapshotScreenAndSourceFingerprint();
        WorldFloorFollowerRegistry.BootstrapLegacyCaveFollowersOnce();

        if (!alignWorldContentToUiEdge)
            return;

        TrySubscribeStripLayoutChanged();
        Apply(force: true, allowCanvasForce: true);

        if (Application.isPlaying)
        {
            if (_startupAlignRoutine != null)
                StopCoroutine(_startupAlignRoutine);
            _startupAlignRoutine = StartCoroutine(CoDeferredStartupAlign());
        }
    }

    private void OnDisable()
    {
        if (_startupAlignRoutine != null)
        {
            StopCoroutine(_startupAlignRoutine);
            _startupAlignRoutine = null;
        }

        TryUnsubscribeStripLayoutChanged();

        if (s_active == this)
            s_active = null;
        _appliedOncePlaying = false;
    }

    private void OnValidate()
    {
        CacheReferences();
        ResetLatchAndSmooth();
        SnapshotScreenAndSourceFingerprint();
        if (!alignWorldContentToUiEdge)
            return;

        // ForceUpdateCanvases during OnValidate triggers SendMessage on other UI (e.g. layout rows) and spams console warnings.
        Apply(force: true, allowCanvasForce: false);
    }

    private void ResetLatchAndSmooth()
    {
        _latchedSourceWorldY = float.NaN;
        _smoothTargetFloorTopY = float.NaN;
        _floorTopOffsetFromWorldRootY = float.NaN;
    }

    private float _cachedOrthoSize = -1f;
    private Rect _cachedStripPixelRect;

    private void LateUpdate()
    {
        if (!alignWorldContentToUiEdge)
            return;

        bool layoutChanged = TryConsumeLayoutChangeFlags(out bool forceFromLayout);

        if (!updateContinuously)
        {
            if (!Application.isPlaying)
                return;

            if (_appliedOncePlaying && layoutChanged)
                Apply(force: forceFromLayout, allowCanvasForce: forceFromLayout);
            return;
        }

        if (Application.isPlaying && _appliedOncePlaying)
        {
            if (!layoutChanged && !NeedsContinuousRealign())
                return;

            if (layoutChanged)
                ResetLatchAndSmooth();

            Apply(force: layoutChanged, allowCanvasForce: layoutChanged || forceCanvasUpdateEveryFrame);
            return;
        }

        Apply(force: false, allowCanvasForce: false);
    }

    private IEnumerator CoDeferredStartupAlign()
    {
        const int frames = 4;
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            if (!this || !isActiveAndEnabled)
                yield break;

            ResetLatchAndSmooth();
            SnapshotScreenAndSourceFingerprint();
            Apply(force: true, allowCanvasForce: true);
        }

        _startupAlignRoutine = null;
    }

    private void HandleStripLayoutChanged()
    {
        if (!alignWorldContentToUiEdge || !isActiveAndEnabled)
            return;

        ResetLatchAndSmooth();
        SnapshotScreenAndSourceFingerprint();
        Apply(force: true, allowCanvasForce: true);
    }

    private void TrySubscribeStripLayoutChanged()
    {
        if (_subscribedStripLayout)
            return;

        StripCameraController.StripLayoutChanged += HandleStripLayoutChanged;
        _subscribedStripLayout = true;
    }

    private void TryUnsubscribeStripLayoutChanged()
    {
        if (!_subscribedStripLayout)
            return;

        StripCameraController.StripLayoutChanged -= HandleStripLayoutChanged;
        _subscribedStripLayout = false;
    }

    private bool TryConsumeLayoutChangeFlags(out bool forceRealign)
    {
        forceRealign = ScreenOrSourceLayoutChanged() || OrthoSizeChanged() || StripPixelRectChanged();
        return forceRealign;
    }

    private bool StripPixelRectChanged()
    {
        CacheReferences();
        if (!worldCamera)
            return false;

        Rect pr = worldCamera.pixelRect;
        if (ApproximatelyRect(pr, _cachedStripPixelRect))
            return false;

        _cachedStripPixelRect = pr;
        return true;
    }

    private static bool ApproximatelyRect(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }

    private bool NeedsContinuousRealign()
    {
        if (!float.IsNaN(_smoothTargetFloorTopY) && !float.IsNaN(_latchedSourceWorldY) &&
            Mathf.Abs(_smoothTargetFloorTopY - _latchedSourceWorldY) > minMoveDelta * 0.5f)
            return true;

        return false;
    }

    /// <summary>The transform whose Y tracks the UI edge (serialized <see cref="worldRoot"/>), e.g. UILaneAlignment.</summary>
    public Transform WorldContentRoot
    {
        get
        {
            CacheReferences();
            return worldRoot;
        }
    }

    private void CacheReferences()
    {
        if (!worldRoot)
            worldRoot = transform;

        if (!worldCamera)
            worldCamera = Camera.main;

        if (!floorCollider)
        {
            if (worldRoot)
                floorCollider = worldRoot.GetComponentInChildren<BoxCollider2D>(true);
            if (!floorCollider)
                floorCollider = GetComponentInChildren<BoxCollider2D>(true);
        }

        TryAutoResolveFloorVisualsRoot();
    }

    private void TryAutoResolveFloorVisualsRoot()
    {
        if (floorVisualsRoot)
            return;

        GameObject worldVisuals = GameObject.Find("WorldVisuals");
        if (!worldVisuals)
            return;

        Transform floorVisuals = worldVisuals.transform.Find("FloorVisuals");
        floorVisualsRoot = floorVisuals ? floorVisuals : worldVisuals.transform;
    }

    private void SnapshotScreenAndSourceFingerprint()
    {
        _cachedPixelW = Screen.width;
        _cachedPixelH = Screen.height;
        _cachedOrthoSize = worldCamera ? worldCamera.orthographicSize : -1f;
        _cachedStripPixelRect = worldCamera ? worldCamera.pixelRect : default;

        if (sourceRect)
        {
            _cachedAnchoredPosition = sourceRect.anchoredPosition;
            _cachedSizeDelta = sourceRect.sizeDelta;
        }
    }

    private bool OrthoSizeChanged()
    {
        CacheReferences();
        if (!worldCamera)
            return false;

        if (_cachedOrthoSize < 0f)
        {
            _cachedOrthoSize = worldCamera.orthographicSize;
            return true;
        }

        if (Mathf.Approximately(worldCamera.orthographicSize, _cachedOrthoSize))
            return false;

        _cachedOrthoSize = worldCamera.orthographicSize;
        return true;
    }

    private bool ScreenOrSourceLayoutChanged()
    {
        CacheReferences();

        if (Screen.width != _cachedPixelW || Screen.height != _cachedPixelH)
        {
            SnapshotScreenAndSourceFingerprint();
            return true;
        }

        if (!sourceRect)
            return false;

        float e = Mathf.Max(0.0001f, layoutChangeEpsilonUnits);
        Vector2 ap = sourceRect.anchoredPosition;
        Vector2 sd = sourceRect.sizeDelta;

        if (Mathf.Abs(ap.x - _cachedAnchoredPosition.x) > e ||
            Mathf.Abs(ap.y - _cachedAnchoredPosition.y) > e ||
            Mathf.Abs(sd.x - _cachedSizeDelta.x) > e ||
            Mathf.Abs(sd.y - _cachedSizeDelta.y) > e)
        {
            SnapshotScreenAndSourceFingerprint();
            return true;
        }

        return false;
    }

    private void Apply(bool force, bool allowCanvasForce = true)
    {
        if (!alignWorldContentToUiEdge)
            return;

        CacheReferences();

        if (!sourceRect || !worldCamera || !worldRoot || !floorCollider)
            return;

        if (Application.isPlaying &&
            floorCollider.transform != worldRoot &&
            !floorCollider.transform.IsChildOf(worldRoot))
        {
            if (!_loggedFloorHierarchyMismatch)
            {
                _loggedFloorHierarchyMismatch = true;
                Debug.LogError(
                    $"[WorldFloorToUIEdge] floorCollider '{floorCollider.name}' is not under worldRoot '{worldRoot.name}'. " +
                    $"Assign a floor BoxCollider2D descendant of UILaneAlignment/worldRoot, or lane Y will drift.",
                    this);
            }

            return;
        }

        if (Application.isPlaying)
            _appliedOncePlaying = true;

        if (allowCanvasForce && (!Application.isPlaying || force || forceCanvasUpdateEveryFrame))
            Canvas.ForceUpdateCanvases();

        if (!TryGetSourceEdgeWorldY(out float sourceEdgeWorldY))
        {
            if (float.IsNaN(_latchedSourceWorldY))
                return;

            sourceEdgeWorldY = _latchedSourceWorldY - worldYOffset;
        }

        float measuredRaw = sourceEdgeWorldY + worldYOffset;

        if (worldCamera.orthographic)
        {
            float dOrtho =
                worldCamera.orthographicSize - Mathf.Max(0.01f, orthoAlignmentReferenceHalfHeight);
            measuredRaw += alignmentWorldYOffsetPerOrthoUnitVsReference * dOrtho;
        }

        if (force || float.IsNaN(_latchedSourceWorldY))
            _latchedSourceWorldY = measuredRaw;
        else if (sourceMeasurementLatchWorld <= 0f)
            _latchedSourceWorldY = measuredRaw;
        else if (Mathf.Abs(measuredRaw - _latchedSourceWorldY) >= sourceMeasurementLatchWorld)
            _latchedSourceWorldY = measuredRaw;

        float rawTargetTop = _latchedSourceWorldY;
        float targetFloorTopY;

        if (!Application.isPlaying || force || alignmentSmoothHz <= 0f || float.IsNaN(_smoothTargetFloorTopY))
        {
            targetFloorTopY = rawTargetTop;
            _smoothTargetFloorTopY = rawTargetTop;
        }
        else
        {
            float t = 1f - Mathf.Exp(-alignmentSmoothHz * Time.deltaTime);
            _smoothTargetFloorTopY = Mathf.Lerp(_smoothTargetFloorTopY, rawTargetTop, t);
            targetFloorTopY = _smoothTargetFloorTopY;
        }

        float floorTopY = floorCollider.bounds.max.y;
        if (float.IsNaN(_floorTopOffsetFromWorldRootY) || force)
            _floorTopOffsetFromWorldRootY = floorTopY - worldRoot.position.y;

        float targetWorldRootY = targetFloorTopY - _floorTopOffsetFromWorldRootY;

        if (!IsFiniteNumber(floorTopY) || !IsFiniteNumber(targetFloorTopY) || !IsFiniteNumber(targetWorldRootY))
        {
            if (debugLaneAlignment)
                Debug.LogWarning($"[WorldFloorToUIEdge] Non-finite floorTop ({floorTopY}) or target ({targetFloorTopY}); skip apply.", this);
            return;
        }

        float deltaY = targetWorldRootY - worldRoot.position.y;
        float rawDeltaBeforeClamp = deltaY;

        if (maxAlignmentStepWorld > 0f && !force)
            deltaY = Mathf.Clamp(deltaY, -maxAlignmentStepWorld, maxAlignmentStepWorld);

        bool skipDueToMinMove = !force && Mathf.Abs(deltaY) < minMoveDelta;

        DebugLogAlignmentSample(force, measuredRaw, rawTargetTop, targetFloorTopY, floorTopY, deltaY, rawDeltaBeforeClamp, skipDueToMinMove);

        if (skipDueToMinMove)
            return;

        MoveTransformY(worldRoot, deltaY);
        SyncFloorVisualsRootToColliderTop();

        if (Application.isPlaying && moveRuntimeActorsWithFloor)
            MoveRuntimeFollowers(deltaY);

        if (Application.isPlaying)
            Physics2D.SyncTransforms();
    }

    private void DebugLogAlignmentSample(
        bool force,
        float measuredWithOffset,
        float latchedTargetWorldY,
        float smoothedFloorTopTargetY,
        float floorColliderTopY,
        float deltaY,
        float rawDeltaBeforeClamp,
        bool skipDueToMinMove)
    {
        if (!debugLaneAlignment || !Application.isPlaying)
            return;

        float t = Time.unscaledTime;

        if (debugLogEveryApply)
        {
            EmitDebugLine(force, measuredWithOffset, latchedTargetWorldY, smoothedFloorTopTargetY, floorColliderTopY, deltaY, rawDeltaBeforeClamp, skipDueToMinMove);
            return;
        }

        bool residualBuzz = skipDueToMinMove &&
                            Mathf.Abs(deltaY) >= debugResidualDeltaLogThreshold;

        bool periodic = t - _lastDebugSampleTime >= Mathf.Max(0.02f, debugSampleInterval);
        bool residualThrottled = residualBuzz && t - _lastResidualJitterLogTime >= 0.12f;

        if (residualThrottled)
            _lastResidualJitterLogTime = t;
        if (periodic)
            _lastDebugSampleTime = t;

        if (periodic || residualThrottled)
            EmitDebugLine(force, measuredWithOffset, latchedTargetWorldY, smoothedFloorTopTargetY, floorColliderTopY, deltaY, rawDeltaBeforeClamp, skipDueToMinMove);
    }

    private void EmitDebugLine(
        bool force,
        float measuredWithOffset,
        float latchedTargetWorldY,
        float smoothedFloorTopTargetY,
        float floorColliderTopY,
        float deltaY,
        float rawDeltaBeforeClamp,
        bool skipDueToMinMove)
    {
        string layout = "";
        if (debugIncludeSourceRectLayout && sourceRect)
        {
            Vector2 ap = sourceRect.anchoredPosition;
            Vector2 sd = sourceRect.sizeDelta;
            layout = $" barAP=({ap.x:F2},{ap.y:F2}) barSD=({sd.x:F2},{sd.y:F2})";
        }

        string canvasThisFrame = (!Application.isPlaying || force || forceCanvasUpdateEveryFrame) ? "canvasForce" : "canvasNoForce";

        bool clamped = Mathf.Abs(deltaY - rawDeltaBeforeClamp) > 0.0001f && maxAlignmentStepWorld > 0f;
        Debug.Log(
            $"[WorldFloorToUIEdge] frame={Time.frameCount} t={Time.unscaledTime:F2}{(force ? " FORCE" : "")} " +
            $"meas={measuredWithOffset:F5} latchTgt={latchedTargetWorldY:F5} smooth={smoothedFloorTopTargetY:F5} " +
            $"floorTop={floorColliderTopY:F5} dY={deltaY:F5}{(clamped ? $" clampedFrom={rawDeltaBeforeClamp:F5}" : "")} {(skipDueToMinMove ? "SKIP_minDelta" : "APPLY_MOVE")} " +
            $"updCont={updateContinuously} canvasAlways={forceCanvasUpdateEveryFrame} this={canvasThisFrame} followers={moveRuntimeActorsWithFloor} " +
            $"wrY={(worldRoot != null ? worldRoot.position.y : 0f):F5}{layout}",
            this);
    }

    private bool TryGetSourceEdgeWorldY(out float worldY)
    {
        worldY = 0f;
        if (!sourceRect)
            return false;

        sourceRect.GetWorldCorners(_corners);

        float screenX = Mathf.Clamp(
            worldCamera.pixelRect.center.x,
            worldCamera.pixelRect.xMin,
            worldCamera.pixelRect.xMax);

        float screenY = sourceEdge == RectEdge.Top
            ? float.MinValue
            : float.MaxValue;

        for (int i = 0; i < _corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, _corners[i]);
            screenY = sourceEdge == RectEdge.Top
                ? Mathf.Max(screenY, screenPoint.y)
                : Mathf.Min(screenY, screenPoint.y);
        }

        screenY += sourcePixelOffset;
        // Whole screen pixels: cuts sub-pixel jitter from dynamic UI layout when projecting to world Y.
        screenY = Mathf.Round(screenY);

        float planeDistance = Mathf.Abs(worldCamera.transform.position.z - floorCollider.transform.position.z);

        if (worldCamera.orthographic)
        {
            Rect pr = worldCamera.pixelRect;
            float vx = (screenX - pr.xMin) / Mathf.Max(1e-4f, pr.width);
            float vy = (screenY - pr.yMin) / Mathf.Max(1e-4f, pr.height);
            Vector3 w = worldCamera.ViewportToWorldPoint(new Vector3(vx, vy, planeDistance));
            worldY = w.y;
            return true;
        }

        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenX, screenY, planeDistance));
        worldY = worldPoint.y;
        return true;
    }

    private void SyncFloorVisualsRootToColliderTop()
    {
        if (!floorVisualsRoot || !floorCollider)
            return;

        if (floorVisualsRoot == worldRoot || floorVisualsRoot.IsChildOf(worldRoot))
            return;

        float targetY = floorCollider.bounds.max.y + floorVisualWorldOffsetFromFloorTop;
        Vector3 pos = floorVisualsRoot.position;
        if (Mathf.Abs(pos.y - targetY) <= 0.0001f)
            return;

        pos.y = targetY;
        floorVisualsRoot.position = pos;
    }

    private void MoveRuntimeFollowers(float deltaY)
    {
        WorldFloorFollowerRegistry.Category mask = 0;
        if (moveRuntimeActorsWithFloor)
            mask |= WorldFloorFollowerRegistry.Category.Actor;
        if (moveItemDropsWithFloor)
            mask |= WorldFloorFollowerRegistry.Category.ItemDrop;
        if (moveResourceNodesWithFloor)
            mask |= WorldFloorFollowerRegistry.Category.Resource;
        if (moveCavesWithFloor)
            mask |= WorldFloorFollowerRegistry.Category.Cave;

        if (mask == 0)
            return;

        s_movedFollowersScratch.Clear();
        WorldFloorFollowerRegistry.MoveAllOutsideRoot(
            worldRoot,
            deltaY,
            s_movedFollowersScratch,
            MoveTransformY,
            mask);
    }

    private static bool IsFiniteNumber(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    private static void MoveTransformY(Transform target, float deltaY)
    {
        if (!target)
            return;

        Vector3 position = target.position;
        position.y += deltaY;
        target.position = position;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.position = position;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
    }

}
