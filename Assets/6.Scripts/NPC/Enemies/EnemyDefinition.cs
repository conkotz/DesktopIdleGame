using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// How elite enemies resolve item loot vs the base <see cref="EnemyDefinition.loot"/> table.
/// </summary>
public enum EnemyEliteLootHandling
{
    [Tooltip("Always roll the base loot table. When elite, multiply each row's drop chance by Elite Loot Chance Multiplier (capped at 1).")]
    ScaleBaseLootChances = 0,

    [Tooltip("Non-elite: base table only. Elite: only rows under Elite Loot (base table ignored for item drops).")]
    EliteLootTableOnly = 1,

    [Tooltip("Roll the base table (elite: chances scaled). If elite, also roll Elite Loot rows.")]
    ScaledBasePlusExtraEliteEntries = 2,
}

/// <summary>
/// One independent item roll when an enemy dies (same rules as endurance trial loot rows).
/// </summary>
[Serializable]
public class EnemyLootEntry : ISerializationCallbackReceiver
{
    [Tooltip("Item granted to the player inventory when this row succeeds its roll.")]
    public ItemDefinition item;

    [Min(1)]
    [Tooltip("Minimum stack when this entry succeeds.")]
    public int amountMin = 1;

    [Min(1)]
    [Tooltip("Maximum stack (inclusive). Must be >= Amount Min.")]
    public int amountMax = 1;

    [Range(0f, 1f)]
    [Tooltip("Independent chance this row succeeds (0 = never, 1 = always). Each row rolls separately.")]
    public float dropChance = 1f;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (amountMax < amountMin)
            amountMax = amountMin;
    }
}

/// <summary>
/// Data-only enemy template: identity, base combat values, and prefab reference.
/// Visuals and behaviour live on the prefab; this asset is the source of truth for tunable stats.
/// </summary>
[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Desktop Idle Game/Enemy Definition", order = 50)]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable id for saves, analytics, and encounter tables (e.g. goblin_grunt).")]
    public string enemyId = "";

    [Tooltip("Shown in overhead UI and tooltips.")]
    public string displayName = "Enemy";

    [TextArea(2, 6)]
    [Tooltip("Longer description for journals / bestiary / tooltips.")]
    public string description = "";

    [Tooltip("Small portrait or list icon.")]
    public Sprite icon;

    [Tooltip("Prefab with EnemyBaseController, CharacterStats, visuals, and colliders.")]
    public GameObject prefab;

    [Header("Spawning / persistence")]
    [Tooltip(
        "When true, this enemy's map spawn slot is remembered as cleared after death: it will not respawn on reload. " +
        "Cleared when starting a New Game (save reset). Uses LevelSpawnDirector spawn-plan keys.")]
    public bool cannotRespawn = false;

    [Tooltip(
        "When true, this enemy is omitted from recommended combat power for map nodes / level select (spawn plans and endurance waves). " +
        "Use for target dummies or other non-threat spawns so they do not inflate suggested CP.")]
    public bool doesNotContributeToRecommendedCombatPower = false;

    [Header("Vitals (base)")]
    [Min(1)]
    [Tooltip("Maps to CharacterStats baseMaxHP.")]
    public int maxHealth = 50;

    [Min(0)]
    [Tooltip("Maps to CharacterStats baseMaxEnergy.")]
    public int maxEnergy = 100;

    [Min(0)]
    [Tooltip("Maps to CharacterStats baseMaxMana.")]
    public int maxMana = 50;

    [Header("Defense")]
    [Min(0)]
    public int armor = 0;

    [Min(0)]
    public int magicResist = 0;

    [Min(0)]
    public int corruptionResist = 0;

    [Range(0f, 1f)]
    [Tooltip("Maps to CharacterStats basePhysBlockChance.")]
    public float physBlockChance = 0f;

    [Header("Guard (same rules as player armor)")]
    [Min(0)]
    [Tooltip("Flat guard pool cap contribution; replenish cap is min(this, Max HP × (1 + Max Guard %))).")]
    public int flatGuard = 0;

    [Tooltip("Additive fraction above Max HP for the guard ceiling (0.1 = +10%, i.e. cap from HP is 110% of Max HP).")]
    [Min(0f)]
    public float maxGuardPercent = 0f;

    [Header("Direct damage (base)")]
    [Tooltip("Physical portion uses unarmed min/max on CharacterStats.")]
    [Min(0)]
    public int minPhysicalDamage = 1;

    [Min(0)]
    public int maxPhysicalDamage = 2;

    [Min(0)]
    public float minMagicDamage = 0f;

    [Min(0)]
    public float maxMagicDamage = 0f;

    [Min(0)]
    [FormerlySerializedAs("minTrueDamage")]
    public float minCorruptionDamage = 0f;

    [Min(0)]
    [FormerlySerializedAs("maxTrueDamage")]
    public float maxCorruptionDamage = 0f;

    [Header("Combat cadence")]
    [Tooltip("Attacks per second (unarmed APS on CharacterStats).")]
    [Min(0.01f)]
    public float attackSpeed = 1f;

    [Tooltip("Chase speed on EnemyBaseController; also feeds combat power mobility. Use 0 for stationary enemies.")]
    [Min(0f)]
    public float moveSpeed = 2.5f;

    [Tooltip("Melee reach when the enemy has no equipped weapon.")]
    [Min(0.1f)]
    public float attackRange = 1f;

    [Range(0f, 1f)]
    [Tooltip("Crit chance for physical/magic hits (corruption never crits on basic attacks).")]
    public float critChance = 0f;

    [Min(1f)]
    public float critMultiplier = 1.5f;

    [Header("Movement & regen (base)")]
    [Min(0f)]
    [Tooltip("Maps to CharacterStats baseLifeRegen.")]
    public float lifeRegenPerSecond = 0f;

    [Min(0f)]
    [Tooltip("Maps to CharacterStats baseEnergyRegen.")]
    public float energyRegenPerSecond = 10f;

    [Min(0f)]
    [Tooltip("Maps to CharacterStats baseManaRegen.")]
    public float manaRegenPerSecond = 1f;

    [Header("Offense — ability & drain")]
    [Min(0f)]
    [Tooltip("Maps to CharacterStats baseAbilityPower.")]
    public float abilityPower = 0f;

    [Range(0f, 1f)]
    [Tooltip("Maps to CharacterStats baseLifeSteal.")]
    public float lifeSteal = 0f;

    [Header("Bleed (base)")]
    [Range(0f, 1f)]
    public float bleedChance = 0f;

    [Min(0f)]
    public float bleedMultiplier = 0f;

    [Min(0.1f)]
    public float bleedDuration = 5f;

    [Header("Poison (base)")]
    [Range(0f, 1f)]
    public float poisonChance = 0f;

    [Min(0f)]
    public float poisonMultiplier = 0f;

    [Min(0.1f)]
    public float poisonDuration = 5f;

    [Min(1)]
    public int poisonMaxStacks = 3;

    [Header("Elemental ailments")]
    [Tooltip("Which magic element drives burn / chill / shock logic for this enemy.")]
    public MagicAttackType magicAttackType = MagicAttackType.Lightning;

    [Range(0f, 1f)]
    [Tooltip("Chance to apply burn when magic type is Fire (combined with runtime magic-hit rules).")]
    public float burnChance = 0f;

    [Range(0f, 1f)]
    [Tooltip("Chance to apply chill when magic type is Ice.")]
    public float chillChance = 0f;

    [Range(0f, 1f)]
    [Tooltip("Chance to apply shock when magic type is Lightning.")]
    public float shockChance = 0f;

    [Min(0.1f)]
    [Tooltip("Maps to CharacterStats baseChillDuration.")]
    public float chillDuration = 5f;

    [Min(1)]
    [Tooltip("Maps to CharacterStats baseChillMaxStacks.")]
    public int chillMaxStacks = 6;

    [Range(0f, 1f)]
    [Tooltip("Maps to CharacterStats baseChillSlowPerStack.")]
    public float chillSlowPerStack = 0.15f;

    [Min(2)]
    [Tooltip("Maps to CharacterStats baseBurnHitsToExplode (clamped to 3 stacks combust in combat).")]
    public int burnHitsToExplode = 3;

    [Min(0f)]
    [Tooltip("Maps to CharacterStats burn tick damage multiplier (15% of strongest fire hit per tick, min 1).")]
    public float burnExplosionMultiplier = 1f;

    [Min(0.1f)]
    [Tooltip("Maps to CharacterStats baseShockDuration.")]
    public float shockDuration = 5f;

    [Range(0f, 1f)]
    [Tooltip("Maps to CharacterStats baseShockDamageTakenMultiplier.")]
    public float shockDamageTakenMultiplier = 0.15f;

    [Header("Unique Effects / Special")]
    [Tooltip("When enabled, this enemy deals periodic typless damage while the player is within Deadly Distance.")]
    public bool deadlyAtCloseRange = false;

    [Min(0.1f)]
    [Tooltip("Edge-to-edge horizontal range required to trigger Deadly at close range.")]
    public float deadlyCloseRangeDistance = 3f;

    [Min(0f)]
    [Tooltip("Typless damage dealt each deadly close-range pulse. Uses true damage rules (unmitigable, unblockable).")]
    public float deadlyCloseRangeTyplessDamage = 0f;

    [Header("Damage Immunities")]
    [Tooltip("Ignore all incoming melee attack hits.")]
    public bool immuneToMeleeDamage = false;

    [Tooltip("Ignore all incoming ranged attack hits.")]
    public bool immuneToRangedDamage = false;

    [Tooltip("Ignore all incoming magic attack hits.")]
    public bool immuneToMagicDamage = false;

    [Header("Idle wander (optional)")]
    [Tooltip("When not aggroed, pace horizontally around the spawn point using Max Walk Distance From Spawn.")]
    public bool idleWanderEnabled = false;

    [Tooltip("Horizontal speed during each wander move burst (independent of chase moveSpeed). Use 0 to disable even if enabled above.")]
    [Min(0f)]
    public float idleWanderSpeed = 0.8f;

    [Tooltip("Duration of each horizontal move burst while idle (seconds).")]
    [Min(0.05f)]
    public float idleWanderMoveMinSec = 1f;

    [Min(0.05f)]
    public float idleWanderMoveMaxSec = 3f;

    [Tooltip("Duration standing still between move bursts (seconds).")]
    [Min(0.05f)]
    public float idleWanderIdleMinSec = 2f;

    [Min(0.05f)]
    public float idleWanderIdleMaxSec = 8f;

    [Tooltip("Maximum horizontal distance from the enemy's spawn position while idling. Enemy turns around at this limit.")]
    [Min(0f)]
    public float idleWanderMaxDistanceFromSpawn = 10f;

    [Header("Experience")]
    [Tooltip("When false, damaging this enemy grants no combat XP. Use for training dummies and test targets.")]
    public bool grantCombatXp = true;

    [Header("Gold drop")]
    [Tooltip("When false, this enemy awards no gold on death.")]
    public bool dropGold = true;

    [Min(0)]
    public int goldMin = 1;

    [Min(0)]
    public int goldMax = 5;

    [Range(0f, 1f)]
    [Tooltip("Chance the gold payout runs at all (before rolling min–max).")]
    public float goldDropChance = 1f;

    [Tooltip("World offset for the floating +gold popup.")]
    public Vector3 goldPopupWorldOffset = new Vector3(0f, 1.2f, 0f);

    [Min(1f)]
    [Tooltip("Applied to the rolled gold amount when this spawn is Elite (see MapNodeDefinition.eliteSpawnChance). Ignored for legacy prefabs with no definition.")]
    public float eliteGoldMultiplier = 2f;

    [Header("Item loot (on death)")]
    [Tooltip("Each row rolls independently when the enemy dies. Empty = no item drops from data.")]
    public List<EnemyLootEntry> loot = new();

    [Header("Elite — item loot")]
    [Tooltip("See enum tooltips. Use Elite Loot Table Only to replace base drops; otherwise scale chances or add extra rows.")]
    public EnemyEliteLootHandling eliteLootHandling = EnemyEliteLootHandling.ScaleBaseLootChances;

    [Min(0f)]
    [Tooltip("When handling scales base chances and this enemy is Elite: effective chance = min(1, row.dropChance × this).")]
    public float eliteLootChanceMultiplier = 2f;

    [Tooltip("Used when handling is Elite Table Only (elite only) or Scaled Base + Extra (elite only).")]
    public List<EnemyLootEntry> eliteLoot = new();

    [Header("Notes")]
    [TextArea(2, 8)]
    [Tooltip("Internal notes for designers; not shown in gameplay.")]
    public string designerNotes = "";

    /// <summary>
    /// Maps <see cref="magicAttackType"/> to the correct chance field for CharacterStats.
    /// </summary>
    public float ResolveMagicAilmentApplyChance()
    {
        switch (magicAttackType)
        {
            case MagicAttackType.Fire:
                return Mathf.Clamp01(burnChance);
            case MagicAttackType.Ice:
                return Mathf.Clamp01(chillChance);
            case MagicAttackType.Lightning:
            default:
                return Mathf.Clamp01(shockChance);
        }
    }

    /// <summary>Prefab to spawn; encounter systems can reference this asset and call <see cref="prefab"/>.</summary>
    public GameObject ResolveSpawnPrefab() => prefab;
}
