using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class QuestItemReward
{
    public ItemDefinition item;
    [Tooltip("If set without Item, name is resolved via ItemDatabase in UI and granted by id.")]
    public string itemId = "";
    [Min(1)] public int quantity = 1;
}

[CreateAssetMenu(menuName = "Desktop Idle Game/Quest/Quest Definition", fileName = "Quest_")]
public class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id for saves and code (e.g. greenlands_slime_hunt).")]
    public string questId;

    [Tooltip("Matches RegionDefinition.regionId.")]
    public string regionId;

    [Tooltip("Shown in the quest list and details header.")]
    public string displayName = "New Quest";

    [TextArea(2, 6)]
    public string description = "";

    [TextArea(2, 8)]
    [Tooltip("Shown only in the quest journal (below Description). NPC quest dialogue uses Description only.")]
    public string details = "";

    [Header("List row")]
    [Tooltip("Right-hand label on the row (e.g. Town, Combat, Gathering).")]
    public string listCategoryLabel = "";

    [Tooltip("Replaces the automatic status line when set (Available / In progress / Ready / COMPLETE). Leave empty for defaults.")]
    public string listStatusOverride = "";

    [Header("Objective")]
    public QuestObjectiveKind objectiveKind = QuestObjectiveKind.KillCount;

    [Min(1)]
    public int targetCount = 1;

    [Tooltip(
        "GatherItem: ItemDefinition.itemId to collect.\n" +
        "KillCount: enemy id (EnemyDefinition.enemyId) shown in progress UI; also used for kill credit when Kill Enemy Id Filter is empty.")]
    [FormerlySerializedAs("gatherItemId")]
    public string objectiveId = "";

    [TextArea(1, 3)]
    [Tooltip("Optional custom objective text used by quest list/tracker for special objective kinds (e.g. DieOnce).")]
    public string specialObjectiveListText = "";

    [Header("Rewards (display)")]
    public int rewardGold;

    public ItemDefinition rewardItem;

    [Tooltip("If set without rewardItem, name is resolved via ItemDatabase in UI.")]
    public string rewardItemId = "";

    [Min(1)]
    [Tooltip("Stack size granted for Reward Item / Reward Item Id when the quest is completed.")]
    public int rewardItemQuantity = 1;

    [Tooltip("Optional extra item rewards granted alongside Reward Item / Reward Item Id.")]
    public List<QuestItemReward> additionalItemRewards = new();

    [TextArea(1, 3)]
    public string rewardNotes = "";

    [Header("Special rewards")]
    [Tooltip(
        "When this quest's reward is claimed (non-repeatable), Auto Battle (idle combat) unlocks for this character. " +
        "Can be enabled on multiple quests; any one claimed is enough to unlock.")]
    public bool grantIdleCombatUnlockOnRewardClaim;

    [Min(0)]
    [Tooltip("Additional inventory slots granted when this quest reward is claimed (e.g. 1, 4, 8).")]
    public int grantAdditionalInventorySlotsOnRewardClaim;

    [Header("Rules")]
    [Tooltip("If false, rewards can only be claimed once; the quest stays COMPLETE in the list.")]
    public bool repeatable;

    [Tooltip("When enabled, rewards are automatically claimed once objective + prerequisites are satisfied (no manual Complete Quest click).")]
    public bool autoCompleteQuest;

    [Tooltip("If true, the quest can be removed from the quest list and returned to its quest giver.")]
    public bool abandonable;

    [Header("Completion transition")]
    [Tooltip("Optional map node id to load immediately after this quest reward is claimed (e.g. tutorial_3). Leave empty for no teleport.")]
    public string teleportPlayerToNodeIdOnCompletion = "";

    [Header("Quest obtain location")]
    [Tooltip("Optional id of the QuestGiver this quest starts from. Empty keeps the old behavior: quest is available directly from the quest list.")]
    public string obtainLocationId = "";

    [Tooltip("Journal / list label for where to get this quest (e.g. Fletcher). If empty, Obtain Location Id is pretty-printed.")]
    public string obtainLocationDisplayName = "";

    [Tooltip("For kill quests: map node id where kills may count (see Kill Progress Only On This Map).")]
    public string progressMapNodeId = "";

    [Tooltip(
        "KillCount: when true, only kills on Progress Map Node Id count toward this quest (any enemy unless Kill Enemy Id Filter is set). " +
        "Progress Map Node Id must be set. When false, an empty Progress Map Node Id lets kills on any map count (legacy / rare).")]
    public bool killProgressOnlyOnProgressMap;

    [Header("Auto-accept after prior quest")]
    [Tooltip(
        "When enabled, after the quest below has had its reward claimed, this quest is accepted automatically " +
        "whenever rules allow (same checks as accepting from the quest giver / journal).")]
    public bool autoAcceptWhenPriorQuestRewardClaimed;

    [Tooltip("QuestDefinition.questId whose reward must be claimed first (e.g. tutorial_aid_merlin before Defeat Ivan).")]
    public string autoAcceptAfterPriorQuestId = "";

    [Header("Prerequisites")]
    [Tooltip("These quest ids must have had rewards claimed before this quest can be completed (claim).")]
    public List<string> prerequisiteRewardClaimedQuestIds = new();

    [Tooltip("If true, every prerequisite id must be claimed. If false, any one claimed suffices.")]
    public bool requireAllPrerequisiteQuests = true;

    [Tooltip("In addition to objective progress, this map node id must be marked completed (e.g. Tutorial 1 cleared via level-select / menu flow).")]
    public string requiredCompletedMapNodeId = "";

    [Header("Recommended location")]
    [Tooltip("Optional map shown by the quest details Enter Map button. This does not restrict where objective progress can be earned.")]
    [InspectorName("Optional Recommended Location")]
    public MapNodeDefinition recommendedLocationNode;

    [Tooltip("All listed skills must meet their levels before this quest can be completed.")]
    public List<SkillLevelRequirement> requiredSkillLevels = new();

    [Tooltip(
        "KillCount only: when set, only kills of this EnemyDefinition.enemyId count. " +
        "If empty, Objective Id is used for kill credit when it matches an enemy id.")]
    public string killEnemyIdFilter = "";

    [Header("Quest list visibility")]
    [Tooltip("If set, this quest is omitted from the quest list until WorldMapProgressManager unlocks this MapNodeDefinition.nodeId (e.g. tutorial_2). Prerequisites still control Locked vs Available once visible.")]
    public string hideUntilMapNodeUnlockedId = "";

    [Header("Ordering")]
    public int sortOrder;

    /// <summary>False when <see cref="hideUntilMapNodeUnlockedId"/> is set and that node is not map-unlocked yet.</summary>
    public bool IsShownInQuestList(WorldMapProgressManager mapProgress)
    {
        if (string.IsNullOrWhiteSpace(hideUntilMapNodeUnlockedId))
            return true;
        if (mapProgress == null)
            return false;
        return mapProgress.IsNodeUnlocked(hideUntilMapNodeUnlockedId.Trim());
    }

    public bool IsComplete(int currentAmount)
    {
        if (objectiveKind == QuestObjectiveKind.None)
            return false;
        int required = Mathf.Max(1, targetCount);
        return currentAmount >= required;
    }

    /// <summary>KillCount: which enemy id must die for credit when <see cref="killEnemyIdFilter"/> is empty.</summary>
    public string ResolveKillCreditEnemyId()
    {
        if (objectiveKind != QuestObjectiveKind.KillCount)
            return "";
        if (!string.IsNullOrWhiteSpace(killEnemyIdFilter))
            return killEnemyIdFilter.Trim();
        if (!string.IsNullOrWhiteSpace(objectiveId))
            return objectiveId.Trim();
        return "";
    }

    /// <summary>KillCount: enemy id used to resolve display name (objective id first, then kill filter).</summary>
    public string ResolveKillDisplayEnemyId()
    {
        if (objectiveKind != QuestObjectiveKind.KillCount)
            return "";
        if (!string.IsNullOrWhiteSpace(objectiveId))
            return objectiveId.Trim();
        if (!string.IsNullOrWhiteSpace(killEnemyIdFilter))
            return killEnemyIdFilter.Trim();
        return "";
    }
}
