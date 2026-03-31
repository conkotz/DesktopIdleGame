using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only progression for world map nodes (not wired to save yet).
/// Starting node is unlocked on reset; other nodes stay locked until explicitly unlocked.
/// Lives in Bootstrap; <see cref="Instance"/> + <see cref="FindFirstObjectByType{T}"/> let UI resolve it without serialized refs.
/// </summary>
public class WorldMapProgressManager : MonoBehaviour
{
    public static WorldMapProgressManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Debug / Test Seeds")]
    [Tooltip("Extra node ids unlocked at init (besides startingNodeId). Level select 'Map locked' means the id is not in this set / story unlocks yet — skill requirements are separate.")]
    [SerializeField] private List<string> additionalUnlockedNodeIds = new();

    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);

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
}
