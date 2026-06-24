using UnityEngine;

/// <summary>
/// Serialized per-tier multipliers for endurance trials (enemy health, damage, armour/MR).
/// Create via Assets → Create → Desktop Idle Game → Endurance Trial Difficulty Scaling.
/// Assign on <see cref="EnduranceTrialDifficultyBootstrap"/> or call <see cref="EnduranceTrialTier.SetDifficultyScaling"/>.
/// Default values match the built-in formula: +50% HP and +25% damage/armour per step above Tier I.
/// </summary>
[CreateAssetMenu(fileName = "EnduranceTrialDifficultyScaling", menuName = "Desktop Idle Game/Endurance Trial Difficulty Scaling")]
public class EnduranceTrialDifficultyScaling : ScriptableObject
{
    private const int TierCount = 5;

    [Tooltip("Health multiplier vs Tier I (five entries: Tier I … Tier V).")]
    [SerializeField] private float[] healthMultiplierPerTier = { 1f, 1.5f, 2f, 2.5f, 3f };

    [Tooltip("Outgoing damage multiplier vs Tier I.")]
    [SerializeField] private float[] damageMultiplierPerTier = { 1f, 1.25f, 1.5f, 1.75f, 2f };

    [Tooltip("Armour and magic resist multiplier vs Tier I.")]
    [SerializeField] private float[] armourAndResistMultiplierPerTier = { 1f, 1.25f, 1.5f, 1.75f, 2f };

    public bool TryGetHealthMultiplier(int tier1Based, out float mult) =>
        TryGet(healthMultiplierPerTier, tier1Based, out mult);

    public bool TryGetDamageMultiplier(int tier1Based, out float mult) =>
        TryGet(damageMultiplierPerTier, tier1Based, out mult);

    public bool TryGetArmourAndResistMultiplier(int tier1Based, out float mult) =>
        TryGet(armourAndResistMultiplierPerTier, tier1Based, out mult);

    private static bool TryGet(float[] arr, int tier1Based, out float mult)
    {
        mult = 1f;
        if (arr == null || arr.Length < TierCount)
            return false;

        int idx = EnduranceTrialTier.ToTierIndex(tier1Based);
        if (idx < 0 || idx >= arr.Length)
            return false;

        mult = Mathf.Max(0.01f, arr[idx]);
        return true;
    }

    private void OnValidate()
    {
        if (healthMultiplierPerTier == null || healthMultiplierPerTier.Length != TierCount)
            Debug.LogWarning("[EnduranceTrialDifficultyScaling] healthMultiplierPerTier must have exactly 5 elements.", this);
        if (damageMultiplierPerTier == null || damageMultiplierPerTier.Length != TierCount)
            Debug.LogWarning("[EnduranceTrialDifficultyScaling] damageMultiplierPerTier must have exactly 5 elements.", this);
        if (armourAndResistMultiplierPerTier == null || armourAndResistMultiplierPerTier.Length != TierCount)
            Debug.LogWarning("[EnduranceTrialDifficultyScaling] armourAndResistMultiplierPerTier must have exactly 5 elements.", this);
    }

    [ContextMenu("Reset arrays to default formula (Tier I–V)")]
    private void ResetToDefaultFormula()
    {
        healthMultiplierPerTier = new float[TierCount];
        damageMultiplierPerTier = new float[TierCount];
        armourAndResistMultiplierPerTier = new float[TierCount];
        for (int i = 0; i < TierCount; i++)
        {
            healthMultiplierPerTier[i] = 1f + 0.5f * i;
            damageMultiplierPerTier[i] = 1f + 0.25f * i;
            armourAndResistMultiplierPerTier[i] = 1f + 0.25f * i;
        }
    }
}
