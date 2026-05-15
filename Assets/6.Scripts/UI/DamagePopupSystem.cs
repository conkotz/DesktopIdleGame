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

    private int _popupSpawnIndex = 0;

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

    public void Spawn(
        Vector3 worldPos,
        int amount,
        FloatingDamageTextUI.PopupDamageKind kind,
        bool isCrit,
        bool isDot,
        Vector3 direction,
        bool blocked = false)
    {
        if (!_worldProjectionCamera)
            ResolveProjectionCameras();

        EnsureDamageFxCanvas();

        RectTransform rectForMath = _popupParentRect ? _popupParentRect : canvasRect;
        if (!popupPrefab || !rectForMath || !_worldProjectionCamera) return;

        Vector2 screenPos = _worldProjectionCamera.WorldToScreenPoint(worldPos);

        Canvas fxCanvas = rectForMath.GetComponent<Canvas>();
        Camera eventCam = fxCanvas && fxCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _rectTransformEventCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectForMath, screenPos, eventCam, out _))
            return;

        float xJitter = Random.Range(-popupXJitter, popupXJitter);
        float yOffset = (_popupSpawnIndex % Mathf.Max(1, popupYOffsetCycle)) * popupYOffsetStep;
        _popupSpawnIndex++;

        var go = Instantiate(popupPrefab, rectForMath);
        var floater = go.GetComponent<FloatingDamageTextUI>();
        if (!floater)
        {
            Destroy(go);
            return;
        }

        floater.BeginWorldAnchorFollow(worldPos, new Vector2(xJitter, yOffset), _worldProjectionCamera, rectForMath, eventCam);

        if (kind == FloatingDamageTextUI.PopupDamageKind.Immune)
            floater.InitImmune(direction);
        else if (blocked || kind == FloatingDamageTextUI.PopupDamageKind.Blocked)
            floater.InitBlocked(direction);
        else
            floater.Init(amount, kind, isCrit, isDot, direction);
    }

    /// <summary>Floating status text (e.g. "Poisoned") using the same overlay canvas as damage numbers.</summary>
    public void SpawnAilmentStatus(Vector3 worldPos, string message, Color color, Vector3 direction)
    {
        if (!_worldProjectionCamera)
            ResolveProjectionCameras();

        EnsureDamageFxCanvas();

        RectTransform rectForMath = _popupParentRect ? _popupParentRect : canvasRect;
        if (!popupPrefab || !rectForMath || !_worldProjectionCamera) return;

        Vector2 screenPos = _worldProjectionCamera.WorldToScreenPoint(worldPos);

        Canvas fxCanvas = rectForMath.GetComponent<Canvas>();
        Camera eventCam = fxCanvas && fxCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _rectTransformEventCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectForMath, screenPos, eventCam, out _))
            return;

        float xJitter = Random.Range(-popupXJitter, popupXJitter);
        float yOffset = (_popupSpawnIndex % Mathf.Max(1, popupYOffsetCycle)) * popupYOffsetStep;
        _popupSpawnIndex++;

        var go = Instantiate(popupPrefab, rectForMath);
        var floater = go.GetComponent<FloatingDamageTextUI>();
        if (!floater)
        {
            Destroy(go);
            return;
        }

        floater.BeginWorldAnchorFollow(worldPos, new Vector2(xJitter, yOffset), _worldProjectionCamera, rectForMath, eventCam);
        floater.InitAilmentStatus(message, color, direction);
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
