using System.Collections.Generic;
using UnityEngine;

/// <summary>Which stat a random pool entry can roll onto an item instance.</summary>
public enum RandomItemStatType
{
    BonusHealth,
    BonusEnergy,
    BonusMana,
    BonusArmor,
    BonusMagicResist,
    BonusCorruptionResist,
    BonusPhysBlockChance,
    LifeRegen,
    EnergyRegen,
    ManaRegen,
    EnergyEfficiency,
    LifeSteal,
    MoveSpeedPercent,
    PhysicalDamageFlat,
    PhysicalDamagePercent,
    GlobalPhysicalDamagePercent,
    RangedPhysicalDamagePercent,
    MagicDamageFlat,
    MagicDamagePercent,
    FireSkillDamagePercent,
    IceSkillDamagePercent,
    LightningSkillDamagePercent,
    CorruptionDamagePercent,
    CorruptionDamageFlat,
    AbilityPowerPercent,
    AttackSpeedPercent,
    AbilityCooldownReduction,
    MinionDamagePercent,
    MinionAttackSpeedPercent,
    MinionCritChance,
    MinionMaxLifePercent,
    CritChanceBonus,
    CritMultiplierBonus,
    AttackRangeBonus,
    BleedChance,
    BleedMultiplier,
    PoisonChance,
    PoisonMultiplier,
    PoisonDurationBonus,
    PoisonMaxStacksBonus,
    BurnChance,
    BurnExplosionMultiplierBonus,
    ChillSlowPerStackBonus,
    ShockDamageTakenMultiplierBonus,
    ParryChance,
    StunChance,

    WeaponMinPhysicalDamage,
    WeaponMaxPhysicalDamage,
    WeaponMinCorruptionDamage,
    WeaponMaxCorruptionDamage,
    WeaponCorruptionDamageRange,
    WeaponAttacksPerSecond,
    WeaponCritChance,
    WeaponCritMultiplier,
    WeaponAttackRange,
    WeaponMagicAilmentApplyChance,

    ArmorFlatArmor,
    ArmorMagicResist,
    ArmorCorruptionResist,
    ArmorPhysBlockChance,
    ArmorBonusHealth,
    ArmorBonusEnergy,
    ArmorEnergyEfficiency,
    ArmorFlatGuard,
    ArmorMaxGuardPercent,

    WeaponMinFireDamage,
    WeaponMaxFireDamage,
    WeaponMinIceDamage,
    WeaponMaxIceDamage,
    WeaponMinLightningDamage,
    WeaponMaxLightningDamage,

    /// <summary>Seconds subtracted from map enemy respawn delay while equipped (misc effect).</summary>
    EnemyRespawnTimeReductionSeconds,
}

public enum RandomStatValueKind
{
    [Tooltip("Whole numbers (health, flat damage).")]
    FlatInteger,

    [Tooltip("Raw float add (regen, weapon APS).")]
    FlatFloat,

    [Tooltip("Fraction where 0.05 = 5% (attack speed %, poison chance, life steal).")]
    Fraction,

    [Tooltip("Enter whole percents (10 = +10%). Converts to stored fraction for most stats; ability power stores as-is.")]
    PercentPoints
}

[System.Serializable]
public class RandomStatPoolEntry
{
    public RandomItemStatType stat = RandomItemStatType.BonusHealth;

    [Min(0f)]
    [Tooltip("Relative weight when picking affixes from this item's pool.")]
    public float weight = 1f;

    [Tooltip("Minimum rolled value (interpretation depends on Value Kind).")]
    public float minValue;

    [Tooltip("Maximum rolled value (interpretation depends on Value Kind).")]
    public float maxValue;

    public RandomStatValueKind valueKind = RandomStatValueKind.FlatInteger;

    [Tooltip("When stat is Weapon Corruption Damage Range, also roll max corruption using Secondary Min/Max.")]
    public bool rollSecondaryValue;

    [Tooltip("Secondary roll minimum (max corruption when using Weapon Corruption Damage Range).")]
    public float secondaryMinValue;

    [Tooltip("Secondary roll maximum (max corruption when using Weapon Corruption Damage Range).")]
    public float secondaryMaxValue;

    public bool IsValid => weight > 0f && maxValue >= minValue &&
                           (!rollSecondaryValue || secondaryMaxValue >= secondaryMinValue);
}

/// <summary>
/// Rolls optional random affixes onto weapon/armour/jewellery when they enter the player's inventory.
/// Empty pools leave items unchanged.
/// </summary>
public static class ItemRandomStatRoller
{
    public static int GetRollCountForRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return 2;
            case ItemRarity.Epic: return 3;
            case ItemRarity.Legendary: return 4;
            default: return 1;
        }
    }

    public static bool ShouldRollOnAcquire(ItemDefinition def, ItemDatabase db)
    {
        if (!def || !def.HasRandomStatPool)
            return false;

        if (!def.IsWeapon && !def.IsArmor && !def.IsJewelry)
            return false;

        if (def.maxStack > 1)
            return false;

        if (db != null && db.IsRuntimeEnhancedItem(def.itemId))
            return false;

        return true;
    }

    public static ItemDefinition CreateRolledItem(ItemDatabase db, ItemDefinition baseDef)
    {
        if (!db || !baseDef || !ShouldRollOnAcquire(baseDef, db))
            return null;

        ItemDefinition clone = db.CreateRuntimeEnhancedItem(baseDef);
        if (!clone)
            return null;

        var pool = new List<RandomStatPoolEntry>(baseDef.RandomStatPoolEntries);
        ApplyRolls(clone, pool, baseDef.rarity);
        clone.ClearRandomStatPool();
        return clone;
    }

    public static void ApplyRolls(ItemDefinition item, List<RandomStatPoolEntry> pool, ItemRarity rarity)
    {
        if (!item || pool == null || pool.Count == 0)
            return;

        int rollCount = GetRollCountForRarity(rarity);
        var available = new List<RandomStatPoolEntry>();
        for (int i = 0; i < pool.Count; i++)
        {
            RandomStatPoolEntry entry = pool[i];
            if (entry != null && entry.IsValid)
                available.Add(entry);
        }

        for (int r = 0; r < rollCount && available.Count > 0; r++)
        {
            int pickIndex = PickWeightedIndex(available);
            RandomStatPoolEntry picked = available[pickIndex];
            available.RemoveAt(pickIndex);
            ApplyEntry(item, picked);
        }
    }

    public static RandomStatValueKind GetDefaultValueKind(RandomItemStatType stat)
    {
        switch (stat)
        {
            case RandomItemStatType.BonusHealth:
            case RandomItemStatType.BonusEnergy:
            case RandomItemStatType.BonusMana:
            case RandomItemStatType.BonusArmor:
            case RandomItemStatType.BonusMagicResist:
            case RandomItemStatType.BonusCorruptionResist:
            case RandomItemStatType.PhysicalDamageFlat:
            case RandomItemStatType.MagicDamageFlat:
            case RandomItemStatType.CorruptionDamageFlat:
            case RandomItemStatType.PoisonMaxStacksBonus:
            case RandomItemStatType.WeaponMinPhysicalDamage:
            case RandomItemStatType.WeaponMaxPhysicalDamage:
            case RandomItemStatType.WeaponMinCorruptionDamage:
            case RandomItemStatType.WeaponMaxCorruptionDamage:
            case RandomItemStatType.ArmorFlatArmor:
            case RandomItemStatType.ArmorMagicResist:
            case RandomItemStatType.ArmorCorruptionResist:
            case RandomItemStatType.ArmorBonusHealth:
            case RandomItemStatType.ArmorBonusEnergy:
            case RandomItemStatType.ArmorFlatGuard:
            case RandomItemStatType.WeaponMinFireDamage:
            case RandomItemStatType.WeaponMaxFireDamage:
            case RandomItemStatType.WeaponMinIceDamage:
            case RandomItemStatType.WeaponMaxIceDamage:
            case RandomItemStatType.WeaponMinLightningDamage:
            case RandomItemStatType.WeaponMaxLightningDamage:
                return RandomStatValueKind.FlatInteger;

            case RandomItemStatType.AbilityPowerPercent:
                return RandomStatValueKind.PercentPoints;

            case RandomItemStatType.LifeRegen:
            case RandomItemStatType.EnergyRegen:
            case RandomItemStatType.ManaRegen:
            case RandomItemStatType.PoisonDurationBonus:
            case RandomItemStatType.WeaponAttacksPerSecond:
            case RandomItemStatType.WeaponAttackRange:
            case RandomItemStatType.WeaponCritMultiplier:
            case RandomItemStatType.EnemyRespawnTimeReductionSeconds:
                return RandomStatValueKind.FlatFloat;

            default:
                return RandomStatValueKind.Fraction;
        }
    }

    private static int PickWeightedIndex(IReadOnlyList<RandomStatPoolEntry> entries)
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++)
            total += Mathf.Max(0f, entries[i].weight);

        if (total <= 0f)
            return Random.Range(0, entries.Count);

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += Mathf.Max(0f, entries[i].weight);
            if (roll <= cumulative)
                return i;
        }

        return entries.Count - 1;
    }

    private static void ApplyEntry(ItemDefinition item, RandomStatPoolEntry entry)
    {
        if (!item || entry == null)
            return;

        RandomStatValueKind kind = entry.valueKind;
        RandomItemStatType stat = entry.stat;
        float primary = RollValue(entry.minValue, entry.maxValue, kind, stat);

        switch (entry.stat)
        {
            case RandomItemStatType.BonusHealth:
                AddBonusInt(ref item.bonusStats.bonusHealth, primary, kind);
                break;
            case RandomItemStatType.BonusEnergy:
                AddBonusInt(ref item.bonusStats.bonusEnergy, primary, kind);
                break;
            case RandomItemStatType.BonusMana:
                AddBonusInt(ref item.bonusStats.bonusMana, primary, kind);
                break;
            case RandomItemStatType.BonusArmor:
                AddBonusInt(ref item.bonusStats.armor, primary, kind);
                break;
            case RandomItemStatType.BonusMagicResist:
                AddBonusInt(ref item.bonusStats.magicResist, primary, kind);
                break;
            case RandomItemStatType.BonusCorruptionResist:
                AddBonusInt(ref item.bonusStats.corruptionResist, primary, kind);
                break;
            case RandomItemStatType.BonusPhysBlockChance:
                item.bonusStats.physBlockChance = Mathf.Clamp01(item.bonusStats.physBlockChance + primary);
                break;
            case RandomItemStatType.LifeRegen:
                item.bonusStats.lifeRegen += primary;
                break;
            case RandomItemStatType.EnergyRegen:
                item.bonusStats.energyRegen += primary;
                break;
            case RandomItemStatType.ManaRegen:
                item.bonusStats.manaRegen += primary;
                break;
            case RandomItemStatType.EnergyEfficiency:
                item.bonusStats.energyEfficiency = Mathf.Clamp01(item.bonusStats.energyEfficiency + primary);
                break;
            case RandomItemStatType.LifeSteal:
                item.bonusStats.lifeSteal = Mathf.Clamp01(item.bonusStats.lifeSteal + primary);
                break;
            case RandomItemStatType.MoveSpeedPercent:
                item.bonusStats.moveSpeedPercent += primary;
                break;
            case RandomItemStatType.PhysicalDamageFlat:
                item.bonusStats.physicalDamage += primary;
                break;
            case RandomItemStatType.PhysicalDamagePercent:
                item.bonusStats.physicalDamagePercent += primary;
                break;
            case RandomItemStatType.GlobalPhysicalDamagePercent:
                item.bonusStats.globalPhysicalDamagePercent += primary;
                break;
            case RandomItemStatType.RangedPhysicalDamagePercent:
                item.bonusStats.rangedPhysicalDamagePercent += primary;
                break;
            case RandomItemStatType.MagicDamageFlat:
                item.bonusStats.magicDamage += primary;
                break;
            case RandomItemStatType.MagicDamagePercent:
                item.bonusStats.magicDamagePercent += primary;
                break;
            case RandomItemStatType.FireSkillDamagePercent:
                item.bonusStats.fireSkillDamagePercent += primary;
                break;
            case RandomItemStatType.IceSkillDamagePercent:
                item.bonusStats.iceSkillDamagePercent += primary;
                break;
            case RandomItemStatType.LightningSkillDamagePercent:
                item.bonusStats.lightningSkillDamagePercent += primary;
                break;
            case RandomItemStatType.CorruptionDamagePercent:
                item.bonusStats.corruptionDamagePercent += primary;
                break;
            case RandomItemStatType.CorruptionDamageFlat:
                item.bonusStats.corruptionDamage += primary;
                break;
            case RandomItemStatType.AbilityPowerPercent:
                item.bonusStats.abilityPower += primary;
                break;
            case RandomItemStatType.AttackSpeedPercent:
                item.bonusStats.attackSpeedPercent += primary;
                break;
            case RandomItemStatType.AbilityCooldownReduction:
                item.bonusStats.abilityCooldownReductionFraction = Mathf.Clamp01(
                    item.bonusStats.abilityCooldownReductionFraction + primary);
                break;
            case RandomItemStatType.MinionDamagePercent:
                item.bonusStats.minionDamagePercent += primary;
                break;
            case RandomItemStatType.MinionAttackSpeedPercent:
                item.bonusStats.minionAttackSpeedPercent += primary;
                break;
            case RandomItemStatType.MinionCritChance:
                item.bonusStats.minionCritChance += primary;
                break;
            case RandomItemStatType.MinionMaxLifePercent:
                item.bonusStats.minionMaxLifePercent += primary;
                break;
            case RandomItemStatType.CritChanceBonus:
                item.bonusStats.critChanceBonus += primary;
                break;
            case RandomItemStatType.CritMultiplierBonus:
                item.bonusStats.critMultiplierBonus += primary;
                break;
            case RandomItemStatType.AttackRangeBonus:
                item.bonusStats.attackRangeBonus += primary;
                break;
            case RandomItemStatType.BleedChance:
                item.bonusStats.bleedChance = Mathf.Clamp01(item.bonusStats.bleedChance + primary);
                break;
            case RandomItemStatType.BleedMultiplier:
                item.bonusStats.bleedMultiplier += primary;
                break;
            case RandomItemStatType.PoisonChance:
                item.bonusStats.poisonChance = Mathf.Clamp01(item.bonusStats.poisonChance + primary);
                break;
            case RandomItemStatType.PoisonMultiplier:
                item.bonusStats.poisonMultiplier += primary;
                break;
            case RandomItemStatType.PoisonDurationBonus:
                item.bonusStats.poisonDurationBonus += primary;
                break;
            case RandomItemStatType.PoisonMaxStacksBonus:
                AddBonusInt(ref item.bonusStats.poisonMaxStacksBonus, primary, kind);
                break;
            case RandomItemStatType.BurnChance:
                item.bonusStats.burnChance = Mathf.Clamp01(item.bonusStats.burnChance + primary);
                break;
            case RandomItemStatType.BurnExplosionMultiplierBonus:
                item.bonusStats.burnExplosionMultiplierBonus += primary;
                break;
            case RandomItemStatType.ChillSlowPerStackBonus:
                item.bonusStats.chillSlowPerStackBonus += primary;
                break;
            case RandomItemStatType.ShockDamageTakenMultiplierBonus:
                item.bonusStats.shockDamageTakenMultiplierBonus += primary;
                break;
            case RandomItemStatType.ParryChance:
                item.bonusStats.parryChance = Mathf.Clamp01(item.bonusStats.parryChance + primary);
                break;
            case RandomItemStatType.StunChance:
                item.bonusStats.stunChance = Mathf.Clamp01(item.bonusStats.stunChance + primary);
                break;

            case RandomItemStatType.WeaponMinPhysicalDamage:
                AddWeaponInt(ref item.weaponStats.minPhysicalDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMaxPhysicalDamage:
                AddWeaponInt(ref item.weaponStats.maxPhysicalDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMinCorruptionDamage:
                AddWeaponInt(ref item.weaponStats.minCorruptionDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMaxCorruptionDamage:
                AddWeaponInt(ref item.weaponStats.maxCorruptionDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponCorruptionDamageRange:
            {
                float minAdd = RollValue(entry.minValue, entry.maxValue, kind, stat);
                float maxAdd = entry.rollSecondaryValue
                    ? RollValue(entry.secondaryMinValue, entry.secondaryMaxValue, kind, stat)
                    : minAdd;
                AddWeaponInt(ref item.weaponStats.minCorruptionDamage, minAdd, kind);
                AddWeaponInt(ref item.weaponStats.maxCorruptionDamage, maxAdd, kind);
                break;
            }
            case RandomItemStatType.WeaponAttacksPerSecond:
                item.weaponStats.attacksPerSecond = Mathf.Max(0.01f, item.weaponStats.attacksPerSecond + primary);
                break;
            case RandomItemStatType.WeaponCritChance:
                item.weaponStats.critChance = Mathf.Clamp01(item.weaponStats.critChance + primary);
                break;
            case RandomItemStatType.WeaponCritMultiplier:
                item.weaponStats.critMultiplier = Mathf.Max(0f, item.weaponStats.critMultiplier + primary);
                break;
            case RandomItemStatType.WeaponAttackRange:
                item.weaponStats.attackRange = Mathf.Max(0f, item.weaponStats.attackRange + primary);
                break;
            case RandomItemStatType.WeaponMagicAilmentApplyChance:
                item.weaponStats.magicAilmentApplyChance = Mathf.Clamp01(
                    item.weaponStats.magicAilmentApplyChance + primary);
                break;

            case RandomItemStatType.ArmorFlatArmor:
                AddArmorInt(ref item.armorStats.armor, primary, kind);
                break;
            case RandomItemStatType.ArmorMagicResist:
                AddArmorInt(ref item.armorStats.magicResist, primary, kind);
                break;
            case RandomItemStatType.ArmorCorruptionResist:
                AddArmorInt(ref item.armorStats.corruptionResist, primary, kind);
                break;
            case RandomItemStatType.ArmorPhysBlockChance:
                item.armorStats.physBlockChance = Mathf.Clamp01(item.armorStats.physBlockChance + primary);
                break;
            case RandomItemStatType.ArmorBonusHealth:
                AddArmorInt(ref item.armorStats.bonusHealth, primary, kind);
                break;
            case RandomItemStatType.ArmorBonusEnergy:
                AddArmorInt(ref item.armorStats.bonusEnergy, primary, kind);
                break;
            case RandomItemStatType.ArmorEnergyEfficiency:
                item.armorStats.energyEfficiency = Mathf.Clamp01(item.armorStats.energyEfficiency + primary);
                break;
            case RandomItemStatType.ArmorFlatGuard:
                AddArmorInt(ref item.armorStats.flatGuard, primary, kind);
                break;
            case RandomItemStatType.ArmorMaxGuardPercent:
                item.armorStats.maxGuardPercent += primary;
                break;

            case RandomItemStatType.WeaponMinFireDamage:
                AddWeaponInt(ref item.weaponStats.minFireDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMaxFireDamage:
                AddWeaponInt(ref item.weaponStats.maxFireDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMinIceDamage:
                AddWeaponInt(ref item.weaponStats.minIceDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMaxIceDamage:
                AddWeaponInt(ref item.weaponStats.maxIceDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMinLightningDamage:
                AddWeaponInt(ref item.weaponStats.minLightningDamage, primary, kind);
                break;
            case RandomItemStatType.WeaponMaxLightningDamage:
                AddWeaponInt(ref item.weaponStats.maxLightningDamage, primary, kind);
                break;
            case RandomItemStatType.EnemyRespawnTimeReductionSeconds:
                item.miscEffects.enemyRespawnTimeReductionSeconds = Mathf.Max(
                    0f,
                    item.miscEffects.enemyRespawnTimeReductionSeconds + primary);
                break;
        }
    }

    private static float RollValue(float min, float max, RandomStatValueKind kind, RandomItemStatType stat)
    {
        float raw = min >= max ? min : Random.Range(min, max);
        return ConvertRolledValue(raw, kind, stat);
    }

    /// <summary>
    /// Ability power alone stores literal percent points (5 = +5%). Most other % stats store fractions (0.05 = +5%).
    /// </summary>
    private static bool UsesPercentPointsStorage(RandomItemStatType stat) =>
        stat == RandomItemStatType.AbilityPowerPercent;

    private static float ConvertRolledValue(float raw, RandomStatValueKind kind, RandomItemStatType stat)
    {
        switch (kind)
        {
            case RandomStatValueKind.PercentPoints:
                return UsesPercentPointsStorage(stat) ? raw : raw / 100f;
            case RandomStatValueKind.Fraction:
                return raw;
            case RandomStatValueKind.FlatInteger:
                return Mathf.RoundToInt(raw);
            default:
                return raw;
        }
    }

    private static void AddBonusInt(ref int field, float value, RandomStatValueKind kind)
    {
        field += kind == RandomStatValueKind.FlatInteger ? Mathf.RoundToInt(value) : Mathf.RoundToInt(value);
    }

    private static void AddWeaponInt(ref int field, float value, RandomStatValueKind kind)
    {
        field += Mathf.RoundToInt(kind == RandomStatValueKind.FlatInteger ? value : value);
    }

    private static void AddArmorInt(ref int field, float value, RandomStatValueKind kind)
    {
        field += Mathf.RoundToInt(kind == RandomStatValueKind.FlatInteger ? value : value);
    }
}
