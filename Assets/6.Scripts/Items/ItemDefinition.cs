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
    CombatSupport
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

public enum MagicAttackType
{
    Lightning,
    Fire,
    Ice
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
    [Tooltip("Shown as Tier 1–3; gate uses Attack Skill (Melee/Ranged/Magic) at L1 / L20 / L40.")]
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
    [Tooltip("Shown as Tier 1–3; gate is Woodcutting / Mining / Fishing by tool type at L1 / L20 / L40. Default Tier1 (enum 0).")]
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

    [Tooltip("Ability Power for skills that scale from it.")]
    public float abilityPower;

    [Tooltip("Attack speed bonus (0.1 = +10% APS).")]
    public float attackSpeedPercent;

    [Header("Minions (owner scaling)")]
    [Tooltip("Extra damage for your minions / summons (0.1 = +10%). Generic; works with inherited or internal minion base damage.")]
    public float minionDamagePercent;

    [Tooltip("Extra minion attack speed (0.1 = +10% APS). Aggregate clamped so 1 + total ≥ 0.1.")]
    public float minionAttackSpeedPercent;

    [Tooltip("Added minion crit chance, 0–1 scale (0.1 = +10 percentage points). Minion crit damage is fixed ×1.5 (not from items).")]
    public float minionCritChance;

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
               lifeRegen != 0f || energyRegen != 0f || manaRegen != 0f || lifeSteal > 0f ||
               moveSpeedPercent != 0f ||
               physicalDamage != 0f || physicalDamagePercent != 0f ||
               globalPhysicalDamagePercent != 0f || rangedPhysicalDamagePercent != 0f ||
               magicDamage != 0f || magicDamagePercent != 0f ||
               fireSkillDamagePercent != 0f || iceSkillDamagePercent != 0f || lightningSkillDamagePercent != 0f ||
               corruptionDamagePercent != 0f ||
               corruptionDamage != 0f || abilityPower != 0f ||
               attackSpeedPercent != 0f ||
               minionDamagePercent != 0f || minionAttackSpeedPercent != 0f || minionCritChance != 0f ||
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
    Potion
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
}

[System.Serializable]
public struct CookableStats
{
    [Header("Cooking")]
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
    [Header("Classification")]
    public ItemKind itemKind = ItemKind.Resource;

    [Header("Stacking")]
    [Min(1)] public int maxStack = 99;

    [Header("Identity")]
    [Tooltip("Unique internal ID. Example: log, ore")]
    public string itemId = "new_item";

    [Header("Display")]
    public string displayName = "New Item";
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

    [Header("Cookable Stats")]
    public CookableStats cookableStats;

    public bool IsWeapon => itemKind == ItemKind.Weapon;
    public bool IsTool => itemKind == ItemKind.Tool;
    public bool IsArmor => itemKind == ItemKind.Armor;
    public bool IsJewelry => itemKind == ItemKind.Jewelry;
    public bool IsEquippable => IsWeapon || IsTool || IsArmor || IsJewelry || IsCombatSupport;

    public bool UsesEquipmentTierGating =>
        IsWeapon || (IsTool && toolStats.toolType != ToolType.None);

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

        return SkillType.Melee;
    }

    public EquipmentTierRank GetEquipmentTierRank()
    {
        if (IsWeapon) return weaponStats.equipmentTier;
        if (IsTool) return toolStats.equipmentTier;
        return EquipmentTierRank.Tier1;
    }

    public string GetEquipmentTierDisplayLabel()
    {
        if (!UsesEquipmentTierGating) return "";
        return EquipmentTierRules.GetTierDisplayLabel(GetEquipmentTierRank());
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
    public int BonusMana => bonusStats.bonusMana;

    public float LifeRegen => bonusStats.lifeRegen;
    public float EnergyRegen => bonusStats.energyRegen;
    public float ManaRegen => bonusStats.manaRegen;
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

    public string BuildTooltipStatsText()
    {
        if (IsWeapon)
        {
            float aps = weaponStats.attacksPerSecond > 0f ? weaponStats.attacksPerSecond : 1f;
            aps *= Mathf.Max(0.1f, 1f + bonusStats.attackSpeedPercent);

            string speed = $"{aps:0.##} atk/s";
            string skillType = weaponStats.attackSkill.ToString();
            string magicExtraLines = "";
            if (weaponStats.attackSkill == AttackSkill.Magic)
            {
                magicExtraLines = $"\nMana Cost: {ManaCostPerAttack:0.##}";
                if (MagicAilmentApplyChance > 0f)
                    magicExtraLines += $"\nAilment Chance: {MagicAilmentApplyChance * 100f:0.#}%";
            }

            float critChancePct = Mathf.Clamp01(weaponStats.critChance + bonusStats.critChanceBonus) * 100f;
            float critMultPct = Mathf.Max(0f, weaponStats.critMultiplier + bonusStats.critMultiplierBonus) * 100f;

            string hands = weaponStats.handedness == Handedness.TwoHanded ? "Two-handed" : "One-handed";
            string range = $"{AttackRange:0.##}";

            string dual = (weaponStats.handedness == Handedness.OneHanded && weaponStats.canEquipInOffHand)
                ? "\nDual Wield: Yes"
                : "";

            string extras = BuildBonusLines(includeDefense: false, omitBurnBonuses: true);

            string s = "";
            s += $"Tier: {GetEquipmentTierDisplayLabel()}\n" +
                 $"Requires: {GetEquipmentTierGateSkill()} Lv {EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank())}\n";

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

            string attackTypeBlock = $"Attack Type: {skillType}\n";
            if (weaponStats.attackSkill == AttackSkill.Magic)
                attackTypeBlock += $"Magic Type: {weaponStats.magicAttackType}\n";

            s +=
                attackTypeBlock +
                $"Speed: {speed}\n" +
                $"Crit Chance: {critChancePct:0.#}%\n" +
                $"Crit Multi: {critMultPct:0.#}%\n" +
                $"Range: {range}\n" +
                $"Hands: {hands}" +
                magicExtraLines +
                dual;

            if (!string.IsNullOrWhiteSpace(extras))
                s += "\n" + extras;

            string misc = BuildMiscTooltipLines();
            if (!string.IsNullOrWhiteSpace(misc))
                s += "\n" + misc;

            return s;
        }

        if (IsCombatSupport)
        {
            string s = $"Support Type: {SupportType}";

            if (SupportBonusPhysicalDamage != 0f) s += $"\nPhysical Damage: {FormatSignedNumber(SupportBonusPhysicalDamage)}";
            if (SupportBonusMagicDamage != 0f) s += $"\nMagic Damage: {FormatSignedNumber(SupportBonusMagicDamage)}";
            if (SupportBonusCorruptionDamage != 0f) s += $"\nCorruption Damage: {FormatSignedNumber(SupportBonusCorruptionDamage)}";
            float supAllPhys = SupportPhysicalDamagePercent + SupportGlobalPhysicalDamagePercent;
            if (supAllPhys != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(supAllPhys, "All physical")}";
            if (SupportRangedPhysicalDamagePercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportRangedPhysicalDamagePercent, "Ranged physical")}";
            if (SupportMagicDamagePercent != 0f)
                s += $"\n{FormatScalingCoefficientPercentLine(SupportMagicDamagePercent, "All magic")}";
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

            return s;
        }

        if (IsTool)
        {
            string type = toolStats.toolType.ToString();
            string extras = BuildBonusLines(includeDefense: true);

            string s = "";
            if (UsesEquipmentTierGating)
            {
                s += $"Tier: {GetEquipmentTierDisplayLabel()}\n" +
                     $"Requires: {GetEquipmentTierGateSkill()} Lv {EquipmentTierRules.GetRequiredSkillLevel(GetEquipmentTierRank())}\n";
            }

            s +=
                $"Tool: {type}\n" +
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
            if (PhysBlockChance > 0f) s += $"Phys Block: {PhysBlockChance * 100f:0.#}%\n";

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
            string s = $"Consumable: {consumableStats.consumableType}";

            if (HealAmount > 0)
                s += $"\nHeals: {HealAmount}";

            if (EnergyAmount > 0)
                s += $"\nEnergy: +{EnergyAmount}";

            if (UseCooldown > 0f)
                s += $"\nCooldown: {UseCooldown:0.##}s";

            if (HasGrantedEffect)
            {
                s += $"\nEffect: {ConsumableEffectTooltip.Format(GrantedEffect)}";
            }

            if (CanCook())
                s += "\nCookable: Yes";

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

    private string BuildBonusLines(bool includeDefense, bool omitBurnBonuses = false)
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
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.moveSpeedPercent, "Move Speed")}\n";
        if (bonusStats.physicalDamage != 0f) s += $"Physical Damage: {FormatSignedNumber(bonusStats.physicalDamage)}\n";
        float allPhysPct = bonusStats.physicalDamagePercent + bonusStats.globalPhysicalDamagePercent;
        if (allPhysPct != 0f)
            s += $"{FormatScalingCoefficientPercentLine(allPhysPct, "All physical")}\n";
        if (bonusStats.rangedPhysicalDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.rangedPhysicalDamagePercent, "Ranged physical")}\n";
        if (bonusStats.magicDamage != 0f) s += $"Magic Damage: {FormatSignedNumber(bonusStats.magicDamage)}\n";
        if (bonusStats.magicDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.magicDamagePercent, "All magic")}\n";
        if (bonusStats.fireSkillDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.fireSkillDamagePercent, "Fire skills")}\n";
        if (bonusStats.iceSkillDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.iceSkillDamagePercent, "Ice skills")}\n";
        if (bonusStats.lightningSkillDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.lightningSkillDamagePercent, "Lightning skills")}\n";
        if (bonusStats.corruptionDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.corruptionDamagePercent, "Corruption")}\n";
        if (bonusStats.corruptionDamage != 0f) s += $"Corruption Damage: {FormatSignedNumber(bonusStats.corruptionDamage)}\n";
        if (bonusStats.abilityPower != 0f) s += $"Ability Power: {FormatSignedNumber(bonusStats.abilityPower)}\n";
        if (bonusStats.lifeSteal != 0f) s += $"Life Steal: {FormatSignedPercent01(bonusStats.lifeSteal)}\n";

        if (bonusStats.attackSpeedPercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.attackSpeedPercent, "Attack Speed")}\n";
        if (bonusStats.minionDamagePercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.minionDamagePercent, "Minion Damage")}\n";
        if (bonusStats.minionAttackSpeedPercent != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.minionAttackSpeedPercent, "Minion Attack Speed")}\n";
        if (bonusStats.minionCritChance != 0f)
            s += $"Minion Crit Chance: {FormatSignedPercent01(bonusStats.minionCritChance)}\n";
        if (bonusStats.critChanceBonus != 0f) s += $"Crit Chance: {FormatSignedPercent01(bonusStats.critChanceBonus)}\n";
        if (bonusStats.critMultiplierBonus != 0f) s += $"Crit Multi: {FormatSignedPercent01(bonusStats.critMultiplierBonus)}\n";
        if (bonusStats.attackRangeBonus != 0f) s += $"Range: {FormatSignedNumber(bonusStats.attackRangeBonus)}\n";

        if (bonusStats.bleedChance != 0f) s += $"Bleed Chance: {FormatSignedPercent01(bonusStats.bleedChance)}\n";
        if (bonusStats.bleedMultiplier != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.bleedMultiplier, "Bleed Damage")}\n";

        if (bonusStats.poisonChance != 0f) s += $"Poison Chance: {FormatSignedPercent01(bonusStats.poisonChance)}\n";
        if (bonusStats.poisonMultiplier != 0f)
            s += $"{FormatScalingCoefficientPercentLine(bonusStats.poisonMultiplier, "Poison Damage")}\n";
        if (bonusStats.poisonDurationBonus != 0f) s += $"Poison Duration: {FormatSignedNumber(bonusStats.poisonDurationBonus)}s\n";
        if (bonusStats.poisonMaxStacksBonus != 0) s += $"Poison Max Stacks: {FormatSignedInt(bonusStats.poisonMaxStacksBonus)}\n";
        if (!omitBurnBonuses)
        {
            if (bonusStats.burnChance != 0f) s += $"Burn Chance: {FormatSignedPercent01(bonusStats.burnChance)}\n";
            if (bonusStats.burnExplosionMultiplierBonus != 0f)
                s += $"{FormatScalingCoefficientPercentLine(bonusStats.burnExplosionMultiplierBonus, "Burn tick mult (added to character base)")}\n";
        }
        if (bonusStats.chillSlowPerStackBonus != 0f) s += $"Chill Slow/Stack Bonus: {FormatSignedPercent01(bonusStats.chillSlowPerStackBonus)}\n";
        if (bonusStats.shockDamageTakenMultiplierBonus != 0f) s += $"Shock Amp Bonus: {FormatSignedPercent01(bonusStats.shockDamageTakenMultiplierBonus)}\n";

        return s.TrimEnd('\n');
    }

    private string BuildMiscTooltipLines()
    {
        if (EnemyRespawnTimeReductionSeconds <= 0f)
            return string.Empty;
        return $"Enemy respawn: -{EnemyRespawnTimeReductionSeconds:0.#}s";
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

            if (CanCook())
                s += " • Cookable";

            return s;
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