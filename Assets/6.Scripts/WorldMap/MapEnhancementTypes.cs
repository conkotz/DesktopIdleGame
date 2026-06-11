using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapEnhancementModType
{
    RespawnTimeReduction = 0,
    ExtraEnemySpawns = 1,
    LootBonus = 2,
    EnemyDamageReduction = 3,
    GoldBonus = 4,
    EliteSpawnChanceBonus = 5,
    EliteSpawnDouble = 6,
    EliteHealthReduction = 7
}

public enum MapEnhancementTier
{
    Tier1 = 1,
    Tier2 = 2
}

/// <summary>Per-modifier roll range and weight for map enhancement template items.</summary>
[Serializable]
public struct MapEnhancementModRollConfig
{
    public MapEnhancementModType modType;

    [Min(0f)]
    [Tooltip("Relative chance when rolling modifiers. 0 = never rolled. Higher = more common.")]
    public float rollWeight;

    [Tooltip("Respawn: seconds reduced. Extra spawns: count. Loot/Gold/Damage/Elite health: fraction (0.10 = 10%). Elite spawn chance: relative bonus (0.10 = +10% of base chance). Elite double: flat chance (0.05 = 5%).")]
    public float minValue;

    public float maxValue;

    public bool IsEnabled => rollWeight > 0.001f && maxValue >= minValue;
}

[Serializable]
public struct MapEnhancementMod
{
    public MapEnhancementModType modType;
    public float value;
    [Tooltip("Used when modType is ExtraEnemySpawns.")]
    public string extraSpawnEnemyId;
}

[Serializable]
public class MapEnhancementInstanceData
{
    public string itemId;
    public string baseItemId;
    public string displayName;
    public string sourceMapNodeId;
    public int tier;
    public List<MapEnhancementMod> mods = new();
}

/// <summary>Aggregated permanent map enhancement bonuses for one node (additive from base values).</summary>
public sealed class MapEnhancementAggregate
{
    public float respawnTimeReductionSeconds;
    public float lootBonusFraction;
    public float enemyDamageReductionFraction;
    public float goldBonusFraction;
    public float eliteSpawnChanceBonusFraction;
    public float eliteDoubleSpawnChance;
    public float eliteHealthReductionFraction;
    public readonly Dictionary<string, int> extraSpawnsByEnemyId = new(StringComparer.OrdinalIgnoreCase);

    public bool HasAnyEffect =>
        respawnTimeReductionSeconds > 0.001f
        || lootBonusFraction > 0.001f
        || enemyDamageReductionFraction > 0.001f
        || goldBonusFraction > 0.001f
        || eliteSpawnChanceBonusFraction > 0.001f
        || eliteDoubleSpawnChance > 0.001f
        || eliteHealthReductionFraction > 0.001f
        || extraSpawnsByEnemyId.Count > 0;
}

public static class MapEnhancementRollDefaults
{
    public const float RespawnMinSeconds = 1f;
    public const float RespawnMaxSeconds = 5f;
    public const int ExtraSpawnsMin = 1;
    public const int ExtraSpawnsMax = 5;
    public const float LootMinFraction = 0.10f;
    public const float LootMaxFraction = 0.30f;
    public const float DamageReductionMinFraction = 0.05f;
    public const float DamageReductionMaxFraction = 0.20f;
    public const float GoldMinFraction = 0.10f;
    public const float GoldMaxFraction = 0.30f;
    public const float EliteSpawnChanceMinFraction = 0.10f;
    public const float EliteSpawnChanceMaxFraction = 0.30f;
    public const float EliteDoubleSpawnMinChance = 0.05f;
    public const float EliteDoubleSpawnMaxChance = 0.10f;
    public const float EliteHealthReductionMinFraction = 0.10f;
    public const float EliteHealthReductionMaxFraction = 0.20f;

    public const float LowModWeight = 1f;
    public const float StandardModWeight = 10f;
    public const float EliteSpawnChanceModWeight = 7f;
    public const float EliteDoubleSpawnModWeight = 5f;
    public const float EliteHealthReductionModWeight = 4f;

    public static MapEnhancementModRollConfig[] CreateDefaultRollConfigs() => new[]
    {
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.RespawnTimeReduction,
            rollWeight = LowModWeight,
            minValue = RespawnMinSeconds,
            maxValue = RespawnMaxSeconds
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.ExtraEnemySpawns,
            rollWeight = LowModWeight,
            minValue = ExtraSpawnsMin,
            maxValue = ExtraSpawnsMax
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.LootBonus,
            rollWeight = StandardModWeight,
            minValue = LootMinFraction,
            maxValue = LootMaxFraction
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.EnemyDamageReduction,
            rollWeight = StandardModWeight,
            minValue = DamageReductionMinFraction,
            maxValue = DamageReductionMaxFraction
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.GoldBonus,
            rollWeight = StandardModWeight,
            minValue = GoldMinFraction,
            maxValue = GoldMaxFraction
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.EliteSpawnChanceBonus,
            rollWeight = EliteSpawnChanceModWeight,
            minValue = EliteSpawnChanceMinFraction,
            maxValue = EliteSpawnChanceMaxFraction
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.EliteSpawnDouble,
            rollWeight = EliteDoubleSpawnModWeight,
            minValue = EliteDoubleSpawnMinChance,
            maxValue = EliteDoubleSpawnMaxChance
        },
        new MapEnhancementModRollConfig
        {
            modType = MapEnhancementModType.EliteHealthReduction,
            rollWeight = EliteHealthReductionModWeight,
            minValue = EliteHealthReductionMinFraction,
            maxValue = EliteHealthReductionMaxFraction
        }
    };

    public static readonly MapEnhancementModType[] AllModTypes =
    {
        MapEnhancementModType.RespawnTimeReduction,
        MapEnhancementModType.ExtraEnemySpawns,
        MapEnhancementModType.LootBonus,
        MapEnhancementModType.EnemyDamageReduction,
        MapEnhancementModType.GoldBonus,
        MapEnhancementModType.EliteSpawnChanceBonus,
        MapEnhancementModType.EliteSpawnDouble,
        MapEnhancementModType.EliteHealthReduction
    };
}
