using System.Collections.Generic;

/// <summary>Merges content unlock rows with bonus rows for smelting/cooking proficiency panels.</summary>
public static class ProcessingProficiencyUnlockLines
{
    public readonly struct Row
    {
        public readonly int Level;
        public readonly string Description;

        public Row(int level, string description)
        {
            Level = level;
            Description = description;
        }
    }

    public static List<Row> MergeByLevel(IReadOnlyList<Row> contentRows, IReadOnlyList<Row> bonusRows)
    {
        int contentIndex = 0;
        int bonusIndex = 0;
        int contentCount = contentRows?.Count ?? 0;
        int bonusCount = bonusRows?.Count ?? 0;
        var merged = new List<Row>(contentCount + bonusCount);

        while (contentIndex < contentCount || bonusIndex < bonusCount)
        {
            int contentLevel = contentIndex < contentCount ? contentRows[contentIndex].Level : int.MaxValue;
            int bonusLevel = bonusIndex < bonusCount ? bonusRows[bonusIndex].Level : int.MaxValue;

            if (contentLevel <= bonusLevel)
            {
                merged.Add(contentRows[contentIndex]);
                contentIndex++;
            }
            else
            {
                merged.Add(bonusRows[bonusIndex]);
                bonusIndex++;
            }
        }

        return merged;
    }
}
