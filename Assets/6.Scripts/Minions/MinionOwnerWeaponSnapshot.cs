/// <summary>
/// Weapon and ailment scalars copied from <see cref="CharacterStats"/> at minion summon so gear swaps
/// mid-summon do not change that instance's on-hit behavior.
/// </summary>
public struct MinionOwnerWeaponSnapshot
{
    public bool currentAttackAppliesAsFireForBurn;
    public float burnExplosionMultiplier;
    public AttackSkill currentAttackSkill;
    public MagicAttackType currentMagicAttackType;
    public float chillDuration;
    public int chillMaxStacks;
    public float chillSlowPerStack;
    public float shockDuration;
    public float shockDamageTakenMultiplier;
    public float meleeMagicLightningFraction;
    public float bleedDuration;
    public float bleedMultiplier;
    public float bleedBaseDuration;
    public float poisonMultiplier;
    public float poisonPoolFractionOfCorruptionDamage;
    public float poisonDuration;
    public int poisonMaxStacks;

    public static MinionOwnerWeaponSnapshot From(CharacterStats owner)
    {
        if (!owner)
            return default;

        return new MinionOwnerWeaponSnapshot
        {
            currentAttackAppliesAsFireForBurn = owner.CurrentAttackAppliesAsFireForBurn,
            burnExplosionMultiplier = owner.BurnExplosionMultiplier,
            currentAttackSkill = owner.CurrentAttackSkill,
            currentMagicAttackType = owner.CurrentMagicAttackType,
            chillDuration = owner.ChillDuration,
            chillMaxStacks = owner.ChillMaxStacks,
            chillSlowPerStack = owner.ChillSlowPerStack,
            shockDuration = owner.ShockDuration,
            shockDamageTakenMultiplier = owner.ShockDamageTakenMultiplier,
            meleeMagicLightningFraction = owner.GetMeleeMagicLightningFraction(),
            bleedDuration = owner.BleedDuration,
            bleedMultiplier = owner.BleedMultiplier,
            bleedBaseDuration = owner.BleedBaseDuration,
            poisonMultiplier = owner.PoisonMultiplier,
            poisonPoolFractionOfCorruptionDamage = owner.PoisonPoolFractionOfCorruptionDamage,
            poisonDuration = owner.PoisonDuration,
            poisonMaxStacks = owner.PoisonMaxStacks,
        };
    }
}
