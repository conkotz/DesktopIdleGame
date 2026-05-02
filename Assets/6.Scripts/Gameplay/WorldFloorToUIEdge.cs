using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aligns the playable lane to a UI <see cref="RectTransform"/> edge (e.g. strip above the bottom HUD).
/// Assign <see cref="worldRoot"/> to a <b>dedicated child transform</b> (e.g. <c>UILaneAlignment</c>) that contains the
/// lane, floor, and spawn markers — not the whole playfield folder. That way the editor folder (e.g. <c>PlayfieldRoot</c>)
/// stays a stable organizing object while only the content that must track the UI strip moves in Y.
/// <para>
/// <b>Why some maps jitter more:</b> the source rect (<c>BotomGameBar</c>) sits in a Canvas with sibling layout rows.
/// Busy maps add tutorial prompts, quests, timers, etc. Each layout pass can flutter the resolved world edge slightly every frame.
/// </para>
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class WorldFloorToUIEdge : MonoBehaviour
{
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

    [Header("World Target")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Transform that is moved in Y to match the UI edge. Prefer a dedicated child (e.g. UILaneAlignment) that wraps lane + spawns — not only a rename of the spawn-point folder.")]
    [SerializeField] private Transform worldRoot;
    [Tooltip("Floor collider used to measure world-space top; usually on the Floor object under the lane.")]
    [SerializeField] private BoxCollider2D floorCollider;
    [SerializeField] private float worldYOffset;

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
    private float _smoothTargetFloorTopY = float.NaN;

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

    private void OnEnable()
    {
        CacheReferences();
        ResetLatchAndSmooth();
        SnapshotScreenAndSourceFingerprint();
        Apply(force: true, allowCanvasForce: true);
    }

    private void OnDisable()
    {
        _appliedOncePlaying = false;
    }

    private void OnValidate()
    {
        CacheReferences();
        ResetLatchAndSmooth();
        SnapshotScreenAndSourceFingerprint();
        // ForceUpdateCanvases during OnValidate triggers SendMessage on other UI (e.g. layout rows) and spams console warnings.
        Apply(force: true, allowCanvasForce: false);
    }

    private void ResetLatchAndSmooth()
    {
        _latchedSourceWorldY = float.NaN;
        _smoothTargetFloorTopY = float.NaN;
    }

    private void LateUpdate()
    {
        if (!updateContinuously)
        {
            if (!Application.isPlaying)
                return;

            if (_appliedOncePlaying && ScreenOrSourceLayoutChanged())
                Apply(force: true, allowCanvasForce: true);
            return;
        }

        Apply(force: false, allowCanvasForce: true);
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
    }

    private void SnapshotScreenAndSourceFingerprint()
    {
        _cachedPixelW = Screen.width;
        _cachedPixelH = Screen.height;

        if (sourceRect)
        {
            _cachedAnchoredPosition = sourceRect.anchoredPosition;
            _cachedSizeDelta = sourceRect.sizeDelta;
        }
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

        float measuredRaw = GetSourceEdgeWorldY() + worldYOffset;

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

        if (Application.isPlaying)
            Physics2D.SyncTransforms();

        float floorTopY = floorCollider.bounds.max.y;

        if (!IsFiniteNumber(floorTopY) || !IsFiniteNumber(targetFloorTopY))
        {
            if (debugLaneAlignment)
                Debug.LogWarning($"[WorldFloorToUIEdge] Non-finite floorTop ({floorTopY}) or target ({targetFloorTopY}); skip apply.", this);
            return;
        }

        float deltaY = targetFloorTopY - floorTopY;
        float rawDeltaBeforeClamp = deltaY;

        if (maxAlignmentStepWorld > 0f)
            deltaY = Mathf.Clamp(deltaY, -maxAlignmentStepWorld, maxAlignmentStepWorld);

        bool skipDueToMinMove = !force && Mathf.Abs(deltaY) < minMoveDelta;

        DebugLogAlignmentSample(force, measuredRaw, rawTargetTop, targetFloorTopY, floorTopY, deltaY, rawDeltaBeforeClamp, skipDueToMinMove);

        if (skipDueToMinMove)
            return;

        MoveTransformY(worldRoot, deltaY);

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

    private float GetSourceEdgeWorldY()
    {
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
            return w.y;
        }

        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenX, screenY, planeDistance));
        return worldPoint.y;
    }

    private void MoveRuntimeFollowers(float deltaY)
    {
        // Same transform may host multiple follower components; move each root once.
        var moved = new HashSet<Transform>();

        MoveAllByDelta(moved, FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);
        MoveAllByDelta(moved, FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);

        if (moveItemDropsWithFloor)
            MoveAllByDelta(moved, FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);

        if (moveResourceNodesWithFloor)
            MoveAllByDelta(moved, FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);

        // Town interactables (not under worldRoot) must track the same floor/UI alignment as combat actors.
        MoveAllByDelta(moved, FindObjectsByType<Merchant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);
        MoveAllByDelta(moved, FindObjectsByType<StorageClick>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);
        MoveAllByDelta(moved, FindObjectsByType<NPCInteractionSettings>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);
    }

    private void MoveAllByDelta<T>(HashSet<Transform> moved, T[] components, float deltaY) where T : Component
    {
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (!component)
                continue;

            Transform t = component.transform;
            if (t == worldRoot || t.IsChildOf(worldRoot))
                continue;

            if (!moved.Add(t))
                continue;

            MoveTransformY(t, deltaY);
        }
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
