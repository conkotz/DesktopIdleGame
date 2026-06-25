#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Applies the Spellcraft minor-passive tree to magic.asset. Run via Tools/Skills/Apply Magic Minor Passives.</summary>
public static class MagicMinorPassiveAssetSetup
{
    private const string MagicSkillPath = "Assets/3.ScriptableObjects/SkillsDefinitions/magic.asset";
    private const string PassiveIconGuid = "7adac55ced66def4ca2ad1c0880b555c";
    private const string PassiveIconFileId = "3534972268811701710";

    private readonly struct PassiveSpec
    {
        public readonly int Level;
        public readonly string Title;
        public readonly string Description;
        public readonly MagicMinorNodeStatOption Option;

        public PassiveSpec(int level, string title, string description, MagicMinorNodeStatOption option)
        {
            Level = level;
            Title = title;
            Description = description;
            Option = option;
        }
    }

    private static readonly PassiveSpec[] Passives =
    {
        new(2, "Spellcraft I", "+5% Spell Damage", MagicMinorNodeStatOption.SpellDamagePercent5),
        new(3, "Spellcraft II", "+5% Spell Damage", MagicMinorNodeStatOption.SpellDamagePercent5),
        new(4, "Arcane Flow I", "+2% Cooldown Reduction", MagicMinorNodeStatOption.CooldownReductionPercent2),
        new(6, "Spellcraft III", "+5% Spell Damage", MagicMinorNodeStatOption.SpellDamagePercent5),
        new(7, "Arcane Precision I", "+2% Crit Chance", MagicMinorNodeStatOption.CritChancePercent2),
        new(8, "Arcane Flow II", "+2% Cooldown Reduction", MagicMinorNodeStatOption.CooldownReductionPercent2),
        new(9, "Rune Conservation I", "+5% Chance to not Consume a Rune", MagicMinorNodeStatOption.RuneConservationPercent5),
        new(11, "Arcane Knowledge I", "+50 Max Mana", MagicMinorNodeStatOption.MaxManaFlat50),
        new(12, "Spellcraft IV", "+5% Spell Damage", MagicMinorNodeStatOption.SpellDamagePercent5),
        new(13, "Arcane Flow III", "+2% Cooldown Reduction", MagicMinorNodeStatOption.CooldownReductionPercent2),
        new(14, "Mana Flow I", "+2 Mana Regeneration per second", MagicMinorNodeStatOption.ManaRegenFlat2),
        new(16, "Fire Mastery I", "+5% Fire Damage", MagicMinorNodeStatOption.FireDamagePercent5),
        new(17, "Burning Touch I", "+10% Burn Chance", MagicMinorNodeStatOption.BurnChancePercent10),
        new(18, "Arcane Precision II", "+2% Crit Chance", MagicMinorNodeStatOption.CritChancePercent2),
        new(19, "Burning Embers I", "Burn Tick Rate +0.25/s", MagicMinorNodeStatOption.BurnTickIntervalReduction025),
        new(21, "Spellcraft V", "+5% Spell Damage", MagicMinorNodeStatOption.SpellDamagePercent5),
        new(22, "Arcane Flow IV", "+2% Cooldown Reduction", MagicMinorNodeStatOption.CooldownReductionPercent2),
        new(23, "Frost Mastery I", "+5% Ice Damage", MagicMinorNodeStatOption.IceDamagePercent5),
        new(24, "Arcane Fury I", "+8% Crit Multiplier", MagicMinorNodeStatOption.CritMultiplierPercent8),
        new(26, "Lightning Mastery I", "+5% Lightning Damage", MagicMinorNodeStatOption.LightningDamagePercent5),
        new(27, "Static Touch I", "+10% Shock Chance", MagicMinorNodeStatOption.ShockChancePercent10),
        new(28, "Inferno I", "+4% Burn Multiplier", MagicMinorNodeStatOption.BurnMultiplierPercent4),
        new(29, "Storm Surge I", "+5% Chance for Lightning Damage to be Lucky", MagicMinorNodeStatOption.LightningLuckyChancePercent5),
        new(31, "Arcane Knowledge II", "+50 Max Mana", MagicMinorNodeStatOption.MaxManaFlat50),
        new(32, "Arcane Flow V", "+2% Cooldown Reduction", MagicMinorNodeStatOption.CooldownReductionPercent2),
        new(33, "Archmage's Insight I", "+4% Spell Damage while above 70% Mana", MagicMinorNodeStatOption.SpellDamageAbove70ManaPercent4),
        new(34, "Arcane Fury II", "+8% Crit Multiplier", MagicMinorNodeStatOption.CritMultiplierPercent8),
        new(36, "Frozen Touch I", "+10% Chill Chance", MagicMinorNodeStatOption.ChillChancePercent10),
        new(37, "Deep Freeze I", "10% Chance to Apply 2 Chill Stacks", MagicMinorNodeStatOption.DoubleChillStackChancePercent10),
        new(38, "Rune Conservation II", "+5% Chance to not Consume a Rune", MagicMinorNodeStatOption.RuneConservationPercent5),
        new(39, "Mana Flow II", "+3 Mana Regeneration per second", MagicMinorNodeStatOption.ManaRegenFlat3),
        new(41, "Spellcraft VI", "+5% Spell Damage", MagicMinorNodeStatOption.SpellDamagePercent5),
        new(41, "Arcane Knowledge III", "+50 Max Mana", MagicMinorNodeStatOption.MaxManaFlat50),
        new(42, "Arcane Flow VI", "+2% Cooldown Reduction", MagicMinorNodeStatOption.CooldownReductionPercent2),
        new(43, "Arcane Precision III", "+2% Crit Chance", MagicMinorNodeStatOption.CritChancePercent2),
        new(44, "Arcane Fury III", "+8% Crit Multiplier", MagicMinorNodeStatOption.CritMultiplierPercent8),
        new(46, "Inferno II", "+4% Burn Multiplier", MagicMinorNodeStatOption.BurnMultiplierPercent4),
        new(47, "Storm Surge II", "+5% Chance for Lightning Damage to be Lucky", MagicMinorNodeStatOption.LightningLuckyChancePercent5),
        new(48, "Burning Embers II", "Burn Tick Rate +0.25/s", MagicMinorNodeStatOption.BurnTickIntervalReduction025),
        new(49, "Archmage's Insight II", "+6% Spell Damage while above 70% Mana", MagicMinorNodeStatOption.SpellDamageAbove70ManaPercent6),
    };

    [MenuItem("Tools/Skills/Apply Magic Minor Passives")]
    public static void ApplySetup()
    {
        SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(MagicSkillPath);
        if (!skill)
        {
            Debug.LogError($"[MagicMinorPassiveAssetSetup] Missing skill asset at {MagicSkillPath}");
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
                magicMinorStatOption = spec.Option
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
        Debug.Log($"[MagicMinorPassiveAssetSetup] Applied {Passives.Length} magic minor passives to {MagicSkillPath}.");
    }

    private static Sprite LoadPassiveIcon()
    {
        string path = AssetDatabase.GUIDToAssetPath(PassiveIconGuid);
        if (string.IsNullOrEmpty(path))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite && asset.name.Contains("3534972268811701710") == false)
            {
                // Sub-sprite name varies; match by file id when possible.
            }
        }

        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
                return sprite;
        }

        return null;
    }
}
#endif
