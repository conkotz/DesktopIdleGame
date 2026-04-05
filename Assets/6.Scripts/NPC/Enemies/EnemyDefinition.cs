using UnityEngine;

/// <summary>
/// Data-only enemy template: identity, base combat values, and prefab reference.
/// Visuals and behaviour live on the prefab; this asset is the source of truth for tunable stats.
/// </summary>
[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Game/Enemy Definition", order = 50)]
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

    [Range(0f, 1f)]
    [Tooltip("Maps to CharacterStats basePhysBlockChance.")]
    public float physBlockChance = 0f;

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
    public float minTrueDamage = 0f;

    [Min(0)]
    public float maxTrueDamage = 0f;

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
    [Tooltip("Crit chance for physical/magical hits (true damage never crits).")]
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
    [Tooltip("Maps to CharacterStats baseBurnHitsToExplode.")]
    public int burnHitsToExplode = 4;

    [Min(0f)]
    [Tooltip("Maps to CharacterStats baseBurnExplosionMultiplier.")]
    public float burnExplosionMultiplier = 0.5f;

    [Min(0.1f)]
    [Tooltip("Maps to CharacterStats baseShockDuration.")]
    public float shockDuration = 5f;

    [Range(0f, 1f)]
    [Tooltip("Maps to CharacterStats baseShockDamageTakenMultiplier.")]
    public float shockDamageTakenMultiplier = 0.15f;

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
