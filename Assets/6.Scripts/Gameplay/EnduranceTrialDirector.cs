using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wave-based spawning for <see cref="MapNodeType.EnduranceTrial"/> maps.
/// Uses <see cref="LevelSpawnDirector.SpawnWavePlans"/> per wave; advances when every spawned enemy in the wave dies.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay/Endurance Trial Director")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class EnduranceTrialDirector : MonoBehaviour
{
    public static EnduranceTrialDirector Instance { get; private set; }

    /// <summary>Fired when the displayed wave index changes (1-based current, total waves).</summary>
    public event Action<int, int> OnWaveChanged;

    /// <summary>When the last wave has been cleared.</summary>
    public event Action OnAllWavesCompleted;

    /// <summary>Whole seconds remaining before the next wave spawns (0 = not in cooldown). Updates during the between-waves wait.</summary>
    public event Action<int> OnNextWaveCountdownSeconds;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Seconds to wait after a wave is cleared before the next wave spawns.")]
    private float waveCooldownSeconds = 3f;

    private MapNodeDefinition _def;
    private LevelSpawnDirector _spawnDirector;
    private int _waveIndex;
    private bool _started;
    private bool _trialComplete;
    private Coroutine _betweenWavesRoutine;

    /// <summary>Whole seconds shown for "Next wave in: n" (0 = hide / not between waves).</summary>
    public int NextWaveCountdownSeconds { get; private set; }

    public int CurrentWaveDisplay => Mathf.Min(_waveIndex + 1, Mathf.Max(1, TotalWaves));

    public int TotalWaves => _def != null && _def.enduranceWaves != null ? _def.enduranceWaves.Count : 0;

    public bool IsActive => _started && _def && _def.nodeType == MapNodeType.EnduranceTrial && !_trialComplete;

    private void OnEnable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;
    }

    private void OnDisable()
    {
        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;

        StopBetweenWavesRoutine();

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (_started)
            return;

        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            TryBegin(GameplayLevelBootstrapper.Instance.ActiveDefinition);
    }

    private void OnLevelStarted(MapNodeDefinition def)
    {
        TryBegin(def);
    }

    private void TryBegin(MapNodeDefinition def)
    {
        if (!def || def.nodeType != MapNodeType.EnduranceTrial)
        {
            if (Instance == this)
                Instance = null;
            _started = false;
            _trialComplete = false;
            _def = null;
            SetNextWaveCountdown(0);
            return;
        }

        if (_started && _def == def && !_trialComplete)
            return;

        _spawnDirector = FindFirstObjectByType<LevelSpawnDirector>();
        if (_spawnDirector == null)
        {
            Debug.LogError("[EnduranceTrialDirector] No LevelSpawnDirector in scene.", this);
            return;
        }

        if (def.enduranceWaves == null || def.enduranceWaves.Count == 0)
        {
            Debug.LogError("[EnduranceTrialDirector] EnduranceTrial map has no enduranceWaves configured.", def);
            return;
        }

        _def = def;
        _started = true;
        _trialComplete = false;
        Instance = this;
        _waveIndex = 0;

        StopBetweenWavesRoutine();
        SetNextWaveCountdown(0);
        BeginWave();
    }

    private void BeginWave()
    {
        SetNextWaveCountdown(0);

        if (_def == null || _def.enduranceWaves == null)
            return;

        if (_waveIndex >= _def.enduranceWaves.Count)
        {
            CompleteTrial();
            return;
        }

        EnduranceWavePlan wave = _def.enduranceWaves[_waveIndex];
        wave?.EnsureReady();

        if (wave == null || wave.spawns == null || wave.spawns.Count == 0)
        {
            Debug.LogWarning($"[EnduranceTrialDirector] Wave {_waveIndex + 1} has no spawns — skipping.", this);
            _waveIndex++;
            BeginWave();
            return;
        }

        var oneWavePlan = new List<LevelSpawnGroupPlan> { wave.ToSyntheticGroupPlan() };
        List<EnemyBaseController> enemies = _spawnDirector.SpawnWavePlans(oneWavePlan);
        enemies.RemoveAll(e => e == null);

        if (enemies.Count == 0)
        {
            Debug.LogError($"[EnduranceTrialDirector] Wave {_waveIndex + 1} spawned zero enemies (check prefabs / SpawnPointGroup ids).", this);
            return;
        }

        OnWaveChanged?.Invoke(_waveIndex + 1, _def.enduranceWaves.Count);

        int remaining = enemies.Count;
        foreach (EnemyBaseController e in enemies)
        {
            EnemyBaseController captured = e;
            void Handler()
            {
                captured.OnDeath -= Handler;
                remaining--;
                if (remaining <= 0)
                    OnWaveCleared();
            }

            captured.OnDeath += Handler;
        }
    }

    private void OnWaveCleared()
    {
        StopBetweenWavesRoutine();
        _betweenWavesRoutine = StartCoroutine(AfterWaveClearedRoutine());
    }

    private IEnumerator AfterWaveClearedRoutine()
    {
        int nextIndex = _waveIndex + 1;
        if (_def == null || _def.enduranceWaves == null || nextIndex >= _def.enduranceWaves.Count)
        {
            SetNextWaveCountdown(0);
            CompleteTrial();
            _betweenWavesRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0f, waveCooldownSeconds);
        if (duration > 0f)
        {
            float elapsed = 0f;
            int lastShown = -1;
            while (elapsed < duration)
            {
                int shown = Mathf.Max(1, Mathf.CeilToInt(duration - elapsed));
                if (shown != lastShown)
                {
                    lastShown = shown;
                    SetNextWaveCountdown(shown);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        SetNextWaveCountdown(0);

        _waveIndex = nextIndex;
        _betweenWavesRoutine = null;
        BeginWave();
    }

    private void StopBetweenWavesRoutine()
    {
        if (_betweenWavesRoutine == null)
            return;
        StopCoroutine(_betweenWavesRoutine);
        _betweenWavesRoutine = null;
        SetNextWaveCountdown(0);
    }

    private void SetNextWaveCountdown(int wholeSeconds)
    {
        if (wholeSeconds < 0)
            wholeSeconds = 0;
        if (NextWaveCountdownSeconds == wholeSeconds)
            return;
        NextWaveCountdownSeconds = wholeSeconds;
        OnNextWaveCountdownSeconds?.Invoke(wholeSeconds);
    }

    private void CompleteTrial()
    {
        SetNextWaveCountdown(0);
        _trialComplete = true;
        if (_def != null && _def.enduranceWaves != null)
            OnWaveChanged?.Invoke(_def.enduranceWaves.Count, _def.enduranceWaves.Count);
        OnAllWavesCompleted?.Invoke();
        if (Instance == this)
            Instance = null;
    }
}
