using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Runtime game log. Recent entries stay in memory for the activity UI; older lines are truncated
/// and optionally appended to a rolling archive file under persistent data.
/// </summary>
public static class GameLog
{
    public const int MaxEntries = 75;
    private const long MaxArchiveFileBytes = 256 * 1024;

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
    /// <summary>Activity log color for lines starting with "Cannot" (blocked actions).</summary>
    public static readonly Color CannotMessageColor = new Color(0.95f, 0.28f, 0.28f, 1f);
    /// <summary>Resource entered depleted state (reduced yield until regen).</summary>
    public static readonly Color ResourceDepletedColor = new Color(0.92f, 0.22f, 0.22f, 1f);
    public static readonly Color QuestCompleteColor = new Color(0.82f, 0.96f, 0.82f, 1f);
    public static readonly Color LevelAvailableColor = new Color(0.35f, 0.8f, 1f, 1f);
    public static readonly Color RegionUnlockedColor = new Color(0.45f, 1f, 0.45f, 1f);
    /// <summary>Player used an ability (action bar / combat).</summary>
    public static readonly Color AbilityUsedColor = new Color(0.55f, 0.78f, 1f, 1f);

    private static readonly List<Entry> Entries = new();

    /// <summary>Increments whenever in-memory history changes (add, merge, trim, clear).</summary>
    public static int Revision { get; private set; }

    /// <summary>Subscribe to batch-refresh the activity window instead of updating per log line.</summary>
    public static event Action HistoryChanged;

    public static IReadOnlyList<Entry> History => Entries;

    private static string ArchiveFilePath =>
        Path.Combine(Application.persistentDataPath, "activity_log_archive.txt");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeHistory()
    {
        Entries.Clear();
        Revision = 0;
        HistoryChanged = null;
    }

    public static void Add(string message)
    {
        Add(message, DefaultTextColor);
    }

    /// <summary>Activity log lines that should not be stored or shown (e.g. per-cast ability spam).</summary>
    public static bool ShouldShowInActivityLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return !message.Trim().StartsWith("Ability used:", StringComparison.OrdinalIgnoreCase);
    }

    public static void Add(string message, Color color)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string trimmed = message.Trim();
        if (!ShouldShowInActivityLog(trimmed))
            return;

        color = ResolveCannotLogColor(trimmed, color);
        System.DateTime timestampLocal = System.DateTime.Now;
        Entries.Add(new Entry(trimmed, color, timestampLocal));
        TrimToMaxEntries();
        NotifyHistoryChanged();
    }

    private static void TrimToMaxEntries()
    {
        int overflow = Entries.Count - MaxEntries;
        if (overflow <= 0)
            return;

        ArchiveTrimmedEntries(0, overflow);
        Entries.RemoveRange(0, overflow);
    }

    private static void ArchiveTrimmedEntries(int startIndex, int count)
    {
        if (count <= 0)
            return;

        try
        {
            var sb = new StringBuilder(count * 64);
            for (int i = 0; i < count; i++)
            {
                Entry e = Entries[startIndex + i];
                if (!ShouldShowInActivityLog(e.Message))
                    continue;

                sb.Append(e.TimestampLocal.ToString("O", CultureInfo.InvariantCulture));
                sb.Append('\t');
                sb.Append(e.Color.r.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(e.Color.g.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(e.Color.b.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(e.Color.a.ToString(CultureInfo.InvariantCulture));
                sb.Append('\t');
                sb.AppendLine(e.Message);
            }

            if (sb.Length == 0)
                return;

            File.AppendAllText(ArchiveFilePath, sb.ToString());
            TrimArchiveFileIfNeeded();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{nameof(GameLog)}] Failed to archive trimmed activity log lines: {ex.Message}");
        }
    }

    private static void TrimArchiveFileIfNeeded()
    {
        try
        {
            if (!File.Exists(ArchiveFilePath))
                return;

            var info = new FileInfo(ArchiveFilePath);
            if (info.Length <= MaxArchiveFileBytes)
                return;

            // Keep the newest half of the archive file.
            string[] lines = File.ReadAllLines(ArchiveFilePath);
            int keepFrom = Mathf.Max(0, lines.Length / 2);
            var kept = new StringBuilder();
            for (int i = keepFrom; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;
                kept.AppendLine(lines[i]);
            }

            File.WriteAllText(ArchiveFilePath, kept.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{nameof(GameLog)}] Failed to trim activity log archive: {ex.Message}");
        }
    }

    private static void NotifyHistoryChanged()
    {
        Revision++;
        HistoryChanged?.Invoke();
    }

    public static void Clear()
    {
        Entries.Clear();
        NotifyHistoryChanged();
    }

    public static void ItemGained(string itemName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        AddStackableItemGain(itemName.Trim(), amount, isPurchase: false);
    }

    /// <summary>Shop (and similar) buys: same stacking as <see cref="ItemGained"/> but message starts with \"Purchased\".</summary>
    public static void ItemPurchased(string itemName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        AddStackableItemGain(itemName.Trim(), amount, isPurchase: true);
    }

    private static void AddStackableItemGain(string itemName, int amount, bool isPurchase)
    {
        string stackKey = (isPurchase ? "ItemPurchase:" : "ItemGain:") + itemName;
        System.DateTime timestampLocal = System.DateTime.Now;

        if (ToggleSettingsStore.Get(ToggleSettingId.GroupRepeatedActivityLogItemGains) &&
            Entries.Count > 0)
        {
            int lastIndex = Entries.Count - 1;
            Entry last = Entries[lastIndex];
            if (last.StackKey == stackKey)
            {
                int total = Mathf.Max(0, last.StackAmount) + amount;
                int repeatCount = Mathf.Max(1, last.RepeatCount) + 1;
                Entries[lastIndex] = new Entry(
                    FormatStackedItemGain(itemName, total, repeatCount, isPurchase),
                    ItemGainColor,
                    timestampLocal,
                    stackKey,
                    total,
                    repeatCount);

                NotifyHistoryChanged();
                return;
            }
        }

        Entries.Add(new Entry(
            FormatStackedItemGain(itemName, amount, 1, isPurchase),
            ItemGainColor,
            timestampLocal,
            stackKey,
            amount,
            1));
        TrimToMaxEntries();
        NotifyHistoryChanged();
    }

    private static string FormatStackedItemGain(string itemName, int amount, int repeatCount, bool purchased)
    {
        string suffix = repeatCount > 1 ? " (Repeat action)" : "";
        string core = $"+{amount} {itemName}{suffix}";
        return purchased ? $"Purchased {core}" : core;
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

    /// <summary>
    /// Logs items returned when undoing a sale, and a gold line matching the original <see cref="GoldGained"/> entry (+X Gold).
    /// </summary>
    public static void SaleUndoRestored(string itemName, int amount, int goldLineAmount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        string label = itemName.Trim();
        ItemGained(label, amount);

        if (goldLineAmount > 0)
            GoldGained(goldLineAmount);
    }

    private static Color ResolveCannotLogColor(string trimmedMessage, Color requestedColor)
    {
        if (ShouldUseBlockedOrNegativeLogColor(trimmedMessage))
            return CannotMessageColor;
        return requestedColor;
    }

    /// <summary>
    /// Default activity lines use <see cref="DefaultTextColor"/>; blocked / failure popups (Cannot, Not enough…, cooldown, etc.)
    /// are shown in <see cref="CannotMessageColor"/> instead.
    /// </summary>
    private static bool ShouldUseBlockedOrNegativeLogColor(string m)
    {
        if (string.IsNullOrEmpty(m))
            return false;

        if (m.StartsWith("Cannot", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Can't ", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Item not available", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Ability not available", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Ability cant", StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.StartsWith("Not enough", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Could not ", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Inventory full", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Requires level", StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.IndexOf(" on cooldown", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.IndexOf("Fatigued", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.StartsWith("This Region is locked", StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.StartsWith("No ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.StartsWith("Missing ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.StartsWith("Unable to", StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith("Failed ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m, "Action not allowed.", StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.IndexOf(" not found", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Equipment tier gate: "Iron Sword (Tier II) needs Mining level 5 (yours: 2)."
        if (m.IndexOf(" (yours:", StringComparison.OrdinalIgnoreCase) >= 0 &&
            m.IndexOf(" needs ", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
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
