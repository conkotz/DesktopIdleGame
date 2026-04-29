using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Runtime-only game log. Entries are kept in memory across scene changes, but are intentionally not saved.
/// </summary>
public static class GameLog
{
    public const int MaxEntries = 150;

    public readonly struct Entry
    {
        public readonly string Message;
        public readonly Color Color;
        public readonly System.DateTime TimestampLocal;
        public readonly string StackKey;
        public readonly int StackAmount;
        public readonly int RepeatCount;

        public Entry(
            string message,
            Color color,
            System.DateTime timestampLocal,
            string stackKey = "",
            int stackAmount = 0,
            int repeatCount = 1)
        {
            Message = message;
            Color = color;
            TimestampLocal = timestampLocal;
            StackKey = stackKey;
            StackAmount = stackAmount;
            RepeatCount = repeatCount;
        }
    }

    public static readonly Color DefaultTextColor = Color.white;
    public static readonly Color ItemGainColor = new Color(0.22f, 0.68f, 0.28f, 1f);
    public static readonly Color ItemLostColor = new Color(0.95f, 0.38f, 0.32f, 1f);
    public static readonly Color GoldColor = new Color(1f, 0.82f, 0.2f, 1f);
    public static readonly Color QuestCompleteColor = new Color(0.82f, 0.96f, 0.82f, 1f);
    public static readonly Color LevelAvailableColor = new Color(0.35f, 0.8f, 1f, 1f);
    public static readonly Color RegionUnlockedColor = new Color(0.45f, 1f, 0.45f, 1f);

    private static readonly List<Entry> Entries = new();

    public static IReadOnlyList<Entry> History => Entries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeHistory()
    {
        Entries.Clear();
    }

    public static void Add(string message)
    {
        Add(message, DefaultTextColor);
    }

    public static void Add(string message, Color color)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string trimmed = message.Trim();
        System.DateTime timestampLocal = System.DateTime.Now;
        Entries.Add(new Entry(trimmed, color, timestampLocal));
        TrimToMaxEntries();

        GameLogWindowUI window = GameLogWindowUI.ResolveOrCreate();
        if (window != null)
            window.AddLog(trimmed, color, FormatClock(timestampLocal));
    }

    private static void TrimToMaxEntries()
    {
        int overflow = Entries.Count - MaxEntries;
        if (overflow <= 0)
            return;

        Entries.RemoveRange(0, overflow);
    }

    public static void Clear()
    {
        Entries.Clear();

        GameLogWindowUI window = GameLogWindowUI.ResolveOrCreate();
        if (window != null)
            window.ClearLogs();
    }

    public static void ItemGained(string itemName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        AddStackableItemGain(itemName.Trim(), amount);
    }

    private static void AddStackableItemGain(string itemName, int amount)
    {
        string stackKey = $"ItemGain:{itemName}";
        System.DateTime timestampLocal = System.DateTime.Now;

        if (Entries.Count > 0)
        {
            int lastIndex = Entries.Count - 1;
            Entry last = Entries[lastIndex];
            if (last.StackKey == stackKey)
            {
                int total = Mathf.Max(0, last.StackAmount) + amount;
                int repeatCount = Mathf.Max(1, last.RepeatCount) + 1;
                Entries[lastIndex] = new Entry(
                    FormatStackedItemGain(itemName, total, repeatCount),
                    ItemGainColor,
                    timestampLocal,
                    stackKey,
                    total,
                    repeatCount);

                GameLogWindowUI window = GameLogWindowUI.ResolveOrCreate();
                if (window != null)
                    window.RebuildFromHistory();
                return;
            }
        }

        Entries.Add(new Entry(
            FormatStackedItemGain(itemName, amount, 1),
            ItemGainColor,
            timestampLocal,
            stackKey,
            amount,
            1));
        TrimToMaxEntries();

        GameLogWindowUI logWindow = GameLogWindowUI.ResolveOrCreate();
        if (logWindow != null)
            logWindow.AddLog(Entries[^1].Message, ItemGainColor, FormatClock(timestampLocal));
    }

    private static string FormatStackedItemGain(string itemName, int amount, int repeatCount)
    {
        string suffix = repeatCount > 1 ? " (Repeat action)" : "";
        return $"+{amount} {itemName}{suffix}";
    }

    public static void ItemLost(string itemName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        Add($"-{amount} {itemName.Trim()}", ItemLostColor);
    }

    public static void InventoryFull(string itemName = null)
    {
        string suffix = string.IsNullOrWhiteSpace(itemName) ? "" : $": {itemName.Trim()}";
        Add($"Inventory full{suffix}", ItemLostColor);
    }

    /// <summary>Idle auto-loot: items that fit only in storage because the inventory had no room.</summary>
    public static void ItemSentToStorageBecauseInventoryFull(string itemDisplayName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemDisplayName))
            return;

        string label = itemDisplayName.Trim();
        Add($"+{amount} {label} - Inventory was full, sent to storage.", ItemGainColor);
    }

    /// <summary>Idle auto-loot: pickup could not fit in inventory or storage.</summary>
    public static void CannotObtainInventoryAndStorageFull(string itemDisplayName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemDisplayName))
            return;

        string label = itemDisplayName.Trim();
        string qtyPrefix = amount == 1 ? "" : $"{amount}x ";
        Add($"Cannot obtain {qtyPrefix}{label} since inventory and storage is full.", ItemLostColor);
    }

    public static void PurchaseFailed(string reason, string itemName = null)
    {
        string trimmedReason = string.IsNullOrWhiteSpace(reason) ? "Purchase failed" : reason.Trim();
        string suffix = string.IsNullOrWhiteSpace(itemName) ? "" : $": {itemName.Trim()}";
        Add($"{trimmedReason}{suffix}", ItemLostColor);
    }

    public static void SoldItem(string itemName, int amount, int gold)
    {
        if (amount <= 0 || gold <= 0)
            return;

        string label = string.IsNullOrWhiteSpace(itemName) ? "Item" : itemName.Trim();
        Add($"Sold {amount}x {label} for {gold} gold", GoldColor);
    }

    public static void GoldGained(int amount, string sourceLine = null)
    {
        if (amount <= 0)
            return;

        string line = string.IsNullOrWhiteSpace(sourceLine)
            ? $"+{amount} Gold"
            : $"+{amount} Gold - {sourceLine.Trim()}";
        Add(line, GoldColor);
    }

    public static void QuestComplete(string questName)
    {
        if (string.IsNullOrWhiteSpace(questName))
            return;

        Add($"Quest complete: {questName.Trim()}", QuestCompleteColor);
    }

    public static void LevelAvailable(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return;

        Add($"New level available: {levelName.Trim()}", LevelAvailableColor);
    }

    public static void RegionUnlocked(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName))
            return;

        Add($"{regionName.Trim()} Region Unlocked", RegionUnlockedColor);
    }

    public static void EnteringMap(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return;

        Add($"Entering Map {mapName.Trim()}");
    }

    public static string FormatClock(System.DateTime localTime)
    {
        string format = ToggleSettingsStore.Get(ToggleSettingId.UseTwentyFourHourTime)
            ? "H:mm:ss"
            : "h:mm:ss tt";
        return localTime.ToString(format, CultureInfo.InvariantCulture);
    }
}
