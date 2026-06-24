/// <summary>
/// Weapon requirements for combat skill-tree minors, majors, and capstones (details panel + hover tooltips).
/// Endurance and gathering tracks are excluded.
/// </summary>
public static class CombatPassiveWeaponRequirementText
{
    public const string MeleeRequirementLine = "Required: Melee";
    public const string RangedRequirementLine = "Required: Ranged weapon";
    public const string MagicRequirementLine = "Required: Magic weapon";

    public static bool TryBuildUnlockRequirementsRichText(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        CharacterStats stats,
        int displayedCapstoneChoiceIndex,
        bool accentWhenOk,
        out string richText)
    {
        richText = null;
        if (skill == null || unlock == null)
            return false;

        switch (unlock.unlockType)
        {
            case SkillUnlockType.CapstonePassive when skill.skillType == SkillType.Melee:
                richText = MeleeMajorPassiveTooltipText.BuildMeleeCapstoneRequirementsRichText(
                    stats, displayedCapstoneChoiceIndex);
                return !string.IsNullOrWhiteSpace(richText);

            case SkillUnlockType.MajorPassive:
                return TryBuildMajorPassiveRequirements(skill.skillType, stats, accentWhenOk, out richText);

            case SkillUnlockType.MinorPassive:
                return TryBuildMinorPassiveRequirements(skill, unlock, stats, accentWhenOk, out richText);

            default:
                return false;
        }
    }

    public static bool TryBuildChoiceParentRequirementsRichText(
        SkillDefinition skill,
        SkillUnlockDefinition parentUnlock,
        AbilityDefinition resolvedAbility,
        CharacterStats stats,
        bool accentWhenOk,
        out string richText)
    {
        richText = null;
        if (resolvedAbility != null)
        {
            richText = AbilityTooltipDamagePreview.BuildAbilityRequirementsRichText(
                resolvedAbility, stats, accentWhenOk);
            return !string.IsNullOrWhiteSpace(richText);
        }

        return TryBuildUnlockRequirementsRichText(skill, parentUnlock, stats, -1, accentWhenOk, out richText);
    }

    private static bool TryBuildMajorPassiveRequirements(
        SkillType skillType,
        CharacterStats stats,
        bool accentWhenOk,
        out string richText)
    {
        switch (skillType)
        {
            case SkillType.Melee:
                richText = MeleeMajorPassiveTooltipText.BuildMeleeMajorPassiveRequirementsRichText(stats);
                return !string.IsNullOrWhiteSpace(richText);

            case SkillType.Ranged:
                richText = FormatRequirementLine(
                    RangedRequirementLine,
                    stats == null || stats.HasRangedWeaponEquippedForMajorPassive(),
                    accentWhenOk);
                return true;

            case SkillType.Magic:
                richText = FormatRequirementLine(
                    MagicRequirementLine,
                    stats == null || stats.CurrentAttackSkill == AttackSkill.Magic,
                    accentWhenOk);
                return true;

            default:
                richText = null;
                return false;
        }
    }

    private static bool TryBuildMinorPassiveRequirements(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        CharacterStats stats,
        bool accentWhenOk,
        out string richText)
    {
        richText = null;
        switch (skill.skillType)
        {
            case SkillType.Melee:
                if (unlock.meleeMinorStatOption == MeleeMinorNodeStatOption.None)
                    return false;
                richText = FormatRequirementLine(
                    MeleeRequirementLine,
                    stats == null || stats.HasMeleeWeaponEquippedForCapstonePassive(),
                    accentWhenOk);
                return true;

            case SkillType.Ranged:
                if (unlock.rangedMinorStatOption == RangedMinorNodeStatOption.None)
                    return false;
                richText = FormatRequirementLine(
                    RangedRequirementLine,
                    stats == null || stats.HasRangedWeaponEquippedForMajorPassive(),
                    accentWhenOk);
                return true;

            case SkillType.Magic:
                if (unlock.magicMinorStatOption == MagicMinorNodeStatOption.None)
                    return false;
                richText = FormatRequirementLine(
                    MagicRequirementLine,
                    stats == null || stats.CurrentAttackSkill == AttackSkill.Magic,
                    accentWhenOk);
                return true;

            default:
                return false;
        }
    }

    private static string FormatRequirementLine(string text, bool met, bool accentWhenOk)
    {
        if (!met)
            return $"<color=#FF5C5C>{text}</color>";

        if (accentWhenOk)
            return $"<color=#55DD55>{text}</color>";

        return text;
    }
}
