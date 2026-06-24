using System;
using System.Collections.Generic;
using UnityEngine;

public static class GearUpgradeMaterialResolver
{
    private const float EnhancementCostScalePerSuccess = 0.25f;
    private const string PoisonVialItemId = "vial_poison";

    public static GearUpgradeMaterialFamily ResolveFamily(ItemDefinition gear)
    {
        if (gear == null)
            return GearUpgradeMaterialFamily.Unknown;

        string itemId = gear.itemId ?? string.Empty;
        if (itemId.StartsWith("stone_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Stone;
        if (itemId.StartsWith("leather_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Leather;
        if (itemId.StartsWith("linen_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Linen;
        if (itemId.StartsWith("splitwood_", StringComparison.OrdinalIgnoreCase) ||
            itemId.StartsWith("hardwood_", StringComparison.OrdinalIgnoreCase) ||
            itemId.StartsWith("wildwood_", StringComparison.OrdinalIgnoreCase))
            return GearUpgradeMaterialFamily.Wood;

        if (gear.IsWeapon && gear.weaponStats.attackSkill == AttackSkill.Magic)
            return GearUpgradeMaterialFamily.Linen;

        if (gear.IsTool)
            return GearUpgradeMaterialFamily.Wood;

        if (gear.IsArmour || gear.IsOffhandCombatSupport)
            return GearUpgradeMaterialFamily.Stone;

        if (gear.IsWeapon)
            return GearUpgradeMaterialFamily.Stone;

        return GearUpgradeMaterialFamily.Unknown;
    }

    public static string ResolveMaterialItemId(ItemDefinition gear) =>
        ResolveMaterialItemId(ResolveFamily(gear));

    public static string ResolveMaterialItemId(GearUpgradeMaterialFamily family) =>
        family switch
        {
            GearUpgradeMaterialFamily.Stone => "stone_chunk",
            GearUpgradeMaterialFamily.Leather => "leather",
            GearUpgradeMaterialFamily.Linen => "linen",
            GearUpgradeMaterialFamily.Wood => "splitwood_log",
            _ => null,
        };

    public static string ResolveMaterialDisplayName(GearUpgradeMaterialFamily family) =>
        family switch
        {
            GearUpgradeMaterialFamily.Stone => "Stone",
            GearUpgradeMaterialFamily.Leather => "Leather",
            GearUpgradeMaterialFamily.Linen => "Linen",
            GearUpgradeMaterialFamily.Wood => "Wood",
            _ => "Materials",
        };

    public static IReadOnlyList<GearUpgradeMaterialRequirement> ResolveMaterialRequirements(
        ItemDefinition gear,
        EnhancementTier tier,
        int successfulEnhancements,
        EnhancementOptionEntry option = null)
    {
        if (gear == null)
            return Array.Empty<GearUpgradeMaterialRequirement>();

        float scale = 1f + EnhancementCostScalePerSuccess * Mathf.Max(0, successfulEnhancements);

        if (gear.IsTool)
            return ScaleRequirements(BuildToolRequirements(gear, tier), scale);

        if (gear.IsWeapon)
        {
            if (UsesAilmentMaterialCost(option))
                return ScaleRequirements(BuildAilmentWeaponRequirements(tier), scale);

            return ScaleRequirements(BuildWeaponRequirements(tier), scale);
        }

        if (gear.IsArmour || gear.IsOffhandCombatSupport)
            return ScaleRequirements(BuildArmourRequirements(gear, tier), scale);

        return Array.Empty<GearUpgradeMaterialRequirement>();
    }

    private static bool UsesAilmentMaterialCost(EnhancementOptionEntry option)
    {
        if (option == null)
            return false;

        return option.targetStat switch
        {
            EnhancementScrollTargetStat.PoisonChance => true,
            EnhancementScrollTargetStat.PoisonMultiplier => true,
            EnhancementScrollTargetStat.BurnChance => true,
            EnhancementScrollTargetStat.ChillChance => true,
            EnhancementScrollTargetStat.ShockChance => true,
            EnhancementScrollTargetStat.BurnMultiplier => true,
            _ => false,
        };
    }

    private static GearUpgradeMaterialRequirement[] BuildAilmentWeaponRequirements(EnhancementTier tier)
    {
        string woodId = ResolveWoodLogId(tier);
        return new[]
        {
            new GearUpgradeMaterialRequirement(woodId, 50),
            new GearUpgradeMaterialRequirement("stone_chunk", 15),
            new GearUpgradeMaterialRequirement(PoisonVialItemId, 30),
        };
    }

    private static GearUpgradeMaterialRequirement[] BuildWeaponRequirements(EnhancementTier tier) =>
        tier switch
        {
            EnhancementTier.Intermediate => new[]
            {
                new GearUpgradeMaterialRequirement("hardwood_log", 100),
                new GearUpgradeMaterialRequirement("stone_chunk", 30),
            },
            EnhancementTier.Advanced => new[]
            {
                new GearUpgradeMaterialRequirement("wildwood_log", 100),
            },
            _ => new[]
            {
                new GearUpgradeMaterialRequirement("splitwood_log", 100),
                new GearUpgradeMaterialRequirement("stone_chunk", 30),
            },
        };

    private static GearUpgradeMaterialRequirement[] BuildArmourRequirements(ItemDefinition gear, EnhancementTier tier)
    {
        int baseAmount = EnhancementTierRules.GetMaterialCost(tier);
        string materialId = ResolveMaterialItemId(ResolveFamily(gear));
        if (string.IsNullOrWhiteSpace(materialId))
            return Array.Empty<GearUpgradeMaterialRequirement>();

        return new[] { new GearUpgradeMaterialRequirement(materialId, baseAmount) };
    }

    private static GearUpgradeMaterialRequirement[] BuildToolRequirements(ItemDefinition gear, EnhancementTier tier)
    {
        string woodId = ResolveWoodLogId(tier);
        ToolType toolType = gear.toolStats.toolType;

        return toolType switch
        {
            ToolType.Pickaxe => new[]
            {
                new GearUpgradeMaterialRequirement("stone_chunk", 80),
                new GearUpgradeMaterialRequirement(woodId, 20),
            },
            ToolType.FishingRod => new[]
            {
                new GearUpgradeMaterialRequirement(woodId, 150),
            },
            _ => new[]
            {
                new GearUpgradeMaterialRequirement(woodId, 100),
                new GearUpgradeMaterialRequirement("stone_chunk", 10),
            },
        };
    }

    private static string ResolveWoodLogId(EnhancementTier tier) =>
        tier switch
        {
            EnhancementTier.Intermediate => "hardwood_log",
            EnhancementTier.Advanced => "wildwood_log",
            _ => "splitwood_log",
        };

    private static GearUpgradeMaterialRequirement[] ScaleRequirements(
        GearUpgradeMaterialRequirement[] requirements,
        float scale)
    {
        if (requirements == null || requirements.Length == 0)
            return Array.Empty<GearUpgradeMaterialRequirement>();

        var scaled = new GearUpgradeMaterialRequirement[requirements.Length];
        for (int i = 0; i < requirements.Length; i++)
        {
            GearUpgradeMaterialRequirement req = requirements[i];
            int amount = Mathf.Max(1, Mathf.CeilToInt(req.Amount * scale));
            scaled[i] = new GearUpgradeMaterialRequirement(req.ItemId, amount);
        }

        return scaled;
    }
}
