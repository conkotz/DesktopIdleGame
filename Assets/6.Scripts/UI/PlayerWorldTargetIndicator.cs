using UnityEngine;

/// <summary>
/// Shows a target icon above the player's current focus: combat enemy, resource node, or pending NPC/merchant interact.
/// Assign <see cref="targetIconSprite"/> on the player and tune <see cref="iconWorldOffset"/> in the Inspector.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerWorldTargetIndicator : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private Sprite targetIconSprite;
    [Tooltip("World-space offset added on top of the resolved anchor (raise/lower the marker).")]
    [SerializeField] private Vector3 iconWorldOffset = new(0f, 0.35f, 0f);
    [Tooltip("Uniform world scale for the target icon (1 = sprite import size).")]
    [Min(0.01f)]
    [SerializeField] private float iconWorldScale = 1f;
    [Tooltip("Extra world height above an enemy's overhead UI (name + HP bar) when positioning the icon.")]
    [SerializeField] private float enemyOverheadIconExtraOffset = 0.15f;
    [SerializeField] private int sortingOrder = 120;
    [Header("Pulse (world icon)")]
    [SerializeField, Range(0f, 1f)] private float iconPulseMinAlpha = 0.05f;
    [SerializeField, Range(0f, 1f)] private float iconPulseMaxAlpha = 1f;
    [SerializeField, Min(0.05f)] private float iconFadeSeconds = 0.75f;
    [SerializeField, Min(0f)] private float iconHoldAtPeakSeconds = 1.5f;

    [Header("Refs (optional)")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private PlayerWorldInteractFocus interactFocus;

    private Transform _iconTransform;
    private SpriteRenderer _iconRenderer;
    private Transform _currentFocusTransform;
    private SimpleHoverHighlight2D _highlightedHover;
    private static int s_lastSyncedIconPositionFrame = -1;
    private static PlayerWorldTargetIndicator s_activeInstance;
    private Color _iconBaseColor = Color.white;

    private void Awake()
    {
        if (!player)
            player = GetComponent<PlayerController>();
        if (!combat)
            combat = GetComponent<PlayerCombatController>();
        if (!interactFocus)
            interactFocus = GetComponent<PlayerWorldInteractFocus>();

        EnsureIconInstance();
    }

    private void OnEnable()
    {
        s_activeInstance = this;

        if (combat != null)
            combat.OnTargetChanged += OnCombatTargetChanged;

        Canvas.willRenderCanvases += OnWillRenderCanvasesSyncIconPosition;
    }

    private void OnDisable()
    {
        if (s_activeInstance == this)
            s_activeInstance = null;

        if (combat != null)
            combat.OnTargetChanged -= OnCombatTargetChanged;

        Canvas.willRenderCanvases -= OnWillRenderCanvasesSyncIconPosition;
        UnitOverheadUI.ClearCombatTargetMarker();
        ClearHoverTargeted();
        HideIcon();
    }

    /// <summary>Enemy overhead stack offsets are applied in <see cref="Canvas.willRenderCanvases"/>; sync after that.</summary>
    private static void OnWillRenderCanvasesSyncIconPosition()
    {
        int frame = Time.frameCount;
        if (frame == s_lastSyncedIconPositionFrame)
            return;

        s_lastSyncedIconPositionFrame = frame;
        s_activeInstance?.SyncIconPositionAfterOverheadStack();
    }

    private void SyncIconPositionAfterOverheadStack()
    {
        if (_currentFocusTransform == null)
            return;

        if (IsEnemyFocus(_currentFocusTransform))
            return;

        if (_iconTransform == null || _iconRenderer == null || !_iconRenderer.enabled)
            return;

        _iconTransform.position = GetIconWorldPosition(_currentFocusTransform);
        ApplyIconScale();
    }

    private static bool IsEnemyFocus(Transform focus) =>
        focus != null && focus.GetComponentInParent<EnemyBaseController>() != null;

    private void OnCombatTargetChanged() => RefreshFocus();

    private void LateUpdate()
    {
        RefreshFocus();
        TickWorldIconPulse();
    }

    private void RefreshFocus()
    {
        Transform focus = ResolveFocusTransform();

        if (focus == _currentFocusTransform && focus != null)
        {
            if (IsEnemyFocus(focus))
                return;

            if (_iconRenderer != null && _iconRenderer.enabled)
                return;
        }

        _currentFocusTransform = focus;
        SyncHoverTargeted(focus);

        if (focus == null || targetIconSprite == null)
        {
            UnitOverheadUI.ClearCombatTargetMarker();
            HideIcon();
            return;
        }

        EnemyBaseController enemy = focus.GetComponentInParent<EnemyBaseController>();
        if (enemy != null)
        {
            UnitOverheadUI.SetCombatTargetMarkerForEnemy(enemy, targetIconSprite, true);
            HideIcon();
            return;
        }

        UnitOverheadUI.ClearCombatTargetMarker();
        EnsureIconInstance();
        if (_iconRenderer == null)
            return;

        _iconRenderer.sprite = targetIconSprite;
        _iconRenderer.sortingOrder = sortingOrder;
        _iconBaseColor = _iconRenderer.color;
        _iconBaseColor.a = 1f;
        _iconRenderer.enabled = true;
        _iconTransform.position = GetIconWorldPosition(focus);
        ApplyIconScale();
    }

    private void ApplyIconScale()
    {
        if (_iconTransform == null)
            return;

        float s = Mathf.Max(0.01f, iconWorldScale);
        _iconTransform.localScale = new Vector3(s, s, s);
    }

    private Transform ResolveFocusTransform()
    {
        if (combat != null)
        {
            EnemyBaseController enemy = combat.GetPrimaryEngagedEnemy();
            if (enemy != null && !IsPlayerSelfFocus(enemy.transform))
                return enemy.transform;
        }

        if (player != null && player.CurrentTarget != null)
        {
            Transform nodeTransform = player.CurrentTarget.transform;
            if (!IsPlayerSelfFocus(nodeTransform))
                return nodeTransform;
        }

        if (interactFocus != null && interactFocus.CurrentFocusTransform != null)
        {
            Transform interactTransform = interactFocus.CurrentFocusTransform;
            if (!IsPlayerSelfFocus(interactTransform))
                return interactTransform;
        }

        return null;
    }

    private bool IsPlayerSelfFocus(Transform focus)
    {
        if (!focus || !player)
            return false;

        Transform playerTransform = player.transform;
        if (focus == playerTransform)
            return true;

        if (focus.IsChildOf(playerTransform) || playerTransform.IsChildOf(focus))
            return true;

        if (focus.GetComponentInParent<PlayerController>() == player)
            return true;

        return false;
    }

    private Vector3 GetIconWorldPosition(Transform focus) =>
        ResolveAnchorWorldPosition(focus) + iconWorldOffset;

    private Vector3 ResolveAnchorWorldPosition(Transform focus)
    {
        if (!focus)
            return Vector3.zero;

        EnemyBaseController enemy = focus.GetComponentInParent<EnemyBaseController>();
        if (enemy != null)
        {
            if (UnitOverheadUI.TryGetWorldPointAboveEnemyOverhead(
                    enemy,
                    enemyOverheadIconExtraOffset,
                    out Vector3 aboveOverhead))
            {
                return aboveOverhead;
            }
        }

        WorldTargetIndicatorAnchor anchor = focus.GetComponentInParent<WorldTargetIndicatorAnchor>();
        if (anchor != null)
            return anchor.GetWorldPosition();

        Collider2D col = focus.GetComponentInChildren<Collider2D>();
        if (col != null)
        {
            Bounds b = col.bounds;
            return new Vector3(b.center.x, b.max.y, focus.position.z);
        }

        SpriteRenderer sr = focus.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Bounds b = sr.bounds;
            return new Vector3(b.center.x, b.max.y, focus.position.z);
        }

        return focus.position + Vector3.up * 1.2f;
    }

    private void EnsureIconInstance()
    {
        if (_iconTransform != null)
            return;

        var go = new GameObject("PlayerTargetIcon");
        _iconTransform = go.transform;
        _iconRenderer = go.AddComponent<SpriteRenderer>();
        _iconRenderer.sortingLayerName = "Foreground";
        _iconRenderer.sortingOrder = sortingOrder;
        _iconRenderer.enabled = false;
    }

    private void HideIcon()
    {
        if (_iconRenderer == null)
            return;

        _iconRenderer.enabled = false;
        Color c = _iconRenderer.color;
        c.a = 1f;
        _iconRenderer.color = c;
    }

    private void TickWorldIconPulse()
    {
        if (_iconRenderer == null || !_iconRenderer.enabled)
            return;

        float alpha = EvaluateIconBreatheAlpha(
            Time.time,
            iconPulseMinAlpha,
            iconPulseMaxAlpha,
            iconFadeSeconds,
            iconHoldAtPeakSeconds);

        Color c = _iconBaseColor;
        c.a = alpha;
        _iconRenderer.color = c;
    }

    private static float EvaluateIconBreatheAlpha(
        float time,
        float minAlpha,
        float maxAlpha,
        float fadeSeconds,
        float holdAtPeakSeconds)
    {
        float minA = Mathf.Clamp01(minAlpha);
        float maxA = Mathf.Clamp01(maxAlpha);
        if (maxA < minA)
            (minA, maxA) = (maxA, minA);

        float fade = Mathf.Max(0.05f, fadeSeconds);
        float hold = Mathf.Max(0f, holdAtPeakSeconds);
        float cycle = fade + hold + fade;
        float t = cycle > 0f ? time % cycle : 0f;

        if (t < fade)
            return Mathf.Lerp(minA, maxA, t / fade);

        t -= fade;
        if (t < hold)
            return maxA;

        t -= hold;
        return Mathf.Lerp(maxA, minA, t / fade);
    }

    private void SyncHoverTargeted(Transform focus)
    {
        SimpleHoverHighlight2D next = null;
        if (focus != null)
            next = focus.GetComponentInParent<SimpleHoverHighlight2D>();

        if (_highlightedHover == next)
            return;

        if (_highlightedHover != null)
            _highlightedHover.SetTargeted(false);

        _highlightedHover = next;
        if (_highlightedHover != null)
            _highlightedHover.SetTargeted(true);
    }

    private void ClearHoverTargeted()
    {
        if (_highlightedHover != null)
            _highlightedHover.SetTargeted(false);
        _highlightedHover = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_iconTransform != null)
            ApplyIconScale();
    }
#endif
}
