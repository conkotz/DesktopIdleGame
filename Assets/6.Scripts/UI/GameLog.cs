using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Runtime-only game log. Entries are kept in memory across scene changes, but are intentionally not saved.
/// </summary>
public static class GameLog
{
    public readonly struct Entry
    {
        public readonly string Message;
        public readonly Color Color;
        public readonly System.DateTime TimestampLocal;

        public Entry(string message, Color color, System.DateTime timestampLocal)
        {
            Message = message;
            Color = color;
            TimestampLocal = timestampLocal;
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

        GameLogWindowUI window = GameLogWindowUI.ResolveOrCreate();
        if (window != null)
            window.AddLog(trimmed, color, FormatClock(timestampLocal));
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

        Add($"+{amount} {itemName.Trim()}", ItemGainColor);
    }

    public static void ItemLost(string itemName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        Add($"-{amount} {itemName.Trim()}", ItemLostColor);
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

    public static string FormatClock(System.DateTime localTime)
    {
        string format = ToggleSettingsStore.Get(ToggleSettingId.UseTwentyFourHourTime)
            ? "H:mm:ss"
            : "h:mm:ss tt";
        return localTime.ToString(format, CultureInfo.InvariantCulture);
    }
}
