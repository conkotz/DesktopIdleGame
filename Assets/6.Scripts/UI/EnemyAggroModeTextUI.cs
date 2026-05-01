using TMPro;
using UnityEngine;

/// <summary>
/// Displays enemy aggro mode for the current active combat map.
/// Clears text for non-combat maps.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Enemy Aggro Mode Text")]
public class EnemyAggroModeTextUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [SerializeField] private string aggressiveText = "Enemies are aggressive";
    [SerializeField] private string calmText = "Enemies are calm";
    [SerializeField] private string calmUntilPlayerAggressiveText = "Enemies are calm";

    private string _lastText;

    private void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;

        Refresh();
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;
    }

    private void OnLevelStarted(MapNodeDefinition _)
    {
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (!label)
            return;

        MapNodeDefinition def = null;
        if (GameplayLevelBootstrapper.Instance != null &&
            GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        else
            def = ActiveLevelContext.Current;

        string text = ResolveText(def);
        if (text == _lastText)
            return;

        _lastText = text;
        label.text = text;
    }

    private string ResolveText(MapNodeDefinition def)
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
}
