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
    private static readonly Color ScalingWarningColor = new(1f, 0.15f, 0.15f, 1f);

    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text scalingWarningLabel;
    [SerializeField] private string fallbackWhenUnknown = "—";

    private string _lastShown;
    private bool _lastWarningVisible;

    private void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();

        EnsureScalingWarningLabel();
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

        bool showWarning = def != null && MapCombatScalingSessionState.NeedsReenterWarningForNode(def.nodeId);
        if (display == _lastShown && showWarning == _lastWarningVisible)
            return;

        _lastShown = display;
        _lastWarningVisible = showWarning;
        label.text = display;
        RefreshWarning(showWarning);
    }

    private void RefreshWarning(bool show)
    {
        if (!scalingWarningLabel)
            return;

        scalingWarningLabel.gameObject.SetActive(show);
        if (!show)
            return;

        scalingWarningLabel.text = MapCombatScalingSessionState.ReenterWarningMessage;
        scalingWarningLabel.color = ScalingWarningColor;
    }

    private void EnsureScalingWarningLabel()
    {
        if (scalingWarningLabel || !label)
            return;

        Transform parent = label.transform.parent;
        if (!parent)
            return;

        Transform existing = parent.Find("ScalingReenterWarning");
        if (existing != null)
        {
            scalingWarningLabel = existing.GetComponent<TMP_Text>();
            return;
        }

        var go = new GameObject("ScalingReenterWarning", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(label.transform.GetSiblingIndex() + 1);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, -82f);
        rt.sizeDelta = new Vector2(620f, 36f);

        scalingWarningLabel = go.AddComponent<TextMeshProUGUI>();
        scalingWarningLabel.font = label.font;
        scalingWarningLabel.fontSharedMaterial = label.fontSharedMaterial;
        scalingWarningLabel.fontSize = 13f;
        scalingWarningLabel.fontStyle = FontStyles.Bold;
        scalingWarningLabel.alignment = TextAlignmentOptions.TopRight;
        scalingWarningLabel.textWrappingMode = TextWrappingModes.Normal;
        scalingWarningLabel.raycastTarget = false;
        scalingWarningLabel.color = ScalingWarningColor;
        go.SetActive(false);
    }
}
