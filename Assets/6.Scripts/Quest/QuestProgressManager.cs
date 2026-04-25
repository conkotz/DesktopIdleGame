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

    [SerializeField] private QuestDatabase questDatabase;

    [Header("Gather quest — popup")]
    [SerializeField] private Color gatherConsumedPopupColor = new Color(0.85f, 0.35f, 0.3f, 1f);

    private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rewardClaimed = new(StringComparer.Ordinal);

    private QuestDatabase _resolvedDatabase;

    public event Action ProgressChanged;

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
        if (Instance == this)
            Instance = null;
    }

    private void ResolveQuestDatabase()
    {
        if (questDatabase)
            _resolvedDatabase = questDatabase;
        else
            _resolvedDatabase = Resources.Load<QuestDatabase>("Databases/QuestDatabase_Main");
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

    /// <summary>Kill quests: saved progress. Gather quests: inventory + storage total (not saved in _amounts).</summary>
    public int GetDisplayProgress(QuestDefinition q)
    {
        if (!q)
            return 0;
        if (q.objectiveKind == QuestObjectiveKind.GatherItem)
            return GetGatherItemCountLive(q.gatherItemId);
        return GetProgress(q.questId);
    }

    public int GetGatherItemCountLive(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;
        itemId = itemId.Trim();
        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        PlayerStorage st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        int n = inv ? inv.GetTotalAmount(itemId) : 0;
        if (st)
            n += st.GetTotalAmount(itemId);
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
        if (!q || string.IsNullOrEmpty(q.questId) || q.objectiveKind == QuestObjectiveKind.None)
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

    public bool AreSkillRequirementsSatisfied(QuestDefinition q)
    {
        if (q == null || q.requiredSkillLevels == null || q.requiredSkillLevels.Count == 0)
            return true;

        SkillsManager skills = SkillsManager.Instance ??
            FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
        if (skills == null)
            return false;

        for (int i = 0; i < q.requiredSkillLevels.Count; i++)
        {
            SkillLevelRequirement req = q.requiredSkillLevels[i];
            if (req == null || req.requiredLevel <= 0)
                continue;
            if (!skills.IsLevelUnlocked(req.skill, req.requiredLevel))
                return false;
        }

        return true;
    }

    public bool TryClaimQuestReward(QuestDefinition q)
    {
        if (!CanClaimReward(q))
            return false;

        if (q.objectiveKind == QuestObjectiveKind.GatherItem)
        {
            if (!TryConsumeGatherItems(q, out int consumed, out string itemIdNorm))
                return false;
            ShowGatherConsumedPopup(consumed, itemIdNorm);
        }

        GrantRewards(q);

        if (q.repeatable)
        {
            if (q.objectiveKind != QuestObjectiveKind.GatherItem)
                SetProgress(q.questId, 0);
            else
                ProgressChanged?.Invoke();
        }
        else
        {
            MarkRewardClaimed(q.questId);
            ProgressChanged?.Invoke();
            TutorialQuestAfterClaim.Invoke(q);
            if (SaveManager.Instance != null)
                SaveManager.Instance.Save();
        }

        return true;
    }

    private bool TryConsumeGatherItems(QuestDefinition q, out int consumedAmount, out string itemIdNormalized)
    {
        consumedAmount = 0;
        itemIdNormalized = null;
        string rawId = q.gatherItemId?.Trim();
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
        GoldPopupSpawner spawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (!spawner || amount <= 0)
            return;

        Transform anchor = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include)?.transform;
        if (!anchor)
            return;

        string itemLabel = ResolveItemDisplayName(itemId);
        string msg = $"-{amount} {itemLabel}";
        Vector3 worldPos = anchor.position + Vector3.up * 1.2f;
        spawner.ShowMessageAtWorld(worldPos, msg, gatherConsumedPopupColor);
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

    private void GrantRewards(QuestDefinition q)
    {
        if (q.rewardGold > 0)
        {
            CurrencyWallet w = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
            if (w)
                w.AddGold(q.rewardGold);

            GoldPopupSpawner popups = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
            if (popups)
                popups.ShowGoldGained(q.rewardGold);
        }

        string itemId = q.rewardItem ? q.rewardItem.itemId : q.rewardItemId;
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
            if (inv)
                inv.Add(itemId.Trim(), Mathf.Max(1, q.rewardItemQuantity));
        }
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
            if (IsQuestGatedByPrerequisites(q))
                continue;

            if (!string.IsNullOrEmpty(q.killEnemyIdFilter))
            {
                string want = q.killEnemyIdFilter.Trim();
                if (string.IsNullOrEmpty(killedEnemyId) ||
                    !string.Equals(want, killedEnemyId.Trim(), StringComparison.Ordinal))
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
    }

    public void LoadFrom(SaveData data)
    {
        _amounts.Clear();
        _rewardClaimed.Clear();

        if (data?.questProgressIds == null || data.questProgressAmounts == null)
        {
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

        ProgressChanged?.Invoke();
    }
}
