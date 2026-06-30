using System;
using System.Collections.Generic;
using UnityEngine;

public static class GearUpgradeMaterialResolver
{
    private const float EnhancementCostScalePerSuccess = 0.25f;
    private const string PoisonVialItemId = "vial_poison";

    private const int AxeWoodCost = 100;
    private const int PickaxeWoodCost = 20;
    private const int BowWoodCost = 150;
    private const int FishingRodWoodCost = 150;
    private const int WeaponWoodCost = 100;
    private const int WeaponOreCost = 10;
    private const int MagicFabricCost = 100;
    private const int ArmourFabricCost = 80;
    private const int AilmentWoodCost = 50;
    private const int AilmentOreCost = 10;
    private const int AilmentPoisonCost = 30;

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
            itemId.StartsWith("wildwood_", StringComparison.OrdinalIgnoreCase) ||
            itemId.StartsWith("ember_oak_", StringComparison.OrdinalIgnoreCase) ||
            itemId.StartsWith("emberoak_", StringComparison.OrdinalIgnoreCase) ||
            itemId.StartsWith("spiritwood_", StringComparison.OrdinalIgnoreCase))
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
            return ScaleRequirements(BuildToolRequirements(gear), scale);

        if (gear.IsWeapon)
        {
            if (UsesAilmentMaterialCost(option))
                return ScaleRequirements(BuildAilmentWeaponRequirements(gear), scale);

            if (gear.weaponStats.attackSkill == AttackSkill.Magic)
                return ScaleRequirements(BuildMagicWeaponRequirements(gear), scale);

            if (UsesBowEnhancementCost(gear))
                return ScaleRequirements(BuildBowRequirements(gear), scale);

            return ScaleRequirements(BuildWeaponRequirements(gear), scale);
        }

        if (gear.IsArmour || gear.IsOffhandCombatSupport)
            return ScaleRequirements(BuildArmourRequirements(gear), scale);

        return Array.Empty<GearUpgradeMaterialRequirement>();
    }

    /// <summary>Ore or stone used for enhancing gear at this equipment tier.</summary>
    public static string ResolveOreItemId(EquipmentTierRank gearTier) =>
        gearTier switch
        {
            EquipmentTierRank.Tier1 => "stone_chunk",
            EquipmentTierRank.Tier2 => "iron_ore",
            EquipmentTierRank.Tier3 => "mythril_ore",
            EquipmentTierRank.Tier4 => "runite_ore",
            EquipmentTierRank.Tier5 => "celestium_ore",
            _ => "stone_chunk",
        };

    /// <summary>Wood log tier matched to the equipment rank being enhanced.</summary>
    public static string ResolveWoodLogItemId(EquipmentTierRank gearTier) =>
        gearTier switch
        {
            EquipmentTierRank.Tier1 => "splitwood_log",
            EquipmentTierRank.Tier2 => "hardwood_log",
            EquipmentTierRank.Tier3 => "wildwood_log",
            EquipmentTierRank.Tier4 => "ember_oak_log",
            EquipmentTierRank.Tier5 => "spiritwood_log",
            _ => "splitwood_log",
        };

    /// <summary>Pickaxes and axes: ore cost drops by 10 per equipment tier (80 → 40).</summary>
    public static int ResolveTieredOreCost(EquipmentTierRank gearTier) =>
        gearTier switch
        {
            EquipmentTierRank.Tier1 => 80,
            EquipmentTierRank.Tier2 => 70,
            EquipmentTierRank.Tier3 => 60,
            EquipmentTierRank.Tier4 => 50,
            EquipmentTierRank.Tier5 => 40,
            _ => 80,
        };

    private static bool UsesBowEnhancementCost(ItemDefinition gear)
    {
        if (gear == null || !gear.IsWeapon || gear.weaponStats.attackSkill != AttackSkill.Ranged)
            return false;

        string itemId = gear.itemId ?? string.Empty;
        return itemId.Contains("bow", StringComparison.OrdinalIgnoreCase);
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

    private static GearUpgradeMaterialRequirement[] BuildAilmentWeaponRequirements(ItemDefinition gear)
    {
        EquipmentTierRank gearTier = gear.GetEquipmentTierRank();
        return new[]
        {
            new GearUpgradeMaterialRequirement(ResolveWoodLogItemId(gearTier), AilmentWoodCost),
            new GearUpgradeMaterialRequirement(ResolveOreItemId(gearTier), AilmentOreCost),
            new GearUpgradeMaterialRequirement(PoisonVialItemId, AilmentPoisonCost),
        };
    }

    private static GearUpgradeMaterialRequirement[] BuildBowRequirements(ItemDefinition gear)
    {
        EquipmentTierRank gearTier = gear.GetEquipmentTierRank();
        return new[]
        {
            new GearUpgradeMaterialRequirement(ResolveWoodLogItemId(gearTier), BowWoodCost),
        };
    }

    private static GearUpgradeMaterialRequirement[] BuildWeaponRequirements(ItemDefinition gear)
    {
        EquipmentTierRank gearTier = gear.GetEquipmentTierRank();
        return new[]
        {
            new GearUpgradeMaterialRequirement(ResolveWoodLogItemId(gearTier), WeaponWoodCost),
            new GearUpgradeMaterialRequirement(ResolveOreItemId(gearTier), WeaponOreCost),
        };
    }

    private static GearUpgradeMaterialRequirement[] BuildMagicWeaponRequirements(ItemDefinition gear)
    {
        return new[]
        {
            new GearUpgradeMaterialRequirement("linen", MagicFabricCost),
        };
    }

    private static GearUpgradeMaterialRequirement[] BuildArmourRequirements(ItemDefinition gear)
    {
        GearUpgradeMaterialFamily family = ResolveFamily(gear);
        string materialId = ResolveMaterialItemId(family);
        if (string.IsNullOrWhiteSpace(materialId))
            return Array.Empty<GearUpgradeMaterialRequirement>();

        return new[] { new GearUpgradeMaterialRequirement(materialId, ArmourFabricCost) };
    }

    private static GearUpgradeMaterialRequirement[] BuildToolRequirements(ItemDefinition gear)
    {
        EquipmentTierRank gearTier = gear.GetEquipmentTierRank();
        string woodId = ResolveWoodLogItemId(gearTier);
        string oreId = ResolveOreItemId(gearTier);
        int oreCost = ResolveTieredOreCost(gearTier);
        ToolType toolType = gear.toolStats.toolType;

        return toolType switch
        {
            ToolType.Pickaxe => new[]
            {
                new GearUpgradeMaterialRequirement(oreId, oreCost),
                new GearUpgradeMaterialRequirement(woodId, PickaxeWoodCost),
            },
            ToolType.FishingRod => new[]
            {
                new GearUpgradeMaterialRequirement(woodId, FishingRodWoodCost),
            },
            _ => new[]
            {
                new GearUpgradeMaterialRequirement(oreId, oreCost),
                new GearUpgradeMaterialRequirement(woodId, AxeWoodCost),
            },
        };
    }

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
