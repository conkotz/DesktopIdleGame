/// <summary>
/// Player-facing stat labels shared by item tooltips, random pool previews, and the inspector.
/// Keep in sync with <see cref="ItemDefinition"/> tooltip lines.
/// </summary>
public static class ItemStatDisplayNames
{
    public const string AdditionalRandomStatPoolHeader = "Additional random stat pool";
    public const string AdditionalRandomStatPoolHeaderColor = "#C8B48A";

    public static string FormatAdditionalRandomStatPoolHeaderRichText() =>
        $"<size=115%><color={AdditionalRandomStatPoolHeaderColor}>{AdditionalRandomStatPoolHeader}</color></size>";

    /// <summary>Player-owned rolled item with affixes already identified — hide Alt pool preview.</summary>
    public static bool AreRandomAffixesDiscovered(ItemDatabase db, string itemId)
    {
        if (db == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!db.IsRuntimeEnhancedItem(itemId))
            return false;

        return !ItemRandomStatIdentification.HasUnidentifiedRandomAffixes(db, itemId);
    }

    public const string Health = "Health";
    public const string Energy = "Energy";
    public const string Mana = "Mana";
    public const string Armour = "Armour";
    public const string MagicRes = "Magic Res";
    public const string CorruptionRes = "Corruption Res";
    public const string PhysBlock = "Phys Block";
    public const string LifeRegen = "Life Regen";
    public const string EnergyRegen = "Energy Regen";
    public const string ManaRegen = "Mana Regen";
    public const string EnergyEfficiency = "Energy Efficiency";
    public const string LifeSteal = "Life Steal";
    public const string MoveSpeed = "Move Speed";
    public const string PhysicalDamageFlat = "Physical Damage";
    public const string MagicDamageFlat = "Magic Damage";
    public const string CorruptionDamageFlat = "Corruption Damage";
    public const string AbilityPower = "Ability Power";
    public const string AttackSpeed = "Attack Speed";
    public const string AbilityCooldownReduction = "Ability Cooldown Reduction";
    public const string MinionDamage = "Minion Damage";
    public const string MinionAttackSpeed = "Minion Attack Speed";
    public const string MinionCritChance = "Minion Crit Chance";
    public const string MinionHealth = "Minion Health";
    public const string CritChance = "Crit Chance";
    public const string CritMulti = "Crit Multi";
    public const string Range = "Range";
    public const string WeaponSpeed = "Speed";
    public const string Guard = "Guard";
    public const string MaxGuard = "Max Guard";
    public const string BleedChance = "Bleed Chance";
    public const string BleedMulti = "Bleed Multi";
    public const string PoisonChance = "Poison Chance";
    public const string PoisonMulti = "Poison Multi";
    public const string PoisonDuration = "Poison Duration";
    public const string PoisonMaxStacks = "Poison Max Stacks";
    public const string BurnChance = "Burn Chance";
    public const string BurnMultiplier = "Burn Multiplier";
    public const string ChillChance = "Chill Chance";
    public const string ChillEffect = "Chill Effect";
    public const string ShockChance = "Shock Chance";
    public const string ShockDamageAmount = "Shock Damage Amount";
    public const string ParryChance = "Parry Chance";
    public const string StunChance = "Stun Chance";
    public const string EnemyRespawnReduction = "Enemy Respawn Reduction";

    /// <summary>Label for a random pool stat entry (shop Alt preview, database, inspector picker).</summary>
    public static string ForRandomPoolStat(RandomItemStatType stat, ItemDefinition item = null)
    {
        switch (stat)
        {
            case RandomItemStatType.BonusHealth:
            case RandomItemStatType.ArmorBonusHealth:
                return Health;
            case RandomItemStatType.BonusEnergy:
            case RandomItemStatType.ArmorBonusEnergy:
                return Energy;
            case RandomItemStatType.BonusMana:
                return Mana;
            case RandomItemStatType.BonusArmor:
            case RandomItemStatType.ArmorFlatArmor:
                return Armour;
            case RandomItemStatType.BonusMagicResist:
            case RandomItemStatType.ArmorMagicResist:
                return MagicRes;
            case RandomItemStatType.BonusCorruptionResist:
            case RandomItemStatType.ArmorCorruptionResist:
                return CorruptionRes;
            case RandomItemStatType.BonusPhysBlockChance:
            case RandomItemStatType.ArmorPhysBlockChance:
                return PhysBlock;
            case RandomItemStatType.LifeRegen:
                return LifeRegen;
            case RandomItemStatType.EnergyRegen:
                return EnergyRegen;
            case RandomItemStatType.ManaRegen:
                return ManaRegen;
            case RandomItemStatType.EnergyEfficiency:
            case RandomItemStatType.ArmorEnergyEfficiency:
                return EnergyEfficiency;
            case RandomItemStatType.LifeSteal:
                return LifeSteal;
            case RandomItemStatType.MoveSpeedPercent:
                return MoveSpeed;
            case RandomItemStatType.PhysicalDamageFlat:
                return PhysicalDamageFlat;
            case RandomItemStatType.MeleePhysicalDamagePercent:
                return OffenseBonusDisplayNames.MeleeDamage;
            case RandomItemStatType.GlobalPhysicalDamagePercent:
                return OffenseBonusDisplayNames.PhysicalDamagePercent;
            case RandomItemStatType.RangedPhysicalDamagePercent:
                return OffenseBonusDisplayNames.RangedDamage;
            case RandomItemStatType.MagicDamageFlat:
                return MagicDamageFlat;
            case RandomItemStatType.MagicDamagePercent:
                return OffenseBonusDisplayNames.MagicDamagePercent;
            case RandomItemStatType.FireSkillDamagePercent:
                return OffenseBonusDisplayNames.FireDamagePercent;
            case RandomItemStatType.IceSkillDamagePercent:
                return OffenseBonusDisplayNames.IceDamagePercent;
            case RandomItemStatType.LightningSkillDamagePercent:
                return OffenseBonusDisplayNames.LightningDamagePercent;
            case RandomItemStatType.CorruptionDamagePercent:
                return OffenseBonusDisplayNames.CorruptionDamagePercent;
            case RandomItemStatType.CorruptionDamageFlat:
                return CorruptionDamageFlat;
            case RandomItemStatType.AbilityPowerPercent:
                return AbilityPower;
            case RandomItemStatType.AttackSpeedPercent:
                return AttackSpeed;
            case RandomItemStatType.AbilityCooldownReduction:
                return AbilityCooldownReduction;
            case RandomItemStatType.MinionDamagePercent:
                return MinionDamage;
            case RandomItemStatType.MinionAttackSpeedPercent:
                return MinionAttackSpeed;
            case RandomItemStatType.MinionCritChance:
                return MinionCritChance;
            case RandomItemStatType.MinionMaxLifePercent:
                return MinionHealth;
            case RandomItemStatType.CritChanceBonus:
            case RandomItemStatType.WeaponCritChance:
                return CritChance;
            case RandomItemStatType.CritMultiplierBonus:
            case RandomItemStatType.WeaponCritMultiplier:
                return CritMulti;
            case RandomItemStatType.AttackRangeBonus:
            case RandomItemStatType.WeaponAttackRange:
                return Range;
            case RandomItemStatType.WeaponAttacksPerSecond:
                return WeaponSpeed;
            case RandomItemStatType.WeaponMinPhysicalDamage:
                return "Min Physical Damage";
            case RandomItemStatType.WeaponMaxPhysicalDamage:
                return "Max Physical Damage";
            case RandomItemStatType.WeaponMinFireDamage:
                return "Min Fire Damage";
            case RandomItemStatType.WeaponMaxFireDamage:
                return "Max Fire Damage";
            case RandomItemStatType.WeaponMinIceDamage:
                return "Min Ice Damage";
            case RandomItemStatType.WeaponMaxIceDamage:
                return "Max Ice Damage";
            case RandomItemStatType.WeaponMinLightningDamage:
                return "Min Lightning Damage";
            case RandomItemStatType.WeaponMaxLightningDamage:
                return "Max Lightning Damage";
            case RandomItemStatType.WeaponMinCorruptionDamage:
                return "Min Corruption Damage";
            case RandomItemStatType.WeaponMaxCorruptionDamage:
                return "Max Corruption Damage";
            case RandomItemStatType.ArmorFlatGuard:
                return Guard;
            case RandomItemStatType.ArmorMaxGuardPercent:
                return MaxGuard;
            case RandomItemStatType.BleedChance:
                return BleedChance;
            case RandomItemStatType.BleedMultiplier:
                return BleedMulti;
            case RandomItemStatType.PoisonChance:
                return PoisonChance;
            case RandomItemStatType.PoisonMultiplier:
                return PoisonMulti;
            case RandomItemStatType.PoisonDurationBonus:
                return PoisonDuration;
            case RandomItemStatType.PoisonMaxStacksBonus:
                return PoisonMaxStacks;
            case RandomItemStatType.BurnChance:
                return BurnChance;
            case RandomItemStatType.BurnMultiplier:
                return BurnMultiplier;
            case RandomItemStatType.ChillChance:
                return ChillChance;
            case RandomItemStatType.ChillMultiplier:
                return ChillEffect;
            case RandomItemStatType.ShockChance:
                return ShockChance;
            case RandomItemStatType.ShockMultiplier:
                return ShockDamageAmount;
            case RandomItemStatType.ParryChance:
                return ParryChance;
            case RandomItemStatType.StunChance:
                return StunChance;
            case RandomItemStatType.WeaponMagicAilmentApplyChance:
                return MagicAilmentChanceRollName(item);
            case RandomItemStatType.WeaponCorruptionDamageRange:
                return CorruptionDamageFlat;
            case RandomItemStatType.EnemyRespawnTimeReductionSeconds:
                return EnemyRespawnReduction;
            default:
                return SplitCamelCase(stat.ToString());
        }
    }

    private static string MagicAilmentChanceRollName(ItemDefinition item)
    {
        if (!item || !item.IsWeapon)
            return "Magic Ailment Apply Chance";

        return item.weaponStats.magicAttackType switch
        {
            MagicAttackType.Fire => BurnChance,
            MagicAttackType.Ice => ChillChance,
            MagicAttackType.Lightning => ShockChance,
            _ => "Magic Ailment Apply Chance"
        };
    }

    private static string SplitCamelCase(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        var sb = new System.Text.StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(raw[i - 1]) || (i + 1 < raw.Length && char.IsLower(raw[i + 1]))))
                sb.Append(' ');
            sb.Append(c);
        }

        return sb.ToString();
    }
}
