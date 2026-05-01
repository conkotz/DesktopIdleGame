using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight wave spawner for non-endurance maps.
/// Uses <see cref="LevelSpawnDirector.SpawnAdditionalGroupPlan"/> and advances when the current wave is fully dead.
/// </summary>
[DisallowMultipleComponent]
public class ScriptedWaveSpawner : MonoBehaviour
{
    [Serializable]
    public class WaveSpawnEntry
    {
        [Tooltip("Enemy to spawn for this row.")]
        public EnemyDefinition enemyDefinition;

        [Min(1)]
        [Tooltip("How many of this enemy to spawn in the wave.")]
        public int count = 1;

        [Tooltip("Optional row-level SpawnPointGroup override. Leave empty to use Wave Group Id.")]
        public string spawnPointGroupId = "";

        [Tooltip("Optional exact spawn point name under the selected SpawnPointGroup.")]
        public string spawnPointName = "";
    }

    [Serializable]
    public class ScriptedWave
    {
        [Tooltip("Default SpawnPointGroup.groupId for entries that leave Spawn Point Group Id empty.")]
        public string groupId = "CombatEnemies";

        [Tooltip("If true, shuffle spawn points before placing this wave.")]
        public bool shuffleSpawnPoints = true;

        [Tooltip("Rows in this wave.")]
        public List<WaveSpawnEntry> entries = new();
    }

    [Header("When to start")]
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField] private string requiredMapNodeId = "";
    [SerializeField] private bool requireQuestRewardClaimed;
    [SerializeField] private string requiredQuestId = "";

    [Header("Sequence")]
    [SerializeField] private bool runOncePerSceneSession = true;
    [SerializeField] private List<ScriptedWave> waves = new();

    private readonly List<EnemyBaseController> _aliveThisWave = new();
    private int _currentWaveIndex = -1;
    private bool _running;
    private bool _completed;
    private QuestProgressManager _quests;

    private void OnEnable()
    {
        if (autoStartOnEnable)
            TryStart();
    }

    private void OnDisable()
    {
        UnsubscribeFromAliveWave();
    }

    private void Update()
    {
        if (!_running)
            return;

        // Safety net: if an enemy is destroyed/disabled without death event, still allow wave advancement.
        for (int i = _aliveThisWave.Count - 1; i >= 0; i--)
        {
            EnemyBaseController ec = _aliveThisWave[i];
            if (!ec || ec.IsDead)
            {
                if (ec)
                    ec.OnDeath -= OnTrackedEnemyDeath;
                _aliveThisWave.RemoveAt(i);
            }
        }

        if (_aliveThisWave.Count == 0)
            SpawnNextWave();
    }

    [ContextMenu("Start Wave Sequence")]
    public void TryStart()
    {
        if (_running)
            return;
        if (runOncePerSceneSession && _completed)
            return;
        if (!PassesStartConditions())
            return;
        if (waves == null || waves.Count == 0)
            return;

        _running = true;
        _completed = false;
        _currentWaveIndex = -1;
        SpawnNextWave();
    }

    [ContextMenu("Reset Wave Sequence State")]
    public void ResetSequenceState()
    {
        UnsubscribeFromAliveWave();
        _currentWaveIndex = -1;
        _running = false;
        _completed = false;
    }

    private bool PassesStartConditions()
    {
        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        if (!string.IsNullOrWhiteSpace(requiredMapNodeId))
        {
            string need = requiredMapNodeId.Trim();
            string have = active != null && !string.IsNullOrWhiteSpace(active.nodeId)
                ? active.nodeId.Trim()
                : string.Empty;
            if (!string.Equals(need, have, StringComparison.Ordinal))
                return false;
        }

        if (requireQuestRewardClaimed)
        {
            if (string.IsNullOrWhiteSpace(requiredQuestId))
                return false;

            _quests = QuestProgressManager.Instance ??
                FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
            if (_quests == null || !_quests.IsRewardClaimed(requiredQuestId))
                return false;
        }

        return true;
    }

    private void SpawnNextWave()
    {
        UnsubscribeFromAliveWave();
        _currentWaveIndex++;

        if (waves == null || _currentWaveIndex >= waves.Count)
        {
            _running = false;
            _completed = true;
            return;
        }

        LevelSpawnDirector director = FindFirstObjectByType<LevelSpawnDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            _running = false;
            return;
        }

        ScriptedWave wave = waves[_currentWaveIndex];
        LevelSpawnGroupPlan plan = BuildPlan(wave);
        if (plan == null || plan.spawns == null || plan.spawns.Count == 0)
        {
            // Empty/invalid wave: skip forward.
            SpawnNextWave();
            return;
        }

        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        List<EnemyBaseController> spawned = director.SpawnAdditionalGroupPlan(plan, active);
        for (int i = 0; i < spawned.Count; i++)
        {
            EnemyBaseController ec = spawned[i];
            if (!ec)
                continue;
            ec.OnDeath -= OnTrackedEnemyDeath;
            ec.OnDeath += OnTrackedEnemyDeath;
            _aliveThisWave.Add(ec);
        }

        if (_aliveThisWave.Count == 0)
            SpawnNextWave();
    }

    private static LevelSpawnGroupPlan BuildPlan(ScriptedWave wave)
    {
        if (wave == null || wave.entries == null || wave.entries.Count == 0)
            return null;

        List<SpawnPrefabCount> rows = new();
        for (int i = 0; i < wave.entries.Count; i++)
        {
            WaveSpawnEntry e = wave.entries[i];
            if (e == null || !e.enemyDefinition || e.count <= 0)
                continue;

            rows.Add(new SpawnPrefabCount
            {
                spawnPointGroupId = string.IsNullOrWhiteSpace(e.spawnPointGroupId) ? "" : e.spawnPointGroupId.Trim(),
                spawnPointName = string.IsNullOrWhiteSpace(e.spawnPointName) ? "" : e.spawnPointName.Trim(),
                enemyDefinition = e.enemyDefinition,
                count = Mathf.Max(1, e.count)
            });
        }

        if (rows.Count == 0)
            return null;

        return new LevelSpawnGroupPlan
        {
            groupId = string.IsNullOrWhiteSpace(wave.groupId) ? "" : wave.groupId.Trim(),
            shuffleSpawnPoints = wave.shuffleSpawnPoints,
            spawns = rows
        };
    }

    private void OnTrackedEnemyDeath()
    {
        // Update() handles progression after list cleanup.
    }

    private void UnsubscribeFromAliveWave()
    {
        for (int i = 0; i < _aliveThisWave.Count; i++)
        {
            EnemyBaseController ec = _aliveThisWave[i];
            if (ec)
                ec.OnDeath -= OnTrackedEnemyDeath;
        }

        _aliveThisWave.Clear();
    }
}
