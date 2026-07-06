using System.Collections.Generic;
using UnityEngine;

public readonly struct BlacksmithingProficiencyBonuses
{
    public readonly float SpeedBonusPercent;
    public readonly float ResourceCostReductionPercent;
    public readonly float ActiveWorkSecondsPerClick;

    public BlacksmithingProficiencyBonuses(
        float speedBonusPercent,
        float resourceCostReductionPercent,
        float activeWorkSecondsPerClick)
    {
        SpeedBonusPercent = speedBonusPercent;
        ResourceCostReductionPercent = resourceCostReductionPercent;
        ActiveWorkSecondsPerClick = activeWorkSecondsPerClick;
    }

    public static BlacksmithingProficiencyBonuses ForLevel(int level) =>
        Calculate(Mathf.Clamp(level, 1, ProcessingSkillCurves.MaxLevel));

    public static BlacksmithingProficiencyBonuses Calculate(int level)
    {
        float speed = 0f;
        float costReduction = 0f;
        float activeWork = 1f;

        for (int i = 0; i < UnlockRows.Length; i++)
        {
            if (level < UnlockRows[i].Level)
                break;

            speed += UnlockRows[i].SpeedBonusPercent;
            costReduction += UnlockRows[i].ResourceCostReductionPercent;
            if (UnlockRows[i].DoubleActiveWorkSpeed)
                activeWork = 2f;
        }

        return new BlacksmithingProficiencyBonuses(speed, costReduction, activeWork);
    }

    public readonly struct UnlockRow
    {
        public readonly int Level;
        public readonly string Description;
        public readonly float SpeedBonusPercent;
        public readonly float ResourceCostReductionPercent;
        public readonly bool DoubleActiveWorkSpeed;

        public UnlockRow(
            int level,
            string description,
            float speedBonusPercent = 0f,
            float resourceCostReductionPercent = 0f,
            bool doubleActiveWorkSpeed = false)
        {
            Level = level;
            Description = description;
            SpeedBonusPercent = speedBonusPercent;
            ResourceCostReductionPercent = resourceCostReductionPercent;
            DoubleActiveWorkSpeed = doubleActiveWorkSpeed;
        }
    }

    public static readonly ProcessingProficiencyUnlockLines.Row[] ContentUnlockRows =
    {
        new(1, "Can craft stone gear"),
        new(5, "Can craft iron gear"),
        new(10, "Can craft mythril gear"),
        new(20, "Can craft runite gear"),
        new(30, "Can craft celestium gear"),
    };

    public static readonly UnlockRow[] UnlockRows =
    {
        new(5, "5% increased blacksmithing speed", speedBonusPercent: 5f),
        new(10, "5% increased blacksmithing speed", speedBonusPercent: 5f),
        new(15, "5% reduced resource costs", resourceCostReductionPercent: 5f),
        new(20, "5% increased blacksmithing speed", speedBonusPercent: 5f),
        new(25, "Double Speed Up effectiveness", doubleActiveWorkSpeed: true),
        new(30, "5% reduced resource costs", resourceCostReductionPercent: 5f),
        new(35, "10% increased blacksmithing speed", speedBonusPercent: 10f),
        new(40, "10% increased blacksmithing speed", speedBonusPercent: 10f),
        new(45, "5% increased blacksmithing speed", speedBonusPercent: 5f),
        new(50, "15% increased blacksmithing speed", speedBonusPercent: 15f),
    };

    public static List<ProcessingProficiencyUnlockLines.Row> BuildDisplayUnlockRows()
    {
        var content = new List<ProcessingProficiencyUnlockLines.Row>(ContentUnlockRows.Length);
        for (int i = 0; i < ContentUnlockRows.Length; i++)
            content.Add(ContentUnlockRows[i]);

        var bonus = new List<ProcessingProficiencyUnlockLines.Row>(UnlockRows.Length);
        for (int i = 0; i < UnlockRows.Length; i++)
            bonus.Add(new ProcessingProficiencyUnlockLines.Row(UnlockRows[i].Level, UnlockRows[i].Description));

        return ProcessingProficiencyUnlockLines.MergeByLevel(content, bonus);
    }
}
