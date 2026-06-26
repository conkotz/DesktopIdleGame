/// <summary>Shared shop cost labels for single and bulk (50x) purchases.</summary>
public static class ShopCostFormatter
{
    public const int BulkPurchaseQuantity = 50;

    public static bool ShouldShowBulkCost(int stockQuantity) =>
        stockQuantity < 0 || stockQuantity > BulkPurchaseQuantity;

    public static string FormatCompactGold(int amount, bool showBulk)
    {
        if (amount <= 0)
            return "Free";

        if (!showBulk)
            return $"{amount}g";

        int bulk = amount * BulkPurchaseQuantity;
        return $"{amount}g ({bulk}g x{BulkPurchaseQuantity})";
    }

    public static string FormatTooltipGold(int amount, bool showBulk)
    {
        if (amount <= 0)
            return "• Free";

        if (!showBulk)
            return $"• {amount} Gold";

        int bulk = amount * BulkPurchaseQuantity;
        return $"• {amount} Gold ({bulk} Gold x{BulkPurchaseQuantity})";
    }

    public static string FormatItemCost(int amount, string itemName, bool showBulk)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return "";

        if (!showBulk)
            return $"{amount} {itemName}";

        int bulk = amount * BulkPurchaseQuantity;
        return $"{amount} {itemName} ({bulk} {itemName} x{BulkPurchaseQuantity})";
    }

    public static string FormatTooltipItemCost(int amount, string itemName, bool showBulk)
    {
        string line = FormatItemCost(amount, itemName, showBulk);
        return string.IsNullOrEmpty(line) ? "" : $"• {line}";
    }
}
