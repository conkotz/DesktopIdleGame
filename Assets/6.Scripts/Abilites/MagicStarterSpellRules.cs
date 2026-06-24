using System;

/// <summary>
/// Magic Lv1 row-pick spells that define wand auto-attack element, base damage, cadence, and mana cost.
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

    public static bool TryGetCommittedStarterSpellAbilityId(SkillsManager skillsManager, out string abilityId)
    {
        abilityId = null;
        if (skillsManager == null)
            return false;

        int pick = skillsManager.GetSkillAbilityRowPick(SkillType.Magic, StarterSpellUnlockLevel, -1);
        if (pick < 0)
            return false;

        SkillDatabase db = SkillDatabase.LoadDefault();
        SkillDefinition magic = db != null ? db.Get(SkillType.Magic) : null;
        var siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(magic, StarterSpellUnlockLevel);
        if (pick < 0 || pick >= siblings.Count)
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
