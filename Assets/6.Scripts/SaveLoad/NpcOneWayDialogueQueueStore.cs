using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// When <see cref="NPCInteractionSettings"/> uses One Way Dialogue Queue with a save id, dialogue advances through a
/// unified queue: element 0 = base dialogue, element 1+ = Additional Conditional Dialogues rows (0-based index + 1).
/// Progress persists per save slot so reload / relog resumes at the correct line.
/// </summary>
public static class NpcOneWayDialogueQueueStore
{
    /// <summary>Base dialogue has been presented (queue index 0).</summary>
    public const int QueueIndexBasePresented = 0;

    /// <summary>Nothing presented yet.</summary>
    public const int QueueIndexNotStarted = -1;

    private static readonly HashSet<string> ConsumedKeys = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Legacy conditional-only highest index (kept in sync on write for older builds).</summary>
    private static readonly Dictionary<string, int> HighestConditionalIndexByKey = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Unified queue index per save id (-1 = not started, 0 = base shown, 1+ = conditional shown).</summary>
    private static readonly Dictionary<string, int> QueueIndexByKey = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>False until save data has been applied at least once this session (prevents first-sighting races).</summary>
    public static bool AreStoresReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ConsumedKeys.Clear();
        HighestConditionalIndexByKey.Clear();
        QueueIndexByKey.Clear();
        AreStoresReady = false;
    }

    internal static void ApplyFromSaveData(SaveData data)
    {
        AreStoresReady = false;
        ConsumedKeys.Clear();
        HighestConditionalIndexByKey.Clear();
        QueueIndexByKey.Clear();

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

                string k = r.saveId.Trim();
                int unified = ResolveUnifiedQueueIndexFromSaveRow(k, r);
                if (unified >= QueueIndexBasePresented)
                    QueueIndexByKey[k] = unified;

                if (r.highestIndex >= 0)
                    HighestConditionalIndexByKey[k] = Mathf.Max(0, r.highestIndex);
            }
        }

        // Legacy: consumed flag without a chain row — at least past base.
        foreach (string k in ConsumedKeys)
        {
            if (!QueueIndexByKey.ContainsKey(k))
                QueueIndexByKey[k] = 1;
        }

        AreStoresReady = true;
    }

    private static int ResolveUnifiedQueueIndexFromSaveRow(string saveId, NpcOneWayDialogueChainProgressRow row)
    {
        if (row.queueIndex >= QueueIndexBasePresented)
            return row.queueIndex;

        if (row.highestIndex >= 0)
            return row.highestIndex + 1;

        if (ConsumedKeys.Contains(saveId))
            return 1;

        return QueueIndexNotStarted;
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

        if (QueueIndexByKey.Count > 0)
        {
            var sorted = new List<string>(QueueIndexByKey.Keys);
            sorted.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count; i++)
            {
                string k = sorted[i];
                int queueIndex = QueueIndexByKey[k];
                int legacyConditional = queueIndex >= 1 ? queueIndex - 1 : QueueIndexNotStarted;
                data.npcOneWayDialogueChainProgressRows.Add(new NpcOneWayDialogueChainProgressRow
                {
                    saveId = k,
                    queueIndex = queueIndex,
                    highestIndex = legacyConditional
                });
            }
        }
    }

    /// <summary>
    /// Unified queue index: -1 = not started, 0 = base dialogue presented, 1+ = that many elements presented
    /// (conditional index = queueIndex - 1 for the last shown conditional).
    /// </summary>
    public static int GetQueueIndex(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            return QueueIndexNotStarted;

        string k = saveId.Trim();
        return QueueIndexByKey.TryGetValue(k, out int v) ? v : QueueIndexNotStarted;
    }

    public static bool IsConsumed(string key) => GetQueueIndex(key) >= QueueIndexBasePresented;

    /// <summary>Highest conditional row index (0-based) that has been presented, or -1 if none.</summary>
    public static int GetHighestConditionalIndexPresented(string saveId)
    {
        int queue = GetQueueIndex(saveId);
        return queue >= 1 ? queue - 1 : QueueIndexNotStarted;
    }

    public static void MarkConsumedAndSave(string key) => EnsureMinimumQueueIndex(key, QueueIndexBasePresented);

    public static void RecordHighestConditionalIndexPresented(string saveId, int conditionalIndex) =>
        SetQueueIndex(saveId, conditionalIndex + 1);

    public static void SetQueueIndex(string saveId, int queueIndex, bool save = true)
    {
        if (string.IsNullOrWhiteSpace(saveId) || queueIndex < QueueIndexBasePresented)
            return;

        string k = saveId.Trim();
        int prev = GetQueueIndex(k);
        if (queueIndex <= prev)
            return;

        QueueIndexByKey[k] = queueIndex;
        if (queueIndex >= QueueIndexBasePresented)
            ConsumedKeys.Add(k);

        if (queueIndex >= 1)
        {
            int cond = queueIndex - 1;
            int old = HighestConditionalIndexByKey.TryGetValue(k, out int ex) ? ex : QueueIndexNotStarted;
            if (cond > old)
                HighestConditionalIndexByKey[k] = cond;
        }

        if (save)
            SaveManager.Instance?.Save();
    }

    /// <summary>Advances queue only when <paramref name="minimumQueueIndex"/> is higher than the saved value.</summary>
    public static void EnsureMinimumQueueIndex(string saveId, int minimumQueueIndex, bool save = true)
    {
        int cur = GetQueueIndex(saveId);
        if (minimumQueueIndex > cur)
            SetQueueIndex(saveId, minimumQueueIndex, save);
    }

    /// <summary>
    /// When the player dies, one-way NPCs with a matching After Death row must not replay base dialogue on reload.
    /// Ensures queue index is at least 0 (base stage passed) for those NPCs.
    /// </summary>
    public static void PrepareOneWayQueuesForPlayerDeath(string diedOnMapNodeId)
    {
        NPCInteractionSettings[] all = Object.FindObjectsByType<NPCInteractionSettings>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            all[i].PrepareOneWayQueueForPlayerDeath(diedOnMapNodeId);
    }
}
