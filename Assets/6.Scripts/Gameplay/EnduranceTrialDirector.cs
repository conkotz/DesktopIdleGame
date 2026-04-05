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

    [SerializeField]
    [Tooltip("Root to enable when an endurance map loads (must contain or be the EnduranceTrialsBeginPopup panel). Leave empty to auto-bind to that popup in the scene. If you moved the panel, clear this so it does not still point at an old HUD shell.")]
    private GameObject enduranceTrialsUI;

    private MapNodeDefinition _def;
    private LevelSpawnDirector _spawnDirector;
    private int _trialTier = 1;
    private int _waveIndex;
    private bool _started;
    private bool _trialComplete;
    private bool _waitingForPlayerBegin;
    private Coroutine _betweenWavesRoutine;
    private Coroutine _completionLootRoutine;

    private readonly List<EnduranceTrialUIHelpers.EnduranceTrialLootGrant> _lastCompletionLoot = new();

    /// <summary>Whole seconds shown for "Next wave in: n" (0 = hide / not between waves).</summary>
    public int NextWaveCountdownSeconds { get; private set; }

    public int CurrentWaveDisplay => Mathf.Min(_waveIndex + 1, Mathf.Max(1, TotalWaves));

    public int TotalWaves => _def != null && _def.enduranceWaves != null ? _def.enduranceWaves.Count : 0;

    /// <summary>True during waves (trial not finished yet).</summary>
    public bool IsActive => _started && _def && _def.nodeType == MapNodeType.EnduranceTrial && !_trialComplete;

    /// <summary>True after the last wave is cleared.</summary>
    public bool TrialCompleted => _trialComplete;

    /// <summary>Show endurance HUD during the trial and after completion (e.g. &quot;Trials complete&quot;).</summary>
    public bool ShowEnduranceHud => _started && _def && _def.nodeType == MapNodeType.EnduranceTrial;

    /// <summary>True after the endurance map loads until <see cref="ConfirmBeginTrial"/> runs (first wave not spawned yet).</summary>
    public bool IsWaitingForPlayerBegin => _waitingForPlayerBegin;

    /// <summary>Difficulty tier for this run (1–5). Set when the player confirms Begin; use <see cref="EnduranceTrialPendingTier"/> while <see cref="IsWaitingForPlayerBegin"/>.</summary>
    public int CurrentTrialTier => _trialTier;

    /// <summary>Loot rolled at the end of the last completed trial (same list passed to <see cref="DropManager"/>).</summary>
    public IReadOnlyList<EnduranceTrialUIHelpers.EnduranceTrialLootGrant> LastCompletionLootGrants => _lastCompletionLoot;

    /// <summary>Tier the player cleared on the last completed run (1–5).</summary>
    public int LastCompletedRunTier { get; private set; }

    /// <summary>True if the last completion increased max selectable tier for this node.</summary>
    public bool LastRunUnlockedNextTier { get; private set; }

    /// <summary>After a successful unlock, the new max selectable tier (roman display via <see cref="EnduranceTrialTier.ToRomanNumeral"/>). 0 if <see cref="LastRunUnlockedNextTier"/> is false.</summary>
    public int LastUnlockedTier { get; private set; }

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
        StopCompletionLootRoutine();

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
            _waitingForPlayerBegin = false;
            _def = null;
            SetNextWaveCountdown(0);
            SetEnduranceTrialsUiActive(false);
            return;
        }

        if (_started && _def == def && !_trialComplete)
            return;

        _spawnDirector = FindFirstObjectByType<LevelSpawnDirector>();
        if (_spawnDirector == null)
        {
            Debug.LogError("[EnduranceTrialDirector] No LevelSpawnDirector in scene.", this);
            SetEnduranceTrialsUiActive(false);
            return;
        }

        if (def.enduranceWaves == null || def.enduranceWaves.Count == 0)
        {
            Debug.LogError("[EnduranceTrialDirector] EnduranceTrial map has no enduranceWaves configured.", def);
            SetEnduranceTrialsUiActive(false);
            return;
        }

        _def = def;
        _started = true;
        _trialComplete = false;
        _waitingForPlayerBegin = true;
        Instance = this;
        _waveIndex = 0;

        _lastCompletionLoot.Clear();
        LastCompletedRunTier = 0;
        LastRunUnlockedNextTier = false;
        LastUnlockedTier = 0;

        StopBetweenWavesRoutine();
        StopCompletionLootRoutine();
        SetNextWaveCountdown(0);
        ResolveEnduranceTrialsUiReference();
        SetEnduranceTrialsUiActive(true);
    }

    /// <summary>
    /// Binds <see cref="enduranceTrialsUI"/> to the real <see cref="EnduranceTrialsBeginPopup"/> when unset, or when
    /// the serialized reference is stale (e.g. old object under Strip HUD after moving the panel to FullWindowCanvas).
    /// </summary>
    private void ResolveEnduranceTrialsUiReference()
    {
        EnduranceTrialsBeginPopup popup = FindFirstObjectByType<EnduranceTrialsBeginPopup>(FindObjectsInactive.Include);
        if (popup == null)
            return;

        Transform popupTr = popup.transform;

        if (enduranceTrialsUI == null)
        {
            enduranceTrialsUI = popup.gameObject;
            return;
        }

        Transform root = enduranceTrialsUI.transform;
        if (IsTransformDescendantOf(popupTr, root))
            return;

        enduranceTrialsUI = popup.gameObject;
    }

    private static bool IsTransformDescendantOf(Transform node, Transform ancestor)
    {
        if (node == null || ancestor == null)
            return false;

        for (Transform t = node; t != null; t = t.parent)
        {
            if (t == ancestor)
                return true;
        }

        return false;
    }

    private void SetEnduranceTrialsUiActive(bool active)
    {
        if (enduranceTrialsUI && enduranceTrialsUI.activeSelf != active)
            enduranceTrialsUI.SetActive(active);
    }

    /// <summary>
    /// Call from the pre-trial UI (e.g. Begin button). Spawns wave 1 and hides the intro popup.
    /// </summary>
    public void ConfirmBeginTrial()
    {
        if (!_started || _def == null || _def.nodeType != MapNodeType.EnduranceTrial || _trialComplete)
            return;
        if (!_waitingForPlayerBegin)
            return;

        _trialTier = Mathf.Clamp(EnduranceTrialPendingTier.Tier, EnduranceTrialTier.MinTier, EnduranceTrialTier.MaxTier);
        _waitingForPlayerBegin = false;
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

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i])
                enemies[i].ApplyEnduranceTrialTier(_trialTier);
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

        ItemDatabase db = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        _lastCompletionLoot.Clear();
        if (_def != null)
            _lastCompletionLoot.AddRange(EnduranceTrialUIHelpers.RollEnduranceCompletionLoot(_def, db, _trialTier));

        LastCompletedRunTier = _trialTier;
        LastRunUnlockedNextTier = false;
        LastUnlockedTier = 0;

        if (_def != null)
        {
            WorldMapProgressManager progress = WorldMapProgressManager.Instance != null
                ? WorldMapProgressManager.Instance
                : FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
            if (progress != null)
            {
                LastRunUnlockedNextTier = progress.NotifyEnduranceTrialTierCleared(_def.nodeId, _trialTier);
                if (LastRunUnlockedNextTier)
                    LastUnlockedTier = progress.GetEnduranceMaxSelectableTier(_def.nodeId);
            }
        }

        if (_lastCompletionLoot.Count > 0)
            _completionLootRoutine = StartCoroutine(SpawnCompletionLootRoutine());

        // Completion UI must open here: EnduranceTrialsBeginPopup.LateUpdate does not run while popupRoot is inactive (hidden during waves).
        NotifyEnduranceTrialCompletionPopups();
    }

    private static void NotifyEnduranceTrialCompletionPopups()
    {
        EnduranceTrialsBeginPopup[] popups = FindObjectsByType<EnduranceTrialsBeginPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++)
        {
            if (popups[i] != null)
                popups[i].NotifyTrialCompletedFromDirector();
        }
    }

    private IEnumerator SpawnCompletionLootRoutine()
    {
        MapNodeDefinition def = _def;
        DropManager dm = DropManager.Instance != null
            ? DropManager.Instance
            : FindFirstObjectByType<DropManager>(FindObjectsInactive.Include);

        if (!dm)
        {
            if (_lastCompletionLoot.Count > 0)
                Debug.LogWarning("[EnduranceTrialDirector] No DropManager in scene — completion loot skipped.", this);
            _completionLootRoutine = null;
            yield break;
        }

        float interval = def != null && def.enduranceCompletionLootInterval > 0f ? def.enduranceCompletionLootInterval : 0.5f;

        for (int i = 0; i < _lastCompletionLoot.Count; i++)
        {
            if (i > 0)
                yield return new WaitForSeconds(interval);

            EnduranceTrialUIHelpers.EnduranceTrialLootGrant g = _lastCompletionLoot[i];
            dm.Spawn(g.itemId, g.amount, g.icon);
        }

        _completionLootRoutine = null;
    }

    private void StopCompletionLootRoutine()
    {
        if (_completionLootRoutine == null)
            return;
        StopCoroutine(_completionLootRoutine);
        _completionLootRoutine = null;
    }
}
