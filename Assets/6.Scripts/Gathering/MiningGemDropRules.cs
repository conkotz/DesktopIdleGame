using System.Collections.Generic;

/// <summary>
/// Uncut gem bonus drops for mining. Bonus Find controls whether a gem drops;
/// Rare Gem Discovery can upgrade an uncommon gem find to a higher-tier gem.
/// </summary>
public static class MiningGemDropRules
{
    private static readonly HashSet<string> UncommonGemIds = new HashSet<string>
    {
        "gem_ruby",
        "gem_sapphire",
        "gem_emerald"
    };

    private static readonly string[] RareGemUpgradePool =
    {
        "gem_amethyst",
        "gem_citrine",
        "gem_quartz",
        "gem_topaz"
    };

    public static bool IsUncutGem(string itemId)
    {
        string key = Normalize(itemId);
        return !string.IsNullOrEmpty(key) && key.StartsWith("gem_");
    }

    public static bool IsUncommonGem(string itemId)
    {
        string key = Normalize(itemId);
        return !string.IsNullOrEmpty(key) && UncommonGemIds.Contains(key);
    }

    /// <summary>
    /// When an uncommon gem bonus drop succeeded, rolls to replace it with a rare-tier gem.
    /// </summary>
    public static bool TryRollRareGemUpgrade(ref string itemId, float upgradeChance)
    {
        if (upgradeChance <= 0f || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!IsUncommonGem(itemId))
            return false;

        if (UnityEngine.Random.value >= UnityEngine.Mathf.Clamp01(upgradeChance))
            return false;

        if (RareGemUpgradePool.Length == 0)
            return false;

        itemId = RareGemUpgradePool[UnityEngine.Random.Range(0, RareGemUpgradePool.Length)];
        return true;
    }

    private static string Normalize(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? null
            : itemId.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}
