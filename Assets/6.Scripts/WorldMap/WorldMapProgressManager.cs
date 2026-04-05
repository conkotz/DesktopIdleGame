using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime progression for world map nodes. Endurance trial max selectable tier per node is saved via <see cref="ISaveable"/>.
/// Starting node is unlocked on reset; other nodes stay locked until explicitly unlocked.
/// Lives in Bootstrap; <see cref="Instance"/> + <see cref="FindFirstObjectByType{T}"/> let UI resolve it without serialized refs.
/// </summary>
public class WorldMapProgressManager : MonoBehaviour, ISaveable
{
    public static WorldMapProgressManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Debug / Test Seeds")]
    [Tooltip("Extra node ids unlocked at init (besides startingNodeId). Level select 'Map locked' means the id is not in this set / story unlocks yet — skill requirements are separate.")]
    [SerializeField] private List<string> additionalUnlockedNodeIds = new();

    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);

    /// <summary>Highest endurance trial tier (1–5) selectable for this node; Tier I always implied. Unlocks when the player clears all waves at the current max tier.</summary>
    private readonly Dictionary<string, int> _enduranceMaxSelectableTier = new(StringComparer.Ordinal);

    public WorldMapDefinition WorldMap => worldMap;

    public event Action ProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResetProgressToDefaults();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Reset Progress To Defaults")]
    public void ResetProgressToDefaults()
    {
        _unlocked.Clear();
        _completed.Clear();
        _enduranceMaxSelectableTier.Clear();

        if (worldMap && !string.IsNullOrEmpty(worldMap.startingNodeId))
            _unlocked.Add(worldMap.startingNodeId);

        foreach (string id in additionalUnlockedNodeIds)
        {
            if (!string.IsNullOrEmpty(id))
                _unlocked.Add(id);
        }

        ProgressChanged?.Invoke();
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        return _unlocked.Contains(nodeId);
    }

    public bool IsNodeCompleted(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        return _completed.Contains(nodeId);
    }

    /// <summary>
    /// Map progression only (not skills). Prefer <see cref="MapNodeDefinition.GetUiStateLabel"/> for the level-select UI.
    /// </summary>
    public string GetStateLabel(string nodeId)
    {
        if (!IsNodeUnlocked(nodeId)) return "Locked";
        if (IsNodeCompleted(nodeId)) return "Completed";
        return "Unlocked";
    }

    public void UnlockNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        if (!_unlocked.Add(nodeId)) return;
        ProgressChanged?.Invoke();
    }

    public void LockNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        if (!_unlocked.Remove(nodeId)) return;
        ProgressChanged?.Invoke();
    }

    public void SetNodeCompleted(string nodeId, bool completed)
    {
        if (string.IsNullOrEmpty(nodeId)) return;

        if (completed)
        {
            if (!_completed.Add(nodeId)) return;
        }
        else
        {
            if (!_completed.Remove(nodeId)) return;
        }

        ProgressChanged?.Invoke();
    }

    /// <summary>
    /// Test helper: marks a node completed and logs the id.
    /// </summary>
    public void DebugCompleteSelected(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        SetNodeCompleted(nodeId, true);
        Debug.Log($"[WorldMapProgress] Marked completed: {nodeId}");
    }

    /// <summary>Highest tier (1–5) the player may select for this endurance node. Defaults to 1 (Tier I only).</summary>
    public int GetEnduranceMaxSelectableTier(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 1;
        if (_enduranceMaxSelectableTier.TryGetValue(nodeId, out int t))
            return Mathf.Clamp(t, 1, EnduranceTrialTier.MaxTier);
        return 1;
    }

    /// <summary>Call when all waves are cleared at <paramref name="completedTier"/>; unlocks the next tier if applicable.</summary>
    /// <returns>True if a higher difficulty tier became selectable (was not already unlocked).</returns>
    public bool NotifyEnduranceTrialTierCleared(string nodeId, int completedTier)
    {
        if (string.IsNullOrEmpty(nodeId))
            return false;
        completedTier = Mathf.Clamp(completedTier, EnduranceTrialTier.MinTier, EnduranceTrialTier.MaxTier);
        int next = Mathf.Min(EnduranceTrialTier.MaxTier, completedTier + 1);
        int cur = GetEnduranceMaxSelectableTier(nodeId);
        if (next <= cur)
            return false;
        _enduranceMaxSelectableTier[nodeId] = next;
        ProgressChanged?.Invoke();

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
        return true;
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        if (data.enduranceTrialNodeIds == null)
            data.enduranceTrialNodeIds = new List<string>();
        if (data.enduranceTrialMaxSelectableTier == null)
            data.enduranceTrialMaxSelectableTier = new List<int>();

        data.enduranceTrialNodeIds.Clear();
        data.enduranceTrialMaxSelectableTier.Clear();

        foreach (var kv in _enduranceMaxSelectableTier)
        {
            if (string.IsNullOrEmpty(kv.Key))
                continue;
            data.enduranceTrialNodeIds.Add(kv.Key);
            data.enduranceTrialMaxSelectableTier.Add(Mathf.Clamp(kv.Value, EnduranceTrialTier.MinTier, EnduranceTrialTier.MaxTier));
        }
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null)
            return;

        _enduranceMaxSelectableTier.Clear();

        if (data.enduranceTrialNodeIds == null || data.enduranceTrialMaxSelectableTier == null)
        {
            ProgressChanged?.Invoke();
            return;
        }

        int n = Mathf.Min(data.enduranceTrialNodeIds.Count, data.enduranceTrialMaxSelectableTier.Count);
        for (int i = 0; i < n; i++)
        {
            string id = data.enduranceTrialNodeIds[i];
            if (string.IsNullOrEmpty(id))
                continue;
            int tier = Mathf.Clamp(data.enduranceTrialMaxSelectableTier[i], EnduranceTrialTier.MinTier, EnduranceTrialTier.MaxTier);
            _enduranceMaxSelectableTier[id] = tier;
        }

        ProgressChanged?.Invoke();
    }
}
