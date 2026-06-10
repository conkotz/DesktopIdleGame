using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum EquipSlot
{
    None,

    // Weapons
    MainHand,
    OffHand,

    // Armor
    [InspectorName("Head")]
    Helmet,
    Body,
    [InspectorName("Feet")]
    Boots,

    // Accessories
    Trinket,
    [InspectorName("Neck")]
    Pendant,
    Ring,
}

public enum ItemKind
{
    Resource,
    Weapon,
    Tool,
    Armor,
    Jewelry,
    Consumable,
    Quest,
    CombatSupport,
    EnhancementScroll
}

public enum CombatSupportType
{
    None,
    Arrows,
    Bolts,
    Runes,
    Focus
}

public enum Handedness
{
    OneHanded,
    TwoHanded
}

public enum ToolType
{
    None,
    Axe,
    Pickaxe,
    FishingRod
}

public enum AttackSkill
{
    Melee,
    Ranged,
    Magic
}

/// <summary>Ranged weapon style for UI and idle auto-targeting (only when <see cref="AttackSkill.Ranged"/>).</summary>
public enum RangedBowType
{
    Swiftbow,
    Longbow
}

public enum MagicAttackType
{
    Lightning,
    Fire,
    Ice
}

/// <summary>
/// Main-hand weapon archetype used for offhand support compatibility (arrows/bolts/runes/focus).
/// </summary>
public enum MainHandWeaponArchetype
{
    None,
    Bow,
    Crossbow,
    Staff,
    Wand,
    Other
}

/// <summary>
/// Weapon heft used to scale enhancement flat damage and ailment multiplier scroll values.
/// Light = fast weapons (daggers, swiftbows, wands); Heavy = slow weapons (polearms, longbows).
/// </summary>
public enum WeaponWeight
{
    NotApplicable,
    Light,
    Medium,
    Heavy
}

[System.Serializable]
public struct WeaponStats
{
    [Header("Physical Damage")]
    public int minPhysicalDamage;
    public int maxPhysicalDamage;

    [Header("Elemental Damage")]
    [Tooltip("Flat fire damage on weapon hits. Total magic = fire + ice + lightning; Magic Damage % on gear scales that total.")]
    public int minFireDamage;
    public int maxFireDamage;
    [Tooltip("Flat ice damage on weapon hits.")]
    public int minIceDamage;
    public int maxIceDamage;
    [Tooltip("Flat lightning damage on weapon hits.")]
    public int minLightningDamage;
    public int maxLightningDamage;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("minMagicDamage")]
    internal int legacyMinMagicDamage;
    [SerializeField, HideInInspector]
    [FormerlySerializedAs("maxMagicDamage")]
    internal int legacyMaxMagicDamage;

    public int TotalElementalDamageMin => minFireDamage + minIceDamage + minLightningDamage;
    public int TotalElementalDamageMax => maxFireDamage + maxIceDamage + maxLightningDamage;

    [Header("Corruption Damage")]
    [FormerlySerializedAs("minTrueDamage")] public int minCorruptionDamage;
    [FormerlySerializedAs("maxTrueDamage")] public int maxCorruptionDamage;

    [Header("Speed")]
    [Tooltip("Attacks per second (1 = one attack per second)")]
    public float attacksPerSecond;

    [Header("Critical")]
    [Range(0f, 1f)]
    public float critChance;

    [Tooltip("1.5 = 150% damage")]
    public float critMultiplier;

    [Header("Handling")]
    public Handedness handedness;
    public float attackRange;

    [Header("Skill Type")]
    public AttackSkill attackSkill;

    [Header("Weapon Archetype")]
    [Tooltip("Used to validate compatible offhand support types (Arrows/Bolts/Runes/Focus).")]
    public MainHandWeaponArchetype mainHandArchetype;

    [Tooltip("Only when Attack Skill is Ranged. Swiftbow: default. Longbow: auto-battle targets the furthest enemy first.")]
    public RangedBowType rangedBowType;

    [Header("Magic Type")]
    [Tooltip("Only used when Attack Skill is Magic.")]
    public MagicAttackType magicAttackType;

    [Header("Resource Cost")]
    [Tooltip("Mana spent per attack when Attack Skill is Magic.")]
    [Min(0f)] public float manaCostPerAttack;

    [Header("Magic Ailments")]
    [Range(0f, 1f)]
    [Tooltip("Chance to apply elemental ailment from magic hits. Uses Magic Type: Ice=Chill, Fire=Burn, Lightning=Shock.")]
    public float magicAilmentApplyChance;

    [HideInInspector]
    [SerializeField]
    [FormerlySerializedAs("burnChance")]
    private float unusedLegacyWeaponBurnChance;

    [Header("Dual Wield")]
    [Tooltip("If true, this weapon may be equipped in the OffHand slot as well.")]
    public bool canEquipInOffHand;

    [Header("Support Requirement")]
    public bool requiresOffhandSupport;
    public CombatSupportType requiredSupportType;

    [Header("Equipment Tier")]
    [Tooltip("Shown as Tier 1–5; gate uses Attack Skill (Melee/Ranged/Magic) at L1 / L10 / L20 / L30 / L50.")]
    public EquipmentTierRank equipmentTier;

    [Header("Weapon Weight")]
    [Tooltip("Enhancement flat damage and ailment multiplier scrolls scale by weight. NotApplicable for non-weapons.")]
    public WeaponWeight weaponWeight;

}

/// <summary>
/// Fractional percent bonuses on items match <see cref="AbilityDefinition"/> scaling fields: 0 = no bonus, 0.25 = +25%, 1 = +100%.
/// Values stack additively across gear; combat uses 1 + sum where applicable (see <see cref="CharacterStats"/>).
/// </summary>
[System.Serializable]
public struct CombatSupportStats
{
    [Header("Type")]
    public CombatSupportType supportType;

    [Header("Main-hand Compatibility")]
    [Tooltip("Optional explicit main-hand archetype requirement. If None, defaults by support type (Arrows=Bow, Bolts=Crossbow, Runes=Staff, Focus=Wand).")]
    public MainHandWeaponArchetype requiredMainHandArchetype;

    [Header("Bonuses")]
    public float bonusPhysicalDamage;
    public float bonusMagicDamage;
    [FormerlySerializedAs("bonusTrueDamage")] public float bonusCorruptionDamage;

    [Range(0f, 1f)] public float critChanceBonus;
    public float critMultiplierBonus;
    [Tooltip("Attack speed bonus on support (0.1 = +10% APS).")]
    public float attackSpeedPercent;

    [Header("Damage % (multipliers)")]
    [Tooltip("Extra all-physical damage. Uses item scaling: 0.1 = +10% (stacks additively across gear).")]
    public float physicalDamagePercent;
    [Tooltip("More all-physical damage, same stacking as Physical %. Use either or both; they add together.")]
    public float globalPhysicalDamagePercent;
    [Tooltip("Extra physical damage with ranged weapons only (0.1 = +10%).")]
    public float rangedPhysicalDamagePercent;
    [Tooltip("Extra magic damage on elemental weapon totals (0.1 = +10%).")]
    public float magicDamagePercent;
    [Tooltip("Extra fire damage on fire-tagged hits and skills (0.1 = +10%).")]
    public float fireDamagePercent;
    [Tooltip("Extra ice damage on ice-tagged hits and skills (0.1 = +10%).")]
    public float iceDamagePercent;
    [Tooltip("Extra cold damage; stacks with ice % (0.1 = +10%).")]
    public float coldDamagePercent;
    [Tooltip("Extra corruption on attack split (0.1 = +10%).")]
    public float corruptionDamagePercent;

    [Header("Optional Charges/Consumption")]
    public bool consumableOnAttack;
    public int consumeAmountPerAttack;
}



[System.Serializable]
public struct ToolStats
{
    [Header("Tool Type")]
    public ToolType toolType;

    [Header("Equipment Tier")]
    [Tooltip("Shown as Tier 1–5; gate is Woodcutting / Mining / Fishing by tool type at L1 / L10 / L20 / L30 / L50. Default Tier1 (enum 0).")]
    public EquipmentTierRank equipmentTier;

    [Header("Gathering Speed")]
    public float gatherSpeedMultiplier;

    [Header("Gathering Grit")]
    [Range(0f, 1f)]
    [Tooltip("Chance to double BASE resource quantity only. Does not affect bonus drop rolls.")]
    public float gatheringGrit;

    [Header("Bonus Resource Find Chance")]
    [Tooltip("Multiplier for bonus drop chance. 1.0 = +100% bonus chance.")]
    public float bonusResourceFindChance;

    [Header("Stamina Efficiency")]
    [Range(0f, 1f)]
    [Tooltip("Reduces stamina cost: cost * (1 - staminaEfficiency). 0.1 = 10% less cost.")]
    public float staminaEfficiency;
}

[System.Serializable]
public struct ArmorStats
{
    [Header("Equipment Tier")]
    [Tooltip("Shown as Tier 1–5; gate uses Endurance at L1 / L10 / L20 / L30 / L50.")]
    public EquipmentTierRank equipmentTier;

    [Header("Defence")]
    public int armor;
    public int magicResist;
    public int corruptionResist;

    [Header("Block")]
    [Range(0f, 1f)]
    public float physBlockChance;

    [Header("Vitals")]
    public int bonusHealth;
    public int bonusEnergy;

    [Range(0f, 1f)]
    [Tooltip("Reduces energy cost of Melee, Ranged, and Magic abilities that spend energy (0.1 = 10%).")]
    [FormerlySerializedAs("staminaEfficiency")]
    public float energyEfficiency;

    [Header("Guard")]
    [Tooltip("Flat bonus to natural guard cap (same stacking as bonus health).")]
    public int flatGuard;

    [Tooltip("Extra maximum guard as a fraction of Max HP (0.1 = +10% cap, i.e. 110% of Max HP before flat bonuses).")]
    public float maxGuardPercent;
}

/// <summary>
/// Equippable bonus stats. Percent-style fractions match <see cref="AbilityDefinition"/> scaling: 0 = none, 0.25 = +25%, 1 = +100%.
/// </summary>
[System.Serializable]
public struct BonusStats
{
    [Header("Vitals")]
    public int bonusHealth;
    public int bonusEnergy;
    public int bonusMana;

    [Header("Defence")]
    public int armor;
    public int magicResist;
    public int corruptionResist;
    [Range(0f, 1f)] public float physBlockChance;

    [Header("Sustain")]
    [Tooltip("HP per second")]
    public float lifeRegen;

    [Tooltip("Energy per second")]
    public float energyRegen;

    [Tooltip("Mana per second")]
    public float manaRegen;

    [Range(0f, 1f)]
    [Tooltip("Reduces energy cost of Melee, Ranged, and Magic abilities that spend energy (0.1 = 10%).")]
    [FormerlySerializedAs("staminaEfficiency")]
    public float energyEfficiency;

    [Range(0f, 1f)]
    [Tooltip("0.05 = 5%")]
    public float lifeSteal;

    [Header("Mobility")]
    [Tooltip("Move speed bonus (0.1 = +10%).")]
    public float moveSpeedPercent;

    [Header("Offense")]
    [Tooltip("Flat physical damage added to attacks.")]
    public float physicalDamage;

    [Tooltip("Extra all-physical damage (0.1 = +10%). Stacks with Global Physical % on this item.")]
    public float physicalDamagePercent;

    [Tooltip("More all-physical damage (0.1 = +10%). Same combat bucket as Physical %.")]
    public float globalPhysicalDamagePercent;

    [Tooltip("Extra physical damage with ranged weapons (0.1 = +10%).")]
    public float rangedPhysicalDamagePercent;

    [Tooltip("Flat magic damage added to elemental weapon totals.")]
    public float magicDamage;

    [Tooltip("Extra magic damage on elemental weapon totals (0.1 = +10%).")]
    public float magicDamagePercent;

    [Tooltip("Extra fire damage on fire skills and instant magic (0.1 = +10%).")]
    public float fireSkillDamagePercent;

    [Tooltip("Extra ice damage on ice skills and instant magic (0.1 = +10%).")]
    public float iceSkillDamagePercent;

    [Tooltip("Extra lightning damage on lightning skills and instant magic (0.1 = +10%).")]
    public float lightningSkillDamagePercent;

    [Tooltip("Extra corruption on attack split (0.1 = +10%).")]
    public float corruptionDamagePercent;

    [Tooltip("Flat corruption damage on attacks.")]
    [FormerlySerializedAs("trueDamage")] public float corruptionDamage;

    [Tooltip("Percent bonus to ability damage (+25 = +25% ability damage).")]
    public float abilityPower;

    [Tooltip("Attack speed bonus (0.1 = +10% APS).")]
    public float attackSpeedPercent;

    [Tooltip("Ability cooldown reduction (0.15 = 15% CDR on ability cooldowns).")]
    public float abilityCooldownReductionFraction;

    [Header("Minion")]
    [Tooltip("Extra damage for your minions / summons (0.1 = +10%). Generic; works with inherited or internal minion base damage.")]
    public float minionDamagePercent;

    [Tooltip("Extra minion attack speed (0.1 = +10% APS). Aggregate clamped so 1 + total ≥ 0.1.")]
    public float minionAttackSpeedPercent;

    [Tooltip("Added minion crit chance, 0–1 scale (0.1 = +10 percentage points). Minion crit damage is fixed ×1.5 (not from items).")]
    public float minionCritChance;

    [Tooltip("Bonus minion max life / health (0.1 = +10%). Halved for inherited weapon-hit minions.")]
    public float minionMaxLifePercent;

    [Tooltip("Added crit chance, 0–1 scale (0.1 = +10 percentage points).")]
    public float critChanceBonus;

    [Tooltip("Added to crit multiplier (0.1 = +0.1 mult).")]
    public float critMultiplierBonus;

    [Tooltip("Added attack range.")]
    public float attackRangeBonus;

    [Header("Ailments")]
    [Range(0f, 1f)]
    [Tooltip("0.10 = 10% chance to apply bleed on hit")]
    public float bleedChance;

    [Tooltip("Bleed damage bonus fraction: 0 = baseline; 0.25 = +25%; 1 = +100% (same convention as ability scaling).")]
    public float bleedMultiplier;

    [Range(0f, 1f)]
    [Tooltip("0.10 = 10% chance to apply poison on hit")]
    public float poisonChance;

    [Tooltip("Poison damage bonus fraction: 0 = baseline; 0.25 = +25%; 1 = +100% (same convention as ability scaling).")]
    public float poisonMultiplier;

    [Tooltip("Bonus poison duration in seconds")]
    public float poisonDurationBonus;

    [Tooltip("Bonus maximum poison stacks")]
    public int poisonMaxStacksBonus;

    [Tooltip("Added to burn tick multiplier. 0.25 = +0.25 to multiplier (same fractional style as ability coefficients).")]
    public float burnExplosionMultiplierBonus;

    [Range(0f, 1f)]
    [Tooltip("Bonus chance to apply a burn stack on fire hits (additive, player).")]
    public float burnChance;

    [Range(0f, 1f)]
    [Tooltip("Bonus chance to apply chill on hit (additive, player).")]
    public float chillChance;

    [Range(0f, 1f)]
    [Tooltip("Bonus chance to apply shock on hit (additive, player).")]
    public float shockChance;

    [Tooltip("Adds to chill slow per stack. 0.02 means +2 percentage points (e.g. 15% -> 17%).")]
    public float chillSlowPerStackBonus;

    [Tooltip("Adds to shock damage taken multiplier. 0.05 means +5 percentage points (e.g. 15% -> 20%).")]
    public float shockDamageTakenMultiplierBonus;

    [Header("Combat Procs")]
    [Range(0f, 1f)]
    [Tooltip("0.10 = 10% parry chance (requires Parry major passive to activate).")]
    public float parryChance;

    [Range(0f, 1f)]
    [Tooltip("0.10 = 10% chance to stun on weapon hit.")]
    public float stunChance;

    public bool HasAny()
    {
        return bonusHealth != 0 || bonusEnergy != 0 ||
               bonusMana != 0 ||
               armor != 0 || magicResist != 0 || corruptionResist != 0 || physBlockChance > 0f ||
               lifeRegen != 0f || energyRegen != 0f || manaRegen != 0f || energyEfficiency > 0f || lifeSteal > 0f ||
               moveSpeedPercent != 0f ||
               physicalDamage != 0f || physicalDamagePercent != 0f ||
               globalPhysicalDamagePercent != 0f || rangedPhysicalDamagePercent != 0f ||
               magicDamage != 0f || magicDamagePercent != 0f ||
               fireSkillDamagePercent != 0f || iceSkillDamagePercent != 0f || lightningSkillDamagePercent != 0f ||
               corruptionDamagePercent != 0f ||
               corruptionDamage != 0f || abilityPower != 0f ||
               attackSpeedPercent != 0f ||
               abilityCooldownReductionFraction != 0f ||
               minionDamagePercent != 0f || minionAttackSpeedPercent != 0f || minionCritChance != 0f ||
               minionMaxLifePercent != 0f ||
               critChanceBonus != 0f || critMultiplierBonus != 0f ||
               attackRangeBonus != 0f ||
               bleedChance > 0f || bleedMultiplier != 0f ||
               poisonChance > 0f || poisonMultiplier != 0f ||
               poisonDurationBonus != 0f || poisonMaxStacksBonus != 0 ||
               burnExplosionMultiplierBonus != 0f ||
               burnChance > 0f ||
               chillChance > 0f ||
               shockChance > 0f ||
               chillSlowPerStackBonus != 0f ||
               shockDamageTakenMultiplierBonus != 0f ||
               parryChance > 0f ||
               stunChance > 0f;
    }
}

/// <summary>
/// Optional per-item effects that do not fit core combat stats (expand over time).
/// </summary>
[System.Serializable]
public struct ItemMiscEffects
{
    [Header("World / Spawns")]
    [Tooltip("While equipped, subtracts this many seconds from the active map's Enemy Respawn Delay (MapNodeDefinition), after LevelSpawnDirector applies it (stacks across equipped items).")]
    [Min(0f)]
    public float enemyRespawnTimeReductionSeconds;

    public bool HasAny()
    {
        return enemyRespawnTimeReductionSeconds > 0f;
    }
}

public enum ConsumableType
{
    None,
    Food,
    Potion,
    FishingBait,

    /// <summary>
    /// "Loot bag" / lootbox-style item. Double-clicking the item in the inventory rolls each entry in
    /// <see cref="ConsumableStats.openableLoot"/> independently and grants the resulting items.
    /// Always consumes 1 of the source item on use (regardless of <see cref="ConsumableStats.consumeOnUse"/>).
    /// </summary>
    Openable,

    /// <summary>
    /// Permanent map enhancement consumable. Template assets define tier; rolled instances are created when dropped on a map.
    /// </summary>
    MapEnhancement
}

public enum FishingBaitTier
{
    Basic = 0,
    Improved = 1,
    Advanced = 2
}

/// <summary>
/// One reward row for an <see cref="ConsumableType.Openable"/> item. Each entry rolls independently — a 100%
/// row is guaranteed, a 25% row drops about a quarter of the time, and any combination of entries can hit on a
/// single open.
/// </summary>
[System.Serializable]
public struct OpenableLootEntry
{
    [Tooltip("Item ID to grant. Must exist in the ItemDatabase used by the Inventory.")]
    public string itemId;

    [Range(0f, 100f)]
    [Tooltip("Independent roll chance in percent (0–100). 100 = always drops, 25 = ~1 in 4 opens.")]
    public float chancePercent;

    [Min(1)]
    [Tooltip("Minimum amount granted when the entry rolls successfully. Defaults to 1.")]
    public int minAmount;

    [Min(1)]
    [Tooltip("Maximum amount granted when the entry rolls successfully. Must be ≥ Min Amount.")]
    public int maxAmount;
}

public enum ConsumableEffectType
{
    None,

    // Instant / special
    CleansePoison,
    CleanseBleed,
    CleanseAllAilments,
    EnergyRestore,

    // Duration buffs
    HealOverTime,

    /// <summary>Total mana restored over <see cref="ConsumableGrantedEffect.duration"/> (same rule as <see cref="HealOverTime"/> for HP).</summary>
    ManaRegenOverTime,

    EnergyRegen,
    MoveSpeed,
    AttackSpeed,

    // Damage
    PhysicalDamageBoost,
    MagicDamageBoost,
    AbilityDamageBoost,

    // Defense
    DefenseBoost,
    ArmorBoost,
    MagicResistBoost,
    DamageReduction,

    // Immunities
    PoisonImmunity,
    BleedImmunity,

    /// <summary>Food HoT (distinct from <see cref="HealOverTime"/> so potion HoT is not replaced on the same strip row).</summary>
    FoodHealOverTime,

    /// <summary>Food move speed bonus; fraction like <see cref="MoveSpeed"/> (0.1 = +10%).</summary>
    FoodMoveSpeed,

    /// <summary>Flat HP allowed above <see cref="CharacterStats.MaxHP"/> while active (total ceiling = MaxHP + magnitude).</summary>
    FoodOverheal,

    /// <summary>Fraction added to basic-attack min/max split damage (0.15 = +15%).</summary>
    FoodFocused,

    /// <summary>Ability-granted HUD buff only; excluded from consumable stat totals.</summary>
    HudAbilityBuff
}

[System.Serializable]
public struct ConsumableGrantedEffect
{
    public ConsumableEffectType effectType;
    public float magnitude;
    public float duration;
    public string effectId; // optional future hook if you move to ScriptableObject buffs later
}

[System.Serializable]
public struct ConsumableStats
{
    [Header("Type")]
    public ConsumableType consumableType;

    [Header("Use")]
    [Min(0)] public int healAmount;
    [Min(0)] public int energyAmount;
    [Min(0f)] public float cooldownSeconds;
    public bool consumeOnUse;

    [Header("Potion Effect")]
    public ConsumableGrantedEffect grantedEffect;

    [Header("Fishing Bait (Consumable Type = FishingBait)")]
    [Tooltip("Tier priority when auto-consuming bait while fishing. Higher tier is consumed first.")]
    public FishingBaitTier baitTier;
    [Min(0f)]
    [Tooltip("Fishing speed bonus in percent while this bait is active for the swing (e.g. 2 = +2%).")]
    public float fishingSpeedPercentBonus;

    [Header("Map Enhancement (Consumable Type = MapEnhancement)")]
    [Tooltip("Tier 1 rolls 1 modifier; Tier 2 rolls 2 modifiers.")]
    public MapEnhancementTier mapEnhancementTier;

    [Tooltip("Roll ranges and weights for each modifier type when this template drops on a map.")]
    public MapEnhancementModRollConfig[] mapEnhancementModRolls;

    [Header("Openable Loot Table (Consumable Type = Openable)")]
    [Min(1)]
    [Tooltip(
        "Minimum amount of this item required in the stack to open it. " +
        "When opened, that many will be consumed in one go (e.g. 5 Shards → 1 open consumes 5).")]
    public int openRequiredAmount;

    [Tooltip(
        "Items that can be obtained when the player double-clicks this item to open it. " +
        "Each entry rolls independently using its own % chance.")]
    public OpenableLootEntry[] openableLoot;

    [Header("Food timed buffs (Consumable Type = Food)")]
    [Tooltip("How long enabled food buffs last. Re-using the food while a buff is active refreshes that buff type.")]
    [Min(0f)]
    public float foodEffectDurationSeconds;

    public bool foodEnableRegen;
    [Min(0)]
    [Tooltip("Total HP restored evenly over Food Effect Duration (separate from instant Heal Amount).")]
    public int foodRegenTotalHeal;

    public bool foodEnableSwiftness;
    [Min(0f)]
    [Tooltip("Move speed bonus in percent of base (10 = +10%), stored as percent for readability.")]
    public float foodSwiftnessPercentBonus;

    public bool foodEnableOverheal;
    [Min(0)]
    [Tooltip("While active, max HP is effectively MaxHP + this value (UI can show e.g. 130/100).")]
    public int foodOverhealMaxAboveMaxHp;
    [Min(0)]
    [Tooltip("Extra heal on eat that can use the overheal ceiling (0 = only raise cap + normal instant heal).")]
    public int foodOverhealInstantHeal;

    public bool foodEnableFocused;
    [Range(0f, 2f)]
    [Tooltip("Bonus to min and max basic-attack damage per lane. 0 uses 15% when Focused is enabled.")]
    public float foodFocusedDamageBonusFraction;

    /// <summary>True when any timed food buff should be applied (requires positive duration).</summary>
    public bool HasAnyFoodTimedBuffConfigured =>
        foodEffectDurationSeconds > 0.001f &&
        ((foodEnableRegen && foodRegenTotalHeal > 0) ||
         (foodEnableSwiftness && foodSwiftnessPercentBonus > 0f) ||
         (foodEnableOverheal && foodOverhealMaxAboveMaxHp > 0) ||
         foodEnableFocused);
}

public enum EnhancementScrollTargetStat
{
    PhysicalDamage,
    MagicDamage,
    CorruptionDamage,
    Health,
    Energy,
    Mana,
    Armor,
    MagicResist,
    CorruptionResist,
    CritChance,
    CritMultiplier,
    AttackSpeed,
    LifeSteal,
    MoveSpeed,
    UpgradeSlotReduction,
    GatherSpeed,
    GatheringGrit,
    PoisonChance,
    PoisonMultiplier,
    StaminaEfficiency,
    FireDamage,
    IceDamage,
    LightningDamage,
    BurnChance,
    ChillChance,
    ShockChance,
    BurnMultiplier,
    EnergyEfficiency,
    FlatGuard,
    ManaRegen,
}

public enum EnhancementScrollModifierKind
{
    Flat,
    Percent
}

public enum EnhancementScrollFailureOutcome
{
    Nothing,
    DestroyItem,
    DowngradeOrRemoveStat
}

[System.Flags]
public enum EnhancementScrollGearMask
{
    None = 0,
    [InspectorName("Any Weapon")]
    Weapon = 1 << 0,
    [Tooltip("Legacy alias for Head, Body, and Feet combined.")]
    Armor = 1 << 1,
    Jewelry = 1 << 2,
    CombatSupport = 1 << 3,
    Tool = 1 << 4,
    [InspectorName("Melee Weapon")]
    MeleeWeapon = 1 << 5,
    [InspectorName("Ranged Weapon")]
    RangedWeapon = 1 << 6,
    [InspectorName("Magic Weapon")]
    MagicWeapon = 1 << 7,
    [InspectorName("Ranged or Melee Weapon")]
    MeleeOrRangedWeapon = MeleeWeapon | RangedWeapon,
    [InspectorName("Head")]
    Helmet = 1 << 8,
    Body = 1 << 9,
    [InspectorName("Feet")]
    Boots = 1 << 10,
    [InspectorName("Offhand")]
    OffHand = 1 << 11,
    AllArmorSlots = Helmet | Body | Boots | OffHand,
    AllGear = Weapon | AllArmorSlots | Tool
}

[System.Serializable]
public struct EnhancementScrollStats
{
    [Header("Success Behaviour")]
    [Range(0f, 1f)]
    [Tooltip("Chance to apply the modifier. 0.8 = 80%.")]
    public float successChance;

    public EnhancementScrollTargetStat targetStat;
    public EnhancementScrollModifierKind modifierKind;

    [Tooltip("Flat value or fractional percent value depending on Modifier Kind. Percent uses 0.1 = 10%.")]
    public float modifierValue;

    [Header("Failure Behaviour")]
    [Tooltip("If true, a gear upgrade slot is consumed when this scroll is used. Turn off for special scrolls like slot reduction.")]
    public bool consumeSlotOnFailure;

    public EnhancementScrollFailureOutcome failureOutcome;

    [Range(0f, 1f)]
    [Tooltip("Only used when Failure Outcome is Destroy Item, or for cursed high-risk scroll designs.")]
    public float destroyChanceOnFailure;

    [Tooltip("Future hook for high-risk scrolls. Use with Destroy Chance on Failure for cursed scrolls.")]
    public bool cursed;

    [Header("Gear Restrictions")]
    public EnhancementScrollGearMask allowedGearTypes;
}

[System.Serializable]
public struct CookableStats
{
    [Header("Cooking")]
    [Tooltip("When enabled, this item can be processed by the cooking system (configure result fields below).")]
    public bool isCookable;

    [Tooltip("Item ID this becomes after cooking.")]
    public string cookedResultItemId;

    [Tooltip("Optional: how many result items are created.")]
    [Min(1)] public int cookedResultAmount;

    [Tooltip("Optional future cooking requirement.")]
    public int requiredCookingLevel;

    [Tooltip("Optional future XP reward for cooking this item.")]
    public int cookingXp;
}

[CreateAssetMenu(menuName = "Desktop Idle Game/Item Definition", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject, ISerializationCallbackReceiver
{
    private const string TooltipMetaColor = "#E8E0D0";
    private const string TooltipMetaSize = "90%";
    private const string TooltipEnhancementHistorySuccessColor = "#7AE582";
    private const string TooltipEnhancementHistoryFailColor = "#D66A6A";

    [Header("Classification")]
    public ItemKind itemKind = ItemKind.Resource;

    [Header("Stacking")]
    [Min(1)] public int maxStack = 99;

    [Header("Identity")]
    [Tooltip("Unique internal ID. Example: log, ore")]
    public string itemId = "new_item";

    [Header("Display")]
    public string displayName = "New Item";
    [Tooltip("Optional. Used for +1 gather popups when set (e.g. Splitwood Log vs Splitwood Logs).")]
    public string displayNameSingular = "";
    public Sprite icon;

    [Header("Held Visual (optional)")]
    [Tooltip("Optional sprite used ONLY when held in hand. If empty, uses icon.")]
    [SerializeField] private Sprite heldSprite;
    public Sprite HeldSprite => heldSprite ? heldSprite : icon;

    [SerializeField] private Sprite equippedSprite;
    public Sprite EquippedSprite => equippedSprite;

    [SerializeField] private bool useCustomEquippedPose = false;
    public bool UseCustomEquippedPose => useCustomEquippedPose;

    [SerializeField] private Vector2 equippedLocalOffset = Vector2.zero;
    public Vector2 EquippedLocalOffset => equippedLocalOffset;

    [SerializeField] private float equippedLocalRotationZ = 0f;
    public float EquippedLocalRotationZ => equippedLocalRotationZ;

    [SerializeField] private bool equippedFlipX = false;
    public bool EquippedFlipX => equippedFlipX;

    [SerializeField] private bool equippedFlipY = false;
    public bool EquippedFlipY => equippedFlipY;

    [Header("Details")]
    [TextArea(2, 4)]
    public string description = "A useful item.";

    public ItemRarity rarity = ItemRarity.Common;

    [Header("Economy")]
    [Min(0)] public int value = 1;

    [Header("Equipment")]
    public EquipSlot equipSlot = EquipSlot.None;

    [Tooltip("Which hand visual this item represents (Weapon/Pickaxe/Axe/FishingRod).")]
    public ToolKey handVisualKey = ToolKey.None;

    [Header("Upgrades")]
    [Tooltip("Currently filled enhancement slots. Max slots are derived from item type and tier.")]
    [Min(0)] public int usedUpgradeSlots;
    [Tooltip("Number of successful enhancements on this item instance. Used for +1/+2 display names.")]
    [Min(0)] public int successfulEnhancements;

    [Tooltip("Per-scroll attempt log for enhanced item instances (tooltip Advanced Details).")]
    public List<EnhancementScrollHistoryEntry> enhancementScrollHistory = new();

    [Header("Weapon Stats (Only if ItemKind = Weapon)")]
    public WeaponStats weaponStats;


    [Header("Combat Support Stats (Only if ItemKind = CombatSupport)")]
    public CombatSupportStats combatSupportStats;

    [Header("Tool Stats (Only if ItemKind = Tool)")]
    public ToolStats toolStats;

    [Header("Armour Stats (Only if ItemKind = Armor)")]
    public ArmorStats armorStats;

    [Header("Bonus Stats (Equippables: Armour/Jewelry/Weapons optional)")]
    public BonusStats bonusStats;

    [Header("Random Stat Pool")]
    [Tooltip(
        "Optional affixes rolled when this item enters the player's inventory. " +
        "Roll count follows rarity (Common/Uncommon=1, Rare=2, Epic=3, Legendary=4). " +
        "Leave empty to keep static stats only.")]
    [SerializeField]
    [HideInInspector]
    private List<RandomStatPoolEntry> randomStatPool = new();

    [Header("Misc (unique effects)")]
    [Tooltip("Per-item hooks not covered by bonus stats (respawn modifiers, future procs, etc.).")]
    public ItemMiscEffects miscEffects;

    [Header("Consumable Stats (Only if ItemKind = Consumable)")]
    public ConsumableStats consumableStats;

    [Header("Enhancement Scroll Stats (Only if ItemKind = EnhancementScroll)")]
    [Tooltip("When set, scroll behaviour resolves from EnhancementOptionDatabase instead of the fields below.")]
    public string enhancementOptionId;

    public EnhancementScrollStats enhancementScrollStats;

    [Header("Cookable Stats")]
    public CookableStats cookableStats;

    public bool IsWeapon => itemKind == ItemKind.Weapon;
    public bool IsTool => itemKind == ItemKind.Tool;
    public bool IsArmor => itemKind == ItemKind.Armor;
    public bool IsJewelry => itemKind == ItemKind.Jewelry;
    public bool IsEnhancementScroll => itemKind == ItemKind.EnhancementScroll;

    public EnhancementOptionEntry ResolveEnhancementOption()
    {
        if (!IsEnhancementScroll)
            return null;

        if (!string.IsNullOrWhiteSpace(enhancementOptionId))
        {
            EnhancementOptionEntry byOptionId = EnhancementOptionResolver.GetOptionById(enhancementOptionId);
            if (byOptionId != null)
                return byOptionId;
        }

        return EnhancementOptionResolver.GetOptionForScroll(this);
    }

    public EnhancementScrollStats GetEffectiveEnhancementScrollStats()
    {
        EnhancementOptionEntry option = ResolveEnhancementOption();
        if (option != null)
            return option.ToScrollStats();

        return enhancementScrollStats;
    }
    public bool IsEquippable => IsWeapon || IsTool || IsArmor || IsJewelry || IsCombatSupport;

    public IReadOnlyList<RandomStatPoolEntry> RandomStatPoolEntries => randomStatPool;

    public bool HasRandomStatPool => randomStatPool != null && randomStatPool.Count > 0;

    private void OnEnable()
    {
        randomStatPool ??= new List<RandomStatPoolEntry>();
    }

    internal void ClearRandomStatPool()
    {
        if (randomStatPool == null)
            randomStatPool = new List<RandomStatPoolEntry>();
        else
            randomStatPool.Clear();
    }

    /// <summary>Mystery affix lines for shop previews before purchase.</summary>
    public string BuildMaskedRandomStatTooltipAppendix()
    {
        if (!HasRandomStatPool)
            return "";

        int count = ItemRandomStatRoller.GetRollCountForRarity(rarity);
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                lines.Append('\n');
            lines.Append("??");
        }

        return lines.ToString();
    }

    public bool UsesEquipmentTierGating =>
        IsWeapon || IsArmor || (IsTool && toolStats.toolType != ToolType.None);

    public SkillType GetEquipmentTierGateSkill()
    {
        if (IsWeapon)
        {
            return weaponStats.attackSkill switch
            {
                AttackSkill.Melee => SkillType.Melee,
                AttackSkill.Ranged => SkillType.Ranged,
                AttackSkill.Magic => SkillType.Magic,
                _ => SkillType.Melee
            };
        }

        if (IsTool)
        {
            return toolStats.toolType switch
            {
                ToolType.Axe => SkillType.Woodcutting,
                ToolType.Pickaxe => SkillType.Mining,
                ToolType.FishingRod => SkillType.Fishing,
                _ => SkillType.Mining
            };
        }

        if (IsArmor)
            return SkillType.Endurance;

        return SkillType.Melee;
    }

    public EquipmentTierRank GetEquipmentTierRank()
    {
        if (IsWeapon) return weaponStats.equipmentTier;
        if (IsArmor) return armorStats.equipmentTier;
        if (IsTool) return toolStats.equipmentTier;
        return EquipmentTierRank.Tier1;
    }

    public string GetEquipmentTierDisplayLabel()
    {
        if (!UsesEquipmentTierGating) return "";
        return EquipmentTierRules.GetTierDisplayLabel(GetEquipmentTierRank());
    }

    private string GetEquipmentTierNumberLabel()
    {
        return (((int)GetEquipmentTierRank()) + 1).ToString();
    }

    public bool MeetsEquipmentTierRequirement(SkillsManager sm)
    {
        if (sm == null) return true;
        if (!UsesEquipmentTierGating) return true;
        int req = EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank());
        return sm.IsLevelUnlocked(GetEquipmentTierGateSkill(), req);
    }

    public string BuildEquipmentTierBlockedMessage()
    {
        if (!UsesEquipmentTierGating) return "Cannot equip that item.";
        var sm = SkillsManager.Instance;
        int req = EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank());
        SkillType gate = GetEquipmentTierGateSkill();
        string label = GetEquipmentTierDisplayLabel();
        int cur = sm != null ? sm.GetLevel(gate) : 0;
        return $"{displayName} ({label}) needs {gate} level {req} (yours: {cur}).";
    }

    public bool RequiresOffhandSupport =>
    IsWeapon &&
    weaponStats.requiresOffhandSupport &&
    weaponStats.requiredSupportType != CombatSupportType.None;

    public CombatSupportType RequiredSupportType =>
        IsWeapon ? weaponStats.requiredSupportType : CombatSupportType.None;

    public float SupportBonusPhysicalDamage =>
    IsCombatSupport ? combatSupportStats.bonusPhysicalDamage : 0f;

    public float SupportBonusMagicDamage =>
        IsCombatSupport ? combatSupportStats.bonusMagicDamage : 0f;

    public float SupportBonusCorruptionDamage =>
        IsCombatSupport ? combatSupportStats.bonusCorruptionDamage : 0f;

    public float SupportCritChanceBonus =>
        IsCombatSupport ? combatSupportStats.critChanceBonus : 0f;

    public float SupportCritMultiplierBonus =>
        IsCombatSupport ? combatSupportStats.critMultiplierBonus : 0f;

    public float SupportAttackSpeedPercent =>
        IsCombatSupport ? combatSupportStats.attackSpeedPercent : 0f;

    public float SupportPhysicalDamagePercent =>
        IsCombatSupport ? combatSupportStats.physicalDamagePercent : 0f;

    public float SupportGlobalPhysicalDamagePercent =>
        IsCombatSupport ? combatSupportStats.globalPhysicalDamagePercent : 0f;

    public float SupportRangedPhysicalDamagePercent =>
        IsCombatSupport ? combatSupportStats.rangedPhysicalDamagePercent : 0f;

    public float SupportMagicDamagePercent =>
        IsCombatSupport ? combatSupportStats.magicDamagePercent : 0f;

    public float SupportFireDamagePercent =>
        IsCombatSupport ? combatSupportStats.fireDamagePercent : 0f;

    public float SupportIceDamagePercent =>
        IsCombatSupport ? combatSupportStats.iceDamagePercent : 0f;

    public float SupportColdDamagePercent =>
        IsCombatSupport ? combatSupportStats.coldDamagePercent : 0f;

    public float SupportCorruptionDamagePercent =>
        IsCombatSupport ? combatSupportStats.corruptionDamagePercent : 0f;

    public bool SupportConsumableOnAttack =>
        IsCombatSupport && combatSupportStats.consumableOnAttack;

    public int SupportConsumeAmountPerAttack =>
        IsCombatSupport ? Mathf.Max(0, combatSupportStats.consumeAmountPerAttack) : 0;

    public bool IsTwoHandedWeapon => IsWeapon && weaponStats.handedness == Handedness.TwoHanded;
    public bool CanDualWieldOffHand => IsWeapon && weaponStats.canEquipInOffHand && weaponStats.handedness == Handedness.OneHanded;
    public bool IsMagicWeapon => IsWeapon && weaponStats.attackSkill == AttackSkill.Magic;
    public float ManaCostPerAttack => (IsWeapon && weaponStats.attackSkill == AttackSkill.Magic)
        ? Mathf.Max(0f, weaponStats.manaCostPerAttack)
        : 0f;
    public float MagicAilmentApplyChance => (IsWeapon && weaponStats.attackSkill == AttackSkill.Magic)
        ? Mathf.Clamp01(weaponStats.magicAilmentApplyChance)
        : 0f;

    /// <summary>Burn stack chance from this fire magic weapon: same as <see cref="WeaponStats.magicAilmentApplyChance"/>.</summary>
    public float ResolveWeaponBurnApplyChance()
    {
        if (!IsWeapon || weaponStats.magicAttackType != MagicAttackType.Fire)
            return 0f;
        return Mathf.Clamp01(weaponStats.magicAilmentApplyChance);
    }

    public bool HasPhysicalWeaponDamage => IsWeapon && (weaponStats.minPhysicalDamage > 0 || weaponStats.maxPhysicalDamage > 0);
    public bool HasMagicWeaponDamage => IsWeapon && (weaponStats.TotalElementalDamageMin > 0 || weaponStats.TotalElementalDamageMax > 0);
    public bool HasFireWeaponDamage => IsWeapon && (weaponStats.minFireDamage > 0 || weaponStats.maxFireDamage > 0);
    public bool HasIceWeaponDamage => IsWeapon && (weaponStats.minIceDamage > 0 || weaponStats.maxIceDamage > 0);
    public bool HasLightningWeaponDamage => IsWeapon && (weaponStats.minLightningDamage > 0 || weaponStats.maxLightningDamage > 0);
    public bool HasCorruptionWeaponDamage => IsWeapon && (weaponStats.minCorruptionDamage > 0 || weaponStats.maxCorruptionDamage > 0);

    /// <summary>Weapon hits are corruption-only (direct hits cannot crit; poison may still use gear crit via Master of Venoms).</summary>
    public bool IsCorruptionOnlyWeapon =>
        IsWeapon &&
        HasCorruptionWeaponDamage &&
        !HasPhysicalWeaponDamage &&
        !HasMagicWeaponDamage;

    public bool IsCombatSupport => itemKind == ItemKind.CombatSupport;

    public bool IsOffhandCombatSupport =>
        IsCombatSupport && equipSlot == EquipSlot.OffHand;

    public CombatSupportType SupportType =>
        IsCombatSupport ? combatSupportStats.supportType : CombatSupportType.None;

    public MainHandWeaponArchetype MainHandArchetype
    {
        get
        {
            if (!IsWeapon)
                return MainHandWeaponArchetype.None;
            if (weaponStats.mainHandArchetype != MainHandWeaponArchetype.None)
                return weaponStats.mainHandArchetype;
            if (RequiresOffhandSupport)
                return InferRequiredMainHandArchetypeFromSupportType(RequiredSupportType);
            return MainHandWeaponArchetype.Other;
        }
    }

    public MainHandWeaponArchetype SupportRequiredMainHandArchetype
    {
        get
        {
            if (!IsCombatSupport)
                return MainHandWeaponArchetype.None;
            if (combatSupportStats.requiredMainHandArchetype != MainHandWeaponArchetype.None)
                return combatSupportStats.requiredMainHandArchetype;
            return InferRequiredMainHandArchetypeFromSupportType(SupportType);
        }
    }

    public static MainHandWeaponArchetype InferRequiredMainHandArchetypeFromSupportType(CombatSupportType supportType)
    {
        return supportType switch
        {
            CombatSupportType.Arrows => MainHandWeaponArchetype.Bow,
            CombatSupportType.Bolts => MainHandWeaponArchetype.Crossbow,
            CombatSupportType.Runes => MainHandWeaponArchetype.Staff,
            CombatSupportType.Focus => MainHandWeaponArchetype.Wand,
            _ => MainHandWeaponArchetype.None
        };
    }

    public bool HasUpgradeSlots => IsWeapon || IsArmor || IsTool || IsOffhandCombatSupport;

    public int MaxUpgradeSlots
    {
        get
        {
            if (IsWeapon || IsArmor || IsOffhandCombatSupport)
                return 5 + (int)GetEquipmentTierRank();
            if (IsTool)
                return 3;
            return 0;
        }
    }

    public int UsedUpgradeSlots => HasUpgradeSlots ? Mathf.Clamp(usedUpgradeSlots, 0, MaxUpgradeSlots) : 0;
    public int AvailableUpgradeSlots => HasUpgradeSlots ? Mathf.Max(0, MaxUpgradeSlots - UsedUpgradeSlots) : 0;
    public bool HasAvailableUpgradeSlot => AvailableUpgradeSlots > 0;
    public int MaxSuccessfulEnhancements => HasUpgradeSlots ? MaxUpgradeSlots : 0;
    public int SuccessfulEnhancements => HasUpgradeSlots ? Mathf.Clamp(successfulEnhancements, 0, MaxSuccessfulEnhancements) : 0;
    public bool HasReachedEnhancementCap => HasUpgradeSlots && MaxSuccessfulEnhancements > 0 && SuccessfulEnhancements >= MaxSuccessfulEnhancements;

    public string GetUpgradeSlotsTooltipLine()
    {
        if (!HasUpgradeSlots)
            return "";
        return $"Upgrade Slots: {GetUpgradeSlotsTooltipValue()}";
    }

    private string FormatUpgradeSlotsMetaBlock(bool includeEnhancementHistory)
    {
        if (!HasUpgradeSlots)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.Append(FormatTooltipMetaLine("Upgrade Slots", GetUpgradeSlotsTooltipValue()));
        if (includeEnhancementHistory)
        {
            string history = BuildEnhancementScrollHistoryTooltipLines();
            if (!string.IsNullOrWhiteSpace(history))
                sb.Append('\n').Append(history);
        }

        return sb.ToString();
    }

    public string BuildEnhancementScrollHistoryTooltipLines()
    {
        if (enhancementScrollHistory == null || enhancementScrollHistory.Count == 0)
            return "";

        var grouped = new List<(EnhancementScrollHistoryEntry entry, int count)>();
        var groupIndex = new Dictionary<string, int>();

        for (int i = 0; i < enhancementScrollHistory.Count; i++)
        {
            EnhancementScrollHistoryEntry entry = enhancementScrollHistory[i];
            if (entry == null)
                continue;

            string key = BuildEnhancementHistoryGroupKey(entry);
            if (groupIndex.TryGetValue(key, out int index))
            {
                var existing = grouped[index];
                grouped[index] = (existing.entry, existing.count + 1);
            }
            else
            {
                groupIndex[key] = grouped.Count;
                grouped.Add((entry, 1));
            }
        }

        var sb = new System.Text.StringBuilder();
        for (int g = 0; g < grouped.Count; g++)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(FormatEnhancementScrollHistoryEntry(grouped[g].entry, grouped[g].count));
        }

        return sb.ToString();
    }

    private static string BuildEnhancementHistoryGroupKey(EnhancementScrollHistoryEntry entry)
    {
        string scrollName = string.IsNullOrWhiteSpace(entry.scrollName) ? "Scroll" : entry.scrollName.Trim();
        string effect = entry.effectSummary?.Trim() ?? "";
        return $"{scrollName.ToLowerInvariant()}|{entry.success}|{effect}";
    }

    private static string FormatEnhancementScrollHistoryEntry(EnhancementScrollHistoryEntry entry, int count = 1)
    {
        string scrollName = string.IsNullOrWhiteSpace(entry.scrollName) ? "Scroll" : entry.scrollName.Trim();
        string countPrefix = count > 1 ? $"x{count} " : "";

        if (entry.success)
        {
            string line = string.IsNullOrWhiteSpace(entry.effectSummary)
                ? $"{countPrefix}{scrollName} success"
                : $"{countPrefix}{scrollName} success: {entry.effectSummary.Trim()}";
            return FormatTooltipMetaHistoryLine(line, TooltipEnhancementHistorySuccessColor);
        }

        return FormatTooltipMetaHistoryLine($"{countPrefix}{scrollName} failed", TooltipEnhancementHistoryFailColor);
    }

    private static string FormatTooltipMetaHistoryLine(string text, string colorHex = TooltipMetaColor) =>
        $"<size={TooltipMetaSize}><color={colorHex}>- {text}</color></size>";

    public void RecordEnhancementScrollAttempt(ItemDefinition scrollDef, bool success)
    {
        if (!scrollDef || scrollDef.itemKind != ItemKind.EnhancementScroll)
            return;

        enhancementScrollHistory ??= new List<EnhancementScrollHistoryEntry>();
        string scrollName = string.IsNullOrWhiteSpace(scrollDef.displayName)
            ? "Scroll"
            : scrollDef.displayName.Trim();

        enhancementScrollHistory.Add(new EnhancementScrollHistoryEntry
        {
            scrollName = scrollName,
            success = success,
            effectSummary = success ? FormatScrollSuccessEffectSummary(scrollDef, this) : ""
        });
    }

    public static string FormatScrollSuccessEffectSummary(ItemDefinition scrollDef, ItemDefinition target)
    {
        if (!scrollDef || scrollDef.itemKind != ItemKind.EnhancementScroll || !target)
            return "";

        EnhancementScrollStats scroll = scrollDef.GetEffectiveEnhancementScrollStats();
        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(scrollDef);
        if (option != null)
            scroll = EnhancementOptionResolver.BuildStatsForApply(option, target);

        float value = scroll.modifierValue;
        bool percent = scroll.modifierKind == EnhancementScrollModifierKind.Percent;
        bool displayAsPercent = percent || IsPercentDisplayedScrollStat(scroll.targetStat);

        switch (scroll.targetStat)
        {
            case EnhancementScrollTargetStat.UpgradeSlotReduction:
            {
                int slots = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(value)));
                return slots == 1 ? "-1 used upgrade slot" : $"-{slots} used upgrade slots";
            }

            case EnhancementScrollTargetStat.PhysicalDamage:
                if (target.IsWeapon)
                    return FormatWeaponMinMaxScrollBonus(value, percent);
                return FormatFlatOrPercentStatBonus(value, percent, displayAsPercent, "physical damage");

            case EnhancementScrollTargetStat.MagicDamage:
                if (target.IsWeapon)
                    return FormatWeaponMinMaxScrollBonus(value, percent, "fire");
                return FormatFlatOrPercentStatBonus(value, percent, displayAsPercent, "magic damage");

            case EnhancementScrollTargetStat.CorruptionDamage:
                if (target.IsWeapon)
                    return FormatWeaponMinMaxScrollBonus(value, percent, "corruption");
                return FormatFlatOrPercentStatBonus(value, percent, displayAsPercent, "corruption damage");

            case EnhancementScrollTargetStat.CritChance:
            case EnhancementScrollTargetStat.CritMultiplier:
            case EnhancementScrollTargetStat.AttackSpeed:
            case EnhancementScrollTargetStat.LifeSteal:
            case EnhancementScrollTargetStat.MoveSpeed:
            case EnhancementScrollTargetStat.PoisonChance:
            case EnhancementScrollTargetStat.PoisonMultiplier:
            case EnhancementScrollTargetStat.GatherSpeed:
            case EnhancementScrollTargetStat.GatheringGrit:
            case EnhancementScrollTargetStat.StaminaEfficiency:
                return FormatFlatOrPercentStatBonus(
                    value,
                    percent,
                    displayAsPercent,
                    GetEnhancementScrollTargetStatDisplayName(scroll.targetStat));

            case EnhancementScrollTargetStat.Health:
            case EnhancementScrollTargetStat.Energy:
            case EnhancementScrollTargetStat.Mana:
            case EnhancementScrollTargetStat.Armor:
            case EnhancementScrollTargetStat.MagicResist:
            case EnhancementScrollTargetStat.CorruptionResist:
                return FormatFlatOrPercentStatBonus(
                    value,
                    percent,
                    displayAsPercent,
                    GetEnhancementScrollTargetStatDisplayName(scroll.targetStat));

            default:
                return FormatFlatOrPercentStatBonus(
                    value,
                    percent,
                    displayAsPercent,
                    GetEnhancementScrollTargetStatDisplayName(scroll.targetStat));
        }
    }

    private static string FormatWeaponMinMaxScrollBonus(float value, bool percent, string damageLabel = null)
    {
        string labelPrefix = string.IsNullOrWhiteSpace(damageLabel) ? "" : $"{damageLabel.Trim()} ";

        if (percent)
        {
            string pct = $"{Mathf.Abs(value) * 100f:0.#}%";
            return $"+{pct} {labelPrefix}min, +{pct} {labelPrefix}max";
        }

        int amount = Mathf.RoundToInt(Mathf.Abs(value));
        return $"+{amount} {labelPrefix}min, +{amount} {labelPrefix}max";
    }

    private static string FormatFlatOrPercentStatBonus(float value, bool percent, bool displayAsPercent, string statLabel)
    {
        if (displayAsPercent)
            return $"{FormatSignedPercent01(value)} {statLabel}";

        float magnitude = Mathf.Abs(value);
        if (!Mathf.Approximately(magnitude, Mathf.Round(magnitude)))
            return $"+{magnitude:0.#} {statLabel}";

        return $"+{Mathf.RoundToInt(magnitude)} {statLabel}";
    }

    public void NormalizeEnhancementState()
    {
        if (!HasUpgradeSlots)
        {
            usedUpgradeSlots = 0;
            successfulEnhancements = 0;
            return;
        }

        usedUpgradeSlots = UsedUpgradeSlots;
        int normalizedSuccessfulEnhancements = SuccessfulEnhancements;
        if (successfulEnhancements != normalizedSuccessfulEnhancements)
            successfulEnhancements = normalizedSuccessfulEnhancements;

        if (successfulEnhancements > 0)
            displayName = $"{StripEnhancementSuffix(displayName)} +{successfulEnhancements}";
    }

    private string GetUpgradeSlotsTooltipValue()
    {
        string value = $"{UsedUpgradeSlots}/{MaxUpgradeSlots}";
        if (HasReachedEnhancementCap)
            value += " <color=#D66A6A>(Max reached)</color>";
        return value;
    }

    private static string StripEnhancementSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Item";

        string trimmed = value.Trim();
        int marker = trimmed.LastIndexOf(" +", System.StringComparison.Ordinal);
        if (marker < 0)
            return trimmed;

        string suffix = trimmed.Substring(marker + 2);
        return int.TryParse(suffix, out _) ? trimmed.Substring(0, marker) : trimmed;
    }

    public int ArmorValue => (IsArmor ? armorStats.armor : 0) + bonusStats.armor;
    public int MagicResist => (IsArmor ? armorStats.magicResist : 0) + bonusStats.magicResist;
    public int CorruptionResist => (IsArmor ? armorStats.corruptionResist : 0) + bonusStats.corruptionResist;

    public float PhysBlockChance
    {
        get
        {
            float baseBlock = IsArmor ? Mathf.Clamp01(armorStats.physBlockChance) : 0f;
            return Mathf.Clamp01(baseBlock + bonusStats.physBlockChance);
        }
    }

    public int BonusHealth => (IsArmor ? armorStats.bonusHealth : 0) + bonusStats.bonusHealth;
    public int BonusEnergy => (IsArmor ? armorStats.bonusEnergy : 0) + bonusStats.bonusEnergy;

    /// <summary>Armor-only flat contribution to natural guard cap.</summary>
    public int ArmorFlatGuard => IsArmor ? Mathf.Max(0, armorStats.flatGuard) : 0;

    /// <summary>Armor-only additive fraction: natural cap includes MaxHP * (1 + sum of these).</summary>
    public float ArmorMaxGuardPercent => IsArmor ? Mathf.Max(0f, armorStats.maxGuardPercent) : 0f;
    public int BonusMana => bonusStats.bonusMana;

    public float LifeRegen => bonusStats.lifeRegen;
    public float EnergyRegen => bonusStats.energyRegen;
    public float ManaRegen => bonusStats.manaRegen;
    public float CombatEnergyEfficiency
    {
        get
        {
            if (!IsArmor && !IsJewelry)
                return 0f;

            float total = 0f;
            if (IsArmor)
                total += Mathf.Max(0f, armorStats.energyEfficiency);
            total += Mathf.Max(0f, bonusStats.energyEfficiency);
            return total;
        }
    }
    public float LifeSteal => Mathf.Clamp01(bonusStats.lifeSteal);
    public float MoveSpeedPercent => bonusStats.moveSpeedPercent;

    public float PhysicalDamage => bonusStats.physicalDamage;
    public float MagicDamage => bonusStats.magicDamage;
    public float BonusCorruptionDamage => bonusStats.corruptionDamage;
    public float AbilityPower => bonusStats.abilityPower;

    public float PhysicalDamagePercent => bonusStats.physicalDamagePercent;

    /// <summary>Armor/weapon bonus + combat support: stacks into the global physical multiplier.</summary>
    public float GlobalPhysicalDamagePercent =>
        bonusStats.globalPhysicalDamagePercent +
        (IsCombatSupport ? combatSupportStats.globalPhysicalDamagePercent : 0f);

    /// <summary>Reserved ranged physical % (gear + support).</summary>
    public float RangedPhysicalDamagePercent =>
        bonusStats.rangedPhysicalDamagePercent +
        (IsCombatSupport ? combatSupportStats.rangedPhysicalDamagePercent : 0f);

    /// <summary>Corruption attack-split % from armor/accessory bonus (not flat corruption damage).</summary>
    public float EquipmentCorruptionDamagePercent => bonusStats.corruptionDamagePercent;

    public float MagicDamagePercent => bonusStats.magicDamagePercent;

    public float FireSkillDamagePercent => bonusStats.fireSkillDamagePercent;
    public float IceSkillDamagePercent => bonusStats.iceSkillDamagePercent;
    public float LightningSkillDamagePercent => bonusStats.lightningSkillDamagePercent;

    public float BleedChance => Mathf.Clamp01(bonusStats.bleedChance);
    public float BleedMultiplier => Mathf.Max(0f, bonusStats.bleedMultiplier);

    public float PoisonChance => Mathf.Clamp01(bonusStats.poisonChance);
    public float PoisonMultiplier => Mathf.Max(0f, bonusStats.poisonMultiplier);
    public float PoisonDurationBonus => bonusStats.poisonDurationBonus;
    public int PoisonMaxStacksBonus => Mathf.Max(0, bonusStats.poisonMaxStacksBonus);
    public float BurnExplosionMultiplierBonus => bonusStats.burnExplosionMultiplierBonus;
    public float BonusBurnChance => Mathf.Clamp01(bonusStats.burnChance);
    public float ChillSlowPerStackBonus => bonusStats.chillSlowPerStackBonus;
    public float ShockDamageTakenMultiplierBonus => bonusStats.shockDamageTakenMultiplierBonus;
    public float ParryChance => Mathf.Clamp01(bonusStats.parryChance);
    public float StunChance => Mathf.Clamp01(bonusStats.stunChance);

    public bool IsConsumable => itemKind == ItemKind.Consumable;

    public bool IsFood =>
        IsConsumable && consumableStats.consumableType == ConsumableType.Food;

    public bool IsPotion =>
        IsConsumable && consumableStats.consumableType == ConsumableType.Potion;

    /// <summary>True when this is an Openable consumable (loot-bag / lootbox style). See <see cref="ConsumableType.Openable"/>.</summary>
    public bool IsOpenable =>
        IsConsumable && consumableStats.consumableType == ConsumableType.Openable;

    public bool IsFishingBait =>
        IsConsumable && consumableStats.consumableType == ConsumableType.FishingBait;

    public bool IsMapEnhancement =>
        IsConsumable && consumableStats.consumableType == ConsumableType.MapEnhancement;

    public MapEnhancementTier MapEnhancementTier =>
        IsMapEnhancement ? consumableStats.mapEnhancementTier : MapEnhancementTier.Tier1;

    public int MapEnhancementModCount =>
        MapEnhancementTier == MapEnhancementTier.Tier2 ? 2 : 1;

    public MapEnhancementModRollConfig[] GetMapEnhancementModRollConfigs()
    {
        MapEnhancementModRollConfig[] rolls = consumableStats.mapEnhancementModRolls;
        if (rolls != null && rolls.Length > 0)
            return rolls;

        return MapEnhancementRollDefaults.CreateDefaultRollConfigs();
    }

    public FishingBaitTier FishingBaitTier =>
        IsFishingBait ? consumableStats.baitTier : global::FishingBaitTier.Basic;

    /// <summary>0..1 additive fishing speed fraction from this bait (2 = +2%).</summary>
    public float FishingBaitSpeedBonusFraction =>
        IsFishingBait ? Mathf.Max(0f, consumableStats.fishingSpeedPercentBonus) / 100f : 0f;

    /// <summary>Configured loot table for an Openable item. Always non-null; empty for non-openables.</summary>
    public OpenableLootEntry[] OpenableLootEntries =>
        IsOpenable && consumableStats.openableLoot != null
            ? consumableStats.openableLoot
            : System.Array.Empty<OpenableLootEntry>();

    /// <summary>
    /// Minimum amount of this Openable item the player must have in a stack to open it (and the amount
    /// consumed per open). Clamped to ≥ 1 so legacy/zero-initialised assets behave like classic single-open items.
    /// </summary>
    public int OpenRequiredAmount =>
        IsOpenable ? Mathf.Max(1, consumableStats.openRequiredAmount) : 1;

    /// <summary>
    /// Rolls each entry in <see cref="OpenableLootEntries"/> independently. Returns the list of (itemId, amount)
    /// rewards to grant. Empty list when no entries roll (player can still open the item — it just gives nothing).
    /// </summary>
    public System.Collections.Generic.List<(string itemId, int amount)> RollOpenableLoot()
    {
        var result = new System.Collections.Generic.List<(string itemId, int amount)>();

        OpenableLootEntry[] entries = OpenableLootEntries;
        if (entries.Length == 0)
            return result;

        for (int i = 0; i < entries.Length; i++)
        {
            OpenableLootEntry e = entries[i];
            if (string.IsNullOrWhiteSpace(e.itemId))
                continue;

            float chance01 = Mathf.Clamp01(e.chancePercent / 100f);
            if (chance01 <= 0f)
                continue;

            if (chance01 < 1f && Random.value > chance01)
                continue;

            int lo = Mathf.Max(1, e.minAmount);
            int hi = Mathf.Max(lo, e.maxAmount);
            int amount = lo == hi ? lo : Random.Range(lo, hi + 1);

            result.Add((e.itemId.Trim(), amount));
        }

        return result;
    }

    public float EnhancementScrollSuccessChance
    {
        get
        {
            if (!IsEnhancementScroll)
                return 0f;

            EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(this);
            if (option == null)
                return 0f;

            if (option.track == EnhancementTrack.Corruption)
                return EnhancementSuccessChanceRules.ChaosSuccessChance;

            if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
                return Mathf.Clamp01(option.successChance);

            return 0f;
        }
    }

    public bool EnhancementScrollCanTarget(ItemDefinition gear)
    {
        if (!IsEnhancementScroll || gear == null || !gear.HasUpgradeSlots)
            return false;

        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(this);
        if (option == null || !EnhancementOptionResolver.ScrollMatchesDatabase(this, option))
            return false;

        return EnhancementScrollGearRules.MaskAllowsGear(option.allowedGearTypes, gear);
    }

    public bool CanUseEnhancementScrollOn(ItemDefinition gear)
    {
        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(this);
        if (option == null || !EnhancementOptionResolver.ScrollMatchesDatabase(this, option))
            return false;

        return option.CanApplyToGear(gear, SkillsManager.Instance);
    }

    /// <summary>
    /// Whether this item already contributes the stat type that an enhancement scroll would modify
    /// (e.g. Physical Damage scroll requires physical weapon range or flat bonus physical on the item).
    /// </summary>
    public bool HasBaseStatForEnhancementScroll(EnhancementScrollTargetStat stat)
    {
        const float eps = 1e-4f;

        switch (stat)
        {
            case EnhancementScrollTargetStat.PhysicalDamage:
                if (IsWeapon)
                    return HasPhysicalWeaponDamage || Mathf.Abs(bonusStats.physicalDamage) > eps;
                return Mathf.Abs(bonusStats.physicalDamage) > eps;

            case EnhancementScrollTargetStat.MagicDamage:
                if (IsWeapon)
                    return HasMagicWeaponDamage || Mathf.Abs(bonusStats.magicDamage) > eps;
                return Mathf.Abs(bonusStats.magicDamage) > eps;

            case EnhancementScrollTargetStat.FireDamage:
                return HasFireWeaponDamage;

            case EnhancementScrollTargetStat.IceDamage:
                return HasIceWeaponDamage;

            case EnhancementScrollTargetStat.LightningDamage:
                return HasLightningWeaponDamage;

            case EnhancementScrollTargetStat.CorruptionDamage:
                if (IsWeapon)
                    return HasCorruptionWeaponDamage || Mathf.Abs(bonusStats.corruptionDamage) > eps;
                return Mathf.Abs(bonusStats.corruptionDamage) > eps;

            case EnhancementScrollTargetStat.Health:
                return BonusHealth != 0;

            case EnhancementScrollTargetStat.Energy:
                return BonusEnergy != 0;

            case EnhancementScrollTargetStat.Mana:
                return BonusMana != 0;

            case EnhancementScrollTargetStat.Armor:
                return ArmorValue != 0;

            case EnhancementScrollTargetStat.MagicResist:
                return MagicResist != 0;

            case EnhancementScrollTargetStat.CorruptionResist:
                return CorruptionResist != 0;

            case EnhancementScrollTargetStat.CritChance:
                if (IsWeapon)
                    return weaponStats.critChance > eps || Mathf.Abs(bonusStats.critChanceBonus) > eps;
                return Mathf.Abs(bonusStats.critChanceBonus) > eps;

            case EnhancementScrollTargetStat.CritMultiplier:
                if (IsWeapon)
                    return weaponStats.critMultiplier > 1f + eps || Mathf.Abs(bonusStats.critMultiplierBonus) > eps;
                return Mathf.Abs(bonusStats.critMultiplierBonus) > eps;

            case EnhancementScrollTargetStat.AttackSpeed:
                if (IsWeapon)
                    return weaponStats.attacksPerSecond > eps || Mathf.Abs(bonusStats.attackSpeedPercent) > eps;
                return Mathf.Abs(bonusStats.attackSpeedPercent) > eps;

            case EnhancementScrollTargetStat.LifeSteal:
                return Mathf.Abs(bonusStats.lifeSteal) > eps;

            case EnhancementScrollTargetStat.MoveSpeed:
                return Mathf.Abs(bonusStats.moveSpeedPercent) > eps;

            case EnhancementScrollTargetStat.GatherSpeed:
                // Tool scrolls should be able to initialize stats from 0 on valid tool items.
                return IsTool;

            case EnhancementScrollTargetStat.GatheringGrit:
                // Tool scrolls should be able to initialize stats from 0 on valid tool items.
                return IsTool;

            case EnhancementScrollTargetStat.StaminaEfficiency:
                // Tool scrolls should be able to initialize stats from 0 on valid tool items.
                return IsTool;

            case EnhancementScrollTargetStat.PoisonChance:
                return Mathf.Abs(bonusStats.poisonChance) > eps;

            case EnhancementScrollTargetStat.PoisonMultiplier:
                return Mathf.Abs(bonusStats.poisonMultiplier) > eps || Mathf.Abs(bonusStats.poisonChance) > eps;

            case EnhancementScrollTargetStat.BurnChance:
                return Mathf.Abs(bonusStats.burnChance) > eps || ResolveWeaponBurnApplyChance() > eps || HasFireWeaponDamage;

            case EnhancementScrollTargetStat.ChillChance:
                return Mathf.Abs(bonusStats.chillChance) > eps || HasIceWeaponDamage;

            case EnhancementScrollTargetStat.ShockChance:
                return Mathf.Abs(bonusStats.shockChance) > eps || HasLightningWeaponDamage;

            case EnhancementScrollTargetStat.BurnMultiplier:
                return Mathf.Abs(bonusStats.burnExplosionMultiplierBonus) > eps ||
                       Mathf.Abs(bonusStats.burnChance) > eps ||
                       ResolveWeaponBurnApplyChance() > eps ||
                       HasFireWeaponDamage;

            case EnhancementScrollTargetStat.EnergyEfficiency:
                return CombatEnergyEfficiency > eps;

            case EnhancementScrollTargetStat.FlatGuard:
                return ArmorFlatGuard > 0;

            case EnhancementScrollTargetStat.ManaRegen:
                return Mathf.Abs(bonusStats.manaRegen) > eps;

            case EnhancementScrollTargetStat.UpgradeSlotReduction:
                return true;

            default:
                return false;
        }
    }

    public int HealAmount =>
        IsConsumable ? Mathf.Max(0, consumableStats.healAmount) : 0;

    public int EnergyAmount =>
        IsConsumable ? Mathf.Max(0, consumableStats.energyAmount) : 0;

    public float UseCooldown =>
        IsConsumable ? Mathf.Max(0f, consumableStats.cooldownSeconds) : 0f;

    public bool ConsumeOnUse =>
        IsConsumable && consumableStats.consumeOnUse;

    public bool HasGrantedEffect =>
        IsPotion &&
        consumableStats.grantedEffect.effectType != ConsumableEffectType.None &&
        consumableStats.grantedEffect.duration > 0f;

    public bool HasFoodTimedBuffs =>
        IsFood && consumableStats.HasAnyFoodTimedBuffConfigured;

    public ConsumableGrantedEffect GrantedEffect => consumableStats.grantedEffect;

    public bool IsCookable => cookableStats.isCookable;

    public string CookedResultItemId =>
        IsCookable ? cookableStats.cookedResultItemId : null;

    public int CookedResultAmount =>
        IsCookable ? Mathf.Max(1, cookableStats.cookedResultAmount) : 0;

    public int RequiredCookingLevel =>
        IsCookable ? Mathf.Max(0, cookableStats.requiredCookingLevel) : 0;

    public int CookingXp =>
        IsCookable ? Mathf.Max(0, cookableStats.cookingXp) : 0;

    /// <summary>Seconds subtracted from enemy respawn delay while this item is equipped (see <see cref="ItemMiscEffects"/>).</summary>
    public float EnemyRespawnTimeReductionSeconds => Mathf.Max(0f, miscEffects.enemyRespawnTimeReductionSeconds);

    public int RollPhysicalDamage()
    {
        if (!IsWeapon) return 0;
        return Random.Range(weaponStats.minPhysicalDamage, weaponStats.maxPhysicalDamage + 1);
    }

    public int RollMagicDamage()
    {
        if (!IsWeapon) return 0;
        return Random.Range(weaponStats.TotalElementalDamageMin, weaponStats.TotalElementalDamageMax + 1);
    }

    public int RollCorruptionDamage()
    {
        if (!IsWeapon) return 0;
        return Random.Range(weaponStats.minCorruptionDamage, weaponStats.maxCorruptionDamage + 1);
    }

    public float AttackCooldown
    {
        get
        {
            if (!IsWeapon) return 1f;

            float aps = weaponStats.attacksPerSecond;
            if (aps <= 0f) aps = 1f;

            aps *= Mathf.Max(0.1f, 1f + bonusStats.attackSpeedPercent);
            return 1f / aps;
        }
    }

    public float AttackRange
    {
        get
        {
            if (!IsWeapon) return 0f;
            return Mathf.Max(0f, weaponStats.attackRange + bonusStats.attackRangeBonus);
        }
    }

    public float GatherSpeedMultiplier
    {
        get
        {
            if (!IsTool || toolStats.gatherSpeedMultiplier <= 0f)
                return 1f;

            return toolStats.gatherSpeedMultiplier;
        }
    }

    public float GatheringGrit => IsTool ? Mathf.Clamp01(toolStats.gatheringGrit) : 0f;
    public float BonusResourceFindChance => IsTool ? Mathf.Max(0f, toolStats.bonusResourceFindChance) : 0f;
    public float StaminaEfficiency => IsTool ? Mathf.Clamp01(toolStats.staminaEfficiency) : 0f;

    public string GetRarityLabel() => rarity.ToString();

    /// <summary>Normal: combined tier requirement. Alt-held: upgrade slots (below rarity), tier, type, hands.</summary>
    public string BuildTooltipMiscStatsText(bool showAdvancedDetails = false)
    {
        if (!showAdvancedDetails)
            return BuildTooltipMiscStatsNormalText();

        string advanced = BuildTooltipMiscStatsAdvancedText(showAdvancedDetails);
        return string.IsNullOrWhiteSpace(advanced) ? BuildTooltipMiscStatsNormalText() : advanced;
    }

    private string BuildTooltipMiscStatsNormalText()
    {
        if (IsWeapon)
            return BuildWeaponTooltipProfileMetaLines();

        if (IsArmor)
            return FormatTooltipTierRequirementLine();

        if (IsTool)
        {
            string type = toolStats.toolType.ToString();
            if (UsesEquipmentTierGating)
                return FormatTooltipTierRequirementLine() + "\n" + FormatTooltipMetaLine("Tool", type);

            return FormatTooltipMetaLine("Tool", type);
        }

        return string.Empty;
    }

    private string FormatTooltipTierRequirementLine()
    {
        if (!UsesEquipmentTierGating && !IsArmor)
            return string.Empty;

        string skill = GetEquipmentTierGateSkill().ToString().ToLowerInvariant();
        int req = EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank());
        return FormatTooltipMetaLine("Tier", $"{GetEquipmentTierNumberLabel()} (Requires {skill} lv {req})");
    }

    private string BuildWeaponTooltipProfileMetaLines()
    {
        string type = weaponStats.attackSkill == AttackSkill.Magic
            ? $"{weaponStats.attackSkill} ({weaponStats.magicAttackType})"
            : weaponStats.attackSkill.ToString();
        string hands = FormatHandednessLabel(weaponStats.handedness);

        return FormatTooltipTierRequirementLine() + "\n" +
               FormatTooltipMetaLine("Type", type) + "\n" +
               FormatTooltipMetaLine("Hands", hands);
    }

    private string BuildTooltipMiscStatsAdvancedText(bool includeEnhancementHistory)
    {
        if (IsWeapon)
        {
            var sb = new System.Text.StringBuilder();
            if (HasUpgradeSlots)
                sb.Append(FormatUpgradeSlotsMetaBlock(includeEnhancementHistory)).Append('\n');
            sb.Append(BuildWeaponTooltipProfileMetaLines());
            return sb.ToString().TrimEnd('\n');
        }

        if (IsCombatSupport)
            return FormatTooltipMetaLine("Type", $"Support ({SupportType})");

        if (IsMapEnhancement)
        {
            return FormatTooltipMetaLine("Tier", ((int)MapEnhancementTier).ToString()) + "\n" +
                   FormatTooltipMetaLine("Type", "Map Enhancement");
        }

        if (IsTool)
        {
            var sb = new System.Text.StringBuilder();
            if (HasUpgradeSlots)
                sb.Append(FormatUpgradeSlotsMetaBlock(includeEnhancementHistory)).Append('\n');
            if (UsesEquipmentTierGating)
                sb.Append(FormatTooltipMetaLine("Tier", GetEquipmentTierNumberLabel())).Append('\n');

            sb.Append(FormatTooltipMetaLine("Tool", toolStats.toolType.ToString()));
            return sb.ToString().TrimEnd('\n');
        }

        if (IsArmor)
        {
            var sb = new System.Text.StringBuilder();
            if (HasUpgradeSlots)
                sb.Append(FormatUpgradeSlotsMetaBlock(includeEnhancementHistory)).Append('\n');
            sb.Append(FormatTooltipTierRequirementLine());
            return sb.ToString().TrimEnd('\n');
        }

        if (IsJewelry && HasUpgradeSlots)
            return FormatUpgradeSlotsMetaBlock(includeEnhancementHistory);

        return string.Empty;
    }

    /// <summary>Damage, speeds, resistances, gather rates — shown in the main stats TMP.</summary>
    public string BuildTooltipMainStatsText()
    {
        return BuildTooltipMainStatsText(null);
    }

    /// <summary>Main stats block with optional bold highlights for bonuses vs a baseline item.</summary>
    public string BuildTooltipMainStatsText(ItemDefinition baselineForHighlights)
    {
        if (baselineForHighlights == null || ReferenceEquals(this, baselineForHighlights))
            return BuildTooltipMainStatsTextInternal();

        return ItemTooltipStatHighlight.BuildMainStatsText(this, baselineForHighlights);
    }

    private string BuildTooltipMainStatsTextInternal()
    {
        if (IsWeapon)
        {
            float aps = weaponStats.attacksPerSecond > 0f ? weaponStats.attacksPerSecond : 1f;
            aps *= Mathf.Max(0.1f, 1f + bonusStats.attackSpeedPercent);

            string speed = $"{aps:0.##} atk/s";
            float critChancePct = Mathf.Clamp01(weaponStats.critChance + bonusStats.critChanceBonus) * 100f;
            // Crit multiplier is stored as 1.00 = +0% (aka 100%). Show bonus over baseline: 1.30 -> +30%.
            float critMultBonusPct =
                (Mathf.Max(0f, weaponStats.critMultiplier + bonusStats.critMultiplierBonus) - 1f) * 100f;

            string range = $"{AttackRange:0.##}";

            string dual = (weaponStats.handedness == Handedness.OneHanded && weaponStats.canEquipInOffHand)
                ? "\nDual Wield: Yes"
                : "";

            string extras = BuildBonusLines(
                includeDefense: false,
                omitBurnBonuses: true,
                omitAilmentChanceBonuses: true,
                omitAilmentMultiplierBonuses: true);

            string s = "";

            if (HasPhysicalWeaponDamage)
                s += $"Physical Damage: {weaponStats.minPhysicalDamage}-{weaponStats.maxPhysicalDamage}\n";

            if (weaponStats.minFireDamage > 0 || weaponStats.maxFireDamage > 0)
                s += $"Fire Damage: {weaponStats.minFireDamage}-{weaponStats.maxFireDamage}\n";
            if (weaponStats.minIceDamage > 0 || weaponStats.maxIceDamage > 0)
                s += $"Ice Damage: {weaponStats.minIceDamage}-{weaponStats.maxIceDamage}\n";
            if (weaponStats.minLightningDamage > 0 || weaponStats.maxLightningDamage > 0)
                s += $"Lightning Damage: {weaponStats.minLightningDamage}-{weaponStats.maxLightningDamage}\n";

            if (HasCorruptionWeaponDamage)
                s += $"Corruption Damage: {weaponStats.minCorruptionDamage}-{weaponStats.maxCorruptionDamage}\n";

            s += $"Speed: {speed}\n";

            if (HasSignificantPercentPoints(critChancePct))
                s += $"Crit Chance: {FormatSignedPercent100WithPlus(critChancePct)}\n";

            if (HasSignificantPercentPoints(critMultBonusPct))
                s += $"Crit Multi: {FormatSignedPercent100WithPlus(critMultBonusPct)}\n";

            string ailments = BuildWeaponAilmentsLine();
            if (!string.IsNullOrWhiteSpace(ailments))
                s += ailments + "\n";

            s += $"Range: {range}" + dual;

            if (PhysBlockChance > 0f)
                s += $"\nPhys Block: {FormatSignedPercent01(PhysBlockChance)}";
            if (BonusHealth > 0)
                s += $"\nHealth: +{BonusHealth}";

            if (weaponStats.attackSkill == AttackSkill.Magic)
                s += $"\nMana Cost: {ManaCostPerAttack:0.##}";

            if (RequiresOffhandSupport)
                s += $"\nRequires: {RequiredSupportType}";

            if (!string.IsNullOrWhiteSpace(extras))
                s += "\n" + StripDuplicateWeaponProcLines(extras);

            string misc = BuildMiscTooltipLines();
            if (!string.IsNullOrWhiteSpace(misc))
                s += "\n" + misc;

            return s.TrimEnd('\n');
        }

        if (IsCombatSupport)
        {
            string s = "";
            if (SupportBonusPhysicalDamage != 0f) s += $"\nPhysical Damage: {FormatSignedNumber(SupportBonusPhysicalDamage)}";
            if (SupportBonusMagicDamage != 0f) s += $"\nMagic Damage: {FormatSignedNumber(SupportBonusMagicDamage)}";
            if (SupportBonusCorruptionDamage != 0f) s += $"\nCorruption Damage: {FormatSignedNumber(SupportBonusCorruptionDamage)}";
            float supAllPhys = SupportPhysicalDamagePercent + SupportGlobalPhysicalDamagePercent;
            if (supAllPhys != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(supAllPhys, "All physical")}";
            if (SupportRangedPhysicalDamagePercent != 0f)
                s += $"\nRanged Dmg: {FormatSignedPercent01(SupportRangedPhysicalDamagePercent)}";
            if (SupportMagicDamagePercent != 0f)
                s += $"\nMagic Damage {FormatSignedPercent01(SupportMagicDamagePercent)}";
            if (SupportFireDamagePercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportFireDamagePercent, "Fire skills")}";
            if (SupportIceDamagePercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportIceDamagePercent, "Ice skills")}";
            if (SupportColdDamagePercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportColdDamagePercent, "Cold skills")}";
            if (SupportCorruptionDamagePercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportCorruptionDamagePercent, "Corruption")}";
            if (SupportCritChanceBonus != 0f) s += $"\nCrit Chance: {FormatSignedPercent01(SupportCritChanceBonus)}";
            if (SupportCritMultiplierBonus != 0f) s += $"\nCrit Multi: {FormatSignedPercent01(SupportCritMultiplierBonus)}";
            if (SupportAttackSpeedPercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportAttackSpeedPercent, "Attack Speed")}";

            if (SupportConsumableOnAttack)
                s += $"\nConsumes: {Mathf.Max(1, SupportConsumeAmountPerAttack)} per attack";

            string miscCs = BuildMiscTooltipLines();
            if (!string.IsNullOrWhiteSpace(miscCs))
                s += "\n" + miscCs;

            return s.TrimStart('\n').TrimEnd('\n');
        }

        if (IsTool)
        {
            string extras = BuildBonusLines(includeDefense: true);

            string s =
                $"Gather Speed: {GatherSpeedMultiplier:0.##}x\n" +
                $"Gather Grit: {GatheringGrit * 100f:0.#}%\n" +
                $"Bonus Find: +{BonusResourceFindChance * 100f:0.#}%\n" +
                $"Stamina Efficiency: +{StaminaEfficiency * 100f:0.#}%";

            if (!string.IsNullOrWhiteSpace(extras))
                s += "\n" + extras;

            string miscT = BuildMiscTooltipLines();
            if (!string.IsNullOrWhiteSpace(miscT))
                s += "\n" + miscT;

            return s;
        }

        if (IsArmor || IsJewelry)
        {
            string s = "";

            if (ArmorValue != 0) s += $"Armour: {ArmorValue}\n";
            if (MagicResist != 0) s += $"Magic Res: {MagicResist}\n";
            if (CorruptionResist != 0) s += $"Corruption Res: {CorruptionResist}\n";
            if (BonusHealth != 0) s += $"Health: +{BonusHealth}\n";
            if (BonusEnergy != 0) s += $"Energy: +{BonusEnergy}\n";
            if (BonusMana != 0) s += $"Mana: +{BonusMana}\n";
            if (CombatEnergyEfficiency > 0.0001f)
                s += $"Energy Efficiency: +{CombatEnergyEfficiency * 100f:0.#}%\n";
            if (PhysBlockChance > 0f) s += $"Phys Block: {PhysBlockChance * 100f:0.#}%\n";
            if (ArmorFlatGuard > 0) s += $"Guard: +{ArmorFlatGuard}\n";
            if (ArmorMaxGuardPercent > 0.00001f) s += $"Max Guard: {FormatSignedPercent01(ArmorMaxGuardPercent)}\n";

            string extras = BuildBonusLines(includeDefense: false);

            if (!string.IsNullOrWhiteSpace(extras))
                s += extras + "\n";

            string miscAj = BuildMiscTooltipLines();
            if (!string.IsNullOrWhiteSpace(miscAj))
                s += miscAj;

            return s.TrimEnd('\n');
        }

        if (IsConsumable)
        {
            string s = IsFishingBait
                ? "Consumable: Used for fishing."
                : $"Consumable: {consumableStats.consumableType}";

            if (IsFishingBait)
                s += $"\nFishing Speed: +{FishingBaitSpeedBonusFraction * 100f:0.#}%";

            if (HealAmount > 0)
                s += $"\nHeals: {HealAmount}";

            if (EnergyAmount > 0)
                s += $"\nEnergy: +{EnergyAmount}";

            if (UseCooldown > 0f)
                s += $"\nCooldown: {UseCooldown:0.##}s";

            if (HasGrantedEffect)
                s += $"\nEffect: {ConsumableEffectTooltip.Format(GrantedEffect)}";

            if (HasFoodTimedBuffs)
            {
                string foodLines = GetFoodTimedBuffSummaryText();
                if (!string.IsNullOrWhiteSpace(foodLines))
                    s += "\n" + foodLines;
            }

            if (IsOpenable)
            {
                int required = OpenRequiredAmount;
                if (required > 1)
                    s += $"\nRequires {required} to open";

                string contents = BuildOpenableLootTooltipLines();
                if (!string.IsNullOrEmpty(contents))
                    s += "\nContains:\n" + contents;

                s += required > 1
                    ? $"\n<i>Double-click to open ({required} consumed)</i>"
                    : "\n<i>Double-click to open</i>";
            }

            if (CanCook())
                s += "\nCookable: Yes";

            return s;
        }

        if (IsEnhancementScroll)
        {
            EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(this);
            if (option == null)
            {
                return "Enhancement scroll (no matching database entry)\n" +
                       $"Effect: {FormatEnhancementScrollModifier()}\n" +
                       $"Allowed Gear: {FormatEnhancementGearMask(GetEffectiveEnhancementScrollStats().allowedGearTypes)}";
            }

            string successLine = option.track == EnhancementTrack.Corruption
                ? $"Success Chance: {EnhancementSuccessChanceRules.ChaosSuccessChance * 100f:0.#}%"
                : option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction
                    ? $"Success Chance: {Mathf.Clamp01(option.successChance) * 100f:0.#}%"
                    : option.allowedGearTypes == EnhancementScrollGearMask.Tool
                        ? "Success Chance: Varies by item (70%–30%)"
                        : "Success Chance: Varies by item (70%–5%)";

            string s =
                $"{successLine}\n" +
                $"Effect: {FormatEnhancementScrollModifierFromOption(option)}\n" +
                $"Allowed Gear: {FormatEnhancementGearMask(option.allowedGearTypes)}\n";

            if (option.targetStat != EnhancementScrollTargetStat.UpgradeSlotReduction)
                s += $"\nRequires Target: {GetEnhancementScrollTargetStatDisplayName(option.targetStat)}";

            s += $"\nConsumes Slot On Use: {(option.consumeSlotOnFailure ? "Yes" : "No")}";

            if (option.failureOutcome != EnhancementScrollFailureOutcome.Nothing)
                s += $"\nFailure: {FormatEnhancementFailureFromOption(option)}";

            return s;
        }

        if (CanCook())
        {
            string s = "Cookable: Yes";

            if (!string.IsNullOrWhiteSpace(CookedResultItemId))
                s += $"\nCook Result: {CookedResultItemId} x{CookedResultAmount}";

            if (RequiredCookingLevel > 0)
                s += $"\nRequired Cooking: {RequiredCookingLevel}";

            if (CookingXp > 0)
                s += $"\nCooking XP: {CookingXp}";

            return s;
        }

        return string.Empty;
    }

    /// <summary>Full block (misc + main) for legacy callers and copy/paste.</summary>
    public string BuildTooltipStatsText()
    {
        string misc = BuildTooltipMiscStatsText();
        string main = BuildTooltipMainStatsText();
        if (string.IsNullOrWhiteSpace(misc))
            return main ?? string.Empty;
        if (string.IsNullOrWhiteSpace(main))
            return misc;
        return misc.TrimEnd('\n') + "\n\n" + main.TrimEnd('\n');
    }

    private static string FormatSignedNumber(float value)
    {
        return $"{value:+0.##;-0.##;0}";
    }

    private static string FormatSignedInt(int value)
    {
        return $"{value:+0;-0;0}";
    }

    private static string FormatSignedPercent01(float value01)
    {
        return $"{value01 * 100f:+0.#;-0.#;0}%";
    }

    private static string FormatSignedPercent100(float value)
    {
        return $"{value:+0.#;-0.#;0}%";
    }

    /// <summary>Same style as ability tooltips: <c>coefficient * 100</c>% label (0 = omit line elsewhere).</summary>
    private static string FormatScalingCoefficientPercentLine(float fraction, string label)
    {
        return $"{label}: {fraction * 100f:+0.#;-0.#;0}%";
    }

    private static string FormatSignedPercent100WithPlus(float value)
    {
        return $"{value:+0.#;-0.#;0}%";
    }

    private static string FormatAilmentMultiplierLine(float multiplierFraction, string label) =>
        $"{label}: {FormatSignedPercent100WithPlus(multiplierFraction * 100f)}";

    private static string DeltaPercentFractionNote(float deltaFraction) =>
        $"{deltaFraction * 100f:+0.#;-0.#;0}%";

    private static bool HasSignificantPercentPoints(float percentPoints) =>
        Mathf.Abs(percentPoints) > 0.001f;

    internal static string StripDuplicateWeaponProcLines(string bonusLines)
    {
        return StripDuplicateTooltipStatLines(bonusLines);
    }

    /// <summary>Removes duplicate stat lines (case-insensitive label) and proc lines already shown above.</summary>
    internal static string StripDuplicateTooltipStatLines(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
            return block;

        string[] lines = block.Split('\n');
        var keptLines = new List<string>();
        var labelIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            string line = StripRichTextForCompare(rawLine);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("Phys Block:", System.StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Parry Chance:", System.StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Stun Chance:", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string labelKey = GetTooltipLineLabelKey(line);
            if (!string.IsNullOrEmpty(labelKey) && labelIndex.TryGetValue(labelKey, out int existingIndex))
            {
                // Prefer the green advanced/bonus line over a plain duplicate from the main block.
                if (IsHighlightedTooltipLine(rawLine) && !IsHighlightedTooltipLine(keptLines[existingIndex]))
                    keptLines[existingIndex] = rawLine;

                continue;
            }

            if (!string.IsNullOrEmpty(labelKey))
                labelIndex[labelKey] = keptLines.Count;

            keptLines.Add(rawLine);
        }

        return string.Join("\n", keptLines);
    }

    private static bool IsHighlightedTooltipLine(string line) =>
        !string.IsNullOrWhiteSpace(line) &&
        line.IndexOf(ItemTooltipStatHighlight.HighlightColorHex, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static string StripRichTextForCompare(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        return System.Text.RegularExpressions.Regex.Replace(line, "<[^>]*>", string.Empty).Trim();
    }

    private static string GetTooltipLineLabelKey(string plainLine)
    {
        int colon = plainLine.IndexOf(':');
        if (colon <= 0)
            return plainLine;

        string label = plainLine.Substring(0, colon).Trim();
        if (label.Equals("Bleed Damage", System.StringComparison.OrdinalIgnoreCase))
            return "Bleed Multi";
        if (label.Equals("Poison Damage", System.StringComparison.OrdinalIgnoreCase))
            return "Poison Multi";

        return label;
    }

    internal string BuildBonusLinesForHighlight(ItemDefinition baseline)
    {
        if (baseline == null)
            return string.Empty;

        return BuildBonusCompareLines(
            baseline.bonusStats,
            includeDefense: false,
            omitBurnBonuses: true,
            omitAilmentChanceBonuses: true,
            omitAilmentMultiplierBonuses: true);
    }

    internal string BuildMiscLinesForHighlight(ItemDefinition baseline)
    {
        if (baseline == null)
            return BuildMiscTooltipLines();

        float current = EnemyRespawnTimeReductionSeconds;
        float baseValue = baseline.EnemyRespawnTimeReductionSeconds;
        if (current <= 0f)
            return string.Empty;

        string total = $"Enemy respawn: -{baseValue:0.#}s";
        if (Mathf.Approximately(current, baseValue))
            return $"Enemy respawn: -{current:0.#}s";

        float delta = current - baseValue;
        return ItemTooltipStatHighlight.HighlightWithAddedNote(total, $"-{delta:0.#}s");
    }

    private string BuildBonusCompareLines(
        BonusStats baselineStats,
        bool includeDefense,
        bool omitBurnBonuses,
        bool omitAilmentChanceBonuses,
        bool omitAilmentMultiplierBonuses,
        bool highlightAllLines = false)
    {
        var sb = new System.Text.StringBuilder();
        BonusStats cur = bonusStats;

        void AppendCompared(string baselineLine, string unchangedLine, bool hasDelta, string deltaNote)
        {
            string line = hasDelta ? baselineLine : unchangedLine;
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (sb.Length > 0)
                sb.Append('\n');

            if (hasDelta)
                sb.Append(ItemTooltipStatHighlight.HighlightWithAddedNote(baselineLine, deltaNote));
            else if (highlightAllLines)
                sb.Append(ItemTooltipStatHighlight.WrapHighlightedStatLine(line));
            else
                sb.Append(line);
        }

        static bool HasFloatDelta(float current, float baseline) =>
            !Mathf.Approximately(current, baseline);

        static bool HasIntDelta(int current, int baseline) => current != baseline;

        static float FloatDelta(float current, float baseline) => current - baseline;

        static int IntDelta(int current, int baseline) => current - baseline;

        void AppendAilmentCompareLineIfDelta(
            float current,
            float baselineValue,
            string baselineLine,
            string currentLine,
            string deltaNote)
        {
            if (!HasFloatDelta(current, baselineValue))
                return;

            AppendCompared(baselineLine, currentLine, true, deltaNote);
        }

        if (includeDefense)
        {
            if (cur.armor != 0)
            {
                int delta = IntDelta(cur.armor, baselineStats.armor);
                AppendCompared(
                    $"Armour: {FormatSignedInt(baselineStats.armor)}",
                    $"Armour: {FormatSignedInt(cur.armor)}",
                    HasIntDelta(cur.armor, baselineStats.armor),
                    FormatSignedInt(delta));
            }

            if (cur.magicResist != 0)
            {
                int delta = IntDelta(cur.magicResist, baselineStats.magicResist);
                AppendCompared(
                    $"Magic Res: {FormatSignedInt(baselineStats.magicResist)}",
                    $"Magic Res: {FormatSignedInt(cur.magicResist)}",
                    HasIntDelta(cur.magicResist, baselineStats.magicResist),
                    FormatSignedInt(delta));
            }

            if (cur.corruptionResist != 0)
            {
                int delta = IntDelta(cur.corruptionResist, baselineStats.corruptionResist);
                AppendCompared(
                    $"Corruption Res: {FormatSignedInt(baselineStats.corruptionResist)}",
                    $"Corruption Res: {FormatSignedInt(cur.corruptionResist)}",
                    HasIntDelta(cur.corruptionResist, baselineStats.corruptionResist),
                    FormatSignedInt(delta));
            }

            if (cur.physBlockChance != 0f)
            {
                float delta = FloatDelta(cur.physBlockChance, baselineStats.physBlockChance);
                AppendCompared(
                    $"Phys Block: {FormatSignedPercent01(baselineStats.physBlockChance)}",
                    $"Phys Block: {FormatSignedPercent01(cur.physBlockChance)}",
                    HasFloatDelta(cur.physBlockChance, baselineStats.physBlockChance),
                    FormatSignedPercent01(delta));
            }
        }

        if (cur.lifeRegen != 0f)
        {
            float delta = FloatDelta(cur.lifeRegen, baselineStats.lifeRegen);
            AppendCompared(

                $"Life Regen: {FormatSignedNumber(baselineStats.lifeRegen)}/s",

                $"Life Regen: {FormatSignedNumber(cur.lifeRegen)}/s",

                HasFloatDelta(cur.lifeRegen, baselineStats.lifeRegen),

                $"{FormatSignedNumber(delta)}/s");
        }

        if (cur.energyRegen != 0f)
        {
            float delta = FloatDelta(cur.energyRegen, baselineStats.energyRegen);
            AppendCompared(

                $"Energy Regen: {FormatSignedNumber(baselineStats.energyRegen)}/s",

                $"Energy Regen: {FormatSignedNumber(cur.energyRegen)}/s",

                HasFloatDelta(cur.energyRegen, baselineStats.energyRegen),

                $"{FormatSignedNumber(delta)}/s");
        }

        if (cur.manaRegen != 0f)
        {
            float delta = FloatDelta(cur.manaRegen, baselineStats.manaRegen);
            AppendCompared(

                $"Mana Regen: {FormatSignedNumber(baselineStats.manaRegen)}/s",

                $"Mana Regen: {FormatSignedNumber(cur.manaRegen)}/s",

                HasFloatDelta(cur.manaRegen, baselineStats.manaRegen),

                $"{FormatSignedNumber(delta)}/s");
        }

        if (cur.moveSpeedPercent != 0f)
        {
            float delta = FloatDelta(cur.moveSpeedPercent, baselineStats.moveSpeedPercent);
            AppendCompared(

                $"Move Speed: {FormatSignedPercent01(baselineStats.moveSpeedPercent)}",

                $"Move Speed: {FormatSignedPercent01(cur.moveSpeedPercent)}",

                HasFloatDelta(cur.moveSpeedPercent, baselineStats.moveSpeedPercent),

                FormatSignedPercent01(delta));
        }

        if (cur.physicalDamage != 0f)
        {
            float delta = FloatDelta(cur.physicalDamage, baselineStats.physicalDamage);
            AppendCompared(

                $"Physical Damage: {FormatSignedNumber(baselineStats.physicalDamage)}",

                $"Physical Damage: {FormatSignedNumber(cur.physicalDamage)}",

                HasFloatDelta(cur.physicalDamage, baselineStats.physicalDamage),

                FormatSignedNumber(delta));
        }

        float allPhysPct = cur.physicalDamagePercent + cur.globalPhysicalDamagePercent;
        if (allPhysPct != 0f)
        {
            float baseAllPhysPct = baselineStats.physicalDamagePercent + baselineStats.globalPhysicalDamagePercent;
            float delta = FloatDelta(allPhysPct, baseAllPhysPct);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baseAllPhysPct, "All physical"),

                FormatScalingCoefficientPercentLine(allPhysPct, "All physical"),

                HasFloatDelta(allPhysPct, baseAllPhysPct),

                DeltaPercentFractionNote(delta));
        }

        if (cur.rangedPhysicalDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.rangedPhysicalDamagePercent, baselineStats.rangedPhysicalDamagePercent);
            AppendCompared(

                $"Ranged Dmg: {FormatSignedPercent01(baselineStats.rangedPhysicalDamagePercent)}",

                $"Ranged Dmg: {FormatSignedPercent01(cur.rangedPhysicalDamagePercent)}",

                HasFloatDelta(cur.rangedPhysicalDamagePercent, baselineStats.rangedPhysicalDamagePercent),

                FormatSignedPercent01(delta));
        }

        if (cur.magicDamage != 0f)
        {
            float delta = FloatDelta(cur.magicDamage, baselineStats.magicDamage);
            AppendCompared(

                $"Magic Damage: {FormatSignedNumber(baselineStats.magicDamage)}",

                $"Magic Damage: {FormatSignedNumber(cur.magicDamage)}",

                HasFloatDelta(cur.magicDamage, baselineStats.magicDamage),

                FormatSignedNumber(delta));
        }

        if (cur.magicDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.magicDamagePercent, baselineStats.magicDamagePercent);
            AppendCompared(

                $"Magic Dmg: {FormatSignedPercent01(baselineStats.magicDamagePercent)}",

                $"Magic Dmg: {FormatSignedPercent01(cur.magicDamagePercent)}",

                HasFloatDelta(cur.magicDamagePercent, baselineStats.magicDamagePercent),

                FormatSignedPercent01(delta));
        }

        if (cur.fireSkillDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.fireSkillDamagePercent, baselineStats.fireSkillDamagePercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.fireSkillDamagePercent, "Fire skills"),

                FormatScalingCoefficientPercentLine(cur.fireSkillDamagePercent, "Fire skills"),

                HasFloatDelta(cur.fireSkillDamagePercent, baselineStats.fireSkillDamagePercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.iceSkillDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.iceSkillDamagePercent, baselineStats.iceSkillDamagePercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.iceSkillDamagePercent, "Ice skills"),

                FormatScalingCoefficientPercentLine(cur.iceSkillDamagePercent, "Ice skills"),

                HasFloatDelta(cur.iceSkillDamagePercent, baselineStats.iceSkillDamagePercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.lightningSkillDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.lightningSkillDamagePercent, baselineStats.lightningSkillDamagePercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.lightningSkillDamagePercent, "Lightning skills"),

                FormatScalingCoefficientPercentLine(cur.lightningSkillDamagePercent, "Lightning skills"),

                HasFloatDelta(cur.lightningSkillDamagePercent, baselineStats.lightningSkillDamagePercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.corruptionDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.corruptionDamagePercent, baselineStats.corruptionDamagePercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.corruptionDamagePercent, "Corruption"),

                FormatScalingCoefficientPercentLine(cur.corruptionDamagePercent, "Corruption"),

                HasFloatDelta(cur.corruptionDamagePercent, baselineStats.corruptionDamagePercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.corruptionDamage != 0f)
        {
            float delta = FloatDelta(cur.corruptionDamage, baselineStats.corruptionDamage);
            AppendCompared(

                $"Corruption Damage: {FormatSignedNumber(baselineStats.corruptionDamage)}",

                $"Corruption Damage: {FormatSignedNumber(cur.corruptionDamage)}",

                HasFloatDelta(cur.corruptionDamage, baselineStats.corruptionDamage),

                FormatSignedNumber(delta));
        }

        if (cur.abilityPower != 0f)
        {
            float delta = FloatDelta(cur.abilityPower, baselineStats.abilityPower);
            AppendCompared(

                $"Ability Power: {FormatSignedPercent100(baselineStats.abilityPower)}",

                $"Ability Power: {FormatSignedPercent100(cur.abilityPower)}",

                HasFloatDelta(cur.abilityPower, baselineStats.abilityPower),

                FormatSignedPercent100(delta));
        }

        if (cur.lifeSteal != 0f)
        {
            float delta = FloatDelta(cur.lifeSteal, baselineStats.lifeSteal);
            AppendCompared(

                $"Life Steal: {FormatSignedPercent01(baselineStats.lifeSteal)}",

                $"Life Steal: {FormatSignedPercent01(cur.lifeSteal)}",

                HasFloatDelta(cur.lifeSteal, baselineStats.lifeSteal),

                FormatSignedPercent01(delta));
        }

        if (cur.attackSpeedPercent != 0f)
        {
            float delta = FloatDelta(cur.attackSpeedPercent, baselineStats.attackSpeedPercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.attackSpeedPercent, "Attack Speed"),

                FormatScalingCoefficientPercentLine(cur.attackSpeedPercent, "Attack Speed"),

                HasFloatDelta(cur.attackSpeedPercent, baselineStats.attackSpeedPercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.abilityCooldownReductionFraction != 0f)
        {
            float delta = FloatDelta(cur.abilityCooldownReductionFraction, baselineStats.abilityCooldownReductionFraction);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.abilityCooldownReductionFraction, "Ability Cooldown Reduction"),

                FormatScalingCoefficientPercentLine(cur.abilityCooldownReductionFraction, "Ability Cooldown Reduction"),

                HasFloatDelta(cur.abilityCooldownReductionFraction, baselineStats.abilityCooldownReductionFraction),

                DeltaPercentFractionNote(delta));
        }

        if (cur.minionDamagePercent != 0f)
        {
            float delta = FloatDelta(cur.minionDamagePercent, baselineStats.minionDamagePercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.minionDamagePercent, "Minion Damage"),

                FormatScalingCoefficientPercentLine(cur.minionDamagePercent, "Minion Damage"),

                HasFloatDelta(cur.minionDamagePercent, baselineStats.minionDamagePercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.minionAttackSpeedPercent != 0f)
        {
            float delta = FloatDelta(cur.minionAttackSpeedPercent, baselineStats.minionAttackSpeedPercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.minionAttackSpeedPercent, "Minion Attack Speed"),

                FormatScalingCoefficientPercentLine(cur.minionAttackSpeedPercent, "Minion Attack Speed"),

                HasFloatDelta(cur.minionAttackSpeedPercent, baselineStats.minionAttackSpeedPercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.minionCritChance != 0f)
        {
            float delta = FloatDelta(cur.minionCritChance, baselineStats.minionCritChance);
            AppendCompared(

                $"Minion Crit Chance: {FormatSignedPercent01(baselineStats.minionCritChance)}",

                $"Minion Crit Chance: {FormatSignedPercent01(cur.minionCritChance)}",

                HasFloatDelta(cur.minionCritChance, baselineStats.minionCritChance),

                FormatSignedPercent01(delta));
        }

        if (cur.minionMaxLifePercent != 0f)
        {
            float delta = FloatDelta(cur.minionMaxLifePercent, baselineStats.minionMaxLifePercent);
            AppendCompared(

                FormatScalingCoefficientPercentLine(baselineStats.minionMaxLifePercent, "Minion Health"),

                FormatScalingCoefficientPercentLine(cur.minionMaxLifePercent, "Minion Health"),

                HasFloatDelta(cur.minionMaxLifePercent, baselineStats.minionMaxLifePercent),

                DeltaPercentFractionNote(delta));
        }

        if (cur.critChanceBonus != 0f)
        {
            float delta = FloatDelta(cur.critChanceBonus, baselineStats.critChanceBonus);
            AppendCompared(

                $"Crit Chance: {FormatSignedPercent01(baselineStats.critChanceBonus)}",

                $"Crit Chance: {FormatSignedPercent01(cur.critChanceBonus)}",

                HasFloatDelta(cur.critChanceBonus, baselineStats.critChanceBonus),

                FormatSignedPercent01(delta));
        }

        if (cur.critMultiplierBonus != 0f)
        {
            float delta = FloatDelta(cur.critMultiplierBonus, baselineStats.critMultiplierBonus);
            AppendCompared(

                $"Crit Multi: {FormatSignedPercent01(baselineStats.critMultiplierBonus)}",

                $"Crit Multi: {FormatSignedPercent01(cur.critMultiplierBonus)}",

                HasFloatDelta(cur.critMultiplierBonus, baselineStats.critMultiplierBonus),

                FormatSignedPercent01(delta));
        }

        if (cur.attackRangeBonus != 0f)
        {
            float delta = FloatDelta(cur.attackRangeBonus, baselineStats.attackRangeBonus);
            AppendCompared(

                $"Range: {FormatSignedNumber(baselineStats.attackRangeBonus)}",

                $"Range: {FormatSignedNumber(cur.attackRangeBonus)}",

                HasFloatDelta(cur.attackRangeBonus, baselineStats.attackRangeBonus),

                FormatSignedNumber(delta));
        }

        if (!omitAilmentChanceBonuses)
        {
            float bleedDelta = FloatDelta(cur.bleedChance, baselineStats.bleedChance);
            AppendAilmentCompareLineIfDelta(
                cur.bleedChance,
                baselineStats.bleedChance,
                $"Bleed Chance: {FormatSignedPercent01(baselineStats.bleedChance)}",
                $"Bleed Chance: {FormatSignedPercent01(cur.bleedChance)}",
                FormatSignedPercent01(bleedDelta));
        }

        if (!omitAilmentMultiplierBonuses)
        {
            float bleedMultDelta = FloatDelta(cur.bleedMultiplier, baselineStats.bleedMultiplier);
            AppendAilmentCompareLineIfDelta(
                cur.bleedMultiplier,
                baselineStats.bleedMultiplier,
                FormatAilmentMultiplierLine(baselineStats.bleedMultiplier, "Bleed Multi"),
                FormatAilmentMultiplierLine(cur.bleedMultiplier, "Bleed Multi"),
                DeltaPercentFractionNote(bleedMultDelta));
        }

        if (!omitAilmentChanceBonuses)
        {
            float poisonDelta = FloatDelta(cur.poisonChance, baselineStats.poisonChance);
            AppendAilmentCompareLineIfDelta(
                cur.poisonChance,
                baselineStats.poisonChance,
                $"Poison Chance: {FormatSignedPercent01(baselineStats.poisonChance)}",
                $"Poison Chance: {FormatSignedPercent01(cur.poisonChance)}",
                FormatSignedPercent01(poisonDelta));
        }

        if (!omitAilmentMultiplierBonuses)
        {
            float poisonMultDelta = FloatDelta(cur.poisonMultiplier, baselineStats.poisonMultiplier);
            AppendAilmentCompareLineIfDelta(
                cur.poisonMultiplier,
                baselineStats.poisonMultiplier,
                FormatAilmentMultiplierLine(baselineStats.poisonMultiplier, "Poison Multi"),
                FormatAilmentMultiplierLine(cur.poisonMultiplier, "Poison Multi"),
                DeltaPercentFractionNote(poisonMultDelta));
        }

        if (!omitAilmentMultiplierBonuses && HasFloatDelta(cur.poisonDurationBonus, baselineStats.poisonDurationBonus))
        {
            float delta = FloatDelta(cur.poisonDurationBonus, baselineStats.poisonDurationBonus);
            AppendCompared(
                $"Poison Duration: {FormatSignedNumber(baselineStats.poisonDurationBonus)}s",
                $"Poison Duration: {FormatSignedNumber(cur.poisonDurationBonus)}s",
                true,
                $"{FormatSignedNumber(delta)}s");
        }

        if (!omitAilmentMultiplierBonuses && HasIntDelta(cur.poisonMaxStacksBonus, baselineStats.poisonMaxStacksBonus))
        {
            int delta = IntDelta(cur.poisonMaxStacksBonus, baselineStats.poisonMaxStacksBonus);
            AppendCompared(
                $"Poison Max Stacks: {FormatSignedInt(baselineStats.poisonMaxStacksBonus)}",
                $"Poison Max Stacks: {FormatSignedInt(cur.poisonMaxStacksBonus)}",
                true,
                FormatSignedInt(delta));
        }

        if (!omitBurnBonuses)
        {
            if (!omitAilmentChanceBonuses)
            {
                float burnDelta = FloatDelta(cur.burnChance, baselineStats.burnChance);
                AppendAilmentCompareLineIfDelta(
                    cur.burnChance,
                    baselineStats.burnChance,
                    $"Burn Chance: {FormatSignedPercent01(baselineStats.burnChance)}",
                    $"Burn Chance: {FormatSignedPercent01(cur.burnChance)}",
                    FormatSignedPercent01(burnDelta));
            }

            if (cur.burnExplosionMultiplierBonus != 0f)
            {
                float delta = FloatDelta(cur.burnExplosionMultiplierBonus, baselineStats.burnExplosionMultiplierBonus);
                AppendCompared(
                    FormatScalingCoefficientPercentLine(baselineStats.burnExplosionMultiplierBonus, "Burn tick mult (added to character base)"),
                    FormatScalingCoefficientPercentLine(cur.burnExplosionMultiplierBonus, "Burn tick mult (added to character base)"),
                    HasFloatDelta(cur.burnExplosionMultiplierBonus, baselineStats.burnExplosionMultiplierBonus),
                    DeltaPercentFractionNote(delta));
            }
        }

        if (cur.chillSlowPerStackBonus != 0f)
        {
            float delta = FloatDelta(cur.chillSlowPerStackBonus, baselineStats.chillSlowPerStackBonus);
            AppendCompared(

                $"Chill Slow/Stack Bonus: {FormatSignedPercent01(baselineStats.chillSlowPerStackBonus)}",

                $"Chill Slow/Stack Bonus: {FormatSignedPercent01(cur.chillSlowPerStackBonus)}",

                HasFloatDelta(cur.chillSlowPerStackBonus, baselineStats.chillSlowPerStackBonus),

                FormatSignedPercent01(delta));
        }

        if (cur.shockDamageTakenMultiplierBonus != 0f)
        {
            float delta = FloatDelta(cur.shockDamageTakenMultiplierBonus, baselineStats.shockDamageTakenMultiplierBonus);
            AppendCompared(

                $"Shock Amp Bonus: {FormatSignedPercent01(baselineStats.shockDamageTakenMultiplierBonus)}",

                $"Shock Amp Bonus: {FormatSignedPercent01(cur.shockDamageTakenMultiplierBonus)}",

                HasFloatDelta(cur.shockDamageTakenMultiplierBonus, baselineStats.shockDamageTakenMultiplierBonus),

                FormatSignedPercent01(delta));
        }

        {
            float parryDelta = FloatDelta(cur.parryChance, baselineStats.parryChance);
            AppendAilmentCompareLineIfDelta(
                cur.parryChance,
                baselineStats.parryChance,
                $"Parry Chance: {FormatSignedPercent01(baselineStats.parryChance)}",
                $"Parry Chance: {FormatSignedPercent01(cur.parryChance)}",
                FormatSignedPercent01(parryDelta));
        }

        {
            float stunDelta = FloatDelta(cur.stunChance, baselineStats.stunChance);
            AppendAilmentCompareLineIfDelta(
                cur.stunChance,
                baselineStats.stunChance,
                $"Stun Chance: {FormatSignedPercent01(baselineStats.stunChance)}",
                $"Stun Chance: {FormatSignedPercent01(cur.stunChance)}",
                FormatSignedPercent01(stunDelta));
        }

        return sb.ToString();
    }

    internal string BuildWeaponAilmentsLineForTooltip(ItemDefinition baseline) =>
        baseline != null ? BuildUnifiedWeaponAilmentLines(baseline).matchingLines : BuildWeaponAilmentsLine(null);

    internal string BuildWeaponAilmentBonusLinesForTooltip(ItemDefinition baseline) =>
        baseline != null ? BuildUnifiedWeaponAilmentLines(baseline).bonusLines : string.Empty;

    internal string BuildMiscTooltipLinesForTooltip() => BuildMiscTooltipLines();

    private string BuildBonusLines(
        bool includeDefense,
        bool omitBurnBonuses = false,
        bool omitAilmentChanceBonuses = false,
        bool omitAilmentMultiplierBonuses = false)
    {
        string s = "";

        if (includeDefense)
        {
            if (bonusStats.armor != 0) s += $"Armour: {FormatSignedInt(bonusStats.armor)}\n";
            if (bonusStats.magicResist != 0) s += $"Magic Res: {FormatSignedInt(bonusStats.magicResist)}\n";
            if (bonusStats.corruptionResist != 0) s += $"Corruption Res: {FormatSignedInt(bonusStats.corruptionResist)}\n";
            if (bonusStats.physBlockChance != 0f) s += $"Phys Block: {FormatSignedPercent01(bonusStats.physBlockChance)}\n";
        }

        if (bonusStats.lifeRegen != 0f) s += $"Life Regen: {FormatSignedNumber(bonusStats.lifeRegen)}/s\n";
        if (bonusStats.energyRegen != 0f) s += $"Energy Regen: {FormatSignedNumber(bonusStats.energyRegen)}/s\n";
        if (bonusStats.manaRegen != 0f) s += $"Mana Regen: {FormatSignedNumber(bonusStats.manaRegen)}/s\n";
        if (bonusStats.moveSpeedPercent != 0f)
            s += $"Move Speed: {FormatSignedPercent01(bonusStats.moveSpeedPercent)}\n";
        if (bonusStats.physicalDamage != 0f) s += $"Physical Damage: {FormatSignedNumber(bonusStats.physicalDamage)}\n";
        float allPhysPct = bonusStats.physicalDamagePercent + bonusStats.globalPhysicalDamagePercent;
        if (allPhysPct != 0f)
            s += $"{FormatScalingCoefficientPercentLine(allPhysPct, "All physical")}\n";
        if (bonusStats.rangedPhysicalDamagePercent != 0f)
            s += $"Ranged Dmg: {FormatSignedPercent01(bonusStats.rangedPhysicalDamagePercent)}\n";
        if (bonusStats.magicDamage != 0f) s += $"Magic Damage: {FormatSignedNumber(bonusStats.magicDamage)}\n";
        if (bonusStats.magicDamagePercent != 0f)
            s += $"Magic Dmg: {FormatSignedPercent01(bonusStats.magicDamagePercent)}\n";
        if (bonusStats.fireSkillDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.fireSkillDamagePercent, "Fire skills")}\n";
        if (bonusStats.iceSkillDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.iceSkillDamagePercent, "Ice skills")}\n";
        if (bonusStats.lightningSkillDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.lightningSkillDamagePercent, "Lightning skills")}\n";
        if (bonusStats.corruptionDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.corruptionDamagePercent, "Corruption")}\n";
        if (bonusStats.corruptionDamage != 0f) s += $"Corruption Damage: {FormatSignedNumber(bonusStats.corruptionDamage)}\n";
        if (bonusStats.abilityPower != 0f) s += $"Ability Power: {FormatSignedPercent100(bonusStats.abilityPower)}\n";
        if (bonusStats.lifeSteal != 0f) s += $"Life Steal: {FormatSignedPercent01(bonusStats.lifeSteal)}\n";

        if (bonusStats.attackSpeedPercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.attackSpeedPercent, "Attack Speed")}\n";
        if (bonusStats.abilityCooldownReductionFraction != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.abilityCooldownReductionFraction, "Ability Cooldown Reduction")}\n";
        if (bonusStats.minionDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.minionDamagePercent, "Minion Damage")}\n";
        if (bonusStats.minionAttackSpeedPercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.minionAttackSpeedPercent, "Minion Attack Speed")}\n";
        if (bonusStats.minionCritChance != 0f)
            s += $"Minion Crit Chance: {FormatSignedPercent01(bonusStats.minionCritChance)}\n";
        if (bonusStats.minionMaxLifePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.minionMaxLifePercent, "Minion Health")}\n";
        if (bonusStats.critChanceBonus != 0f) s += $"Crit Chance: {FormatSignedPercent01(bonusStats.critChanceBonus)}\n";
        if (bonusStats.critMultiplierBonus != 0f) s += $"Crit Multi: {FormatSignedPercent01(bonusStats.critMultiplierBonus)}\n";
        if (bonusStats.attackRangeBonus != 0f) s += $"Range: {FormatSignedNumber(bonusStats.attackRangeBonus)}\n";

        if (!omitAilmentChanceBonuses && bonusStats.bleedChance != 0f)
            s += $"Bleed Chance: {FormatSignedPercent01(bonusStats.bleedChance)}\n";
        if (!omitAilmentMultiplierBonuses && bonusStats.bleedMultiplier != 0f)
            s += $"{FormatAilmentMultiplierLine(bonusStats.bleedMultiplier, "Bleed Multi")}\n";

        if (!omitAilmentChanceBonuses && bonusStats.poisonChance != 0f)
            s += $"Poison Chance: {FormatSignedPercent01(bonusStats.poisonChance)}\n";
        if (!omitAilmentMultiplierBonuses && bonusStats.poisonMultiplier != 0f)
            s += $"{FormatAilmentMultiplierLine(bonusStats.poisonMultiplier, "Poison Multi")}\n";
        if (!omitAilmentMultiplierBonuses && bonusStats.poisonDurationBonus != 0f) s += $"Poison Duration: {FormatSignedNumber(bonusStats.poisonDurationBonus)}s\n";
        if (!omitAilmentMultiplierBonuses && bonusStats.poisonMaxStacksBonus != 0) s += $"Poison Max Stacks: {FormatSignedInt(bonusStats.poisonMaxStacksBonus)}\n";
        if (!omitBurnBonuses)
        {
            if (!omitAilmentChanceBonuses && bonusStats.burnChance != 0f)
                s += $"Burn Chance: {FormatSignedPercent01(bonusStats.burnChance)}\n";
            if (bonusStats.burnExplosionMultiplierBonus != 0f)
                s += $"{FormatScalingCoefficientPercentLine(bonusStats.burnExplosionMultiplierBonus, "Burn tick mult (added to character base)")}\n";
        }
        if (bonusStats.chillSlowPerStackBonus != 0f) s += $"Chill Slow/Stack Bonus: {FormatSignedPercent01(bonusStats.chillSlowPerStackBonus)}\n";
        if (bonusStats.shockDamageTakenMultiplierBonus != 0f) s += $"Shock Amp Bonus: {FormatSignedPercent01(bonusStats.shockDamageTakenMultiplierBonus)}\n";
        if (bonusStats.parryChance != 0f) s += $"Parry Chance: {FormatSignedPercent01(bonusStats.parryChance)}\n";
        if (bonusStats.stunChance != 0f) s += $"Stun Chance: {FormatSignedPercent01(bonusStats.stunChance)}\n";

        return s.TrimEnd('\n');
    }

    private static string FormatTooltipMetaLine(string label, string value)
    {
        return $"<size={TooltipMetaSize}><color={TooltipMetaColor}>{label}: {value}</color></size>";
    }

    private static string FormatHandednessLabel(Handedness handedness)
    {
        return handedness == Handedness.TwoHanded ? "Two-Handed" : "One-Handed";
    }

    private string BuildWeaponAilmentsLine(ItemDefinition baseline = null)
    {
        // Base (orange) block: ailment stats that match the unenhanced item template.
        // Rolled / bonus deltas are shown only in BuildBonusCompareLines (green).
        string block = "";

        if (weaponStats.attackSkill == AttackSkill.Magic && MagicAilmentApplyChance > 0f)
        {
            float baseMagic = baseline != null ? baseline.MagicAilmentApplyChance : MagicAilmentApplyChance;
            if (baseline == null || Mathf.Approximately(MagicAilmentApplyChance, baseMagic))
                block += $"{GetMagicAilmentName()} Chance: {MagicAilmentApplyChance * 100f:0.#}%\n";
        }

        AppendAilmentLineIfBaselineMatch(
            ref block,
            bonusStats.bleedChance,
            baseline?.bonusStats.bleedChance ?? 0f,
            baseline,
            $"Bleed Chance: {FormatSignedPercent01(bonusStats.bleedChance)}");
        AppendAilmentLineIfBaselineMatch(
            ref block,
            bonusStats.poisonChance,
            baseline?.bonusStats.poisonChance ?? 0f,
            baseline,
            $"Poison Chance: {FormatSignedPercent01(bonusStats.poisonChance)}");
        AppendAilmentLineIfBaselineMatch(
            ref block,
            bonusStats.burnChance,
            baseline?.bonusStats.burnChance ?? 0f,
            baseline,
            $"Burn Chance: {FormatSignedPercent01(bonusStats.burnChance)}");
        AppendAilmentLineIfBaselineMatch(
            ref block,
            bonusStats.bleedMultiplier,
            baseline?.bonusStats.bleedMultiplier ?? 0f,
            baseline,
            FormatAilmentMultiplierLine(bonusStats.bleedMultiplier, "Bleed Multi"));
        AppendAilmentLineIfBaselineMatch(
            ref block,
            bonusStats.poisonMultiplier,
            baseline?.bonusStats.poisonMultiplier ?? 0f,
            baseline,
            FormatAilmentMultiplierLine(bonusStats.poisonMultiplier, "Poison Multi"));

        if (bonusStats.poisonDurationBonus != 0f)
        {
            float baseDur = baseline?.bonusStats.poisonDurationBonus ?? 0f;
            if (baseline == null || Mathf.Approximately(bonusStats.poisonDurationBonus, baseDur))
                block += $"Poison Duration: {FormatSignedNumber(bonusStats.poisonDurationBonus)}s\n";
        }

        if (bonusStats.poisonMaxStacksBonus != 0)
        {
            int baseStacks = baseline?.bonusStats.poisonMaxStacksBonus ?? 0;
            if (baseline == null || bonusStats.poisonMaxStacksBonus == baseStacks)
                block += $"Poison Max Stacks: {FormatSignedInt(bonusStats.poisonMaxStacksBonus)}\n";
        }

        return block.TrimEnd('\n');
    }

    private (string matchingLines, string bonusLines) BuildUnifiedWeaponAilmentLines(ItemDefinition baseline)
    {
        var matching = new System.Text.StringBuilder();
        var bonus = new System.Text.StringBuilder();

        if (weaponStats.attackSkill == AttackSkill.Magic && MagicAilmentApplyChance > 0f)
        {
            AppendUnifiedWeaponAilmentStatLine(
                matching,
                bonus,
                MagicAilmentApplyChance,
                baseline.MagicAilmentApplyChance,
                v => $"{GetMagicAilmentName()} Chance: {v * 100f:0.#}%",
                d => $"{d * 100f:+0.#;-0.#;0}%");
        }

        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.bleedChance,
            baseline.bonusStats.bleedChance,
            v => $"Bleed Chance: {FormatSignedPercent01(v)}",
            FormatSignedPercent01);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.poisonChance,
            baseline.bonusStats.poisonChance,
            v => $"Poison Chance: {FormatSignedPercent01(v)}",
            FormatSignedPercent01);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.burnChance,
            baseline.bonusStats.burnChance,
            v => $"Burn Chance: {FormatSignedPercent01(v)}",
            FormatSignedPercent01);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.chillChance,
            baseline.bonusStats.chillChance,
            v => $"Chill Chance: {FormatSignedPercent01(v)}",
            FormatSignedPercent01);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.shockChance,
            baseline.bonusStats.shockChance,
            v => $"Shock Chance: {FormatSignedPercent01(v)}",
            FormatSignedPercent01);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.bleedMultiplier,
            baseline.bonusStats.bleedMultiplier,
            v => FormatAilmentMultiplierLine(v, "Bleed Multi"),
            DeltaPercentFractionNote);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.poisonMultiplier,
            baseline.bonusStats.poisonMultiplier,
            v => FormatAilmentMultiplierLine(v, "Poison Multi"),
            DeltaPercentFractionNote);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.burnExplosionMultiplierBonus,
            baseline.bonusStats.burnExplosionMultiplierBonus,
            v => FormatAilmentMultiplierLine(v, "Burn Multi"),
            DeltaPercentFractionNote);
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.poisonDurationBonus,
            baseline.bonusStats.poisonDurationBonus,
            v => $"Poison Duration: {FormatSignedNumber(v)}s",
            d => $"{FormatSignedNumber(d)}s");
        AppendUnifiedWeaponAilmentStatLine(
            matching,
            bonus,
            bonusStats.poisonMaxStacksBonus,
            baseline.bonusStats.poisonMaxStacksBonus,
            v => $"Poison Max Stacks: {FormatSignedInt(Mathf.RoundToInt(v))}",
            d => FormatSignedInt(Mathf.RoundToInt(d)));

        return (matching.ToString(), bonus.ToString());
    }

    private static void AppendUnifiedWeaponAilmentStatLine(
        System.Text.StringBuilder matching,
        System.Text.StringBuilder bonus,
        float current,
        float baselineValue,
        System.Func<float, string> formatLineAtValue,
        System.Func<float, string> formatDelta)
    {
        if (current == 0f && baselineValue == 0f)
            return;

        if (Mathf.Approximately(current, baselineValue))
        {
            if (matching.Length > 0)
                matching.Append('\n');
            matching.Append(formatLineAtValue(current));
            return;
        }

        if (bonus.Length > 0)
            bonus.Append('\n');
        bonus.Append(ItemTooltipStatHighlight.HighlightWithAddedNote(
            formatLineAtValue(baselineValue),
            formatDelta(current - baselineValue)));
    }

    private static void AppendAilmentLineIfBaselineMatch(
        ref string block,
        float current,
        float baselineValue,
        ItemDefinition baseline,
        string line)
    {
        if (current == 0f && baselineValue == 0f)
            return;

        if (baseline != null && !Mathf.Approximately(current, baselineValue))
            return;

        if (!string.IsNullOrEmpty(block))
            block += "\n";
        block += line;
    }

    private string GetMagicAilmentName()
    {
        return weaponStats.magicAttackType switch
        {
            MagicAttackType.Fire => "Burn",
            MagicAttackType.Ice => "Chill",
            MagicAttackType.Lightning => "Shock",
            _ => "Ailment"
        };
    }

    private static void AppendInlineListItem(ref string list, string item)
    {
        if (string.IsNullOrWhiteSpace(item))
            return;

        list = string.IsNullOrWhiteSpace(list) ? item : $"{list}, {item}";
    }

    private string BuildMiscTooltipLines()
    {
        if (EnemyRespawnTimeReductionSeconds <= 0f)
            return string.Empty;
        return $"Enemy respawn: -{EnemyRespawnTimeReductionSeconds:0.#}s";
    }

    public string GetFoodTimedBuffSummaryText()
    {
        if (!HasFoodTimedBuffs)
            return string.Empty;

        ConsumableStats cs = consumableStats;
        float dur = cs.foodEffectDurationSeconds;
        string header = $"Food buffs ({dur:0.#}s):";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(160);
        sb.Append(header);

        if (cs.foodEnableRegen && cs.foodRegenTotalHeal > 0)
            sb.Append($"\n• +{cs.foodRegenTotalHeal} HP over duration (regen)");

        if (cs.foodEnableSwiftness && cs.foodSwiftnessPercentBonus > 0f)
            sb.Append($"\n• +{cs.foodSwiftnessPercentBonus:0.#}% move speed");

        if (cs.foodEnableOverheal && cs.foodOverhealMaxAboveMaxHp > 0)
        {
            sb.Append($"\n• Overheal cap +{cs.foodOverhealMaxAboveMaxHp} above max HP");
            if (cs.foodOverhealInstantHeal > 0)
                sb.Append($" (+{cs.foodOverhealInstantHeal} on use)");
        }

        if (cs.foodEnableFocused)
        {
            float f = cs.foodFocusedDamageBonusFraction > 0f ? cs.foodFocusedDamageBonusFraction : 0.15f;
            sb.Append($"\n• Focused: +{f * 100f:0.#}% min/max hit");
        }

        return sb.ToString();
    }

    /// <summary>Pretty per-row "• Item Name (chance%) ×min-max" listing for Openable item tooltips.</summary>
    private string BuildOpenableLootTooltipLines()
    {
        OpenableLootEntry[] entries = OpenableLootEntries;
        if (entries.Length == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder(64);
        for (int i = 0; i < entries.Length; i++)
        {
            OpenableLootEntry e = entries[i];
            if (string.IsNullOrWhiteSpace(e.itemId))
                continue;

            float chance = Mathf.Clamp(e.chancePercent, 0f, 100f);
            int lo = Mathf.Max(1, e.minAmount);
            int hi = Mathf.Max(lo, e.maxAmount);

            string label = ItemGainPopupNotifier.ResolveDisplayLabel(e.itemId, hi);
            string amountSuffix = lo == hi ? (lo > 1 ? $" ×{lo}" : "") : $" ×{lo}-{hi}";
            string chanceText = chance >= 99.9999f ? "100%" : $"{chance:0.#}%";

            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append("• ").Append(label).Append(amountSuffix).Append(" (").Append(chanceText).Append(')');
        }

        return sb.ToString();
    }

    private static string FormatEnhancementScrollModifierFromOption(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
        {
            int slots = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(option.modifierValue)));
            return $"-{slots} Used Upgrade Slot{(slots == 1 ? "" : "s")}";
        }

        if (option.UsesWeightScalingEffective())
            return UpgradeScrollDisplay.FormatGenericScrollEffectLabel(option.targetStat);

        bool displayAsPercent = option.modifierKind == EnhancementScrollModifierKind.Percent ||
            IsPercentDisplayedScrollStat(option.targetStat);

        string value = displayAsPercent
            ? FormatSignedPercent01(option.modifierValue)
            : FormatSignedNumber(option.modifierValue);

        return $"{value} {GetEnhancementScrollTargetStatDisplayName(option.targetStat)}";
    }

    private static string FormatEnhancementFailureFromOption(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        string outcome = option.failureOutcome switch
        {
            EnhancementScrollFailureOutcome.Nothing => "Nothing",
            EnhancementScrollFailureOutcome.DestroyItem => "Item may be destroyed",
            EnhancementScrollFailureOutcome.DowngradeOrRemoveStat => "Downgrade/remove stat (future)",
            _ => option.failureOutcome.ToString()
        };

        if (option.failureOutcome == EnhancementScrollFailureOutcome.DestroyItem ||
            option.track == EnhancementTrack.Corruption)
        {
            outcome += $" ({Mathf.Clamp01(option.destroyChanceOnFailure) * 100f:0.#}% destroy chance)";
        }

        if (option.track == EnhancementTrack.Corruption)
            outcome += ", Cursed";

        return outcome;
    }

    private string FormatEnhancementScrollModifier()
    {
        EnhancementScrollStats stats = GetEffectiveEnhancementScrollStats();
        if (stats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
        {
            int slots = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(stats.modifierValue)));
            return $"-{slots} Used Upgrade Slot{(slots == 1 ? "" : "s")}";
        }

        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(this);
        if (option != null && option.UsesWeightScalingEffective())
            return UpgradeScrollDisplay.FormatGenericScrollEffectLabel(stats.targetStat);

        if (EnhancementWeightScalingRules.IsWeightScaledStat(stats.targetStat))
            return UpgradeScrollDisplay.FormatGenericScrollEffectLabel(stats.targetStat);

        bool displayAsPercent = stats.modifierKind == EnhancementScrollModifierKind.Percent ||
            IsPercentDisplayedScrollStat(stats.targetStat);

        string value = displayAsPercent
            ? FormatSignedPercent01(stats.modifierValue)
            : FormatSignedNumber(stats.modifierValue);

        return $"{value} {GetEnhancementScrollTargetStatDisplayName(stats.targetStat)}";
    }

    private static bool IsPercentDisplayedScrollStat(EnhancementScrollTargetStat stat)
    {
        switch (stat)
        {
            case EnhancementScrollTargetStat.GatheringGrit:
            case EnhancementScrollTargetStat.StaminaEfficiency:
            case EnhancementScrollTargetStat.EnergyEfficiency:
                return true;
            default:
                return false;
        }
    }

    public static string GetEnhancementScrollTargetStatDisplayName(EnhancementScrollTargetStat stat)
    {
        return stat switch
        {
            EnhancementScrollTargetStat.PhysicalDamage => "Physical Damage",
            EnhancementScrollTargetStat.MagicDamage => "Magic Damage",
            EnhancementScrollTargetStat.FireDamage => "Fire Damage",
            EnhancementScrollTargetStat.IceDamage => "Ice Damage",
            EnhancementScrollTargetStat.LightningDamage => "Lightning Damage",
            EnhancementScrollTargetStat.CorruptionDamage => "Corruption Damage",
            EnhancementScrollTargetStat.Health => "Health",
            EnhancementScrollTargetStat.Energy => "Energy",
            EnhancementScrollTargetStat.Mana => "Mana",
            EnhancementScrollTargetStat.Armor => "Armour",
            EnhancementScrollTargetStat.MagicResist => "Magic Res",
            EnhancementScrollTargetStat.CorruptionResist => "Corruption Res",
            EnhancementScrollTargetStat.CritChance => "Crit Chance",
            EnhancementScrollTargetStat.CritMultiplier => "Crit Multi",
            EnhancementScrollTargetStat.AttackSpeed => "Attack Speed",
            EnhancementScrollTargetStat.LifeSteal => "Life Steal",
            EnhancementScrollTargetStat.MoveSpeed => "Move Speed",
            EnhancementScrollTargetStat.GatherSpeed => "Gather Speed",
            EnhancementScrollTargetStat.GatheringGrit => "Gathering Grit",
            EnhancementScrollTargetStat.StaminaEfficiency => "Stamina Efficiency",
            EnhancementScrollTargetStat.UpgradeSlotReduction => "Used Upgrade Slot",
            EnhancementScrollTargetStat.PoisonChance => "Poison Chance",
            EnhancementScrollTargetStat.PoisonMultiplier => "Poison Multi",
            EnhancementScrollTargetStat.BurnChance => "Burn Chance",
            EnhancementScrollTargetStat.ChillChance => "Chill Chance",
            EnhancementScrollTargetStat.ShockChance => "Shock Chance",
            EnhancementScrollTargetStat.BurnMultiplier => "Burn Multi",
            EnhancementScrollTargetStat.EnergyEfficiency => "Energy Efficiency",
            EnhancementScrollTargetStat.FlatGuard => "Flat Guard",
            EnhancementScrollTargetStat.ManaRegen => "Mana Regen",
            _ => stat.ToString()
        };
    }

    private string FormatEnhancementFailure()
    {
        string outcome = enhancementScrollStats.failureOutcome switch
        {
            EnhancementScrollFailureOutcome.Nothing => "Nothing",
            EnhancementScrollFailureOutcome.DestroyItem => "Item may be destroyed",
            EnhancementScrollFailureOutcome.DowngradeOrRemoveStat => "Downgrade/remove stat (future)",
            _ => enhancementScrollStats.failureOutcome.ToString()
        };

        if (enhancementScrollStats.failureOutcome == EnhancementScrollFailureOutcome.DestroyItem ||
            enhancementScrollStats.cursed)
        {
            outcome += $" ({Mathf.Clamp01(enhancementScrollStats.destroyChanceOnFailure) * 100f:0.#}% destroy chance)";
        }

        if (enhancementScrollStats.cursed)
            outcome += ", Cursed";

        return outcome;
    }

    private static string FormatEnhancementGearMask(EnhancementScrollGearMask mask)
    {
        if (mask == EnhancementScrollGearMask.None)
            return "None";
        if ((mask & EnhancementScrollGearMask.AllGear) == EnhancementScrollGearMask.AllGear)
            return "All Gear";

        string s = "";
        if ((mask & EnhancementScrollGearMask.Weapon) == EnhancementScrollGearMask.Weapon)
        {
            AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.Weapon, "Any Weapon");
        }
        else if ((mask & EnhancementScrollGearMask.MeleeOrRangedWeapon) == EnhancementScrollGearMask.MeleeOrRangedWeapon)
        {
            AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.MeleeOrRangedWeapon, "Ranged or Melee Weapon");
        }
        else
        {
            AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.MeleeWeapon, "Melee Weapon");
            AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.RangedWeapon, "Ranged Weapon");
        }

        AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.MagicWeapon, "Magic Weapon");

        EnhancementScrollGearMask normalized = EnhancementScrollGearRules.NormalizeMask(mask);
        AppendMaskLabel(ref s, normalized, EnhancementScrollGearMask.Helmet, "Head");
        AppendMaskLabel(ref s, normalized, EnhancementScrollGearMask.Body, "Body");
        AppendMaskLabel(ref s, normalized, EnhancementScrollGearMask.Boots, "Feet");
        AppendMaskLabel(ref s, normalized, EnhancementScrollGearMask.OffHand, "Offhand");
        AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.Tool, "Tool");
        return s;
    }

    private static void AppendMaskLabel(ref string list, EnhancementScrollGearMask mask, EnhancementScrollGearMask flag, string label)
    {
        if ((mask & flag) == 0)
            return;

        list = string.IsNullOrWhiteSpace(list) ? label : $"{list}, {label}";
    }

    public string BuildTooltipStatsOneLine()
    {
        if (IsWeapon)
        {
            string s = "";

            if (HasPhysicalWeaponDamage)
                s += $"Phys {weaponStats.minPhysicalDamage}-{weaponStats.maxPhysicalDamage}  ";

            if (weaponStats.minFireDamage > 0 || weaponStats.maxFireDamage > 0)
                s += $"Fire {weaponStats.minFireDamage}-{weaponStats.maxFireDamage}  ";
            if (weaponStats.minIceDamage > 0 || weaponStats.maxIceDamage > 0)
                s += $"Ice {weaponStats.minIceDamage}-{weaponStats.maxIceDamage}  ";
            if (weaponStats.minLightningDamage > 0 || weaponStats.maxLightningDamage > 0)
                s += $"Lightning {weaponStats.minLightningDamage}-{weaponStats.maxLightningDamage}  ";

            if (HasCorruptionWeaponDamage)
                s += $"Corruption {weaponStats.minCorruptionDamage}-{weaponStats.maxCorruptionDamage}  ";

            s += $"{weaponStats.attackSkill}  {AttackRange:0.#} range";
            if (weaponStats.attackSkill == AttackSkill.Magic)
                s += $"  Mana {ManaCostPerAttack:0.##}";

            return s.Trim();
        }

        if (IsTool)
            return $"Tool • Speed {GatherSpeedMultiplier:0.##}x • Grit {GatheringGrit * 100f:0.#}% • Bonus +{BonusResourceFindChance * 100f:0.#}%";

        if (IsArmor || IsJewelry)
        {
            string s = "";

            if (ArmorValue != 0) s += $"Armour {ArmorValue} • ";
            if (MagicResist != 0) s += $"MRes {MagicResist} • ";
            if (CorruptionResist != 0) s += $"CRes {CorruptionResist} • ";
            if (BonusHealth != 0) s += $"HP +{BonusHealth} • ";
            if (BonusEnergy != 0) s += $"Energy +{BonusEnergy} • ";
            if (BonusMana != 0) s += $"Mana +{BonusMana} • ";
            if (ArmorFlatGuard > 0) s += $"Guard +{ArmorFlatGuard} • ";
            if (ArmorMaxGuardPercent > 0.00001f) s += $"Max Guard {FormatSignedPercent01(ArmorMaxGuardPercent)} • ";

            return s.TrimEnd(' ', '•');
        }

        if (IsConsumable)
        {
            string s = consumableStats.consumableType.ToString();

            if (HealAmount > 0)
                s += $" • Heal {HealAmount}";

            if (EnergyAmount > 0)
                s += $" • Energy +{EnergyAmount}";

            if (UseCooldown > 0f)
                s += $" • {UseCooldown:0.#}s CD";

            if (IsOpenable)
                s += $" • {OpenableLootEntries.Length} possible drop{(OpenableLootEntries.Length == 1 ? "" : "s")}";

            if (CanCook())
                s += " • Cookable";

            return s;
        }

        if (IsEnhancementScroll)
        {
            EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(this);
            if (option == null)
                return FormatEnhancementScrollModifier();

            if (option.track == EnhancementTrack.Corruption)
                return $"{EnhancementSuccessChanceRules.ChaosSuccessChance * 100f:0.#}% • {FormatEnhancementScrollModifierFromOption(option)}";

            if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
                return $"{Mathf.Clamp01(option.successChance) * 100f:0.#}% • {FormatEnhancementScrollModifierFromOption(option)}";

            return $"Varies • {FormatEnhancementScrollModifierFromOption(option)}";
        }

        if (CanCook())
            return "Cookable";

        return string.Empty;
    }

    public bool CanCook()
    {
        return IsCookable && !string.IsNullOrWhiteSpace(cookableStats.cookedResultItemId);
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize() => TryMigrateLegacyWeaponMagicDamage();

    void ISerializationCallbackReceiver.OnBeforeSerialize() { }

    void TryMigrateLegacyWeaponMagicDamage()
    {
        if (itemKind != ItemKind.Weapon)
            return;

        WeaponStats w = weaponStats;
        int elemMinSum = w.minFireDamage + w.minIceDamage + w.minLightningDamage;
        int elemMaxSum = w.maxFireDamage + w.maxIceDamage + w.maxLightningDamage;
        int legMin = w.legacyMinMagicDamage;
        int legMax = w.legacyMaxMagicDamage;

        if (elemMinSum == 0 && elemMaxSum == 0 && (legMin > 0 || legMax > 0))
        {
            int lo = Mathf.Max(0, legMin);
            int hi = Mathf.Max(lo, legMax);
            switch (w.magicAttackType)
            {
                case MagicAttackType.Fire:
                    w.minFireDamage = lo;
                    w.maxFireDamage = hi;
                    break;
                case MagicAttackType.Ice:
                    w.minIceDamage = lo;
                    w.maxIceDamage = hi;
                    break;
                case MagicAttackType.Lightning:
                    w.minLightningDamage = lo;
                    w.maxLightningDamage = hi;
                    break;
            }
        }

        w.legacyMinMagicDamage = 0;
        w.legacyMaxMagicDamage = 0;
        weaponStats = w;
    }
}