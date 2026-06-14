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
    [SerializeField] private float popupYOffsetStep = 16f;
    [SerializeField] private float popupXJitter = 8f;
    [SerializeField] private int popupYOffsetCycle = 4;

    public const float LingeringStatusLifetimeSeconds = 1.5f;
    [SerializeField] private float statusPopupSideOffset = 0.35f;
    [Tooltip("World-space Y added at the victim anchor before projecting to screen.")]
    [SerializeField] private float statusPopupYOffset = 0.12f;
    [Tooltip("Extra screen-space Y (pixels) for status labels (Poisoned, Stunned, Blocked, etc.). Negative moves down, below overhead debuff icons.")]
    [SerializeField] private float statusPopupScreenYOffset = -36f;
    [SerializeField] private float statusPopupXJitter = 3f;
    [Tooltip("Extra screen pixels beyond the overhead UI half-width when placing status labels beside the strip bar.")]
    [SerializeField] private float statusPopupBesideOverheadScreenPx = 72f;
    [Tooltip("Vertical screen offset between simultaneous status labels on the same unit (Burnt / Shocked, etc.).")]
    [SerializeField] private float statusPopupStackYOffsetStep = 18f;
    [SerializeField] private float statusPopupStackBatchSeconds = 0.2f;

    [Header("Healing popup (player)")]
    [SerializeField] private float healingPopupSideOffset = 0.32f;
    [SerializeField] private float healingPopupYOffset = 0.18f;
    [SerializeField] private float healingPopupXJitter = 2f;

    [Header("Pooling")]
    [SerializeField, Min(0)] private int popupPoolPrewarmCount = 20;
    [SerializeField, Min(8)] private int maxActivePopups = 48;
    [Tooltip("Caps numeric damage popups per frame so multi-hit channels (Whirlwind) do not GC-spike.")]
    [SerializeField, Min(4)] private int maxNumericPopupSpawnsPerFrame = 12;

    private int _popupSpawnIndex = 0;
    private int _numericPopupSpawnsThisFrame;
    private int _numericPopupSpawnFrame = -1;

    private readonly Stack<FloatingDamageTextUI> _pool = new();
    private readonly List<FloatingDamageTextUI> _active = new();
    private readonly Dictionary<FloatingDamageTextUI, PopupFollowData> _followers = new();
    private readonly Dictionary<int, StatusPopupStackState> _statusPopupStackByAnchorId = new();

    private struct PopupFollowData
    {
        public Vector3 WorldAnchor;
        public Vector2 SpawnJitter;
    }

    private struct StatusPopupStackState
    {
        public int Count;
        public float LastSpawnTime;
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
    }

    internal void Release(FloatingDamageTextUI floater)
    {
        if (!floater)
            return;

        floater.PrepareForPool();
        _followers.Remove(floater);
        _active.Remove(floater);
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

    private void RegisterFollower(FloatingDamageTextUI floater, Vector3 worldAnchor, Vector2 spawnJitter)
    {
        if (!floater)
            return;

        _followers[floater] = new PopupFollowData
        {
            WorldAnchor = worldAnchor,
            SpawnJitter = spawnJitter
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

            Vector2 screenPos = _worldProjectionCamera.WorldToScreenPoint(data.WorldAnchor);
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
        if (ray) ray.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;

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
        bool blocked = false)
    {
        bool isLingeringStatus =
            blocked ||
            kind == FloatingDamageTextUI.PopupDamageKind.Blocked ||
            kind == FloatingDamageTextUI.PopupDamageKind.Immune;

        float xJitter = isLingeringStatus
            ? Random.Range(-statusPopupXJitter, statusPopupXJitter)
            : Random.Range(-popupXJitter, popupXJitter);
        float yOffset = isLingeringStatus
            ? statusPopupScreenYOffset
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

        if (!TryRentFloater(worldPos, new Vector2(xJitter, yOffset), out FloatingDamageTextUI floater))
            return;

        if (kind == FloatingDamageTextUI.PopupDamageKind.Immune)
            floater.InitImmune(direction);
        else if (blocked || kind == FloatingDamageTextUI.PopupDamageKind.Blocked)
            floater.InitBlocked(direction);
        else
            floater.Init(amount, kind, isCrit, isDot, direction);
    }

    /// <summary>Lingering status text (ailments, Blocked, Parry) — pinned in place, no travel arc.</summary>
    public void SpawnLingeringStatus(Vector3 worldPos, string message, Color color, Transform stackAnchor = null)
    {
        float xJitter = Random.Range(-statusPopupXJitter, statusPopupXJitter);
        float yStackOffset = GetStatusLabelScreenYOffset(stackAnchor);

        if (!TryRentFloater(worldPos, new Vector2(xJitter, yStackOffset), out FloatingDamageTextUI floater))
            return;

        floater.InitLingeringStatus(message, color);
    }

    private float GetStatusLabelScreenYOffset(Transform stackAnchor = null) =>
        statusPopupScreenYOffset + ResolveStatusPopupStackYOffset(stackAnchor);

    private float ResolveStatusPopupStackYOffset(Transform stackAnchor)
    {
        if (!stackAnchor)
            return 0f;

        int anchorId = stackAnchor.GetInstanceID();
        float now = Time.time;
        if (!_statusPopupStackByAnchorId.TryGetValue(anchorId, out StatusPopupStackState state)
            || now - state.LastSpawnTime > statusPopupStackBatchSeconds)
        {
            state = new StatusPopupStackState { Count = 0, LastSpawnTime = now };
        }

        float yOffset = state.Count * statusPopupStackYOffsetStep;
        state.Count++;
        state.LastSpawnTime = now;
        _statusPopupStackByAnchorId[anchorId] = state;
        return yOffset;
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
    public void SpawnParry(Vector3 worldPos, Vector3 direction = default, bool riposteLabel = false)
    {
        float xJitter = Random.Range(-statusPopupXJitter, statusPopupXJitter);

        if (!TryRentFloater(worldPos, new Vector2(xJitter, statusPopupScreenYOffset), out FloatingDamageTextUI floater))
            return;

        floater.InitParry(direction, riposteLabel ? "Riposte" : "Parry");
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
            blocked
        );
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
