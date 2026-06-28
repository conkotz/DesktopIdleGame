using System;

/// <summary>Log fuel rules shared by furnace smelting and cooking range.</summary>
public static class ProcessingFuelCatalog
{
    public const int MaxFuelLogs = 999;

    private static readonly string[] LogIdsByTier =
    {
        "splitwood_log",
        "hardwood_log",
        "wildwood_log",
        "ember_oak_log",
        "spiritwood_log",
    };

    private static readonly float[] SecondsPerLogByTier =
    {
        5f,
        15f,
        30f,
        45f,
        60f,
    };

    public static bool IsValidFuelLog(string itemId) =>
        TryGetSecondsPerLog(itemId, out _);

    public static bool TryGetSecondsPerLog(string itemId, out float secondsPerLog)
    {
        secondsPerLog = 0f;
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        string key = itemId.Trim().ToLowerInvariant();
        for (int i = 0; i < LogIdsByTier.Length; i++)
        {
            if (!string.Equals(LogIdsByTier[i], key, StringComparison.Ordinal))
                continue;

            secondsPerLog = SecondsPerLogByTier[i];
            return true;
        }

        return false;
    }

    public static int GetTierIndex(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return -1;

        string key = itemId.Trim().ToLowerInvariant();
        for (int i = 0; i < LogIdsByTier.Length; i++)
        {
            if (string.Equals(LogIdsByTier[i], key, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    public static ReadOnlySpan<string> AllLogIds => LogIdsByTier;
}
