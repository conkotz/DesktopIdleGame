using UnityEngine;
using UnityEngine.Serialization;

public enum AbilityWeaponRequirement
{
    Any,
    Melee,
    Ranged,
    Magic,
    /// <summary>Physical weapon attacks only: melee or ranged (excludes magic weapons).</summary>
    [InspectorName("Melee or Ranged")]
    MeleeOrRanged
}

/// <summary>
/// High-level ability category displayed as a tag line above the description in tooltips
/// (e.g. "Active", "Minion", "Buff", "Toggle Buff"). <see cref="None"/> hides the tag line entirely.
/// </summary>
public enum AbilityTag
{
    None,
    Active,
    Minion,
    Buff,
    [InspectorName("Toggle Buff")]
    ToggleBuff
}

[CreateAssetMenu(fileName = "Ability_", menuName = "Desktop Idle Game/Skills/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    public string abilityId;
    public string displayName;
    [Tooltip("High-level category label shown above the tooltip description (Active / Minion / Buff / Toggle Buff). None hides the label.")]
    public AbilityTag tag = AbilityTag.None;

    [Tooltip("Buff / Minion: BASE seconds for the league tooltip Duration line (and Lumber Frenzy runtime when > 0). Enhancement bonuses from skill choices are added on top in AbilityTooltipDamagePreview. Use 0 to use the code default base for that ability.")]
    [Min(0f)]
    public float tooltipBuffMinionDurationSeconds;

    [Header("Presentation (optional)")]
    [Tooltip("Icon + wording (name, short line, optional prose). Effect bullets and scaling stay in AbilityTooltipDamagePreview for each abilityId.")]
    public SkillsAndAbilityPresentationDefinition presentation;

    public float cooldown = 1f;
    [Min(0f)] public float energyCost = 0f;

    [Header("Scaling")]
    [Tooltip(
        "Scales Physical, Magic, and Corruption portions of the weapon hit equally (1.5 = 150% of each rolled type). " +
        "≤0 leaves each portion at 100% of the hit. Rend / Envenom / minions should stay at 0.")]
    [FormerlySerializedAs("physicalDamageMultiplier")]
    public float weaponDamageMultiplier = 0f;

    [Tooltip("Applied after weapon scaling: entire hit (all types combined) × this. 1 = no change. ≤0 treated as 1.")]
    public float allDamageMultiplier = 1f;

    /// <summary>All abilities use the same AP curve: ×(1 + AP×coef/100) with this coefficient (0.5 = +0.5% damage per AP).</summary>
    public const float StandardAbilityPowerCoefficient = 0.5f;

    [Tooltip("Extra scaling on magic damage when the character's magic attack type is Fire (see CharacterStats CurrentMagicAttackType). 0 = ignore.")]
    [Min(0f)] public float fireDamageMultiplier = 0f;

    [Tooltip("Extra scaling when magic attack type is Ice. 0 = ignore.")]
    [Min(0f)] public float iceDamageMultiplier = 0f;

    [Tooltip("Extra scaling when magic attack type is Lightning. 0 = ignore.")]
    [Min(0f)] public float lightningDamageMultiplier = 0f;

    [Tooltip("CP / scaling hook: weights expected poison DPS contribution from this ability (0 = none).")]
    [Min(0f)] public float poisonDamageMultiplier = 0f;

    [Tooltip("CP / scaling hook: weights expected bleed DPS contribution from this ability (0 = none).")]
    [Min(0f)] public float bleedDamageMultiplier = 0f;

    [Header("Weapon Requirement")]
    [Tooltip("Restricts this ability to a matching equipped main-hand weapon type.")]
    public AbilityWeaponRequirement requiredWeaponType = AbilityWeaponRequirement.Any;

    public SkillType sourceSkill;
    public int unlockLevel = 1;

    [Header("Summon (optional)")]
    [Tooltip(
        "When set, this ability uses the summon path in PlayerAbilityController: energy on first spawn, SoulforgedWeaponMinion is spawned, " +
        "and ability cooldown starts when the summon expires (recast while active only retargets). " +
        "Scaling fields above are not used for that path (damage comes from MinionDefinition / MinionCombatConfig + owner minion stats).")]
    public MinionDefinition minionSpawnDefinition;

    /// <summary>True when casting should spawn a minion instead of the generic instant-hit damage pipeline.</summary>
    public bool SpawnsMinionOnCast => minionSpawnDefinition != null;

    /// <summary>≤0 uses 1 (legacy / Inspector 0): no extra all-damage pass; weapon coeff only.</summary>
    public float GetEffectiveAllDamageMultiplier() =>
        allDamageMultiplier <= 0f ? 1f : allDamageMultiplier;

    /// <summary>&gt;0 scales rolled Physical, Magic, and Corruption equally; ≤0 keeps 100% of each rolled type.</summary>
    public float GetWeaponHitScalingMultiplier() =>
        weaponDamageMultiplier <= 0f ? 1f : weaponDamageMultiplier;
}
