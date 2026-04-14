using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Quest/Quest Database", fileName = "QuestDatabase")]
public class QuestDatabase : ScriptableObject
{
    [SerializeField] private List<QuestDefinition> quests = new();

    public IReadOnlyList<QuestDefinition> All => quests;

    public void CollectForRegion(string regionId, List<QuestDefinition> into)
    {
        if (into == null || string.IsNullOrEmpty(regionId))
            return;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinition q = quests[i];
            if (!q) continue;
            if (q.regionId == regionId)
                into.Add(q);
        }
    }
}
