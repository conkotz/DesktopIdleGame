using UnityEngine;

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

    [Header("List row")]
    [Tooltip("Right-hand label on the row (e.g. Town, Combat, Gathering).")]
    public string listCategoryLabel = "";

    [Tooltip("Replaces the automatic status line when set (Available / In progress / Ready / COMPLETE). Leave empty for defaults.")]
    public string listStatusOverride = "";

    [Header("Objective")]
    public QuestObjectiveKind objectiveKind = QuestObjectiveKind.KillCount;

    [Min(1)]
    public int targetCount = 1;

    [Tooltip("For GatherItem: ItemDefinition.itemId")]
    public string gatherItemId = "";

    [Header("Rewards (display)")]
    public int rewardGold;

    public ItemDefinition rewardItem;

    [Tooltip("If set without rewardItem, name is resolved via ItemDatabase in UI.")]
    public string rewardItemId = "";

    [TextArea(1, 3)]
    public string rewardNotes = "";

    [Header("Rules")]
    [Tooltip("If false, rewards can only be claimed once; the quest stays COMPLETE in the list.")]
    public bool repeatable;

    [Tooltip("For kill quests: only increments while this MapNodeDefinition.nodeId is active (empty = any map).")]
    public string progressMapNodeId = "";

    [Header("Ordering")]
    public int sortOrder;

    public bool IsComplete(int currentAmount)
    {
        if (objectiveKind == QuestObjectiveKind.None)
            return false;
        return currentAmount >= targetCount;
    }
}
