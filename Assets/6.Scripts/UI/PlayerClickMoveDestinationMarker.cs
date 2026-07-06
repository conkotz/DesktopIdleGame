using UnityEngine;

/// <summary>
/// Shows a floor marker at the player's click-to-move destination while they walk there.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(99)]
public class PlayerClickMoveDestinationMarker : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private Sprite markerSprite;
    [Tooltip("Uniform world scale for the floor marker.")]
    [Min(0.01f)]
    [SerializeField] private float markerWorldScale = 0.12f;
    [Tooltip("Extra lift after snapping the sprite bottom (arrow tip) to the lane floor.")]
    [SerializeField] private float floorYOffset = 0f;
    [SerializeField] private int sortingOrder = 45;

    [Header("Pulse")]
    [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float pulseMaxAlpha = 1f;
    [SerializeField, Min(0.05f)] private float pulseFadeSeconds = 0.75f;
    [SerializeField, Min(0f)] private float pulseHoldAtPeakSeconds = 1.5f;

    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private PlayerWorldInteractFocus interactFocus;

    private Transform _markerRoot;
    private SpriteRenderer _markerRenderer;
    private Color _baseColor = Color.white;
    private float _shownTargetX = float.NaN;

    private void Awake()
    {
        if (!player)
            player = GetComponent<PlayerController>();
        if (!combat)
            combat = GetComponent<PlayerCombatController>();
        if (!interactFocus)
            interactFocus = GetComponent<PlayerWorldInteractFocus>();
    }

    private void LateUpdate()
    {
        if (!ShouldShowGroundMarker())
        {
            Hide();
            return;
        }

        float targetX = player.ClickMoveTargetWorldX;
        if (_markerRenderer == null || !_markerRenderer.enabled || Mathf.Abs(targetX - _shownTargetX) > 0.02f)
            ShowAt(targetX);

        TickPulse();
    }

    private void ShowAt(float worldX)
    {
        EnsureMarker();
        if (_markerRenderer == null)
            return;

        _shownTargetX = worldX;
        _markerRenderer.sprite = markerSprite;
        _markerRenderer.sortingOrder = sortingOrder;
        _baseColor = _markerRenderer.color;
        _baseColor.a = pulseMaxAlpha;
        _markerRenderer.enabled = true;

        float scale = Mathf.Max(0.01f, markerWorldScale);
        _markerRoot.localScale = new Vector3(scale, scale, scale);

        float laneLineY = ResolveLaneLineY(worldX) + floorYOffset;
        SnapMarkerSpriteBottomToWorld(worldX, laneLineY);
    }

    private static float ResolveLaneLineY(float worldX)
    {
        if (PlayAreaBounds.TryGetFloorTopYForWorldX(worldX, out float laneY))
            return laneY;

        return LaneGroundEffectPlacement.GetLaneFloorTopWorldY();
    }

    private void SnapMarkerSpriteBottomToWorld(float worldX, float laneLineY)
    {
        Transform parent = LaneGroundEffectPlacement.ResolveGroundEffectsRoot()
            ?? LaneGroundEffectPlacement.ResolveLaneFloorTransform();
        if (parent != null && _markerRoot.parent != parent)
            _markerRoot.SetParent(parent, true);

        float scale = _markerRoot.lossyScale.y;
        float pivotToBottom = markerSprite != null ? -markerSprite.bounds.min.y * scale : 0f;
        _markerRoot.position = new Vector3(worldX, laneLineY + pivotToBottom, 0f);
    }

    private void Hide()
    {
        _shownTargetX = float.NaN;
        if (_markerRenderer == null)
            return;

        _markerRenderer.enabled = false;
        Color c = _markerRenderer.color;
        c.a = pulseMaxAlpha;
        _markerRenderer.color = c;
    }

    private void EnsureMarker()
    {
        if (_markerRoot != null)
            return;

        var go = new GameObject("ClickMoveDestinationMarker");
        _markerRoot = go.transform;
        _markerRenderer = go.AddComponent<SpriteRenderer>();
        _markerRenderer.sortingLayerName = "Foreground";
        _markerRenderer.enabled = false;
    }

    private void TickPulse()
    {
        if (_markerRenderer == null || !_markerRenderer.enabled)
            return;

        float alpha = EvaluatePulseAlpha(
            Time.time,
            pulseMinAlpha,
            pulseMaxAlpha,
            pulseFadeSeconds,
            pulseHoldAtPeakSeconds);

        Color c = _baseColor;
        c.a = alpha;
        _markerRenderer.color = c;
    }

    private static float EvaluatePulseAlpha(
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

    private bool ShouldShowGroundMarker()
    {
        if (player == null || markerSprite == null || !player.IsClickMoveActive)
            return false;

        if (combat != null && combat.GetPrimaryEngagedEnemy() != null)
            return false;

        if (interactFocus != null && interactFocus.CurrentFocusTransform != null)
            return false;

        return true;
    }

    private void OnDisable() => Hide();
}
