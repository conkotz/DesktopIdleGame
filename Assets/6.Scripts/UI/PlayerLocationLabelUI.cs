using TMPro;
using UnityEngine;

/// <summary>
/// HUD: sets a TextMeshPro to the active map name (with scaling suffix when applicable)
/// from <see cref="GameplayLevelBootstrapper"/> / <see cref="ActiveLevelContext"/>.
/// Attach to the same GameObject as the TMP text (e.g. LocationText).
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Debug/Player Location Label")]
public class PlayerLocationLabelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string fallbackWhenUnknown = "—";

    private string _lastShown;

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

        WorldMapProgressManager progress = WorldMapProgressManager.Instance;
        string display = def != null
            ? MapCombatScaling.BuildLocationDisplayName(def, progress)
            : fallbackWhenUnknown;

        if (display == _lastShown)
            return;

        _lastShown = display;
        label.text = display;
    }
}
