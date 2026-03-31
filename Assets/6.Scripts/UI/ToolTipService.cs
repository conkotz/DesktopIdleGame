using UnityEngine;

public class TooltipService : MonoBehaviour
{
    [SerializeField] private SharedTooltipUI tooltipUI;

    private RectTransform _rt;

    private void Awake()
    {
        if (!tooltipUI) tooltipUI = GetComponent<SharedTooltipUI>();
        _rt = transform as RectTransform;
    }

    public void ShowAtAnchor(ItemDefinition def, int stackAmount, Transform anchor)
    {
        if (!def || !tooltipUI) return;

        // Move tooltip panel to the anchor position (world position)
        if (_rt && anchor)
            _rt.position = anchor.position;

        tooltipUI.Show(def, stackAmount);
    }

    public void Hide()
    {
        if (tooltipUI) tooltipUI.Hide();
    }
}