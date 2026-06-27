using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CookingProficiencyBonuses
{
    public const float BaseBurnChancePercent = 50f;

    public readonly float SpeedBonusPercent;
    public readonly float BurnRateReductionPercent;
    public readonly float ActiveWorkSecondsPerClick;

    public float EffectiveBurnChancePercent =>
        Mathf.Max(0f, BaseBurnChancePercent - BurnRateReductionPercent);

    public CookingProficiencyBonuses(float speedBonusPercent, float burnRateReductionPercent, float activeWorkSecondsPerClick)
    {
        SpeedBonusPercent = speedBonusPercent;
        BurnRateReductionPercent = burnRateReductionPercent;
        ActiveWorkSecondsPerClick = activeWorkSecondsPerClick;
    }

    public static CookingProficiencyBonuses ForLevel(int level) => Calculate(Mathf.Clamp(level, 1, ProcessingSkillCurves.MaxLevel));

    public static CookingProficiencyBonuses Calculate(int level)
    {
        float speed = 0f;
        float burnReduction = 0f;
        float activeWork = 1f;

        for (int i = 0; i < UnlockRows.Length; i++)
        {
            if (level < UnlockRows[i].Level)
                break;

            speed += UnlockRows[i].SpeedBonusPercent;
            burnReduction += UnlockRows[i].BurnRateReductionPercent;
            if (UnlockRows[i].DoubleActiveWorkSpeed)
                activeWork = 2f;
        }

        return new CookingProficiencyBonuses(speed, burnReduction, activeWork);
    }

    public readonly struct UnlockRow
    {
        public readonly int Level;
        public readonly string Description;
        public readonly float SpeedBonusPercent;
        public readonly float BurnRateReductionPercent;
        public readonly bool DoubleActiveWorkSpeed;

        public UnlockRow(
            int level,
            string description,
            float speedBonusPercent = 0f,
            float burnRateReductionPercent = 0f,
            bool doubleActiveWorkSpeed = false)
        {
            Level = level;
            Description = description;
            SpeedBonusPercent = speedBonusPercent;
            BurnRateReductionPercent = burnRateReductionPercent;
            DoubleActiveWorkSpeed = doubleActiveWorkSpeed;
        }
    }

    public static readonly UnlockRow[] UnlockRows =
    {
        new(5, "5% increased cooking speed", speedBonusPercent: 5f),
        new(10, "5% increased cooking speed", speedBonusPercent: 5f),
        new(15, "5% reduced burn chance", burnRateReductionPercent: 5f),
        new(20, "5% increased cooking speed", speedBonusPercent: 5f),
        new(25, "Double Speed Up effectiveness", doubleActiveWorkSpeed: true),
        new(30, "5% reduced burn chance", burnRateReductionPercent: 5f),
        new(35, "10% increased cooking speed", speedBonusPercent: 10f),
        new(40, "10% increased cooking speed", speedBonusPercent: 10f),
        new(45, "5% reduced burn chance", burnRateReductionPercent: 5f),
        new(50, "15% increased cooking speed, 5% reduced burn chance", speedBonusPercent: 15f, burnRateReductionPercent: 5f),
    };

    public static IReadOnlyList<string> BuildUnlockLines()
    {
        var lines = new List<string>(UnlockRows.Length);
        for (int i = 0; i < UnlockRows.Length; i++)
        {
            UnlockRow row = UnlockRows[i];
            lines.Add($"Lv {row.Level}: {row.Description}");
        }

        return lines;
    }
}
