using UnityEngine;

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

    [Header("Magic Damage")]
    public int minMagicDamage;
    public int maxMagicDamage;

    [Header("True Damage")]
    public int minTrueDamage;
    public int maxTrueDamage;

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

    [Header("Dual Wield")]
    [Tooltip("If true, this weapon may be equipped in the OffHand slot as well.")]
    public bool canEquipInOffHand;

    [Header("Support Requirement")]
    public bool requiresOffhandSupport;
    public CombatSupportType requiredSupportType;

}

[System.Serializable]
public struct CombatSupportStats
{
    [Header("Type")]
    public CombatSupportType supportType;

    [Header("Bonuses")]
    public float bonusPhysicalDamage;
    public float bonusMagicDamage;
    public float bonusTrueDamage;

    [Range(0f, 1f)] public float critChanceBonus;
    public float critMultiplierBonus;
    public float attackSpeedPercent;

    [Header("Optional Charges/Consumption")]
    public bool consumableOnAttack;
    public int consumeAmountPerAttack;
}



[System.Serializable]
public struct ToolStats
{
    [Header("Tool Type")]
    public ToolType toolType;

    [Header("Gathering")]
    public int gatherPower;
    public float gatherSpeedMultiplier;
}

[System.Serializable]
public struct ArmorStats
{
    [Header("Defence")]
    public int armor;
    public int magicResist;

    [Header("Block")]
    [Range(0f, 1f)]
    public float physBlockChance;

    [Header("Vitals")]
    public int bonusHealth;
    public int bonusEnergy;
}

[System.Serializable]
public struct BonusStats
{
    [Header("Vitals")]
    public int bonusHealth;
    public int bonusEnergy;

    [Header("Defence")]
    public int armor;
    public int magicResist;
    [Range(0f, 1f)] public float physBlockChance;

    [Header("Sustain")]
    [Tooltip("HP per second")]
    public float lifeRegen;

    [Tooltip("Energy per second")]
    public float energyRegen;

    [Range(0f, 1f)]
    [Tooltip("0.05 = 5%")]
    public float lifeSteal;

    [Header("Mobility")]
    [Tooltip("0.10 = +10% move speed")]
    public float moveSpeedPercent;

    [Header("Offense")]
    [Tooltip("Flat bonus to physical/basic attack damage")]
    public float physicalDamage;

    [Tooltip("Flat bonus to magic/basic attack damage")]
    public float magicDamage;

    [Tooltip("Flat bonus to true/basic attack damage")]
    public float trueDamage;

    [Tooltip("Generic power for abilities/spells")]
    public float abilityPower;

    [Tooltip("0.10 = +10% attack speed (APS multiplier)")]
    public float attackSpeedPercent;

    [Tooltip("0.10 = +10% crit chance (additive)")]
    public float critChanceBonus;

    [Tooltip("+0.25 means +25% crit damage (additive multiplier value, not percent)")]
    public float critMultiplierBonus;

    [Tooltip("Extra attack range (additive)")]
    public float attackRangeBonus;

    [Header("Ailments")]
    [Range(0f, 1f)]
    [Tooltip("0.10 = 10% chance to apply bleed on hit")]
    public float bleedChance;

    [Tooltip("Bonus bleed damage. 0.00 = +0% (base 1x bleed), 1.00 = +100% (2x bleed).")]
    public float bleedMultiplier;

    [Range(0f, 1f)]
    [Tooltip("0.10 = 10% chance to apply poison on hit")]
    public float poisonChance;

    [Tooltip("Bonus poison damage. 0.00 = +0% (base 1x poison), 1.00 = +100% (2x poison).")]
    public float poisonMultiplier;

    [Tooltip("Bonus poison duration in seconds")]
    public float poisonDurationBonus;

    [Tooltip("Bonus maximum poison stacks")]
    public int poisonMaxStacksBonus;

    public bool HasAny()
    {
        return bonusHealth != 0 || bonusEnergy != 0 ||
               armor != 0 || magicResist != 0 || physBlockChance > 0f ||
               lifeRegen != 0f || energyRegen != 0f || lifeSteal > 0f ||
               moveSpeedPercent != 0f ||
               physicalDamage != 0f || magicDamage != 0f || trueDamage != 0f || abilityPower != 0f ||
               attackSpeedPercent != 0f ||
               critChanceBonus != 0f || critMultiplierBonus != 0f ||
               attackRangeBonus != 0f ||
               bleedChance > 0f || bleedMultiplier != 0f ||
               poisonChance > 0f || poisonMultiplier != 0f ||
               poisonDurationBonus != 0f || poisonMaxStacksBonus != 0;
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
    BleedImmunity
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

[CreateAssetMenu(menuName = "DesktopIdleGame/Item Definition", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject
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

    [Header("Consumable Stats (Only if ItemKind = Consumable)")]
    public ConsumableStats consumableStats;

    [Header("Cookable Stats")]
    public CookableStats cookableStats;

    public bool IsWeapon => itemKind == ItemKind.Weapon;
    public bool IsTool => itemKind == ItemKind.Tool;
    public bool IsArmor => itemKind == ItemKind.Armor;
    public bool IsJewelry => itemKind == ItemKind.Jewelry;
    public bool IsEquippable => IsWeapon || IsTool || IsArmor || IsJewelry || IsCombatSupport;

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

    public float SupportBonusTrueDamage =>
        IsCombatSupport ? combatSupportStats.bonusTrueDamage : 0f;

    public float SupportCritChanceBonus =>
        IsCombatSupport ? combatSupportStats.critChanceBonus : 0f;

    public float SupportCritMultiplierBonus =>
        IsCombatSupport ? combatSupportStats.critMultiplierBonus : 0f;

    public float SupportAttackSpeedPercent =>
        IsCombatSupport ? combatSupportStats.attackSpeedPercent : 0f;

    public bool SupportConsumableOnAttack =>
        IsCombatSupport && combatSupportStats.consumableOnAttack;

    public int SupportConsumeAmountPerAttack =>
        IsCombatSupport ? Mathf.Max(0, combatSupportStats.consumeAmountPerAttack) : 0;

    public bool IsTwoHandedWeapon => IsWeapon && weaponStats.handedness == Handedness.TwoHanded;
    public bool CanDualWieldOffHand => IsWeapon && weaponStats.canEquipInOffHand && weaponStats.handedness == Handedness.OneHanded;
    public bool IsMagicWeapon => IsWeapon && weaponStats.attackSkill == AttackSkill.Magic;

    public bool HasPhysicalWeaponDamage => IsWeapon && (weaponStats.minPhysicalDamage > 0 || weaponStats.maxPhysicalDamage > 0);
    public bool HasMagicWeaponDamage => IsWeapon && (weaponStats.minMagicDamage > 0 || weaponStats.maxMagicDamage > 0);
    public bool HasTrueWeaponDamage => IsWeapon && (weaponStats.minTrueDamage > 0 || weaponStats.maxTrueDamage > 0);

    public bool IsCombatSupport => itemKind == ItemKind.CombatSupport;

    public bool IsOffhandCombatSupport =>
        IsCombatSupport && equipSlot == EquipSlot.OffHand;

    public CombatSupportType SupportType =>
        IsCombatSupport ? combatSupportStats.supportType : CombatSupportType.None;

    public int ArmorValue => (IsArmor ? armorStats.armor : 0) + bonusStats.armor;
    public int MagicResist => (IsArmor ? armorStats.magicResist : 0) + bonusStats.magicResist;

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

    public float LifeRegen => bonusStats.lifeRegen;
    public float EnergyRegen => bonusStats.energyRegen;
    public float LifeSteal => Mathf.Clamp01(bonusStats.lifeSteal);
    public float MoveSpeedPercent => bonusStats.moveSpeedPercent;

    public float PhysicalDamage => bonusStats.physicalDamage;
    public float MagicDamage => bonusStats.magicDamage;
    public float TrueDamage => bonusStats.trueDamage;
    public float AbilityPower => bonusStats.abilityPower;

    public float BleedChance => Mathf.Clamp01(bonusStats.bleedChance);
    public float BleedMultiplier => Mathf.Max(0f, bonusStats.bleedMultiplier);

    public float PoisonChance => Mathf.Clamp01(bonusStats.poisonChance);
    public float PoisonMultiplier => Mathf.Max(0f, bonusStats.poisonMultiplier);
    public float PoisonDurationBonus => bonusStats.poisonDurationBonus;
    public int PoisonMaxStacksBonus => Mathf.Max(0, bonusStats.poisonMaxStacksBonus);

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

    public int RollPhysicalDamage()
    {
        if (!IsWeapon) return 0;
        return Random.Range(weaponStats.minPhysicalDamage, weaponStats.maxPhysicalDamage + 1);
    }

    public int RollMagicDamage()
    {
        if (!IsWeapon) return 0;
        return Random.Range(weaponStats.minMagicDamage, weaponStats.maxMagicDamage + 1);
    }

    public int RollTrueDamage()
    {
        if (!IsWeapon) return 0;
        return Random.Range(weaponStats.minTrueDamage, weaponStats.maxTrueDamage + 1);
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

    public int GatherPower => IsTool ? toolStats.gatherPower : 0;

    public float GatherSpeedMultiplier
    {
        get
        {
            if (!IsTool || toolStats.gatherSpeedMultiplier <= 0f)
                return 1f;

            return toolStats.gatherSpeedMultiplier;
        }
    }

    public string GetRarityLabel() => rarity.ToString();

    public string BuildTooltipStatsText()
    {
        if (IsWeapon)
        {
            float aps = weaponStats.attacksPerSecond > 0f ? weaponStats.attacksPerSecond : 1f;
            aps *= Mathf.Max(0.1f, 1f + bonusStats.attackSpeedPercent);

            string speed = $"{aps:0.##} atk/s";
            string skillType = weaponStats.attackSkill.ToString();
            string magicTypeLine = "";
            if (weaponStats.attackSkill == AttackSkill.Magic)
                magicTypeLine = $"\nMagic Type: {weaponStats.magicAttackType}";

            float critChancePct = Mathf.Clamp01(weaponStats.critChance + bonusStats.critChanceBonus) * 100f;
            float critMultPct = Mathf.Max(0f, weaponStats.critMultiplier + bonusStats.critMultiplierBonus) * 100f;

            string hands = weaponStats.handedness == Handedness.TwoHanded ? "Two-handed" : "One-handed";
            string range = $"{AttackRange:0.##}";

            string dual = (weaponStats.handedness == Handedness.OneHanded && weaponStats.canEquipInOffHand)
                ? "\nDual Wield: Yes"
                : "";

            string extras = BuildBonusLines(includeDefense: false);

            string s = "";

            if (HasPhysicalWeaponDamage)
                s += $"Physical Damage: {weaponStats.minPhysicalDamage}-{weaponStats.maxPhysicalDamage}\n";

            if (HasMagicWeaponDamage)
                s += $"Magic Damage: {weaponStats.minMagicDamage}-{weaponStats.maxMagicDamage}\n";

            if (HasTrueWeaponDamage)
                s += $"True Damage: {weaponStats.minTrueDamage}-{weaponStats.maxTrueDamage}\n";

            s +=
                $"Attack Type: {skillType}\n" +
                $"Speed: {speed}\n" +
                $"Crit Chance: {critChancePct:0.#}%\n" +
                $"Crit Multi: {critMultPct:0.#}%\n" +
                $"Range: {range}\n" +
                $"Hands: {hands}" +
                magicTypeLine +
                dual;

            if (RequiresOffhandSupport)
                s += $"\nRequires: {RequiredSupportType}";

            if (!string.IsNullOrWhiteSpace(extras))
                s += "\n" + extras;

            return s;
        }

        if (IsCombatSupport)
        {
            string s = $"Support Type: {SupportType}";

            if (SupportBonusPhysicalDamage != 0f) s += $"\nPhysical Damage: {FormatSignedNumber(SupportBonusPhysicalDamage)}";
            if (SupportBonusMagicDamage != 0f) s += $"\nMagic Damage: {FormatSignedNumber(SupportBonusMagicDamage)}";
            if (SupportBonusTrueDamage != 0f) s += $"\nTrue Damage: {FormatSignedNumber(SupportBonusTrueDamage)}";
            if (SupportCritChanceBonus != 0f) s += $"\nCrit Chance: {FormatSignedPercent01(SupportCritChanceBonus)}";
            if (SupportCritMultiplierBonus != 0f) s += $"\nCrit Multi: {FormatSignedPercent01(SupportCritMultiplierBonus)}";
            if (SupportAttackSpeedPercent != 0f) s += $"\nAttack Speed: {FormatSignedPercent01(SupportAttackSpeedPercent)}";

            if (SupportConsumableOnAttack)
                s += $"\nConsumes: {Mathf.Max(1, SupportConsumeAmountPerAttack)} per attack";

            return s;
        }

        if (IsTool)
        {
            string type = toolStats.toolType.ToString();
            string extras = BuildBonusLines(includeDefense: true);

            string s =
                $"Tool: {type}\n" +
                $"Gather Power: {GatherPower}\n" +
                $"Gather Speed: {GatherSpeedMultiplier:0.##}x";

            if (!string.IsNullOrWhiteSpace(extras))
                s += "\n" + extras;

            return s;
        }

        if (IsArmor || IsJewelry)
        {
            string s = "";

            if (ArmorValue != 0) s += $"Armour: {ArmorValue}\n";
            if (MagicResist != 0) s += $"Magic Res: {MagicResist}\n";
            if (BonusHealth != 0) s += $"Health: +{BonusHealth}\n";
            if (BonusEnergy != 0) s += $"Energy: +{BonusEnergy}\n";
            if (PhysBlockChance > 0f) s += $"Phys Block: {PhysBlockChance * 100f:0.#}%\n";

            string extras = BuildBonusLines(includeDefense: false);

            if (!string.IsNullOrWhiteSpace(extras))
                s += extras + "\n";

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
                s += $"\nEffect: {FormatConsumableEffectText(GrantedEffect)}";
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

    private static string FormatConsumableEffectText(ConsumableGrantedEffect effect)
    {
        string magPct = $"{effect.magnitude * 100f:0.#}%";
        string dur = $"{effect.duration:0.#}s";

        return effect.effectType switch
        {
            ConsumableEffectType.PhysicalDamageBoost => $"+{magPct} Physical Damage for {dur}",
            ConsumableEffectType.MagicDamageBoost => $"+{magPct} Magic Damage for {dur}",
            ConsumableEffectType.AttackSpeed => $"+{magPct} Attack Speed for {dur}",
            ConsumableEffectType.MoveSpeed => $"+{magPct} Move Speed for {dur}",
            ConsumableEffectType.DefenseBoost => $"+{magPct} Defence for {dur}",
            ConsumableEffectType.EnergyRegen => $"+{effect.magnitude:0.##} Energy Regen for {dur}",
            ConsumableEffectType.EnergyRestore => $"+{effect.magnitude:0.##} Energy",
            ConsumableEffectType.HealOverTime => $"+{effect.magnitude:0.##} HP over {dur}",
            _ => $"{effect.effectType} for {dur}"
        };
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

    private string BuildBonusLines(bool includeDefense)
    {
        string s = "";

        if (includeDefense)
        {
            if (bonusStats.armor != 0) s += $"Armour: {FormatSignedInt(bonusStats.armor)}\n";
            if (bonusStats.magicResist != 0) s += $"Magic Res: {FormatSignedInt(bonusStats.magicResist)}\n";
            if (bonusStats.physBlockChance != 0f) s += $"Phys Block: {FormatSignedPercent01(bonusStats.physBlockChance)}\n";
        }

        if (bonusStats.lifeRegen != 0f) s += $"Life Regen: {FormatSignedNumber(bonusStats.lifeRegen)}/s\n";
        if (bonusStats.energyRegen != 0f) s += $"Energy Regen: {FormatSignedNumber(bonusStats.energyRegen)}/s\n";
        if (bonusStats.moveSpeedPercent != 0f) s += $"Move Speed: {FormatSignedPercent01(bonusStats.moveSpeedPercent)}\n";
        if (bonusStats.physicalDamage != 0f) s += $"Physical Damage: {FormatSignedNumber(bonusStats.physicalDamage)}\n";
        if (bonusStats.magicDamage != 0f) s += $"Magic Damage: {FormatSignedNumber(bonusStats.magicDamage)}\n";
        if (bonusStats.trueDamage != 0f) s += $"True Damage: {FormatSignedNumber(bonusStats.trueDamage)}\n";
        if (bonusStats.abilityPower != 0f) s += $"Ability Power: {FormatSignedNumber(bonusStats.abilityPower)}\n";
        if (bonusStats.lifeSteal != 0f) s += $"Life Steal: {FormatSignedPercent01(bonusStats.lifeSteal)}\n";

        if (bonusStats.attackSpeedPercent != 0f) s += $"Attack Speed: {FormatSignedPercent01(bonusStats.attackSpeedPercent)}\n";
        if (bonusStats.critChanceBonus != 0f) s += $"Crit Chance: {FormatSignedPercent01(bonusStats.critChanceBonus)}\n";
        if (bonusStats.critMultiplierBonus != 0f) s += $"Crit Multi: {FormatSignedPercent01(bonusStats.critMultiplierBonus)}\n";
        if (bonusStats.attackRangeBonus != 0f) s += $"Range: {FormatSignedNumber(bonusStats.attackRangeBonus)}\n";

        if (bonusStats.bleedChance != 0f) s += $"Bleed Chance: {FormatSignedPercent01(bonusStats.bleedChance)}\n";
        if (bonusStats.bleedMultiplier != 0f) s += $"Bleed Bonus: {FormatSignedPercent01(bonusStats.bleedMultiplier)}\n";

        if (bonusStats.poisonChance != 0f) s += $"Poison Chance: {FormatSignedPercent01(bonusStats.poisonChance)}\n";
        if (bonusStats.poisonMultiplier != 0f) s += $"Poison Bonus: {FormatSignedPercent01(bonusStats.poisonMultiplier)}\n";
        if (bonusStats.poisonDurationBonus != 0f) s += $"Poison Duration: {FormatSignedNumber(bonusStats.poisonDurationBonus)}s\n";
        if (bonusStats.poisonMaxStacksBonus != 0) s += $"Poison Max Stacks: {FormatSignedInt(bonusStats.poisonMaxStacksBonus)}\n";

        return s.TrimEnd('\n');
    }

    public string BuildTooltipStatsOneLine()
    {
        if (IsWeapon)
        {
            string s = "";

            if (HasPhysicalWeaponDamage)
                s += $"Phys {weaponStats.minPhysicalDamage}-{weaponStats.maxPhysicalDamage}  ";

            if (HasMagicWeaponDamage)
                s += $"Magic {weaponStats.minMagicDamage}-{weaponStats.maxMagicDamage}  ";

            if (HasTrueWeaponDamage)
                s += $"True {weaponStats.minTrueDamage}-{weaponStats.maxTrueDamage}  ";

            s += $"{weaponStats.attackSkill}  {AttackRange:0.#} range";

            return s.Trim();
        }

        if (IsTool)
            return $"Tool • Power {GatherPower} • Speed {GatherSpeedMultiplier:0.##}x";

        if (IsArmor || IsJewelry)
        {
            string s = "";

            if (ArmorValue != 0) s += $"Armour {ArmorValue} • ";
            if (MagicResist != 0) s += $"MRes {MagicResist} • ";
            if (BonusHealth != 0) s += $"HP +{BonusHealth} • ";
            if (BonusEnergy != 0) s += $"Energy +{BonusEnergy} • ";

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
}