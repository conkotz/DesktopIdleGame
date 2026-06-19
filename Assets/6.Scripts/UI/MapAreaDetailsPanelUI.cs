using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centralized controller for the in-game map area details panel.
/// Displays location (with scaling suffix), map node type, and enemy aggression text.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Desktop Idle Game/UI/Map Area Details Panel")]
public class MapAreaDetailsPanelUI : MonoBehaviour
{
    [Header("Labels")]
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private TMP_Text locationTypeText;
    [SerializeField] private TMP_Text aggressionHeaderText;
    [SerializeField] private TMP_Text aggressionText;

    [Header("Collapse")]
    [SerializeField] private Button collapseButton;
    [SerializeField] private Image collapseIcon;
    [SerializeField] private Sprite expandedIcon;
    [SerializeField] private Sprite collapsedIcon;
    [SerializeField] private bool startCollapsed;

    [Header("Fallback Text")]
    [SerializeField] private string unknownLocationText = "—";
    [SerializeField] private string unknownLocationTypeText = "Unknown";
    [SerializeField] private string aggressiveText = "Enemies are aggressive";
    [SerializeField] private string calmText = "Enemies are calm";
    [SerializeField] private string calmUntilPlayerAggressiveText = "Enemies are calm";

    [Header("Aggression Colours")]
    [SerializeField] private Color aggressionColor = new Color32(32, 64, 128, 255);

    /// <summary>Session-scoped collapse preference (resets when the game restarts).</summary>
    private static bool? _sessionCollapsedPreference;

    private string _lastLocation;
    private string _lastLocationType;
    private string _lastAggression;
    private Color _lastAggressionColor;
    private bool _collapsed;
    private bool _isCombatMap;

    private void Awake()
    {
        if (!collapseButton)
            collapseButton = GetComponentInChildren<Button>(true);
    }

    private void OnEnable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;

        if (collapseButton)
            collapseButton.onClick.AddListener(ToggleCollapsed);

        LevelAggroState.AggroPulseTriggered += OnAggroPulseTriggered;

        _collapsed = _sessionCollapsedPreference ?? startCollapsed;
        ApplyCollapsedVisualState();
        Refresh();
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;

        if (collapseButton)
            collapseButton.onClick.RemoveListener(ToggleCollapsed);

        LevelAggroState.AggroPulseTriggered -= OnAggroPulseTriggered;
    }

    private void OnAggroPulseTriggered(string _)
    {
        Refresh(force: true);
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void OnLevelStarted(MapNodeDefinition _)
    {
        Refresh(force: true);
    }

    public void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
        _sessionCollapsedPreference = _collapsed;
        ApplyCollapsedVisualState();
    }

    public void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
            return;

        _collapsed = collapsed;
        _sessionCollapsedPreference = _collapsed;
        ApplyCollapsedVisualState();
    }

    private void ApplyCollapsedVisualState()
    {
        if (locationText)
            locationText.gameObject.SetActive(true);
        if (locationTypeText)
            locationTypeText.gameObject.SetActive(!_collapsed);
        if (aggressionHeaderText)
            aggressionHeaderText.gameObject.SetActive(!_collapsed && _isCombatMap);
        if (aggressionText)
            aggressionText.gameObject.SetActive(!_collapsed && _isCombatMap);

        if (collapseIcon)
        {
            if (_collapsed && collapsedIcon)
            {
                collapseIcon.sprite = collapsedIcon;
                collapseIcon.rectTransform.localEulerAngles = Vector3.zero;
            }
            else if (!_collapsed && expandedIcon)
            {
                collapseIcon.sprite = expandedIcon;
                collapseIcon.rectTransform.localEulerAngles = Vector3.zero;
            }
            else
            {
                // Default sprite points down: up when expanded (active), down when collapsed.
                collapseIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, _collapsed ? 0f : 180f);
            }
        }
    }

    private void Refresh(bool force = false)
    {
        MapNodeDefinition def = ResolveActiveMapNodeDefinition();
        WorldMapProgressManager progress = WorldMapProgressManager.Instance;

        string location = def != null
            ? MapCombatScaling.BuildLocationDisplayName(def, progress)
            : unknownLocationText;

        string locationType = def != null
            ? FormatLocationType(def, progress)
            : unknownLocationTypeText;

        _isCombatMap = def != null && def.nodeType == MapNodeType.Combat;
        ResolveAggressionPresentation(def, out string aggression, out Color aggressionColor);

        if (force || location != _lastLocation)
        {
            _lastLocation = location;
            if (locationText)
                locationText.text = location;
        }

        if (force || locationType != _lastLocationType)
        {
            _lastLocationType = locationType;
            if (locationTypeText)
                locationTypeText.text = locationType;
        }

        if (force || aggression != _lastAggression || aggressionColor != _lastAggressionColor)
        {
            _lastAggression = aggression;
            _lastAggressionColor = aggressionColor;
            if (aggressionHeaderText)
            {
                aggressionHeaderText.text = "Aggression";
                aggressionHeaderText.color = aggressionColor;
            }

            if (aggressionText)
            {
                aggressionText.text = aggression;
                aggressionText.color = aggressionColor;
            }
        }

        ApplyCollapsedVisualState();
    }

    private void ResolveAggressionPresentation(
        MapNodeDefinition def,
        out string aggression,
        out Color color)
    {
        aggression = string.Empty;
        color = aggressionColor;

        if (def == null || def.nodeType != MapNodeType.Combat)
            return;

        switch (def.enemyAggroMode)
        {
            case LevelEnemyAggroMode.Aggressive:
                aggression = aggressiveText;
                break;
            case LevelEnemyAggroMode.Calm:
                aggression = calmText;
                break;
            case LevelEnemyAggroMode.CalmUntilPlayerAggressive:
                bool provoked = LevelAggroState.IsWaveAggroLatched(def);
                aggression = provoked ? aggressiveText : calmUntilPlayerAggressiveText;
                break;
        }
    }

    private static MapNodeDefinition ResolveActiveMapNodeDefinition()
    {
        if (GameplayLevelBootstrapper.Instance != null &&
            GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition;

        return ActiveLevelContext.Current;
    }

    private static string FormatLocationType(MapNodeDefinition def, WorldMapProgressManager progress)
    {
        if (def == null)
            return "Unknown";

        if (def.nodeType == MapNodeType.Combat && def.spawnGroupPlans != null && def.spawnGroupPlans.Count > 0)
        {
            int slider = def.IsMapCombatScalingEnabled()
                ? MapCombatScaling.GetEffectiveSliderValue(def, progress)
                : MapCombatScaling.SliderMin;
            MapEnhancementAggregate enhancements = MapEnhancementService.BuildAggregate(def);
            return MapCombatScaling.BuildCombatLocationTypeRichText(def, slider, enhancements);
        }

        return FormatNodeType(def.nodeType);
    }

    private static string FormatNodeType(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.EnduranceTrial => "Endurance Trial",
            _ => type.ToString()
        };
    }
}
