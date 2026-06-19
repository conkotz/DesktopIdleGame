using System.Collections.Generic;
using UnityEngine;

public class DamagePopupSystem : MonoBehaviour
{
    public static DamagePopupSystem Instance { get; private set; }

    /// <summary>Prefab used for numeric damage; also exposes ailment presentation colors for HP tint / status labels.</summary>
    public FloatingDamageTextUI PopupPrefab => popupPrefab;

    [Header("Refs")]
    [Tooltip("Fallback / legacy. Popups are parented under a high-sort overlay canvas at runtime so they draw above full-screen HUD canvases.")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform canvasRect;

    /// <summary>Optional override for world→screen. If unset: gameplay camera (Main), or root canvas worldCamera when Screen Space Camera.</summary>
    [SerializeField] private Camera uiCamera;

    [SerializeField] private FloatingDamageTextUI popupPrefab;

    [Tooltip("Draw order for floating damage only. Must be above fullscreen UI (e.g. FullWindowCanvas ~10000).")]
    [SerializeField] private int damageFxCanvasSortOrder = 10060;

    [Header("Spawn Offset")]
    [SerializeField] private float popupYOffsetStep = 4f;
    [SerializeField] private float popupXJitter = 1f;
    [SerializeField] private int popupYOffsetCycle = 2;
    [SerializeField] private float numericSpawnSideOffsetWorld = 0.04f;
    [SerializeField] private float numericSpawnYOffsetWorld = 0.05f;

    public const float LingeringStatusLifetimeSeconds = 1.5f;
    [SerializeField] private float statusPopupSideOffset = 0.35f;
    [Tooltip("World-space Y added at the victim anchor before projecting to screen.")]
    [SerializeField] private float statusPopupYOffset = 0.12f;
    [Tooltip("Extra screen-space Y (pixels) for status labels (Poisoned, Stunned, Blocked, etc.). Negative moves down, below overhead debuff icons.")]
    [SerializeField] private float statusPopupScreenYOffset = -48f;
    [SerializeField] private float statusPopupXJitter = 3f;
    [Tooltip("Extra screen pixels beyond the overhead UI half-width when placing status labels beside the strip bar.")]
    [SerializeField] private float statusPopupBesideOverheadScreenPx = 90f;
    [Tooltip("Additional downward screen offset for player status/effect labels only (negative = lower).")]
    [SerializeField] private float playerStatusPopupScreenYOffset = -10f;
    [Tooltip("Additional horizontal spacing for enemy status/effect labels (pixels).")]
    [SerializeField] private float enemyStatusPopupBesideOverheadExtraScreenPx = 20f;
    [Tooltip("Additional downward offset for enemy status/effect labels only (negative = lower).")]
    [SerializeField] private float enemyStatusPopupScreenYOffset = -14f;
    [Tooltip("Vertical screen offset between simultaneous status labels on the same unit (Burnt / Shocked, etc.).")]
    [SerializeField] private float statusPopupStackYOffsetStep = 18f;

    [Header("Healing popup (player)")]
    [SerializeField] private float healingPopupSideOffset = 0.32f;
    [SerializeField] private float healingPopupYOffset = 0.18f;
    [SerializeField] private float healingPopupXJitter = 2f;

    [Header("Pooling")]
    [SerializeField, Min(0)] private int popupPoolPrewarmCount = 20;
    [SerializeField, Min(8)] private int maxActivePopups = 48;
    [Tooltip("Caps numeric damage popups per frame so multi-hit channels (Whirlwind) do not GC-spike.")]
    [SerializeField, Min(4)] private int maxNumericPopupSpawnsPerFrame = 12;
    [SerializeField, Min(1)] private int maxActiveHitPopupsPerTarget = 4;
    [SerializeField, Min(1)] private int maxActiveDotPopupsPerTarget = 2;
    [SerializeField, Min(0.04f)] private float overflowQuickFadeSeconds = 0.12f;

    private int _popupSpawnIndex = 0;
    private int _numericPopupSpawnsThisFrame;
    private int _numericPopupSpawnFrame = -1;

    private readonly Stack<FloatingDamageTextUI> _pool = new();
    private readonly List<FloatingDamageTextUI> _active = new();
    private readonly Dictionary<FloatingDamageTextUI, PopupFollowData> _followers = new();
    private readonly Dictionary<int, int> _activeLingeringStatusByAnchorId = new();
    private readonly Dictionary<FloatingDamageTextUI, int> _lingeringStatusAnchorByFloater = new();
    private readonly Dictionary<int, TargetPopupState> _targetPopupStates = new();
    private readonly Dictionary<FloatingDamageTextUI, int> _popupOwnerTargetId = new();
    private static readonly List<int> DeadTargetIdsScratch = new(32);

    private struct PopupFollowData
    {
        public Vector3 WorldAnchor;
        public Vector2 SpawnJitter;
        public Transform FollowTarget;
        public Vector3 TargetWorldOffset;
    }

    private sealed class TargetPopupState
    {
        public readonly List<FloatingDamageTextUI> Hits = new(4);
        public readonly List<FloatingDamageTextUI> Dots = new(2);
        public float LastDamageTime;
    }

    private Camera _worldProjectionCamera;
    private Camera _rectTransformEventCamera;

    private RectTransform _popupParentRect;

    private void Awake()
    {
        Instance = this;

        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!canvasRect && canvas) canvasRect = canvas.GetComponent<RectTransform>();

        ResolveProjectionCameras();
        EnsureDamageFxCanvas();
        PrewarmPopupPool();
    }

    private void LateUpdate()
    {
        RefreshFollowerPositions();
        RefreshTargetPopupStates();
    }

    internal void Release(FloatingDamageTextUI floater)
    {
        if (!floater)
            return;

        floater.PrepareForPool();
        _followers.Remove(floater);
        _active.Remove(floater);
        UnregisterLingeringStatusFloater(floater);
        if (_popupOwnerTargetId.TryGetValue(floater, out int ownerId))
        {
            _popupOwnerTargetId.Remove(floater);
            if (_targetPopupStates.TryGetValue(ownerId, out TargetPopupState state))
            {
                state.Hits.Remove(floater);
                state.Dots.Remove(floater);
            }
        }
        floater.gameObject.SetActive(false);
        _pool.Push(floater);
    }

    private void PrewarmPopupPool()
    {
        if (!popupPrefab || popupPoolPrewarmCount <= 0)
            return;

        RectTransform parent = _popupParentRect ? _popupParentRect : canvasRect;
        if (!parent)
            return;

        for (int i = 0; i < popupPoolPrewarmCount; i++)
        {
            FloatingDamageTextUI floater = Instantiate(popupPrefab, parent);
            if (!floater)
                continue;

            floater.gameObject.SetActive(false);
            _pool.Push(floater);
        }
    }

    private FloatingDamageTextUI RentPopup(RectTransform parent)
    {
        FloatingDamageTextUI floater = null;
        while (_pool.Count > 0)
        {
            floater = _pool.Pop();
            if (floater)
                break;
        }

        if (!floater)
        {
            floater = Instantiate(popupPrefab, parent);
            if (!floater)
                return null;
        }
        else
        {
            floater.transform.SetParent(parent, false);
        }

        if (_active.Count >= maxActivePopups)
            ReleaseOldestActive();

        floater.gameObject.SetActive(true);
        _active.Add(floater);
        return floater;
    }

    private void ReleaseOldestActive()
    {
        if (_active.Count == 0)
            return;

        Release(_active[0]);
    }

    private void RegisterFollower(
        FloatingDamageTextUI floater,
        Vector3 worldAnchor,
        Vector2 spawnJitter,
        Transform followTarget = null,
        Vector3 targetWorldOffset = default)
    {
        if (!floater)
            return;

        _followers[floater] = new PopupFollowData
        {
            WorldAnchor = worldAnchor,
            SpawnJitter = spawnJitter,
            FollowTarget = followTarget,
            TargetWorldOffset = targetWorldOffset
        };
    }

    private void RefreshFollowerPositions()
    {
        if (_followers.Count == 0)
            return;

        if (!_worldProjectionCamera)
            ResolveProjectionCameras();

        RectTransform rectForMath = _popupParentRect ? _popupParentRect : canvasRect;
        if (!rectForMath || !_worldProjectionCamera)
            return;

        Canvas fxCanvas = rectForMath.GetComponent<Canvas>();
        Camera eventCam = fxCanvas && fxCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _rectTransformEventCamera
            : null;

        for (int i = 0; i < _active.Count; i++)
        {
            FloatingDamageTextUI floater = _active[i];
            if (!floater || !_followers.TryGetValue(floater, out PopupFollowData data))
                continue;

            Vector3 worldAnchor = data.WorldAnchor;
            if (data.FollowTarget)
                worldAnchor = data.FollowTarget.position + data.TargetWorldOffset;
            Vector2 screenPos = _worldProjectionCamera.WorldToScreenPoint(worldAnchor);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectForMath, screenPos, eventCam, out Vector2 local))
            {
                floater.SetCachedAnchorLocal(local);
            }
        }
    }

    private bool TryRentFloater(Vector3 worldPos, Vector2 spawnJitter, out FloatingDamageTextUI floater)
    {
        floater = null;

        if (!_worldProjectionCamera)
            ResolveProjectionCameras();

        EnsureDamageFxCanvas();

        RectTransform rectForMath = _popupParentRect ? _popupParentRect : canvasRect;
        if (!popupPrefab || !rectForMath || !_worldProjectionCamera)
            return false;

        Vector2 screenPos = _worldProjectionCamera.WorldToScreenPoint(worldPos);

        Canvas fxCanvas = rectForMath.GetComponent<Canvas>();
        Camera eventCam = fxCanvas && fxCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _rectTransformEventCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectForMath, screenPos, eventCam, out _))
            return false;

        floater = RentPopup(rectForMath);
        if (!floater)
            return false;

        floater.BeginWorldAnchorFollow(worldPos, spawnJitter, _worldProjectionCamera, rectForMath, eventCam);
        RegisterFollower(floater, worldPos, spawnJitter);
        return true;
    }

    private void EnsureDamageFxCanvas()
    {
        if (_popupParentRect)
            return;

        Transform existing = transform.Find("DamageFloatingFxCanvas");
        if (existing)
        {
            Canvas c = existing.GetComponent<Canvas>();
            RectTransform rt = existing.GetComponent<RectTransform>();
            if (c && rt)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.overrideSorting = true;
                c.sortingOrder = damageFxCanvasSortOrder;
                StretchFullScreen(rt);
                UnityEngine.UI.GraphicRaycaster existingRay = existing.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (existingRay) existingRay.enabled = false;
                _popupParentRect = rt;
                return;
            }
        }

        var root = new GameObject("DamageFloatingFxCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);

        Canvas overlay = root.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.overrideSorting = true;
        overlay.sortingOrder = damageFxCanvasSortOrder;

        var ray = root.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (ray)
        {
            ray.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
            ray.enabled = false;
        }

        RectTransform ort = root.GetComponent<RectTransform>();
        StretchFullScreen(ort);
        _popupParentRect = ort;
        ort.SetParent(null, false);
        StretchFullScreen(ort);
    }

    private static void StretchFullScreen(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private void ResolveProjectionCameras()
    {
        _worldProjectionCamera = uiCamera;
        if (!_worldProjectionCamera && canvas)
        {
            Canvas root = canvas.rootCanvas;
            if (root && root.renderMode == RenderMode.ScreenSpaceCamera && root.worldCamera)
                _worldProjectionCamera = root.worldCamera;
        }

        if (!_worldProjectionCamera)
            _worldProjectionCamera = Camera.main;

        if (canvas)
        {
            Canvas root = canvas.rootCanvas;
            if (root && root.renderMode == RenderMode.ScreenSpaceOverlay)
                _rectTransformEventCamera = null;
            else if (root && root.worldCamera)
                _rectTransformEventCamera = root.worldCamera;
            else
                _rectTransformEventCamera = _worldProjectionCamera;
        }
        else
            _rectTransformEventCamera = null;
    }

    /// <summary>Normalized world drift victim − dealer (same convention as direct hits and DoT ticks).</summary>
    public static Vector3 GetDriftDirectionForVictim(Transform victim, Transform dealer)
    {
        if (!victim || !dealer) return Vector3.up;
        Vector3 w = victim.position - dealer.position;
        return w.sqrMagnitude > 1e-6f ? w.normalized : Vector3.up;
    }

    /// <summary>Same as <see cref="GetDriftDirectionForVictim(Transform, Transform)"/> but uses a world-space dealer position (e.g. last known pos after the dealer was destroyed).</summary>
    public static Vector3 GetDriftDirectionForVictim(Transform victim, Vector3 dealerWorldPosition)
    {
        if (!victim) return Vector3.up;
        Vector3 w = victim.position - dealerWorldPosition;
        return w.sqrMagnitude > 1e-6f ? w.normalized : Vector3.up;
    }

    /// <summary>World spawn for lingering status labels — on the victim, offset away from the dealer (behind the victim in side view).</summary>
    public static Vector3 GetWorldPosBehindVictim(
        Vector3 victimAnchor,
        Vector3 dealerWorld,
        float sideOffset = float.NaN,
        float yOffset = float.NaN)
    {
        DamagePopupSystem sys = Instance;
        if (float.IsNaN(sideOffset))
            sideOffset = sys != null ? sys.statusPopupSideOffset : 0.35f;
        if (float.IsNaN(yOffset))
            yOffset = sys != null ? sys.statusPopupYOffset : 0.12f;

        float awayFromDealerX = Mathf.Sign(victimAnchor.x - dealerWorld.x);
        if (Mathf.Approximately(awayFromDealerX, 0f))
            awayFromDealerX = 1f;

        return victimAnchor + new Vector3(awayFromDealerX * sideOffset, yOffset, 0f);
    }

    /// <summary>
    /// Horizontal screen side for status text beside overhead UI: dealer on the right → text on the left (-1), and vice versa.
    /// When no dealer is known, uses victim facing: facing right → left of UI (-1), facing left → right of UI (+1).
    /// </summary>
    public static float ResolveStatusPopupSideSign(
        Vector3 victimAnchorPos,
        Vector3 dealerWorld,
        bool hasDealer,
        float? victimFacingDirX)
    {
        if (hasDealer)
        {
            float awayFromDealerX = Mathf.Sign(victimAnchorPos.x - dealerWorld.x);
            return Mathf.Approximately(awayFromDealerX, 0f) ? 1f : awayFromDealerX;
        }

        if (victimFacingDirX.HasValue)
            return victimFacingDirX.Value >= 0f ? -1f : 1f;

        return 0f;
    }

    /// <summary>
    /// Prefer a world anchor beside the victim's strip overhead UI; fall back to legacy behind-victim placement.
    /// </summary>
    public Vector3 ResolveLingeringStatusWorldPos(
        Transform victim,
        Vector3 fallbackAnchorPos,
        Vector3 dealerWorld,
        bool hasDealer,
        float? victimFacingDirX = null)
    {
        if (victim != null && MinionCombatTarget.IsMinionTransform(victim))
            return fallbackAnchorPos;

        float sideSign = ResolveStatusPopupSideSign(fallbackAnchorPos, dealerWorld, hasDealer, victimFacingDirX);
        float pixelOffset = statusPopupBesideOverheadScreenPx;
        if (victim != null && victim.GetComponentInParent<PlayerController>() == null)
            pixelOffset += enemyStatusPopupBesideOverheadExtraScreenPx;

        if (UnitOverheadUI.TryGetStatusPopupWorldPosBesideOverhead(victim, sideSign, pixelOffset, out Vector3 besideOverhead))
            return besideOverhead;

        if (hasDealer)
            return GetWorldPosBehindVictim(fallbackAnchorPos, dealerWorld);

        return fallbackAnchorPos;
    }

    public float GetStatusPopupBesideOverheadScreenPx() => statusPopupBesideOverheadScreenPx;

    public void Spawn(
        Vector3 worldPos,
        int amount,
        FloatingDamageTextUI.PopupDamageKind kind,
        bool isCrit,
        bool isDot,
        Vector3 direction,
        bool blocked = false,
        Transform target = null,
        bool evaded = false)
    {
        bool isLingeringStatus =
            blocked ||
            evaded ||
            kind == FloatingDamageTextUI.PopupDamageKind.Blocked ||
            kind == FloatingDamageTextUI.PopupDamageKind.Evaded ||
            kind == FloatingDamageTextUI.PopupDamageKind.Immune;

        float xJitter = isLingeringStatus
            ? Random.Range(-statusPopupXJitter, statusPopupXJitter)
            : Random.Range(-popupXJitter, popupXJitter);
        float yOffset = isLingeringStatus
            ? AllocateLingeringStatusStackYOffset(ResolveStatusStackAnchor(target))
            : (_popupSpawnIndex % Mathf.Max(1, popupYOffsetCycle)) * popupYOffsetStep;
        if (!isLingeringStatus)
        {
            int frame = Time.frameCount;
            if (frame != _numericPopupSpawnFrame)
            {
                _numericPopupSpawnFrame = frame;
                _numericPopupSpawnsThisFrame = 0;
            }

            if (_numericPopupSpawnsThisFrame >= maxNumericPopupSpawnsPerFrame)
                return;

            _numericPopupSpawnsThisFrame++;
            _popupSpawnIndex++;
        }

        if (!isLingeringStatus && target != null &&
            UnitOverheadUI.NotifyCompactDamage(target, amount, isCrit, kind))
            return;

        if (!isLingeringStatus)
        {
            float sideSign = Mathf.Sign(direction.x);
            if (Mathf.Approximately(sideSign, 0f))
                sideSign = 1f;
            worldPos.x += sideSign * numericSpawnSideOffsetWorld;
            worldPos.y += numericSpawnYOffsetWorld;
        }

        if (!TryRentFloater(worldPos, new Vector2(xJitter, yOffset), out FloatingDamageTextUI floater))
            return;

        if (kind == FloatingDamageTextUI.PopupDamageKind.Immune)
            floater.InitImmune(direction);
        else if (evaded || kind == FloatingDamageTextUI.PopupDamageKind.Evaded)
            floater.InitEvaded(direction);
        else if (blocked || kind == FloatingDamageTextUI.PopupDamageKind.Blocked)
            floater.InitBlocked(direction);
        else
            floater.Init(amount, kind, isCrit, isDot, direction);

        if (isLingeringStatus)
            RegisterLingeringStatusFloater(floater, target);

        if (!isLingeringStatus && target != null)
            TrackNumericPopupForTarget(target, floater, isDot);
    }

    /// <summary>Lingering status text (ailments, Blocked, Parry) — pinned in place, no travel arc.</summary>
    public void SpawnLingeringStatus(Vector3 worldPos, string message, Color color, Transform stackAnchor = null)
    {
        stackAnchor = ResolveStatusStackAnchor(stackAnchor);
        float xJitter = Random.Range(-statusPopupXJitter, statusPopupXJitter);
        float yStackOffset = AllocateLingeringStatusStackYOffset(stackAnchor);

        if (!TryRentFloater(worldPos, new Vector2(xJitter, yStackOffset), out FloatingDamageTextUI floater))
            return;

        floater.InitLingeringStatus(message, color);
        RegisterLingeringStatusFloater(floater, stackAnchor);
    }

    /// <summary>Spawns a lingering status label using <see cref="FloatingDamageTextUI"/> presentation colours.</summary>
    public void SpawnStatusPresentation(Vector3 worldPos, string message, Transform stackAnchor = null)
    {
        Color color = Color.white;
        if (popupPrefab != null && popupPrefab.TryGetStatusPresentationColor(message, out Color resolved))
            color = resolved;

        SpawnLingeringStatus(worldPos, message, color, stackAnchor);
    }

    private float AllocateLingeringStatusStackYOffset(Transform stackAnchor)
    {
        float baseY = GetStatusBaseScreenYOffset(stackAnchor);
        if (!stackAnchor)
            return baseY;

        int anchorId = stackAnchor.GetInstanceID();
        int slot = _activeLingeringStatusByAnchorId.TryGetValue(anchorId, out int count) ? count : 0;
        _activeLingeringStatusByAnchorId[anchorId] = slot + 1;
        return baseY + slot * statusPopupStackYOffsetStep;
    }

    private void RegisterLingeringStatusFloater(FloatingDamageTextUI floater, Transform stackAnchor)
    {
        if (!floater || !stackAnchor)
            return;

        stackAnchor = ResolveStatusStackAnchor(stackAnchor);
        _lingeringStatusAnchorByFloater[floater] = stackAnchor.GetInstanceID();
    }

    private void UnregisterLingeringStatusFloater(FloatingDamageTextUI floater)
    {
        if (!floater || !_lingeringStatusAnchorByFloater.TryGetValue(floater, out int anchorId))
            return;

        _lingeringStatusAnchorByFloater.Remove(floater);
        if (!_activeLingeringStatusByAnchorId.TryGetValue(anchorId, out int count))
            return;

        count = Mathf.Max(0, count - 1);
        if (count <= 0)
            _activeLingeringStatusByAnchorId.Remove(anchorId);
        else
            _activeLingeringStatusByAnchorId[anchorId] = count;
    }

    /// <summary>
    /// Player status popups use <see cref="PlayerController"/> root while overhead UI uses the anchor child —
    /// normalize so Blocked, Bleeding, Parry, etc. share one vertical stack.
    /// </summary>
    public static Transform ResolveStatusStackAnchor(Transform stackAnchor)
    {
        if (!stackAnchor)
            return null;

        PlayerController player = stackAnchor.GetComponentInParent<PlayerController>();
        if (player)
            return player.transform;

        MinionCombatTarget minion = stackAnchor.GetComponentInParent<MinionCombatTarget>();
        if (minion)
            return minion.transform;

        EnemyBaseController enemy = stackAnchor.GetComponentInParent<EnemyBaseController>();
        if (enemy)
            return enemy.transform;

        return stackAnchor;
    }

    private float GetStatusBaseScreenYOffset(Transform stackAnchor = null)
    {
        float y = statusPopupScreenYOffset;
        bool isPlayer = stackAnchor != null && stackAnchor.GetComponentInParent<PlayerController>() != null;
        if (isPlayer)
            y += playerStatusPopupScreenYOffset;
        else
            y += enemyStatusPopupScreenYOffset;
        return y;
    }

    /// <summary>Floating status text (e.g. "Poisoned") using the same overlay canvas as damage numbers.</summary>
    public void SpawnAilmentStatus(Vector3 worldPos, string message, Color color, Transform stackAnchor = null, Vector3 direction = default)
    {
        SpawnLingeringStatus(worldPos, message, color, stackAnchor);
    }

    /// <summary>Green +healing text behind the player, slightly lower than Blocked / Parry and rising vertically.</summary>
    public void SpawnHealingForPlayer(PlayerController player, int amount)
    {
        if (player == null || amount <= 0)
            return;

        DamagePopupAnchor anchor = player.GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 anchorWorld = anchor ? anchor.WorldPos : player.transform.position;

        float facing = player.FacingDirectionX >= 0f ? 1f : -1f;
        Vector3 worldPos = anchorWorld + new Vector3(-facing * healingPopupSideOffset, healingPopupYOffset, 0f);
        float xJitter = Random.Range(-healingPopupXJitter, healingPopupXJitter);

        if (!TryRentFloater(worldPos, new Vector2(xJitter, 0f), out FloatingDamageTextUI floater))
            return;

        floater.InitHealing(amount);
    }

    /// <summary>Parry / Riposte on the player (incoming-damage placement), separate colour from Blocked.</summary>
    public void SpawnParry(Vector3 worldPos, Vector3 direction = default, bool riposteLabel = false, Transform stackAnchor = null)
    {
        stackAnchor = ResolveStatusStackAnchor(stackAnchor);
        float xJitter = Random.Range(-statusPopupXJitter, statusPopupXJitter);
        float yOffset = AllocateLingeringStatusStackYOffset(stackAnchor);

        if (!TryRentFloater(worldPos, new Vector2(xJitter, yOffset), out FloatingDamageTextUI floater))
            return;

        floater.InitParry(direction, riposteLabel ? "Riposte" : "Parry");
        RegisterLingeringStatusFloater(floater, stackAnchor);
    }

    public void Spawn(Vector3 worldPos, int amount, bool isCrit, Vector3 direction, bool blocked)
    {
        Spawn(
            worldPos,
            amount,
            FloatingDamageTextUI.PopupDamageKind.Physical,
            isCrit,
            false,
            direction,
            blocked,
            null
        );
    }

    private void TrackNumericPopupForTarget(Transform target, FloatingDamageTextUI floater, bool isDot)
    {
        if (!target || !floater)
            return;

        int id = target.GetInstanceID();
        if (!_targetPopupStates.TryGetValue(id, out TargetPopupState state))
        {
            state = new TargetPopupState();
            _targetPopupStates[id] = state;
        }

        List<FloatingDamageTextUI> list = isDot ? state.Dots : state.Hits;
        int maxAllowed = isDot ? maxActiveDotPopupsPerTarget : maxActiveHitPopupsPerTarget;
        list.Add(floater);
        _popupOwnerTargetId[floater] = id;
        state.LastDamageTime = Time.unscaledTime;

        while (list.Count > maxAllowed)
        {
            FloatingDamageTextUI oldest = list[0];
            list.RemoveAt(0);
            if (oldest)
                oldest.ExpireQuickly(overflowQuickFadeSeconds);
        }
    }

    private void RefreshTargetPopupStates()
    {
        if (_targetPopupStates.Count == 0)
            return;

        DeadTargetIdsScratch.Clear();
        foreach (var kvp in _targetPopupStates)
        {
            int id = kvp.Key;
            TargetPopupState state = kvp.Value;

            PruneDeadPopups(state.Hits);
            PruneDeadPopups(state.Dots);

            if (state.Hits.Count == 0 && state.Dots.Count == 0)
                DeadTargetIdsScratch.Add(id);
        }

        for (int i = 0; i < DeadTargetIdsScratch.Count; i++)
            _targetPopupStates.Remove(DeadTargetIdsScratch[i]);
    }

    private static void PruneDeadPopups(List<FloatingDamageTextUI> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i])
                list.RemoveAt(i);
        }
    }

    public static Vector3 GetDamagePopupPos(Transform victim, Transform attacker, float xOffset = 0.35f, float yOffset = 1.2f)
    {
        float dir = 0f;

        if (attacker && victim)
            dir = Mathf.Sign(victim.position.x - attacker.position.x);

        if (dir == 0f) dir = 1f;

        return victim.position + new Vector3(dir * xOffset, yOffset, 0f);
    }
}
