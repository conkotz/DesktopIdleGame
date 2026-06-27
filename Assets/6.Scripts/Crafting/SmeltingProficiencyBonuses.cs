using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct SmeltingProficiencyBonuses
{
    public readonly float SpeedBonusPercent;
    public readonly float DoubleBarChancePercent;
    public readonly float ActiveWorkSecondsPerClick;

    public SmeltingProficiencyBonuses(float speedBonusPercent, float doubleBarChancePercent, float activeWorkSecondsPerClick)
    {
        SpeedBonusPercent = speedBonusPercent;
        DoubleBarChancePercent = doubleBarChancePercent;
        ActiveWorkSecondsPerClick = activeWorkSecondsPerClick;
    }

    public static SmeltingProficiencyBonuses ForLevel(int level) => Calculate(Mathf.Clamp(level, 1, ProcessingSkillCurves.MaxLevel));

    public static SmeltingProficiencyBonuses Calculate(int level)
    {
        float speed = 0f;
        float doubleBar = 0f;
        float activeWork = 1f;

        for (int i = 0; i < UnlockRows.Length; i++)
        {
            if (level < UnlockRows[i].Level)
                break;

            speed += UnlockRows[i].SpeedBonusPercent;
            doubleBar += UnlockRows[i].DoubleBarChancePercent;
            if (UnlockRows[i].DoubleActiveWorkSpeed)
                activeWork = 2f;
        }

        return new SmeltingProficiencyBonuses(speed, doubleBar, activeWork);
    }

    public readonly struct UnlockRow
    {
        public readonly int Level;
        public readonly string Description;
        public readonly float SpeedBonusPercent;
        public readonly float DoubleBarChancePercent;
        public readonly bool DoubleActiveWorkSpeed;

        public UnlockRow(
            int level,
            string description,
            float speedBonusPercent = 0f,
            float doubleBarChancePercent = 0f,
            bool doubleActiveWorkSpeed = false)
        {
            Level = level;
            Description = description;
            SpeedBonusPercent = speedBonusPercent;
            DoubleBarChancePercent = doubleBarChancePercent;
            DoubleActiveWorkSpeed = doubleActiveWorkSpeed;
        }
    }

    public static readonly UnlockRow[] UnlockRows =
    {
        new(5, "5% increased smelting speed", speedBonusPercent: 5f),
        new(10, "5% increased smelting speed", speedBonusPercent: 5f),
        new(15, "3% chance to make 2 bars instead of 1", doubleBarChancePercent: 3f),
        new(20, "5% increased smelting speed", speedBonusPercent: 5f),
        new(25, "Double Speed Up effectiveness", doubleActiveWorkSpeed: true),
        new(30, "3% chance to make 2 bars instead of 1", doubleBarChancePercent: 3f),
        new(35, "10% increased smelting speed", speedBonusPercent: 10f),
        new(40, "10% increased smelting speed", speedBonusPercent: 10f),
        new(45, "4% chance to make 2 bars instead of 1", doubleBarChancePercent: 4f),
        new(50, "15% increased smelting speed, 5% chance to make 2 bars instead of 1", speedBonusPercent: 15f, doubleBarChancePercent: 5f),
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
