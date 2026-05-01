using System.Collections.Generic;

/// <summary>
/// Tracks one-time level-placed item pickups (<see cref="SaveData.levelItemPickupOnceClaimedKeys"/>).
/// </summary>
public static class LevelItemPickupSaveStore
{
    private static readonly HashSet<string> ClaimedKeys = new();

    public static bool IsClaimed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        return ClaimedKeys.Contains(key.Trim());
    }

    public static void MarkClaimed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        string k = key.Trim();
        if (!ClaimedKeys.Add(k))
            return;

        SaveManager.Instance?.Save();
    }

    internal static void ApplyFromSaveData(SaveData data)
    {
        ClaimedKeys.Clear();

        List<string> list = data?.levelItemPickupOnceClaimedKeys;
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            string row = list[i];
            if (string.IsNullOrWhiteSpace(row))
                continue;
            ClaimedKeys.Add(row.Trim());
        }
    }

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        data.levelItemPickupOnceClaimedKeys ??= new List<string>();
        data.levelItemPickupOnceClaimedKeys.Clear();

        if (ClaimedKeys.Count == 0)
            return;

        var sorted = new List<string>(ClaimedKeys);
        sorted.Sort(string.CompareOrdinal);

        for (int i = 0; i < sorted.Count; i++)
            data.levelItemPickupOnceClaimedKeys.Add(sorted[i]);
    }
}
