using UnityEngine;

public class EquipmentTooltipDockingSetup : MonoBehaviour
{
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    [ContextMenu("Apply Tooltip Docking")]
    public void Apply()
    {
        var slots = GetComponentsInChildren<EquipmentSlotUI>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetTooltipDocking(tooltipHeightRect, preferredSide);
        }
    }
}