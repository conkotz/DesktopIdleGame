using System.Collections.Generic;

/// <summary>Builds context menu entries for merchant stock slots.</summary>
public static class ShopContextMenuBuilder
{
    public static List<ContextMenuEntry> BuildForShopSlot(ShopSlotUI slot)
    {
        var entries = new List<ContextMenuEntry>(2);
        if (!slot || slot.Definition == null || slot.Entry == null)
            return entries;

        bool inStock = slot.IsInStock;

        entries.Add(new ContextMenuEntry("Buy 1", slot.PerformBuy1Action, disabled: !inStock));
        entries.Add(new ContextMenuEntry("Buy 50", slot.PerformBuy50Action, disabled: !inStock));
        return entries;
    }
}
