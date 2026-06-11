using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Inventory-style item tooltip for database item rows (no loot drop metadata).</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class DatabaseItemIconTooltipUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private ItemDefinition _item;
    private SharedTooltipUI _tooltip;

    public void Bind(ItemDefinition item, SharedTooltipUI tooltip)
    {
        _item = item;
        _tooltip = tooltip;
    }

    public void OnPointerEnter(PointerEventData eventData) => ShowTooltipIfPossible();

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide();

    private void ShowTooltipIfPossible()
    {
        if (_tooltip == null || _item == null)
            return;

        _tooltip.ShowAt(transform, _item, 1, compact: false, itemId: _item.itemId);
    }
}
