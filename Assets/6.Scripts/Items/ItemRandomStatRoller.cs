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
    BurnMultiplier,
    ChillMultiplier,
    ShockMultiplier,
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

    /// <summary>Additive bonus chill apply chance on weapons (stacks with ice magic ailment chance).</summary>
    ChillChance,

    /// <summary>Additive bonus shock apply chance on weapons (stacks with lightning magic ailment chance).</summary>
    ShockChance,
}

public enum RandomStatValueKind
{
    [Tooltip("Whole numbers (health, flat damage).")]
    FlatInteger,

    [Tooltip("Raw float add (regen, weapon APS).")]
    FlatFloat,

    [Tooltip("Legacy: raw fraction (0.05 = 5%). Prefer Percent Points for new entries.")]
    Fraction,

    [Tooltip("Whole percent points (1 = +1%). Flat-added to % stats; weapon APS rolls add that many APS (2 = +0.02 APS).")]
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
        clone.randomStatsPendingIdentification = true;
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

    /// <summary>
    /// Builds pool entries from non-zero stats on <paramref name="item"/> (weight 1, small min/max, default value kind per stat).
    /// </summary>
    public static List<RandomStatPoolEntry> BuildTemplatePoolEntries(ItemDefinition item)
    {
        var results = new List<RandomStatPoolEntry>();
        if (!item)
            return results;

        void TryAdd(RandomItemStatType stat, float value)
        {
            if (IsEffectivelyZero(value))
                return;

            RandomStatValueKind kind = GetDefaultValueKind(stat);
            GetTemplateMinMax(stat, value, kind, out float min, out float max);
            results.Add(new RandomStatPoolEntry
            {
                stat = stat,
                weight = 1f,
                valueKind = kind,
                minValue = min,
                maxValue = max,
            });
        }

        BonusStats bonus = item.bonusStats;
        TryAdd(RandomItemStatType.BonusHealth, bonus.bonusHealth);
        TryAdd(RandomItemStatType.BonusEnergy, bonus.bonusEnergy);
        TryAdd(RandomItemStatType.BonusMana, bonus.bonusMana);
        TryAdd(RandomItemStatType.BonusArmor, bonus.armor);
        TryAdd(RandomItemStatType.BonusMagicResist, bonus.magicResist);
        TryAdd(RandomItemStatType.BonusCorruptionResist, bonus.corruptionResist);
        TryAdd(RandomItemStatType.BonusPhysBlockChance, bonus.physBlockChance);
        TryAdd(RandomItemStatType.LifeRegen, bonus.lifeRegen);
        TryAdd(RandomItemStatType.EnergyRegen, bonus.energyRegen);
        TryAdd(RandomItemStatType.ManaRegen, bonus.manaRegen);
        TryAdd(RandomItemStatType.EnergyEfficiency, bonus.energyEfficiency);
        TryAdd(RandomItemStatType.LifeSteal, bonus.lifeSteal);
        TryAdd(RandomItemStatType.MoveSpeedPercent, bonus.moveSpeedPercent);
        TryAdd(RandomItemStatType.PhysicalDamageFlat, bonus.physicalDamage);
        TryAdd(RandomItemStatType.PhysicalDamagePercent, bonus.physicalDamagePercent);
        TryAdd(RandomItemStatType.GlobalPhysicalDamagePercent, bonus.globalPhysicalDamagePercent);
        TryAdd(RandomItemStatType.RangedPhysicalDamagePercent, bonus.rangedPhysicalDamagePercent);
        TryAdd(RandomItemStatType.MagicDamageFlat, bonus.magicDamage);
        TryAdd(RandomItemStatType.MagicDamagePercent, bonus.magicDamagePercent);
        TryAdd(RandomItemStatType.FireSkillDamagePercent, bonus.fireSkillDamagePercent);
        TryAdd(RandomItemStatType.IceSkillDamagePercent, bonus.iceSkillDamagePercent);
        TryAdd(RandomItemStatType.LightningSkillDamagePercent, bonus.lightningSkillDamagePercent);
        TryAdd(RandomItemStatType.CorruptionDamagePercent, bonus.corruptionDamagePercent);
        TryAdd(RandomItemStatType.CorruptionDamageFlat, bonus.corruptionDamage);
        TryAdd(RandomItemStatType.AbilityPowerPercent, bonus.abilityPower);
        TryAdd(RandomItemStatType.AttackSpeedPercent, bonus.attackSpeedPercent);
        TryAdd(RandomItemStatType.AbilityCooldownReduction, bonus.abilityCooldownReductionFraction);
        TryAdd(RandomItemStatType.MinionDamagePercent, bonus.minionDamagePercent);
        TryAdd(RandomItemStatType.MinionAttackSpeedPercent, bonus.minionAttackSpeedPercent);
        TryAdd(RandomItemStatType.MinionCritChance, bonus.minionCritChance);
        TryAdd(RandomItemStatType.MinionMaxLifePercent, bonus.minionMaxLifePercent);
        TryAdd(RandomItemStatType.CritChanceBonus, bonus.critChanceBonus);
        TryAdd(RandomItemStatType.CritMultiplierBonus, bonus.critMultiplierBonus);
        TryAdd(RandomItemStatType.AttackRangeBonus, bonus.attackRangeBonus);
        TryAdd(RandomItemStatType.BleedChance, bonus.bleedChance);
        TryAdd(RandomItemStatType.BleedMultiplier, bonus.bleedMultiplier);
        TryAdd(RandomItemStatType.PoisonChance, bonus.poisonChance);
        TryAdd(RandomItemStatType.PoisonMultiplier, bonus.poisonMultiplier);
        TryAdd(RandomItemStatType.PoisonDurationBonus, bonus.poisonDurationBonus);
        TryAdd(RandomItemStatType.PoisonMaxStacksBonus, bonus.poisonMaxStacksBonus);
        TryAdd(RandomItemStatType.BurnChance, bonus.burnChance);
        TryAdd(RandomItemStatType.ChillChance, bonus.chillChance);
        TryAdd(RandomItemStatType.ShockChance, bonus.shockChance);
        TryAdd(RandomItemStatType.BurnMultiplier, bonus.burnExplosionMultiplierBonus);
        TryAdd(RandomItemStatType.ChillMultiplier, bonus.chillSlowPerStackBonus);
        TryAdd(RandomItemStatType.ShockMultiplier, bonus.shockDamageTakenMultiplierBonus);
        TryAdd(RandomItemStatType.ParryChance, bonus.parryChance);
        TryAdd(RandomItemStatType.StunChance, bonus.stunChance);

        if (item.IsWeapon)
        {
            WeaponStats weapon = item.weaponStats;
            TryAdd(RandomItemStatType.WeaponMinPhysicalDamage, weapon.minPhysicalDamage);
            TryAdd(RandomItemStatType.WeaponMaxPhysicalDamage, weapon.maxPhysicalDamage);
            TryAdd(RandomItemStatType.WeaponMinFireDamage, weapon.minFireDamage);
            TryAdd(RandomItemStatType.WeaponMaxFireDamage, weapon.maxFireDamage);
            TryAdd(RandomItemStatType.WeaponMinIceDamage, weapon.minIceDamage);
            TryAdd(RandomItemStatType.WeaponMaxIceDamage, weapon.maxIceDamage);
            TryAdd(RandomItemStatType.WeaponMinLightningDamage, weapon.minLightningDamage);
            TryAdd(RandomItemStatType.WeaponMaxLightningDamage, weapon.maxLightningDamage);
            TryAdd(RandomItemStatType.WeaponMinCorruptionDamage, weapon.minCorruptionDamage);
            TryAdd(RandomItemStatType.WeaponMaxCorruptionDamage, weapon.maxCorruptionDamage);
            TryAdd(RandomItemStatType.WeaponAttacksPerSecond, weapon.attacksPerSecond);
            TryAdd(RandomItemStatType.WeaponCritChance, weapon.critChance);
            TryAdd(RandomItemStatType.WeaponCritMultiplier, weapon.critMultiplier);
            TryAdd(RandomItemStatType.WeaponAttackRange, weapon.attackRange);
            TryAdd(RandomItemStatType.WeaponMagicAilmentApplyChance, weapon.magicAilmentApplyChance);
        }

        if (item.IsArmor)
        {
            ArmorStats armor = item.armorStats;
            TryAdd(RandomItemStatType.ArmorFlatArmor, armor.armor);
            TryAdd(RandomItemStatType.ArmorMagicResist, armor.magicResist);
            TryAdd(RandomItemStatType.ArmorCorruptionResist, armor.corruptionResist);
            TryAdd(RandomItemStatType.ArmorPhysBlockChance, armor.physBlockChance);
            TryAdd(RandomItemStatType.ArmorBonusHealth, armor.bonusHealth);
            TryAdd(RandomItemStatType.ArmorBonusEnergy, armor.bonusEnergy);
            TryAdd(RandomItemStatType.ArmorEnergyEfficiency, armor.energyEfficiency);
            TryAdd(RandomItemStatType.ArmorFlatGuard, armor.flatGuard);
            TryAdd(RandomItemStatType.ArmorMaxGuardPercent, armor.maxGuardPercent);
        }

        if (item.miscEffects.enemyRespawnTimeReductionSeconds > 0f)
            TryAdd(RandomItemStatType.EnemyRespawnTimeReductionSeconds, item.miscEffects.enemyRespawnTimeReductionSeconds);

        results.Sort((a, b) => ComparePoolEntriesForDisplay(a, b, item));
        return results;
    }

    public static int ComparePoolEntriesForDisplay(
        RandomStatPoolEntry a,
        RandomStatPoolEntry b,
        ItemDefinition item = null)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int order = GetRandomStatPoolSortOrder(a.stat).CompareTo(GetRandomStatPoolSortOrder(b.stat));
        if (order != 0)
            return order;

        return string.Compare(
            GetRandomStatDisplayName(a.stat, item),
            GetRandomStatDisplayName(b.stat, item),
            System.StringComparison.OrdinalIgnoreCase);
    }

    public static void GetTemplateMinMax(
        RandomItemStatType stat,
        float currentValue,
        RandomStatValueKind kind,
        out float min,
        out float max)
    {
        float abs = Mathf.Abs(currentValue);
        switch (kind)
        {
            case RandomStatValueKind.FlatInteger:
                if (UsesDefenceFlatTemplateRange(stat))
                {
                    min = 4f;
                    max = 8f;
                    break;
                }

                if (UsesHealthFlatTemplateRange(stat))
                {
                    min = 5f;
                    max = 10f;
                    break;
                }

                if (stat == RandomItemStatType.BonusMana)
                {
                    min = 10f;
                    max = 20f;
                    break;
                }

                if (stat == RandomItemStatType.ArmorFlatGuard)
                {
                    min = 5f;
                    max = 10f;
                    break;
                }

                if (abs <= 3f)
                {
                    min = 1f;
                    max = 2f;
                }
                else
                {
                    min = Mathf.Max(1f, Mathf.Round(abs * 0.05f));
                    max = Mathf.Max(min + 1f, Mathf.Round(abs * 0.12f));
                }
                break;

            case RandomStatValueKind.FlatFloat:
                if (stat == RandomItemStatType.ManaRegen)
                {
                    min = 0.3f;
                    max = 0.5f;
                    break;
                }

                min = Mathf.Max(0.01f, abs * 0.08f);
                max = Mathf.Max(min + 0.01f, abs * 0.2f);
                break;

            case RandomStatValueKind.PercentPoints:
                if (UsesMultiplicativeAttackSpeedRoll(stat) || stat == RandomItemStatType.WeaponCritMultiplier)
                {
                    min = 1f;
                    max = 2f;
                    break;
                }

                float points = StoredValueToPercentPoints(currentValue, stat);
                if (stat == RandomItemStatType.AbilityPowerPercent)
                {
                    if (points < 10f)
                    {
                        min = 3f;
                        max = 6f;
                    }
                    else
                    {
                        min = Mathf.Max(1f, Mathf.Round(points * 0.12f));
                        max = Mathf.Max(min + 1f, Mathf.Round(points * 0.25f));
                    }
                    break;
                }

                if (points < 3f)
                {
                    min = 1f;
                    max = 2f;
                }
                else
                {
                    min = Mathf.Max(1f, Mathf.Round(points * 0.2f));
                    max = Mathf.Max(min + 1f, Mathf.Round(points * 0.4f));
                }
                break;

            default:
                if (abs <= 1f)
                {
                    min = Mathf.Max(0.01f, abs * 0.5f);
                    max = Mathf.Max(min + 0.005f, abs * 1.25f);
                }
                else
                {
                    min = abs * 0.1f;
                    max = abs * 0.25f;
                }
                break;
        }
    }

    /// <summary>Converts stored item stat values into whole percent points for pool min/max display.</summary>
    public static float StoredValueToPercentPoints(float stored, RandomItemStatType stat)
    {
        if (stat == RandomItemStatType.AbilityPowerPercent)
            return stored;

        if (stat == RandomItemStatType.WeaponCritMultiplier || stat == RandomItemStatType.CritMultiplierBonus)
            return stored * 100f;

        return stored * 100f;
    }

    private static bool UsesMultiplicativeAttackSpeedRoll(RandomItemStatType stat) =>
        stat == RandomItemStatType.WeaponAttacksPerSecond;

    private static bool UsesDefenceFlatTemplateRange(RandomItemStatType stat) =>
        stat == RandomItemStatType.BonusArmor ||
        stat == RandomItemStatType.BonusMagicResist ||
        stat == RandomItemStatType.BonusCorruptionResist ||
        stat == RandomItemStatType.ArmorFlatArmor ||
        stat == RandomItemStatType.ArmorMagicResist ||
        stat == RandomItemStatType.ArmorCorruptionResist;

    private static bool UsesHealthFlatTemplateRange(RandomItemStatType stat) =>
        stat == RandomItemStatType.BonusHealth ||
        stat == RandomItemStatType.ArmorBonusHealth;

    private static bool IsEffectivelyZero(float value) => Mathf.Abs(value) < 0.0001f;

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

            case RandomItemStatType.LifeRegen:
            case RandomItemStatType.EnergyRegen:
            case RandomItemStatType.ManaRegen:
            case RandomItemStatType.PoisonDurationBonus:
            case RandomItemStatType.WeaponAttackRange:
            case RandomItemStatType.AttackRangeBonus:
            case RandomItemStatType.EnemyRespawnTimeReductionSeconds:
                return RandomStatValueKind.FlatFloat;

            default:
                return RandomStatValueKind.PercentPoints;
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
                if (item.IsWeapon)
                    ApplyWeaponAttackSpeedRoll(item, primary, kind);
                else
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
            case RandomItemStatType.ChillChance:
                item.bonusStats.chillChance = Mathf.Clamp01(item.bonusStats.chillChance + primary);
                break;
            case RandomItemStatType.ShockChance:
                item.bonusStats.shockChance = Mathf.Clamp01(item.bonusStats.shockChance + primary);
                break;
            case RandomItemStatType.BurnMultiplier:
                item.bonusStats.burnExplosionMultiplierBonus += primary;
                break;
            case RandomItemStatType.ChillMultiplier:
                item.bonusStats.chillSlowPerStackBonus += primary;
                break;
            case RandomItemStatType.ShockMultiplier:
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
                ApplyWeaponAttackSpeedRoll(item, primary, kind);
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

    private static void ApplyWeaponAttackSpeedRoll(ItemDefinition item, float primary, RandomStatValueKind kind)
    {
        if (!item || !item.IsWeapon)
            return;

        // Percent Points: roll 2 → +0.02 APS (0.6 → 0.62). Legacy Fraction entries still add the rolled fraction directly.
        if (kind == RandomStatValueKind.Fraction)
        {
            item.weaponStats.attacksPerSecond = Mathf.Max(
                0.01f,
                item.weaponStats.attacksPerSecond * (1f + primary));
            return;
        }

        item.weaponStats.attacksPerSecond = Mathf.Max(0.01f, item.weaponStats.attacksPerSecond + primary);
    }

    private static float RollValue(float min, float max, RandomStatValueKind kind, RandomItemStatType stat)
    {
        float raw = min >= max ? min : Random.Range(min, max);
        return ConvertRolledValue(raw, kind, stat);
    }

    /// <summary>Ability power stores literal percent points (5 = +5%). All other % stats store fractions (0.05 = +5%).</summary>
    private static bool StoresLiteralPercentPoints(RandomItemStatType stat) =>
        stat == RandomItemStatType.AbilityPowerPercent;

    private static float ConvertRolledValue(float raw, RandomStatValueKind kind, RandomItemStatType stat)
    {
        switch (kind)
        {
            case RandomStatValueKind.PercentPoints:
                return StoresLiteralPercentPoints(stat) ? raw : raw / 100f;
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

    /// <summary>Sort key for database pool listings (matches weapon tooltip stat order).</summary>
    public static int GetRandomStatPoolSortOrder(RandomItemStatType stat)
    {
        switch (stat)
        {
            case RandomItemStatType.WeaponMinPhysicalDamage: return 100;
            case RandomItemStatType.WeaponMinFireDamage: return 101;
            case RandomItemStatType.WeaponMinIceDamage: return 102;
            case RandomItemStatType.WeaponMinLightningDamage: return 103;
            case RandomItemStatType.WeaponMinCorruptionDamage: return 104;

            case RandomItemStatType.WeaponMaxPhysicalDamage: return 200;
            case RandomItemStatType.WeaponMaxFireDamage: return 201;
            case RandomItemStatType.WeaponMaxIceDamage: return 202;
            case RandomItemStatType.WeaponMaxLightningDamage: return 203;
            case RandomItemStatType.WeaponMaxCorruptionDamage: return 204;
            case RandomItemStatType.WeaponCorruptionDamageRange: return 205;

            case RandomItemStatType.WeaponAttacksPerSecond: return 300;
            case RandomItemStatType.AttackSpeedPercent: return 301;

            case RandomItemStatType.WeaponCritChance: return 400;
            case RandomItemStatType.CritChanceBonus: return 401;
            case RandomItemStatType.WeaponCritMultiplier: return 402;
            case RandomItemStatType.CritMultiplierBonus: return 403;

            case RandomItemStatType.WeaponMagicAilmentApplyChance: return 500;
            case RandomItemStatType.BleedChance: return 510;
            case RandomItemStatType.BleedMultiplier: return 511;
            case RandomItemStatType.PoisonChance: return 512;
            case RandomItemStatType.PoisonMultiplier: return 513;
            case RandomItemStatType.PoisonDurationBonus: return 514;
            case RandomItemStatType.PoisonMaxStacksBonus: return 515;
            case RandomItemStatType.BurnChance: return 516;
            case RandomItemStatType.ChillChance: return 517;
            case RandomItemStatType.ShockChance: return 518;
            case RandomItemStatType.BurnMultiplier: return 519;
            case RandomItemStatType.ChillMultiplier: return 520;
            case RandomItemStatType.ShockMultiplier: return 521;

            case RandomItemStatType.BonusMana: return 600;
            case RandomItemStatType.ManaRegen: return 601;

            case RandomItemStatType.ParryChance: return 700;
            case RandomItemStatType.StunChance: return 701;

            case RandomItemStatType.WeaponAttackRange: return 900;
            case RandomItemStatType.AttackRangeBonus: return 901;

            default: return 750;
        }
    }

    /// <summary>Player-facing label for a random pool stat (database / encyclopedia tooltips).</summary>
    public static string GetRandomStatDisplayName(RandomItemStatType stat, ItemDefinition item = null)
    {
        switch (stat)
        {
            case RandomItemStatType.BonusHealth:
            case RandomItemStatType.ArmorBonusHealth:
                return "Health";
            case RandomItemStatType.BonusEnergy:
            case RandomItemStatType.ArmorBonusEnergy:
                return "Energy";
            case RandomItemStatType.BonusMana:
                return "Mana";
            case RandomItemStatType.BonusArmor:
            case RandomItemStatType.ArmorFlatArmor:
                return "Armour";
            case RandomItemStatType.BonusMagicResist:
            case RandomItemStatType.ArmorMagicResist:
                return "Magic Res";
            case RandomItemStatType.BonusCorruptionResist:
            case RandomItemStatType.ArmorCorruptionResist:
                return "Corruption Res";
            case RandomItemStatType.BonusPhysBlockChance:
            case RandomItemStatType.ArmorPhysBlockChance:
                return "Phys Block";
            case RandomItemStatType.ManaRegen:
                return "Mana Regen";
            case RandomItemStatType.LifeRegen:
                return "Life Regen";
            case RandomItemStatType.EnergyRegen:
                return "Energy Regen";
            case RandomItemStatType.CritChanceBonus:
            case RandomItemStatType.WeaponCritChance:
                return "Crit Chance";
            case RandomItemStatType.CritMultiplierBonus:
            case RandomItemStatType.WeaponCritMultiplier:
                return "Crit Multi";
            case RandomItemStatType.AttackRangeBonus:
            case RandomItemStatType.WeaponAttackRange:
                return "Range";
            case RandomItemStatType.WeaponAttacksPerSecond:
                return "Speed";
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
                return "Guard";
            case RandomItemStatType.ArmorMaxGuardPercent:
                return "Max Guard";
            case RandomItemStatType.BurnChance:
                return "Burn Chance";
            case RandomItemStatType.ChillChance:
                return "Chill Chance";
            case RandomItemStatType.ShockChance:
                return "Shock Chance";
            case RandomItemStatType.BurnMultiplier:
                return "Burn Multiplier";
            case RandomItemStatType.ChillMultiplier:
                return "Chill Effect";
            case RandomItemStatType.ShockMultiplier:
                return "Shock Damage Amount";
            case RandomItemStatType.BleedMultiplier:
                return "Bleed Multi";
            case RandomItemStatType.PoisonMultiplier:
                return "Poison Multi";
            case RandomItemStatType.WeaponMagicAilmentApplyChance:
                return GetMagicAilmentChanceRollName(item);
            case RandomItemStatType.ParryChance:
                return "Parry Chance";
            case RandomItemStatType.StunChance:
                return "Stun Chance";
            case RandomItemStatType.WeaponCorruptionDamageRange:
                return "Corruption Damage";
            case RandomItemStatType.EnemyRespawnTimeReductionSeconds:
                return "Enemy Respawn Reduction";
            default:
                return SplitCamelCase(stat.ToString());
        }
    }

    /// <summary>One line for the database item tooltip random-stat pool section.</summary>
    public static string FormatPoolEntryDatabaseLine(RandomStatPoolEntry entry, ItemDefinition item = null)
    {
        if (entry == null || !entry.IsValid)
            return "";

        string name = GetRandomStatDisplayName(entry.stat, item);
        string range = FormatPoolEntryValueRange(entry);
        return string.IsNullOrWhiteSpace(range) ? name : $"{name} ({range})";
    }

    private static string GetMagicAilmentChanceRollName(ItemDefinition item)
    {
        if (!item || !item.IsWeapon)
            return "Magic Ailment Apply Chance";

        return item.weaponStats.magicAttackType switch
        {
            MagicAttackType.Fire => "Burn Chance",
            MagicAttackType.Ice => "Chill Chance",
            MagicAttackType.Lightning => "Shock Chance",
            _ => "Magic Ailment Apply Chance"
        };
    }

    private static string FormatPoolEntryValueRange(RandomStatPoolEntry entry)
    {
        float min = entry.minValue;
        float max = entry.maxValue;

        if (entry.stat == RandomItemStatType.WeaponCorruptionDamageRange && entry.rollSecondaryValue)
        {
            string primary = FormatPoolValueSpan(min, max, entry.valueKind, entry.stat);
            string secondary = FormatPoolValueSpan(entry.secondaryMinValue, entry.secondaryMaxValue, entry.valueKind, entry.stat);
            return $"{primary} to {secondary}";
        }

        return FormatPoolValueSpan(min, max, entry.valueKind, entry.stat);
    }

    private static string FormatPoolValueSpan(float min, float max, RandomStatValueKind kind, RandomItemStatType stat)
    {
        if (Mathf.Approximately(min, max))
            return FormatSinglePoolValue(min, kind, stat);

        return $"{FormatSinglePoolValue(min, kind, stat)}-{FormatSinglePoolValue(max, kind, stat)}";
    }

    private static string FormatSinglePoolValue(float value, RandomStatValueKind kind, RandomItemStatType stat)
    {
        if (stat == RandomItemStatType.WeaponAttacksPerSecond && kind == RandomStatValueKind.PercentPoints)
            return FormatCompactFloat(value / 100f);

        switch (kind)
        {
            case RandomStatValueKind.FlatInteger:
                return Mathf.RoundToInt(value).ToString();

            case RandomStatValueKind.FlatFloat:
                return FormatCompactFloat(value);

            case RandomStatValueKind.PercentPoints:
                return StoresLiteralPercentPoints(stat)
                    ? $"{FormatCompactFloat(value)}%"
                    : $"{FormatCompactFloat(value)}%";

            case RandomStatValueKind.Fraction:
                if (Mathf.Abs(value) <= 1f)
                    return $"{FormatCompactFloat(value * 100f)}%";
                return FormatCompactFloat(value);

            default:
                return FormatCompactFloat(value);
        }
    }

    private static string FormatCompactFloat(float value)
    {
        float abs = Mathf.Abs(value);
        if (abs >= 100f)
            return value.ToString("0");
        if (abs >= 10f)
            return value.ToString("0.#");
        if (abs >= 1f)
            return value.ToString("0.##");
        return value.ToString("0.###");
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
