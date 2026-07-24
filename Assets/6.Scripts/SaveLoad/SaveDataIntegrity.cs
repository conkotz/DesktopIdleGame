using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defensive fixes for save JSON before write and after read — clamps corrupt vitals/currency,
/// rebuilds inventory rows, and aligns parallel lists without blocking saves.
/// </summary>
public static class SaveDataIntegrity
{
    private const int GoldSoftCeiling = 500_000_000;
    private const int MaxInventorySlots = 512;
    /// <summary>Bounds corrupt counts; allows Main+tab layouts and unlock bonuses past the old uniform 440.</summary>
    private const int MaxStorageSlots = 2048;

    /// <summary>Call after <see cref="SaveManager"/> aggregates a <see cref="SaveData"/> and before writing disk.</summary>
    public static void SanitizeBeforeWrite(SaveData data, string context)
    {
        if (data == null)
            return;

        if (data.furnaceSmelters == null)
            data.furnaceSmelters = new List<SaveData.FurnaceSmelterSave>();
        if (data.cookingStations == null)
            data.cookingStations = new List<SaveData.CookingStationSave>();
        if (data.blacksmithingStations == null)
            data.blacksmithingStations = new List<SaveData.BlacksmithingStationSave>();
        if (data.processingProficiency == null)
            data.processingProficiency = new List<SaveData.ProcessingProficiencySave>();

        RepairParallelLists(data);

        // Only treat non-finite and negative HP as corrupt (-1 is uninitialized / missing PlayerSave snapshot).
        if (float.IsNaN(data.playerCurrentHP) || float.IsInfinity(data.playerCurrentHP) || data.playerCurrentHP < 0f)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] Save '{context}': invalid playerCurrentHP={data.playerCurrentHP}; clamping to 1.");
            data.playerCurrentHP = 1f;
        }

        if (float.IsNaN(data.playerCurrentGuard) || float.IsInfinity(data.playerCurrentGuard))
        {
            Debug.LogWarning($"[SaveDataIntegrity] Save '{context}': invalid playerCurrentGuard (NaN/inf); resetting to -1.");
            data.playerCurrentGuard = -1f;
        }
        else if (data.playerCurrentGuard < -1f)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] Save '{context}': playerCurrentGuard={data.playerCurrentGuard}; resetting to -1.");
            data.playerCurrentGuard = -1f;
        }

        if (float.IsNaN(data.playerCurrentEnergy) || float.IsInfinity(data.playerCurrentEnergy))
            data.playerCurrentEnergy = -1f;
        else if (data.playerCurrentEnergy < -1f)
        {
            Debug.LogWarning($"[SaveDataIntegrity] Save '{context}': playerCurrentEnergy={data.playerCurrentEnergy}; using -1.");
            data.playerCurrentEnergy = -1f;
        }

        if (float.IsNaN(data.playerCurrentMana) || float.IsInfinity(data.playerCurrentMana))
            data.playerCurrentMana = -1f;
        else if (data.playerCurrentMana < -1f)
        {
            Debug.LogWarning($"[SaveDataIntegrity] Save '{context}': playerCurrentMana={data.playerCurrentMana}; using -1.");
            data.playerCurrentMana = -1f;
        }

        if (data.gold < 0)
        {
            Debug.LogWarning($"[SaveDataIntegrity] Save '{context}': gold={data.gold}; clamping to 0.");
            data.gold = 0;
        }
        else if (data.gold > GoldSoftCeiling)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] Save '{context}': gold={data.gold} exceeds soft ceiling {GoldSoftCeiling}; clamping.");
            data.gold = GoldSoftCeiling;
        }

        EnsureInventorySlotsCoherent(data, logTag: $"write:{context}", allowEmptyPadding: false);
        EnsureStorageSlotsCoherent(data, logTag: $"write:{context}", allowEmptyPadding: false);
        SanitizeStorageTabBonusSlots(data, logTag: $"write:{context}");
        TrimGatheringActionBarBlocks(data);
    }

    /// <summary>Call after JSON deserialize (or new game template) and <see cref="SaveManager.NormalizeSaveDataLists"/>.</summary>
    public static void RepairAfterJsonLoad(SaveData data, string context)
    {
        if (data == null)
            return;

        if (data.furnaceSmelters == null)
            data.furnaceSmelters = new List<SaveData.FurnaceSmelterSave>();
        if (data.cookingStations == null)
            data.cookingStations = new List<SaveData.CookingStationSave>();
        if (data.blacksmithingStations == null)
            data.blacksmithingStations = new List<SaveData.BlacksmithingStationSave>();
        if (data.processingProficiency == null)
            data.processingProficiency = new List<SaveData.ProcessingProficiencySave>();

        RepairParallelLists(data);

        if (float.IsNaN(data.playerCurrentHP) || float.IsInfinity(data.playerCurrentHP) || data.playerCurrentHP < 0f)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] Load '{context}': invalid playerCurrentHP={data.playerCurrentHP}; using 0 (will restore to safe HP).");
            data.playerCurrentHP = 0f;
        }
        else if (data.playerCurrentHP == 0f)
        {
            // Keep 0 — PlayerSave promotes to a living value when stats exist.
        }

        if (float.IsNaN(data.playerCurrentGuard) || float.IsInfinity(data.playerCurrentGuard))
            data.playerCurrentGuard = -1f;
        else if (data.playerCurrentGuard < -1f)
            data.playerCurrentGuard = -1f;

        if (float.IsNaN(data.playerCurrentEnergy) || float.IsInfinity(data.playerCurrentEnergy))
            data.playerCurrentEnergy = -1f;
        else if (data.playerCurrentEnergy < -1f)
            data.playerCurrentEnergy = -1f;

        if (float.IsNaN(data.playerCurrentMana) || float.IsInfinity(data.playerCurrentMana))
            data.playerCurrentMana = -1f;
        else if (data.playerCurrentMana < -1f)
            data.playerCurrentMana = -1f;

        if (data.gold < 0)
        {
            Debug.LogWarning($"[SaveDataIntegrity] Load '{context}': gold={data.gold}; clamping to 0.");
            data.gold = 0;
        }
        else if (data.gold > GoldSoftCeiling)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] Load '{context}': gold={data.gold} exceeds soft ceiling; clamping to {GoldSoftCeiling}.");
            data.gold = GoldSoftCeiling;
        }

        EnsureInventorySlotsCoherent(data, logTag: $"load:{context}");
        EnsureStorageSlotsCoherent(data, logTag: $"load:{context}");
        SanitizeStorageTabBonusSlots(data, logTag: $"load:{context}");

        TrimActionBarListsToMin(data);
        TrimGatheringActionBarBlocks(data);
    }

    private static void EnsureInventorySlotsCoherent(SaveData data, string logTag, bool allowEmptyPadding = true)
    {
        if (data.inventorySlots == null)
            data.inventorySlots = new List<SaveData.InventorySlotData>();

        int rawCount = data.inventorySlotCount > 0 ? data.inventorySlotCount : 32;
        int want = Mathf.Clamp(rawCount, 1, MaxInventorySlots);
        // Always write the clamped value back — a positive corrupt count (e.g. 2e9) used to
        // bypass this clamp and make Inventory.LoadFrom allocate until OOM/hang.
        if (data.inventorySlotCount != want)
        {
            if (rawCount > MaxInventorySlots || rawCount <= 0)
            {
                Debug.LogWarning(
                    $"[SaveDataIntegrity] ({logTag}): inventorySlotCount={rawCount} clamped to {want}.");
            }

            data.inventorySlotCount = want;
        }

        ParkOverflowSlotsIntoPendingLoot(data, data.inventorySlots, want, logTag, "inventory");

        if (data.inventorySlots.Count == 0 && want > 0)
        {
            if (!allowEmptyPadding)
                return;

            Debug.LogWarning(
                $"[SaveDataIntegrity] ({logTag}): inventorySlots empty but inventorySlotCount={want}; padding empty slots.");
            for (int i = 0; i < want; i++)
                data.inventorySlots.Add(default);
            return;
        }

        if (data.inventorySlots.Count < want)
        {
            int add = want - data.inventorySlots.Count;
            Debug.LogWarning(
                $"[SaveDataIntegrity] ({logTag}): inventory had {data.inventorySlots.Count} rows, slot count {want}; padding {add}.");
            for (int i = 0; i < add; i++)
                data.inventorySlots.Add(default);
        }
        else if (data.inventorySlots.Count > want)
        {
            data.inventorySlots.RemoveRange(want, data.inventorySlots.Count - want);
        }
    }

    /// <summary>
    /// Corrupt or oversized <see cref="SaveData.storageTabBonusSlots"/> can wrap tab index math
    /// and break deposit/withdraw loops. Clamp each bonus and keep base+bonus under MaxStorageSlots.
    /// </summary>
    private static void SanitizeStorageTabBonusSlots(SaveData data, string logTag)
    {
        const int tabCount = 5; // PlayerStorage.TabCount
        const int mainBase = 88;
        const int nonMainBase = 40;
        int baseTotal = mainBase + nonMainBase * (tabCount - 1);
        int bonusBudget = Mathf.Max(0, MaxStorageSlots - baseTotal);

        if (data.storageTabBonusSlots == null)
            data.storageTabBonusSlots = new List<int>(tabCount);

        while (data.storageTabBonusSlots.Count < tabCount)
            data.storageTabBonusSlots.Add(0);
        if (data.storageTabBonusSlots.Count > tabCount)
            data.storageTabBonusSlots.RemoveRange(tabCount, data.storageTabBonusSlots.Count - tabCount);

        long bonusSum = 0;
        bool changed = false;
        for (int i = 0; i < tabCount; i++)
        {
            int raw = data.storageTabBonusSlots[i];
            int clamped = Mathf.Clamp(raw, 0, MaxStorageSlots);
            if (clamped != raw)
                changed = true;
            data.storageTabBonusSlots[i] = clamped;
            bonusSum += clamped;
        }

        while (bonusSum > bonusBudget)
        {
            int richest = 0;
            for (int i = 1; i < tabCount; i++)
            {
                if (data.storageTabBonusSlots[i] > data.storageTabBonusSlots[richest])
                    richest = i;
            }

            if (data.storageTabBonusSlots[richest] <= 0)
                break;

            data.storageTabBonusSlots[richest]--;
            bonusSum--;
            changed = true;
        }

        if (changed)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] ({logTag}): storageTabBonusSlots clamped to fit MaxStorageSlots={MaxStorageSlots}.");
        }
    }

    private static void EnsureStorageSlotsCoherent(SaveData data, string logTag, bool allowEmptyPadding = true)
    {
        if (data.storageSlots == null)
            data.storageSlots = new List<SaveData.InventorySlotData>();

        int rawCount = data.storageSlotCount > 0 ? data.storageSlotCount : 28;
        int want = Mathf.Clamp(rawCount, 1, MaxStorageSlots);
        if (data.storageSlotCount != want)
        {
            if (rawCount > MaxStorageSlots || rawCount <= 0)
            {
                Debug.LogWarning(
                    $"[SaveDataIntegrity] ({logTag}): storageSlotCount={rawCount} clamped to {want}.");
            }

            data.storageSlotCount = want;
        }

        ParkOverflowSlotsIntoPendingLoot(data, data.storageSlots, want, logTag, "storage");

        if (data.storageSlots.Count == 0 && want > 0)
        {
            if (!allowEmptyPadding)
                return;

            Debug.LogWarning(
                $"[SaveDataIntegrity] ({logTag}): storageSlots empty but storageSlotCount={want}; padding empty slots.");
            for (int i = 0; i < want; i++)
                data.storageSlots.Add(default);
            return;
        }

        if (data.storageSlots.Count < want)
        {
            int add = want - data.storageSlots.Count;
            Debug.LogWarning(
                $"[SaveDataIntegrity] ({logTag}): storage had {data.storageSlots.Count} rows, slot count {want}; padding {add}.");
            for (int i = 0; i < add; i++)
                data.storageSlots.Add(default);
        }
        else if (data.storageSlots.Count > want)
        {
            data.storageSlots.RemoveRange(want, data.storageSlots.Count - want);
        }
    }

    /// <summary>
    /// When a corrupt/oversized slot list is trimmed to the 512 cap, non-empty overflow rows
    /// are moved into pending loot recovery so items are not permanently deleted.
    /// </summary>
    private static void ParkOverflowSlotsIntoPendingLoot(
        SaveData data,
        List<SaveData.InventorySlotData> slots,
        int keepCount,
        string logTag,
        string containerName)
    {
        if (data == null || slots == null || slots.Count <= keepCount)
            return;

        PendingLootRecoveryStore.EnsureLists(data);
        int parked = 0;
        for (int i = keepCount; i < slots.Count; i++)
        {
            SaveData.InventorySlotData row = slots[i];
            if (string.IsNullOrWhiteSpace(row.itemId) || row.amount <= 0)
                continue;

            data.pendingLootRecoveryItemIds.Add(row.itemId.Trim());
            data.pendingLootRecoveryAmounts.Add(row.amount);
            parked++;
        }

        if (parked > 0)
        {
            Debug.LogWarning(
                $"[SaveDataIntegrity] ({logTag}): parked {parked} overflow {containerName} stack(s) beyond slot cap {keepCount} into pending loot.");
        }
    }

    private static void EnsureGatheringBlockLists(SaveData.GatheringActionBarSaveBlock block)
    {
        if (block == null)
            return;

        if (block.slotIndexes == null)
            block.slotIndexes = new List<int>();
        if (block.kinds == null)
            block.kinds = new List<int>();
        if (block.ids == null)
            block.ids = new List<string>();
        if (block.itemAmounts == null)
            block.itemAmounts = new List<int>();
    }

    private static void TrimGatheringActionBarBlocks(SaveData data)
    {
        if (data == null)
            return;

        if (data.actionBarGatherWoodcutting == null)
            data.actionBarGatherWoodcutting = new SaveData.GatheringActionBarSaveBlock();
        if (data.actionBarGatherMining == null)
            data.actionBarGatherMining = new SaveData.GatheringActionBarSaveBlock();
        if (data.actionBarGatherFishing == null)
            data.actionBarGatherFishing = new SaveData.GatheringActionBarSaveBlock();

        EnsureGatheringBlockLists(data.actionBarGatherWoodcutting);
        EnsureGatheringBlockLists(data.actionBarGatherMining);
        EnsureGatheringBlockLists(data.actionBarGatherFishing);

        TrimGatheringBlockParallelLists(data.actionBarGatherWoodcutting, "gatherWood");
        TrimGatheringBlockParallelLists(data.actionBarGatherMining, "gatherMining");
        TrimGatheringBlockParallelLists(data.actionBarGatherFishing, "gatherFishing");
    }

    private static void TrimGatheringBlockParallelLists(SaveData.GatheringActionBarSaveBlock block, string label)
    {
        if (block == null)
            return;

        while (block.itemAmounts.Count < block.ids.Count)
        {
            int i = block.itemAmounts.Count;
            int kind = (block.kinds != null && i < block.kinds.Count) ? block.kinds[i] : -1;
            block.itemAmounts.Add(kind == (int)ActionBarAssignmentKind.Item ? 1 : 0);
        }

        int n = Mathf.Min(
            block.slotIndexes.Count,
            block.kinds.Count,
            block.ids.Count,
            block.itemAmounts.Count);
        if (n == block.slotIndexes.Count &&
            n == block.kinds.Count &&
            n == block.ids.Count &&
            n == block.itemAmounts.Count)
            return;

        Debug.LogWarning(
            $"[SaveDataIntegrity] Gathering action bar '{label}' lists mismatched; trimming to {n}.");

        while (block.slotIndexes.Count > n)
            block.slotIndexes.RemoveAt(block.slotIndexes.Count - 1);
        while (block.kinds.Count > n)
            block.kinds.RemoveAt(block.kinds.Count - 1);
        while (block.ids.Count > n)
            block.ids.RemoveAt(block.ids.Count - 1);
        while (block.itemAmounts.Count > n)
            block.itemAmounts.RemoveAt(block.itemAmounts.Count - 1);
    }

    private static void TrimActionBarListsToMin(SaveData data)
    {
        if (data.actionBarSlotIndexes == null || data.actionBarKinds == null || data.actionBarIds == null || data.actionBarItemAmounts == null)
            return;

        // Migration: older saves never persisted per-slot item counts.
        // Backfill with sensible defaults before length alignment.
        while (data.actionBarItemAmounts.Count < data.actionBarIds.Count)
        {
            int i = data.actionBarItemAmounts.Count;
            int kind = (data.actionBarKinds != null && i < data.actionBarKinds.Count) ? data.actionBarKinds[i] : -1;
            data.actionBarItemAmounts.Add(kind == (int)ActionBarAssignmentKind.Item ? 1 : 0);
        }

        int n = Mathf.Min(
            data.actionBarSlotIndexes.Count,
            data.actionBarKinds.Count,
            data.actionBarIds.Count,
            data.actionBarItemAmounts.Count);
        if (n == data.actionBarSlotIndexes.Count &&
            n == data.actionBarKinds.Count &&
            n == data.actionBarIds.Count &&
            n == data.actionBarItemAmounts.Count)
            return;

        Debug.LogWarning(
            $"[SaveDataIntegrity] Action bar parallel lists mismatched (idx={data.actionBarSlotIndexes.Count}, kind={data.actionBarKinds.Count}, id={data.actionBarIds.Count}, amount={data.actionBarItemAmounts.Count}); trimming to {n}.");

        while (data.actionBarSlotIndexes.Count > n)
            data.actionBarSlotIndexes.RemoveAt(data.actionBarSlotIndexes.Count - 1);
        while (data.actionBarKinds.Count > n)
            data.actionBarKinds.RemoveAt(data.actionBarKinds.Count - 1);
        while (data.actionBarIds.Count > n)
            data.actionBarIds.RemoveAt(data.actionBarIds.Count - 1);
        while (data.actionBarItemAmounts.Count > n)
            data.actionBarItemAmounts.RemoveAt(data.actionBarItemAmounts.Count - 1);
    }

    private static void RepairParallelLists(SaveData data)
    {
        PadOrTrimStringIntLists(data.questProgressIds, data.questProgressAmounts, "questProgress", padValue: 0);
        PadOrTrimStringIntLists(
            data.enduranceTrialNodeIds,
            data.enduranceTrialMaxSelectableTier,
            "enduranceTrials",
            padValue: 1);
        PadOrTrimStringIntLists(
            data.skillChoiceSelectionKeys,
            data.skillChoiceSelectionValues,
            "skillChoiceSelections",
            padValue: 0);
        PadOrTrimStringIntLists(
            data.skillAbilityRowPickKeys,
            data.skillAbilityRowPickValues,
            "skillAbilityRowPicks",
            padValue: 0);
        PadOrTrimStringIntLists(
            data.uiWindowLockKeys,
            data.uiWindowLockLocked,
            "uiWindowLock",
            padValue: 0);

        PlayerMapExitPositionStore.RepairParallelLists(data);
        WorldObjectPositionStore.RepairParallelLists(data);
        TownServiceUnlockStore.EnsureLists(data);
        PendingLootRecoveryStore.EnsureLists(data);
        PadOrTrimStringIntLists(
            data.pendingLootRecoveryItemIds,
            data.pendingLootRecoveryAmounts,
            "pendingLootRecovery",
            padValue: 0);
    }

    /// <summary>Make parallel lists the same length (pad ints or trim excess values).</summary>
    private static void PadOrTrimStringIntLists(List<string> keys, List<int> values, string label, int padValue)
    {
        if (keys == null || values == null)
            return;

        if (keys.Count == values.Count)
            return;

        Debug.LogWarning(
            $"[SaveDataIntegrity] Mismatched {label} lists (keys={keys.Count}, values={values.Count}); padding/trimming.");

        while (values.Count < keys.Count)
            values.Add(padValue);

        while (values.Count > keys.Count)
            values.RemoveAt(values.Count - 1);
    }
}
