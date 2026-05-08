using TMPro;
using UnityEngine;

/// <summary>
/// Shows the current map/level name. While the level-select page is open, uses the highlighted node
/// (<see cref="LevelSelectPageUI.HudPreviewSelection"/>); otherwise the active session
/// (<see cref="GameplayLevelBootstrapper"/> / <see cref="ActiveLevelContext"/>).
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Active Map Display")]
public class ActiveMapDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("e.g. \"Map: {0}\" or \"Location: {0}\"")]
    [SerializeField] private string format = "Map: {0}";

    [SerializeField] private string fallbackWhenUnknown = "—";

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

        MapNodeDefinition preview = LevelSelectSharedState.HudPreviewSelection;
        MapNodeDefinition def = preview;
        if (!def)
        {
            if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
                def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
            else
                def = ActiveLevelContext.Current;
        }

        string name = def ? def.displayName : fallbackWhenUnknown;
        string text = string.Format(format, name);
        if (text == _lastText)
            return;

        _lastText = text;
        label.text = text;
    }
}
