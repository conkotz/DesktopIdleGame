using UnityEngine;

/// <summary>
/// Minion-local on-hit ailment rolls. Not merged with the owner&apos;s ailment stats by default.
/// Phase 3+: wire into combat when minion hit pipelines support ailments.
/// </summary>
[System.Serializable]
public struct MinionAilmentChances
{
    [Range(0f, 1f)]
    [Tooltip("Independent poison stack / DoT application chance for this minion's hits.")]
    public float poisonChance;

    [Range(0f, 1f)]
    public float bleedChance;

    [Range(0f, 1f)]
    public float burnChance;

    [Range(0f, 1f)]
    public float shockChance;

    public static MinionAilmentChances Zero => default;
}
