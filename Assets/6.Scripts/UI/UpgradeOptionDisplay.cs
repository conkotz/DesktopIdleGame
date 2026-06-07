using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class UpgradeOptionDisplay
{
    private const string CostOwnedColor = "#33CC66";
    private const string CostMissingColor = "#FF5C5C";
    public static string FormatOptionType(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(option.displayName))
            return option.displayName.Trim();

        string stat = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(option.targetStat);
        return $"{stat} {EnhancementTierRules.GetTierDisplayName(option.tier)}";
    }

    public static string FormatOptionValue(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        return UpgradeScrollDisplay.FormatStatsDescription(option.ToScrollStats());
    }

    public static string FormatSelectedEnhancementLine(
        EnhancementOptionEntry option,
        EnhancementOptionPayment payment,
        ItemDatabase itemDb)
    {
        if (option == null)
            return "Enhancement Selected: None";

        string type = FormatOptionType(option);
        string value = FormatOptionValue(option);
        string cost = EnhancementOptionPayment.FormatCostLabel(payment, itemDb);
        if (payment.Kind == EnhancementPaymentKind.None)
            return $"Enhancement Selected: {type}: {value}";

        return $"Enhancement Selected: {type}: {value} ({cost})";
    }

    public static string FormatSectionTitle(EnhancementTrack track)
    {
        return track switch
        {
            EnhancementTrack.Corruption => "Corruption Upgrades",
            EnhancementTrack.Special => "Special Upgrades",
            _ => "Standard Upgrades",
        };
    }

    public static string FormatOptionScrollName(EnhancementOptionEntry option, ItemDatabase itemDb)
    {
        if (option == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(option.linkedScrollItemId) && itemDb != null)
        {
            ItemDefinition scroll = itemDb.Get(option.linkedScrollItemId);
            if (scroll != null && !string.IsNullOrWhiteSpace(scroll.displayName))
                return scroll.displayName.Trim();
        }

        string statName = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(option.targetStat);
        return $"{statName} scroll";
    }

    public static string FormatOptionCost(
        EnhancementOptionEntry option,
        EnhancementOptionPayment payment,
        ItemDefinition gear,
        ItemDatabase itemDb)
    {
        if (option == null)
            return string.Empty;

        return FormatPaymentRequirements(option, gear, itemDb);
    }

    public static string FormatOptionAvailableGear(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        EnhancementScrollGearMask mask = option.allowedGearTypes;
        if (mask == EnhancementScrollGearMask.None)
            return "None";

        List<string> labels = new(6);
        if ((mask & EnhancementScrollGearMask.Armor) != 0)
            labels.Add("armour items");
        if ((mask & EnhancementScrollGearMask.Tool) != 0)
            labels.Add("tool items");

        if ((mask & EnhancementScrollGearMask.Weapon) == EnhancementScrollGearMask.Weapon)
        {
            labels.Add("weapon items");
        }
        else if ((mask & EnhancementScrollGearMask.MeleeOrRangedWeapon) == EnhancementScrollGearMask.MeleeOrRangedWeapon)
        {
            labels.Add("melee and ranged weapon items");
        }
        else
        {
            if ((mask & EnhancementScrollGearMask.MeleeWeapon) != 0)
                labels.Add("melee weapon items");
            if ((mask & EnhancementScrollGearMask.RangedWeapon) != 0)
                labels.Add("ranged weapon items");
        }

        if ((mask & EnhancementScrollGearMask.MagicWeapon) != 0)
            labels.Add("magic weapon items");

        if (labels.Count == 0)
            return "gear items";

        return string.Join(", ", labels);
    }

    public static string FormatOptionChance(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        return FormatSuccessChancePercent(option);
    }

    public static string FormatLabeledValue(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        return $"Value: {FormatOptionValue(option)}";
    }

    public static string FormatLabeledEnhanceCost(
        EnhancementOptionEntry option,
        ItemDefinition gear,
        ItemDatabase itemDb,
        Inventory inventory)
    {
        if (option == null)
            return string.Empty;

        bool hasScroll = EnhancementOptionPayment.HasScrollPayment(inventory, option);
        string scrollCost = !string.IsNullOrWhiteSpace(option.linkedScrollItemId)
            ? FormatLinkedScrollName(option, itemDb)
            : null;

        string materialCost = null;
        bool hasMaterials = false;
        bool scrollOnly = EnhancementOptionPayment.RequiresScrollOnlyPayment(option);
        if (gear != null && !scrollOnly)
        {
            string materialId = GearUpgradeMaterialResolver.ResolveMaterialItemId(gear);
            int amount = EnhancementTierRules.GetMaterialCost(option.tier);
            if (!string.IsNullOrWhiteSpace(materialId))
            {
                materialCost = FormatMaterialCost(amount, materialId, itemDb);
                hasMaterials = EnhancementOptionPayment.HasMaterialPayment(inventory, option, gear);
            }
        }

        StringBuilder builder = new();
        builder.Append("Enhance Cost: ");

        bool wrotePart = false;
        if (!string.IsNullOrWhiteSpace(scrollCost))
        {
            builder.Append(ColorCostPart(scrollCost, hasScroll));
            wrotePart = true;
        }

        if (!string.IsNullOrWhiteSpace(materialCost))
        {
            if (wrotePart)
                builder.Append(" or ");

            builder.Append(ColorCostPart(materialCost, hasMaterials));
            wrotePart = true;
        }
        else if (!string.IsNullOrWhiteSpace(scrollCost) && gear == null)
        {
            builder.Append(" or materials");
        }

        if (!wrotePart)
            builder.Append(ColorCostPart("Unavailable", false));

        return builder.ToString();
    }

    public static string FormatLabeledItemType(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        string gearTypes = FormatGearTypeLabels(option);
        string tierUsage = EnhancementTierRules.FormatAllowedGearTierUsage(option.tier);
        if (!string.IsNullOrWhiteSpace(tierUsage))
            tierUsage = $". {tierUsage}";

        if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return $"Item Type: {gearTypes} (must have used upgrade slots){tierUsage}";

        string statName = FormatTargetStatLabel(option.targetStat);
        return $"Item Type: {gearTypes} (Item must contain {statName}){tierUsage}";
    }

    public static string FormatScrollOrMaterialsCost(EnhancementOptionEntry option, ItemDatabase itemDb)
    {
        if (option == null)
            return "Scroll or materials";

        return $"{FormatLinkedScrollName(option, itemDb)} or materials";
    }

    public static string FormatLabeledSuccessChance(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        return $"Success Chance: {FormatSuccessChancePercent(option)}";
    }

    public static string FormatLabeledAdditionalInfo(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        return $"Additional: {FormatOptionAdditionalInfo(option)}";
    }

    public static string FormatOptionAdditionalInfo(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        if (option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
            return "Reduces used upgrade slots on success";

        StringBuilder builder = new();
        builder.Append("Consumes available upgrade slot on use");

        if (option.failureOutcome == EnhancementScrollFailureOutcome.DestroyItem &&
            option.destroyChanceOnFailure > 0f)
        {
            float destroyPct = Mathf.Clamp01(option.destroyChanceOnFailure) * 100f;
            string destroyText = destroyPct >= 1f ? $"{Mathf.RoundToInt(destroyPct)}%" : $"{destroyPct:0.#}%";
            builder.Append($". {destroyText} chance to destroy item on failure");
        }

        return builder.ToString();
    }

    public static string FormatAvailableUpgradeSlots(ItemDefinition gear)
    {
        if (gear == null || !gear.HasUpgradeSlots)
            return string.Empty;

        int available = gear.AvailableUpgradeSlots;
        int used = gear.UsedUpgradeSlots;
        int max = gear.MaxUpgradeSlots;
        string slotWord = available == 1 ? "slot" : "slots";
        return $"{available} upgrade {slotWord} available ({used}/{max} used)";
    }

    private static string FormatTargetStatLabel(EnhancementScrollTargetStat stat)
    {
        string displayName = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(stat);
        return string.IsNullOrWhiteSpace(displayName)
            ? stat.ToString().ToLowerInvariant()
            : displayName.ToLowerInvariant();
    }

    private static string FormatSuccessChancePercent(EnhancementOptionEntry option)
    {
        float pct = Mathf.Clamp01(option.successChance) * 100f;
        return pct >= 1f ? $"{Mathf.RoundToInt(pct)}%" : $"{pct:0.#}%";
    }

    private static string FormatGearTypeLabels(EnhancementOptionEntry option)
    {
        EnhancementScrollGearMask mask = EnhancementScrollGearRules.NormalizeMask(option.allowedGearTypes);
        if (mask == EnhancementScrollGearMask.None)
            return "None";

        List<string> labels = new(6);
        if ((mask & EnhancementScrollGearMask.Helmet) != 0)
            labels.Add("Head");
        if ((mask & EnhancementScrollGearMask.Body) != 0)
            labels.Add("Body");
        if ((mask & EnhancementScrollGearMask.Boots) != 0)
            labels.Add("Feet");
        if ((mask & EnhancementScrollGearMask.Tool) != 0)
            labels.Add("Tool");

        if ((mask & EnhancementScrollGearMask.Weapon) == EnhancementScrollGearMask.Weapon)
        {
            labels.Add("Weapon");
        }
        else if ((mask & EnhancementScrollGearMask.MeleeOrRangedWeapon) == EnhancementScrollGearMask.MeleeOrRangedWeapon)
        {
            labels.Add("Melee and Ranged Weapon");
        }
        else
        {
            if ((mask & EnhancementScrollGearMask.MeleeWeapon) != 0)
                labels.Add("Melee Weapon");
            if ((mask & EnhancementScrollGearMask.RangedWeapon) != 0)
                labels.Add("Ranged Weapon");
        }

        if ((mask & EnhancementScrollGearMask.MagicWeapon) != 0)
            labels.Add("Magic Weapon");

        if (labels.Count == 0)
            return "Gear";

        return string.Join(", ", labels);
    }

    private static string FormatPaymentRequirements(
        EnhancementOptionEntry option,
        ItemDefinition gear,
        ItemDatabase itemDb)
    {
        string scrollCost = !string.IsNullOrWhiteSpace(option.linkedScrollItemId)
            ? FormatLinkedScrollName(option, itemDb)
            : null;
        string materialCost = null;
        if (gear != null)
        {
            string materialId = GearUpgradeMaterialResolver.ResolveMaterialItemId(gear);
            int amount = EnhancementTierRules.GetMaterialCost(option.tier);
            if (!string.IsNullOrWhiteSpace(materialId))
                materialCost = FormatMaterialCost(amount, materialId, itemDb);
        }

        if (!string.IsNullOrWhiteSpace(scrollCost) && !string.IsNullOrWhiteSpace(materialCost))
            return $"{scrollCost} or {materialCost}";

        if (!string.IsNullOrWhiteSpace(scrollCost))
            return scrollCost;

        if (!string.IsNullOrWhiteSpace(materialCost))
            return materialCost;

        return "Unavailable";
    }

    private static string ColorCostPart(string text, bool owned)
    {
        string hex = owned ? CostOwnedColor : CostMissingColor;
        return $"<color={hex}>{text}</color>";
    }

    private static string FormatLinkedScrollName(EnhancementOptionEntry option, ItemDatabase itemDb)
    {
        if (option == null || string.IsNullOrWhiteSpace(option.linkedScrollItemId))
            return "Scroll";

        if (itemDb != null)
        {
            ItemDefinition scroll = itemDb.Get(option.linkedScrollItemId);
            if (scroll != null && !string.IsNullOrWhiteSpace(scroll.displayName))
                return scroll.displayName.Trim();
        }

        return option.linkedScrollItemId.Trim();
    }

    private static string FormatMaterialCost(int amount, string materialItemId, ItemDatabase itemDb)
    {
        if (string.IsNullOrWhiteSpace(materialItemId))
            return $"{amount} materials";

        ItemDefinition material = itemDb != null ? itemDb.Get(materialItemId) : null;
        string name = material != null && !string.IsNullOrWhiteSpace(material.displayName)
            ? material.displayName.Trim()
            : materialItemId.Trim();
        return $"{amount} {name}";
    }
}
