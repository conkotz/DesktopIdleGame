using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Right-click clears the gear placed in the upgrade center slot.</summary>
[DisallowMultipleComponent]
public sealed class UpgradeItemSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private UpgradePageUI page;

    private void Awake()
    {
        if (!page)
            page = GetComponentInParent<UpgradePageUI>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (!page)
            page = GetComponentInParent<UpgradePageUI>(true);
        if (!page)
            return;

        page.ClearSelectedGear();
        eventData.Use();
    }
}
