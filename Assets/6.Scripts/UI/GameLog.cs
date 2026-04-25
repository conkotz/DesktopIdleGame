using System.Collections.Generic;
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

        public Entry(string message, Color color)
        {
            Message = message;
            Color = color;
        }
    }

    public static readonly Color DefaultTextColor = Color.white;
    public static readonly Color ItemGainColor = new Color(0.55f, 0.92f, 0.58f, 1f);
    public static readonly Color GoldColor = new Color(1f, 0.82f, 0.2f, 1f);
    public static readonly Color QuestCompleteColor = new Color(0.82f, 0.96f, 0.82f, 1f);
    public static readonly Color LevelAvailableColor = new Color(0.35f, 0.8f, 1f, 1f);

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
        Entries.Add(new Entry(trimmed, color));

        GameLogWindowUI window = GameLogWindowUI.ResolveOrCreate();
        if (window != null)
            window.AddLog(trimmed, color);
    }

    public static void ItemGained(string itemName, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;

        Add($"+{amount} {itemName.Trim()}", ItemGainColor);
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
}
