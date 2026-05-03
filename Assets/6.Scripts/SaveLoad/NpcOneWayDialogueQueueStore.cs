using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// When <see cref="NPCInteractionSettings"/> uses One Way Dialogue Queue with a save id, any conditional line shown
/// marks that id so the base dialogue is never chosen again for that NPC on this save slot. Also tracks how far down
/// the conditional list has been shown so earlier rows are not re-selected after later ones have activated.
/// </summary>
public static class NpcOneWayDialogueQueueStore
{
    private static readonly HashSet<string> ConsumedKeys = new(System.StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> HighestConditionalIndexByKey = new(System.StringComparer.OrdinalIgnoreCase);

    internal static void ApplyFromSaveData(SaveData data)
    {
        ConsumedKeys.Clear();
        HighestConditionalIndexByKey.Clear();

        List<string> list = data?.npcOneWayConditionalDialogueConsumedKeys;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                string row = list[i];
                if (string.IsNullOrWhiteSpace(row))
                    continue;
                ConsumedKeys.Add(row.Trim());
            }
        }

        List<NpcOneWayDialogueChainProgressRow> rows = data?.npcOneWayDialogueChainProgressRows;
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                NpcOneWayDialogueChainProgressRow r = rows[i];
                if (r == null || string.IsNullOrWhiteSpace(r.saveId))
                    continue;
                HighestConditionalIndexByKey[r.saveId.Trim()] = Mathf.Max(0, r.highestIndex);
            }
        }
    }

    internal static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        if (data.npcOneWayConditionalDialogueConsumedKeys == null)
            data.npcOneWayConditionalDialogueConsumedKeys = new List<string>();
        else
            data.npcOneWayConditionalDialogueConsumedKeys.Clear();

        foreach (string k in ConsumedKeys)
            data.npcOneWayConditionalDialogueConsumedKeys.Add(k);

        if (data.npcOneWayDialogueChainProgressRows == null)
            data.npcOneWayDialogueChainProgressRows = new List<NpcOneWayDialogueChainProgressRow>();
        else
            data.npcOneWayDialogueChainProgressRows.Clear();

        if (HighestConditionalIndexByKey.Count > 0)
        {
            var sorted = new List<string>(HighestConditionalIndexByKey.Keys);
            sorted.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count; i++)
            {
                string k = sorted[i];
                data.npcOneWayDialogueChainProgressRows.Add(new NpcOneWayDialogueChainProgressRow
                {
                    saveId = k,
                    highestIndex = HighestConditionalIndexByKey[k]
                });
            }
        }
    }

    public static bool IsConsumed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        return ConsumedKeys.Contains(key.Trim());
    }

    public static void MarkConsumedAndSave(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        string k = key.Trim();
        if (!ConsumedKeys.Add(k))
            return;

        SaveManager.Instance?.Save();
    }

    /// <summary>
    /// Highest conditional row index (0-based) that has been presented for this one-way id, or -1 if unknown.
    /// Legacy saves: consumed key exists but no chain row → assume index 0 was shown (skip only the first row).
    /// </summary>
    public static int GetHighestConditionalIndexPresented(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            return -1;

        string k = saveId.Trim();
        if (HighestConditionalIndexByKey.TryGetValue(k, out int v))
            return v;

        if (ConsumedKeys.Contains(k))
            return 0;

        return -1;
    }

    public static void RecordHighestConditionalIndexPresented(string saveId, int conditionalIndex)
    {
        if (string.IsNullOrWhiteSpace(saveId) || conditionalIndex < 0)
            return;

        string k = saveId.Trim();
        int prev = HighestConditionalIndexByKey.TryGetValue(k, out int ex) ? ex : -1;
        if (conditionalIndex <= prev)
            return;

        HighestConditionalIndexByKey[k] = conditionalIndex;
        SaveManager.Instance?.Save();
    }
}
