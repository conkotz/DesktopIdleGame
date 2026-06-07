using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Right-click clears the gear placed in the upgrade center slot.</summary>
[DisallowMultipleComponent]
public sealed class UpgradeItemSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private UpgradePageUI page;

    private void Awake()
    {
        if (!page)
            page = GetComponentInParent<UpgradePageUI>(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!page)
            page = GetComponentInParent<UpgradePageUI>(true);
        if (!page)
            return;

        page.ShowSelectedGearTooltip(transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!page)
            page = GetComponentInParent<UpgradePageUI>(true);
        if (!page)
            return;

        page.HideSelectedGearTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (!page)
            page = GetComponentInParent<UpgradePageUI>(true);
        if (!page)
            return;

        page.HideSelectedGearTooltip();
        page.ClearSelectedGear();
        eventData.Use();
    }
}
