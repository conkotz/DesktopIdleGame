using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks per-quest objective amounts (kills gathered, items gathered, etc.). Persisted via <see cref="ISaveable"/>.
/// Call <see cref="AddProgress"/> from gameplay when relevant events occur.
/// </summary>
public class QuestProgressManager : MonoBehaviour, ISaveable
{
    public static QuestProgressManager Instance { get; private set; }

    private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);

    public event Action ProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int GetProgress(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return 0;
        return _amounts.TryGetValue(questId.Trim(), out int v) ? v : 0;
    }

    public void SetProgress(string questId, int value)
    {
        if (string.IsNullOrEmpty(questId))
            return;
        questId = questId.Trim();
        value = Mathf.Max(0, value);
        int prev = GetProgress(questId);
        if (prev == value)
            return;
        _amounts[questId] = value;
        ProgressChanged?.Invoke();
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

    public void AddProgress(string questId, int delta)
    {
        if (delta == 0 || string.IsNullOrEmpty(questId))
            return;
        int cur = GetProgress(questId);
        SetProgress(questId, cur + delta);
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        if (data.questProgressIds == null)
            data.questProgressIds = new List<string>();
        if (data.questProgressAmounts == null)
            data.questProgressAmounts = new List<int>();

        data.questProgressIds.Clear();
        data.questProgressAmounts.Clear();

        foreach (var kv in _amounts)
        {
            if (string.IsNullOrEmpty(kv.Key))
                continue;
            data.questProgressIds.Add(kv.Key);
            data.questProgressAmounts.Add(kv.Value);
        }
    }

    public void LoadFrom(SaveData data)
    {
        _amounts.Clear();

        if (data?.questProgressIds == null || data.questProgressAmounts == null)
        {
            ProgressChanged?.Invoke();
            return;
        }

        int n = Mathf.Min(data.questProgressIds.Count, data.questProgressAmounts.Count);
        for (int i = 0; i < n; i++)
        {
            string id = data.questProgressIds[i];
            if (string.IsNullOrEmpty(id))
                continue;
            _amounts[id.Trim()] = Mathf.Max(0, data.questProgressAmounts[i]);
        }

        ProgressChanged?.Invoke();
    }
}
