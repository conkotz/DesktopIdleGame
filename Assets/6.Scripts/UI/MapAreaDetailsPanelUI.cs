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

    private string _lastLocation;
    private string _lastLocationType;
    private string _lastAggression;
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

        _collapsed = startCollapsed;
        ApplyCollapsedVisualState();
        Refresh();
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;

        if (collapseButton)
            collapseButton.onClick.RemoveListener(ToggleCollapsed);
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
        ApplyCollapsedVisualState();
    }

    public void SetCollapsed(bool collapsed)
    {
        if (_collapsed == collapsed)
            return;

        _collapsed = collapsed;
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
                collapseIcon.sprite = collapsedIcon;
            else if (!_collapsed && expandedIcon)
                collapseIcon.sprite = expandedIcon;
            else
                collapseIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, _collapsed ? 180f : 0f);
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
            ? FormatNodeType(def.nodeType)
            : unknownLocationTypeText;

        _isCombatMap = def != null && def.nodeType == MapNodeType.Combat;
        string aggression = ResolveAggressionText(def);

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

        if (force || aggression != _lastAggression)
        {
            _lastAggression = aggression;
            if (aggressionText)
                aggressionText.text = aggression;
        }

        ApplyCollapsedVisualState();
    }

    private static MapNodeDefinition ResolveActiveMapNodeDefinition()
    {
        if (GameplayLevelBootstrapper.Instance != null &&
            GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition;

        return ActiveLevelContext.Current;
    }

    private string ResolveAggressionText(MapNodeDefinition def)
    {
        if (def == null || def.nodeType != MapNodeType.Combat)
            return string.Empty;

        return def.enemyAggroMode switch
        {
            LevelEnemyAggroMode.Aggressive => aggressiveText,
            LevelEnemyAggroMode.Calm => calmText,
            LevelEnemyAggroMode.CalmUntilPlayerAggressive => calmUntilPlayerAggressiveText,
            _ => string.Empty
        };
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
