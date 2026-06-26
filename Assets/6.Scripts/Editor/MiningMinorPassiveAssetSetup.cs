#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Applies the mining minor-passive tree to mining.asset. Run via Tools/Skills/Apply Mining Minor Passives.</summary>
public static class MiningMinorPassiveAssetSetup
{
    private const string MiningSkillPath = "Assets/3.ScriptableObjects/SkillsDefinitions/mining.asset";
    private const string PassiveIconGuid = "7adac55ced66def4ca2ad1c0880b555c";

    private readonly struct PassiveSpec
    {
        public readonly int Level;
        public readonly string Title;
        public readonly string Description;
        public readonly MiningMinorNodeStatOption Option;

        public PassiveSpec(int level, string title, string description, MiningMinorNodeStatOption option)
        {
            Level = level;
            Title = title;
            Description = description;
            Option = option;
        }
    }

    private static readonly PassiveSpec[] Passives =
    {
        new(2, "Mining Speed 1", "+2% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent2),
        new(3, "Mining Efficiency 1", "+1% Mining Stamina Efficiency", MiningMinorNodeStatOption.MiningStaminaEfficiencyPercent1),
        new(4, "Mining Grit 1", "+1% Mining Grit Chance", MiningMinorNodeStatOption.MiningGritPercent1),
        new(6, "Mining Bonus Find 1", "+1% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent1),
        new(7, "Mining Speed 2", "+2% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent2),
        new(8, "Mining Efficiency 2", "+1% Mining Stamina Efficiency", MiningMinorNodeStatOption.MiningStaminaEfficiencyPercent1),
        new(9, "Mining Grit Restore 1", "Mining Grit procs restore +10 Stamina", MiningMinorNodeStatOption.MiningGritRestoreStaminaFlat10),
        new(10, "Mining Grit 2", "+1% Mining Grit Chance", MiningMinorNodeStatOption.MiningGritPercent1),
        new(11, "Mining Bonus Find 2", "+1% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent1),
        new(12, "Mining Speed 3", "+2% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent2),
        new(13, "Mining Efficiency 3", "+2% Mining Stamina Efficiency", MiningMinorNodeStatOption.MiningStaminaEfficiencyPercent2),
        new(14, "Mining Double XP 1", "3% chance to gain double Mining XP", MiningMinorNodeStatOption.MiningDoubleXpChancePercent3),
        new(16, "Mining Grit 3", "+2% Mining Grit Chance", MiningMinorNodeStatOption.MiningGritPercent2Skill),
        new(17, "Mining Speed 4", "+2% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent2),
        new(18, "Gem Finder 1", "+2% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent2),
        new(19, "Tireless Swing 1", "3% chance mining actions cost no stamina", MiningMinorNodeStatOption.MiningNoStaminaSwingChancePercent3),
        new(20, "Mining Efficiency 4", "+2% Mining Stamina Efficiency", MiningMinorNodeStatOption.MiningStaminaEfficiencyPercent2),
        new(21, "Ore Conservation 1", "+10% chance this mining tick does not count toward ore depletion", MiningMinorNodeStatOption.MiningChanceNotToCountTowardOreDepletionPercent10),
        new(22, "Mining Speed 5", "+3% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent3),
        new(23, "Gem Finder 2", "+2% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent2),
        new(24, "Mining Momentum 1", "After Mining Grit: +5% Mining Speed for 7 seconds", MiningMinorNodeStatOption.MiningMomentumAfterGritSpeedPercent5Duration7s),
        new(26, "Mining Speed 6", "+3% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent3),
        new(27, "Mining Efficiency 5", "+2% Mining Stamina Efficiency", MiningMinorNodeStatOption.MiningStaminaEfficiencyPercent2),
        new(28, "Mining Grit Restore 2", "Mining Grit procs restore +10 Stamina", MiningMinorNodeStatOption.MiningGritRestoreStaminaFlat10),
        new(29, "Mining Grit 4", "+2% Mining Grit Chance", MiningMinorNodeStatOption.MiningGritPercent2Skill),
        new(30, "Gem Finder 3", "+3% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent3),
        new(31, "Deep Focus 1", "While continuously mining: +3% Speed, +3% Stamina Efficiency", MiningMinorNodeStatOption.MiningDeepFocusContinuousSpeedPercent3EfficiencyPercent3),
        new(32, "Ore Conservation 2", "+10% chance this mining tick does not count toward ore depletion", MiningMinorNodeStatOption.MiningChanceNotToCountTowardOreDepletionPercent10),
        new(33, "Mining Double XP 2", "3% chance to gain double Mining XP", MiningMinorNodeStatOption.MiningDoubleXpChancePercent3),
        new(34, "Rare Gem Discovery 1", "+2% Rare Gem Discovery when finding a gem", MiningMinorNodeStatOption.MiningRareGemUpgradeChancePercent2),
        new(36, "Mining Speed 7", "+3% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent3),
        new(37, "Mining Efficiency 6", "+3% Mining Stamina Efficiency", MiningMinorNodeStatOption.MiningStaminaEfficiencyPercent3),
        new(38, "Gem Finder 4", "+3% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent3),
        new(39, "Tireless Swing 2", "3% chance mining actions cost no stamina", MiningMinorNodeStatOption.MiningNoStaminaSwingChancePercent3),
        new(40, "Mining Grit 5", "+3% Mining Grit Chance", MiningMinorNodeStatOption.MiningGritPercent3),
        new(41, "Mining Speed 8", "+4% Mining Speed", MiningMinorNodeStatOption.MiningSpeedPercent4),
        new(42, "Mining Momentum 2", "After Mining Grit: +5% Mining Speed for 7 seconds", MiningMinorNodeStatOption.MiningMomentumAfterGritSpeedPercent5Duration7s),
        new(43, "Gem Finder 5", "+5% Mining Bonus Find Chance", MiningMinorNodeStatOption.MiningBonusFindPercent5),
        new(44, "Deep Focus 2", "While continuously mining: +3% Speed, +3% Stamina Efficiency", MiningMinorNodeStatOption.MiningDeepFocusContinuousSpeedPercent3EfficiencyPercent3),
        new(46, "Ore Conservation 3", "+10% chance this mining tick does not count toward ore depletion", MiningMinorNodeStatOption.MiningChanceNotToCountTowardOreDepletionPercent10),
        new(47, "Mining Grit 6", "+3% Mining Grit Chance", MiningMinorNodeStatOption.MiningGritPercent3),
        new(48, "Rare Gem Discovery 2", "+4% Rare Gem Discovery when finding a gem", MiningMinorNodeStatOption.MiningRareGemUpgradeChancePercent4),
        new(49, "Mining Mastery", "5% chance a Mining Grit proc immediately triggers a second grit", MiningMinorNodeStatOption.MiningMasteryDoubleGritChancePercent5),
    };

    [MenuItem("Tools/Skills/Apply Mining Minor Passives")]
    public static void ApplySetup()
    {
        SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(MiningSkillPath);
        if (!skill)
        {
            Debug.LogError($"[MiningMinorPassiveAssetSetup] Missing skill asset at {MiningSkillPath}");
            return;
        }

        Sprite passiveIcon = LoadPassiveIcon();
        var kept = new List<SkillUnlockDefinition>();
        foreach (SkillUnlockDefinition unlock in skill.unlocks)
        {
            if (unlock == null)
                continue;
            if (unlock.unlockType == SkillUnlockType.MinorPassive)
                continue;
            kept.Add(unlock);
        }

        foreach (PassiveSpec spec in Passives)
        {
            kept.Add(new SkillUnlockDefinition
            {
                requiredLevel = spec.Level,
                title = spec.Title,
                description = spec.Description,
                icon = passiveIcon,
                unlockType = SkillUnlockType.MinorPassive,
                miningMinorStatOption = spec.Option
            });
        }

        kept.Sort((a, b) =>
        {
            int c = a.requiredLevel.CompareTo(b.requiredLevel);
            if (c != 0)
                return c;
            bool aMinor = a.unlockType == SkillUnlockType.MinorPassive;
            bool bMinor = b.unlockType == SkillUnlockType.MinorPassive;
            if (aMinor != bMinor)
                return aMinor ? 1 : -1;
            return string.Compare(a.title, b.title, System.StringComparison.Ordinal);
        });

        skill.unlocks = kept;
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MiningMinorPassiveAssetSetup] Applied {Passives.Length} mining minor passives to {MiningSkillPath}.");
    }

    private static Sprite LoadPassiveIcon()
    {
        string path = AssetDatabase.GUIDToAssetPath(PassiveIconGuid);
        if (string.IsNullOrEmpty(path))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
                return sprite;
        }

        return null;
    }
}
#endif
