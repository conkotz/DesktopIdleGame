using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs optional non-endurance combat waves authored on <see cref="MapNodeDefinition.simpleCombatWaves"/>.
/// This replaces scene-local scripted wave setup with map-definition-driven wave data.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Simple Combat Wave Director")]
[DisallowMultipleComponent]
public class SimpleCombatWaveDirector : MonoBehaviour
{
    public static SimpleCombatWaveDirector Instance { get; private set; }

    private readonly List<EnemyBaseController> _aliveThisWave = new();

    private MapNodeDefinition _activeDefinition;
    private QuestProgressManager _quests;
    private int _currentWaveIndex = -1;
    private bool _running;
    private bool _completedThisSceneSession;

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SimpleCombatWaveDirector] Duplicate instance found. Keeping the newest enabled instance.", this);
        }
        Instance = this;

        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;

        TryBindQuestProgress();

        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            TryBeginFor(GameplayLevelBootstrapper.Instance.ActiveDefinition, forceStart: false);
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;

        if (_quests != null)
            _quests.ProgressChanged -= OnQuestProgressChanged;

        _quests = null;
        UnsubscribeFromAliveWave();
        _running = false;

        if (Instance == this)
            Instance = null;
    }

    public static bool HaveSimpleWavesStartedFor(MapNodeDefinition def)
    {
        if (def == null || Instance == null)
            return false;
        if (Instance._activeDefinition != def)
            return false;
        return Instance._currentWaveIndex >= 0 || Instance._running || Instance._completedThisSceneSession;
    }

    public static bool IsSimpleWavesRunningFor(MapNodeDefinition def)
    {
        if (def == null || Instance == null)
            return false;
        if (Instance._activeDefinition != def)
            return false;
        return Instance._running && Instance._currentWaveIndex >= 0;
    }

    private void Update()
    {
        if (!_running)
            return;

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
            HandleWaveCleared();
    }

    [ContextMenu("Start Map Definition Simple Waves")]
    public void TryStartCurrentMapSimpleWaves()
    {
        MapNodeDefinition def = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;

        TryBeginFor(def, forceStart: true);
    }

    private void OnLevelStarted(MapNodeDefinition def)
    {
        _activeDefinition = def;
        _completedThisSceneSession = false;
        _currentWaveIndex = -1;
        _running = false;
        UnsubscribeFromAliveWave();
        TryBeginFor(def, forceStart: false);
    }

    private void TryBindQuestProgress()
    {
        if (_quests != null)
            return;

        _quests = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);

        if (_quests == null)
            return;

        _quests.ProgressChanged -= OnQuestProgressChanged;
        _quests.ProgressChanged += OnQuestProgressChanged;
    }

    private void OnQuestProgressChanged()
    {
        if (_running)
            return;

        TryBeginFor(_activeDefinition, forceStart: false);
    }

    private void TryBeginFor(MapNodeDefinition def, bool forceStart)
    {
        if (def == null || def.nodeType == MapNodeType.EnduranceTrial)
            return;
        if (!forceStart && !def.simpleCombatWavesAutoStartOnLevelEnter && _currentWaveIndex < 0)
            return;
        if (def.simpleCombatWaves == null || def.simpleCombatWaves.Count == 0)
            return;
        if (_running)
            return;
        if (def.simpleCombatWavesRunOncePerSceneSession && _completedThisSceneSession)
            return;
        if (!PassesQuestGate(def))
            return;

        _activeDefinition = def;
        _running = true;
        _currentWaveIndex = -1;
        SpawnNextWave(advance: true);
    }

    private bool PassesQuestGate(MapNodeDefinition def)
    {
        if (!def.simpleCombatWavesRequireQuestRewardClaimed &&
            !def.simpleCombatWavesRequireQuestAccepted)
            return true;

        TryBindQuestProgress();
        if (_quests == null)
            return false;

        if (def.simpleCombatWavesRequireQuestRewardClaimed)
        {
            if (string.IsNullOrWhiteSpace(def.simpleCombatWavesRequiredQuestId) ||
                !_quests.IsRewardClaimed(def.simpleCombatWavesRequiredQuestId.Trim()))
                return false;
        }

        if (def.simpleCombatWavesRequireQuestAccepted)
        {
            if (string.IsNullOrWhiteSpace(def.simpleCombatWavesQuestAcceptedId))
                return false;

            QuestDefinition q = _quests.GetQuestDefinition(def.simpleCombatWavesQuestAcceptedId.Trim());
            if (!q || !_quests.IsQuestAccepted(q))
                return false;
        }

        return true;
    }

    private void HandleWaveCleared()
    {
        if (_activeDefinition == null ||
            _activeDefinition.simpleCombatWaves == null ||
            _currentWaveIndex < 0 ||
            _currentWaveIndex >= _activeDefinition.simpleCombatWaves.Count)
        {
            SpawnNextWave(advance: true);
            return;
        }

        EnduranceWavePlan current = _activeDefinition.simpleCombatWaves[_currentWaveIndex];
        bool repeatCurrent = current != null && current.repeatThisWave;
        SpawnNextWave(advance: !repeatCurrent);
    }

    private void SpawnNextWave(bool advance)
    {
        UnsubscribeFromAliveWave();
        if (advance)
            _currentWaveIndex++;

        if (_activeDefinition == null ||
            _activeDefinition.simpleCombatWaves == null ||
            _currentWaveIndex >= _activeDefinition.simpleCombatWaves.Count)
        {
            _running = false;
            _completedThisSceneSession = true;
            return;
        }

        EnduranceWavePlan wave = _activeDefinition.simpleCombatWaves[_currentWaveIndex];
        wave?.EnsureReady();
        if (wave == null || wave.spawns == null || wave.spawns.Count == 0)
        {
            SpawnNextWave(advance: true);
            return;
        }

        LevelSpawnDirector director = FindFirstObjectByType<LevelSpawnDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            _running = false;
            return;
        }

        LevelSpawnGroupPlan plan = wave.ToSyntheticGroupPlan();
        List<EnemyBaseController> spawned = director.SpawnAdditionalGroupPlan(plan, _activeDefinition);
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
            HandleWaveCleared();
    }

    private void OnTrackedEnemyDeath()
    {
        // Wave progression is handled in Update after list cleanup.
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
