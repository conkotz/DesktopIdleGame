using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapEnhancementModType
{
    RespawnTimeReduction = 0,
    ExtraEnemySpawns = 1,
    LootBonus = 2,
    EnemyDamageReduction = 3,
    GoldBonus = 4
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

    [Tooltip("Respawn: seconds reduced. Extra spawns: count. Loot/Gold/Damage: fraction (0.10 = +10% from base).")]
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
    public readonly Dictionary<string, int> extraSpawnsByEnemyId = new(StringComparer.OrdinalIgnoreCase);

    public bool HasAnyEffect =>
        respawnTimeReductionSeconds > 0.001f
        || lootBonusFraction > 0.001f
        || enemyDamageReductionFraction > 0.001f
        || goldBonusFraction > 0.001f
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

    public const float LowModWeight = 1f;
    public const float StandardModWeight = 10f;

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
        }
    };

    public static readonly MapEnhancementModType[] AllModTypes =
    {
        MapEnhancementModType.RespawnTimeReduction,
        MapEnhancementModType.ExtraEnemySpawns,
        MapEnhancementModType.LootBonus,
        MapEnhancementModType.EnemyDamageReduction,
        MapEnhancementModType.GoldBonus
    };
}
