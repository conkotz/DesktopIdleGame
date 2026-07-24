using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds item stacks that could not fit in inventory/storage (e.g. map-exit sweep, quest rewards)
/// and retries delivery when space opens. Survives scene changes via save snapshot + DDOL flusher.
/// </summary>
public static class PendingLootRecoveryStore
{
    private static readonly List<Entry> Entries = new();

    private struct Entry
    {
        public string ItemId;
        public int Amount;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Entries.Clear();

    public static bool HasPending => Entries.Count > 0;

    public static void Enqueue(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        string id = itemId.Trim();
        for (int i = 0; i < Entries.Count; i++)
        {
            if (!string.Equals(Entries[i].ItemId, id, StringComparison.OrdinalIgnoreCase))
                continue;

            Entry existing = Entries[i];
            existing.Amount = Mathf.Max(0, existing.Amount) + amount;
            Entries[i] = existing;
            PendingLootRecoveryRuntime.EnsureInstance();
            return;
        }

        Entries.Add(new Entry { ItemId = id, Amount = amount });
        PendingLootRecoveryRuntime.EnsureInstance();
    }

    /// <summary>
    /// Tries <see cref="DropManager.Spawn"/>; if the manager is missing or spawn returns false
    /// (no anchor/prefab/etc.), parks the stack so removals never silently destroy items.
    /// </summary>
    /// <returns>True when a world drop was spawned; false when the amount was enqueued.</returns>
    public static bool TrySpawnWorldDropOrEnqueue(
        string itemId,
        int amount,
        Sprite icon,
        string sourceName = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        DropManager dm = DropManager.Instance;
        if (dm != null && dm.Spawn(itemId, amount, icon, sourceName))
            return true;

        Enqueue(itemId, amount);
        return false;
    }

    public static void EnsureLists(SaveData data)
    {
        if (data == null)
            return;

        data.pendingLootRecoveryItemIds ??= new List<string>();
        data.pendingLootRecoveryAmounts ??= new List<int>();
    }

    public static void ApplyFromSaveData(SaveData data)
    {
        Entries.Clear();
        if (data == null)
            return;

        EnsureLists(data);
        int count = Mathf.Min(data.pendingLootRecoveryItemIds.Count, data.pendingLootRecoveryAmounts.Count);
        for (int i = 0; i < count; i++)
        {
            string id = data.pendingLootRecoveryItemIds[i];
            int amount = data.pendingLootRecoveryAmounts[i];
            if (string.IsNullOrWhiteSpace(id) || amount <= 0)
                continue;

            Entries.Add(new Entry { ItemId = id.Trim(), Amount = amount });
        }

        if (Entries.Count > 0)
            PendingLootRecoveryRuntime.EnsureInstance();
    }

    public static void WriteInto(SaveData data)
    {
        if (data == null)
            return;

        EnsureLists(data);
        data.pendingLootRecoveryItemIds.Clear();
        data.pendingLootRecoveryAmounts.Clear();

        for (int i = 0; i < Entries.Count; i++)
        {
            Entry e = Entries[i];
            if (string.IsNullOrWhiteSpace(e.ItemId) || e.Amount <= 0)
                continue;

            data.pendingLootRecoveryItemIds.Add(e.ItemId);
            data.pendingLootRecoveryAmounts.Add(e.Amount);
        }
    }

    public static void CopyFromSnapshot(SaveData target, SaveData source)
    {
        if (target == null)
            return;

        EnsureLists(target);
        target.pendingLootRecoveryItemIds.Clear();
        target.pendingLootRecoveryAmounts.Clear();

        if (source?.pendingLootRecoveryItemIds == null || source.pendingLootRecoveryAmounts == null)
            return;

        int count = Mathf.Min(source.pendingLootRecoveryItemIds.Count, source.pendingLootRecoveryAmounts.Count);
        for (int i = 0; i < count; i++)
        {
            string id = source.pendingLootRecoveryItemIds[i];
            int amount = source.pendingLootRecoveryAmounts[i];
            if (string.IsNullOrWhiteSpace(id) || amount <= 0)
                continue;

            target.pendingLootRecoveryItemIds.Add(id.Trim());
            target.pendingLootRecoveryAmounts.Add(amount);
        }
    }

    /// <summary>
    /// Delivers as much pending loot as inventory then storage can hold.
    /// </summary>
    /// <returns>True when any amount was delivered.</returns>
    public static bool TryFlush(bool logIfBlocked)
    {
        if (Entries.Count == 0)
            return false;

        // Never deliver into a half-hydrated session: map-enhancement / rolled item defs
        // may not be registered yet, and inventory may still be at Awake defaults.
        if (!CanFlushAgainstSaveLifecycle())
            return false;

        Inventory inv = Inventory.ResolvePlayer();
        PlayerStorage storage = UnityEngine.Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (inv == null && storage == null)
            return false;

        var remaining = new List<Entry>(Entries.Count);
        bool deliveredAny = false;
        bool sentAnyToStorage = false;

        for (int i = 0; i < Entries.Count; i++)
        {
            Entry e = Entries[i];
            if (string.IsNullOrWhiteSpace(e.ItemId) || e.Amount <= 0)
                continue;

            // Rolled unique map enhancements must exist in the registry before delivery,
            // otherwise AddPartial parks an unresolvable id that LoadFrom will strip.
            if (MapEnhancementRegistry.IsRuntimeItem(e.ItemId) &&
                !MapEnhancementRegistry.TryGetInstance(e.ItemId, out _))
            {
                remaining.Add(e);
                continue;
            }

            int left = e.Amount;
            if (inv != null)
            {
                int toInv = inv.AddPartial(e.ItemId, left, notifyItemGainPopup: false);
                if (toInv > 0)
                {
                    left -= toInv;
                    deliveredAny = true;
                    SessionTrackerData.EnsureInstance()?.RegisterLootChange("PendingLootRecovery", e.ItemId, toInv);
                }
            }

            if (left > 0 && storage != null)
            {
                int toStorage = storage.TryDepositAmountFromExternal(e.ItemId, left);
                if (toStorage > 0)
                {
                    left -= toStorage;
                    deliveredAny = true;
                    sentAnyToStorage = true;
                    SessionTrackerData.EnsureInstance()?.RegisterLootChange("PendingLootRecovery", e.ItemId, toStorage);
                }
            }

            if (left > 0)
                remaining.Add(new Entry { ItemId = e.ItemId, Amount = left });
        }

        Entries.Clear();
        Entries.AddRange(remaining);

        if (!logIfBlocked || !deliveredAny)
            return deliveredAny;

        if (sentAnyToStorage)
            GameLog.Add("Recovered held loot into storage.", GameLog.LevelAvailableColor);
        else
            GameLog.Add("Recovered held loot into inventory.", GameLog.LevelAvailableColor);

        if (Entries.Count > 0)
        {
            GameLog.Add(
                "Inventory and storage are still full — free space to recover the remaining held loot.",
                GameLog.CannotMessageColor);
        }

        return deliveredAny;
    }

    private static bool CanFlushAgainstSaveLifecycle()
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null)
            return true;

        if (sm.IsApplyingSaveData)
            return false;

        if (!sm.IsGameFullyLoaded)
            return false;

        if (sm.HasUnresolvedPendingLoad)
            return false;

        return true;
    }
}

/// <summary>DDOL ticker that retries <see cref="PendingLootRecoveryStore"/> delivery.</summary>
[DisallowMultipleComponent]
public sealed class PendingLootRecoveryRuntime : MonoBehaviour
{
    private static PendingLootRecoveryRuntime _instance;
    private float _nextFlushUnscaledTime;
    private const float FlushIntervalSeconds = 0.5f;

    public static PendingLootRecoveryRuntime Instance => _instance;

    public static PendingLootRecoveryRuntime EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<PendingLootRecoveryRuntime>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var host = new GameObject(nameof(PendingLootRecoveryRuntime));
        _instance = host.AddComponent<PendingLootRecoveryRuntime>();
        DontDestroyOnLoad(host);
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!PendingLootRecoveryStore.HasPending)
            return;

        if (Time.unscaledTime < _nextFlushUnscaledTime)
            return;

        _nextFlushUnscaledTime = Time.unscaledTime + FlushIntervalSeconds;
        if (PendingLootRecoveryStore.TryFlush(logIfBlocked: false) && SaveManager.Instance != null)
            SaveManager.Instance.NotifyInventoryChangedDebounced();
    }
}
