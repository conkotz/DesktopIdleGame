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
    private readonly HashSet<string> _progressUnlockAnnounced = new(StringComparer.Ordinal);
    /// <summary>Nodes the player has actually loaded in GamePlay at least once (see GameplayLevelBootstrapper).</summary>
    private readonly HashSet<string> _entered = new(StringComparer.Ordinal);
    /// <summary>Total enemy kills credited per map node id (across this save).</summary>
    private readonly Dictionary<string, int> _enemyKillsByNode = new(StringComparer.Ordinal);

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
        ResolveWorldMap();
        ResetProgressToDefaults();
    }

    private void ResolveWorldMap()
    {
        if (worldMap)
            return;
        worldMap = Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
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
        _entered.Clear();
        _enemyKillsByNode.Clear();
        _enduranceMaxSelectableTier.Clear();
        _progressUnlockAnnounced.Clear();

        if (worldMap && !string.IsNullOrEmpty(worldMap.startingNodeId))
            _unlocked.Add(worldMap.startingNodeId);

        foreach (string id in additionalUnlockedNodeIds)
        {
            if (!string.IsNullOrEmpty(id))
                _unlocked.Add(id);
        }

        ProgressChanged?.Invoke();
        RefreshProgressUnlockAnnouncementBaseline();
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

    public bool HasEnteredNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        return _entered.Contains(nodeId.Trim());
    }

    public int GetEnemyKillsOnNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return 0;
        if (_enemyKillsByNode.TryGetValue(nodeId.Trim(), out int kills))
            return Mathf.Max(0, kills);
        return 0;
    }

    public void NotifyEnemyKilledOnNode(string nodeId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || amount <= 0)
            return;

        string id = nodeId.Trim();
        _enemyKillsByNode.TryGetValue(id, out int current);
        int next = Mathf.Max(0, current + amount);
        if (next == current)
            return;

        _enemyKillsByNode[id] = next;
        TryAnnounceNewProgressUnlockedNodes();
        ProgressChanged?.Invoke();
    }

    /// <summary>Call when GamePlay starts for a map node (persists for completion-label gates).</summary>
    /// <returns>True when this was the first recorded visit for <paramref name="nodeId"/>.</returns>
    public bool MarkNodeEntered(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return false;
        if (!_entered.Add(nodeId.Trim())) return false;
        ProgressChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// True if the player has loaded GamePlay on a map whose <see cref="RegionDefinition.regionId"/> differs from <paramref name="regionId"/>.
    /// </summary>
    public bool HasEnteredOutsideWorldRegion(string regionId, WorldMapDefinition map)
    {
        if (string.IsNullOrEmpty(regionId) || map == null)
            return false;

        foreach (string nodeId in _entered)
        {
            if (string.IsNullOrEmpty(nodeId))
                continue;
            RegionDefinition r = map.FindRegionContainingNode(nodeId);
            if (r == null)
                continue;
            if (!string.Equals(r.regionId, regionId, StringComparison.Ordinal))
                return true;
        }

        return false;
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
        string id = nodeId.Trim();
        if (!_unlocked.Add(id)) return;
        GameLog.LevelAvailable(ResolveNodeDisplayName(id));
        // Node is already unlocked by map-story gate; don't duplicate a progress-lock unlock log for this id.
        _progressUnlockAnnounced.Add(id);
        ProgressChanged?.Invoke();
    }

    public void LockNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;
        if (!_unlocked.Remove(nodeId)) return;
        ProgressChanged?.Invoke();
    }

    private string ResolveNodeDisplayName(string nodeId)
    {
        if (worldMap != null)
        {
            MapNodeDefinition node = worldMap.FindNodeById(nodeId);
            if (node != null && !string.IsNullOrWhiteSpace(node.displayName))
                return node.displayName;
        }

        return nodeId;
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

        TryAnnounceNewProgressUnlockedNodes();
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

        if (data.worldMapUnlockedNodeIds == null)
            data.worldMapUnlockedNodeIds = new List<string>();
        if (data.worldMapCompletedNodeIds == null)
            data.worldMapCompletedNodeIds = new List<string>();
        if (data.worldMapEnteredNodeIds == null)
            data.worldMapEnteredNodeIds = new List<string>();
        if (data.worldMapEnemyKillNodeIds == null)
            data.worldMapEnemyKillNodeIds = new List<string>();
        if (data.worldMapEnemyKillTotals == null)
            data.worldMapEnemyKillTotals = new List<int>();

        data.worldMapUnlockedNodeIds.Clear();
        data.worldMapCompletedNodeIds.Clear();
        data.worldMapEnteredNodeIds.Clear();
        data.worldMapEnemyKillNodeIds.Clear();
        data.worldMapEnemyKillTotals.Clear();

        foreach (string id in _unlocked)
        {
            if (!string.IsNullOrEmpty(id))
                data.worldMapUnlockedNodeIds.Add(id);
        }

        foreach (string id in _completed)
        {
            if (!string.IsNullOrEmpty(id))
                data.worldMapCompletedNodeIds.Add(id);
        }

        foreach (string id in _entered)
        {
            if (!string.IsNullOrEmpty(id))
                data.worldMapEnteredNodeIds.Add(id);
        }

        foreach (var kv in _enemyKillsByNode)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;
            if (kv.Value <= 0)
                continue;

            data.worldMapEnemyKillNodeIds.Add(kv.Key);
            data.worldMapEnemyKillTotals.Add(kv.Value);
        }

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
        ResolveWorldMap();

        _unlocked.Clear();
        _completed.Clear();
        _entered.Clear();
        _enemyKillsByNode.Clear();
        _enduranceMaxSelectableTier.Clear();

        if (worldMap && !string.IsNullOrEmpty(worldMap.startingNodeId))
            _unlocked.Add(worldMap.startingNodeId.Trim());

        foreach (string id in additionalUnlockedNodeIds)
        {
            if (!string.IsNullOrEmpty(id))
                _unlocked.Add(id.Trim());
        }

        if (data != null)
        {
            if (data.worldMapUnlockedNodeIds != null)
            {
                for (int i = 0; i < data.worldMapUnlockedNodeIds.Count; i++)
                {
                    string id = data.worldMapUnlockedNodeIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _unlocked.Add(id.Trim());
                }
            }

            if (data.worldMapCompletedNodeIds != null)
            {
                for (int i = 0; i < data.worldMapCompletedNodeIds.Count; i++)
                {
                    string id = data.worldMapCompletedNodeIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _completed.Add(id.Trim());
                }
            }

            if (data.worldMapEnteredNodeIds != null)
            {
                for (int i = 0; i < data.worldMapEnteredNodeIds.Count; i++)
                {
                    string id = data.worldMapEnteredNodeIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _entered.Add(id.Trim());
                }
            }

            if (data.worldMapEnemyKillNodeIds != null && data.worldMapEnemyKillTotals != null)
            {
                int n = Mathf.Min(data.worldMapEnemyKillNodeIds.Count, data.worldMapEnemyKillTotals.Count);
                for (int i = 0; i < n; i++)
                {
                    string id = data.worldMapEnemyKillNodeIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    int kills = Mathf.Max(0, data.worldMapEnemyKillTotals[i]);
                    if (kills <= 0)
                        continue;
                    _enemyKillsByNode[id.Trim()] = kills;
                }
            }

            if (data.enduranceTrialNodeIds != null && data.enduranceTrialMaxSelectableTier != null)
            {
                int n = Mathf.Min(data.enduranceTrialNodeIds.Count, data.enduranceTrialMaxSelectableTier.Count);
                for (int i = 0; i < n; i++)
                {
                    string id = data.enduranceTrialNodeIds[i];
                    if (string.IsNullOrEmpty(id))
                        continue;
                    int tier = Mathf.Clamp(data.enduranceTrialMaxSelectableTier[i], EnduranceTrialTier.MinTier, EnduranceTrialTier.MaxTier);
                    _enduranceMaxSelectableTier[id] = tier;
                }
            }
        }

        RefreshProgressUnlockAnnouncementBaseline();
        ProgressChanged?.Invoke();
    }

    private void RefreshProgressUnlockAnnouncementBaseline()
    {
        _progressUnlockAnnounced.Clear();
        if (worldMap == null || worldMap.regions == null)
            return;

        for (int r = 0; r < worldMap.regions.Count; r++)
        {
            RegionDefinition region = worldMap.regions[r];
            if (region == null || region.nodes == null)
                continue;

            for (int n = 0; n < region.nodes.Count; n++)
            {
                MapNodeDefinition node = region.nodes[n];
                if (node == null || string.IsNullOrWhiteSpace(node.nodeId))
                    continue;

                if (node.IsMapProgressSatisfied(this) && node.MeetsPreviousMapCompletionRequirements(this))
                    _progressUnlockAnnounced.Add(node.nodeId.Trim());
            }
        }
    }

    private void TryAnnounceNewProgressUnlockedNodes()
    {
        if (worldMap == null || worldMap.regions == null)
            return;

        for (int r = 0; r < worldMap.regions.Count; r++)
        {
            RegionDefinition region = worldMap.regions[r];
            if (region == null || region.nodes == null)
                continue;

            for (int n = 0; n < region.nodes.Count; n++)
            {
                MapNodeDefinition node = region.nodes[n];
                if (node == null || string.IsNullOrWhiteSpace(node.nodeId))
                    continue;

                string nodeId = node.nodeId.Trim();
                if (_progressUnlockAnnounced.Contains(nodeId))
                    continue;
                if (!node.IsMapProgressSatisfied(this))
                    continue;
                if (!node.MeetsPreviousMapCompletionRequirements(this))
                    continue;

                _progressUnlockAnnounced.Add(nodeId);
                GameLog.LevelAvailable(ResolveNodeDisplayName(nodeId));
            }
        }
    }
}
