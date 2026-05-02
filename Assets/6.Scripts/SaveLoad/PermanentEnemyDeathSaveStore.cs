using System.Collections.Generic;

/// <summary>
/// Tracks <see cref="EnemyDefinition.cannotRespawn"/> spawn slots cleared by death (<see cref="SaveData.permanentDeadEnemySpawnKeys"/>).
/// </summary>
public static class PermanentEnemyDeathSaveStore
{
    private static readonly HashSet<string> DeadKeys = new();

    public static bool IsPermanentlyDead(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        return DeadKeys.Contains(key.Trim());
    }

    public static void MarkPermanentlyDead(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        string k = key.Trim();
        if (!DeadKeys.Add(k))
            return;

        SaveManager.Instance?.Save();
    }

    internal static void ApplyFromSaveData(SaveData data)
    {
        DeadKeys.Clear();

        List<string> list = data?.permanentDeadEnemySpawnKeys;
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            string row = list[i];
            if (string.IsNullOrWhiteSpace(row))
                continue;
            DeadKeys.Add(row.Trim());
        }
    }

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        data.permanentDeadEnemySpawnKeys ??= new List<string>();
        data.permanentDeadEnemySpawnKeys.Clear();

        if (DeadKeys.Count == 0)
            return;

        var sorted = new List<string>(DeadKeys);
        sorted.Sort(string.CompareOrdinal);

        for (int i = 0; i < sorted.Count; i++)
            data.permanentDeadEnemySpawnKeys.Add(sorted[i]);
    }
}
