using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Magic Lv1 row-pick spells used as the primary magic weapon attack (manual bar or auto-battle).
/// </summary>
public static class MagicStarterSpellRules
{
    public const int StarterSpellUnlockLevel = 1;
    public const float StarterSpellCooldownSeconds = 2f;
    public const float StarterSpellAttacksPerSecond = 0.5f;
    public const int StarterSpellManaCost = 8;

    public const string FireBallAbilityId = "fire_ball";
    public const string IceShardAbilityId = "ice_shard";
    public const string EnergyBoltAbilityId = "energy_bolt";

    public const int DefaultStarterSpellSiblingIndex = 0;

    public const float FireBallMinDamage = 8f;
    public const float FireBallMaxDamage = 10f;
    public const float IceShardMinDamage = 6f;
    public const float IceShardMaxDamage = 12f;
    public const float EnergyBoltMinDamage = 3f;
    public const float EnergyBoltMaxDamage = 15f;

    public static bool IsMagicStarterSpellId(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId))
            return false;

        return string.Equals(abilityId, FireBallAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(abilityId, IceShardAbilityId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(abilityId, EnergyBoltAbilityId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Magic Lv1 starter row: auto-commit Fire Ball when no sibling is picked yet (special case for basic magic attack).
    /// Does not override an existing pick — at most one Lv1 spell is active via row pick storage.
    /// </summary>
    public static bool TryEnsureDefaultStarterSpellCommitted(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return false;

        if (!skillsManager.IsLevelUnlocked(SkillType.Magic, StarterSpellUnlockLevel))
            return false;

        SkillDatabase db = SkillDatabase.LoadDefault();
        SkillDefinition magic = db != null ? db.Get(SkillType.Magic) : null;
        if (magic == null)
            return false;

        int existingPick = skillsManager.GetSkillAbilityRowPick(
            SkillType.Magic, StarterSpellUnlockLevel, -1);
        if (existingPick >= 0)
            return true;

        List<AbilityDefinition> siblings =
            SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(magic, StarterSpellUnlockLevel);
        if (siblings == null || siblings.Count == 0)
            return false;

        int pick = SkillAbilityCommitRules.IndexOfAbilityInSiblingList(
            siblings, ResolveDefaultStarterSpellAbility(siblings));
        if (pick < 0)
            pick = Mathf.Clamp(DefaultStarterSpellSiblingIndex, 0, siblings.Count - 1);

        skillsManager.SetSkillAbilityRowPick(SkillType.Magic, StarterSpellUnlockLevel, pick);
        return true;
    }

    private static AbilityDefinition ResolveDefaultStarterSpellAbility(List<AbilityDefinition> siblings)
    {
        if (siblings == null)
            return null;

        for (int i = 0; i < siblings.Count; i++)
        {
            AbilityDefinition def = siblings[i];
            if (def != null
                && string.Equals(def.abilityId, FireBallAbilityId, StringComparison.OrdinalIgnoreCase))
                return def;
        }

        return siblings.Count > 0 ? siblings[DefaultStarterSpellSiblingIndex] : null;
    }

    public static bool TryGetCommittedStarterSpellAbilityId(SkillsManager skillsManager, out string abilityId)
    {
        abilityId = null;
        if (skillsManager == null)
            return false;

        TryEnsureDefaultStarterSpellCommitted(skillsManager);

        SkillDatabase db = SkillDatabase.LoadDefault();
        SkillDefinition magic = db != null ? db.Get(SkillType.Magic) : null;
        if (magic == null)
            return false;

        int pick = SkillAbilityCommitRules.GetCommittedAbilityRowPick(
            skillsManager, magic, StarterSpellUnlockLevel, -1);
        if (pick < 0)
            return false;

        var siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(magic, StarterSpellUnlockLevel);
        if (pick >= siblings.Count)
            return false;

        AbilityDefinition def = siblings[pick];
        if (def == null || string.IsNullOrEmpty(def.abilityId))
            return false;

        abilityId = def.abilityId;
        return IsMagicStarterSpellId(abilityId);
    }

    public static MagicAttackType GetMagicAttackTypeForAbilityId(string abilityId)
    {
        if (string.Equals(abilityId, FireBallAbilityId, StringComparison.OrdinalIgnoreCase))
            return MagicAttackType.Fire;
        if (string.Equals(abilityId, IceShardAbilityId, StringComparison.OrdinalIgnoreCase))
            return MagicAttackType.Ice;
        return MagicAttackType.Lightning;
    }

    public static bool TryGetBaseDamageBounds(string abilityId, out float minDamage, out float maxDamage)
    {
        minDamage = 0f;
        maxDamage = 0f;
        if (string.Equals(abilityId, FireBallAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            minDamage = FireBallMinDamage;
            maxDamage = FireBallMaxDamage;
            return true;
        }

        if (string.Equals(abilityId, IceShardAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            minDamage = IceShardMinDamage;
            maxDamage = IceShardMaxDamage;
            return true;
        }

        if (string.Equals(abilityId, EnergyBoltAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            minDamage = EnergyBoltMinDamage;
            maxDamage = EnergyBoltMaxDamage;
            return true;
        }

        return false;
    }

    public static float GetManaCostForAbilityId(string abilityId) =>
        IsMagicStarterSpellId(abilityId) ? StarterSpellManaCost : 0f;

    public static float GetCooldownSecondsForAbilityId(string abilityId) =>
        IsMagicStarterSpellId(abilityId) ? StarterSpellCooldownSeconds : 0f;
}
