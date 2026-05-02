using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public enum UiTooltipSource
{
    /// <summary>Uses <see cref="GameTooltipTexts.TryGetForUiElement"/> with this <c>GameObject</c> name.</summary>
    AutoByGameObjectName = 0,
    /// <summary>No hover tooltip.</summary>
    Disabled = 1,
    /// <summary>Inspector title + body (escape hatch for one-offs).</summary>
    Custom = 2,
}

[DisallowMultipleComponent]
public class UIHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip copy")]
    [Tooltip("Auto: text comes from GameTooltipTexts using this object's name. Custom: use fields below.")]
    [SerializeField] private UiTooltipSource tooltipSource = UiTooltipSource.AutoByGameObjectName;

    [FormerlySerializedAs("tooltipTitle")]
    [SerializeField] private string customTitle = "Info";

    [FormerlySerializedAs("tooltipText")]
    [TextArea(3, 10)]
    [SerializeField] private string customBody;

    [Header("Refs")]
    [SerializeField] private SharedTooltipUI tooltipPanel;

    [Header("Docking")]
    [Tooltip("Optional. Leave Use Shared Tooltip Anchor off so the tooltip parents to this row (same as ailment lines).")]
    [SerializeField] private Transform tooltipAnchor;
    [Tooltip("If on, Tooltip Anchor is used (e.g. one shared dock for the whole window). Off by default so each stat line is its own anchor.")]
    [SerializeField] private bool useSharedTooltipAnchor;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;

    private void Awake()
    {
        if (!tooltipPanel)
            tooltipPanel = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Use the equipment/inventory shared tooltip and auto-resolve copy from <see cref="GameTooltipTexts"/> by GameObject name.
    /// (Avoids picking the HUD tooltip via <see cref="FindFirstObjectByType{T}"/>.)
    /// </summary>
    public void ConfigureForEquipmentStats(SharedTooltipUI panel)
    {
        if (panel)
            tooltipPanel = panel;
        tooltipSource = UiTooltipSource.AutoByGameObjectName;
        useSharedTooltipAnchor = false;
    }

    /// <summary>
    /// Equipment stats row: bind shared tooltip with explicit copy so hover works even when the TMP
    /// <see cref="GameObject"/> name does not match <see cref="GameTooltipTexts"/> keys.
    /// </summary>
    public void ConfigureForEquipmentStatsFixedCopy(SharedTooltipUI panel, string title, string body)
    {
        if (panel)
            tooltipPanel = panel;
        tooltipSource = UiTooltipSource.Custom;
        customTitle = string.IsNullOrEmpty(title) ? "Info" : title;
        customBody = body ?? string.Empty;
        useSharedTooltipAnchor = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!tooltipPanel || tooltipSource == UiTooltipSource.Disabled)
            return;

        string title;
        string body;

        if (tooltipSource == UiTooltipSource.Custom)
        {
            title = customTitle;
            body = customBody;
        }
        else if (!GameTooltipTexts.TryGetForUiElement(gameObject.name, out title, out body))
            return;

        if (string.IsNullOrWhiteSpace(body))
            return;

        var flipper = tooltipPanel.GetComponent<FlipInsideBounds>();
        if (flipper)
        {
            flipper.SetPreferredSide(preferredSide);

            if (tooltipMeasureRect)
            {
                flipper.SetMeasureRect(tooltipMeasureRect);
                flipper.SetHeightRect(tooltipMeasureRect);
            }
        }

        Transform anchor = useSharedTooltipAnchor && tooltipAnchor ? tooltipAnchor : transform;
        tooltipPanel.SetAnchor(anchor);
        tooltipPanel.ShowText(title, body, null, useStatsDisplayHeader: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipPanel?.Hide();
    }

    private void OnDisable()
    {
        tooltipPanel?.Hide();
    }
}
