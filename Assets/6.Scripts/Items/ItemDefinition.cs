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
    Helmet,
    Body,
    Boots,

    // Accessories
    Trinket,
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

    [Tooltip("Adds to chill slow per stack. 0.02 means +2 percentage points (e.g. 15% -> 17%).")]
    public float chillSlowPerStackBonus;

    [Tooltip("Adds to shock damage taken multiplier. 0.05 means +5 percentage points (e.g. 15% -> 20%).")]
    public float shockDamageTakenMultiplierBonus;

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
               chillSlowPerStackBonus != 0f ||
               shockDamageTakenMultiplierBonus != 0f;
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
    Openable
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
    AllGear = Weapon | Armor | Tool
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

    [Header("Misc (unique effects)")]
    [Tooltip("Per-item hooks not covered by bonus stats (respawn modifiers, future procs, etc.).")]
    public ItemMiscEffects miscEffects;

    [Header("Consumable Stats (Only if ItemKind = Consumable)")]
    public ConsumableStats consumableStats;

    [Header("Enhancement Scroll Stats (Only if ItemKind = EnhancementScroll)")]
    public EnhancementScrollStats enhancementScrollStats;

    [Header("Cookable Stats")]
    public CookableStats cookableStats;

    public bool IsWeapon => itemKind == ItemKind.Weapon;
    public bool IsTool => itemKind == ItemKind.Tool;
    public bool IsArmor => itemKind == ItemKind.Armor;
    public bool IsJewelry => itemKind == ItemKind.Jewelry;
    public bool IsEnhancementScroll => itemKind == ItemKind.EnhancementScroll;
    public bool IsEquippable => IsWeapon || IsTool || IsArmor || IsJewelry || IsCombatSupport;

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
    public bool HasCorruptionWeaponDamage => IsWeapon && (weaponStats.minCorruptionDamage > 0 || weaponStats.maxCorruptionDamage > 0);

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

    public bool HasUpgradeSlots => IsWeapon || IsArmor || IsTool;

    public int MaxUpgradeSlots
    {
        get
        {
            if (IsWeapon || IsArmor)
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

    public float EnhancementScrollSuccessChance =>
        IsEnhancementScroll ? Mathf.Clamp01(enhancementScrollStats.successChance) : 0f;

    public bool EnhancementScrollCanTarget(ItemDefinition gear)
    {
        if (!IsEnhancementScroll || gear == null || !gear.HasUpgradeSlots)
            return false;

        EnhancementScrollGearMask mask = enhancementScrollStats.allowedGearTypes;
        if (mask == EnhancementScrollGearMask.None)
            return false;

        EnhancementScrollGearMask gearType = GetEnhancementGearMaskFor(gear);

        return gearType != EnhancementScrollGearMask.None && (mask & gearType) != 0;
    }

    private static EnhancementScrollGearMask GetEnhancementGearMaskFor(ItemDefinition gear)
    {
        if (gear == null)
            return EnhancementScrollGearMask.None;

        if (gear.itemKind == ItemKind.Weapon)
        {
            EnhancementScrollGearMask weaponMask = EnhancementScrollGearMask.Weapon;
            weaponMask |= gear.weaponStats.attackSkill switch
            {
                AttackSkill.Melee => EnhancementScrollGearMask.MeleeWeapon,
                AttackSkill.Ranged => EnhancementScrollGearMask.RangedWeapon,
                AttackSkill.Magic => EnhancementScrollGearMask.MagicWeapon,
                _ => EnhancementScrollGearMask.None
            };

            return weaponMask;
        }

        return gear.itemKind switch
        {
            ItemKind.Armor => EnhancementScrollGearMask.Armor,
            ItemKind.Tool => EnhancementScrollGearMask.Tool,
            _ => EnhancementScrollGearMask.None
        };
    }

    public bool CanUseEnhancementScrollOn(ItemDefinition gear)
    {
        if (!EnhancementScrollCanTarget(gear))
            return false;

        if (enhancementScrollStats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return gear.UsedUpgradeSlots > 0;

        if (!gear.HasBaseStatForEnhancementScroll(enhancementScrollStats.targetStat))
            return false;

        return gear.HasAvailableUpgradeSlot && !gear.HasReachedEnhancementCap;
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
                return Mathf.Abs(bonusStats.poisonMultiplier) > eps;

            case EnhancementScrollTargetStat.UpgradeSlotReduction:
                return true;

            default:
                return true;
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

    /// <summary>Tier, requirements, tool/weapon type labels — shown in the misc stats TMP.</summary>
    public string BuildTooltipMiscStatsText()
    {
        if (IsWeapon)
        {
            string hands = FormatHandednessLabel(weaponStats.handedness);
            string type = weaponStats.attackSkill == AttackSkill.Magic
                ? $"{weaponStats.attackSkill} ({weaponStats.magicAttackType})"
                : weaponStats.attackSkill.ToString();
            return FormatTooltipMetaLine("Tier", GetEquipmentTierNumberLabel()) + "\n" +
                   FormatTooltipMetaLine("Upgrade Slots", GetUpgradeSlotsTooltipValue()) + "\n" +
                   FormatTooltipMetaLine("Level Req", $"{GetEquipmentTierGateSkill()} lv {EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank())}") + "\n" +
                   FormatTooltipMetaLine("Type", type) + "\n" +
                   FormatTooltipMetaLine("Hands", hands);
        }

        if (IsCombatSupport)
            return $"Support Type: {SupportType}";

        if (IsTool)
        {
            string type = toolStats.toolType.ToString();
            string s = "";
            if (UsesEquipmentTierGating)
            {
                s += FormatTooltipMetaLine("Tier", GetEquipmentTierNumberLabel()) + "\n" +
                     FormatTooltipMetaLine("Upgrade Slots", GetUpgradeSlotsTooltipValue()) + "\n" +
                     FormatTooltipMetaLine("Level Req", $"{GetEquipmentTierGateSkill()} lv {EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank())}") + "\n";
            }
            else if (HasUpgradeSlots)
                s += FormatTooltipMetaLine("Upgrade Slots", GetUpgradeSlotsTooltipValue()) + "\n";

            s += FormatTooltipMetaLine("Tool", type);
            return s;
        }

        if (IsArmor)
        {
            return FormatTooltipMetaLine("Tier", GetEquipmentTierNumberLabel()) + "\n" +
                   FormatTooltipMetaLine("Upgrade Slots", GetUpgradeSlotsTooltipValue()) + "\n" +
                   FormatTooltipMetaLine("Level Req", $"{GetEquipmentTierGateSkill()} lv {EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank())}");
        }

        if (IsJewelry && HasUpgradeSlots)
            return GetUpgradeSlotsTooltipLine();

        return string.Empty;
    }

    /// <summary>Damage, speeds, resistances, gather rates — shown in the main stats TMP.</summary>
    public string BuildTooltipMainStatsText()
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

            s +=
                $"Speed: {speed}\n" +
                $"Crit Chance: {FormatSignedPercent100WithPlus(critChancePct)}\n" +
                $"Crit Multi: {FormatSignedPercent100WithPlus(critMultBonusPct)}\n" +
                $"{BuildWeaponAilmentsLine()}\n" +
                $"Range: {range}" +
                dual;

            if (weaponStats.attackSkill == AttackSkill.Magic)
                s += $"\nMana Cost: {ManaCostPerAttack:0.##}";

            if (RequiresOffhandSupport)
                s += $"\nRequires: {RequiredSupportType}";

            if (!string.IsNullOrWhiteSpace(extras))
                s += "\n" + extras;

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
            string s =
                $"Success Chance: {EnhancementScrollSuccessChance * 100f:0.#}%\n" +
                $"Effect: {FormatEnhancementScrollModifier()}\n" +
                $"Allowed Gear: {FormatEnhancementGearMask(enhancementScrollStats.allowedGearTypes)}\n";

            if (enhancementScrollStats.targetStat != EnhancementScrollTargetStat.UpgradeSlotReduction)
                s += $"\nRequires Target: {GetEnhancementScrollTargetStatDisplayName(enhancementScrollStats.targetStat)}";

            s += $"\nConsumes Slot On Use: {(enhancementScrollStats.consumeSlotOnFailure ? "Yes" : "No")}";

            if (enhancementScrollStats.failureOutcome != EnhancementScrollFailureOutcome.Nothing)
                s += $"\nFailure: {FormatEnhancementFailure()}";

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
        return $"{fraction * 100f:0.#}% {label}";
    }

    private static string FormatSignedPercent100WithPlus(float value)
    {
        return $"{value:+0.#;-0.#;0}%";
    }

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
        if (bonusStats.abilityPower != 0f) s += $"Ability Power %: {FormatSignedPercent100(bonusStats.abilityPower)}\n";
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
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.bleedMultiplier, "Bleed Damage")}\n";

        if (!omitAilmentChanceBonuses && bonusStats.poisonChance != 0f)
            s += $"Poison Chance: {FormatSignedPercent01(bonusStats.poisonChance)}\n";
        if (!omitAilmentMultiplierBonuses && bonusStats.poisonMultiplier != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.poisonMultiplier, "Poison Damage")}\n";
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

    private string BuildWeaponAilmentsLine()
    {
        // Weapon tooltips want explicit "Chance" / "Multi" wording and no "Ailments:" prefix.
        // Keep this block directly under crit lines (see BuildTooltipMainStatsText weapon path).
        string block = "";

        // Elemental ailment apply chance for magic weapons (Burn/Chill/Shock based on magic type).
        if (weaponStats.attackSkill == AttackSkill.Magic && MagicAilmentApplyChance > 0f)
            block += $"{GetMagicAilmentName()} Chance: {MagicAilmentApplyChance * 100f:0.#}%\n";

        // Physical/corruption ailment chances (bonuses).
        if (bonusStats.bleedChance != 0f)
            block += $"Bleed Chance: {FormatSignedPercent01(bonusStats.bleedChance)}\n";
        if (bonusStats.poisonChance != 0f)
            block += $"Poison Chance: {FormatSignedPercent01(bonusStats.poisonChance)}\n";
        if (bonusStats.burnChance != 0f)
            block += $"Burn Chance: {FormatSignedPercent01(bonusStats.burnChance)}\n";

        // Multipliers / scaling for weapons.
        if (bonusStats.bleedMultiplier != 0f)
            block += $"Bleed Multi: {FormatSignedPercent100WithPlus(bonusStats.bleedMultiplier * 100f)}\n";
        if (bonusStats.poisonMultiplier != 0f)
            block += $"Poison Multi: {FormatSignedPercent100WithPlus(bonusStats.poisonMultiplier * 100f)}\n";

        if (bonusStats.poisonDurationBonus != 0f)
            block += $"Poison Duration: {FormatSignedNumber(bonusStats.poisonDurationBonus)}s\n";
        if (bonusStats.poisonMaxStacksBonus != 0)
            block += $"Poison Max Stacks: {FormatSignedInt(bonusStats.poisonMaxStacksBonus)}\n";

        return block.TrimEnd('\n');
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

    private string FormatEnhancementScrollModifier()
    {
        if (enhancementScrollStats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
        {
            int slots = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(enhancementScrollStats.modifierValue)));
            return $"-{slots} Used Upgrade Slot{(slots == 1 ? "" : "s")}";
        }

        // Stats stored as 0..1 fractions read more naturally as percentages in the tooltip
        // (e.g. "+6% Stamina Efficiency" instead of "+0.06 Stamina Efficiency"). Gather Speed is
        // an additive multiplier modifier (e.g. +0.15x), so we leave it in flat form.
        bool displayAsPercent = enhancementScrollStats.modifierKind == EnhancementScrollModifierKind.Percent ||
            IsPercentDisplayedScrollStat(enhancementScrollStats.targetStat);

        string value = displayAsPercent
            ? FormatSignedPercent01(enhancementScrollStats.modifierValue)
            : FormatSignedNumber(enhancementScrollStats.modifierValue);

        return $"{value} {GetEnhancementScrollTargetStatDisplayName(enhancementScrollStats.targetStat)}";
    }

    private static bool IsPercentDisplayedScrollStat(EnhancementScrollTargetStat stat)
    {
        switch (stat)
        {
            case EnhancementScrollTargetStat.GatheringGrit:
            case EnhancementScrollTargetStat.StaminaEfficiency:
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
        AppendMaskLabel(ref s, mask, EnhancementScrollGearMask.Armor, "Armour");
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
            return $"{EnhancementScrollSuccessChance * 100f:0.#}% • {FormatEnhancementScrollModifier()}";

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