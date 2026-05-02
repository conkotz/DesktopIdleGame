using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public struct SplitDamage
{
    public float physical;
    [FormerlySerializedAs("magical")]
    public float magic;
    public float corruptionDamage;

    public SplitDamage(float physical, float magic, float corruptionDamage)
    {
        this.physical = physical;
        this.magic = magic;
        this.corruptionDamage = corruptionDamage;
    }

    public float Total => physical + magic + corruptionDamage;

    public bool IsEmpty => physical <= 0f && magic <= 0f && corruptionDamage <= 0f;

    public static SplitDamage Zero => new SplitDamage(0f, 0f, 0f);

    public static SplitDamage operator +(SplitDamage a, SplitDamage b)
    {
        return new SplitDamage(
            a.physical + b.physical,
            a.magic + b.magic,
            a.corruptionDamage + b.corruptionDamage
        );
    }

    public static SplitDamage operator *(SplitDamage a, float mult)
    {
        return new SplitDamage(
            a.physical * mult,
            a.magic * mult,
            a.corruptionDamage * mult
        );
    }
}

/// <summary>Per-lane min/max damage (player-style basics); each hit rolls independently in range.</summary>
[System.Serializable]
public struct SplitDamageRange
{
    public SplitDamage min;
    public SplitDamage max;

    public static SplitDamageRange Uniform(SplitDamage v) => new SplitDamageRange { min = v, max = v };

    public bool IsUnset => min.IsEmpty && max.IsEmpty;

    /// <summary>
    /// Per-lane uniform roll + crit on phys/mag only (corruption never crits),
    /// matching <see cref="CharacterStats.RollSplitAttackDamage"/>.
    /// </summary>
    public SplitDamage RollBasicAttackDamage(float critChance, float critMultiplier, out bool wasCrit)
    {
        float phys = RollDamageLaneForBasicAttack(min.physical, max.physical);
        float mag = RollDamageLaneForBasicAttack(min.magic, max.magic);
        float corr = RollDamageLaneForBasicAttack(min.corruptionDamage, max.corruptionDamage);

        wasCrit = false;

        if (UnityEngine.Random.value < Mathf.Clamp01(critChance))
        {
            float crit = Mathf.Max(1f, critMultiplier);

            if (phys > 0f || mag > 0f)
            {
                wasCrit = true;
                phys *= crit;
                mag *= crit;
            }
        }

        return new SplitDamage(
            Mathf.Max(0f, phys),
            Mathf.Max(0f, mag),
            Mathf.Max(0f, corr)
        );
    }

    /// <summary>One damage lane: orders min/max so rounding errors cannot zero a hit that should deal damage.</summary>
    public static float RollDamageLaneForBasicAttack(float minVal, float maxVal)
    {
        float lo = Mathf.Min(minVal, maxVal);
        float hi = Mathf.Max(minVal, maxVal);
        if (hi <= 0f && lo <= 0f)
            return 0f;
        if (lo >= hi)
            return Mathf.Max(0f, lo);
        return Mathf.Max(0f, UnityEngine.Random.Range(lo, hi + 0.0001f));
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
    [SerializeField, HideInInspector] private float currentGuard = -1f;

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

    /// <summary>Current guard vs natural cap (armor or enemy definition). UI fill uses natural cap unless current exceeds it.</summary>
    public event Action<float, float> OnGuardChanged;

    private PlayerController _ownerPlayer;
    private EnemyBaseController _ownerEnemy;
    private PlayerCombatState _playerCombatState;

    private SkillsManager _skillProgressCombatPowerSubscribed;

    private float _guardPeaceTimer;
    private bool _wasInCombatForGuardTimer;

    [SerializeField, HideInInspector] private int _enemyDefinitionFlatGuard;
    [SerializeField, HideInInspector] private float _enemyDefinitionMaxGuardPercent;

    /// <summary>Enemy guard regen: no regen/decay until this many seconds after last damage to guard or HP.</summary>
    private float _lastIncomingDamageTimeForGuard = -999f;

    public string UnitDisplayName => unitDisplayName;
    public float HP => currentHP;
    public float Energy => currentEnergy;
    public float Mana => currentMana;
    /// <summary>Current guard pool (absorbs damage before HP). May exceed <see cref="NaturalGuardCap"/> from future abilities.</summary>
    public float Guard => Mathf.Max(0f, currentGuard);
    public bool IsDead => _isDead;

    [Header("Base Stats")]
    [SerializeField] private int baseMaxHP = 100;
    [SerializeField] private int baseMaxEnergy = 100;
    [SerializeField] private int baseMaxMana = 50;
    [SerializeField] private int baseArmor = 0;
    [SerializeField] private int baseMagicResist = 0;
    [SerializeField] private int baseCorruptionResist = 0;
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

    [FormerlySerializedAs("baseMinTrueDamage")]
    [SerializeField] private float baseMinCorruptionDamage = 0f;
    [FormerlySerializedAs("baseMaxTrueDamage")]
    [SerializeField] private float baseMaxCorruptionDamage = 0f;

    [SerializeField] private float baseAbilityPower = 0f;
    [SerializeField, Range(0f, 1f)] private float baseLifeSteal = 0f;

    [Header("Base Ailments")]
    [SerializeField, Range(0f, 1f)] private float baseBleedChance = 0f;
    [SerializeField] private float baseBleedMultiplier = 0f;
    [SerializeField] private float baseBleedDuration = 5f;

    [SerializeField, Range(0f, 1f)] private float basePoisonChance = 0f;
    [SerializeField] private float basePoisonMultiplier = 0f;
    [SerializeField, Range(0.05f, 1f), Tooltip("Portion of corruption damage on a hit that becomes one poison stack's total pool (before poison multiplier). Stacks add sustained damage.")]
    private float poisonPoolFractionOfCorruptionDamage = 0.4f;
    [SerializeField] private float basePoisonDuration = 5f;
    [SerializeField] private int basePoisonMaxStacks = 3;

    [Header("Base Elemental Ailments")]
    [SerializeField] private MagicAttackType baseMagicAttackType = MagicAttackType.Lightning;
    [SerializeField, Range(0f, 1f)] private float baseMagicAilmentApplyChance = 0f;
    [SerializeField, Range(0f, 1f), Tooltip("Additive burn stack chance on fire hits (player). Weapon fire weapons add their own chance.")]
    private float baseBurnChance = 0f;
    [SerializeField] private float baseChillDuration = 5f;
    [SerializeField] private int baseChillMaxStacks = 6;
    [SerializeField, Range(0f, 1f)] private float baseChillSlowPerStack = 0.15f;
    [Tooltip("Legacy tuning; burn combusts at 3 stacks (clamped).")]
    [SerializeField] private int baseBurnHitsToExplode = 3;
    [Tooltip("Multiplies burn tick damage (15% of strongest fire hit per tick, min 1).")]
    [SerializeField] private float baseBurnExplosionMultiplier = 1f;
    [Tooltip("Additive burn tick multiplier from skills, buffs, and passives (added after character base and gear).")]
    [SerializeField] private float bonusBurnDamageMultiplier = 0f;
    [SerializeField] private float baseShockDuration = 5f;
    [SerializeField, Range(0f, 1f)] private float baseShockDamageTakenMultiplier = 0.15f;

    [Header("Combat Tuning")]
    [SerializeField] private float dualWieldApsBonus = 1.15f;

    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ToolbeltManager toolbelt;
    [SerializeField] private SkillsManager skillsManager;
    [SerializeField] private SkillDatabase skillDatabase;

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

    [Header("Debug")]
    [SerializeField, Tooltip("Logs ability → combat power (action bar, DB, per-slot DPS). Can be verbose if CP UI refreshes every frame.")]
    private bool debugAbilityCombatPower;

    // Global combat-power tuning (shared across all characters and enemies).
    // Non-serialized by design to avoid per-instance drift in the inspector.
    private const float combatPowerOffenseScale = 3.0f;
    /// <summary>CP only: scales expected ailment DPS down vs direct hits (UI DPS / ailment math unchanged).</summary>
    private const float combatPowerAilmentContributionFactor = 0.65f;

    private const float combatPowerDefenseScale = 0.10f;
    /// <summary>Multiplier on <see cref="ExpectedSustainPerSecond"/> (regen + expected LS/s + small energy term).</summary>
    private const float combatPowerSustainScale = 4.35f;
    /// <summary>Cap on expected life steal / s as a fraction of <see cref="MaxHP"/> (CP sustain model).</summary>
    private const float combatPowerLifeStealMaxHpFractionPerSecond = 0.20f;

    private const float combatPowerMobilityExponent = 1.25f;
    private const float combatPowerMobilityScale = 2.0f;

    private const float combatPowerPhysicalWeight = 0.5f;
    private const float combatPowerMagicWeight = 0.3f;
    private const float combatPowerCorruptionWeight = 0.2f;
    /// <summary>CP defense only: guard contributes as a discounted HP-equivalent pool.</summary>
    private const float combatPowerGuardHealthEquivalentWeight = 0.70f;

    private const float LowHealthThreshold01 = 0.35f;

    private const float GuardOutOfCombatSecondsBeforeRegen = 10f;
    private const float GuardRegenOrDecayPerSecondFraction = 0.10f;

    private struct MeleeMinorNodeBonuses
    {
        public float meleeDamagePercent;
        public float flatMinMeleeDamage;
        public float flatMaxMeleeDamage;
        public float meleeAttackSpeedPercent;
        public float meleeMoveSpeedPercent;
        public float meleeCritChance;
        public float meleeCritDamage;
        public float meleeBleedChance;
        public float meleeBleedDamage;
        public float meleeBleedDuration;
        public float meleeMagicDamagePercent;
        public float meleeShockChance;
        public float meleePoisonChance;
        public float meleePoisonDuration;
        public float meleeAilmentDamage;
        public float meleeLifeSteal;
        public float meleeLifeRegen;
        public float meleeEnergyRegen;
        public float meleeArmor;
        public float meleeMagicResist;
        public float meleeDamageReduction;
        public float damageVsBleeding;
        public float damageVsPoisoned;
        public float damageVsShocked;
        public float damageVsLowHp;
        /// <summary>Minion damage % (fraction). Phase 1: wired from skill options later; gear uses <see cref="BonusStats"/>.</summary>
        public float minionDamagePercent;
        public float minionAttackSpeedPercent;
        public float minionCritChance;
        public float minionMaxLifePercent;
    }

    private struct RangedMinorNodeBonuses
    {
        public float rangedDamagePercent;
    }

    private void Start()
    {
        InitializeVitals();
        // SkillsManager may initialize after this component in some load orders.
        TrySubscribeSkillProgressForCombatPower();
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
        PreferRuntimeSkillsManager();
        if (!skillDatabase) skillDatabase = SkillDatabase.LoadDefault();
        _ownerPlayer = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>();
        if (_ownerPlayer)
            _playerCombatState = _ownerPlayer.GetComponent<PlayerCombatState>();
        ResolveOwnerEnemy();
    }

    private void OnEnable()
    {
        TrySubscribeSkillProgressForCombatPower();
    }

    private void OnDisable()
    {
        TryUnsubscribeSkillProgressForCombatPower();
    }

    /// <summary>
    /// Ability CP uses <see cref="AbilityCombatPower"/> (choice upgrades, row picks). Refresh when skill tree data changes.
    /// </summary>
    private void TrySubscribeSkillProgressForCombatPower()
    {
        if (!_ownerPlayer)
            return;

        PreferRuntimeSkillsManager();
        SkillsManager sm = SkillsManager.Instance != null ? SkillsManager.Instance : skillsManager;
        if (!sm)
            return;

        if (_skillProgressCombatPowerSubscribed == sm)
            return;

        TryUnsubscribeSkillProgressForCombatPower();

        sm.OnSkillChoiceSelectionChanged += OnSkillTreeChangedForCombatPower;
        sm.OnSkillAbilityRowPickChanged += OnSkillAbilityRowPickChangedForCombatPower;
        sm.OnLevelUp += OnSkillLevelUpForCombatPower;
        sm.OnSkillLevelDecreased += OnSkillLevelUpForCombatPower;
        _skillProgressCombatPowerSubscribed = sm;
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
    }

    private void TryUnsubscribeSkillProgressForCombatPower()
    {
        if (!_skillProgressCombatPowerSubscribed)
            return;

        SkillsManager sm = _skillProgressCombatPowerSubscribed;
        sm.OnSkillChoiceSelectionChanged -= OnSkillTreeChangedForCombatPower;
        sm.OnSkillAbilityRowPickChanged -= OnSkillAbilityRowPickChangedForCombatPower;
        sm.OnLevelUp -= OnSkillLevelUpForCombatPower;
        sm.OnSkillLevelDecreased -= OnSkillLevelUpForCombatPower;
        _skillProgressCombatPowerSubscribed = null;
    }

    private void OnSkillTreeChangedForCombatPower(SkillType skillType, int sourceLevel, int choiceIndex) =>
        NotifyStatsChanged();

    private void OnSkillAbilityRowPickChangedForCombatPower(SkillType skillType, int requiredLevel, int pickIndex) =>
        NotifyStatsChanged();

    private void OnSkillLevelUpForCombatPower(SkillType skillType, int newLevel) =>
        NotifyStatsChanged();

    private void ResolveOwnerEnemy()
    {
        if (_ownerEnemy)
            return;
        _ownerEnemy = GetComponent<EnemyBaseController>();
        if (!_ownerEnemy)
            _ownerEnemy = GetComponentInParent<EnemyBaseController>();
    }

    // -------------------------
    // Public computed stats
    // -------------------------

    // Defensive
    public int MaxHP => baseMaxHP + GetEquippedBonusHealth();

    /// <summary>
    /// Maximum natural guard: min(total flat, Max HP × (1 + total max-guard %)).
    /// Player: from armor; enemy: from <see cref="EnemyDefinition"/>. Abilities may still push <see cref="Guard"/> above this.
    /// </summary>
    public float NaturalGuardCap =>
        Mathf.Max(0f, Mathf.Min((float)GetNaturalGuardFlatTotal(), NaturalGuardHpCeilingFromGear));

    /// <summary>Upper bound from vitals only: Max HP × (1 + additive max-guard % from armor or enemy definition).</summary>
    public float NaturalGuardHpCeilingFromGear
    {
        get
        {
            float hp = Mathf.Max(1f, MaxHP);
            return hp * (1f + GetNaturalGuardMaxGuardPercentTotal());
        }
    }

    public int GearFlatGuardSum => GetEquippedArmorFlatGuardSum();
    public float GearMaxGuardPercentSum => GetEquippedArmorMaxGuardPercentSum();
    public int MaxEnergy => baseMaxEnergy + GetEquippedBonusEnergy();
    public int MaxMana => Mathf.Max(0, baseMaxMana + GetEquippedBonusMana());
    public int Armor =>
        baseArmor + GetEquippedArmor() + Mathf.RoundToInt(GetActiveMeleeMinorBonuses().meleeArmor) +
        (buffController ? Mathf.RoundToInt(buffController.GetTotalMagnitude(ConsumableEffectType.ArmorBoost)) : 0);

    public int MagicResist =>
        baseMagicResist + GetEquippedMagicResist() + Mathf.RoundToInt(GetActiveMeleeMinorBonuses().meleeMagicResist) +
        (buffController ? Mathf.RoundToInt(buffController.GetTotalMagnitude(ConsumableEffectType.MagicResistBoost)) : 0);
    public int CorruptionResist => baseCorruptionResist + GetEquippedCorruptionResist();

    public float PhysBlockChance => Mathf.Clamp01(basePhysBlockChance + GetEquippedPhysBlockChance());
    public float PhysBlockChancePercent => PhysBlockChance * 100f;

    // Utility / sustain
    public float GearMoveSpeedPercent => GetEquippedMoveSpeedPercent();

    // Future-ready: temporary slows / buffs from ailments, skills, etc.
    public float TemporaryMoveSpeedPercent => 0f;

    public float TotalMoveSpeedPercent =>
        GearMoveSpeedPercent + TemporaryMoveSpeedPercent + GetActiveMeleeMinorBonuses().meleeMoveSpeedPercent +
        (buffController ? buffController.GetTotalMagnitude(ConsumableEffectType.MoveSpeed) : 0f);

    public float MoveSpeedMultiplier => Mathf.Max(0.1f, baseMoveSpeedMult * (1f + TotalMoveSpeedPercent));
    public float FinalMoveSpeed => BaseMoveSpeed * MoveSpeedMultiplier;

    /// <summary>
    /// Move speed for combat power mobility and Relentless checks. Enemies use
    /// <see cref="EnemyBaseController.MoveSpeed"/>; player and others use <see cref="FinalMoveSpeed"/>.
    /// </summary>
    public float GetMoveSpeedForCombatPower()
    {
        ResolveOwnerEnemy();

        if (_ownerEnemy)
            return Mathf.Max(0f, _ownerEnemy.MoveSpeed);

        return Mathf.Max(0.01f, FinalMoveSpeed);
    }

    // Displayed bonus/penalty relative to normal base speed
    public float MoveSpeedBonusPercent => MoveSpeedMultiplier - 1f;

    public float LifeRegenPerSecond => Mathf.Max(0f, baseLifeRegen + GetEquippedLifeRegen() + GetActiveMeleeMinorBonuses().meleeLifeRegen);
    public float EnergyRegenPerSecond =>
        Mathf.Max(
            0f,
            baseEnergyRegen + GetEquippedEnergyRegen() + GetActiveMeleeMinorBonuses().meleeEnergyRegen +
            (buffController ? buffController.GetTotalMagnitude(ConsumableEffectType.EnergyRegen) : 0f));
    public float ManaRegenPerSecond => Mathf.Max(0f, baseManaRegen + GetEquippedManaRegen());
    public float LifeSteal => Mathf.Clamp01(baseLifeSteal + GetEquippedLifeSteal() + GetActiveMeleeMinorBonuses().meleeLifeSteal);

    // Offensive stats
    public float BaseMinPhysicalDamage => Mathf.Max(0f, baseMinPhysicalDamage);
    public float BaseMaxPhysicalDamage => Mathf.Max(BaseMinPhysicalDamage, baseMaxPhysicalDamage);

    public float BaseMinMagicDamage => Mathf.Max(0f, baseMinMagicDamage);
    public float BaseMaxMagicDamage => Mathf.Max(BaseMinMagicDamage, baseMaxMagicDamage);

    public float BaseMinCorruptionDamage => Mathf.Max(0f, baseMinCorruptionDamage);
    public float BaseMaxCorruptionDamage => Mathf.Max(BaseMinCorruptionDamage, baseMaxCorruptionDamage);
    public float AbilityPower => Mathf.Max(0f, baseAbilityPower + GetEquippedAbilityPower());

    /// <summary>Active ability-damage potion as percent points (+20 for +20%), for stats panel; matches <see cref="GetAbilityDamage"/> multiplier.</summary>
    public float AbilityDamageBoostConsumablePercentPoints =>
        (buffController ? buffController.GetTotalMagnitude(ConsumableEffectType.AbilityDamageBoost) : 0f) * 100f;

    /// <summary>Denominator for ability power: bonus damage = AbilityPower × (per-ability coefficient) / this value (e.g. 100 AP with standard coef 0.5 = +50% damage).</summary>
    public const float AbilityPowerDamagePercentDivisor = 100f;

    /// <summary>
    /// Multiplier applied to ability damage after weapon/skill multipliers: <c>1 + AbilityPower × coefficient / <see cref="AbilityPowerDamagePercentDivisor"/></c>.
    /// With <see cref="AbilityDefinition.StandardAbilityPowerCoefficient"/>: each AP adds +0.5% damage; 100 AP adds +50% (×1.5 total).
    /// </summary>
    public float GetAbilityPowerDamageMultiplier(float abilityPowerCoefficient)
    {
        float c = Mathf.Max(0f, abilityPowerCoefficient);
        if (c <= 0f)
            return 1f;
        return 1f + AbilityPower * c / AbilityPowerDamagePercentDivisor;
    }

    // Ailments
    public float BleedChance => Mathf.Clamp01(baseBleedChance + GetEquippedBleedChance() + GetActiveMeleeMinorBonuses().meleeBleedChance);
    public float BleedMultiplier => Mathf.Max(0f, baseBleedMultiplier + GetEquippedBleedMultiplier() + GetActiveMeleeMinorBonuses().meleeBleedDamage + GetActiveMeleeMinorBonuses().meleeAilmentDamage);

    public float BleedBaseDuration => Mathf.Max(1f, baseBleedDuration);
    public float BleedDuration => Mathf.Max(1f, baseBleedDuration + GetEquippedBleedDurationBonus() + GetActiveMeleeMinorBonuses().meleeBleedDuration);

    public float PoisonChance => Mathf.Clamp01(basePoisonChance + GetEquippedPoisonChance() + GetActiveMeleeMinorBonuses().meleePoisonChance);
    public float PoisonMultiplier => Mathf.Max(0f, basePoisonMultiplier + GetEquippedPoisonMultiplier() + GetActiveMeleeMinorBonuses().meleeAilmentDamage);
    public float PoisonDuration => Mathf.Max(0.1f, basePoisonDuration + GetEquippedPoisonDurationBonus() + GetActiveMeleeMinorBonuses().meleePoisonDuration);
    public int PoisonMaxStacks => Mathf.Max(1, basePoisonMaxStacks + GetEquippedPoisonMaxStacksBonus());

    public float BleedChancePercent => BleedChance * 100f;
    public float PoisonChancePercent => PoisonChance * 100f;

    /// <summary>Fraction of corruption dealt on a hit stored in one poison stack's pool (before <see cref="PoisonMultiplier"/>).</summary>
    public float PoisonPoolFractionOfCorruptionDamage => Mathf.Clamp(poisonPoolFractionOfCorruptionDamage, 0.05f, 1f);

    public MagicAttackType CurrentMagicAttackType => GetCurrentMagicAttackType();
    public float MagicAilmentApplyChance => Mathf.Clamp01(baseMagicAilmentApplyChance + GetEquippedMagicAilmentApplyChance());

    /// <summary>True when the current attack profile should run burn logic on fire hits (weapon fire type or active magic element Fire).</summary>
    public bool CurrentAttackAppliesAsFireForBurn => GetCurrentAttackAppliesAsFireForBurn();

    /// <summary>
    /// Chance to add a burn stack on fire damage hits. Player: character base + gear bonus + fire weapon <see cref="ItemDefinition.ResolveWeaponBurnApplyChance"/> (weapon magic ailment chance).
    /// </summary>
    public float BurnApplyChance => GetBurnApplyChance();
    public float ChillDuration => Mathf.Max(0.1f, baseChillDuration);
    public int ChillMaxStacks => Mathf.Max(1, baseChillMaxStacks);
    public float ChillSlowPerStack => Mathf.Clamp01(baseChillSlowPerStack + GetEquippedChillSlowPerStackBonus());
    /// <summary>Stacks before combust (fixed at 3 at runtime).</summary>
    public int BurnHitsToExplode => Mathf.Clamp(Mathf.Max(2, baseBurnHitsToExplode), 2, 3);

    /// <summary>
    /// Total multiplier on burn tick damage (character base + gear + <see cref="BonusBurnDamageMultiplier"/>
    /// + melee skill-tree <c>meleeAilmentDamage</c> when using a melee weapon, same as bleed/poison multipliers).
    /// Matches combat: 15% of strongest fire hit × this value per tick (min 1), combust uses strongest tick × configured factor.
    /// </summary>
    public float BurnDamageMultiplier =>
        Mathf.Max(
            0f,
            baseBurnExplosionMultiplier + GetEquippedBurnExplosionMultiplierBonus() + bonusBurnDamageMultiplier +
            GetActiveMeleeMinorBonuses().meleeAilmentDamage);

    /// <summary>Multiplies burn tick damage; same value as <see cref="BurnDamageMultiplier"/>.</summary>
    public float BurnExplosionMultiplier => BurnDamageMultiplier;

    /// <summary>Skills, buffs, passives — additive on top of base and gear. Adjust at runtime for scaling systems.</summary>
    public float BonusBurnDamageMultiplier
    {
        get => bonusBurnDamageMultiplier;
        set
        {
            float v = Mathf.Max(0f, value);
            if (Mathf.Approximately(bonusBurnDamageMultiplier, v))
                return;
            bonusBurnDamageMultiplier = v;
            OnStatsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Sum of equipped <see cref="ItemDefinition.BurnExplosionMultiplierBonus"/> (fraction: 0.25 = +25% to tick multiplier when added to character base).
    /// Use this for UI that should match item sliders; <see cref="BurnDamageMultiplier"/> also includes character base and <see cref="BonusBurnDamageMultiplier"/>.
    /// </summary>
    public float BurnExplosionMultiplierBonusFromEquipment => Mathf.Max(0f, GetEquippedBurnExplosionMultiplierBonus());
    public float ShockDuration => Mathf.Max(0.1f, baseShockDuration);
    public float ShockDamageTakenMultiplier => Mathf.Clamp01(baseShockDamageTakenMultiplier + GetEquippedShockDamageTakenMultiplierBonus());
    public float MeleeShockChance => Mathf.Clamp01(GetActiveMeleeMinorBonuses().meleeShockChance);
    public float MeleeDamageVsBleeding => Mathf.Max(0f, GetActiveMeleeMinorBonuses().damageVsBleeding);
    public float MeleeDamageVsPoisoned => Mathf.Max(0f, GetActiveMeleeMinorBonuses().damageVsPoisoned);
    public float MeleeDamageVsShocked => Mathf.Max(0f, GetActiveMeleeMinorBonuses().damageVsShocked);
    public float MeleeDamageVsLowHp => Mathf.Max(0f, GetActiveMeleeMinorBonuses().damageVsLowHp);
    public float MeleeLowHpThreshold01 => LowHealthThreshold01;

    public float PhysicalReductionFromArmorPercent
    {
        get
        {
            float a = Mathf.Max(0f, Armor) * GetConsumableDefenseBoostRatingMultiplier();
            float multiplier = 100f / (100f + a);
            return (1f - multiplier) * 100f;
        }
    }

    public float MagicReductionFromMrPercent
    {
        get
        {
            float mr = Mathf.Max(0f, MagicResist) * GetConsumableDefenseBoostRatingMultiplier();
            float multiplier = 100f / (100f + mr);
            return (1f - multiplier) * 100f;
        }
    }

    public float CorruptionReductionFromResistPercent
    {
        get
        {
            float cr = Mathf.Max(0f, CorruptionResist) * GetConsumableDefenseBoostRatingMultiplier();
            float multiplier = 100f / (100f + cr);
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

    /// <summary>Fixed crit damage for all minions: ×1.5 total hit damage (+50% bonus). Not scalable from gear or passives.</summary>
    public const float MinionCritDamageMultiplier = 1.5f;

    /// <summary>Lowest allowed aggregate minion attack speed bonus so future APS uses <c>max(0.1, 1 + total)</c>.</summary>
    private const float MinMinionAttackSpeedPercentAdditive = -0.9f;

    /// <summary>Aggregated minion damage bonus fraction from gear + owner passives (melee skill minors for now). ≥ 0.</summary>
    public float FinalMinionDamagePercent =>
        Mathf.Max(0f, GetEquippedMinionDamagePercent() + GetOwnerMinionBonusesFromSkills().minionDamagePercent);

    /// <summary>Aggregated minion attack speed bonus fraction. Clamped so <c>1 + value ≥ 0.1</c> for future APS math.</summary>
    public float FinalMinionAttackSpeedPercent =>
        Mathf.Max(
            MinMinionAttackSpeedPercentAdditive,
            GetEquippedMinionAttackSpeedPercent() + GetOwnerMinionBonusesFromSkills().minionAttackSpeedPercent);

    /// <summary>Aggregated minion crit chance (0–1 additive from gear + passives). ≥ 0; no upper clamp (matches player crit style for high values).</summary>
    public float FinalMinionCritChance =>
        Mathf.Max(0f, GetEquippedMinionCritChance() + GetOwnerMinionBonusesFromSkills().minionCritChance);

    /// <summary>Aggregated bonus max life fraction for minions (gear + passives). Use with minion HP when implemented.</summary>
    public float FinalMinionMaxLifePercent =>
        Mathf.Max(0f, GetEquippedMinionMaxLifePercent() + GetOwnerMinionBonusesFromSkills().minionMaxLifePercent);

    /// <summary>UI: minion damage % as display points (+15 for +15%).</summary>
    public float FinalMinionDamagePercentPoints => FinalMinionDamagePercent * 100f;

    /// <summary>UI: minion attack speed % as display points.</summary>
    public float FinalMinionAttackSpeedPercentPoints => FinalMinionAttackSpeedPercent * 100f;

    /// <summary>UI: minion crit as percentage points for labels.</summary>
    public float FinalMinionCritChancePercentPoints => FinalMinionCritChance * 100f;

    /// <summary>UI: minion max life % as display points.</summary>
    public float FinalMinionMaxLifePercentPoints => FinalMinionMaxLifePercent * 100f;

    /// <summary>
    /// Owner minion max-life % after inherit penalty (half strength when inheriting weapon hit stats).
    /// </summary>
    public float GetEffectiveMinionMaxLifePercent(MinionDamageSourceMode mode)
    {
        float scale = mode == MinionDamageSourceMode.InheritOwnerHitSplit
            ? MinionRuntimeStatsCalculator.InheritMinionOwnerBonusScale
            : 1f;
        return FinalMinionMaxLifePercent * scale;
    }

    /// <summary>True when the attack has physical or magic damage; corruption-only hits cannot crit on basic attacks.</summary>
    public bool HasCrittableDirectDamage =>
        MaxSplitDamage.physical > 0f || MaxSplitDamage.magic > 0f;

    /// <summary>Crit % for stats UI: 0 when damage is corruption-only (gear crit still applies only to crittable types).</summary>
    public float StatsPanelCritChancePercent =>
        HasCrittableDirectDamage ? CritChancePercent : 0f;

    public AttackSkill CurrentAttackSkill => GetCurrentAttackSkill();
    public DamageType CurrentDamageType => GetLegacyCurrentDamageType();

    public float BaseDPS => GetBaseDps();

    /// <summary>Expected total DPS (direct + full ailment DPS). <see cref="CombatPower"/> applies a separate ailment factor to offense only.</summary>
    public float DPS => GetStatsSheetDirectDps() + GetStatsSheetAilmentDps();

    public float WeaponDpsComponent => GetStatsSheetDirectDps();
    public float AilmentDpsComponent => GetStatsSheetAilmentDps();

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
    public float AverageMagicHit => (MinSplitDamage.magic + MaxSplitDamage.magic) * 0.5f;
    public float AverageCorruptionHit => (MinSplitDamage.corruptionDamage + MaxSplitDamage.corruptionDamage) * 0.5f;

    /// <summary>
    /// Fraction of average melee magic damage that is lightning (weapon elemental split only; flat magic from gear is not lightning).
    /// Used so melee shock only procs when the hit actually includes lightning damage.
    /// </summary>
    public float GetMeleeMagicLightningFraction()
    {
        float avgM = AverageMagicHit;
        if (avgM <= 0f) return 0f;
        return Mathf.Clamp01(GetMeleeAverageWeaponLightningDamagePerHit() / avgM);
    }

    /// <summary>Average lightning from the equipped weapon(s) on a melee hit after <see cref="GetMeleeSplitDamageScalingMultipliers"/> magic mult.</summary>
    private float GetMeleeAverageWeaponLightningDamagePerHit()
    {
        var mh = GetMainHandWeaponDef();
        if (!mh) return 0f;
        if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport()) return 0f;

        GetMeleeSplitDamageScalingMultipliers(GetActiveMeleeMinorBonuses(), out _, out float magicDamageMult, out _);

        float Lmin = Mathf.Max(0f, mh.weaponStats.minLightningDamage);
        float Lmax = Mathf.Max(0f, mh.weaponStats.maxLightningDamage);
        var oh = GetOffHandWeaponDef();
        if (oh)
        {
            Lmin = (Mathf.Max(0f, mh.weaponStats.minLightningDamage) + Mathf.Max(0f, oh.weaponStats.minLightningDamage)) * 0.5f;
            Lmax = (Mathf.Max(0f, mh.weaponStats.maxLightningDamage) + Mathf.Max(0f, oh.weaponStats.maxLightningDamage)) * 0.5f;
        }

        Lmin *= magicDamageMult;
        Lmax *= magicDamageMult;
        return (Lmin + Lmax) * 0.5f;
    }

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
    public float ExpectedMagicHit => AverageMagicHit * ExpectedCritFactor;

    /// <summary>Corruption damage never benefits from crit on basic attacks (combat rolls and DPS models assume this).</summary>
    public float ExpectedCorruptionHit => AverageCorruptionHit;

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
    // Stats-panel display model (rough DPS estimate from your rolled corruption; ignores enemy mitigation):
    // - uses average corruption hit × PoisonPoolFractionOfCorruptionDamage × (1 + poison multiplier)
    // - assumes poison can ramp toward full stacks
    // - PoisonMaxDPS is sustained DPS if every stack slot were always full (see ExpectedPoisonStacks for sheet model)
    // Runtime: poison application uses corruption damage actually dealt to that target (post mitigation).

    public float PoisonPerStackTotalDamage
    {
        get
        {
            if (PoisonChance <= 0f) return 0f;
            if (PoisonDuration <= 0f) return 0f;
            if (AverageCorruptionHit <= 0f) return 0f;

            return AverageCorruptionHit * PoisonPoolFractionOfCorruptionDamage * (1f + PoisonMultiplier);
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

            float m = PoisonMaxStacks;
            float p = Mathf.Clamp01(PoisonChance);
            // Little's-style mean concurrent stacks if uncapped: λ × duration × proc chance.
            float little = AttacksPerSecond * p * PoisonTicks;

            // Below the stack cap this already includes apply chance (fewer procs → fewer stacks).
            if (little < m - 0.0001f)
                return Mathf.Clamp(little, 0f, m);

            // At the cap, clamping to M assumed you always sit on max stacks. Missed procs still
            // create downtime, so scale sustained stacks by proc chance for sheet DPS / CP.
            return m * p;
        }
    }

    public float ExpectedPoisonDPS => PoisonPerStackDPS * ExpectedPoisonStacks;
    public float ExpectedBurnDPS => GetExpectedBurnDps();
    public float ExpectedAilmentDPS => ExpectedBleedDPS + ExpectedPoisonDPS + ExpectedBurnDPS;

    private float GetExpectedBurnDps()
    {
        if (!CurrentAttackAppliesAsFireForBurn)
            return 0f;
        float p = Mathf.Clamp01(BurnApplyChance);
        if (p <= 0f || AttacksPerSecond <= 0f)
            return 0f;

        float hit = ExpectedPhysicalHit + ExpectedMagicHit + ExpectedCorruptionHit;
        if (hit <= 0f)
            return 0f;

        const float burnFraction = 0.15f;
        const int combustStacks = 3;
        float mult = Mathf.Max(0f, BurnExplosionMultiplier);
        float tick = Mathf.Max(1f, Mathf.Ceil(hit * burnFraction * mult));
        float applyPerSec = p * AttacksPerSecond;
        float dotDps = tick * Mathf.Clamp(applyPerSec * 0.35f, 0f, 1f);
        float combustDps = (tick * 10f) * (applyPerSec / Mathf.Max(1, combustStacks));
        return dotDps + combustDps;
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

            return CombatPowerDefenseHealthPool / Mathf.Max(0.01f, damageTakenMultiplier);
        }
    }

    public float EffectiveHPVsMagic
    {
        get
        {
            float damageTakenMultiplier = 100f / (100f + Mathf.Max(0f, MagicResist));
            return CombatPowerDefenseHealthPool / Mathf.Max(0.01f, damageTakenMultiplier);
        }
    }

    public float EffectiveHPVsCorruption
    {
        get
        {
            float damageTakenMultiplier = 100f / (100f + Mathf.Max(0f, CorruptionResist));
            return CombatPowerDefenseHealthPool / Mathf.Max(0.01f, damageTakenMultiplier);
        }
    }

    /// <summary>
    /// Defensive pool used by CP: Max HP plus a discounted guard-equivalent buffer.
    /// Guard matters, but less than true HP for scaling.
    /// </summary>
    public float CombatPowerDefenseHealthPool =>
        Mathf.Max(1f, MaxHP + NaturalGuardCap * combatPowerGuardHealthEquivalentWeight);

    public float WeightedEffectiveHP
    {
        get
        {
            float totalWeight =
                combatPowerPhysicalWeight +
                combatPowerMagicWeight +
                combatPowerCorruptionWeight;

            if (totalWeight <= 0f)
                return CombatPowerDefenseHealthPool;

            return
                (EffectiveHPVsPhysical * combatPowerPhysicalWeight +
                 EffectiveHPVsMagic * combatPowerMagicWeight +
                 EffectiveHPVsCorruption * combatPowerCorruptionWeight)
                / totalWeight;
        }
    }

    public float ExpectedLifeStealPerSecond
    {
        get
        {
            float raw = GetStatsSheetDirectDps() * Mathf.Clamp01(LifeSteal);
            return Mathf.Min(raw, MaxHP * combatPowerLifeStealMaxHpFractionPerSecond);
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

    /// <summary>Bucket values for combat power; same math as <see cref="CombatPower"/>.</summary>
    public CombatPowerBreakdown GetCombatPowerBreakdown() => BuildCombatPowerBreakdown();

    public float CombatPower => GetCombatPowerBreakdown().TotalCombatPower;

    public int CombatPowerRounded => Mathf.RoundToInt(CombatPower);

    public CombatProfileDefenseHints GetCombatProfileDefenseHints()
    {
        return new CombatProfileDefenseHints(
            EffectiveHPVsPhysical,
            EffectiveHPVsMagic,
            EffectiveHPVsCorruption,
            Armor,
            MagicResist,
            CorruptionResist,
            MaxHP,
            GetMoveSpeedForCombatPower());
    }

    public string GetCombatProfileLabel()
    {
        CombatPowerBreakdown b = GetCombatPowerBreakdown();
        return CombatProfileClassifier.Classify(b, GetCombatProfileDefenseHints());
    }

    public Color GetCombatProfileColor()
    {
        CombatPowerBreakdown b = GetCombatPowerBreakdown();
        CombatProfileDefenseHints hints = GetCombatProfileDefenseHints();
        return CombatProfileClassifier.GetColorForLabel(CombatProfileClassifier.Classify(b, hints));
    }

    public string GetCombatProfileDebugSummary()
    {
        CombatPowerBreakdown b = GetCombatPowerBreakdown();
        CombatProfileDefenseHints hints = GetCombatProfileDefenseHints();
        string label = CombatProfileClassifier.Classify(b, hints);
        return CombatProfileClassifier.BuildDebugSummary(b, label, hints);
    }

    private CombatPowerBreakdown BuildCombatPowerBreakdown()
    {
        float directDps = GetStatsSheetDirectDps();
        float ailmentDps = GetStatsSheetAilmentDps();
        float abilityDps = 0f;
        if (!_ownerPlayer)
        {
            if (debugAbilityCombatPower)
                Debug.LogWarning(
                    "[CharacterStats] CP: ability DPS skipped — no PlayerController on this hierarchy (GetComponent/GetComponentInParent).",
                    this);
        }
        else
        {
            abilityDps = AbilityCombatPower.EstimateTotalSlottedAbilityDps(this, debugAbilityCombatPower);
            if (debugAbilityCombatPower)
                Debug.Log(
                    $"[CharacterStats] CP buckets: directDps={directDps:F4} ailmentDps={ailmentDps:F4} " +
                    $"× ailmentFactor={combatPowerAilmentContributionFactor:F2} abilityDps={abilityDps:F4} " +
                    $"offenseRaw={directDps + ailmentDps * combatPowerAilmentContributionFactor + abilityDps:F4}",
                    this);
        }

        float offenseRaw = directDps + ailmentDps * combatPowerAilmentContributionFactor + abilityDps;
        float offense = offenseRaw * combatPowerOffenseScale;

        float defense = WeightedEffectiveHP * combatPowerDefenseScale;

        float sustain = ExpectedSustainPerSecond * combatPowerSustainScale;

        float mobility =
            Mathf.Pow(GetMoveSpeedForCombatPower(), combatPowerMobilityExponent) * combatPowerMobilityScale;

        return new CombatPowerBreakdown(offense, defense, sustain, mobility);
    }

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

    /// <summary>
    /// Total seconds subtracted from <see cref="MapNodeDefinition.enemyRespawnDelaySeconds"/> (via
    /// <see cref="LevelSpawnDirector"/>) from equipped items' <see cref="ItemMiscEffects.enemyRespawnTimeReductionSeconds"/>.
    /// </summary>
    public float GetTotalEquippedEnemyRespawnTimeReductionSeconds()
    {
        float sum = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def != null)
                sum += def.EnemyRespawnTimeReductionSeconds;
        }

        return sum;
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
        // Match gather / visual unarmed override: use fist profile from Attack Profile (Unarmed) for damage.
        if (equipment != null && equipment.ForceUnarmed)
            return null;

        var def = GetDef(equipment ? equipment.MainHandItemId : null);
        return (def && def.IsWeapon) ? def : null;
    }

    /// <summary>
    /// Matches <see cref="PlayerAbilityController"/> weapon gating for abilities with
    /// <see cref="AbilityDefinition.requiredWeaponType"/>.
    /// </summary>
    public bool IsAbilityUsableWithEquippedWeapon(AbilityDefinition def)
    {
        if (def == null || def.requiredWeaponType == AbilityWeaponRequirement.Any)
            return true;

        if (!equipment)
            equipment = GetComponent<EquipmentManager>();
        if (!inventory)
            inventory = GetComponent<Inventory>();
        if (!equipment || !inventory || string.IsNullOrWhiteSpace(equipment.MainHandItemId))
            return false;

        ItemDefinition mainHand = inventory.GetItemDef(equipment.MainHandItemId);
        if (!mainHand || !mainHand.IsWeapon)
            return false;

        AttackSkill skill = mainHand.weaponStats.attackSkill;
        return def.requiredWeaponType switch
        {
            AbilityWeaponRequirement.Melee => skill == AttackSkill.Melee,
            AbilityWeaponRequirement.Ranged => skill == AttackSkill.Ranged,
            AbilityWeaponRequirement.Magic => skill == AttackSkill.Magic,
            AbilityWeaponRequirement.MeleeOrRanged =>
                skill == AttackSkill.Melee || skill == AttackSkill.Ranged,
            _ => true
        };
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

    private float GetEquippedMinionDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.minionDamagePercent;
        }
        return total;
    }

    private float GetEquippedMinionAttackSpeedPercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.minionAttackSpeedPercent;
        }
        return total;
    }

    private float GetEquippedMinionCritChance()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.minionCritChance;
        }
        return total;
    }

    private float GetEquippedMinionMaxLifePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.bonusStats.minionMaxLifePercent;
        }
        return total;
    }

    /// <summary>
    /// Melee skill-tree minors that grant minion stats. Phase 2 TODO: add other skill tracks (ranged/magic) without changing <see cref="FinalMinionDamagePercent"/> API.
    /// </summary>
    private MeleeMinorNodeBonuses GetOwnerMinionBonusesFromSkills()
    {
        if (!_ownerPlayer)
            return default;
        return GetUnlockedMeleeMinorBonuses();
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

    private float GetBurnApplyChance()
    {
        ResolveOwnerEnemy();
        if (_ownerEnemy != null)
            return MagicAilmentApplyChance;

        return Mathf.Clamp01(baseBurnChance + GetEquippedBurnChanceBonus() + GetMainHandWeaponBurnAdditive());
    }

    private bool GetCurrentAttackAppliesAsFireForBurn()
    {
        var mh = GetMainHandWeaponDef();
        if (mh != null && mh.IsWeapon && mh.weaponStats.magicAttackType == MagicAttackType.Fire)
            return true;

        return GetCurrentMagicAttackType() == MagicAttackType.Fire;
    }

    private float GetEquippedBurnChanceBonus()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.BonusBurnChance;
        }

        return total;
    }

    private float GetMainHandWeaponBurnAdditive()
    {
        var mh = GetMainHandWeaponDef();
        return mh != null ? mh.ResolveWeaponBurnApplyChance() : 0f;
    }

    private DamageType GetLegacyCurrentDamageType()
    {
        var min = MinSplitDamage;
        var max = MaxSplitDamage;

        float totalPhys = min.physical + max.physical;
        float totalMag = min.magic + max.magic;
        float totalCorruption = min.corruptionDamage + max.corruptionDamage;

        if (totalCorruption >= totalPhys && totalCorruption >= totalMag && totalCorruption > 0f)
            return DamageType.Corruption;

        if (totalMag >= totalPhys && totalMag > 0f)
            return DamageType.Magic;

        return DamageType.Physical;
    }

    // -------------------------
    // Offensive calcs
    // -------------------------
    /// <summary>
    /// Additive physical % (before the <c>1 +</c> multiplier) for weapon split damage: global gear/support,
    /// plus melee minor tree <c>meleeDamagePercent</c> when <paramref name="weaponAttackSkill"/> is <see cref="AttackSkill.Melee"/>,
    /// or ranged gear + ranged skill-tree % when <see cref="AttackSkill.Ranged"/>.
    /// (Ranged gear and <c>rangedDamagePercent</c> also scale magic and corruption on ranged basics — see <see cref="GetMeleeSplitDamageScalingMultipliers"/>.)
    /// Magic-staff attacks use global physical % only here.
    /// </summary>
    private static float GetAdditivePhysicalPercentForWeaponStyle(
        AttackSkill weaponAttackSkill,
        float globalPhysicalFraction,
        float rangedPhysicalFraction,
        float rangedSkillTreeDamagePercent,
        MeleeMinorNodeBonuses meleeBonuses)
    {
        float p = globalPhysicalFraction;
        if (weaponAttackSkill == AttackSkill.Ranged)
            p += rangedPhysicalFraction + rangedSkillTreeDamagePercent;
        else if (weaponAttackSkill == AttackSkill.Melee)
            p += meleeBonuses.meleeDamagePercent;
        return Mathf.Max(0f, p);
    }

    /// <summary>
    /// Same stacking as basic-attack physical scaling (global + melee tree or ranged gear), as a fraction before buffs (0.1 = +10%).
    /// Use for ranged/melee abilities that should match weapon passive scaling; multiply by <c>1 +</c> this value, then apply consumable physical buffs if needed.
    /// </summary>
    public float GetPhysicalDamageScalingFractionBeforeBuffs(AttackSkill forWeaponAttackSkill)
    {
        MeleeMinorNodeBonuses melee =
            forWeaponAttackSkill == AttackSkill.Melee ? GetActiveMeleeMinorBonuses() : default;
        float rangedSkillTree = 0f;
        if (forWeaponAttackSkill == AttackSkill.Ranged)
            rangedSkillTree = GetActiveRangedMinorBonuses().rangedDamagePercent;

        return GetAdditivePhysicalPercentForWeaponStyle(
            forWeaponAttackSkill,
            GetEquippedGlobalPhysicalDamagePercent(),
            GetEquippedRangedPhysicalDamagePercent(),
            rangedSkillTree,
            melee);
    }

    /// <summary>Uses <see cref="CurrentAttackSkill"/> (main-hand weapon).</summary>
    public float GetPhysicalDamageScalingFractionBeforeBuffs() =>
        GetPhysicalDamageScalingFractionBeforeBuffs(GetCurrentAttackSkill());

    /// <summary>
    /// Combined multipliers for basic-attack split damage (matches <see cref="GetMinSplitDamage"/> / <see cref="GetMaxSplitDamage"/>):
    /// Physical: consumable physical × (global + melee-tree % or ranged gear + ranged-tree %).
    /// Magic: consumable magic × (global magic + melee magic % + melee-tree % when melee, or + ranged totals when ranged).
    /// Corruption: global corruption % + melee-tree % when melee, or + ranged totals when ranged.
    /// </summary>
    private void GetMeleeSplitDamageScalingMultipliers(
        MeleeMinorNodeBonuses meleeBonuses,
        out float physicalDamageMult,
        out float magicDamageMult,
        out float corruptionDamageMult)
    {
        AttackSkill skill = GetCurrentAttackSkill();
        RangedMinorNodeBonuses rangedBonuses = GetActiveRangedMinorBonuses();
        float rangedGear = GetEquippedRangedPhysicalDamagePercent();
        float rangedSkill = rangedBonuses.rangedDamagePercent;
        float rangedTotalPct = rangedGear + rangedSkill;

        float physicalBuffMult = 1f + (buffController ? buffController.PhysicalDamageBoostPercent : 0f);
        float magicBuffMult = 1f + (buffController ? buffController.MagicDamageBoostPercent : 0f);
        float physPct = GetAdditivePhysicalPercentForWeaponStyle(
            skill,
            GetEquippedGlobalPhysicalDamagePercent(),
            rangedGear,
            rangedSkill,
            meleeBonuses);
        float physicalGearPctMult = 1f + physPct;

        float magicPct = GetEquippedMagicDamagePercent() + meleeBonuses.meleeMagicDamagePercent;
        if (skill == AttackSkill.Melee)
            magicPct += meleeBonuses.meleeDamagePercent;
        if (skill == AttackSkill.Ranged)
            magicPct += rangedTotalPct;
        float magicGearPctMult = 1f + Mathf.Max(0f, magicPct);

        float corrPct = GetEquippedCorruptionDamagePercent();
        if (skill == AttackSkill.Melee)
            corrPct += meleeBonuses.meleeDamagePercent;
        if (skill == AttackSkill.Ranged)
            corrPct += rangedTotalPct;
        corruptionDamageMult = 1f + Mathf.Max(0f, corrPct);

        physicalDamageMult = physicalBuffMult * physicalGearPctMult;
        magicDamageMult = magicBuffMult * magicGearPctMult;
    }

    /// <summary>Total % increase to physical melee split (20 = +20%).</summary>
    public float MeleePhysicalDamageTotalScalingPercentPoints
    {
        get
        {
            GetMeleeSplitDamageScalingMultipliers(GetActiveMeleeMinorBonuses(), out float mult, out _, out _);
            return (mult - 1f) * 100f;
        }
    }

    /// <summary>Total % increase to magic on the attack split (20 = +20%), including melee skill-tree % when using a melee weapon.</summary>
    public float MeleeMagicDamageTotalScalingPercentPoints
    {
        get
        {
            GetMeleeSplitDamageScalingMultipliers(GetActiveMeleeMinorBonuses(), out _, out float mult, out _);
            return (mult - 1f) * 100f;
        }
    }

    /// <summary>Total % increase to corruption on the attack split from gear plus melee skill-tree % when using a melee weapon.</summary>
    public float MeleeCorruptionDamageTotalScalingPercentPoints
    {
        get
        {
            GetMeleeSplitDamageScalingMultipliers(GetActiveMeleeMinorBonuses(), out _, out _, out float mult);
            return (mult - 1f) * 100f;
        }
    }

    /// <summary>Equipped additive fraction for Fire skill damage (0.10 = +10%). Buffs can extend later.</summary>
    public float FireSkillDamageTotalScalingPercentPoints => GetEquippedFireSkillDamagePercent() * 100f;

    public float IceSkillDamageTotalScalingPercentPoints => GetEquippedIceSkillDamagePercent() * 100f;

    public float LightningSkillDamageTotalScalingPercentPoints => GetEquippedLightningSkillDamagePercent() * 100f;

    /// <summary>Global physical % on attacks: gear, supports, and active <see cref="ConsumableEffectType.PhysicalDamageBoost"/>; additive fraction (0.10 = +10%).</summary>
    public float GlobalPhysicalDamageBonusPercentPoints =>
        (GetEquippedGlobalPhysicalDamagePercent() + (buffController ? buffController.PhysicalDamageBoostPercent : 0f)) * 100f;

    /// <summary>Global magic % on attacks: gear, supports, and active <see cref="ConsumableEffectType.MagicDamageBoost"/>.</summary>
    public float GlobalMagicDamageBonusPercentPoints =>
        (GetEquippedMagicDamagePercent() + (buffController ? buffController.MagicDamageBoostPercent : 0f)) * 100f;

    /// <summary>Equipped corruption attack-split % (armor bonus + combat supports).</summary>
    public float GlobalCorruptionDamageBonusPercentPoints => GetEquippedCorruptionDamagePercent() * 100f;

    /// <summary>
    /// Melee skill-tree % bonus to all damage on your melee attack split (physical, magic, corruption). Shown for unlocked passives; applies only with a <see cref="AttackSkill.Melee"/> weapon.
    /// </summary>
    public float MeleePhysicalConditionalBonusPercentPoints
    {
        get
        {
            var m = GetUnlockedMeleeMinorBonuses();
            return m.meleeDamagePercent * 100f;
        }
    }

    /// <summary>
    /// Ranged gear + ranged skill-tree % applied to all basic-attack damage (physical, magic, corruption) with a ranged weapon.
    /// </summary>
    public float RangedTotalDamageBonusPercentPoints =>
        (GetEquippedRangedPhysicalDamagePercent() + GetUnlockedRangedMinorBonuses().rangedDamagePercent) * 100f;

    /// <inheritdoc cref="RangedTotalDamageBonusPercentPoints"/>
    public float RangedPhysicalDamageBonusPercentPoints => RangedTotalDamageBonusPercentPoints;

    /// <summary>Bleed is a single-stack DoT in the current combat model.</summary>
    public int BleedMaxStacks => 1;

    /// <summary>Burn DoT duration shown in UI (matches default burn tick window on targets).</summary>
    public float BurnDotDurationSeconds => 15f;

    /// <summary>Chance to apply shock for stats panel: lightning magic hits use magic ailment chance; otherwise melee shock.</summary>
    public float ShockApplyChancePercentForStatsPanel
    {
        get
        {
            if (CurrentAttackSkill == AttackSkill.Magic && CurrentMagicAttackType == MagicAttackType.Lightning)
                return MagicAilmentApplyChance * 100f;
            return MeleeShockChance * 100f;
        }
    }

    /// <summary>Chill apply chance for stats panel when ice magic attacks.</summary>
    public float ChillApplyChancePercentForStatsPanel
    {
        get
        {
            if (CurrentAttackSkill == AttackSkill.Magic && CurrentMagicAttackType == MagicAttackType.Ice)
                return MagicAilmentApplyChance * 100f;
            return 0f;
        }
    }

    /// <summary>Fraction added to multiplier for abilities keyed to <see cref="CurrentMagicAttackType"/> (e.g. 0.2 = +20%).</summary>
    public float ElementSkillDamageScalingFractionForCurrentType() =>
        ElementSkillDamageScalingFractionFor(CurrentMagicAttackType);

    public float ElementSkillDamageScalingFractionFor(MagicAttackType type)
    {
        switch (type)
        {
            case MagicAttackType.Fire:
                return GetEquippedFireSkillDamagePercent();
            case MagicAttackType.Ice:
                return GetEquippedIceSkillDamagePercent();
            case MagicAttackType.Lightning:
                return GetEquippedLightningSkillDamagePercent();
            default:
                return 0f;
        }
    }

    private SplitDamage GetMinSplitDamage()
    {
        MeleeMinorNodeBonuses meleeBonuses = GetActiveMeleeMinorBonuses();
        GetMeleeSplitDamageScalingMultipliers(
            meleeBonuses,
            out float physicalDamageMult,
            out float magicDamageMult,
            out float corruptionDamageMult);

        var mh = GetMainHandWeaponDef();

        if (!mh)
        {
            float phys = unarmedMinPhysicalDamage + BaseMinPhysicalDamage + GetEquippedPhysicalDamage() + meleeBonuses.flatMinMeleeDamage;
            float mag = BaseMinMagicDamage + GetEquippedMagicDamage();
            float corr = BaseMinCorruptionDamage + GetEquippedCorruptionDamage();

            phys *= physicalDamageMult;
            mag *= magicDamageMult;
            corr *= corruptionDamageMult;

            return new SplitDamage(
                Mathf.Max(0f, phys),
                Mathf.Max(0f, mag),
                Mathf.Max(0f, corr)
            );
        }

        if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
            return SplitDamage.Zero;

        var ohWeapon = GetOffHandWeaponDef();
        var support = GetActiveOffHandSupportDef();

        float physMin = mh.weaponStats.minPhysicalDamage;
        float magMin = mh.weaponStats.TotalElementalDamageMin;
        float corruptionMin = mh.weaponStats.minCorruptionDamage;

        if (ohWeapon)
        {
            physMin = (mh.weaponStats.minPhysicalDamage + ohWeapon.weaponStats.minPhysicalDamage) * 0.5f;
            magMin = (mh.weaponStats.TotalElementalDamageMin + ohWeapon.weaponStats.TotalElementalDamageMin) * 0.5f;
            corruptionMin = (mh.weaponStats.minCorruptionDamage + ohWeapon.weaponStats.minCorruptionDamage) * 0.5f;
        }

        if (support)
        {
            physMin += support.SupportBonusPhysicalDamage;
            magMin += support.SupportBonusMagicDamage;
            corruptionMin += support.SupportBonusCorruptionDamage;
        }

        physMin += GetEquippedPhysicalDamage();
        magMin += GetEquippedMagicDamage();
        corruptionMin += GetEquippedCorruptionDamage();
        AddFlatDamageAcrossExistingLanes(
            meleeBonuses.flatMinMeleeDamage,
            ref physMin,
            ref magMin,
            ref corruptionMin);

        physMin *= physicalDamageMult;
        magMin *= magicDamageMult;
        corruptionMin *= corruptionDamageMult;

        return new SplitDamage(
            Mathf.Max(0f, physMin),
            Mathf.Max(0f, magMin),
            Mathf.Max(0f, corruptionMin)
        );
    }

    private SplitDamage GetMaxSplitDamage()
    {
        MeleeMinorNodeBonuses meleeBonuses = GetActiveMeleeMinorBonuses();
        GetMeleeSplitDamageScalingMultipliers(
            meleeBonuses,
            out float physicalDamageMult,
            out float magicDamageMult,
            out float corruptionDamageMult);

        var mh = GetMainHandWeaponDef();

        if (!mh)
        {
            float phys = unarmedMaxPhysicalDamage + BaseMaxPhysicalDamage + GetEquippedPhysicalDamage() + meleeBonuses.flatMaxMeleeDamage;
            float mag = BaseMaxMagicDamage + GetEquippedMagicDamage();
            float corr = BaseMaxCorruptionDamage + GetEquippedCorruptionDamage();

            phys *= physicalDamageMult;
            mag *= magicDamageMult;
            corr *= corruptionDamageMult;

            return new SplitDamage(
                Mathf.Max(0f, phys),
                Mathf.Max(0f, mag),
                Mathf.Max(0f, corr)
            );
        }

        if (mh.RequiresOffhandSupport && !HasRequiredOffHandSupport())
            return SplitDamage.Zero;

        var ohWeapon = GetOffHandWeaponDef();
        var support = GetActiveOffHandSupportDef();

        float physMax = mh.weaponStats.maxPhysicalDamage;
        float magMax = mh.weaponStats.TotalElementalDamageMax;
        float corruptionMax = mh.weaponStats.maxCorruptionDamage;

        if (ohWeapon)
        {
            physMax = (mh.weaponStats.maxPhysicalDamage + ohWeapon.weaponStats.maxPhysicalDamage) * 0.5f;
            magMax = (mh.weaponStats.TotalElementalDamageMax + ohWeapon.weaponStats.TotalElementalDamageMax) * 0.5f;
            corruptionMax = (mh.weaponStats.maxCorruptionDamage + ohWeapon.weaponStats.maxCorruptionDamage) * 0.5f;
        }

        if (support)
        {
            physMax += support.SupportBonusPhysicalDamage;
            magMax += support.SupportBonusMagicDamage;
            corruptionMax += support.SupportBonusCorruptionDamage;
        }

        physMax += GetEquippedPhysicalDamage();
        magMax += GetEquippedMagicDamage();
        corruptionMax += GetEquippedCorruptionDamage();
        AddFlatDamageAcrossExistingLanes(
            meleeBonuses.flatMaxMeleeDamage,
            ref physMax,
            ref magMax,
            ref corruptionMax);

        physMax *= physicalDamageMult;
        magMax *= magicDamageMult;
        corruptionMax *= corruptionDamageMult;

        return new SplitDamage(
            Mathf.Max(0f, physMax),
            Mathf.Max(0f, magMax),
            Mathf.Max(0f, corruptionMax)
        );
    }

    private static void AddFlatDamageAcrossExistingLanes(
        float flatDamage,
        ref float physical,
        ref float magic,
        ref float corruption)
    {
        if (flatDamage <= 0f)
            return;

        float phys = Mathf.Max(0f, physical);
        float mag = Mathf.Max(0f, magic);
        float corr = Mathf.Max(0f, corruption);
        float total = phys + mag + corr;

        if (total <= 0f)
        {
            physical += flatDamage;
            return;
        }

        physical += flatDamage * (phys / total);
        magic += flatDamage * (mag / total);
        corruption += flatDamage * (corr / total);
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
        MeleeMinorNodeBonuses meleeBonuses = GetActiveMeleeMinorBonuses();
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

        float gearAtkSpeedPct = GetEquippedAttackSpeedPercent() + meleeBonuses.meleeAttackSpeedPercent;

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
        float avgMagic = (MinSplitDamage.magic + MaxSplitDamage.magic) * 0.5f;
        float avgCorruption = (MinSplitDamage.corruptionDamage + MaxSplitDamage.corruptionDamage) * 0.5f;

        float avgTotal = avgPhysical + avgMagic + avgCorruption;
        return avgTotal * AttacksPerSecond;
    }

    /// <summary>Direct hit DPS for character sheet: expected damage with crit on phys/mag only; corruption never crits on basics.</summary>
    private float GetStatsSheetDirectDps()
    {
        float aps = AttacksPerSecond;
        if (aps <= 0f) return 0f;

        float cc = Mathf.Clamp01(CritChance);
        float cm = Mathf.Max(1f, CritMultiplier);
        float critFactor = 1f + cc * (cm - 1f);

        float avgPhys = (MinSplitDamage.physical + MaxSplitDamage.physical) * 0.5f;
        float avgMag = (MinSplitDamage.magic + MaxSplitDamage.magic) * 0.5f;
        float avgCorruption = (MinSplitDamage.corruptionDamage + MaxSplitDamage.corruptionDamage) * 0.5f;

        return ((avgPhys + avgMag) * critFactor + avgCorruption) * aps;
    }

    /// <summary>Ailment DPS for character sheet without CP tuning weights.</summary>
    private float GetStatsSheetAilmentDps()
    {
        return ExpectedBleedDPS + ExpectedPoisonDPS + ExpectedBurnDPS;
    }

    private float GetCritChance()
    {
        MeleeMinorNodeBonuses meleeBonuses = GetActiveMeleeMinorBonuses();
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

        return Mathf.Clamp01(baseCrit + gearBonus + meleeBonuses.meleeCritChance);
    }

    private float GetCritMultiplier()
    {
        MeleeMinorNodeBonuses meleeBonuses = GetActiveMeleeMinorBonuses();
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

        return Mathf.Max(1f, baseMult + gearBonus + meleeBonuses.meleeCritDamage);
    }

    private MeleeMinorNodeBonuses GetActiveMeleeMinorBonuses()
    {
        if (GetCurrentAttackSkill() != AttackSkill.Melee)
            return default;

        return GetUnlockedMeleeMinorBonuses();
    }

    private RangedMinorNodeBonuses GetActiveRangedMinorBonuses()
    {
        if (GetCurrentAttackSkill() != AttackSkill.Ranged)
            return default;

        return GetUnlockedRangedMinorBonuses();
    }

    private MeleeMinorNodeBonuses GetUnlockedMeleeMinorBonuses()
    {
        // Skill-tree unlock/enhancement bonuses are player-only.
        // Enemies also use CharacterStats, but must never inherit player progression.
        if (!_ownerPlayer)
            return default;

        PreferRuntimeSkillsManager();
        if (!skillDatabase) skillDatabase = SkillDatabase.LoadDefault();
        if (!skillsManager || !skillDatabase)
            return default;

        SkillDefinition meleeDef = skillDatabase.Get(SkillType.Melee);
        if (meleeDef == null || meleeDef.unlocks == null || meleeDef.unlocks.Count == 0)
            return default;

        int meleeLevel = skillsManager.GetLevel(SkillType.Melee);
        MeleeMinorNodeBonuses total = default;
        for (int i = 0; i < meleeDef.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = meleeDef.unlocks[i];
            if (unlock == null)
                continue;
            if (unlock.unlockType != SkillUnlockType.MinorPassive)
                continue;
            if (unlock.requiredLevel > meleeLevel)
                continue;

            ApplyMeleeMinorOption(unlock.meleeMinorStatOption, ref total);
        }

        ApplyLevel10BloodlettingBranch(meleeLevel, ref total);

        return total;
    }

    private RangedMinorNodeBonuses GetUnlockedRangedMinorBonuses()
    {
        if (!_ownerPlayer)
            return default;

        PreferRuntimeSkillsManager();
        if (!skillDatabase) skillDatabase = SkillDatabase.LoadDefault();
        if (!skillsManager || !skillDatabase)
            return default;

        SkillDefinition rangedDef = skillDatabase.Get(SkillType.Ranged);
        if (rangedDef == null || rangedDef.unlocks == null || rangedDef.unlocks.Count == 0)
            return default;

        int rangedLevel = skillsManager.GetLevel(SkillType.Ranged);
        RangedMinorNodeBonuses total = default;
        for (int i = 0; i < rangedDef.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = rangedDef.unlocks[i];
            if (unlock == null)
                continue;
            if (unlock.unlockType != SkillUnlockType.MinorPassive)
                continue;
            if (unlock.requiredLevel > rangedLevel)
                continue;

            ApplyRangedMinorOption(unlock.rangedMinorStatOption, ref total);
        }

        return total;
    }

    private void ApplyLevel10BloodlettingBranch(int meleeLevel, ref MeleeMinorNodeBonuses total)
    {
        if (meleeLevel < 10)
            return;

        // Lv10 Melee major passive branch:
        // default Bloodletting unless a conversion choice is selected.
        int selected = skillsManager != null
            ? skillsManager.GetSkillChoiceSelection(SkillType.Melee, 10, -1)
            : -1;

        if (selected == 0)
        {
            // Venom Edge: replace Bloodletting bleed package.
            total.meleePoisonChance += 0.10f;
            total.damageVsPoisoned += 0.10f;
        }
        else if (selected == 1)
        {
            // Hemorrhage: keep Bloodletting base and add deeper-bleed bonuses.
            total.meleeBleedChance += 0.10f;
            total.damageVsBleeding += 0.10f;
            total.meleeBleedDamage += 0.05f;
            total.meleeBleedDuration += 1f;
        }
        else
        {
            // Bloodletting default.
            total.meleeBleedChance += 0.10f;
            total.damageVsBleeding += 0.10f;
        }
    }

    private static void ApplyMeleeMinorOption(MeleeMinorNodeStatOption option, ref MeleeMinorNodeBonuses total)
    {
        switch (option)
        {
            case MeleeMinorNodeStatOption.MinMeleeDamageFlat2:
                total.flatMinMeleeDamage += 2f;
                break;
            case MeleeMinorNodeStatOption.MaxMeleeDamageFlat2:
                total.flatMaxMeleeDamage += 2f;
                break;
            case MeleeMinorNodeStatOption.MeleeAttackSpeedPercent3:
                total.meleeAttackSpeedPercent += 0.03f;
                break;
            case MeleeMinorNodeStatOption.MeleeDamagePercent3:
                total.meleeDamagePercent += 0.03f;
                break;
            case MeleeMinorNodeStatOption.MeleeCritChancePercent2:
                total.meleeCritChance += 0.02f;
                break;
            case MeleeMinorNodeStatOption.MeleeDamageVsLowHpPercent10:
                total.damageVsLowHp += 0.10f;
                break;
            case MeleeMinorNodeStatOption.MeleeBleedChancePercent5:
                total.meleeBleedChance += 0.05f;
                break;
            case MeleeMinorNodeStatOption.MeleeBleedDamagePercent10:
                total.meleeBleedDamage += 0.10f;
                break;
            case MeleeMinorNodeStatOption.MeleeMoveSpeedPercent2:
                total.meleeMoveSpeedPercent += 0.02f;
                break;
            case MeleeMinorNodeStatOption.MeleeMoveSpeedPercent5:
                total.meleeMoveSpeedPercent += 0.05f;
                break;
            case MeleeMinorNodeStatOption.MeleeCritDamagePercent8:
                total.meleeCritDamage += 0.08f;
                break;
            case MeleeMinorNodeStatOption.MeleePoisonChancePercent5:
                total.meleePoisonChance += 0.05f;
                break;
            case MeleeMinorNodeStatOption.MeleePoisonDurationPercent10:
                total.meleePoisonDuration += 0.10f;
                break;
            case MeleeMinorNodeStatOption.MeleeAilmentDamagePercent4:
                total.meleeAilmentDamage += 0.04f;
                break;
            case MeleeMinorNodeStatOption.MeleeDamageVsPoisonedPercent10:
                total.damageVsPoisoned += 0.10f;
                break;
            case MeleeMinorNodeStatOption.MeleeShockChancePercent5:
                total.meleeShockChance += 0.05f;
                break;
            case MeleeMinorNodeStatOption.MeleeDamageVsShockedPercent10:
                total.damageVsShocked += 0.10f;
                break;
            case MeleeMinorNodeStatOption.MeleeLifeStealPercent1:
                total.meleeLifeSteal += 0.01f;
                break;
            case MeleeMinorNodeStatOption.MeleeDamageVsBleedingPercent10:
                total.damageVsBleeding += 0.10f;
                break;
            case MeleeMinorNodeStatOption.CoreMeleeOffense:
                total.meleeDamagePercent += 0.03f;
                total.flatMinMeleeDamage += 2f;
                total.flatMaxMeleeDamage += 2f;
                break;
            case MeleeMinorNodeStatOption.MeleeSpeed:
                total.meleeAttackSpeedPercent += 0.03f;
                total.meleeMoveSpeedPercent += 0.02f;
                break;
            case MeleeMinorNodeStatOption.MeleeCrit:
                total.meleeCritChance += 0.02f;
                total.meleeCritDamage += 0.08f;
                break;
            case MeleeMinorNodeStatOption.MeleeBleedPhysicalPath:
                total.meleeBleedChance += 0.05f;
                total.meleeBleedDamage += 0.10f;
                break;
            case MeleeMinorNodeStatOption.MeleeElementalHybrid:
                total.meleeMagicDamagePercent += 0.03f;
                total.meleeShockChance += 0.05f;
                break;
            case MeleeMinorNodeStatOption.MeleePoisonCorruptionHybrid:
                total.meleePoisonChance += 0.05f;
                total.meleePoisonDuration += 0.10f;
                total.meleeAilmentDamage += 0.04f;
                break;
            case MeleeMinorNodeStatOption.ConditionalMeleeOnly:
                total.damageVsBleeding += 0.10f;
                total.damageVsPoisoned += 0.10f;
                total.damageVsShocked += 0.10f;
                total.damageVsLowHp += 0.10f;
                break;
            case MeleeMinorNodeStatOption.SustainMeleeScaled:
                total.meleeLifeSteal += 0.01f;
                total.meleeLifeRegen += 2f;
                total.meleeEnergyRegen += 2f;
                break;
            case MeleeMinorNodeStatOption.DefensiveMeleeBuild:
                total.meleeArmor += 10f;
                total.meleeMagicResist += 10f;
                total.meleeDamageReduction += 0.02f;
                break;
        }
    }

    private static void ApplyRangedMinorOption(RangedMinorNodeStatOption option, ref RangedMinorNodeBonuses total)
    {
        switch (option)
        {
            case RangedMinorNodeStatOption.RangedDamagePercent3:
                total.rangedDamagePercent += 0.03f;
                break;
        }
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

    private int GetEquippedArmorFlatGuardSum()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.ArmorFlatGuard;
        }

        return Mathf.Max(0, total);
    }

    private int GetNaturalGuardFlatTotal()
    {
        ResolveOwnerEnemy();
        if (_ownerEnemy)
            return Mathf.Max(0, _enemyDefinitionFlatGuard);
        return GetEquippedArmorFlatGuardSum();
    }

    private float GetEquippedArmorMaxGuardPercentSum()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            if (def == null) continue;
            total += def.ArmorMaxGuardPercent;
        }

        return Mathf.Max(0f, total);
    }

    private float GetNaturalGuardMaxGuardPercentTotal()
    {
        ResolveOwnerEnemy();
        if (_ownerEnemy)
            return Mathf.Max(0f, _enemyDefinitionMaxGuardPercent);
        return GetEquippedArmorMaxGuardPercentSum();
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

    private float GetEquippedGlobalPhysicalDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            total += def.GlobalPhysicalDamagePercent;
            total += def.PhysicalDamagePercent;
            total += def.SupportPhysicalDamagePercent;
        }
        return Mathf.Max(0f, total);
    }

    private float GetEquippedRangedPhysicalDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.RangedPhysicalDamagePercent;
        return Mathf.Max(0f, total);
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
        {
            total += def.MagicDamagePercent;
            total += def.SupportMagicDamagePercent;
        }
        return total;
    }

    private float GetEquippedFireSkillDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            total += def.FireSkillDamagePercent;
            total += def.SupportFireDamagePercent;
        }
        return Mathf.Max(0f, total);
    }

    private float GetEquippedIceSkillDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            total += def.IceSkillDamagePercent;
            total += def.SupportIceDamagePercent;
            total += def.SupportColdDamagePercent;
        }
        return Mathf.Max(0f, total);
    }

    private float GetEquippedLightningSkillDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.LightningSkillDamagePercent;
        return Mathf.Max(0f, total);
    }

    private int GetEquippedCorruptionResist()
    {
        int total = 0;
        foreach (var def in EnumerateEquippedDefs())
            total += def.CorruptionResist;
        return total;
    }

    private float GetEquippedCorruptionDamage()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
            total += def.BonusCorruptionDamage;
        return total;
    }

    private float GetEquippedCorruptionDamagePercent()
    {
        float total = 0f;
        foreach (var def in EnumerateEquippedDefs())
        {
            total += def.SupportCorruptionDamagePercent;
            total += def.EquipmentCorruptionDamagePercent;
        }
        return Mathf.Max(0f, total);
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

        float phys = SplitDamageRange.RollDamageLaneForBasicAttack(min.physical, max.physical);
        float mag = SplitDamageRange.RollDamageLaneForBasicAttack(min.magic, max.magic);
        float corr = SplitDamageRange.RollDamageLaneForBasicAttack(min.corruptionDamage, max.corruptionDamage);

        wasCrit = false;

        if (UnityEngine.Random.value < CritChance)
        {
            float crit = Mathf.Max(1f, CritMultiplier);

            // Corruption damage never benefits from crit on basic attacks; only flag crit if phys/mag scaled.
            if (phys > 0f || mag > 0f)
            {
                wasCrit = true;
                phys *= crit;
                mag *= crit;
            }
        }

        return new SplitDamage(
            Mathf.Max(0f, phys),
            Mathf.Max(0f, mag),
            Mathf.Max(0f, corr)
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
    float corruptionDamageScale)
    {
        float ap = AbilityPower;

        // Use average of min/max ranges
        float pd = (BaseMinPhysicalDamage + BaseMaxPhysicalDamage) * 0.5f + GetEquippedPhysicalDamage();
        float md = (BaseMinMagicDamage + BaseMaxMagicDamage) * 0.5f + GetEquippedMagicDamage();
        float cd = (BaseMinCorruptionDamage + BaseMaxCorruptionDamage) * 0.5f + GetEquippedCorruptionDamage();

        float weaponPart =
            (pd * physicalDamageScale) +
            (md * magicDamageScale) +
            (cd * corruptionDamageScale);
        float apMult = 1f + Mathf.Max(0f, ap) * Mathf.Max(0f, abilityPowerScale) / AbilityPowerDamagePercentDivisor;
        float result = (baseDamage + weaponPart) * apMult;

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
            if (currentGuard < 0f) currentGuard = 0f;
            _didInitialFill = true;
        }

        currentHP = Mathf.Clamp(currentHP, 0f, newMaxHP);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, newMaxEnergy);
        currentMana = Mathf.Clamp(currentMana, 0f, newMaxMana);
        if (currentGuard < 0f)
            currentGuard = 0f;
        else
            currentGuard = Mathf.Max(0f, currentGuard);

        OnHPChanged?.Invoke(currentHP, newMaxHP);
        OnEnergyChanged?.Invoke(currentEnergy, newMaxEnergy);
        OnManaChanged?.Invoke(currentMana, newMaxMana);
        RaiseGuardChanged();
        OnStatsChanged?.Invoke();
    }

    // Vitals persistence is orchestrated by PlayerSave to avoid load-order races.
    // Keep ISaveable implementation as no-op here for compatibility.
    public void SaveInto(SaveData data) { }
    public void LoadFrom(SaveData data) { }

    public void ApplyLoadedVitals(float hp, float energy)
    {
        ApplyLoadedVitals(hp, energy, currentMana >= 0f ? currentMana : MaxMana, -1f);
    }

    public void ApplyLoadedVitals(float hp, float energy, float mana)
    {
        ApplyLoadedVitals(hp, energy, mana, -1f);
    }

    public void ApplyLoadedVitals(float hp, float energy, float mana, float guard)
    {
        // Never restore into a dead state on load; dead state blocks resource spending (e.g. mana).
        currentHP = Mathf.Max(1f, hp);
        currentEnergy = energy;
        currentMana = mana;
        _ = guard;
        _didInitialFill = true;
        _hasPendingLoadedVitals = false;
        _isDead = currentHP <= 0f;
        RefreshVitalsFromStats(fillIfEmpty: false);
    }

    /// <summary>
    /// Sets guard to <see cref="NaturalGuardCap"/> (player: after save/map load; not used while dead).
    /// </summary>
    public void SnapGuardToNaturalCapOnSessionLoad()
    {
        if (_isDead)
            return;
        ResolveOwnerEnemy();
        if (!_ownerPlayer)
            return;

        currentGuard = NaturalGuardCap;
        RaiseGuardChanged();
    }

    public void SetDisplayName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        unitDisplayName = newName;
        OnNameChanged?.Invoke(unitDisplayName);
    }

    /// <summary>
    /// Applies base combat numbers from an <see cref="EnemyDefinition"/>.
    /// Enemies use the unarmed attack path (no weapon); physical damage maps to unarmed min/max.
    /// </summary>
    public void ApplyEnemyDefinition(EnemyDefinition def)
    {
        if (def == null) return;

        if (!GetComponent<EnemyBaseController>())
        {
            Debug.LogWarning("[CharacterStats] ApplyEnemyDefinition is only valid on enemies with EnemyBaseController.", this);
            return;
        }

        unitDisplayName = string.IsNullOrWhiteSpace(def.displayName) ? "Enemy" : def.displayName.Trim();

        baseMaxHP = Mathf.Max(1, def.maxHealth);
        baseMaxEnergy = Mathf.Max(0, def.maxEnergy);
        baseMaxMana = Mathf.Max(0, def.maxMana);
        baseArmor = Mathf.Max(0, def.armor);
        baseMagicResist = Mathf.Max(0, def.magicResist);
        basePhysBlockChance = Mathf.Clamp01(def.physBlockChance);

        baseMinPhysicalDamage = 0f;
        baseMaxPhysicalDamage = 0f;

        unarmedMinPhysicalDamage = Mathf.Max(0, def.minPhysicalDamage);
        unarmedMaxPhysicalDamage = Mathf.Max(unarmedMinPhysicalDamage, def.maxPhysicalDamage);

        baseMinMagicDamage = Mathf.Max(0f, def.minMagicDamage);
        baseMaxMagicDamage = Mathf.Max(baseMinMagicDamage, def.maxMagicDamage);
        baseMinCorruptionDamage = Mathf.Max(0f, def.minCorruptionDamage);
        baseMaxCorruptionDamage = Mathf.Max(baseMinCorruptionDamage, def.maxCorruptionDamage);
        baseCorruptionResist = Mathf.Max(0, def.corruptionResist);

        baseAbilityPower = Mathf.Max(0f, def.abilityPower);
        baseLifeSteal = Mathf.Clamp01(def.lifeSteal);

        unarmedAttacksPerSecond = Mathf.Max(0.01f, def.attackSpeed);
        baseMoveSpeed = Mathf.Max(0f, def.moveSpeed);
        baseMoveSpeedMult = 1f;
        unarmedRange = Mathf.Max(0.1f, def.attackRange);

        unarmedCritChance = Mathf.Clamp01(def.critChance);
        unarmedCritMultiplier = Mathf.Max(1f, def.critMultiplier);

        baseLifeRegen = Mathf.Max(0f, def.lifeRegenPerSecond);
        baseEnergyRegen = Mathf.Max(0f, def.energyRegenPerSecond);
        baseManaRegen = Mathf.Max(0f, def.manaRegenPerSecond);

        baseBleedChance = Mathf.Clamp01(def.bleedChance);
        baseBleedMultiplier = Mathf.Max(0f, def.bleedMultiplier);
        baseBleedDuration = Mathf.Max(0.1f, def.bleedDuration);

        basePoisonChance = Mathf.Clamp01(def.poisonChance);
        basePoisonMultiplier = Mathf.Max(0f, def.poisonMultiplier);
        basePoisonDuration = Mathf.Max(0.1f, def.poisonDuration);
        basePoisonMaxStacks = Mathf.Max(1, def.poisonMaxStacks);

        baseMagicAttackType = def.magicAttackType;
        baseMagicAilmentApplyChance = def.ResolveMagicAilmentApplyChance();
        baseChillDuration = Mathf.Max(0.1f, def.chillDuration);
        baseChillMaxStacks = Mathf.Max(1, def.chillMaxStacks);
        baseChillSlowPerStack = Mathf.Clamp01(def.chillSlowPerStack);
        baseBurnHitsToExplode = Mathf.Clamp(Mathf.Max(2, def.burnHitsToExplode), 2, 3);
        baseBurnExplosionMultiplier = Mathf.Max(0f, def.burnExplosionMultiplier);
        baseShockDuration = Mathf.Max(0.1f, def.shockDuration);
        baseShockDamageTakenMultiplier = Mathf.Clamp01(def.shockDamageTakenMultiplier);

        _enemyDefinitionFlatGuard = Mathf.Max(0, def.flatGuard);
        _enemyDefinitionMaxGuardPercent = Mathf.Max(0f, def.maxGuardPercent);

        currentGuard = NaturalGuardCap;
        _lastIncomingDamageTimeForGuard = Time.time;

        OnStatsChanged?.Invoke();
        OnNameChanged?.Invoke(unitDisplayName);
        RaiseGuardChanged();
    }

    /// <summary>
    /// After <see cref="ApplyEnemyDefinition"/>, scales this enemy for Elite: +100% HP, +25% outgoing damage (enemies only).
    /// </summary>
    public void ApplyEliteEnemyScaling()
    {
        if (!GetComponent<EnemyBaseController>())
        {
            Debug.LogWarning("[CharacterStats] ApplyEliteEnemyScaling is only valid on enemies with EnemyBaseController.", this);
            return;
        }

        const float hpMult = 2f;
        const float dmgMult = 1.25f;

        baseMaxHP = Mathf.Max(1, Mathf.RoundToInt(baseMaxHP * hpMult));

        unarmedMinPhysicalDamage = Mathf.Max(0, Mathf.RoundToInt(unarmedMinPhysicalDamage * dmgMult));
        unarmedMaxPhysicalDamage = Mathf.Max(
            unarmedMinPhysicalDamage,
            Mathf.RoundToInt(unarmedMaxPhysicalDamage * dmgMult));

        baseMinPhysicalDamage *= dmgMult;
        baseMaxPhysicalDamage = Mathf.Max(baseMinPhysicalDamage, baseMaxPhysicalDamage * dmgMult);

        baseMinMagicDamage *= dmgMult;
        baseMaxMagicDamage = Mathf.Max(baseMinMagicDamage, baseMaxMagicDamage * dmgMult);
        baseMinCorruptionDamage *= dmgMult;
        baseMaxCorruptionDamage = Mathf.Max(baseMinCorruptionDamage, baseMaxCorruptionDamage * dmgMult);

        baseAbilityPower *= dmgMult;

        currentHP = MaxHP;
        RefreshVitalsFromStats(fillIfEmpty: false);
        ResolveOwnerEnemy();
        if (_ownerEnemy)
        {
            currentGuard = NaturalGuardCap;
            RaiseGuardChanged();
        }

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// After <see cref="ApplyEnemyDefinition"/>, scales bases for endurance trial Tier II–V (enemies only).
    /// </summary>
    public void ApplyEnduranceTrialDifficultyScaling(float healthMult, float outgoingDamageMult, float armorMrMult)
    {
        ResolveOwnerEnemy();
        if (!_ownerEnemy)
            return;

        healthMult = Mathf.Max(0.01f, healthMult);
        outgoingDamageMult = Mathf.Max(0.01f, outgoingDamageMult);
        armorMrMult = Mathf.Max(0.01f, armorMrMult);

        baseMaxHP = Mathf.Max(1, Mathf.RoundToInt(baseMaxHP * healthMult));
        baseArmor = Mathf.Max(0, Mathf.RoundToInt(baseArmor * armorMrMult));
        baseMagicResist = Mathf.Max(0, Mathf.RoundToInt(baseMagicResist * armorMrMult));
        baseCorruptionResist = Mathf.Max(0, Mathf.RoundToInt(baseCorruptionResist * armorMrMult));

        unarmedMinPhysicalDamage = Mathf.Max(0, Mathf.RoundToInt(unarmedMinPhysicalDamage * outgoingDamageMult));
        unarmedMaxPhysicalDamage = Mathf.Max(
            unarmedMinPhysicalDamage,
            Mathf.RoundToInt(unarmedMaxPhysicalDamage * outgoingDamageMult));

        baseMinPhysicalDamage *= outgoingDamageMult;
        baseMaxPhysicalDamage = Mathf.Max(baseMinPhysicalDamage, baseMaxPhysicalDamage * outgoingDamageMult);

        baseMinMagicDamage *= outgoingDamageMult;
        baseMaxMagicDamage = Mathf.Max(baseMinMagicDamage, baseMaxMagicDamage * outgoingDamageMult);
        baseMinCorruptionDamage *= outgoingDamageMult;
        baseMaxCorruptionDamage = Mathf.Max(baseMinCorruptionDamage, baseMaxCorruptionDamage * outgoingDamageMult);

        baseAbilityPower *= outgoingDamageMult;

        // InitializeFromDefinition already set currentHP to Tier-I max; fillIfEmpty only replaces HP when currentHP < 0.
        // Re-fill to the new scaled max so UI shows correct current/max (e.g. 150/150 not 100/150).
        currentHP = MaxHP;
        RefreshVitalsFromStats(fillIfEmpty: false);
        currentGuard = NaturalGuardCap;
        RaiseGuardChanged();
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

        TickGuardRegen(dt);
    }

    private void TickGuardRegen(float dt)
    {
        if (_isDead || dt <= 0f)
            return;

        ResolveOwnerEnemy();

        if (_ownerPlayer)
        {
            bool inCombat = _playerCombatState != null && _playerCombatState.InCombat;
            if (inCombat)
            {
                _guardPeaceTimer = 0f;
                _wasInCombatForGuardTimer = true;
                return;
            }

            if (_wasInCombatForGuardTimer)
            {
                _guardPeaceTimer = 0f;
                _wasInCombatForGuardTimer = false;
            }

            _guardPeaceTimer += dt;
            if (_guardPeaceTimer < GuardOutOfCombatSecondsBeforeRegen)
                return;
        }
        else if (_ownerEnemy)
        {
            if (Time.time - _lastIncomingDamageTimeForGuard < GuardOutOfCombatSecondsBeforeRegen)
                return;
        }
        else
            return;

        float cap = NaturalGuardCap;
        if (cap <= 0f && currentGuard <= 0.0001f)
            return;

        bool changed = false;

        if (currentGuard < cap - 0.0001f && cap > 0f)
        {
            float add = GuardRegenOrDecayPerSecondFraction * cap * dt;
            float newG = Mathf.Min(cap, currentGuard + add);
            if (!Mathf.Approximately(newG, currentGuard))
            {
                currentGuard = newG;
                changed = true;
            }
        }
        else if (currentGuard > cap + 0.0001f)
        {
            float decayBase = cap > 0.0001f ? cap : Mathf.Max(1f, currentGuard);
            float sub = GuardRegenOrDecayPerSecondFraction * decayBase * dt;
            float newG = Mathf.Max(cap, currentGuard - sub);
            if (!Mathf.Approximately(newG, currentGuard))
            {
                currentGuard = newG;
                changed = true;
            }
        }

        if (changed)
            RaiseGuardChanged();
    }

    private void RaiseGuardChanged()
    {
        OnGuardChanged?.Invoke(Guard, NaturalGuardCap);
    }

    private void NotifyGuardAbsorbedCombatClock()
    {
        if (!_ownerPlayer)
            return;

        PlayerCombatController pcc = _ownerPlayer.GetComponent<PlayerCombatController>();
        if (pcc)
            pcc.NotifyNonHpCombatInteraction();
    }

    /// <summary>Future: temporary guard from abilities (may exceed <see cref="NaturalGuardCap"/>).</summary>
    public void AddBonusGuard(float amount)
    {
        if (_isDead || amount <= 0f)
            return;

        currentGuard = Mathf.Max(0f, currentGuard + amount);
        RaiseGuardChanged();
    }

    public void ClampGuardToNaturalCap()
    {
        float cap = NaturalGuardCap;
        float clamped = Mathf.Clamp(currentGuard, 0f, cap);
        if (Mathf.Approximately(currentGuard, clamped))
        {
            RaiseGuardChanged();
            return;
        }

        currentGuard = clamped;
        RaiseGuardChanged();
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

    /// <returns>Damage that reached guard and/or HP after mitigation (for popups and combat totals). Use <paramref name="hpDamageDealt"/> for HP-only effects.</returns>
    public float TakeDamage(float amount, DamageType type, out bool blocked, out float hpDamageDealt)
    {
        blocked = false;
        hpDamageDealt = 0f;
        if (_isDead) return 0f;

        float mitigated = ApplyMitigation(amount, type, out blocked);
        float totalToVitals = Mathf.Max(0f, mitigated);
        float remainder = totalToVitals;
        hpDamageDealt = ApplyDamageToGuardThenHp(ref remainder);
        OnHPChanged?.Invoke(currentHP, MaxHP);

        if (currentHP <= 0f && !_isDead)
        {
            _isDead = true;
            currentGuard = 0f;
            RaiseGuardChanged();
            OnDied?.Invoke();
        }

        return totalToVitals;
    }

    /// <summary>
    /// Applies DoT (or other pre-resolved) damage: amount is already the intended tick total;
    /// only global melee damage reduction applies (no armor / MR / corruption resist).
    /// </summary>
    /// <returns>Damage that reached guard and/or HP after reductions (same semantics as <see cref="TakeDamage"/> return value).</returns>
    public float TakeDamageFromResolvedDot(float amount, out bool blocked)
    {
        blocked = false;
        if (_isDead) return 0f;

        float mitigated = ApplyFlatDamageTakenReduction(
            ApplyMeleeDamageReduction(Mathf.Max(0f, amount)),
            GetConsumableFlatDamageReductionFraction());
        float totalToVitals = Mathf.Max(0f, mitigated);
        float remainder = totalToVitals;
        ApplyDamageToGuardThenHp(ref remainder);
        OnHPChanged?.Invoke(currentHP, MaxHP);

        if (currentHP <= 0f && !_isDead)
        {
            _isDead = true;
            currentGuard = 0f;
            RaiseGuardChanged();
            OnDied?.Invoke();
        }

        return totalToVitals;
    }

    public void ReviveFull()
    {
        _isDead = false;
        currentHP = MaxHP;
        currentEnergy = MaxEnergy;
        currentMana = MaxMana;
        currentGuard = NaturalGuardCap;
        OnHPChanged?.Invoke(currentHP, MaxHP);
        OnEnergyChanged?.Invoke(currentEnergy, MaxEnergy);
        OnManaChanged?.Invoke(currentMana, MaxMana);
        RaiseGuardChanged();
    }

    /// <summary>
    /// Consumes <paramref name="damage"/> against guard first, then HP. Mutates <paramref name="damage"/> to HP loss only.
    /// </summary>
    private float ApplyDamageToGuardThenHp(ref float damage)
    {
        damage = Mathf.Max(0f, damage);
        if (damage <= 0f)
            return 0f;

        float guardBefore = currentGuard;
        float absorb = Mathf.Min(guardBefore, damage);
        if (absorb > 0f)
        {
            currentGuard = Mathf.Max(0f, guardBefore - absorb);
            damage -= absorb;
            NotifyGuardAbsorbedCombatClock();
            RaiseGuardChanged();
        }

        float hpLoss = damage;
        currentHP = Mathf.Clamp(currentHP - hpLoss, 0f, MaxHP);

        ResolveOwnerEnemy();
        if (_ownerEnemy && (absorb > 0f || hpLoss > 0.0001f))
            _lastIncomingDamageTimeForGuard = Time.time;

        return hpLoss;
    }

    private float GetConsumableDefenseBoostRatingMultiplier()
    {
        return 1f + (buffController ? buffController.GetTotalMagnitude(ConsumableEffectType.DefenseBoost) : 0f);
    }

    private float GetConsumableFlatDamageReductionFraction()
    {
        return buffController ? Mathf.Clamp01(buffController.GetTotalMagnitude(ConsumableEffectType.DamageReduction)) : 0f;
    }

    private static float ApplyFlatDamageTakenReduction(float damage, float reduction01)
    {
        if (reduction01 <= 0f || damage <= 0f)
            return damage;
        return damage * (1f - reduction01);
    }

    private float ApplyMitigation(float rawDamage, DamageType type, out bool blocked)
    {
        blocked = false;

        rawDamage = Mathf.Max(0f, rawDamage);
        if (rawDamage <= 0f) return 0f;

        float defMult = GetConsumableDefenseBoostRatingMultiplier();
        float consumableDr = GetConsumableFlatDamageReductionFraction();

        switch (type)
        {
            case DamageType.Typless:
                // True untyped damage: no block chance, no mitigation, no reduction modifiers.
                return rawDamage;

            case DamageType.Corruption:
                return ApplyFlatDamageTakenReduction(
                    ApplyMeleeDamageReduction(MitigateByRating(rawDamage, CorruptionResist * defMult)),
                    consumableDr);

            case DamageType.Physical:
                {
                    float dmg = MitigateByRating(rawDamage, Armor * defMult);

                    if (PhysBlockChance > 0f && UnityEngine.Random.value < Mathf.Clamp01(PhysBlockChance))
                    {
                        blocked = true;
                        return 0f;
                    }

                    return ApplyFlatDamageTakenReduction(ApplyMeleeDamageReduction(dmg), consumableDr);
                }

            case DamageType.Magic:
                return ApplyFlatDamageTakenReduction(
                    ApplyMeleeDamageReduction(MitigateByRating(rawDamage, MagicResist * defMult)),
                    consumableDr);

            default:
                return ApplyFlatDamageTakenReduction(ApplyMeleeDamageReduction(rawDamage), consumableDr);
        }
    }

    private float ApplyMeleeDamageReduction(float incomingDamage)
    {
        float reduction = Mathf.Clamp01(GetActiveMeleeMinorBonuses().meleeDamageReduction);
        return incomingDamage * (1f - reduction);
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