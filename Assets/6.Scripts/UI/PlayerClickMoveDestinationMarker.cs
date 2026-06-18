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

    private Transform _markerRoot;
    private SpriteRenderer _markerRenderer;
    private Color _baseColor = Color.white;
    private float _shownTargetX = float.NaN;

    private void Awake()
    {
        if (!player)
            player = GetComponent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (player == null || markerSprite == null || !player.IsClickMoveActive)
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

        float floorBottomY = LaneGroundEffectPlacement.GetLaneFloorTopWorldY() + floorYOffset;
        SnapMarkerSpriteBottomToWorld(worldX, floorBottomY);
    }

    private void SnapMarkerSpriteBottomToWorld(float worldX, float bottomWorldY)
    {
        Transform parent = LaneGroundEffectPlacement.ResolveGroundEffectsRoot()
            ?? LaneGroundEffectPlacement.ResolveLaneFloorTransform();
        if (parent != null && _markerRoot.parent != parent)
            _markerRoot.SetParent(parent, true);

        Vector3 pos = _markerRoot.position;
        pos.x = worldX;
        pos.z = 0f;
        _markerRoot.position = pos;

        float deltaY = bottomWorldY - _markerRenderer.bounds.min.y;
        if (Mathf.Abs(deltaY) > 1e-5f)
            _markerRoot.position += new Vector3(0f, deltaY, 0f);
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

    private void OnDisable() => Hide();
}
