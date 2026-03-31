using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SplitDamage
{
    public float physical;
    public float magical;
    public float trueDamage;

    public SplitDamage(float physical, float magical, float trueDamage)
    {
        this.physical = physical;
        this.magical = magical;
        this.trueDamage = trueDamage;
    }

    public float Total => physical + magical + trueDamage;

    public bool IsEmpty => physical <= 0f && magical <= 0f && trueDamage <= 0f;

    public static SplitDamage Zero => new SplitDamage(0f, 0f, 0f);

    public static SplitDamage operator +(SplitDamage a, SplitDamage b)
    {
        return new SplitDamage(
            a.physical + b.physical,
            a.magical + b.magical,
            a.trueDamage + b.trueDamage
        );
    }

    public static SplitDamage operator *(SplitDamage a, float mult)
    {
        return new SplitDamage(
            a.physical * mult,
            a.magical * mult,
            a.trueDamage * mult
        );
    }
}

[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour, ISaveable
{

    [SerializeField] private PlayerBuffController buffController;

    [Header("Vitals")]
    [SerializeField] private string unitDisplayName = "Adventurer";
    [SerializeField, HideInInspector] private float currentHP = -1f;
    [SerializeField, HideInInspector] private float currentEnergy = -1f;
    [SerializeField, HideInInspector] private float currentMana = -1f;

    private bool _isDead;
    private bool _didInitialFill;
    private bool _hasPendingLoadedVitals;
    private float _pendingLoadedHP = -1f;
    private float _pendingLoadedEnergy = -1f;
    private float _pendingLoadedMana = -1f;

    public event Action<float, float> OnHPChanged;
    public event Action<float, float> OnEnergyChanged;
    public event Action<float, float> OnManaChanged;
    public event Action<string> OnNameChanged;
    public event Action OnDied;

    public event Action OnStatsChanged;

    private PlayerController _ownerPlayer;


    public string UnitDisplayName => unitDisplayName;
    public float HP => currentHP;
    public float Energy => currentEnergy;
    public float Mana => currentMana;
    public bool IsDead => _isDead;

    [Header("Base Stats")]
    [SerializeField] private int baseMaxHP = 100;
    [SerializeField] private int baseMaxEnergy = 100;
    [SerializeField] private int baseMaxMana = 50;
    [SerializeField] private int baseArmor = 0;
    [SerializeField] private int baseMagicResist = 0;
    [SerializeField, Range(0f, 1f)] private float basePhysBlockChance = 0f;

    [Header("Base Utility")]
    [SerializeField] private float baseMoveSpeed = 3f;
    [SerializeField] private float baseMoveSpeedMult = 1f;
    public float BaseMoveSpeed => baseMoveSpeed;

    [SerializeField] private float baseLifeRegen = 1f;
    [SerializeField] private float baseEnergyRegen = 10f;
    [SerializeField] private float baseManaRegen = 1f;

    [Header("Base Offense")]
    [SerializeField] private float baseMinPhysicalDamage = 0f;
    [SerializeField] private float baseMaxPhysicalDamage = 0f;

    [SerializeField] private float baseMinMagicDamage = 0f;
    [SerializeField] private float baseMaxMagicDamage = 0f;

    [SerializeField] private float baseMinTrueDamage = 0f;
    [SerializeField] private float baseMaxTrueDamage = 0f;

    [SerializeField] private float baseAbilityPower = 0f;
    [SerializeField, Range(0f, 1f)] private float baseLifeSteal = 0f;

    [Header("Base Ailments")]
    [SerializeField, Range(0f, 1f)] private float baseBleedChance = 0f;
    [SerializeField] private float baseBleedMultiplier = 0f;
    [SerializeField] private float baseBleedDuration = 5f;

    [SerializeField, Range(0f, 1f)] private float basePoisonChance = 0f;
    [SerializeField] private float basePoisonMultiplier = 0f;
    [SerializeField] private float basePoisonDuration = 5f;
    [SerializeField] private int basePoisonMaxStacks = 3;

    [Header("Base Elemental Ailments")]
    [SerializeField] private MagicAttackType baseMagicAttackType = MagicAttackType.Lightning;
    [SerializeField, Range(0f, 1f)] private float baseMagicAilmentApplyChance = 0f;
    [SerializeField] private float baseChillDuration = 5f;
    [SerializeField] private int baseChillMaxStacks = 6;
    [SerializeField, Range(0f, 1f)] private float baseChillSlowPerStack = 0.15f;
    [SerializeField] private int baseBurnHitsToExplode = 4;
    [SerializeField] private float baseBurnExplosionMultiplier = 0.5f;
    [SerializeField] private float baseShockDuration = 5f;
    [SerializeField, Range(0f, 1f)] private float baseShockDamageTakenMultiplier = 0.15f;

    [Header("Combat Tuning")]
    [SerializeField] private float dualWieldApsBonus = 1.15f;
    [SerializeField] private bool trueDamageCanCrit = false;

    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ToolbeltManager toolbelt;

    [Header("Attack Profile (Unarmed / Enemy)")]
    [SerializeField] private int unarmedMinPhysicalDamage = 1;
    [SerializeField] private int unarmedMaxPhysicalDamage = 2;

    [SerializeField] private float unarmedAttacksPerSecond = 1.0f;
    [SerializeField] private float unarmedRange = 1.0f;

    [SerializeField, Range(0f, 1f)] private float unarmedCritChance = 0f;
    [SerializeField] private float unarmedCritMultiplier = 1.5f;

    [Header("Gathering Bonuses (from skills later)")]
    [SerializeField] private float bonusAxeSpeedMult = 0f;
    [SerializeField] private float bonusPickaxeSpeedMult = 0f;
    [SerializeField] private float bonusRodSpeedMult = 0f;

    // Global combat-power tuning (shared across all characters and enemies).
    // Non-serialized by design to avoid per-instance drift in the inspector.
    private const float combatPowerDefenseScale = 0.10f;
    private const float combatPowerSustainScale = 3.0f;
    private const float combatPowerMoveSpeedScale = 1.5f;

    private const float combatPowerPhysicalWeight = 0.4f;
    private const float combatPowerMagicalWeight = 0.4f;
    private const float combatPowerTrueWeight = 0.2f;

    private const float combatPowerPhysicalOffenseWeight = 1.0f;
    private const float combatPowerMagicalOffenseWeight = 1.05f;
    private const float combatPowerTrueOffenseWeight = 1.3f;

    private const float combatPowerBleedWeight = 1.0f;
    private const float combatPowerPoisonWeight = 1.25f;
    private const float combatPowerBurnWeight = 1.0f;

    private void Start()
    {
        InitializeVitals();
    }

    private void InitializeVitals()
    {
        RefreshVitalsFromStats(fillIfEmpty: true);
        OnNameChanged?.Invoke(unitDisplayName);
    }

    private void Awake()
    {
        if (!equipment) equipment = GetComponent<EquipmentManager>();
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!inventory && equipment) inventory = equipment.Inventory;
        if (!toolbelt) toolbelt = GetComponent<ToolbeltManager>();
        if (!buffController) buffController = GetComponent<PlayerBuffController>();
        _ownerPlayer = GetComponent<PlayerController>();
    }

    // -------------------------
    // Public computed stats
    // -------------------------

    // Defensive
    public int MaxHP => baseMaxHP + GetEquippedBonusHealth();
    public int MaxEnergy => baseMaxEnergy + GetEquippedBonusEnergy();
    public int MaxMana => Mathf.Max(0, baseMaxMana + GetEquippedBonusMana());
    public int Armor => baseArmor + GetEquippedArmor();
    public int MagicResist => baseMagicResist + GetEquippedMagicResist();

    public float PhysBlockChance => Mathf.Clamp01(basePhysBlockChance + GetEquippedPhysBlockChance());
    public float PhysBlockChancePercent => PhysBlockChance * 100f;

    // Utility / sustain
    public float GearMoveSpeedPercent => GetEquippedMoveSpeedPercent();

    // Future-ready: temporary slows / buffs from ailments, skills, etc.
    public float TemporaryMoveSpeedPercent => 0f;

    public float TotalMoveSpeedPercent => GearMoveSpeedPercent + TemporaryMoveSpeedPercent;

    public float MoveSpeedMultiplier => Mathf.Max(0.1f, baseMoveSpeedMult * (1f + TotalMoveSpeedPercent));
    public float FinalMoveSpeed => BaseMoveSpeed * MoveSpeedMultiplier;

    // Displayed bonus/penalty relative to normal base speed
    public float MoveSpeedBonusPercent => MoveSpeedMultiplier - 1f;

    public float LifeRegenPerSecond => Mathf.Max(0f, baseLifeRegen + GetEquippedLifeRegen());
    public float EnergyRegenPerSecond => Mathf.Max(0f, baseEnergyRegen + GetEquippedEnergyRegen());
    public float ManaRegenPerSecond => Mathf.Max(0f, baseManaRegen + GetEquippedManaRegen());
    public float LifeSteal => Mathf.Clamp01(baseLifeSteal + GetEquippedLifeSteal());

    // Offensive stats
    public float BaseMinPhysicalDamage => Mathf.Max(0f, baseMinPhysicalDamage);
    public float BaseMaxPhysicalDamage => Mathf.Max(BaseMinPhysicalDamage, baseMaxPhysicalDamage);

    public float BaseMinMagicDamage => Mathf.Max(0f, baseMinMagicDamage);
    public float BaseMaxMagicDamage => Mathf.Max(BaseMinMagicDamage, baseMaxMagicDamage);

    public float BaseMinTrueDamage => Mathf.Max(0f, baseMinTrueDamage);
    public float BaseMaxTrueDamage => Mathf.Max(BaseMinTrueDamage, baseMaxTrueDamage);
    public float AbilityPower => Mathf.Max(0f, baseAbilityPower + GetEquippedAbilityPower());

    // Ailments
    public float BleedChance => Mathf.Clamp01(baseBleedChance + GetEquippedBleedChance());
    public float BleedMultiplier => Mathf.Max(0f, baseBleedMultiplier + GetEquippedBleedMultiplier());

    public float BleedBaseDuration => Mathf.Max(1f, baseBleedDuration);
    public float BleedDuration => Mathf.Max(1f, baseBleedDuration + GetEquippedBleedDurationBonus());

    public float PoisonChance => Mathf.Clamp01(basePoisonChance + GetEquippedPoisonChance());
    public float PoisonMultiplier => Mathf.Max(0f, basePoisonMultiplier + GetEquippedPoisonMultiplier());
    public float PoisonDuration => Mathf.Max(0.1f, basePoisonDuration + GetEquippedPoisonDurationBonus());
    public int PoisonMaxStacks => Mathf.Max(1, basePoisonMaxStacks + GetEquippedPoisonMaxStacksBonus());

    public float BleedChancePercent => BleedChance * 100f;
    public float PoisonChancePercent => PoisonChance * 100f;

    public MagicAttackType CurrentMagicAttackType => GetCurrentMagicAttackType();
    public float MagicAilmentApplyChance => Mathf.Clamp01(baseMagicAilmentApplyChance + GetEquippedMagicAilmentApplyChance());
    public float ChillDuration => Mathf.Max(0.1f, baseChillDuration);
    public int ChillMaxStacks => Mathf.Max(1, baseChillMaxStacks);
    public float ChillSlowPerStack => Mathf.Clamp01(baseChillSlowPerStack + GetEquippedChillSlowPerStackBonus());
    public int BurnHitsToExplode => Mathf.Max(2, baseBurnHitsToExplode);
    public float BurnExplosionMultiplier => Mathf.Max(0f, baseBurnExplosionMultiplier + GetEquippedBurnExplosionMultiplierBonus());
    public float ShockDuration => Mathf.Max(0.1f, baseShockDuration);
    public float ShockDamageTakenMultiplier => Mathf.Clamp01(baseShockDamageTakenMultiplier + GetEquippedShockDamageTakenMultiplierBonus());

    public float PhysicalReductionFromArmorPercent
    {
        get
        {
            float a = Mathf.Max(0f, Armor);
            float multiplier = 100f / (100f + a);
            return (1f - multiplier) * 100f;
        }
    }

    public float MagicalReductionFromMrPercent
    {
        get
        {
            float mr = Mathf.Max(0f, MagicResist);
            float multiplier = 100f / (100f + mr);
            return (1f - multiplier) * 100f;
        }
    }

    // Offensive (display)
    public int MinDamage => Mathf.RoundToInt(MinSplitDamage.Total);
    public int MaxDamage => Mathf.RoundToInt(MaxSplitDamage.Total);

    public SplitDamage MinSplitDamage => GetMinSplitDamage();
    public SplitDamage MaxSplitDamage => GetMaxSplitDamage();

    public float AttacksPerSecond => GetAttacksPerSecond();
    public float Range => GetAttackRange();

    public float CritChance => GetCritChance();
    public float CritMultiplier => GetCritMultiplier();

    public float CritChancePercent => CritChance * 100f;
    public float CritMultiplierPercent => CritMultiplier * 100f;

    public AttackSkill CurrentAttackSkill => GetCurrentAttackSkill();
    public DamageType CurrentDamageType => GetLegacyCurrentDamageType();

    public float BaseDPS => GetBaseDps();
    public float DPS => GetTrueDps();
    public float WeaponDpsComponent => GetCombatModelDirectDps();
    public float AilmentDpsComponent => GetCombatModelAilmentDps();

    public bool CanUseEquippedWeapon
    {
        get
        {
            var mh = GetMainHandWeaponDef();
            if (!mh) return true;
            if (!mh.RequiresOffhandSupport) return true;
            return HasRequiredOffHandSupport();
        }
    }

    // Ailment (Expected DPS / UI)
    public float AveragePhysicalHit => (MinSplitDamage.physical + MaxSplitDamage.physical) * 0.5f;
    public float AverageMagicalHit => (MinSplitDamage.magical + MaxSplitDamage.magical) * 0.5f;
    public float AverageTrueHit => (MinSplitDamage.trueDamage + MaxSplitDamage.trueDamage) * 0.5f;

    public float ExpectedCritFactor
    {
        get
        {
            float cc = Mathf.Clamp01(CritChance);
            float cm = Mathf.Max(1f, CritMultiplier);
            return 1f + cc * (cm - 1f);
        }
    }

    public float ExpectedPhysicalHit => AveragePhysicalHit * ExpectedCritFactor;
    public float ExpectedMagicalHit => AverageMagicalHit * ExpectedCritFactor;
    public float ExpectedTrueHit => trueDamageCanCrit
        ? AverageTrueHit * ExpectedCritFactor
        : AverageTrueHit;

    // ---------- Bleed ----------
    public float BleedBaseTotalDamage => ExpectedPhysicalHit * (1f + BleedMultiplier);

    public int BleedTicks => Mathf.Max(1, Mathf.RoundToInt(BleedDuration));

    public float BleedTickDamage
    {
        get
        {
            return Mathf.Max(0f, BleedBaseTotalDamage / BleedTicks);
        }
    }

    public float BleedDPS => BleedTickDamage;

    public float ExpectedBleedUptime
    {
        get
        {
            if (BleedChance <= 0f || AttacksPerSecond <= 0f || BleedDuration <= 0f)
                return 0f;

            float attemptsInWindow = AttacksPerSecond * BleedDuration;
            float uptime = 1f - Mathf.Pow(1f - BleedChance, attemptsInWindow);
            return Mathf.Clamp01(uptime);
        }
    }

    public float ExpectedBleedDPS => BleedDPS * ExpectedBleedUptime;

    public float BleedTotalDamageAtCurrentDuration => BleedBaseTotalDamage;

    // ---------- Poison ----------
    // Stats-panel display model:
    // - uses average true hit
    // - includes poison multiplier
    // - assumes poison can ramp to full stacks
    // - PoisonMaxDPS is the sustained DPS at max stacks

    public float PoisonPerStackTotalDamage
    {
        get
        {
            if (PoisonChance <= 0f) return 0f;
            if (PoisonDuration <= 0f) return 0f;
            if (AverageTrueHit <= 0f) return 0f;

            return AverageTrueHit * (1f + PoisonMultiplier);
        }
    }

    public int PoisonTicks => Mathf.Max(1, Mathf.RoundToInt(PoisonDuration));

    public float PoisonPerStackTickDamage
    {
        get
        {
            if (PoisonPerStackTotalDamage <= 0f) return 0f;
            int ticks = Mathf.Max(1, PoisonTicks);
            // Match runtime poison application:
            // tickDamage = max(1, ceil(totalDamage / ticks))
            return Mathf.Max(1f, Mathf.Ceil(PoisonPerStackTotalDamage / ticks));
        }
    }

    public float PoisonPerStackDPS
    {
        get
        {
            // Runtime poison ticks once per second, so DPS is tick damage.
            return PoisonPerStackTickDamage;
        }
    }

    public float PoisonMaxDPS => PoisonPerStackDPS * PoisonMaxStacks;

    public float ExpectedPoisonStacks
    {
        get
        {
            if (PoisonChance <= 0f || AttacksPerSecond <= 0f || PoisonTicks <= 0)
                return 0f;

            // Runtime stack life is `PoisonTicks` seconds (one tick per second).
            float expectedApplicationsInWindow = AttacksPerSecond * PoisonChance * PoisonTicks;
            return Mathf.Clamp(expectedApplicationsInWindow, 0f, PoisonMaxStacks);
        }
    }

    public float ExpectedPoisonDPS => PoisonPerStackDPS * ExpectedPoisonStacks;
    public float ExpectedBurnDPS => GetExpectedBurnDps();
    public float ExpectedAilmentDPS => ExpectedBleedDPS + ExpectedPoisonDPS + ExpectedBurnDPS;

    private float GetExpectedBurnDps()
    {
        if (CurrentAttackSkill != AttackSkill.Magic)
            return 0f;
        if (CurrentMagicAttackType != MagicAttackType.Fire)
            return 0f;
        float p = Mathf.Clamp01(MagicAilmentApplyChance);
        if (p <= 0f)
            return 0f;
        if (AttacksPerSecond <= 0f)
            return 0f;
        if (ExpectedMagicalHit <= 0f)
            return 0f;

        // Burn model (current implementation):
        // - burn has a START chance (p) on a Fire hit
        // - once started, it consumes the next N Fire hits (no further chance rolls)
        // - explosion damage = explosionMultiplier * accumulatedDamage
        // - accumulatedDamage includes the start hit + the N follow-up fire hits
        //
        // Expected hits per explosion cycle:
        // - expected hits until start = 1/p
        // - plus N follow-up hits to detonate
        //
        // Expected explosion damage: (N + 1) * ExpectedMagicalHit * explosionMultiplier
        // Expected time per hit: 1 / APS
        // ExpectedBurnDPS =
        //   explosionDamage / ((1/p + N) / APS)
        // = APS * explosionDamage / (N + 1/p)
        int n = Mathf.Max(1, BurnHitsToExplode);
        float explosionDamage = (n + 1) * ExpectedMagicalHit * BurnExplosionMultiplier;
        float expectedHitsPerExplosion = n + (1f / p);
        return AttacksPerSecond * explosionDamage / Mathf.Max(0.0001f, expectedHitsPerExplosion);
    }

    // Tools
    public float AxeSpeedMult => GetToolSpeedMult(ToolType.Axe) * (1f + Mathf.Max(0f, bonusAxeSpeedMult));
    public float PickaxeSpeedMult => GetToolSpeedMult(ToolType.Pickaxe) * (1f + Mathf.Max(0f, bonusPickaxeSpeedMult));
    public float RodSpeedMult => GetToolSpeedMult(ToolType.FishingRod) * (1f + Mathf.Max(0f, bonusRodSpeedMult));
    public float AxeGrit => GetToolGrit(ToolType.Axe);
    public float PickaxeGrit => GetToolGrit(ToolType.Pickaxe);
    public float RodGrit => GetToolGrit(ToolType.FishingRod);
    public float AxeBonusFindChance => GetToolBonusFindChance(ToolType.Axe);
    public float PickaxeBonusFindChance => GetToolBonusFindChance(ToolType.Pickaxe);
    public float RodBonusFindChance => GetToolBonusFindChance(ToolType.FishingRod);
    public float AxeStaminaEfficiency => GetToolStaminaEfficiency(ToolType.Axe);
    public float PickaxeStaminaEfficiency => GetToolStaminaEfficiency(ToolType.Pickaxe);
    public float RodStaminaEfficiency => GetToolStaminaEfficiency(ToolType.FishingRod);



    // -------------------------
    // Combat Power
    // -------------------------

    public float EffectiveHPVsPhysical
    {
        get
        {
            float damageTakenMultiplier = 100f / (100f + Mathf.Max(0f, Armor));

            // Physical block is chance to take 0 damage, so expected damage taken is reduced by (1 - blockChance)
            damageTakenMultiplier *= Mathf.Max(0.05f, 1f - PhysBlockChance);

            return MaxHP / Mathf.Max(0.01f, damageTakenMultiplier);
        }
    }

    public float EffectiveHPVsMagical
    {
        get
        {
            float damageTakenMultiplier = 100f / (100f + Mathf.Max(0f, MagicResist));
            return MaxHP / Mathf.Max(0.01f, damageTakenMultiplier);
        }
    }

    public float EffectiveHPVsTrue => MaxHP;

    public float WeightedEffectiveHP
    {
        get
        {
            float totalWeight =
                combatPowerPhysicalWeight +
                combatPowerMagicalWeight +
                combatPowerTrueWeight;

            if (totalWeight <= 0f)
                return MaxHP;

            return
                (EffectiveHPVsPhysical * combatPowerPhysicalWeight +
                 EffectiveHPVsMagical * combatPowerMagicalWeight +
                 EffectiveHPVsTrue * combatPowerTrueWeight)
                / totalWeight;
        }
    }

    public float ExpectedLifeStealPerSecond
    {
        get
        {
            // Assumes LifeSteal is a % of dealt damage returned as healing.
            // Since your DPS already includes expected ailment DPS, this gives a clean sustain estimate.
            return DPS * Mathf.Clamp01(LifeSteal);
        }
    }

    public float ExpectedSustainPerSecond
    {
        get
        {
            // Energy regen matters less than HP sustain, so give it a smaller contribution.
            return LifeRegenPerSecond
                   + ExpectedLifeStealPerSecond
                   + (EnergyRegenPerSecond * 0.25f);
        }
    }

    public float CombatPower
    {
        get
        {
            float offense = GetCombatPowerOffenseFromCombatModel();

            float defense = WeightedEffectiveHP * combatPowerDefenseScale;

            float sustain = ExpectedSustainPerSecond * combatPowerSustainScale;

            float mobility = FinalMoveSpeed * combatPowerMoveSpeedScale;

            return offense + defense + sustain + mobility;
        }
    }

    public int CombatPowerRounded => Mathf.RoundToInt(CombatPower);

    // -------------------------
    // Equipped item iteration
    // -------------------------
    private IEnumerable<ItemDefinition> EnumerateEquippedDefs()
    {
        if (!inventory || !equipment) yield break;

        ItemDefinition d;

        d = GetDef(equipment.MainHandItemId); if (d) yield return d;
        d = GetDef(equipment.OffHandItemId); if (d) yield return d;

        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Helmet)); if (d) yield return d;
        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Body)); if (d) yield return d;
        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Boots)); if (d) yield return d;
        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Trinket)); if (d) yield return d;
        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Pendant)); if (d) yield return d;

        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Ring, 0)); if (d) yield return d;
        d = GetDef(equipment.GetEquippedItemId(EquipSlot.Ring, 1)); if (d) yield return d;
    }

    // -------------------------
    // Toolbelt lookup
    // -------------------------
    private ItemDefinition GetToolDefFromToolbelt(ToolType type)
    {
        if (!toolbelt || !inventory) return null;

        for (int i = 0; i < ToolbeltManager.SlotCount; i++)
        {
            string id = toolbelt.GetToolItemId(i);
            if (string.IsNullOrWhiteSpace(id)) continue;

            var def = inventory.GetItemDef(id);
            if (!def || !def.IsTool) continue;

            if (def.toolStats.toolType == type)
                return def;
        }

        return null;
    }

    private float GetToolSpeedMult(ToolType type)
    {
        var def = GetToolDefFromToolbelt(type);
        return def ? def.GatherSpeedMultiplier : 1f;
    }

    private float GetToolGrit(ToolType type)
    {
        var def = GetToolDefFromToolbelt(type);
        return def ? def.GatheringGrit : 0f;
    }

    private float GetToolBonusFindChance(ToolType type)
    {
        var def = GetToolDefFromToolbelt(type);
        return def ? def.BonusResourceFindChance : 0f;
    }

    private float GetToolStaminaEfficiency(ToolType type)
    {
        var def = GetToolDefFromToolbelt(type);
        return def ? def.StaminaEfficiency : 0f;
    }

    // -------------------------
    // Weapon defs / support defs
    // -------------------------
    private ItemDefinition GetMainHandWeaponDef()
    {
        var def = GetDef(equipment ? equipment.MainHandItemId : null);
        return (def && def.IsWeapon) ? def : null;
    }

    private ItemDefinition GetOffHandWeaponDef()
    {
        var main = GetMainHandWeaponDef();
        if (main && main.RequiresOffhandSupport)
            return null;

        var def = GetDef(equipment ? equipment.OffHandItemId : null);
        if (!def || !def.IsWeapon) return null;
        if (def.weaponStats.handedness != Handedness.OneHanded) return null;
        return def;
    }

    private ItemDefinition GetOffHandSupportDef()
    {
        var def = GetDef(equipment ? equipment.OffHandItemId : null);
        return (def && def.IsCombatSupport) ? def : null;
    }

    private bool HasRequiredOffHandSupport()
    {
        var mh = GetMainHandWeaponDef();
        if (!mh) return true;
        if (!mh.RequiresOffhandSupport) return true;

        var support = GetOffHandSupportDef();
        if (!support) return false;

        return support.SupportType == mh.RequiredSupportType;
    }

    private ItemDefinition GetActiveOffHandSupportDef()
    {
        var mh = GetMainHandWeaponDef();
        if (!mh) return null;
        if (!mh.RequiresOffhandSupport) return null;

        var support = GetOffHandSupportDef();
        if (!support) return null;
        if (support.SupportType != mh.RequiredSupportType) return null;

        return support;
    }

    private static float GetWeaponAps(ItemDefinition def)
    {
        if (!def) return 0f;
        float cd = Mathf.Max(0.01f, def.AttackCooldown);
        return 1f / cd;
    }

    // -------------------------
    // Gear bonus aggregation
    // -------------------------
    private float GetEquippedCritChanceBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.critChanceBonus;
        }
        return total;
    }

    private float GetEquippedCritMultiplierBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.critMultiplierBonus;
        }
        return total;
    }

    private float GetEquippedAttackSpeedPercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.attackSpeedPercent;
        }
        return total;
    }

    // -------------------------
    // Attack type helpers
    // -------------------------
    private AttackSkill GetCurrentAttackSkill()
    {
        var mh = GetMainHandWeaponDef();
        if (!mh) return AttackSkill.Melee;

        return mh.weaponStats.attackSkill;
    }

    private MagicAttackType GetCurrentMagicAttackType()
    {
        var mh = GetMainHandWeaponDef();
        if (mh && mh.IsWeapon && mh.weaponStats.attackSkill == AttackSkill.Magic)
            return mh.weaponStats.magicAttackType;

        return baseMagicAttackType;
    }

    private float GetEquippedMagicAilmentApplyChance()
    {
        // Player path: comes from equipped magic weapon.
        // Enemy path: usually has no equipment, so this resolves to 0 and base value is used.
        var mh = GetMainHandWeaponDef();
        if (mh && mh.IsWeapon && mh.weaponStats.attackSkill == AttackSkill.Magic)
            return mh.MagicAilmentApplyChance;

        return 0f;
    }

    private DamageType GetLegacyCurrentDamageType()
    {
        var min = MinSplitDamage;
        var max = MaxSplitDamage;

        float totalPhys = min.physical + max.physical;
        float totalMag = min.magical + max.magical;
        float totalTrue = min.trueDamage + max.trueDamage;

        if (totalTrue >= totalPhys && totalTrue >= totalMag && totalTrue > 0f)
            return DamageType.True;

        if (totalMag >= totalPhys && totalMag > 0f)
            return DamageType.Magical;

        return DamageType.Physical;
    }

    // -------------------------
    // Offensive calcs
    // -------------------------
    private SplitDamage GetMinSplitDamage()
    {
        float physicalBuffMult = 1f + (buffController ? buffController.GetTotalMagnitude(ConsumableEffectType.PhysicalDamageBoost) : 0f);
        float magicBuffMult = 1f + (buffController ? buffController.GetTotalMagnitude(ConsumableEffectType.MagicDamageBoost) : 0f);
        float physicalGearPctMult = 1f + Mathf.Max(0f, GetEquippedPhysicalDamagePercent());
        float magicGearPctMult = 1f + Mathf.Max(0f, GetEquippedMagicDamagePercent());

        var mh = GetMainHandWeaponDef();

        if (!mh)
        {
            float phys = unarmedMinPhysicalDamage + BaseMinPhysicalDamage + GetEquippedPhysicalDamage();
            float mag = BaseMinMagicDamage + GetEquippedMagicDamage();
            float tru = BaseMinTrueDamage + GetEquippedTrueDamage();

            phys *= (physicalBuffMult * physicalGearPctMult);
            mag *= (magicBuffMult * magicGearPctMult);

            return new SplitDamage(
                Mathf.Max(0f, phys),
                Mathf.Max(0f, mag),
                Mathf.Max(0f, tru)
            );
        }

        if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
            return SplitDamage.Zero;

        var ohWeapon = GetOffHandWeaponDef();
        var support = GetActiveOffHandSupportDef();

        float physMin = mh.weaponStats.minPhysicalDamage;
        float magMin = mh.weaponStats.minMagicDamage;
        float trueMin = mh.weaponStats.minTrueDamage;

        if (ohWeapon)
        {
            physMin = (mh.weaponStats.minPhysicalDamage + ohWeapon.weaponStats.minPhysicalDamage) * 0.5f;
            magMin = (mh.weaponStats.minMagicDamage + ohWeapon.weaponStats.minMagicDamage) * 0.5f;
            trueMin = (mh.weaponStats.minTrueDamage + ohWeapon.weaponStats.minTrueDamage) * 0.5f;
        }

        if (support)
        {
            physMin += support.SupportBonusPhysicalDamage;
            magMin += support.SupportBonusMagicDamage;
            trueMin += support.SupportBonusTrueDamage;
        }

        physMin += BaseMinPhysicalDamage + GetEquippedPhysicalDamage();
        magMin += BaseMinMagicDamage + GetEquippedMagicDamage();
        trueMin += BaseMinTrueDamage + GetEquippedTrueDamage();

        physMin *= (physicalBuffMult * physicalGearPctMult);
        magMin *= (magicBuffMult * magicGearPctMult);

        return new SplitDamage(
            Mathf.Max(0f, physMin),
            Mathf.Max(0f, magMin),
            Mathf.Max(0f, trueMin)
        );
    }

    private SplitDamage GetMaxSplitDamage()
    {
        float physicalBuffMult = 1f + (buffController ? buffController.PhysicalDamageBoostPercent : 0f);
        float magicBuffMult = 1f + (buffController ? buffController.MagicDamageBoostPercent : 0f);
        float physicalGearPctMult = 1f + Mathf.Max(0f, GetEquippedPhysicalDamagePercent());
        float magicGearPctMult = 1f + Mathf.Max(0f, GetEquippedMagicDamagePercent());

        var mh = GetMainHandWeaponDef();

        if (!mh)
        {
            float phys = unarmedMaxPhysicalDamage + BaseMaxPhysicalDamage + GetEquippedPhysicalDamage();
            float mag = BaseMaxMagicDamage + GetEquippedMagicDamage();
            float tru = BaseMaxTrueDamage + GetEquippedTrueDamage();

            phys *= (physicalBuffMult * physicalGearPctMult);
            mag *= (magicBuffMult * magicGearPctMult);

            return new SplitDamage(
                Mathf.Max(0f, phys),
                Mathf.Max(0f, mag),
                Mathf.Max(0f, tru)
            );
        }

        if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
            return SplitDamage.Zero;

        var ohWeapon = GetOffHandWeaponDef();
        var support = GetActiveOffHandSupportDef();

        float physMax = mh.weaponStats.maxPhysicalDamage;
        float magMax = mh.weaponStats.maxMagicDamage;
        float trueMax = mh.weaponStats.maxTrueDamage;

        if (ohWeapon)
        {
            physMax = (mh.weaponStats.maxPhysicalDamage + ohWeapon.weaponStats.maxPhysicalDamage) * 0.5f;
            magMax = (mh.weaponStats.maxMagicDamage + ohWeapon.weaponStats.maxMagicDamage) * 0.5f;
            trueMax = (mh.weaponStats.maxTrueDamage + ohWeapon.weaponStats.maxTrueDamage) * 0.5f;
        }

        if (support)
        {
            physMax += support.SupportBonusPhysicalDamage;
            magMax += support.SupportBonusMagicDamage;
            trueMax += support.SupportBonusTrueDamage;
        }

        physMax += BaseMaxPhysicalDamage + GetEquippedPhysicalDamage();
        magMax += BaseMaxMagicDamage + GetEquippedMagicDamage();
        trueMax += BaseMaxTrueDamage + GetEquippedTrueDamage();

        physMax *= (physicalBuffMult * physicalGearPctMult);
        magMax *= (magicBuffMult * magicGearPctMult);

        return new SplitDamage(
            Mathf.Max(0f, physMax),
            Mathf.Max(0f, magMax),
            Mathf.Max(0f, trueMax)
        );
    }

    private float GetAttackRange()
    {
        var mh = GetMainHandWeaponDef();
        if (!mh) return Mathf.Max(0f, unarmedRange);

        if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
            return 0f;

        float mhRange = Mathf.Max(0f, mh.AttackRange);

        var oh = GetOffHandWeaponDef();
        if (!oh) return mhRange;

        float ohRange = Mathf.Max(0f, oh.AttackRange);
        return Mathf.Max(mhRange, ohRange);
    }

    private float GetAttacksPerSecond()
    {
        var mh = GetMainHandWeaponDef();
        float aps;

        if (!mh)
        {
            aps = Mathf.Max(0f, unarmedAttacksPerSecond);
        }
        else
        {
            if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
                return 0f;

            float mhAps = Mathf.Max(0f, GetWeaponAps(mh));

            var oh = GetOffHandWeaponDef();
            if (!oh)
            {
                aps = mhAps;
            }
            else
            {
                float ohAps = Mathf.Max(0f, GetWeaponAps(oh));
                float avg = (mhAps + ohAps) * 0.5f;
                aps = avg * Mathf.Max(0f, dualWieldApsBonus);
            }
        }

        float gearAtkSpeedPct = GetEquippedAttackSpeedPercent();

        if (buffController)
            gearAtkSpeedPct += buffController.GetTotalMagnitude(ConsumableEffectType.AttackSpeed);

        var support = GetActiveOffHandSupportDef();
        if (support)
            gearAtkSpeedPct += support.SupportAttackSpeedPercent;

        aps *= Mathf.Max(0.1f, 1f + gearAtkSpeedPct);

        return aps;
    }

    private float GetBaseDps()
    {
        float avgPhysical = (MinSplitDamage.physical + MaxSplitDamage.physical) * 0.5f;
        float avgMagical = (MinSplitDamage.magical + MaxSplitDamage.magical) * 0.5f;
        float avgTrue = (MinSplitDamage.trueDamage + MaxSplitDamage.trueDamage) * 0.5f;

        float avgTotal = avgPhysical + avgMagical + avgTrue;
        return avgTotal * AttacksPerSecond;
    }

    private float GetTrueDps()
    {
        return GetOffenseFromCombatModel();
    }

    private float GetCombatPowerOffenseFromCombatModel()
    {
        // Keep CP offense independent from the DPS property accessor while
        // using the same underlying combat model math.
        return GetOffenseFromCombatModel();
    }

    private float GetOffenseFromCombatModel()
    {
        return GetCombatModelDirectDps() + GetCombatModelAilmentDps();
    }

    private float GetCombatModelDirectDps()
    {
        float aps = AttacksPerSecond;
        if (aps <= 0f) return 0f;

        float cc = Mathf.Clamp01(CritChance);
        float cm = Mathf.Max(1f, CritMultiplier);
        float critFactor = 1f + cc * (cm - 1f);

        float avgPhys = (MinSplitDamage.physical + MaxSplitDamage.physical) * 0.5f;
        float avgMag = (MinSplitDamage.magical + MaxSplitDamage.magical) * 0.5f;
        float avgTrue = (MinSplitDamage.trueDamage + MaxSplitDamage.trueDamage) * 0.5f;

        float directPhysDps = avgPhys * aps * critFactor;
        float directMagDps = avgMag * aps * critFactor;
        float directTrueDps = avgTrue * aps * (trueDamageCanCrit ? critFactor : 1f);

        float weightedDirectDps =
            (directPhysDps * combatPowerPhysicalOffenseWeight) +
            (directMagDps * combatPowerMagicalOffenseWeight) +
            (directTrueDps * combatPowerTrueOffenseWeight);
        return weightedDirectDps;
    }

    private float GetCombatModelAilmentDps()
    {
        return
            (ExpectedBleedDPS * combatPowerBleedWeight) +
            (ExpectedPoisonDPS * combatPowerPoisonWeight) +
            (ExpectedBurnDPS * combatPowerBurnWeight);
    }

    private float GetCritChance()
    {
        float baseCrit;
        var mh = GetMainHandWeaponDef();

        if (!mh)
        {
            baseCrit = Mathf.Clamp01(unarmedCritChance);
        }
        else
        {
            if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
                return 0f;

            float mhCrit = Mathf.Clamp01(mh.weaponStats.critChance + mh.bonusStats.critChanceBonus);

            var oh = GetOffHandWeaponDef();
            if (!oh) baseCrit = mhCrit;
            else
            {
                float ohCrit = Mathf.Clamp01(oh.weaponStats.critChance + oh.bonusStats.critChanceBonus);
                baseCrit = Mathf.Max(mhCrit, ohCrit);
            }
        }

        float gearBonus = GetEquippedCritChanceBonus();

        var support = GetActiveOffHandSupportDef();
        if (support)
            gearBonus += support.SupportCritChanceBonus;

        return Mathf.Clamp01(baseCrit + gearBonus);
    }

    private float GetCritMultiplier()
    {
        float baseMult;
        var mh = GetMainHandWeaponDef();

        if (!mh)
        {
            baseMult = Mathf.Max(1f, unarmedCritMultiplier);
        }
        else
        {
            if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
                return 1f;

            float mhMult = Mathf.Max(1f, mh.weaponStats.critMultiplier + mh.bonusStats.critMultiplierBonus);

            var oh = GetOffHandWeaponDef();
            if (!oh) baseMult = mhMult;
            else
            {
                float ohMult = Mathf.Max(1f, oh.weaponStats.critMultiplier + oh.bonusStats.critMultiplierBonus);
                baseMult = Mathf.Max(mhMult, ohMult);
            }
        }

        float gearBonus = GetEquippedCritMultiplierBonus();

        var support = GetActiveOffHandSupportDef();
        if (support)
            gearBonus += support.SupportCritMultiplierBonus;

        return Mathf.Max(1f, baseMult + gearBonus);
    }

    // -------------------------
    // Equipped totals
    // -------------------------
    private int GetEquippedArmor()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.ArmorValue;
        return total;
    }

    private int GetEquippedMagicResist()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.MagicResist;
        return total;
    }

    private int GetEquippedBonusHealth()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BonusHealth;
        return total;
    }

    private int GetEquippedBonusEnergy()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BonusEnergy;
        return total;
    }

    private int GetEquippedBonusMana()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BonusMana;
        return total;
    }

    private float GetEquippedPhysBlockChance()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PhysBlockChance;
        return total;
    }

    private float GetEquippedMoveSpeedPercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.MoveSpeedPercent;
        return total;
    }

    private float GetEquippedLifeRegen()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.LifeRegen;
        return total;
    }

    private float GetEquippedEnergyRegen()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.EnergyRegen;
        return total;
    }

    private float GetEquippedManaRegen()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.ManaRegen;
        return total;
    }

    private float GetEquippedPhysicalDamage()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PhysicalDamage;
        return total;
    }

    private float GetEquippedPhysicalDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PhysicalDamagePercent;
        return total;
    }

    private float GetEquippedMagicDamage()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.MagicDamage;
        return total;
    }

    private float GetEquippedMagicDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.MagicDamagePercent;
        return total;
    }

    private float GetEquippedTrueDamage()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.TrueDamage;
        return total;
    }

    private float GetEquippedAbilityPower()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.AbilityPower;
        return total;
    }

    private float GetEquippedLifeSteal()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.LifeSteal;
        return total;
    }

    private float GetEquippedBleedChance()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BleedChance;
        return total;
    }

    private float GetEquippedBleedMultiplier()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BleedMultiplier;
        return total;
    }

    private float GetEquippedBleedDurationBonus()
    {
        return 0f;
    }

    private float GetEquippedPoisonChance()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PoisonChance;
        return total;
    }

    private float GetEquippedPoisonMultiplier()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PoisonMultiplier;
        return total;
    }

    private float GetEquippedPoisonDurationBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PoisonDurationBonus;
        return total;
    }

    private int GetEquippedPoisonMaxStacksBonus()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.PoisonMaxStacksBonus;
        return total;
    }

    private float GetEquippedBurnExplosionMultiplierBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BurnExplosionMultiplierBonus;
        return total;
    }

    private float GetEquippedChillSlowPerStackBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.ChillSlowPerStackBonus;
        return total;
    }

    private float GetEquippedShockDamageTakenMultiplierBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.ShockDamageTakenMultiplierBonus;
        return total;
    }

    private ItemDefinition GetDef(string id)
    {
        if (!inventory) return null;
        if (string.IsNullOrWhiteSpace(id)) return null;
        return inventory.GetItemDef(id);
    }

    // -------------------------
    // Combat rolls
    // -------------------------
    public SplitDamage RollSplitAttackDamage(out bool wasCrit)
    {
        var min = MinSplitDamage;
        var max = MaxSplitDamage;

        float phys = (max.physical > 0f && max.physical >= min.physical)
            ? UnityEngine.Random.Range(min.physical, max.physical + 0.0001f)
            : 0f;

        float mag = (max.magical > 0f && max.magical >= min.magical)
            ? UnityEngine.Random.Range(min.magical, max.magical + 0.0001f)
            : 0f;

        float tru = (max.trueDamage > 0f && max.trueDamage >= min.trueDamage)
            ? UnityEngine.Random.Range(min.trueDamage, max.trueDamage + 0.0001f)
            : 0f;

        wasCrit = false;

        if (UnityEngine.Random.value < CritChance)
        {
            wasCrit = true;
            float crit = Mathf.Max(1f, CritMultiplier);

            phys *= crit;
            mag *= crit;

            if (trueDamageCanCrit)
                tru *= crit;
        }

        return new SplitDamage(
            Mathf.Max(0f, phys),
            Mathf.Max(0f, mag),
            Mathf.Max(0f, tru)
        );
    }

    public int RollAttackDamage()
    {
        return Mathf.RoundToInt(RollSplitAttackDamage(out _).Total);
    }

    public float GetAbilityDamage(
    float baseDamage,
    float abilityPowerScale,
    float physicalDamageScale,
    float magicDamageScale,
    float trueDamageScale)
    {
        float ap = AbilityPower;

        // Use average of min/max ranges
        float pd = (BaseMinPhysicalDamage + BaseMaxPhysicalDamage) * 0.5f + GetEquippedPhysicalDamage();
        float md = (BaseMinMagicDamage + BaseMaxMagicDamage) * 0.5f + GetEquippedMagicDamage();
        float td = (BaseMinTrueDamage + BaseMaxTrueDamage) * 0.5f + GetEquippedTrueDamage();

        float result =
        baseDamage +
        (ap * abilityPowerScale) +
        (pd * physicalDamageScale) +
        (md * magicDamageScale) +
        (td * trueDamageScale);

        if (buffController)
        {
            float abilityBonus = buffController.GetTotalMagnitude(ConsumableEffectType.AbilityDamageBoost);
            result *= (1f + abilityBonus);
        }

        return Mathf.Max(0f, result);
    }

    public void RefreshVitalsFromStats(bool fillIfEmpty = false)
    {
        float newMaxHP = Mathf.Max(1f, MaxHP);
        float newMaxEnergy = Mathf.Max(0f, MaxEnergy);
        float newMaxMana = Mathf.Max(0f, MaxMana);

        // Apply saved vitals lazily so they resolve against the final loaded gear/max stats.
        if (_hasPendingLoadedVitals)
        {
            currentHP = _pendingLoadedHP;
            currentEnergy = _pendingLoadedEnergy;
            currentMana = _pendingLoadedMana;
            _didInitialFill = true;
            _hasPendingLoadedVitals = false;
        }

        if (!_didInitialFill || fillIfEmpty)
        {
            if (currentHP < 0f) currentHP = newMaxHP;
            if (currentEnergy < 0f) currentEnergy = newMaxEnergy;
            if (currentMana < 0f) currentMana = newMaxMana;
            _didInitialFill = true;
        }

        currentHP = Mathf.Clamp(currentHP, 0f, newMaxHP);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, newMaxEnergy);
        currentMana = Mathf.Clamp(currentMana, 0f, newMaxMana);

        OnHPChanged?.Invoke(currentHP, newMaxHP);
        OnEnergyChanged?.Invoke(currentEnergy, newMaxEnergy);
        OnManaChanged?.Invoke(currentMana, newMaxMana);
        OnStatsChanged?.Invoke();
    }

    // Vitals persistence is orchestrated by PlayerSave to avoid load-order races.
    // Keep ISaveable implementation as no-op here for compatibility.
    public void SaveInto(SaveData data) { }
    public void LoadFrom(SaveData data) { }

    public void ApplyLoadedVitals(float hp, float energy)
    {
        ApplyLoadedVitals(hp, energy, currentMana >= 0f ? currentMana : MaxMana);
    }

    public void ApplyLoadedVitals(float hp, float energy, float mana)
    {
        // Never restore into a dead state on load; dead state blocks resource spending (e.g. mana).
        currentHP = Mathf.Max(1f, hp);
        currentEnergy = energy;
        currentMana = mana;
        _didInitialFill = true;
        _hasPendingLoadedVitals = false;
        _isDead = currentHP <= 0f;
        RefreshVitalsFromStats(fillIfEmpty: false);
    }

    public void SetDisplayName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        unitDisplayName = newName;
        OnNameChanged?.Invoke(unitDisplayName);
    }

    public void TickRegen(float dt)
    {
        if (_isDead) return;

        float maxHp = Mathf.Max(1f, MaxHP);
        float maxEnergy = Mathf.Max(0f, MaxEnergy);
        float maxMana = Mathf.Max(0f, MaxMana);

        bool hpChanged = false;
        bool energyChanged = false;
        bool manaChanged = false;

        float hpRegen = LifeRegenPerSecond;
        float energyRegen = EnergyRegenPerSecond;
        float manaRegen = ManaRegenPerSecond;

        if (currentHP < maxHp && hpRegen > 0f)
        {
            float newHp = Mathf.Min(maxHp, currentHP + hpRegen * dt);
            if (!Mathf.Approximately(newHp, currentHP))
            {
                currentHP = newHp;
                hpChanged = true;
            }
        }

        if (currentEnergy < maxEnergy && energyRegen > 0f)
        {
            float newEnergy = Mathf.Min(maxEnergy, currentEnergy + energyRegen * dt);
            if (!Mathf.Approximately(newEnergy, currentEnergy))
            {
                currentEnergy = newEnergy;
                energyChanged = true;
            }
        }

        if (currentMana < maxMana && manaRegen > 0f)
        {
            float newMana = Mathf.Min(maxMana, currentMana + manaRegen * dt);
            if (!Mathf.Approximately(newMana, currentMana))
            {
                currentMana = newMana;
                manaChanged = true;
            }
        }

        if (hpChanged) OnHPChanged?.Invoke(currentHP, maxHp);
        if (energyChanged) OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        if (manaChanged) OnManaChanged?.Invoke(currentMana, maxMana);
    }

    public bool SpendEnergy(float amount)
    {
        if (_isDead) return false;
        if (amount <= 0f) return true;
        if (currentEnergy < amount) return false;

        currentEnergy = Mathf.Clamp(currentEnergy - amount, 0f, MaxEnergy);
        OnEnergyChanged?.Invoke(currentEnergy, MaxEnergy);
        return true;
    }

    public void AddEnergy(float amount)
    {
        if (_isDead || amount <= 0f) return;

        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, MaxEnergy);
        OnEnergyChanged?.Invoke(currentEnergy, MaxEnergy);
    }

    public bool SpendMana(float amount)
    {
        if (_isDead) return false;
        if (amount <= 0f) return true;
        if (currentMana < amount) return false;

        currentMana = Mathf.Clamp(currentMana - amount, 0f, MaxMana);
        OnManaChanged?.Invoke(currentMana, MaxMana);
        return true;
    }

    public void AddMana(float amount)
    {
        if (_isDead || amount <= 0f) return;

        currentMana = Mathf.Clamp(currentMana + amount, 0f, MaxMana);
        OnManaChanged?.Invoke(currentMana, MaxMana);
    }

    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;

        currentHP = Mathf.Clamp(currentHP + amount, 0f, MaxHP);
        OnHPChanged?.Invoke(currentHP, MaxHP);
    }

    public float TakeDamage(float amount, DamageType type, out bool blocked)
    {
        blocked = false;
        if (_isDead) return 0f;

        float finalDamage = ApplyMitigation(amount, type, out blocked);
        currentHP = Mathf.Clamp(currentHP - finalDamage, 0f, MaxHP);
        OnHPChanged?.Invoke(currentHP, MaxHP);

        if (currentHP <= 0f && !_isDead)
        {
            _isDead = true;
            OnDied?.Invoke();
        }

        return finalDamage;
    }

    public void ReviveFull()
    {
        _isDead = false;
        currentHP = MaxHP;
        currentEnergy = MaxEnergy;
        currentMana = MaxMana;
        OnHPChanged?.Invoke(currentHP, MaxHP);
        OnEnergyChanged?.Invoke(currentEnergy, MaxEnergy);
        OnManaChanged?.Invoke(currentMana, MaxMana);
    }

    private float ApplyMitigation(float rawDamage, DamageType type, out bool blocked)
    {
        blocked = false;

        rawDamage = Mathf.Max(0f, rawDamage);
        if (rawDamage <= 0f) return 0f;

        switch (type)
        {
            case DamageType.True:
                return rawDamage;

            case DamageType.Physical:
                {
                    float dmg = MitigateByRating(rawDamage, Armor);

                    if (PhysBlockChance > 0f && UnityEngine.Random.value < Mathf.Clamp01(PhysBlockChance))
                    {
                        blocked = true;
                        return 0f;
                    }

                    return dmg;
                }

            case DamageType.Magical:
                return MitigateByRating(rawDamage, MagicResist);

            default:
                return rawDamage;
        }
    }

    private static float MitigateByRating(float damage, float rating)
    {
        rating = Mathf.Max(0f, rating);
        float multiplier = 100f / (100f + rating);
        return damage * multiplier;
    }

    public void NotifyStatsChanged()
    {
        OnStatsChanged?.Invoke();
    }
}