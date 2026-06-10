using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Loot icon in the database enemy list; shows a full item tooltip on hover.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class DatabaseLootTableEntryUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IItemTooltipHoverSource
{
    [SerializeField] private Image iconImage;

    private ItemDefinition _item;
    private SharedTooltipUI _tooltip;

    private void Awake()
    {
        if (!iconImage)
            iconImage = GetComponent<Image>();
    }

    public void Bind(ItemDefinition item, SharedTooltipUI tooltip)
    {
        _item = item;
        _tooltip = tooltip;

        if (!iconImage)
            iconImage = GetComponent<Image>();

        Sprite sprite = item != null ? item.icon : null;
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        iconImage.raycastTarget = item != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemTooltipHoverRegistry.SetHovered(this, true);
        ShowTooltipIfPossible();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemTooltipHoverRegistry.SetHovered(this, false);
        _tooltip?.Hide();
    }

    public void RefreshTooltipIfHovered()
    {
        ShowTooltipIfPossible();
    }

    private void ShowTooltipIfPossible()
    {
        if (_tooltip == null || _item == null)
            return;

        _tooltip.ShowAt(transform, _item, 1, compact: false);
    }
}
