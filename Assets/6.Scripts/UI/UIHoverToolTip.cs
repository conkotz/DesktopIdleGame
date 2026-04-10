using UnityEngine;
using UnityEngine.EventSystems;

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

    [SerializeField] private string customTitle = "Info";

    [TextArea(3, 10)]
    [SerializeField] private string customBody;

    [Header("Refs")]
    [SerializeField] private SharedTooltipUI tooltipPanel;

    [Header("Docking")]
    [SerializeField] private Transform tooltipAnchor;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;

    private void Awake()
    {
        if (!tooltipPanel)
            tooltipPanel = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
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

        Transform anchor = tooltipAnchor ? tooltipAnchor : transform;
        tooltipPanel.SetAnchor(anchor);
        tooltipPanel.ShowText(title, body);
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
