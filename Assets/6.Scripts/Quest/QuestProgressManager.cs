using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks per-quest objective amounts (kills, etc.). Gather-item quests use live inventory + storage counts.
/// Persisted via <see cref="ISaveable"/>.
/// </summary>
public class QuestProgressManager : MonoBehaviour, ISaveable
{
    public static QuestProgressManager Instance { get; private set; }
    private const string GameplaySceneName = "GamePlay";

    [SerializeField] private QuestDatabase questDatabase;

    [Header("Gather quest — popup")]
    [SerializeField] private Color gatherConsumedPopupColor = new Color(0.85f, 0.35f, 0.3f, 1f);

    [Header("Reward delivery")]
    [Tooltip("Activity log when quest item rewards overflow to storage.")]
    [SerializeField] private Color questRewardToStorageLogColor = new Color(0.55f, 0.78f, 1f, 1f);

    private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rewardClaimed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _acceptedQuestIds = new(StringComparer.Ordinal);

    /// <summary>True after any non-repeatable quest with <see cref="QuestDefinition.grantIdleCombatUnlockOnRewardClaim"/> has had its reward claimed.</summary>
    private bool _idleCombatUnlocked;

    private QuestDatabase _resolvedDatabase;
    private bool _isAutoCompleteProcessing;
    private bool _autoCompleteDeferred;

    private Inventory _autoInv;
    private PlayerStorage _autoStorage;
    private SkillsManager _autoSkills;
    private WorldMapProgressManager _autoWorldMap;
    private bool _autoCompleteInventoryTriggered;
    private CharacterStats _playerDeathStats;
    private PlayerController _cachedPlayer;

    public event Action ProgressChanged;

    /// <summary>Fired after a quest is newly accepted (NPC offer, auto-accept, etc.).</summary>
    public event Action<QuestDefinition> QuestAccepted;

    public bool IsIdleCombatUnlocked => _idleCombatUnlocked;

    public QuestDatabase QuestDatabase
    {
        get
        {
            ResolveQuestDatabase();
            return _resolvedDatabase;
        }
    }

    /// <summary>
    /// Activity-log line when the player tries to use Auto Battle before any
    /// <see cref="QuestDefinition.grantIdleCombatUnlockOnRewardClaim"/> quest has had its reward claimed.
    /// </summary>
    public string BuildIdleCombatUnlockBlockedMessage()
    {
        if (IsIdleCombatUnlocked)
            return "";

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return "";

        var names = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || q.repeatable || !q.grantIdleCombatUnlockOnRewardClaim)
                continue;
            if (IsRewardClaimed(q.questId))
                continue;

            string nm = string.IsNullOrWhiteSpace(q.displayName) ? q.questId.Trim() : q.displayName.Trim();
            if (names.Contains(nm))
                continue;
            names.Add(nm);
        }

        if (names.Count == 0)
            return "";

        if (names.Count == 1)
            return $"Locked until you complete and claim the quest reward: {names[0]}.";

        return "Locked until you complete and claim the quest reward for one of: " + string.Join(", ", names) + ".";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveQuestDatabase();
    }

    private void OnDestroy()
    {
        UnbindAutoCompleteSignals();

        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        BindAutoCompleteSignals();
        TryBindPlayerDeathSignal();
        QuestAcceptedEnemyRespawnService.BindLifecycle();
    }

    private void OnDisable()
    {
        UnbindPlayerDeathSignal();
        UnbindAutoCompleteSignals();
        QuestAcceptedEnemyRespawnService.UnbindLifecycle();
        _autoCompleteDeferred = false;
    }

    private void ResolveQuestDatabase()
    {
        if (questDatabase)
            _resolvedDatabase = questDatabase;
        else
            _resolvedDatabase = Resources.Load<QuestDatabase>("Databases/QuestDatabase_Main");
    }

    private void BindAutoCompleteSignals()
    {
        _autoInv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (_autoInv != null)
        {
            _autoInv.OnInventoryChanged -= HandleInventoryAutoCompleteSignalChanged;
            _autoInv.OnInventoryChanged += HandleInventoryAutoCompleteSignalChanged;
        }

        _autoStorage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        _autoSkills = SkillsManager.Instance ??
            FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
        if (_autoSkills != null)
        {
            _autoSkills.OnLevelUp -= HandleAutoCompleteSkillLevelUp;
            _autoSkills.OnLevelUp += HandleAutoCompleteSkillLevelUp;
        }

        _autoWorldMap = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (_autoWorldMap != null)
        {
            _autoWorldMap.ProgressChanged -= HandleAutoCompleteSignalChanged;
            _autoWorldMap.ProgressChanged += HandleAutoCompleteSignalChanged;
        }
    }

    private void UnbindAutoCompleteSignals()
    {
        if (_autoInv != null)
            _autoInv.OnInventoryChanged -= HandleInventoryAutoCompleteSignalChanged;
        if (_autoSkills != null)
            _autoSkills.OnLevelUp -= HandleAutoCompleteSkillLevelUp;
        if (_autoWorldMap != null)
            _autoWorldMap.ProgressChanged -= HandleAutoCompleteSignalChanged;

        _autoInv = null;
        _autoStorage = null;
        _autoSkills = null;
        _autoWorldMap = null;
    }

    private void HandleAutoCompleteSignalChanged()
    {
        _autoCompleteDeferred = true;
    }

    private void HandleInventoryAutoCompleteSignalChanged()
    {
        _autoCompleteDeferred = true;
        _autoCompleteInventoryTriggered = true;
    }

    private void HandleAutoCompleteSkillLevelUp(SkillType _, int __)
    {
        _autoCompleteDeferred = true;
    }

    private void Update()
    {
        if (_playerDeathStats == null)
            TryBindPlayerDeathSignal();
    }

    private void LateUpdate()
    {
        if (!_autoCompleteDeferred)
            return;

        bool fromInventory = _autoCompleteInventoryTriggered;
        _autoCompleteDeferred = false;
        _autoCompleteInventoryTriggered = false;

        if (fromInventory)
            TryAutoCompleteGatherQuestsFromInventory();
        else
            TryAutoCompleteEligibleQuests();
    }

    private void TryBindPlayerDeathSignal()
    {
        if (_playerDeathStats != null)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
            return;

        CharacterStats stats = player.GetComponent<CharacterStats>();
        if (stats == null)
            return;

        _cachedPlayer = player;
        _playerDeathStats = stats;
        _playerDeathStats.OnDied -= HandlePlayerDiedForQuestProgress;
        _playerDeathStats.OnDied += HandlePlayerDiedForQuestProgress;
    }

    private void UnbindPlayerDeathSignal()
    {
        if (_playerDeathStats == null)
            return;

        _playerDeathStats.OnDied -= HandlePlayerDiedForQuestProgress;
        _playerDeathStats = null;
        _cachedPlayer = null;
    }

    public bool IsPlayerAliveForQuestClaim()
    {
        if (_cachedPlayer == null)
            TryBindPlayerDeathSignal();
        return _cachedPlayer != null && !_cachedPlayer.IsDead;
    }

    private void HandlePlayerDiedForQuestProgress()
    {
        ResolveQuestDatabase();
        if (_resolvedDatabase == null)
            return;

        IReadOnlyList<QuestDefinition> all = _resolvedDatabase.All;
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || q.objectiveKind != QuestObjectiveKind.DieOnce)
                continue;
            if (!q.repeatable && IsRewardClaimed(q.questId))
                continue;
            if (!IsQuestAccepted(q))
                continue;
            if (IsQuestGatedByPrerequisites(q))
                continue;

            int required = Mathf.Max(1, q.targetCount);
            if (GetProgress(q.questId) >= required)
                continue;

            SetProgress(q.questId, required);
        }
    }

    private QuestDefinition FindQuestDefinition(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return null;
        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null)
            return null;
        string key = questId.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (q && string.Equals(q.questId?.Trim(), key, StringComparison.Ordinal))
                return q;
        }

        return null;
    }

    public QuestDefinition GetQuestDefinition(string questId) => FindQuestDefinition(questId);

    /// <summary>Kill quests: saved progress. Gather quests: inventory + storage total (not saved in _amounts).</summary>
    public int GetDisplayProgress(QuestDefinition q)
    {
        if (!q)
            return 0;
        if (q.objectiveKind == QuestObjectiveKind.GatherItem)
            return GetGatherItemCountLive(q.objectiveId);
        return GetProgress(q.questId);
    }

    public int GetGatherItemCountLive(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;
        itemId = itemId.Trim();

        if (_autoInv == null)
            _autoInv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (_autoStorage == null)
            _autoStorage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        int n = _autoInv ? _autoInv.GetTotalAmount(itemId) : 0;
        if (_autoStorage)
            n += _autoStorage.GetTotalAmount(itemId);
        return n;
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
        QuestDefinition def = FindQuestDefinition(questId);
        if (def != null && def.objectiveKind == QuestObjectiveKind.GatherItem)
            return;

        value = Mathf.Max(0, value);
        int prev = GetProgress(questId);
        if (prev == value)
            return;
        _amounts[questId] = value;
        ProgressChanged?.Invoke();
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
        TryAutoCompleteEligibleQuests();
    }

    public void AddProgress(string questId, int delta)
    {
        if (delta == 0 || string.IsNullOrEmpty(questId))
            return;
        QuestDefinition def = FindQuestDefinition(questId);
        if (def != null && def.objectiveKind == QuestObjectiveKind.GatherItem)
            return;

        int cur = GetProgress(questId);
        SetProgress(questId, cur + delta);
    }

    public bool IsRewardClaimed(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return false;
        return _rewardClaimed.Contains(questId.Trim());
    }

    public bool IsPermanentlyComplete(QuestDefinition q)
    {
        return q && !q.repeatable && IsRewardClaimed(q.questId);
    }

    public static bool RequiresQuestGiver(QuestDefinition q)
    {
        return q && !string.IsNullOrWhiteSpace(q.obtainLocationId);
    }

    public bool IsQuestAccepted(QuestDefinition q)
    {
        if (!q || string.IsNullOrWhiteSpace(q.questId))
            return false;
        if (!RequiresQuestGiver(q))
            return true;
        return _acceptedQuestIds.Contains(q.questId.Trim());
    }

    /// <summary>
    /// Regional quest journal eligibility. Includes undiscovered quest-giver offers so the list can show where to obtain them.
    /// Map visibility gates still use <see cref="QuestDefinition.IsShownInQuestList"/>.
    /// </summary>
    public bool IsQuestVisibleInList(QuestDefinition q)
    {
        if (q == null)
            return false;

        if (!q.hideFromQuestJournalUnlessAccepted)
            return true;

        if (IsPermanentlyComplete(q))
            return false;

        return IsQuestAccepted(q);
    }

    public bool CanAcceptQuest(QuestDefinition q, string giverLocationId = null)
    {
        if (!q || string.IsNullOrWhiteSpace(q.questId))
            return false;
        if (!RequiresQuestGiver(q))
            return false;
        if (IsQuestAccepted(q) || IsPermanentlyComplete(q))
            return false;

        WorldMapProgressManager mapProgress = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (!q.IsShownInQuestList(mapProgress))
            return false;

        if (!string.IsNullOrWhiteSpace(giverLocationId) &&
            !string.Equals(q.obtainLocationId.Trim(), giverLocationId.Trim(), StringComparison.Ordinal))
            return false;
        if (!ArePrerequisitesSatisfied(q) || !AreSkillRequirementsSatisfied(q))
            return false;
        return true;
    }

    public bool TryAcceptQuest(QuestDefinition q, string giverLocationId = null)
    {
        if (!CanAcceptQuest(q, giverLocationId))
            return false;

        _acceptedQuestIds.Add(q.questId.Trim());
        GameLog.Add($"Quest accepted: {q.displayName}");
        QuestAccepted?.Invoke(q);
        ProgressChanged?.Invoke();
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();

        if (ToggleSettingsStore.Get(ToggleSettingId.AutoTrackNewQuest))
            ApplyAutoTrackAndShowQuestTracker(q.questId);

        TryAutoCompleteEligibleQuests();
        QuestAcceptedEnemyRespawnService.OnQuestAccepted(q);

        return true;
    }

    private static void ApplyAutoTrackAndShowQuestTracker(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        string id = questId.Trim();
        if (!QuestTrackerState.TrackQuest(id))
        {
            if (!QuestTrackerState.IsTracked(id) && !QuestTrackerState.CanTrackMore)
                GameLog.Add($"Cannot track more than {QuestTrackerState.MaxTrackedQuestCount} quests.");
            return;
        }

        QuestTrackerWindowUI.EnsureWindowOpenAfterTrack();
    }

    public bool TryAcceptQuest(string questId, string giverLocationId = null)
    {
        return TryAcceptQuest(FindQuestDefinition(questId), giverLocationId);
    }

    public QuestDefinition FindFirstAcceptableQuestAtLocation(string giverLocationId)
    {
        if (string.IsNullOrWhiteSpace(giverLocationId))
            return null;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null)
            return null;

        string location = giverLocationId.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition quest = all[i];
            if (!quest || string.IsNullOrWhiteSpace(quest.obtainLocationId))
                continue;
            if (!string.Equals(quest.obtainLocationId.Trim(), location, StringComparison.Ordinal))
                continue;
            if (CanAcceptQuest(quest, location))
                return quest;
        }

        return null;
    }

    /// <summary>
    /// All quests the player can accept at this giver location, sorted for stable UI order.
    /// </summary>
    public void CollectAcceptableQuestsAtLocation(string giverLocationId, List<QuestDefinition> into)
    {
        if (into == null)
            return;
        into.Clear();

        if (string.IsNullOrWhiteSpace(giverLocationId))
            return;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null)
            return;

        string location = giverLocationId.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition quest = all[i];
            if (!quest || string.IsNullOrWhiteSpace(quest.obtainLocationId))
                continue;
            if (!string.Equals(quest.obtainLocationId.Trim(), location, StringComparison.Ordinal))
                continue;
            if (!CanAcceptQuest(quest, location))
                continue;
            into.Add(quest);
        }

        into.Sort(CompareQuestGiverOfferOrder);
    }

    /// <summary>
    /// True when turning in <paramref name="questBeingClaimed"/> at this giver would unlock at least one new quest offer here.
    /// Used so turn-in and follow-up offer can be split across two NPC clicks.
    /// </summary>
    public bool HasAcceptableQuestAtLocationAfterRewardClaim(string giverLocationId, QuestDefinition questBeingClaimed)
    {
        if (string.IsNullOrWhiteSpace(giverLocationId) || questBeingClaimed == null ||
            string.IsNullOrWhiteSpace(questBeingClaimed.questId))
            return false;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return false;

        string location = giverLocationId.Trim();
        string claimedId = questBeingClaimed.questId.Trim();

        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition followUp = all[i];
            if (!followUp || string.IsNullOrWhiteSpace(followUp.obtainLocationId))
                continue;
            if (!string.Equals(followUp.obtainLocationId.Trim(), location, StringComparison.Ordinal))
                continue;
            if (IsQuestAccepted(followUp) || IsPermanentlyComplete(followUp))
                continue;
            if (!WouldQuestUnlockWhenRewardClaimed(followUp, claimedId))
                continue;
            if (!AreSkillRequirementsSatisfied(followUp))
                continue;

            WorldMapProgressManager mapProgress = WorldMapProgressManager.Instance ??
                FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
            if (!followUp.IsShownInQuestList(mapProgress))
                continue;

            return true;
        }

        return false;
    }

    private bool WouldQuestUnlockWhenRewardClaimed(QuestDefinition quest, string newlyClaimedRewardId)
    {
        if (quest == null || string.IsNullOrWhiteSpace(newlyClaimedRewardId))
            return false;
        if (IsRewardClaimed(newlyClaimedRewardId))
            return false;

        if (quest.prerequisiteRewardClaimedQuestIds == null || quest.prerequisiteRewardClaimedQuestIds.Count == 0)
            return false;

        bool requiresNewClaim = false;
        for (int i = 0; i < quest.prerequisiteRewardClaimedQuestIds.Count; i++)
        {
            string id = quest.prerequisiteRewardClaimedQuestIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            id = id.Trim();
            if (string.Equals(id, newlyClaimedRewardId, StringComparison.Ordinal))
            {
                requiresNewClaim = true;
                continue;
            }

            if (quest.requireAllPrerequisiteQuests && !IsRewardClaimed(id))
                return false;
        }

        if (!requiresNewClaim)
            return false;

        if (!quest.requireAllPrerequisiteQuests)
        {
            for (int i = 0; i < quest.prerequisiteRewardClaimedQuestIds.Count; i++)
            {
                string id = quest.prerequisiteRewardClaimedQuestIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (IsRewardClaimed(id.Trim()))
                    return true;
            }

            return requiresNewClaim;
        }

        return true;
    }

    private static int CompareQuestGiverOfferOrder(QuestDefinition a, QuestDefinition b)
    {
        if (!a && !b) return 0;
        if (!a) return 1;
        if (!b) return -1;

        int o = a.sortOrder.CompareTo(b.sortOrder);
        if (o != 0)
            return o;

        return string.Compare(a.questId, b.questId, StringComparison.Ordinal);
    }

    public bool CanAbandonQuest(QuestDefinition q)
    {
        if (!q || !q.abandonable || !RequiresQuestGiver(q))
            return false;

        // One-and-done quests stay done — no abandon from journal after COMPLETE reward is claimed.
        if (IsPermanentlyComplete(q))
            return false;

        return IsQuestAccepted(q);
    }

    public bool TryAbandonQuest(QuestDefinition q)
    {
        if (!CanAbandonQuest(q))
            return false;

        string questId = q.questId.Trim();
        _acceptedQuestIds.Remove(questId);
        _rewardClaimed.Remove(questId);
        if (q.objectiveKind != QuestObjectiveKind.GatherItem)
            _amounts.Remove(questId);
        QuestTrackerState.UntrackQuest(questId);

        RecomputeIdleCombatUnlockedFromClaimedRewards();
        DisableIdleCombatIfLocked();

        GameLog.Add($"Quest abandoned: {q.displayName}");
        ProgressChanged?.Invoke();
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
        return true;
    }

    public bool ArePrerequisitesSatisfied(QuestDefinition q)
    {
        if (q == null || q.prerequisiteRewardClaimedQuestIds == null || q.prerequisiteRewardClaimedQuestIds.Count == 0)
            return true;

        bool anyConfigured = false;
        bool anyMet = false;

        for (int i = 0; i < q.prerequisiteRewardClaimedQuestIds.Count; i++)
        {
            string id = q.prerequisiteRewardClaimedQuestIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            anyConfigured = true;
            bool met = IsRewardClaimed(id.Trim());

            if (q.requireAllPrerequisiteQuests && !met)
                return false;
            if (!q.requireAllPrerequisiteQuests && met)
                anyMet = true;
        }

        if (!anyConfigured)
            return true;

        return q.requireAllPrerequisiteQuests || anyMet;
    }

    public bool IsRequiredMapNodeSatisfied(QuestDefinition q)
    {
        if (q == null || string.IsNullOrWhiteSpace(q.requiredCompletedMapNodeId))
            return true;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        return wmp != null && wmp.IsNodeCompleted(q.requiredCompletedMapNodeId.Trim());
    }

    public bool IsQuestGatedByPrerequisites(QuestDefinition q)
    {
        return q != null && (!ArePrerequisitesSatisfied(q) || !AreSkillRequirementsSatisfied(q));
    }

    public bool CanClaimReward(QuestDefinition q)
    {
        if (!CanClaimRewardIgnoringPlayerAlive(q))
            return false;
        return IsPlayerAliveForQuestClaim();
    }

    /// <summary>Objective and turn-in rules satisfied; does not check whether the player is alive.</summary>
    public bool CanClaimRewardIgnoringPlayerAlive(QuestDefinition q)
    {
        if (!q || string.IsNullOrEmpty(q.questId) || q.objectiveKind == QuestObjectiveKind.None)
            return false;
        if (!IsQuestAccepted(q))
            return false;
        if (!ArePrerequisitesSatisfied(q))
            return false;
        if (!AreSkillRequirementsSatisfied(q))
            return false;
        int prog = GetDisplayProgress(q);
        if (!q.IsComplete(prog))
            return false;
        if (!IsRequiredMapNodeSatisfied(q))
            return false;
        if (!q.repeatable && IsRewardClaimed(q.questId))
            return false;
        return true;
    }

    /// <summary>Ready to turn in except the player is dead (for journal button label).</summary>
    public bool IsQuestBlockedOnlyByPlayerDeath(QuestDefinition q)
    {
        if (IsPlayerAliveForQuestClaim() || !CanClaimRewardIgnoringPlayerAlive(q))
            return false;

        if (q.objectiveKind != QuestObjectiveKind.GatherItem &&
            HasItemRewardsToGrant(q) &&
            !CanReceiveAllItemRewards(q))
            return false;

        return true;
    }

    /// <summary>
    /// Same gating as the green <c>Complete Quest</c> button in <see cref="QuestPageUI"/> (includes inventory space for item rewards on non-gather quests).
    /// </summary>
    public bool IsQuestReadyToClaimInJournal(QuestDefinition q)
    {
        if (!CanClaimReward(q))
            return false;

        if (q.objectiveKind != QuestObjectiveKind.GatherItem &&
            HasItemRewardsToGrant(q) &&
            !CanReceiveAllItemRewards(q))
            return false;

        return true;
    }

    /// <summary>
    /// Quest is ready to claim via <see cref="TryClaimQuestReward"/> and belongs to this obtain-location (when <paramref name="giverLocationId"/> is set).
    /// </summary>
    public bool IsQuestReadyToClaimAtGiverLocation(QuestDefinition q, string giverLocationId)
    {
        if (!IsQuestReadyToClaimInJournal(q))
            return false;
        if (!RequiresQuestGiver(q))
            return false;
        if (string.IsNullOrWhiteSpace(giverLocationId))
            return true;
        return string.Equals(q.obtainLocationId.Trim(), giverLocationId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fills <paramref name="into"/> with accepted quests from this obtain-location that match the journal Complete Quest button (sorted like pickup offers).
    /// </summary>
    public void CollectClaimableQuestsAtLocation(string giverLocationId, List<QuestDefinition> into)
    {
        if (into == null)
            return;
        into.Clear();

        if (string.IsNullOrWhiteSpace(giverLocationId))
            return;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null)
            return;

        string location = giverLocationId.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition quest = all[i];
            if (!quest || string.IsNullOrWhiteSpace(quest.obtainLocationId))
                continue;
            if (!IsQuestReadyToClaimAtGiverLocation(quest, location))
                continue;
            into.Add(quest);
        }

        into.Sort(CompareQuestGiverOfferOrder);
    }

    public bool AreSkillRequirementsSatisfied(QuestDefinition q)
    {
        if (q == null)
            return true;

        SkillsManager skills = SkillsManager.Instance ??
            FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
        if (skills == null)
        {
            return q.combatSkillGateMode == CombatSkillGateMode.None &&
                   (q.requiredSkillLevels == null || q.requiredSkillLevels.Count == 0);
        }

        if (q.requiredSkillLevels != null)
        {
            for (int i = 0; i < q.requiredSkillLevels.Count; i++)
            {
                SkillLevelRequirement req = q.requiredSkillLevels[i];
                if (req == null || req.requiredLevel <= 0)
                    continue;
                if (!skills.IsLevelUnlocked(req.skill, req.requiredLevel))
                    return false;
            }
        }

        switch (q.combatSkillGateMode)
        {
            case CombatSkillGateMode.None:
                return true;
            case CombatSkillGateMode.SingleCombatSkill:
                return q.combatRequiredLevel <= 0 ||
                       skills.IsLevelUnlocked(q.combatSingleSkill, q.combatRequiredLevel);
            case CombatSkillGateMode.AnyOfMeleeRangedMagic:
                if (q.combatRequiredLevel <= 0)
                    return true;
                return skills.IsLevelUnlocked(SkillType.Melee, q.combatRequiredLevel) ||
                       skills.IsLevelUnlocked(SkillType.Ranged, q.combatRequiredLevel) ||
                       skills.IsLevelUnlocked(SkillType.Magic, q.combatRequiredLevel);
            case CombatSkillGateMode.AnyCombatSkill:
                if (q.combatRequiredLevel <= 0)
                    return true;
                return skills.IsLevelUnlocked(SkillType.Melee, q.combatRequiredLevel) ||
                       skills.IsLevelUnlocked(SkillType.Ranged, q.combatRequiredLevel) ||
                       skills.IsLevelUnlocked(SkillType.Magic, q.combatRequiredLevel) ||
                       skills.IsLevelUnlocked(SkillType.Endurance, q.combatRequiredLevel);
            default:
                return true;
        }
    }

    public bool TryClaimQuestReward(QuestDefinition q)
    {
        if (!IsPlayerAliveForQuestClaim())
        {
            GameLog.Add("You must be alive to complete a quest.", GameLog.CannotMessageColor);
            return false;
        }

        if (!CanClaimReward(q))
            return false;

        int gatherToRestore = 0;
        string gatherItemToRestore = null;

        if (q.objectiveKind == QuestObjectiveKind.GatherItem)
        {
            if (!TryConsumeGatherItems(q, out int consumed, out string itemIdNorm))
                return false;

            gatherToRestore = consumed;
            gatherItemToRestore = itemIdNorm;
            ShowGatherConsumedPopup(consumed, itemIdNorm);
        }

        if (!CanReceiveAllItemRewards(q))
        {
            if (gatherToRestore > 0 && !string.IsNullOrEmpty(gatherItemToRestore))
                RestoreGatheredItems(gatherItemToRestore, gatherToRestore);

            GameLog.Add(
                "Inventory and storage are full — make space before you claim this reward.",
                GameLog.CannotMessageColor);
            return false;
        }

        GrantRewards(q);

        if (q.restockMerchantStockOnRewardClaim)
            TryResetMerchantStockFromQuestReward(q);

        TryUnlockTownServiceFromQuestReward(q);

        if (q.repeatable)
        {
            if (q.objectiveKind != QuestObjectiveKind.GatherItem)
                SetProgress(q.questId, 0);
            else
                ProgressChanged?.Invoke();

            // Hidden repeatables (e.g. shop restock) should leave the quest list after claim.
            // They can be accepted again from their source action (Restock button).
            if (q.hideFromQuestJournalUnlessAccepted && !string.IsNullOrWhiteSpace(q.questId))
            {
                string id = q.questId.Trim();
                _acceptedQuestIds.Remove(id);
                QuestTrackerState.UntrackQuest(id);
                ProgressChanged?.Invoke();
            }
        }
        else
        {
            MarkRewardClaimed(q.questId);
            RecomputeIdleCombatUnlockedFromClaimedRewards();
            DisableIdleCombatIfLocked();
            GameLog.QuestComplete(string.IsNullOrWhiteSpace(q.displayName) ? q.questId : q.displayName);
            ProgressChanged?.Invoke();
            TutorialQuestAfterClaim.Invoke(q);
            if (SaveManager.Instance != null)
                SaveManager.Instance.Save();

            TryAutoAcceptQuestsAfterPriorRewardClaimed(q.questId);
        }

        TryTeleportPlayerAfterClaim(q);

        return true;
    }

    /// <summary>
    /// Dev/testing only: grants quest rewards and marks the quest complete without objective / journal gating.
    /// Used by <see cref="DevTestingPanelUI"/> tutorial skip. Does not consume gather items.
    /// </summary>
    public void DevTestingForceCompleteQuestReward(
        QuestDefinition q,
        bool runTeleportAfterClaim,
        bool suppressQuestCompleteLog = false,
        bool skipSave = false)
    {
        if (q == null || string.IsNullOrWhiteSpace(q.questId))
            return;

        string id = q.questId.Trim();
        if (IsRewardClaimed(id))
            return;

        if (!IsQuestAccepted(q) && RequiresQuestGiver(q))
            _acceptedQuestIds.Add(id);

        GrantRewards(q);
        if (q.restockMerchantStockOnRewardClaim)
            TryResetMerchantStockFromQuestReward(q);

        TryUnlockTownServiceFromQuestReward(q);

        if (q.repeatable)
        {
            if (q.objectiveKind != QuestObjectiveKind.GatherItem)
                SetProgress(q.questId, 0);
            ProgressChanged?.Invoke();
            if (runTeleportAfterClaim)
                TryTeleportPlayerAfterClaim(q);
            return;
        }

        MarkRewardClaimed(id);
        RecomputeIdleCombatUnlockedFromClaimedRewards();
        DisableIdleCombatIfLocked();
        if (!suppressQuestCompleteLog)
            GameLog.QuestComplete(string.IsNullOrWhiteSpace(q.displayName) ? id : q.displayName);
        ProgressChanged?.Invoke();
        TutorialQuestAfterClaim.Invoke(q);
        if (!skipSave && SaveManager.Instance != null)
            SaveManager.Instance.Save();
        TryAutoAcceptQuestsAfterPriorRewardClaimed(id);
        if (runTeleportAfterClaim)
            TryTeleportPlayerAfterClaim(q);
    }

    private void RecomputeIdleCombatUnlockedFromClaimedRewards()
    {
        _idleCombatUnlocked = false;
        ResolveQuestDatabase();
        if (_resolvedDatabase == null)
            return;

        foreach (string id in _rewardClaimed)
        {
            QuestDefinition d = FindQuestDefinition(id);
            if (d && d.grantIdleCombatUnlockOnRewardClaim)
            {
                _idleCombatUnlocked = true;
                return;
            }
        }
    }

    private static void DisableIdleCombatIfLocked()
    {
        QuestProgressManager inst = Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (inst == null || inst._idleCombatUnlocked)
            return;

        PlayerCombatController combat =
            FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
        if (combat != null && combat.IdleCombatEnabled)
            combat.SetIdleCombatEnabled(false);
    }

    /// <summary>
    /// Accepts quests that opt in via <see cref="QuestDefinition.autoAcceptWhenPriorQuestRewardClaimed"/> now that
    /// <paramref name="priorQuestIdClaimed"/> had its reward claimed.
    /// </summary>
    private void TryAutoAcceptQuestsAfterPriorRewardClaimed(string priorQuestIdClaimed)
    {
        if (string.IsNullOrWhiteSpace(priorQuestIdClaimed))
            return;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return;

        string prior = priorQuestIdClaimed.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition followUp = all[i];
            if (!followUp || !followUp.autoAcceptWhenPriorQuestRewardClaimed)
                continue;
            if (string.IsNullOrWhiteSpace(followUp.autoAcceptAfterPriorQuestId))
                continue;
            if (!string.Equals(followUp.autoAcceptAfterPriorQuestId.Trim(), prior, StringComparison.Ordinal))
                continue;

            TryAcceptQuest(followUp, null);
        }
    }

    /// <summary>
    /// After loading a save, accepts any auto-accept quests whose prior is already reward-claimed.
    /// </summary>
    private void TryAutoAcceptQuestsAfterLoadHydration()
    {
        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return;

        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition followUp = all[i];
            if (!followUp || !followUp.autoAcceptWhenPriorQuestRewardClaimed)
                continue;
            if (string.IsNullOrWhiteSpace(followUp.autoAcceptAfterPriorQuestId))
                continue;
            if (!IsRewardClaimed(followUp.autoAcceptAfterPriorQuestId))
                continue;

            TryAcceptQuest(followUp, null);
        }
    }

    private static void TryTeleportPlayerAfterClaim(QuestDefinition q)
    {
        if (q == null || string.IsNullOrWhiteSpace(q.teleportPlayerToNodeIdOnCompletion))
            return;

        string nodeId = q.teleportPlayerToNodeIdOnCompletion.Trim();
        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition map = wmp ? wmp.WorldMap : null;
        if (!map)
            map = Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        if (!map)
            return;

        MapNodeDefinition target = map.FindNodeById(nodeId);
        if (target == null)
        {
            Debug.LogWarning($"[QuestProgressManager] Teleport node '{nodeId}' not found for quest '{q.questId}'.");
            return;
        }

        MapTravelSession.BeginTravel(target, MapTravelSession.EntryMethod.InWorldEntrance, logPendingLevel: false);
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(GameplaySceneName);
    }

    private void TryAutoCompleteGatherQuestsFromInventory()
    {
        if (_isAutoCompleteProcessing)
            return;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return;

        _isAutoCompleteProcessing = true;
        try
        {
            for (int i = 0; i < all.Count; i++)
            {
                QuestDefinition q = all[i];
                if (!q || !q.autoCompleteQuest || q.objectiveKind != QuestObjectiveKind.GatherItem)
                    continue;
                if (!CanClaimReward(q))
                    continue;
                if (HasItemRewardsToGrant(q) && !CanReceiveAllItemRewards(q))
                    continue;

                if (TryClaimQuestReward(q))
                    break;
            }
        }
        finally
        {
            _isAutoCompleteProcessing = false;
        }
    }

    private void TryAutoCompleteEligibleQuests()
    {
        if (_isAutoCompleteProcessing)
            return;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return;

        _isAutoCompleteProcessing = true;
        try
        {
            const int maxPasses = 16;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool claimedAny = false;
                for (int i = 0; i < all.Count; i++)
                {
                    QuestDefinition q = all[i];
                    if (!q || !q.autoCompleteQuest)
                        continue;
                    if (!CanClaimReward(q))
                        continue;

                    // Auto-claim should stay quiet when claim is blocked only by capacity.
                    if (HasItemRewardsToGrant(q) && !CanReceiveAllItemRewards(q))
                        continue;

                    if (TryClaimQuestReward(q))
                    {
                        claimedAny = true;
                        break;
                    }
                }

                if (!claimedAny)
                    break;
            }
        }
        finally
        {
            _isAutoCompleteProcessing = false;
        }
    }

    /// <summary>True when the quest grants at least one item stack (not gold-only).</summary>
    public bool HasItemRewardsToGrant(QuestDefinition q)
    {
        var stacks = new Dictionary<string, int>();
        CollectQuestItemRewardStacks(q, stacks);
        return stacks.Count > 0;
    }

    /// <summary>Whether inventory + storage can hold all item rewards at the current moment.</summary>
    public bool CanReceiveAllItemRewards(QuestDefinition q)
    {
        var stacks = new Dictionary<string, int>();
        CollectQuestItemRewardStacks(q, stacks);
        if (stacks.Count == 0)
            return true;

        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!inv)
            return false;

        PlayerStorage st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        foreach (KeyValuePair<string, int> kv in stacks)
        {
            int qty = kv.Value;
            if (qty <= 0)
                continue;

            int fitInv = inv.GetReceivableAmount(kv.Key, qty);
            int rest = qty - fitInv;
            int fitSt = st ? st.GetReceivableAmountFromExternal(kv.Key, rest) : 0;
            if (fitInv + fitSt < qty)
                return false;
        }

        return true;
    }

    private static void CollectQuestItemRewardStacks(QuestDefinition q, Dictionary<string, int> into)
    {
        if (q == null || into == null)
            return;

        void AddStack(string itemId, int qty)
        {
            if (string.IsNullOrWhiteSpace(itemId) || qty <= 0)
                return;
            itemId = itemId.Trim();
            if (into.TryGetValue(itemId, out int cur))
                into[itemId] = cur + qty;
            else
                into[itemId] = qty;
        }

        string mainId = q.rewardItem ? q.rewardItem.itemId : q.rewardItemId;
        if (!string.IsNullOrWhiteSpace(mainId))
            AddStack(mainId, Mathf.Max(1, q.rewardItemQuantity));

        if (q.additionalItemRewards == null)
            return;

        for (int i = 0; i < q.additionalItemRewards.Count; i++)
        {
            QuestItemReward r = q.additionalItemRewards[i];
            if (r == null)
                continue;

            string id = r.item ? r.item.itemId : r.itemId;
            AddStack(id, Mathf.Max(1, r.quantity));
        }
    }

    private void RestoreGatheredItems(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        PlayerStorage st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        int left = amount;
        if (inv != null)
            left -= inv.AddPartial(itemId, left, null, notifyItemGainPopup: false);

        if (left > 0 && st != null)
            left -= st.TryDepositAmountFromExternal(itemId, left);

        if (left > 0)
            Debug.LogWarning($"[QuestProgressManager] Could not restore {left}x {itemId} after a blocked claim.", this);
    }

    private bool TryConsumeGatherItems(QuestDefinition q, out int consumedAmount, out string itemIdNormalized)
    {
        consumedAmount = 0;
        itemIdNormalized = null;
        string rawId = q.objectiveId?.Trim();
        if (string.IsNullOrEmpty(rawId))
            return false;

        int need = q.targetCount;
        if (GetGatherItemCountLive(rawId) < need)
            return false;

        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        PlayerStorage st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

        int remaining = need;
        if (inv != null)
        {
            int inInv = inv.GetTotalAmount(rawId);
            int take = Mathf.Min(remaining, inInv);
            if (take > 0)
            {
                if (!inv.Remove(rawId, take))
                    return false;
                remaining -= take;
            }
        }

        if (remaining > 0 && st != null)
        {
            if (!st.Remove(rawId, remaining))
                return false;
            remaining = 0;
        }

        if (remaining != 0)
            return false;

        consumedAmount = need;
        itemIdNormalized = rawId;
        return true;
    }

    private void ShowGatherConsumedPopup(int amount, string itemId)
    {
        if (amount <= 0)
            return;

        string itemLabel = ResolveItemDisplayName(itemId);
        string msg = $"-{amount} {itemLabel}";
        GameLog.Add(msg, gatherConsumedPopupColor);
    }

    private static string ResolveItemDisplayName(string itemId)
    {
        ItemDatabase db = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        if (db)
        {
            ItemDefinition d = db.Get(itemId);
            if (d)
                return d.displayName;
        }

        return FormatItemIdFallback(itemId);
    }

    private static string FormatItemIdFallback(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "items";
        string[] parts = raw.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (p.Length == 0)
                continue;
            parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "");
        }

        return string.Join(" ", parts);
    }

    private void MarkRewardClaimed(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return;
        _rewardClaimed.Add(questId.Trim());
    }

    private void TryGrantRandomMapEnhancementReward(string nodeId, Inventory inv, PlayerStorage storage)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || inv == null)
            return;

        ItemDatabase itemDb = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        if (!itemDb)
            itemDb = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (!itemDb)
            return;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition worldMap = wmp ? wmp.WorldMap : null;
        if (!worldMap)
            worldMap = Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        MapNodeDefinition mapNode = worldMap != null ? worldMap.FindNodeById(nodeId) : null;
        if (mapNode == null)
            return;

        ItemDefinition template = itemDb.Get(MapCombatScalingSpecialDropDefaults.MapEnhancementTier1ItemId);
        if (template == null || !template.IsMapEnhancement)
            return;

        string rolledId = MapEnhancementService.CreateRolledDropItemId(template, mapNode, itemDb);
        if (string.IsNullOrWhiteSpace(rolledId))
            return;

        TryShowQuestRewardItemPopup(rolledId, 1);

        var invTouched = new List<int>(4);
        int toInv = inv.AddPartial(rolledId, 1, null, notifyItemGainPopup: true, invTouched);
        for (int i = 0; i < invTouched.Count; i++)
            AutoBattleLootHighlight.MarkInventorySlot(invTouched[i]);

        if (toInv >= 1 || storage == null)
            return;

        var stTouched = new List<int>(4);
        int toSt = storage.TryDepositAmountFromExternal(rolledId, 1, stTouched);
        for (int i = 0; i < stTouched.Count; i++)
            AutoBattleLootHighlight.MarkStorageSlot(stTouched[i]);

        if (toSt > 0)
        {
            string label = ResolveItemDisplayName(rolledId);
            GameLog.Add($"Inventory was full — sent {label} to storage.", questRewardToStorageLogColor);
        }
    }

    private void GrantRewards(QuestDefinition q)
    {
        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        int slotsToGrant = Mathf.Max(0, q.grantAdditionalInventorySlotsOnRewardClaim);
        if (slotsToGrant > 0 && inv)
            inv.UnlockAdditionalSlots(slotsToGrant);

        PlayerStorage storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        int mainStorageSlots = Mathf.Max(0, q.grantAdditionalMainStorageSlotsOnRewardClaim);
        if (mainStorageSlots > 0 && storage)
            storage.UnlockAdditionalTabSlots(StorageTabKind.Main, mainStorageSlots);

        int nonMainStorageSlots = Mathf.Max(0, q.grantAdditionalNonMainStorageSlotsOnRewardClaim);
        if (nonMainStorageSlots > 0 && storage)
        {
            for (int t = 1; t < PlayerStorage.TabCount; t++)
                storage.UnlockAdditionalTabSlots((StorageTabKind)t, nonMainStorageSlots);
        }

        SkillsManager skills = SkillsManager.Instance ??
            FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
        if (skills != null)
        {
            string source = string.IsNullOrWhiteSpace(q.displayName) ? "Quest reward" : $"Quest: {q.displayName}";

            int combatXp = Mathf.Max(0, q.grantCombatXpToAllCombatSkillsOnRewardClaim);
            if (combatXp > 0)
            {
                skills.AddXp(SkillType.Melee, combatXp, source);
                skills.AddXp(SkillType.Ranged, combatXp, source);
                skills.AddXp(SkillType.Magic, combatXp, source);
                skills.AddXp(SkillType.Endurance, combatXp, source);
            }

            int singleSkillXp = Mathf.Max(0, q.grantCombatSkillXpOnRewardClaim);
            if (singleSkillXp > 0)
                skills.AddXp(q.grantCombatSkillXpSkill, singleSkillXp, source);
        }

        if (!string.IsNullOrWhiteSpace(q.grantRandomMapEnhancementForNodeIdOnRewardClaim) && inv)
        {
            TryGrantRandomMapEnhancementReward(
                q.grantRandomMapEnhancementForNodeIdOnRewardClaim.Trim(),
                inv,
                storage);
        }

        if (q.rewardGold > 0)
        {
            CurrencyWallet w = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
            if (w)
                w.AddGold(q.rewardGold);

            GoldPopupSpawner popups = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
            if (popups)
                popups.ShowGoldGained(q.rewardGold);
        }

        var stacks = new Dictionary<string, int>();
        CollectQuestItemRewardStacks(q, stacks);
        if (stacks.Count == 0)
            return;

        if (!inv)
            return;

        foreach (KeyValuePair<string, int> kv in stacks)
        {
            string itemId = kv.Key;
            int qty = kv.Value;
            if (qty <= 0)
                continue;

            TryShowQuestRewardItemPopup(itemId, qty);

            var invTouched = new List<int>(8);
            int toInv = inv.AddPartial(itemId, qty, null, notifyItemGainPopup: true, invTouched);
            for (int i = 0; i < invTouched.Count; i++)
                AutoBattleLootHighlight.MarkInventorySlot(invTouched[i]);

            if (toInv >= qty)
                continue;

            if (!storage)
                continue;

            int remainder = qty - toInv;
            var stTouched = new List<int>(8);
            int toSt = storage.TryDepositAmountFromExternal(itemId, remainder, stTouched);
            for (int i = 0; i < stTouched.Count; i++)
                AutoBattleLootHighlight.MarkStorageSlot(stTouched[i]);

            if (toSt > 0)
            {
                string label = ResolveItemDisplayName(itemId);
                GameLog.Add($"Inventory was full — sent {toSt}x {label} to storage.", questRewardToStorageLogColor);
            }
        }

        AutoBattleLootHighlight.RefreshLootHighlightUIs();
    }

    private static void TryShowQuestRewardItemPopup(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        GoldPopupSpawner popups = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (popups != null)
            popups.ShowQuestRewardItemGained(itemId.Trim(), amount);
    }

    private void TryResetMerchantStockFromQuestReward(QuestDefinition q)
    {
        if (q == null || !q.restockMerchantStockOnRewardClaim)
            return;

        string key = q.restockMerchantStockSaveKey != null ? q.restockMerchantStockSaveKey.Trim() : "";
        if (string.IsNullOrEmpty(key))
            return;

        Merchant[] merchants = FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < merchants.Length; i++)
        {
            Merchant m = merchants[i];
            if (m == null || m.Stock == null)
                continue;
            if (!string.Equals(m.Stock.StockSaveKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            m.ResetStockToDefaults(persistToDisk: true);
            return;
        }
    }

    private void TryUnlockTownServiceFromQuestReward(QuestDefinition q)
    {
        if (q == null)
            return;

        string serviceId = q.unlockTownServiceIdOnRewardClaim != null
            ? q.unlockTownServiceIdOnRewardClaim.Trim()
            : "";
        if (string.IsNullOrEmpty(serviceId))
            return;

        if (!TownServiceUnlockStore.Unlock(serviceId, out bool wasNew) || !wasNew)
            return;

        string message = string.Equals(serviceId, TownServiceIds.Blacksmith, StringComparison.OrdinalIgnoreCase)
            ? "Draven the Blacksmith has returned to Duskwood."
            : $"A town service is now available in Duskwood.";
        GameLog.Add(message, GameLog.RegionUnlockedColor);
    }

    /// <summary>
    /// Accepts the first restock quest bound to this merchant stock key.
    /// Returns true when accepted now, false otherwise and sets a user-facing message.
    /// </summary>
    public bool TryAcceptShopRestockQuest(string merchantStockSaveKey, out string message)
    {
        message = "No restock quest configured.";
        if (string.IsNullOrWhiteSpace(merchantStockSaveKey))
            return false;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return false;

        string stockKey = merchantStockSaveKey.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || !q.restockMerchantStockOnRewardClaim)
                continue;
            if (!string.Equals(q.restockMerchantStockSaveKey?.Trim(), stockKey, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsPermanentlyComplete(q))
            {
                message = "This restock quest is complete.";
                return false;
            }

            if (IsQuestAccepted(q))
            {
                message = "Restock quest already active.";
                return false;
            }

            if (!TryAcceptQuest(q, q.obtainLocationId))
            {
                message = "Cannot accept restock quest right now.";
                return false;
            }

            message = $"Quest accepted: {q.displayName}";
            return true;
        }

        return false;
    }

    /// <summary>True when a restock quest for this merchant stock key is currently accepted and not permanently complete.</summary>
    public bool HasActiveShopRestockQuest(string merchantStockSaveKey)
    {
        if (string.IsNullOrWhiteSpace(merchantStockSaveKey))
            return false;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return false;

        string stockKey = merchantStockSaveKey.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || !q.restockMerchantStockOnRewardClaim)
                continue;
            if (!string.Equals(q.restockMerchantStockSaveKey?.Trim(), stockKey, StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsPermanentlyComplete(q))
                continue;
            if (IsQuestAccepted(q))
                return true;
        }

        return false;
    }

    /// <summary>True when at least one restock quest is configured for this merchant stock save key.</summary>
    public bool HasShopRestockQuestConfigured(string merchantStockSaveKey)
    {
        if (string.IsNullOrWhiteSpace(merchantStockSaveKey))
            return false;

        ResolveQuestDatabase();
        IReadOnlyList<QuestDefinition> all = _resolvedDatabase != null ? _resolvedDatabase.All : null;
        if (all == null || all.Count == 0)
            return false;

        string stockKey = merchantStockSaveKey.Trim();
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || !q.restockMerchantStockOnRewardClaim)
                continue;
            if (!string.Equals(q.restockMerchantStockSaveKey?.Trim(), stockKey, StringComparison.OrdinalIgnoreCase))
                continue;
            return true;
        }

        return false;
    }

    /// <summary>Call from enemy death; applies kill credit to active kill quests for the current map node.</summary>
    public void NotifyEnemyKilledForActiveMap(string killedEnemyId = null)
    {
        ResolveQuestDatabase();
        if (!_resolvedDatabase)
            return;

        string nodeId = ActiveLevelContext.Current != null ? ActiveLevelContext.Current.nodeId : "";
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            nodeId = GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId ?? nodeId;

        IReadOnlyList<QuestDefinition> all = _resolvedDatabase.All;
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || q.objectiveKind != QuestObjectiveKind.KillCount)
                continue;
            if (!q.repeatable && IsRewardClaimed(q.questId))
                continue;
            if (!IsQuestAccepted(q))
                continue;
            if (IsQuestGatedByPrerequisites(q))
                continue;

            string wantKillId = q.ResolveKillCreditEnemyId();
            if (!string.IsNullOrEmpty(wantKillId))
            {
                if (string.IsNullOrEmpty(killedEnemyId) ||
                    !string.Equals(wantKillId, killedEnemyId.Trim(), StringComparison.Ordinal))
                    continue;
            }

            bool mapScoped = q.killProgressOnlyOnProgressMap ||
                             !string.IsNullOrEmpty(q.progressMapNodeId?.Trim());
            if (mapScoped)
            {
                string wantNode = q.progressMapNodeId != null ? q.progressMapNodeId.Trim() : "";
                if (string.IsNullOrEmpty(wantNode))
                    continue;
                if (!string.Equals(wantNode, nodeId, StringComparison.Ordinal))
                    continue;
            }

            if (q.requiredMinMapScalingLevel > 0)
            {
                MapNodeDefinition activeNode = GameplayLevelBootstrapper.Instance != null
                    ? GameplayLevelBootstrapper.Instance.ActiveDefinition
                    : null;
                WorldMapProgressManager mapProgress = WorldMapProgressManager.Instance ??
                    FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
                int scalingLevel = activeNode != null && mapProgress != null
                    ? activeNode.GetCombatScalingLevel(mapProgress)
                    : 0;
                if (scalingLevel < q.requiredMinMapScalingLevel)
                    continue;
            }

            int amt = GetProgress(q.questId);
            if (amt >= q.targetCount)
                continue;

            AddProgress(q.questId, 1);
        }
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        if (data.questProgressIds == null)
            data.questProgressIds = new List<string>();
        if (data.questProgressAmounts == null)
            data.questProgressAmounts = new List<int>();
        if (data.questRewardClaimedIds == null)
            data.questRewardClaimedIds = new List<string>();
        if (data.acceptedQuestIds == null)
            data.acceptedQuestIds = new List<string>();
        if (data.trackedQuestIds == null)
            data.trackedQuestIds = new List<string>();

        QuestAcceptedEnemyRespawnService.SaveInto(data);

        data.questProgressIds.Clear();
        data.questProgressAmounts.Clear();

        ResolveQuestDatabase();
        foreach (var kv in _amounts)
        {
            if (string.IsNullOrEmpty(kv.Key))
                continue;
            QuestDefinition def = FindQuestDefinition(kv.Key);
            if (def != null && def.objectiveKind == QuestObjectiveKind.GatherItem)
                continue;
            data.questProgressIds.Add(kv.Key);
            data.questProgressAmounts.Add(kv.Value);
        }

        data.questRewardClaimedIds.Clear();
        foreach (string id in _rewardClaimed)
        {
            if (!string.IsNullOrEmpty(id))
                data.questRewardClaimedIds.Add(id);
        }

        data.acceptedQuestIds.Clear();
        foreach (string id in _acceptedQuestIds)
        {
            if (!string.IsNullOrEmpty(id))
                data.acceptedQuestIds.Add(id);
        }

        data.trackedQuestIds.Clear();
        IReadOnlyList<string> tracked = QuestTrackerState.OrderedTrackedQuestIds;
        for (int i = 0; i < tracked.Count; i++)
        {
            string id = tracked[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            string trimmed = id.Trim();
            QuestDefinition def = FindQuestDefinition(trimmed);
            if (!def)
                continue;
            if (IsPermanentlyComplete(def))
                continue;

            data.trackedQuestIds.Add(trimmed);
            if (data.trackedQuestIds.Count >= QuestTrackerState.MaxTrackedQuestCount)
                break;
        }
    }

    public void LoadFrom(SaveData data)
    {
        _amounts.Clear();
        _rewardClaimed.Clear();
        _acceptedQuestIds.Clear();

        if (data?.questProgressIds == null || data.questProgressAmounts == null)
        {
            LoadAcceptedQuestIds(data);
            QuestAcceptedEnemyRespawnService.LoadFromSave(data);
            RecomputeIdleCombatUnlockedFromClaimedRewards();
            DisableIdleCombatIfLocked();
            ProgressChanged?.Invoke();
            return;
        }

        ResolveQuestDatabase();
        int n = Mathf.Min(data.questProgressIds.Count, data.questProgressAmounts.Count);
        for (int i = 0; i < n; i++)
        {
            string id = data.questProgressIds[i];
            if (string.IsNullOrEmpty(id))
                continue;
            QuestDefinition def = FindQuestDefinition(id);
            if (def != null && def.objectiveKind == QuestObjectiveKind.GatherItem)
                continue;
            _amounts[id.Trim()] = Mathf.Max(0, data.questProgressAmounts[i]);
        }

        if (data.questRewardClaimedIds != null)
        {
            for (int i = 0; i < data.questRewardClaimedIds.Count; i++)
            {
                string id = data.questRewardClaimedIds[i];
                if (!string.IsNullOrEmpty(id))
                    _rewardClaimed.Add(id.Trim());
            }
        }

        LoadAcceptedQuestIds(data);
        QuestAcceptedEnemyRespawnService.LoadFromSave(data);

        if (data?.trackedQuestIds != null)
        {
            var restoredTracked = new List<string>(Mathf.Min(data.trackedQuestIds.Count, QuestTrackerState.MaxTrackedQuestCount));
            for (int i = 0; i < data.trackedQuestIds.Count; i++)
            {
                string id = data.trackedQuestIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                string trimmed = id.Trim();
                QuestDefinition def = FindQuestDefinition(trimmed);
                if (!def)
                    continue;
                if (IsPermanentlyComplete(def))
                    continue;

                restoredTracked.Add(trimmed);
                if (restoredTracked.Count >= QuestTrackerState.MaxTrackedQuestCount)
                    break;
            }

            QuestTrackerState.ReplaceTrackedQuestIds(restoredTracked);
            if (restoredTracked.Count > 0)
                QuestTrackerWindowUI.EnsureWindowOpenAfterTrack();
        }
        else
        {
            QuestTrackerState.ReplaceTrackedQuestIds(null);
        }

        ApplyTutorialStoryUnlocksForExistingSaves();

        RecomputeIdleCombatUnlockedFromClaimedRewards();
        DisableIdleCombatIfLocked();

        ProgressChanged?.Invoke();
        TryAutoCompleteEligibleQuests();
        TryAutoAcceptQuestsAfterLoadHydration();
    }

    private void ApplyTutorialStoryUnlocksForExistingSaves()
    {
        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp == null)
            return;

        if (IsRewardClaimed(TutorialQuestAfterClaim.LearningRopes2))
            wmp.UnlockNode(TutorialQuestAfterClaim.NodeTutorial2);

        if (IsRewardClaimed(TutorialQuestAfterClaim.BasicCombat2))
            wmp.UnlockNode(TutorialQuestAfterClaim.NodeTutorial3);
    }

    private void LoadAcceptedQuestIds(SaveData data)
    {
        if (data?.acceptedQuestIds == null)
            return;

        for (int i = 0; i < data.acceptedQuestIds.Count; i++)
        {
            string id = data.acceptedQuestIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                _acceptedQuestIds.Add(id.Trim());
        }
    }
}
