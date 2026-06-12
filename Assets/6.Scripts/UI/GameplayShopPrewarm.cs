using System.Collections;
using UnityEngine;

/// <summary>
/// Prewarms shop windows (merchant stock grid + embedded inventory grid) during load.</summary>
public static class GameplayShopPrewarm
{
    public static IEnumerator CoPrewarmAllShops()
    {
        ShopUI[] shops = Object.FindObjectsByType<ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (shops == null || shops.Length == 0)
            yield break;

        Merchant sampleMerchant = FindLargestStockMerchant();

        for (int i = 0; i < shops.Length; i++)
        {
            ShopUI shop = shops[i];
            if (!shop)
                continue;

            yield return shop.CoPrewarmForLoad(sampleMerchant);
        }
    }

    public static Merchant FindLargestStockMerchant()
    {
        Merchant[] merchants =
            Object.FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        Merchant best = null;
        int bestCount = 0;

        for (int i = 0; i < merchants.Length; i++)
        {
            Merchant merchant = merchants[i];
            if (!merchant || merchant.Stock == null)
                continue;

            int count = ShopUI.CountValidStockEntries(merchant, inventory);
            if (count > bestCount)
            {
                bestCount = count;
                best = merchant;
            }
        }

        return best;
    }
}
