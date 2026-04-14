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

    [Tooltip("Smaller status line (e.g. Unlocked). Leave empty to hide.")]
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

    [Header("Ordering")]
    public int sortOrder;

    public bool IsComplete(int currentAmount)
    {
        if (objectiveKind == QuestObjectiveKind.None)
            return false;
        return currentAmount >= targetCount;
    }
}
