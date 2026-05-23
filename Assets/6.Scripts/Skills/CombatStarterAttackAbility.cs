using System;
using UnityEngine;

/// <summary>
/// Lv1 combat <see cref="SkillUnlockType.Unlock"/> rows grant a shared "Attack" ability per skill (melee / ranged / magic).
/// Always listed first in the abilities panel; engages the closest enemy in weapon attack range (no ability damage).
/// </summary>
public static class CombatStarterAttackAbility
{
    public const string MeleeAttackAbilityId = "melee_attack";
    public const string RangedAttackAbilityId = "ranged_attack";
    public const string MagicAttackAbilityId = "magic_attack";

    public const string TooltipShortDescription = "Auto attack enemies";
    public const string WrongWeaponEquippedLogMessage = "Wrong weapon equipped";

    public static bool IsMeleeStarterAttack(AbilityDefinition ability) =>
        ability != null &&
        string.Equals(ability.abilityId, MeleeAttackAbilityId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Melee Attack: unarmed or melee weapon. Ranged/Magic Attack: matching weapon only.
    /// </summary>
    public static bool IsUsableWithEquippedWeapon(AbilityDefinition ability, CharacterStats stats)
    {
        if (ability == null || stats == null)
            return false;

        ItemDefinition mainHand = stats.GetEquippedMainHandWeaponOrNull();

        if (IsMeleeStarterAttack(ability))
        {
            if (mainHand == null)
                return true;
            return mainHand.weaponStats.attackSkill == AttackSkill.Melee;
        }

        if (string.Equals(ability.abilityId, RangedAttackAbilityId, StringComparison.OrdinalIgnoreCase))
            return mainHand != null && mainHand.weaponStats.attackSkill == AttackSkill.Ranged;

        if (string.Equals(ability.abilityId, MagicAttackAbilityId, StringComparison.OrdinalIgnoreCase))
            return mainHand != null && mainHand.weaponStats.attackSkill == AttackSkill.Magic;

        return false;
    }

    public static bool IsCombatSkill(SkillType skillType) =>
        skillType == SkillType.Melee || skillType == SkillType.Ranged || skillType == SkillType.Magic;

    public static bool IsCombatStarterAttack(AbilityDefinition ability)
    {
        if (ability == null || string.IsNullOrEmpty(ability.abilityId))
            return false;

        return string.Equals(ability.abilityId, MeleeAttackAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(ability.abilityId, RangedAttackAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(ability.abilityId, MagicAttackAbilityId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetCombatStarterAttackForSkill(SkillDefinition skill, out AbilityDefinition ability)
    {
        ability = null;
        if (skill == null || skill.unlocks == null || !IsCombatSkill(skill.skillType))
            return false;

        for (int i = 0; i < skill.unlocks.Count; i++)
        {
            SkillUnlockDefinition row = skill.unlocks[i];
            if (row == null || row.unlockType != SkillUnlockType.Unlock || row.ability == null)
                continue;

            if (!IsCombatStarterAttack(row.ability))
                continue;

            ability = row.ability;
            return true;
        }

        return false;
    }

    public static bool IsCombatStarterAttackUnlockedForGameplay(
        SkillDefinition skill,
        AbilityDefinition ability,
        SkillsManager sm)
    {
        if (!TryGetCombatStarterAttackForSkill(skill, out AbilityDefinition starter) || starter != ability)
            return false;

        if (sm == null)
            return true;

        return sm.IsLevelUnlocked(skill.skillType, Mathf.Max(1, starter.unlockLevel));
    }

    /// <summary>Default action-bar slot indices for new-game seeding (melee / ranged / magic Attack).</summary>
    public static void SeedDefaultActionBarAssignments(SaveData data)
    {
        if (data == null || HasAnyAbilityBarAssignment(data))
            return;

        // Slot 0 = primary hotkey; slots 1–2 hold the other combat Attack abilities for weapon swaps.
        AppendAbilityAssignment(data, 0, MeleeAttackAbilityId);
        AppendAbilityAssignment(data, 1, RangedAttackAbilityId);
        AppendAbilityAssignment(data, 2, MagicAttackAbilityId);
    }

    private static bool HasAnyAbilityBarAssignment(SaveData data)
    {
        if (data.actionBarKinds == null || data.actionBarIds == null)
            return false;

        int n = Mathf.Min(data.actionBarKinds.Count, data.actionBarIds.Count);
        for (int i = 0; i < n; i++)
        {
            if (data.actionBarKinds[i] != (int)ActionBarAssignmentKind.Ability)
                continue;
            if (!string.IsNullOrWhiteSpace(data.actionBarIds[i]))
                return true;
        }

        return false;
    }

    private static void AppendAbilityAssignment(SaveData data, int slotIndex, string abilityId)
    {
        data.actionBarSlotIndexes ??= new System.Collections.Generic.List<int>();
        data.actionBarKinds ??= new System.Collections.Generic.List<int>();
        data.actionBarIds ??= new System.Collections.Generic.List<string>();
        data.actionBarItemAmounts ??= new System.Collections.Generic.List<int>();

        data.actionBarSlotIndexes.Add(slotIndex);
        data.actionBarKinds.Add((int)ActionBarAssignmentKind.Ability);
        data.actionBarIds.Add(abilityId);
        data.actionBarItemAmounts.Add(0);
    }
}
