using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip")]
    [SerializeField] private string tooltipTitle = "Info";

    [TextArea(3, 10)]
    [SerializeField] private string tooltipText;

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
        if (!tooltipPanel || string.IsNullOrWhiteSpace(tooltipText))
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

        // IMPORTANT: use the dedicated stats anchor, not this text object's transform
        Transform anchor = tooltipAnchor ? tooltipAnchor : transform;
        tooltipPanel.SetAnchor(anchor);
        tooltipPanel.ShowText(tooltipTitle, tooltipText);
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