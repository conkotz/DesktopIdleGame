using System.Collections.Generic;
using UnityEngine;

/// <summary>Which stat a random pool entry can roll onto an item instance.</summary>
public enum RandomItemStatType
{
    BonusHealth,
    BonusEnergy,
    BonusMana,
    BonusArmour,
    BonusMagicResist,
    BonusCorruptionResist,
    BonusPhysBlockChance,
    BonusPhysBlockMitigation,
    LifeRegen,
    EnergyRegen,
    ManaRegen,
    EnergyEfficiency,
    LifeSteal,
    MoveSpeedPercent,
    PhysicalDamageFlat,
    MeleePhysicalDamagePercent,
    GlobalPhysicalDamagePercent,
    RangedPhysicalDamagePercent,
    MagicDamageFlat,
    MagicDamagePercent,
    FireSkillDamagePercent,
    IceSkillDamagePercent,
    LightningSkillDamagePercent,
    SpellDamagePercent,
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

    ArmourFlatArmour,
    ArmourMagicResist,
    ArmourCorruptionResist,
    ArmourPhysBlockChance,
    ArmourPhysBlockMitigation,
    ArmourBonusHealth,
    ArmourBonusEnergy,
    ArmourEnergyEfficiency,
    ArmourFlatGuard,
    ArmourMaxGuardPercent,

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

    /// <summary>Additive burn/chill/shock apply chance while using elemental magic attacks.</summary>
    AllElementalAilmentChance,
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

[System.Flags]
public enum RandomStatPoolPackageFlags
{
    None = 0,
    Bleed = 1 << 0,
    Poison = 1 << 1,
    Fire = 1 << 2,
    Lightning = 1 << 3,
    Ice = 1 << 4,
    Defensive = 1 << 5,
    AttackRange = 1 << 6,
    Minion = 1 << 7,
    Crit = 1 << 8,
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
    /// <summary>Common, Uncommon, and Rare items roll this many identified affix lines.</summary>
    public const int StandardIdentifiedAffixRollCount = 3;

    /// <summary>Epic and Legendary items roll this many identified affix lines.</summary>
    public const int HighTierIdentifiedAffixRollCount = 4;

    /// <summary>Same random affix type can be rolled at most this many times per identification.</summary>
    public const int MaxIdentifiedRollsPerStatType = 2;

    public static int GetRollCountForRarity(ItemRarity rarity, ItemDefinition item = null)
    {
        return rarity switch
        {
            ItemRarity.Epic => HighTierIdentifiedAffixRollCount,
            ItemRarity.Legendary => HighTierIdentifiedAffixRollCount,
            _ => StandardIdentifiedAffixRollCount
        };
    }

    /// <summary>
    /// True when <paramref name="pool"/> can roll an affix that the given enhancement scroll targets.
    /// </summary>
    public static bool PoolCanSupplyEnhancementScrollStat(
        IReadOnlyList<RandomStatPoolEntry> pool,
        EnhancementScrollTargetStat stat)
    {
        if (pool == null || pool.Count == 0 || stat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return false;

        for (int i = 0; i < pool.Count; i++)
        {
            RandomStatPoolEntry entry = pool[i];
            if (entry == null || !entry.IsValid)
                continue;

            if (RandomStatTypeMatchesEnhancementScroll(entry.stat, stat))
                return true;
        }

        return false;
    }

    private static bool RandomStatTypeMatchesEnhancementScroll(
        RandomItemStatType poolStat,
        EnhancementScrollTargetStat scrollStat)
    {
        switch (scrollStat)
        {
            case EnhancementScrollTargetStat.PhysicalDamage:
                return poolStat == RandomItemStatType.PhysicalDamageFlat
                       || poolStat == RandomItemStatType.WeaponMinPhysicalDamage
                       || poolStat == RandomItemStatType.WeaponMaxPhysicalDamage
                       || poolStat == RandomItemStatType.MeleePhysicalDamagePercent
                       || poolStat == RandomItemStatType.GlobalPhysicalDamagePercent
                       || poolStat == RandomItemStatType.RangedPhysicalDamagePercent;

            case EnhancementScrollTargetStat.MagicDamage:
                return poolStat == RandomItemStatType.MagicDamageFlat
                       || poolStat == RandomItemStatType.MagicDamagePercent;

            case EnhancementScrollTargetStat.FireDamage:
                return poolStat == RandomItemStatType.WeaponMinFireDamage
                       || poolStat == RandomItemStatType.WeaponMaxFireDamage;

            case EnhancementScrollTargetStat.IceDamage:
                return poolStat == RandomItemStatType.WeaponMinIceDamage
                       || poolStat == RandomItemStatType.WeaponMaxIceDamage;

            case EnhancementScrollTargetStat.LightningDamage:
                return poolStat == RandomItemStatType.WeaponMinLightningDamage
                       || poolStat == RandomItemStatType.WeaponMaxLightningDamage;

            case EnhancementScrollTargetStat.CorruptionDamage:
                return poolStat == RandomItemStatType.CorruptionDamageFlat
                       || poolStat == RandomItemStatType.CorruptionDamagePercent
                       || poolStat == RandomItemStatType.WeaponMinCorruptionDamage
                       || poolStat == RandomItemStatType.WeaponMaxCorruptionDamage
                       || poolStat == RandomItemStatType.WeaponCorruptionDamageRange;

            case EnhancementScrollTargetStat.Health:
                return poolStat == RandomItemStatType.BonusHealth
                       || poolStat == RandomItemStatType.ArmourBonusHealth;

            case EnhancementScrollTargetStat.Energy:
                return poolStat == RandomItemStatType.BonusEnergy
                       || poolStat == RandomItemStatType.ArmourBonusEnergy;

            case EnhancementScrollTargetStat.Mana:
                return poolStat == RandomItemStatType.BonusMana;

            case EnhancementScrollTargetStat.Armour:
                return poolStat == RandomItemStatType.BonusArmour
                       || poolStat == RandomItemStatType.ArmourFlatArmour;

            case EnhancementScrollTargetStat.MagicResist:
                return poolStat == RandomItemStatType.BonusMagicResist
                       || poolStat == RandomItemStatType.ArmourMagicResist;

            case EnhancementScrollTargetStat.CorruptionResist:
                return poolStat == RandomItemStatType.BonusCorruptionResist
                       || poolStat == RandomItemStatType.ArmourCorruptionResist;

            case EnhancementScrollTargetStat.CritChance:
                return poolStat == RandomItemStatType.CritChanceBonus
                       || poolStat == RandomItemStatType.WeaponCritChance
                       || poolStat == RandomItemStatType.MinionCritChance;

            case EnhancementScrollTargetStat.CritMultiplier:
                return poolStat == RandomItemStatType.CritMultiplierBonus
                       || poolStat == RandomItemStatType.WeaponCritMultiplier;

            case EnhancementScrollTargetStat.AttackSpeed:
                return poolStat == RandomItemStatType.AttackSpeedPercent
                       || poolStat == RandomItemStatType.WeaponAttacksPerSecond
                       || poolStat == RandomItemStatType.MinionAttackSpeedPercent;

            case EnhancementScrollTargetStat.LifeSteal:
                return poolStat == RandomItemStatType.LifeSteal;

            case EnhancementScrollTargetStat.MoveSpeed:
                return poolStat == RandomItemStatType.MoveSpeedPercent;

            case EnhancementScrollTargetStat.PoisonChance:
                return poolStat == RandomItemStatType.PoisonChance;

            case EnhancementScrollTargetStat.PoisonMultiplier:
                return poolStat == RandomItemStatType.PoisonMultiplier
                       || poolStat == RandomItemStatType.PoisonDurationBonus
                       || poolStat == RandomItemStatType.PoisonMaxStacksBonus;

            case EnhancementScrollTargetStat.BurnChance:
                return poolStat == RandomItemStatType.BurnChance
                       || poolStat == RandomItemStatType.AllElementalAilmentChance
                       || poolStat == RandomItemStatType.WeaponMagicAilmentApplyChance;

            case EnhancementScrollTargetStat.ChillChance:
                return poolStat == RandomItemStatType.ChillChance
                       || poolStat == RandomItemStatType.AllElementalAilmentChance
                       || poolStat == RandomItemStatType.WeaponMagicAilmentApplyChance;

            case EnhancementScrollTargetStat.ShockChance:
                return poolStat == RandomItemStatType.ShockChance
                       || poolStat == RandomItemStatType.AllElementalAilmentChance
                       || poolStat == RandomItemStatType.WeaponMagicAilmentApplyChance;

            case EnhancementScrollTargetStat.BurnMultiplier:
                return poolStat == RandomItemStatType.BurnMultiplier;

            case EnhancementScrollTargetStat.EnergyEfficiency:
                return poolStat == RandomItemStatType.EnergyEfficiency
                       || poolStat == RandomItemStatType.ArmourEnergyEfficiency;

            case EnhancementScrollTargetStat.FlatGuard:
                return poolStat == RandomItemStatType.ArmourFlatGuard
                       || poolStat == RandomItemStatType.ArmourMaxGuardPercent;

            case EnhancementScrollTargetStat.ManaRegen:
                return poolStat == RandomItemStatType.ManaRegen;

            case EnhancementScrollTargetStat.SpellDamage:
                return poolStat == RandomItemStatType.SpellDamagePercent;

            case EnhancementScrollTargetStat.FireDamagePercent:
                return poolStat == RandomItemStatType.FireSkillDamagePercent;

            case EnhancementScrollTargetStat.IceDamagePercent:
                return poolStat == RandomItemStatType.IceSkillDamagePercent;

            case EnhancementScrollTargetStat.LightningDamagePercent:
                return poolStat == RandomItemStatType.LightningSkillDamagePercent;

            default:
                return false;
        }
    }

    public static bool ShouldRollOnAcquire(ItemDefinition def, ItemDatabase db)
    {
        if (!def || !def.HasRandomStatPool)
            return false;

        if (!def.IsWeapon && !def.IsArmour && !def.IsJewelry)
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

        // Keep the authored pool on the clone; affixes roll when the player identifies the item.
        clone.CopyRandomStatPoolFrom(baseDef);
        clone.randomStatsPendingIdentification = true;
        return clone;
    }

    public static void ApplyRolls(ItemDefinition item, List<RandomStatPoolEntry> pool, ItemRarity rarity)
    {
        if (!item || pool == null || pool.Count == 0)
            return;

        int rollCount = GetRollCountForRarity(rarity, item);
        var available = new List<RandomStatPoolEntry>();
        for (int i = 0; i < pool.Count; i++)
        {
            RandomStatPoolEntry entry = pool[i];
            if (entry != null && entry.IsValid)
                available.Add(entry);
        }

        var rollsPerStat = new Dictionary<RandomItemStatType, int>();
        var pickableIndices = new List<int>();

        for (int r = 0; r < rollCount; r++)
        {
            pickableIndices.Clear();
            for (int i = 0; i < available.Count; i++)
            {
                RandomItemStatType stat = available[i].stat;
                rollsPerStat.TryGetValue(stat, out int timesRolled);
                if (timesRolled < MaxIdentifiedRollsPerStatType)
                    pickableIndices.Add(i);
            }

            if (pickableIndices.Count == 0)
                break;

            int pickIndex = PickWeightedIndex(available, pickableIndices);
            if (pickIndex < 0)
                break;

            RandomStatPoolEntry picked = available[pickIndex];
            ApplyEntry(item, picked);

            rollsPerStat.TryGetValue(picked.stat, out int rolled);
            rollsPerStat[picked.stat] = rolled + 1;
        }
    }

    private enum TemplateRarityBand
    {
        CommonUncommon,
        Rare,
        Epic,
        Legendary
    }

    private readonly struct PoolValueBand
    {
        public readonly float Min;
        public readonly float Max;

        public PoolValueBand(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    private readonly struct CombatWeaponPoolTemplate
    {
        public readonly PoolValueBand Damage;
        public readonly PoolValueBand Speed;
        public readonly PoolValueBand CritChance;
        public readonly PoolValueBand CritMulti;
        public readonly PoolValueBand AilmentChance;
        public readonly PoolValueBand AilmentMulti;
        public readonly PoolValueBand Range;

        public CombatWeaponPoolTemplate(
            PoolValueBand damage,
            PoolValueBand speed,
            PoolValueBand critChance,
            PoolValueBand critMulti,
            PoolValueBand ailmentChance,
            PoolValueBand ailmentMulti,
            PoolValueBand range)
        {
            Damage = damage;
            Speed = speed;
            CritChance = critChance;
            CritMulti = critMulti;
            AilmentChance = ailmentChance;
            AilmentMulti = ailmentMulti;
            Range = range;
        }
    }

    private readonly struct MagicWeaponPoolBands
    {
        public readonly PoolValueBand SpellDamage;
        public readonly PoolValueBand ElementSkillDamage;
        public readonly PoolValueBand CritChance;
        public readonly PoolValueBand CritMulti;
        public readonly PoolValueBand Mana;
        public readonly PoolValueBand ElementalAilmentChance;
        public readonly PoolValueBand ShockChance;
        public readonly PoolValueBand BurnMultiplier;
        public readonly PoolValueBand ChillMultiplier;
        public readonly PoolValueBand ShockMultiplier;

        public MagicWeaponPoolBands(
            PoolValueBand spellDamage,
            PoolValueBand elementSkillDamage,
            PoolValueBand critChance,
            PoolValueBand critMulti,
            PoolValueBand mana,
            PoolValueBand elementalAilmentChance,
            PoolValueBand shockChance,
            PoolValueBand burnMultiplier,
            PoolValueBand chillMultiplier,
            PoolValueBand shockMultiplier)
        {
            SpellDamage = spellDamage;
            ElementSkillDamage = elementSkillDamage;
            CritChance = critChance;
            CritMulti = critMulti;
            Mana = mana;
            ElementalAilmentChance = elementalAilmentChance;
            ShockChance = shockChance;
            BurnMultiplier = burnMultiplier;
            ChillMultiplier = chillMultiplier;
            ShockMultiplier = shockMultiplier;
        }
    }

    private readonly struct WeaponDefensivePackageBands
    {
        public readonly PoolValueBand BlockChance;
        public readonly PoolValueBand BlockMitigation;
        public readonly PoolValueBand ParryChance;

        public WeaponDefensivePackageBands(
            PoolValueBand blockChance,
            PoolValueBand blockMitigation,
            PoolValueBand parryChance)
        {
            BlockChance = blockChance;
            BlockMitigation = blockMitigation;
            ParryChance = parryChance;
        }
    }

    private readonly struct ArmourTypePoolBands
    {
        public readonly PoolValueBand Health;
        public readonly PoolValueBand MagicResist;
        public readonly PoolValueBand CorruptionResist;
        public readonly PoolValueBand Armour;
        public readonly PoolValueBand Mana;
        public readonly PoolValueBand ManaRegen;
        public readonly PoolValueBand MagicDamagePercent;
        public readonly PoolValueBand MoveSpeedPercent;
        public readonly PoolValueBand RangedDamagePercent;
        public readonly PoolValueBand MeleeDamagePercent;
        public readonly PoolValueBand FlatGuard;
        public readonly PoolValueBand EnergyEfficiency;
        public readonly PoolValueBand PhysBlockChance;
        public readonly PoolValueBand PhysBlockMitigation;

        public ArmourTypePoolBands(
            PoolValueBand health,
            PoolValueBand magicResist,
            PoolValueBand corruptionResist,
            PoolValueBand armour,
            PoolValueBand mana,
            PoolValueBand manaRegen,
            PoolValueBand magicDamagePercent,
            PoolValueBand moveSpeedPercent,
            PoolValueBand rangedDamagePercent,
            PoolValueBand meleeDamagePercent,
            PoolValueBand flatGuard,
            PoolValueBand energyEfficiency,
            PoolValueBand physBlockChance,
            PoolValueBand physBlockMitigation)
        {
            Health = health;
            MagicResist = magicResist;
            CorruptionResist = corruptionResist;
            Armour = armour;
            Mana = mana;
            ManaRegen = manaRegen;
            MagicDamagePercent = magicDamagePercent;
            MoveSpeedPercent = moveSpeedPercent;
            RangedDamagePercent = rangedDamagePercent;
            MeleeDamagePercent = meleeDamagePercent;
            FlatGuard = flatGuard;
            EnergyEfficiency = energyEfficiency;
            PhysBlockChance = physBlockChance;
            PhysBlockMitigation = physBlockMitigation;
        }

        private static PoolValueBand None => new(0f, 0f);

        public static ArmourTypePoolBands Light(TemplateRarityBand band) => band switch
        {
            TemplateRarityBand.Rare => new ArmourTypePoolBands(
                new(10f, 15f), new(5f, 8f), None, None, new(6f, 10f), new(0.4f, 0.6f), new(2f, 4f),
                None, None, None, None, None, None, None),
            TemplateRarityBand.Epic => new ArmourTypePoolBands(
                new(12f, 18f), new(6f, 10f), None, None, new(8f, 12f), new(0.5f, 0.8f), new(3f, 6f),
                None, None, None, None, None, None, None),
            TemplateRarityBand.Legendary => new ArmourTypePoolBands(
                new(16f, 24f), new(8f, 14f), None, None, new(10f, 16f), new(0.7f, 1f), new(4f, 8f),
                None, None, None, None, None, None, None),
            _ => new ArmourTypePoolBands(
                new(8f, 12f), new(4f, 6f), None, None, new(5f, 8f), new(0.3f, 0.5f), new(1f, 3f),
                None, None, None, None, None, None, None)
        };

        public static ArmourTypePoolBands Medium(TemplateRarityBand band) => band switch
        {
            TemplateRarityBand.Rare => new ArmourTypePoolBands(
                new(12f, 22f), new(5f, 9f), new(4f, 7f), None, None, None, None,
                new(2f, 4f), new(2f, 4f), None, None, new(2f, 4f), None, None),
            TemplateRarityBand.Epic => new ArmourTypePoolBands(
                new(15f, 28f), new(6f, 11f), new(5f, 9f), None, None, None, None,
                new(2f, 5f), new(3f, 5f), None, None, new(2f, 5f), None, None),
            TemplateRarityBand.Legendary => new ArmourTypePoolBands(
                new(20f, 35f), new(8f, 14f), new(6f, 12f), None, None, None, None,
                new(3f, 6f), new(4f, 7f), None, None, new(3f, 6f), None, None),
            _ => new ArmourTypePoolBands(
                new(10f, 18f), new(4f, 7f), new(3f, 5f), None, None, None, None,
                new(1f, 3f), new(1f, 3f), None, None, new(1f, 3f), None, None)
        };

        public static ArmourTypePoolBands Heavy(TemplateRarityBand band) => band switch
        {
            TemplateRarityBand.Rare => new ArmourTypePoolBands(
                new(15f, 25f), new(5f, 9f), new(4f, 7f), new(7f, 14f), None, None, None,
                None, None, new(2f, 4f), new(8f, 14f), new(2f, 4f), None, None),
            TemplateRarityBand.Epic => new ArmourTypePoolBands(
                new(18f, 30f), new(6f, 11f), new(5f, 9f), new(9f, 18f), None, None, None,
                None, None, new(3f, 5f), new(10f, 18f), new(2f, 5f), None, None),
            TemplateRarityBand.Legendary => new ArmourTypePoolBands(
                new(22f, 38f), new(8f, 14f), new(6f, 12f), new(12f, 24f), None, None, None,
                None, None, new(4f, 7f), new(14f, 24f), new(3f, 6f), None, None),
            _ => new ArmourTypePoolBands(
                new(12f, 20f), new(4f, 7f), new(3f, 5f), new(5f, 10f), None, None, None,
                None, None, new(1f, 3f), new(6f, 10f), new(1f, 3f), None, None)
        };

        public static ArmourTypePoolBands Shield(TemplateRarityBand band) => band switch
        {
            TemplateRarityBand.Rare => new ArmourTypePoolBands(
                None, None, None, None, None, None, None, None, None, None, None, new(2f, 4f),
                new(7f, 12f), new(3f, 6f)),
            TemplateRarityBand.Epic => new ArmourTypePoolBands(
                None, None, None, None, None, None, None, None, None, None, None, new(2f, 5f),
                new(9f, 15f), new(4f, 8f)),
            TemplateRarityBand.Legendary => new ArmourTypePoolBands(
                None, None, None, None, None, None, None, None, None, None, None, new(3f, 6f),
                new(12f, 18f), new(5f, 10f)),
            _ => new ArmourTypePoolBands(
                None, None, None, None, None, None, None, None, None, None, None, new(1f, 3f),
                new(5f, 10f), new(2f, 5f))
        };
    }

    private static bool IsShieldArmour(ItemDefinition item) =>
        item != null && item.IsArmour && item.equipSlot == EquipSlot.OffHand;

    private static ArmourType GetArmourTemplateType(ItemDefinition item)
    {
        if (item == null)
            return ArmourType.Medium;

        return item.armourStats.armourType switch
        {
            ArmourType.Light => ArmourType.Light,
            ArmourType.Heavy => ArmourType.Heavy,
            _ => ArmourType.Medium
        };
    }

    private static ArmourTypePoolBands GetArmourTypePoolBands(ItemDefinition item, TemplateRarityBand band)
    {
        if (IsShieldArmour(item))
            return ArmourTypePoolBands.Shield(band);

        return GetArmourTemplateType(item) switch
        {
            ArmourType.Light => ArmourTypePoolBands.Light(band),
            ArmourType.Heavy => ArmourTypePoolBands.Heavy(band),
            _ => ArmourTypePoolBands.Medium(band)
        };
    }

    private static TemplateRarityBand GetTemplateRarityBand(ItemRarity rarity) =>
        rarity switch
        {
            ItemRarity.Rare => TemplateRarityBand.Rare,
            ItemRarity.Epic => TemplateRarityBand.Epic,
            ItemRarity.Legendary => TemplateRarityBand.Legendary,
            _ => TemplateRarityBand.CommonUncommon
        };

    private static bool IsSpellScalingMagicWeapon(ItemDefinition item) =>
        item != null
        && item.IsWeapon
        && (item.weaponStats.mainHandArchetype == MainHandWeaponArchetype.Wand
            || item.weaponStats.mainHandArchetype == MainHandWeaponArchetype.Staff);

    private static CombatWeaponPoolTemplate GetCombatWeaponPoolTemplate(
        WeaponWeight weight,
        TemplateRarityBand band)
    {
        return (weight, band) switch
        {
            (WeaponWeight.Light, TemplateRarityBand.CommonUncommon) => new CombatWeaponPoolTemplate(
                new PoolValueBand(1f, 2f), new PoolValueBand(4f, 6f), new PoolValueBand(2f, 5f), new PoolValueBand(4f, 9f),
                new PoolValueBand(2f, 4f), new PoolValueBand(4f, 9f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Medium, TemplateRarityBand.CommonUncommon) => new CombatWeaponPoolTemplate(
                new PoolValueBand(2f, 4f), new PoolValueBand(3f, 5f), new PoolValueBand(3f, 7f), new PoolValueBand(6f, 11f),
                new PoolValueBand(2f, 6f), new PoolValueBand(6f, 11f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Heavy, TemplateRarityBand.CommonUncommon) => new CombatWeaponPoolTemplate(
                new PoolValueBand(3f, 6f), new PoolValueBand(2f, 4f), new PoolValueBand(4f, 8f), new PoolValueBand(7f, 12f),
                new PoolValueBand(3f, 7f), new PoolValueBand(7f, 13f), new PoolValueBand(0.5f, 1f)),

            (WeaponWeight.Light, TemplateRarityBand.Rare) => new CombatWeaponPoolTemplate(
                new PoolValueBand(2f, 3f), new PoolValueBand(5f, 8f), new PoolValueBand(3f, 6f), new PoolValueBand(6f, 12f),
                new PoolValueBand(3f, 6f), new PoolValueBand(6f, 12f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Medium, TemplateRarityBand.Rare) => new CombatWeaponPoolTemplate(
                new PoolValueBand(3f, 5f), new PoolValueBand(4f, 6f), new PoolValueBand(4f, 8f), new PoolValueBand(7f, 13f),
                new PoolValueBand(3f, 7f), new PoolValueBand(7f, 13f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Heavy, TemplateRarityBand.Rare) => new CombatWeaponPoolTemplate(
                new PoolValueBand(4f, 8f), new PoolValueBand(3f, 5f), new PoolValueBand(5f, 10f), new PoolValueBand(8f, 15f),
                new PoolValueBand(4f, 8f), new PoolValueBand(8f, 15f), new PoolValueBand(0.5f, 1f)),

            (WeaponWeight.Light, TemplateRarityBand.Epic) => new CombatWeaponPoolTemplate(
                new PoolValueBand(2f, 4f), new PoolValueBand(6f, 9f), new PoolValueBand(4f, 8f), new PoolValueBand(7f, 15f),
                new PoolValueBand(4f, 7f), new PoolValueBand(7f, 15f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Medium, TemplateRarityBand.Epic) => new CombatWeaponPoolTemplate(
                new PoolValueBand(4f, 6f), new PoolValueBand(5f, 8f), new PoolValueBand(5f, 9f), new PoolValueBand(8f, 16f),
                new PoolValueBand(4f, 8f), new PoolValueBand(8f, 16f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Heavy, TemplateRarityBand.Epic) => new CombatWeaponPoolTemplate(
                new PoolValueBand(5f, 9f), new PoolValueBand(4f, 6f), new PoolValueBand(6f, 11f), new PoolValueBand(9f, 18f),
                new PoolValueBand(5f, 9f), new PoolValueBand(9f, 18f), new PoolValueBand(0.5f, 1f)),

            (WeaponWeight.Light, TemplateRarityBand.Legendary) => new CombatWeaponPoolTemplate(
                new PoolValueBand(3f, 5f), new PoolValueBand(7f, 11f), new PoolValueBand(5f, 10f), new PoolValueBand(9f, 18f),
                new PoolValueBand(5f, 10f), new PoolValueBand(9f, 18f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Medium, TemplateRarityBand.Legendary) => new CombatWeaponPoolTemplate(
                new PoolValueBand(5f, 8f), new PoolValueBand(6f, 10f), new PoolValueBand(6f, 11f), new PoolValueBand(10f, 20f),
                new PoolValueBand(5f, 10f), new PoolValueBand(10f, 20f), new PoolValueBand(0.5f, 1f)),
            (WeaponWeight.Heavy, TemplateRarityBand.Legendary) => new CombatWeaponPoolTemplate(
                new PoolValueBand(6f, 12f), new PoolValueBand(5f, 8f), new PoolValueBand(7f, 13f), new PoolValueBand(11f, 22f),
                new PoolValueBand(6f, 11f), new PoolValueBand(11f, 22f), new PoolValueBand(0.5f, 1f)),

            _ => GetCombatWeaponPoolTemplate(WeaponWeight.Medium, TemplateRarityBand.CommonUncommon)
        };
    }


    private static void AddTemplatePoolEntry(
        List<RandomStatPoolEntry> results,
        RandomItemStatType stat,
        PoolValueBand band,
        RandomStatValueKind kind,
        float weight = 1f)
    {
        if (band.Max <= 0f)
            return;

        results.Add(new RandomStatPoolEntry
        {
            stat = stat,
            weight = weight,
            valueKind = kind,
            minValue = band.Min,
            maxValue = band.Max,
        });
    }

    private static bool HasTemplatePoolEntryForStat(
        List<RandomStatPoolEntry> results,
        RandomItemStatType stat)
    {
        if (results == null)
            return false;

        for (int i = 0; i < results.Count; i++)
        {
            RandomStatPoolEntry entry = results[i];
            if (entry != null && entry.stat == stat)
                return true;
        }

        return false;
    }

    private static void AddTemplatePoolEntryIfMissing(
        List<RandomStatPoolEntry> results,
        RandomItemStatType stat,
        PoolValueBand band,
        RandomStatValueKind kind,
        float weight = 1f)
    {
        if (HasTemplatePoolEntryForStat(results, stat))
            return;

        AddTemplatePoolEntry(results, stat, band, kind, weight);
    }

    private static void AppendCombatWeaponDefaultPackage(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        WeaponWeight weight = WeaponWeightRules.GetEffectiveWeight(item);
        if (weight == WeaponWeight.NotApplicable)
            weight = WeaponWeight.Medium;

        CombatWeaponPoolTemplate template = GetCombatWeaponPoolTemplate(weight, GetTemplateRarityBand(item.rarity));
        WeaponStats weapon = item.weaponStats;

        if (weapon.minPhysicalDamage > 0 || weapon.maxPhysicalDamage > 0)
        {
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMinPhysicalDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMaxPhysicalDamage, template.Damage, RandomStatValueKind.FlatInteger);
        }

        if (weapon.minFireDamage > 0 || weapon.maxFireDamage > 0)
        {
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMinFireDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMaxFireDamage, template.Damage, RandomStatValueKind.FlatInteger);
        }

        if (weapon.minIceDamage > 0 || weapon.maxIceDamage > 0)
        {
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMinIceDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMaxIceDamage, template.Damage, RandomStatValueKind.FlatInteger);
        }

        if (weapon.minLightningDamage > 0 || weapon.maxLightningDamage > 0)
        {
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMinLightningDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMaxLightningDamage, template.Damage, RandomStatValueKind.FlatInteger);
        }

        if (weapon.minCorruptionDamage > 0 || weapon.maxCorruptionDamage > 0)
        {
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMinCorruptionDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntry(results, RandomItemStatType.WeaponMaxCorruptionDamage, template.Damage, RandomStatValueKind.FlatInteger);
        }

        AddTemplatePoolEntry(results, RandomItemStatType.AttackSpeedPercent, template.Speed, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.WeaponCritChance, template.CritChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.WeaponCritMultiplier, template.CritMulti, RandomStatValueKind.PercentPoints);
    }

    private static void AppendCombatWeaponLegacyFullTemplatePool(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        AppendCombatWeaponDefaultPackage(item, results);

        WeaponWeight weight = WeaponWeightRules.GetEffectiveWeight(item);
        if (weight == WeaponWeight.NotApplicable)
            weight = WeaponWeight.Medium;

        CombatWeaponPoolTemplate template = GetCombatWeaponPoolTemplate(weight, GetTemplateRarityBand(item.rarity));

        AddTemplatePoolEntry(results, RandomItemStatType.WeaponAttackRange, template.Range, RandomStatValueKind.FlatFloat);
        AddTemplatePoolEntry(results, RandomItemStatType.PoisonChance, template.AilmentChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.PoisonMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.BleedChance, template.AilmentChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.BleedMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.BurnChance, template.AilmentChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.BurnMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.ChillChance, template.AilmentChance, RandomStatValueKind.PercentPoints, weight: 0.75f);
        AddTemplatePoolEntry(results, RandomItemStatType.ChillMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints, weight: 0.75f);
        AddTemplatePoolEntry(results, RandomItemStatType.ShockChance, template.AilmentChance, RandomStatValueKind.PercentPoints, weight: 0.5f);
        AddTemplatePoolEntry(results, RandomItemStatType.ShockMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints, weight: 0.5f);
    }

    private static MagicWeaponPoolBands GetMagicWeaponPoolBands(bool isStaff, TemplateRarityBand band)
    {
        if (isStaff)
        {
            return band switch
            {
                TemplateRarityBand.Rare => new MagicWeaponPoolBands(
                    new PoolValueBand(8f, 16f), new PoolValueBand(6f, 14f), new PoolValueBand(4f, 7f), new PoolValueBand(8f, 16f),
                    new PoolValueBand(18f, 32f), new PoolValueBand(4f, 9f), new PoolValueBand(8f, 20f),
                    new PoolValueBand(8f, 18f), new PoolValueBand(4f, 8f), new PoolValueBand(4f, 8f)),
                TemplateRarityBand.Epic => new MagicWeaponPoolBands(
                    new PoolValueBand(14f, 24f), new PoolValueBand(9f, 18f), new PoolValueBand(5f, 8f), new PoolValueBand(10f, 18f),
                    new PoolValueBand(22f, 38f), new PoolValueBand(5f, 10f), new PoolValueBand(10f, 22f),
                    new PoolValueBand(10f, 20f), new PoolValueBand(5f, 10f), new PoolValueBand(5f, 10f)),
                TemplateRarityBand.Legendary => new MagicWeaponPoolBands(
                    new PoolValueBand(30f, 50f), new PoolValueBand(12f, 22f), new PoolValueBand(6f, 10f), new PoolValueBand(12f, 20f),
                    new PoolValueBand(28f, 45f), new PoolValueBand(6f, 12f), new PoolValueBand(12f, 24f),
                    new PoolValueBand(12f, 22f), new PoolValueBand(6f, 12f), new PoolValueBand(6f, 12f)),
                _ => new MagicWeaponPoolBands(
                    new PoolValueBand(5f, 14f), new PoolValueBand(4f, 12f), new PoolValueBand(3f, 6f), new PoolValueBand(6f, 14f),
                    new PoolValueBand(15f, 30f), new PoolValueBand(3f, 8f), new PoolValueBand(6f, 18f),
                    new PoolValueBand(6f, 18f), new PoolValueBand(3f, 8f), new PoolValueBand(3f, 8f)),
            };
        }

        return band switch
        {
            TemplateRarityBand.Rare => new MagicWeaponPoolBands(
                new PoolValueBand(6f, 14f), new PoolValueBand(5f, 12f), new PoolValueBand(3f, 6f), new PoolValueBand(7f, 14f),
                new PoolValueBand(14f, 28f), new PoolValueBand(3f, 7f), new PoolValueBand(6f, 16f),
                new PoolValueBand(6f, 14f), new PoolValueBand(3f, 7f), new PoolValueBand(3f, 7f)),
            TemplateRarityBand.Epic => new MagicWeaponPoolBands(
                new PoolValueBand(10f, 20f), new PoolValueBand(7f, 15f), new PoolValueBand(4f, 7f), new PoolValueBand(9f, 16f),
                new PoolValueBand(18f, 32f), new PoolValueBand(4f, 8f), new PoolValueBand(8f, 18f),
                new PoolValueBand(8f, 16f), new PoolValueBand(4f, 8f), new PoolValueBand(4f, 8f)),
            TemplateRarityBand.Legendary => new MagicWeaponPoolBands(
                new PoolValueBand(15f, 28f), new PoolValueBand(10f, 18f), new PoolValueBand(5f, 8f), new PoolValueBand(10f, 18f),
                new PoolValueBand(22f, 38f), new PoolValueBand(5f, 10f), new PoolValueBand(10f, 20f),
                new PoolValueBand(10f, 18f), new PoolValueBand(5f, 10f), new PoolValueBand(5f, 10f)),
            _ => new MagicWeaponPoolBands(
                new PoolValueBand(3f, 10f), new PoolValueBand(3f, 10f), new PoolValueBand(2f, 5f), new PoolValueBand(5f, 12f),
                new PoolValueBand(10f, 25f), new PoolValueBand(2f, 6f), new PoolValueBand(5f, 15f),
                new PoolValueBand(5f, 15f), new PoolValueBand(2f, 6f), new PoolValueBand(2f, 6f)),
        };
    }

    private static void AppendMagicWeaponDefaultPackage(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        bool isStaff = item.IsMagicStaff;
        MagicWeaponPoolBands bands = GetMagicWeaponPoolBands(isStaff, GetTemplateRarityBand(item.rarity));

        AddTemplatePoolEntry(results, RandomItemStatType.SpellDamagePercent, bands.SpellDamage, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.FireSkillDamagePercent, bands.ElementSkillDamage, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.IceSkillDamagePercent, bands.ElementSkillDamage, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.LightningSkillDamagePercent, bands.ElementSkillDamage, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.CritChanceBonus, bands.CritChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.CritMultiplierBonus, bands.CritMulti, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.BonusMana, bands.Mana, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.AllElementalAilmentChance, bands.ElementalAilmentChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.ShockChance, bands.ShockChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.BurnMultiplier, bands.BurnMultiplier, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.ChillMultiplier, bands.ChillMultiplier, RandomStatValueKind.PercentPoints, weight: 0.5f);
        AddTemplatePoolEntry(results, RandomItemStatType.ShockMultiplier, bands.ShockMultiplier, RandomStatValueKind.PercentPoints, weight: 0.1f);
    }

    private static WeaponDefensivePackageBands GetWeaponDefensivePackageBands(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new WeaponDefensivePackageBands(
                new PoolValueBand(3f, 6f), new PoolValueBand(15f, 30f), new PoolValueBand(3f, 6f)),
            TemplateRarityBand.Epic => new WeaponDefensivePackageBands(
                new PoolValueBand(4f, 8f), new PoolValueBand(20f, 35f), new PoolValueBand(4f, 7f)),
            TemplateRarityBand.Legendary => new WeaponDefensivePackageBands(
                new PoolValueBand(5f, 10f), new PoolValueBand(25f, 40f), new PoolValueBand(5f, 8f)),
            _ => new WeaponDefensivePackageBands(
                new PoolValueBand(2f, 5f), new PoolValueBand(10f, 25f), new PoolValueBand(2f, 5f)),
        };

    private static void AppendWeaponExtraPackages(
        ItemDefinition item,
        RandomStatPoolPackageFlags packages,
        List<RandomStatPoolEntry> results)
    {
        if (packages == RandomStatPoolPackageFlags.None || !item.IsWeapon || IsSpellScalingMagicWeapon(item))
            return;

        WeaponWeight weight = WeaponWeightRules.GetEffectiveWeight(item);
        if (weight == WeaponWeight.NotApplicable)
            weight = WeaponWeight.Medium;

        CombatWeaponPoolTemplate template = GetCombatWeaponPoolTemplate(weight, GetTemplateRarityBand(item.rarity));
        WeaponDefensivePackageBands defensive = GetWeaponDefensivePackageBands(GetTemplateRarityBand(item.rarity));

        if ((packages & RandomStatPoolPackageFlags.Bleed) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BleedChance, template.AilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BleedMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Poison) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.PoisonChance, template.AilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.PoisonMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Fire) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponMinFireDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponMaxFireDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BurnChance, template.AilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BurnMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Lightning) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponMinLightningDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponMaxLightningDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ShockMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Ice) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponMinIceDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponMaxIceDamage, template.Damage, RandomStatValueKind.FlatInteger);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ChillMultiplier, template.AilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Defensive) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BonusPhysBlockChance, defensive.BlockChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BonusPhysBlockMitigation, defensive.BlockMitigation, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ParryChance, defensive.ParryChance, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.AttackRange) != 0)
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.WeaponAttackRange, new PoolValueBand(1f, 2f), RandomStatValueKind.FlatFloat);
    }

    private static PoolValueBand GetJewelryHealthBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(8f, 12f),
            TemplateRarityBand.Epic => new PoolValueBand(10f, 15f),
            TemplateRarityBand.Legendary => new PoolValueBand(12f, 18f),
            _ => new PoolValueBand(5f, 10f),
        };

    private static PoolValueBand GetJewelrySmallPercentBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(2f, 5f),
            TemplateRarityBand.Epic => new PoolValueBand(3f, 6f),
            TemplateRarityBand.Legendary => new PoolValueBand(4f, 8f),
            _ => new PoolValueBand(1f, 3f),
        };

    private static PoolValueBand GetJewelryMediumPercentBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(3f, 6f),
            TemplateRarityBand.Epic => new PoolValueBand(4f, 8f),
            TemplateRarityBand.Legendary => new PoolValueBand(5f, 10f),
            _ => new PoolValueBand(2f, 5f),
        };

    private static PoolValueBand GetJewelryAilmentChanceBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(2f, 5f),
            TemplateRarityBand.Epic => new PoolValueBand(3f, 6f),
            TemplateRarityBand.Legendary => new PoolValueBand(4f, 7f),
            _ => new PoolValueBand(2f, 4f),
        };

    private static PoolValueBand GetJewelryAilmentMultiBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(4f, 8f),
            TemplateRarityBand.Epic => new PoolValueBand(5f, 10f),
            TemplateRarityBand.Legendary => new PoolValueBand(6f, 12f),
            _ => new PoolValueBand(3f, 6f),
        };

    private static PoolValueBand GetJewelryAbilityPowerBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(4f, 8f),
            TemplateRarityBand.Epic => new PoolValueBand(5f, 10f),
            TemplateRarityBand.Legendary => new PoolValueBand(6f, 12f),
            _ => new PoolValueBand(3f, 7f),
        };

    private static readonly PoolValueBand JewelrySharedResistBand = new(3f, 6f);

    private static void AppendJewelrySharedResistPackage(List<RandomStatPoolEntry> results)
    {
        AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BonusMagicResist, JewelrySharedResistBand, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BonusArmour, JewelrySharedResistBand, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BonusCorruptionResist, JewelrySharedResistBand, RandomStatValueKind.FlatInteger);
    }

    private static void AppendJewelryTrinketExtras(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        if (!item || item.equipSlot != EquipSlot.Trinket)
            return;

        if (item.miscEffects.enemyRespawnTimeReductionSeconds > 0f)
        {
            AddTemplatePoolEntryIfMissing(
                results,
                RandomItemStatType.EnemyRespawnTimeReductionSeconds,
                new PoolValueBand(0.5f, 1f),
                RandomStatValueKind.FlatFloat);
        }
    }

    private static void AppendJewelryDefaultPackage(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        if (!item || !item.IsJewelry)
            return;

        TemplateRarityBand rarityBand = GetTemplateRarityBand(item.rarity);
        AddTemplatePoolEntry(results, RandomItemStatType.BonusHealth, GetJewelryHealthBand(rarityBand), RandomStatValueKind.FlatInteger);

        if (item.JewelryGemType != JewelryGemType.None)
            AppendJewelryGemTypePool(item.JewelryGemType, rarityBand, results);
        else
            AppendJewelryExtraPackages(item, item.ExtraRandomStatPoolPackages, results);

        AppendJewelrySharedResistPackage(results);
        AppendJewelryTrinketExtras(item, results);
    }

    private static void AppendJewelryGemTypePool(
        JewelryGemType gemType,
        TemplateRarityBand rarityBand,
        List<RandomStatPoolEntry> results)
    {
        PoolValueBand small = GetJewelrySmallPercentBand(rarityBand);
        PoolValueBand medium = GetJewelryMediumPercentBand(rarityBand);
        PoolValueBand ailmentChance = GetJewelryAilmentChanceBand(rarityBand);
        PoolValueBand ailmentMulti = GetJewelryAilmentMultiBand(rarityBand);

        switch (gemType)
        {
            case JewelryGemType.Ruby:
                AddTemplatePoolEntry(results, RandomItemStatType.MeleePhysicalDamagePercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.BleedChance, ailmentChance, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.BleedMultiplier, ailmentMulti, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Topaz:
                AddTemplatePoolEntry(results, RandomItemStatType.MeleePhysicalDamagePercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.MagicDamagePercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.FireSkillDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.BurnChance, ailmentChance, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.BurnMultiplier, ailmentMulti, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Emerald:
                AddTemplatePoolEntry(results, RandomItemStatType.RangedPhysicalDamagePercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.AttackSpeedPercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.AbilityPowerPercent, GetJewelryAbilityPowerBand(rarityBand), RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.LightningSkillDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.ShockChance, ailmentChance, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.ShockMultiplier, ailmentMulti, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Sapphire:
                AddTemplatePoolEntry(results, RandomItemStatType.SpellDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.MagicDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.IceSkillDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.ChillMultiplier, ailmentMulti, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.ChillChance, ailmentChance, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Citrine:
                AddTemplatePoolEntry(results, RandomItemStatType.MinionDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.MinionAttackSpeedPercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.MinionCritChance, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.MinionMaxLifePercent, medium, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Quartz:
                AddTemplatePoolEntry(results, RandomItemStatType.LifeSteal, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.GlobalPhysicalDamagePercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.MagicDamagePercent, small, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.RangedPhysicalDamagePercent, small, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Diamond:
                AddTemplatePoolEntry(results, RandomItemStatType.CritChanceBonus, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.CritMultiplierBonus, ailmentMulti, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.AttackSpeedPercent, medium, RandomStatValueKind.PercentPoints);
                break;

            case JewelryGemType.Amethyst:
                AddTemplatePoolEntry(results, RandomItemStatType.CorruptionDamagePercent, medium, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.PoisonChance, ailmentChance, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.PoisonMultiplier, ailmentMulti, RandomStatValueKind.PercentPoints);
                AddTemplatePoolEntry(results, RandomItemStatType.AttackSpeedPercent, medium, RandomStatValueKind.PercentPoints);
                break;
        }
    }

    private static PoolValueBand ScaleBand(PoolValueBand source, float scale) =>
        new(Mathf.Max(0f, source.Min * scale), Mathf.Max(0f, source.Max * scale));

    private static PoolValueBand GetJewelryCritChanceBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(2f, 4f),
            TemplateRarityBand.Epic => new PoolValueBand(3f, 5f),
            TemplateRarityBand.Legendary => new PoolValueBand(4f, 7f),
            _ => new PoolValueBand(1f, 3f),
        };

    private static PoolValueBand GetJewelryCritMultiBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(4f, 8f),
            TemplateRarityBand.Epic => new PoolValueBand(5f, 10f),
            TemplateRarityBand.Legendary => new PoolValueBand(7f, 14f),
            _ => new PoolValueBand(3f, 6f),
        };

    private static PoolValueBand GetJewelryMinionDamageBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(3f, 7f),
            TemplateRarityBand.Epic => new PoolValueBand(4f, 9f),
            TemplateRarityBand.Legendary => new PoolValueBand(5f, 12f),
            _ => new PoolValueBand(2f, 5f),
        };

    private static PoolValueBand GetJewelryMinionSpeedBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(2f, 5f),
            TemplateRarityBand.Epic => new PoolValueBand(3f, 6f),
            TemplateRarityBand.Legendary => new PoolValueBand(4f, 8f),
            _ => new PoolValueBand(1f, 4f),
        };

    private static PoolValueBand GetJewelryMinionHealthBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(3f, 7f),
            TemplateRarityBand.Epic => new PoolValueBand(4f, 9f),
            TemplateRarityBand.Legendary => new PoolValueBand(5f, 12f),
            _ => new PoolValueBand(2f, 5f),
        };

    private static void AppendJewelryExtraPackages(
        ItemDefinition item,
        RandomStatPoolPackageFlags packages,
        List<RandomStatPoolEntry> results)
    {
        if (!item || !item.IsJewelry || packages == RandomStatPoolPackageFlags.None)
            return;

        TemplateRarityBand rarityBand = GetTemplateRarityBand(item.rarity);
        CombatWeaponPoolTemplate baseWeaponTemplate = GetCombatWeaponPoolTemplate(WeaponWeight.Medium, rarityBand);
        PoolValueBand reducedAilmentChance = ScaleBand(baseWeaponTemplate.AilmentChance, 0.7f);
        PoolValueBand reducedAilmentMulti = ScaleBand(baseWeaponTemplate.AilmentMulti, 0.7f);

        if ((packages & RandomStatPoolPackageFlags.Minion) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.MinionDamagePercent, GetJewelryMinionDamageBand(rarityBand), RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.MinionAttackSpeedPercent, GetJewelryMinionSpeedBand(rarityBand), RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.MinionMaxLifePercent, GetJewelryMinionHealthBand(rarityBand), RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Crit) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.CritChanceBonus, GetJewelryCritChanceBand(rarityBand), RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.CritMultiplierBonus, GetJewelryCritMultiBand(rarityBand), RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Bleed) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BleedChance, reducedAilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BleedMultiplier, reducedAilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Poison) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.PoisonChance, reducedAilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.PoisonMultiplier, reducedAilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Fire) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BurnChance, reducedAilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.BurnMultiplier, reducedAilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Lightning) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ShockChance, reducedAilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ShockMultiplier, reducedAilmentMulti, RandomStatValueKind.PercentPoints);
        }

        if ((packages & RandomStatPoolPackageFlags.Ice) != 0)
        {
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ChillChance, reducedAilmentChance, RandomStatValueKind.PercentPoints);
            AddTemplatePoolEntryIfMissing(results, RandomItemStatType.ChillMultiplier, reducedAilmentMulti, RandomStatValueKind.PercentPoints);
        }
    }

    /// <summary>Default package + optional weapon packages for items using the package workflow.</summary>
    public static List<RandomStatPoolEntry> BuildConfiguredDefaultPoolEntries(ItemDefinition item)
    {
        var results = new List<RandomStatPoolEntry>();
        if (!item)
            return results;

        if (item.IsWeapon)
        {
            if (IsSpellScalingMagicWeapon(item))
                AppendMagicWeaponDefaultPackage(item, results);
            else
            {
                AppendCombatWeaponDefaultPackage(item, results);
                AppendWeaponExtraPackages(item, item.ExtraRandomStatPoolPackages, results);
            }
        }
        else if (item.IsArmour)
        {
            AppendArmourTemplatePool(item, results);
        }
        else if (item.IsJewelry)
        {
            AppendJewelryDefaultPackage(item, results);
        }
        else
        {
            AppendLegacyDerivedTemplatePoolEntries(item, results);
        }

        results.Sort((a, b) => ComparePoolEntriesForDisplay(a, b, item));
        return results;
    }

    public static bool SupportsDefaultRandomStatPoolPackage(ItemDefinition item) =>
        item && (item.IsWeapon || item.IsArmour || item.IsJewelry);

    private static void AppendCombatWeaponTemplatePool(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        AppendCombatWeaponLegacyFullTemplatePool(item, results);
    }

    private static void AppendArmourTemplatePool(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
        ArmourTypePoolBands bands = GetArmourTypePoolBands(item, GetTemplateRarityBand(item.rarity));
        bool isBoots = item != null && item.equipSlot == EquipSlot.Boots;
        PoolValueBand moveSpeedBand = bands.MoveSpeedPercent;
        PoolValueBand rangedBand = bands.RangedDamagePercent;

        if (isBoots)
        {
            moveSpeedBand = GetBootMoveSpeedBand(GetTemplateRarityBand(item.rarity));
            rangedBand = new PoolValueBand(0f, 0f);
        }

        AddTemplatePoolEntry(results, RandomItemStatType.ArmourBonusHealth, bands.Health, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourFlatGuard, bands.FlatGuard, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourFlatArmour, bands.Armour, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourCorruptionResist, bands.CorruptionResist, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourMagicResist, bands.MagicResist, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourEnergyEfficiency, bands.EnergyEfficiency, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.MeleePhysicalDamagePercent, bands.MeleeDamagePercent, RandomStatValueKind.PercentPoints);

        AddTemplatePoolEntry(results, RandomItemStatType.BonusMana, bands.Mana, RandomStatValueKind.FlatInteger);
        AddTemplatePoolEntry(results, RandomItemStatType.ManaRegen, bands.ManaRegen, RandomStatValueKind.FlatFloat);
        AddTemplatePoolEntry(results, RandomItemStatType.MagicDamagePercent, bands.MagicDamagePercent, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.MoveSpeedPercent, moveSpeedBand, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.RangedPhysicalDamagePercent, rangedBand, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourPhysBlockChance, bands.PhysBlockChance, RandomStatValueKind.PercentPoints);
        AddTemplatePoolEntry(results, RandomItemStatType.ArmourPhysBlockMitigation, bands.PhysBlockMitigation, RandomStatValueKind.PercentPoints);
    }

    private static PoolValueBand GetBootMoveSpeedBand(TemplateRarityBand band) =>
        band switch
        {
            TemplateRarityBand.Rare => new PoolValueBand(5f, 15f),
            TemplateRarityBand.Epic => new PoolValueBand(5f, 20f),
            TemplateRarityBand.Legendary => new PoolValueBand(5f, 25f),
            _ => new PoolValueBand(5f, 10f),
        };

    private static void AppendLegacyDerivedTemplatePoolEntries(ItemDefinition item, List<RandomStatPoolEntry> results)
    {
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
        TryAdd(RandomItemStatType.BonusArmour, bonus.armour);
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
        TryAdd(RandomItemStatType.MeleePhysicalDamagePercent, bonus.meleePhysicalDamagePercent);
        TryAdd(RandomItemStatType.GlobalPhysicalDamagePercent, bonus.globalPhysicalDamagePercent);
        TryAdd(RandomItemStatType.RangedPhysicalDamagePercent, bonus.rangedPhysicalDamagePercent);
        TryAdd(RandomItemStatType.MagicDamageFlat, bonus.magicDamage);
        TryAdd(RandomItemStatType.MagicDamagePercent, bonus.magicDamagePercent);
        TryAdd(RandomItemStatType.FireSkillDamagePercent, bonus.fireSkillDamagePercent);
        TryAdd(RandomItemStatType.IceSkillDamagePercent, bonus.iceSkillDamagePercent);
        TryAdd(RandomItemStatType.LightningSkillDamagePercent, bonus.lightningSkillDamagePercent);
        TryAdd(RandomItemStatType.SpellDamagePercent, bonus.spellDamagePercent);
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
        TryAdd(RandomItemStatType.AllElementalAilmentChance, bonus.allElementalAilmentChance);
        TryAdd(RandomItemStatType.ParryChance, bonus.parryChance);
        TryAdd(RandomItemStatType.StunChance, bonus.stunChance);

        if (item.miscEffects.enemyRespawnTimeReductionSeconds > 0f)
            TryAdd(RandomItemStatType.EnemyRespawnTimeReductionSeconds, item.miscEffects.enemyRespawnTimeReductionSeconds);
    }

    /// <summary>
    /// Builds pool entries for editor template generation (weight + rarity tables for weapons/armour).
    /// </summary>
    public static List<RandomStatPoolEntry> BuildTemplatePoolEntries(ItemDefinition item)
    {
        var results = new List<RandomStatPoolEntry>();
        if (!item)
            return results;

        if (item.IsWeapon)
        {
            if (IsSpellScalingMagicWeapon(item))
                AppendMagicWeaponDefaultPackage(item, results);
            else
                AppendCombatWeaponTemplatePool(item, results);
        }
        else if (item.IsArmour)
        {
            AppendArmourTemplatePool(item, results);
        }
        else
        {
            AppendLegacyDerivedTemplatePoolEntries(item, results);
        }

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

                if (stat == RandomItemStatType.ArmourFlatGuard)
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
        stat == RandomItemStatType.BonusArmour ||
        stat == RandomItemStatType.BonusMagicResist ||
        stat == RandomItemStatType.BonusCorruptionResist ||
        stat == RandomItemStatType.ArmourFlatArmour ||
        stat == RandomItemStatType.ArmourMagicResist ||
        stat == RandomItemStatType.ArmourCorruptionResist;

    private static bool UsesHealthFlatTemplateRange(RandomItemStatType stat) =>
        stat == RandomItemStatType.BonusHealth ||
        stat == RandomItemStatType.ArmourBonusHealth;

    private static bool IsEffectivelyZero(float value) => Mathf.Abs(value) < 0.0001f;

    public static RandomStatValueKind GetDefaultValueKind(RandomItemStatType stat)
    {
        switch (stat)
        {
            case RandomItemStatType.BonusHealth:
            case RandomItemStatType.BonusEnergy:
            case RandomItemStatType.BonusMana:
            case RandomItemStatType.BonusArmour:
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
            case RandomItemStatType.ArmourFlatArmour:
            case RandomItemStatType.ArmourMagicResist:
            case RandomItemStatType.ArmourCorruptionResist:
            case RandomItemStatType.ArmourBonusHealth:
            case RandomItemStatType.ArmourBonusEnergy:
            case RandomItemStatType.ArmourFlatGuard:
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
        if (entries == null || entries.Count == 0)
            return -1;

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

    private static int PickWeightedIndex(
        IReadOnlyList<RandomStatPoolEntry> entries,
        IReadOnlyList<int> allowedIndices)
    {
        if (entries == null || allowedIndices == null || allowedIndices.Count == 0)
            return -1;

        if (allowedIndices.Count == 1)
            return allowedIndices[0];

        float total = 0f;
        for (int i = 0; i < allowedIndices.Count; i++)
        {
            int entryIndex = allowedIndices[i];
            if (entryIndex >= 0 && entryIndex < entries.Count)
                total += Mathf.Max(0f, entries[entryIndex].weight);
        }

        if (total <= 0f)
            return allowedIndices[Random.Range(0, allowedIndices.Count)];

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < allowedIndices.Count; i++)
        {
            int entryIndex = allowedIndices[i];
            if (entryIndex < 0 || entryIndex >= entries.Count)
                continue;

            cumulative += Mathf.Max(0f, entries[entryIndex].weight);
            if (roll <= cumulative)
                return entryIndex;
        }

        return allowedIndices[allowedIndices.Count - 1];
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
            case RandomItemStatType.BonusArmour:
                AddBonusInt(ref item.bonusStats.armour, primary, kind);
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
            case RandomItemStatType.BonusPhysBlockMitigation:
                item.bonusStats.physBlockMitigation = Mathf.Clamp01(item.bonusStats.physBlockMitigation + primary);
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
            case RandomItemStatType.MeleePhysicalDamagePercent:
                item.bonusStats.meleePhysicalDamagePercent += primary;
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
            case RandomItemStatType.SpellDamagePercent:
                item.bonusStats.spellDamagePercent += primary;
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
                    ApplyWeaponAttackSpeedPercentRoll(item, primary);
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
            case RandomItemStatType.AllElementalAilmentChance:
                item.bonusStats.allElementalAilmentChance = Mathf.Clamp01(
                    item.bonusStats.allElementalAilmentChance + primary);
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

            case RandomItemStatType.ArmourFlatArmour:
                AddArmourInt(ref item.armourStats.armour, primary, kind);
                break;
            case RandomItemStatType.ArmourMagicResist:
                AddArmourInt(ref item.armourStats.magicResist, primary, kind);
                break;
            case RandomItemStatType.ArmourCorruptionResist:
                AddArmourInt(ref item.armourStats.corruptionResist, primary, kind);
                break;
            case RandomItemStatType.ArmourPhysBlockChance:
                item.armourStats.physBlockChance = Mathf.Clamp01(item.armourStats.physBlockChance + primary);
                break;
            case RandomItemStatType.ArmourPhysBlockMitigation:
                item.armourStats.physBlockMitigation = Mathf.Clamp01(item.armourStats.physBlockMitigation + primary);
                break;
            case RandomItemStatType.ArmourBonusHealth:
                AddArmourInt(ref item.armourStats.bonusHealth, primary, kind);
                break;
            case RandomItemStatType.ArmourBonusEnergy:
                AddArmourInt(ref item.armourStats.bonusEnergy, primary, kind);
                break;
            case RandomItemStatType.ArmourEnergyEfficiency:
                item.armourStats.energyEfficiency = Mathf.Clamp01(item.armourStats.energyEfficiency + primary);
                break;
            case RandomItemStatType.ArmourFlatGuard:
                AddArmourInt(ref item.armourStats.flatGuard, primary, kind);
                break;
            case RandomItemStatType.ArmourMaxGuardPercent:
                item.armourStats.maxGuardPercent += primary;
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

    private static void ApplyWeaponAttackSpeedPercentRoll(ItemDefinition item, float primaryFraction)
    {
        if (!item || !item.IsWeapon)
            return;

        item.weaponStats.attacksPerSecond = Mathf.Max(
            0.01f,
            item.weaponStats.attacksPerSecond * Mathf.Max(0.1f, 1f + primaryFraction));
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

    private static void AddArmourInt(ref int field, float value, RandomStatValueKind kind)
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
            case RandomItemStatType.AllElementalAilmentChance: return 522;

            case RandomItemStatType.BonusMana: return 600;
            case RandomItemStatType.ManaRegen: return 601;
            case RandomItemStatType.BonusHealth: return 50;
            case RandomItemStatType.ArmourBonusHealth: return 610;
            case RandomItemStatType.ArmourFlatGuard: return 611;
            case RandomItemStatType.ArmourFlatArmour: return 612;
            case RandomItemStatType.ArmourCorruptionResist: return 613;
            case RandomItemStatType.ArmourMagicResist: return 614;
            case RandomItemStatType.ArmourEnergyEfficiency: return 615;
            case RandomItemStatType.MeleePhysicalDamagePercent: return 616;
            case RandomItemStatType.MoveSpeedPercent: return 617;
            case RandomItemStatType.RangedPhysicalDamagePercent: return 618;
            case RandomItemStatType.MagicDamagePercent: return 619;

            case RandomItemStatType.ParryChance: return 700;
            case RandomItemStatType.StunChance: return 701;

            case RandomItemStatType.WeaponAttackRange: return 900;
            case RandomItemStatType.AttackRangeBonus: return 901;

            default: return 750;
        }
    }

    /// <summary>Player-facing label for a random pool stat (database / encyclopedia tooltips).</summary>
    public static string GetRandomStatDisplayName(RandomItemStatType stat, ItemDefinition item = null) =>
        ItemStatDisplayNames.ForRandomPoolStat(stat, item);

    /// <summary>One line for the database item tooltip random-stat pool section.</summary>
    public static string FormatPoolEntryDatabaseLine(RandomStatPoolEntry entry, ItemDefinition item = null)
    {
        if (entry == null || !entry.IsValid)
            return "";

        string name = GetRandomStatDisplayName(entry.stat, item);
        string range = FormatPoolEntryValueRange(entry);
        return string.IsNullOrWhiteSpace(range) ? name : $"{name} ({range})";
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
}
