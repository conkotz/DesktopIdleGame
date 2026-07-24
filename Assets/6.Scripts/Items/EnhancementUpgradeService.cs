using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class EnhancementUpgradeService
{
    private static readonly Regex EnhancementSuffixRegex = new Regex(@"\s\+\d+$", RegexOptions.Compiled);

    public static bool TryUseScrollOnInventorySlot(
        Inventory inventory,
        int scrollSlotIndex,
        int targetSlotIndex,
        out bool success)
    {
        success = false;

        if (inventory == null || scrollSlotIndex < 0 || targetSlotIndex < 0 || scrollSlotIndex == targetSlotIndex)
            return false;

        Inventory.Slot scrollSlot = inventory.GetSlot(scrollSlotIndex);
        Inventory.Slot targetSlot = inventory.GetSlot(targetSlotIndex);
        if (scrollSlot.IsEmpty || targetSlot.IsEmpty)
            return false;
        if (targetSlot.amount != 1)
            return false;

        ItemDefinition scrollDef = inventory.GetItemDef(scrollSlot.itemId);
        ItemDefinition targetDef = inventory.GetItemDef(targetSlot.itemId);
        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(scrollDef);
        LogRejectedScrollTargetAttempt(scrollDef, targetDef, option);
        if (!IsValidScrollTarget(scrollDef, targetDef, option))
            return false;

        // Consume the scroll before mutating the gear slot so a failed remove cannot
        // leave a permanently converted runtime clone with the scroll still owned.
        if (inventory.RemoveAmountAtSlot(scrollSlotIndex, 1) != 1)
            return false;

        ItemDefinition enhancedTarget = targetDef;
        if (!inventory.IsRuntimeEnhancedItem(targetSlot.itemId))
        {
            enhancedTarget = inventory.CreateRuntimeEnhancedItem(targetDef);
            if (!enhancedTarget)
            {
                RefundAmountWithStorageOverflow(inventory, scrollDef.itemId, 1);
                return false;
            }

            targetSlot.itemId = enhancedTarget.itemId;
            targetSlot.amount = 1;
            inventory.ReplaceSlot(targetSlotIndex, targetSlot);
        }

        bool attempted = TryApplyEnhancementStats(
            inventory,
            EnhancementOptionResolver.BuildStatsForApply(option, enhancedTarget),
            enhancedTarget,
            targetDef,
            () => inventory.RemoveStackAtSlot(targetSlotIndex),
            scrollDef,
            out success);

        if (!attempted)
            return false;

        inventory.ReplaceSlot(targetSlotIndex, inventory.GetSlot(targetSlotIndex));
        return true;
    }

    public static bool TryApplyOptionOnInventorySlot(
        Inventory inventory,
        int gearSlotIndex,
        EnhancementOptionEntry option,
        EnhancementOptionPayment payment,
        out bool success)
    {
        success = false;

        if (inventory == null || gearSlotIndex < 0 || option == null)
            return false;

        if (payment.Kind == EnhancementPaymentKind.None)
            return false;

        Inventory.Slot targetSlot = inventory.GetSlot(gearSlotIndex);
        if (targetSlot.IsEmpty || targetSlot.amount != 1)
            return false;

        ItemDefinition targetDef = inventory.GetItemDef(targetSlot.itemId);
        if (!option.CanApplyToGear(targetDef, SkillsManager.Instance))
            return false;

        // Pay first so a failed consume cannot leave gear converted to a runtime id.
        if (!TryConsumePayment(inventory, payment))
            return false;

        ItemDefinition enhancedTarget = targetDef;
        if (!inventory.IsRuntimeEnhancedItem(targetSlot.itemId))
        {
            enhancedTarget = inventory.CreateRuntimeEnhancedItem(targetDef);
            if (!enhancedTarget)
            {
                TryRefundPayment(inventory, payment, option.linkedScrollItemId);
                return false;
            }

            targetSlot.itemId = enhancedTarget.itemId;
            targetSlot.amount = 1;
            inventory.ReplaceSlot(gearSlotIndex, targetSlot);
        }

        ItemDefinition historyScroll = !string.IsNullOrWhiteSpace(option.linkedScrollItemId)
            ? inventory.GetItemDef(option.linkedScrollItemId)
            : null;

        bool attempted = TryApplyEnhancementStats(
            inventory,
            option.ToScrollStats(enhancedTarget),
            enhancedTarget,
            targetDef,
            () => inventory.RemoveStackAtSlot(gearSlotIndex),
            historyScroll,
            out success);

        if (!attempted)
            return false;

        inventory.ReplaceSlot(gearSlotIndex, inventory.GetSlot(gearSlotIndex));
        return true;
    }

    public static bool TryApplyOptionOnEquippedItem(
        Inventory inventory,
        EquipmentManager equipment,
        ToolbeltManager toolbelt,
        EquipmentUISlotType equipmentSlot,
        EnhancementOptionEntry option,
        EnhancementOptionPayment payment,
        out bool success)
    {
        success = false;

        if (inventory == null || option == null || equipmentSlot == EquipmentUISlotType.None)
            return false;

        if (payment.Kind == EnhancementPaymentKind.None)
            return false;

        string targetItemId = EquipmentSlotUI.GetItemIdForSlot(equipmentSlot, equipment, toolbelt);
        if (string.IsNullOrWhiteSpace(targetItemId))
            return false;

        ItemDefinition targetDef = inventory.GetItemDef(targetItemId);
        if (!option.CanApplyToGear(targetDef, SkillsManager.Instance))
            return false;

        if (!TryConsumePayment(inventory, payment))
            return false;

        ItemDefinition enhancedTarget = targetDef;
        if (!inventory.IsRuntimeEnhancedItem(targetItemId))
        {
            enhancedTarget = inventory.CreateRuntimeEnhancedItem(targetDef);
            if (!enhancedTarget)
            {
                TryRefundPayment(inventory, payment, option.linkedScrollItemId);
                return false;
            }

            EquipmentSlotUI.ReplaceItemIdForSlot(equipmentSlot, equipment, toolbelt, enhancedTarget.itemId);
        }

        ItemDefinition historyScroll = !string.IsNullOrWhiteSpace(option.linkedScrollItemId)
            ? inventory.GetItemDef(option.linkedScrollItemId)
            : null;

        return TryApplyEnhancementStats(
            inventory,
            option.ToScrollStats(enhancedTarget),
            enhancedTarget,
            targetDef,
            () => EquipmentSlotUI.ClearSlotOnEnhancementDestroy(equipmentSlot, equipment, toolbelt),
            historyScroll,
            out success);
    }

    public static bool TryUseScrollOnEquippedItem(
        Inventory inventory,
        int scrollSlotIndex,
        string targetItemId,
        Action<string> replaceTargetItemId,
        Action clearTargetItem,
        out bool success)
    {
        success = false;

        if (inventory == null || scrollSlotIndex < 0 || string.IsNullOrWhiteSpace(targetItemId))
            return false;
        if (replaceTargetItemId == null || clearTargetItem == null)
            return false;

        Inventory.Slot scrollSlot = inventory.GetSlot(scrollSlotIndex);
        if (scrollSlot.IsEmpty)
            return false;

        ItemDefinition scrollDef = inventory.GetItemDef(scrollSlot.itemId);
        ItemDefinition targetDef = inventory.GetItemDef(targetItemId);
        EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(scrollDef);
        LogRejectedScrollTargetAttempt(scrollDef, targetDef, option);
        if (!IsValidScrollTarget(scrollDef, targetDef, option))
            return false;

        if (inventory.RemoveAmountAtSlot(scrollSlotIndex, 1) != 1)
            return false;

        ItemDefinition enhancedTarget = targetDef;
        if (!inventory.IsRuntimeEnhancedItem(targetItemId))
        {
            enhancedTarget = inventory.CreateRuntimeEnhancedItem(targetDef);
            if (!enhancedTarget)
            {
                RefundAmountWithStorageOverflow(inventory, scrollDef.itemId, 1);
                return false;
            }

            replaceTargetItemId(enhancedTarget.itemId);
        }

        return TryApplyEnhancementStats(
            inventory,
            EnhancementOptionResolver.BuildStatsForApply(option, enhancedTarget),
            enhancedTarget,
            targetDef,
            clearTargetItem,
            scrollDef,
            out success);
    }

    private static bool TryConsumePayment(Inventory inventory, EnhancementOptionPayment payment)
    {
        switch (payment.Kind)
        {
            case EnhancementPaymentKind.Scroll:
                if (payment.ScrollFromStorage)
                {
                    PlayerStorage storage = FindPlayerStorage();
                    return storage != null && storage.RemoveAmountAtSlot(payment.ScrollSlotIndex, 1) == 1;
                }

                return inventory != null && inventory.RemoveAmountAtSlot(payment.ScrollSlotIndex, 1) == 1;
            case EnhancementPaymentKind.Materials:
                return inventory != null && inventory.TryConsumeItems(payment.MaterialRequirements);
            default:
                return false;
        }
    }

    /// <summary>
    /// Best-effort undo when payment succeeded but runtime gear clone creation failed.
    /// Prefers returning scrolls to storage when that was the payment source, and overflows
    /// remainder to storage so a full inventory cannot silently discard the refund.
    /// </summary>
    private static void TryRefundPayment(
        Inventory inventory,
        EnhancementOptionPayment payment,
        string scrollItemId = null)
    {
        if (payment.Kind == EnhancementPaymentKind.Scroll)
        {
            if (string.IsNullOrWhiteSpace(scrollItemId))
                return;

            if (payment.ScrollFromStorage)
            {
                PlayerStorage storage = FindPlayerStorage();
                if (storage != null && storage.TryDepositAmountFromExternal(scrollItemId, 1) == 1)
                    return;
            }

            RefundAmountWithStorageOverflow(inventory, scrollItemId, 1);
            return;
        }

        if (payment.Kind != EnhancementPaymentKind.Materials)
            return;

        IReadOnlyList<GearUpgradeMaterialRequirement> requirements = payment.MaterialRequirements;
        if (requirements == null)
            return;

        for (int i = 0; i < requirements.Count; i++)
        {
            GearUpgradeMaterialRequirement req = requirements[i];
            if (string.IsNullOrWhiteSpace(req.ItemId) || req.Amount <= 0)
                continue;

            RefundAmountWithStorageOverflow(inventory, req.ItemId, req.Amount);
        }
    }

    private static void RefundAmountWithStorageOverflow(Inventory inventory, string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        int left = amount;
        if (inventory != null)
            left -= inventory.AddPartial(itemId, left, notifyItemGainPopup: false);

        if (left <= 0)
            return;

        PlayerStorage storage = FindPlayerStorage();
        if (storage != null)
            storage.TryDepositAmountFromExternal(itemId, left);
    }

    private static PlayerStorage FindPlayerStorage() =>
        UnityEngine.Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);

    private static bool TryApplyEnhancementStats(
        Inventory inventory,
        EnhancementScrollStats stats,
        ItemDefinition enhancedTarget,
        ItemDefinition previousTargetDef,
        Action destroyTarget,
        ItemDefinition historyScrollDef,
        out bool success)
    {
        success = false;
        bool slotReductionScroll = stats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction;
        int usedSlotsBefore = enhancedTarget.UsedUpgradeSlots;

        if (stats.consumeSlotOnFailure)
        {
            enhancedTarget.usedUpgradeSlots = Mathf.Clamp(
                enhancedTarget.usedUpgradeSlots + 1,
                0,
                Mathf.Max(0, enhancedTarget.MaxUpgradeSlots));
        }

        success = UnityEngine.Random.value <= Mathf.Clamp01(stats.successChance);
        int enhancementBefore = enhancedTarget.successfulEnhancements;
        if (success)
        {
            ApplyModifier(enhancedTarget, stats);
            int usedSlotsAfter = enhancedTarget.UsedUpgradeSlots;

            if (!slotReductionScroll)
            {
                enhancedTarget.successfulEnhancements = Mathf.Clamp(
                    enhancedTarget.successfulEnhancements + 1,
                    0,
                    Mathf.Max(0, enhancedTarget.MaxSuccessfulEnhancements));
                ApplyEnhancedDisplayName(enhancedTarget, previousTargetDef);
            }

            int enhancementAfter = enhancedTarget.successfulEnhancements;
            LogResult(enhancedTarget, true, slotReductionScroll, usedSlotsBefore, usedSlotsAfter, enhancementBefore, enhancementAfter);
        }
        else
        {
            LogResult(enhancedTarget, false, slotReductionScroll, usedSlotsBefore, usedSlotsBefore, enhancementBefore, enhancementBefore);
            ApplyFailureOutcome(destroyTarget, stats);
        }

        if (historyScrollDef != null)
            enhancedTarget.RecordEnhancementScrollAttempt(historyScrollDef, success);

        return true;
    }

    private static bool IsValidScrollTarget(
        ItemDefinition scrollDef,
        ItemDefinition targetDef,
        EnhancementOptionEntry option)
    {
        if (!scrollDef || !targetDef || option == null)
            return false;
        if (scrollDef.itemKind != ItemKind.EnhancementScroll)
            return false;
        if (targetDef.itemKind == ItemKind.EnhancementScroll)
            return false;
        if (targetDef.MaxUpgradeSlots <= 0)
            return false;
        if (!EnhancementOptionResolver.ScrollMatchesDatabase(scrollDef, option))
            return false;
        if (!IsSlotReductionOption(option) && targetDef.HasReachedEnhancementCap)
            return false;

        return option.CanApplyToGear(targetDef, SkillsManager.Instance);
    }

    private static bool IsSlotReductionOption(EnhancementOptionEntry option) =>
        option != null && option.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction;

    private static void ApplyModifier(ItemDefinition target, EnhancementScrollStats scroll)
    {
        float value = scroll.modifierValue;
        bool percent = scroll.modifierKind == EnhancementScrollModifierKind.Percent;

        switch (scroll.targetStat)
        {
            case EnhancementScrollTargetStat.PhysicalDamage:
                if (target.IsWeapon)
                {
                    target.weaponStats.minPhysicalDamage = ApplyIntValue(target.weaponStats.minPhysicalDamage, value, percent);
                    target.weaponStats.maxPhysicalDamage = ApplyIntValue(target.weaponStats.maxPhysicalDamage, value, percent);
                }
                else
                {
                    target.bonusStats.physicalDamage = ApplyValue(target.bonusStats.physicalDamage, value, percent);
                }
                break;

            case EnhancementScrollTargetStat.MagicDamage:
                if (target.IsWeapon)
                {
                    target.weaponStats.minFireDamage = ApplyIntValue(target.weaponStats.minFireDamage, value, percent);
                    target.weaponStats.maxFireDamage = ApplyIntValue(target.weaponStats.maxFireDamage, value, percent);
                }
                else
                {
                    target.bonusStats.magicDamage = ApplyValue(target.bonusStats.magicDamage, value, percent);
                }
                break;

            case EnhancementScrollTargetStat.FireDamage:
                if (target.IsWeapon)
                {
                    target.weaponStats.minFireDamage = ApplyIntValue(target.weaponStats.minFireDamage, value, percent);
                    target.weaponStats.maxFireDamage = ApplyIntValue(target.weaponStats.maxFireDamage, value, percent);
                }
                break;

            case EnhancementScrollTargetStat.IceDamage:
                if (target.IsWeapon)
                {
                    target.weaponStats.minIceDamage = ApplyIntValue(target.weaponStats.minIceDamage, value, percent);
                    target.weaponStats.maxIceDamage = ApplyIntValue(target.weaponStats.maxIceDamage, value, percent);
                }
                break;

            case EnhancementScrollTargetStat.LightningDamage:
                if (target.IsWeapon)
                {
                    target.weaponStats.minLightningDamage = ApplyIntValue(target.weaponStats.minLightningDamage, value, percent);
                    target.weaponStats.maxLightningDamage = ApplyIntValue(target.weaponStats.maxLightningDamage, value, percent);
                }
                break;

            case EnhancementScrollTargetStat.CorruptionDamage:
                if (target.IsWeapon)
                {
                    target.weaponStats.minCorruptionDamage = ApplyIntValue(target.weaponStats.minCorruptionDamage, value, percent);
                    target.weaponStats.maxCorruptionDamage = ApplyIntValue(target.weaponStats.maxCorruptionDamage, value, percent);
                }
                else
                {
                    target.bonusStats.corruptionDamage = ApplyValue(target.bonusStats.corruptionDamage, value, percent);
                }
                break;

            case EnhancementScrollTargetStat.Health:
                if (target.IsArmour)
                    target.armourStats.bonusHealth = ApplyIntValue(target.armourStats.bonusHealth, value, percent);
                else
                    target.bonusStats.bonusHealth = ApplyIntValue(target.bonusStats.bonusHealth, value, percent);
                break;

            case EnhancementScrollTargetStat.Energy:
                if (target.IsArmour)
                    target.armourStats.bonusEnergy = ApplyIntValue(target.armourStats.bonusEnergy, value, percent);
                else
                    target.bonusStats.bonusEnergy = ApplyIntValue(target.bonusStats.bonusEnergy, value, percent);
                break;

            case EnhancementScrollTargetStat.Mana:
                target.bonusStats.bonusMana = ApplyIntValue(target.bonusStats.bonusMana, value, percent);
                break;

            case EnhancementScrollTargetStat.Armour:
                if (target.IsArmour)
                    target.armourStats.armour = ApplyIntValue(target.armourStats.armour, value, percent);
                else
                    target.bonusStats.armour = ApplyIntValue(target.bonusStats.armour, value, percent);
                break;

            case EnhancementScrollTargetStat.MagicResist:
                if (target.IsArmour)
                    target.armourStats.magicResist = ApplyIntValue(target.armourStats.magicResist, value, percent);
                else
                    target.bonusStats.magicResist = ApplyIntValue(target.bonusStats.magicResist, value, percent);
                break;

            case EnhancementScrollTargetStat.CorruptionResist:
                if (target.IsArmour)
                    target.armourStats.corruptionResist = ApplyIntValue(target.armourStats.corruptionResist, value, percent);
                else
                    target.bonusStats.corruptionResist = ApplyIntValue(target.bonusStats.corruptionResist, value, percent);
                break;

            case EnhancementScrollTargetStat.CritChance:
                if (target.IsWeapon)
                    target.weaponStats.critChance = ApplyValue(target.weaponStats.critChance, value, percent);
                else
                    target.bonusStats.critChanceBonus = ApplyValue(target.bonusStats.critChanceBonus, value, percent);
                break;

            case EnhancementScrollTargetStat.CritMultiplier:
                if (target.IsWeapon)
                    target.weaponStats.critMultiplier = ApplyValue(target.weaponStats.critMultiplier, value, percent);
                else
                    target.bonusStats.critMultiplierBonus = ApplyValue(target.bonusStats.critMultiplierBonus, value, percent);
                break;

            case EnhancementScrollTargetStat.AttackSpeed:
                if (percent)
                    target.bonusStats.attackSpeedPercent = ApplyValue(target.bonusStats.attackSpeedPercent, value, percent);
                else if (target.IsWeapon)
                    target.weaponStats.attacksPerSecond = ApplyValue(target.weaponStats.attacksPerSecond, value, percent);
                else
                    target.bonusStats.attackSpeedPercent = ApplyValue(target.bonusStats.attackSpeedPercent, value, percent);
                break;

            case EnhancementScrollTargetStat.LifeSteal:
                target.bonusStats.lifeSteal = ApplyValue(target.bonusStats.lifeSteal, value, percent);
                break;

            case EnhancementScrollTargetStat.MoveSpeed:
                target.bonusStats.moveSpeedPercent = ApplyValue(target.bonusStats.moveSpeedPercent, value, percent);
                break;

            case EnhancementScrollTargetStat.GatherSpeed:
                if (target.IsTool)
                    target.toolStats.gatherSpeedMultiplier = ApplyValue(target.toolStats.gatherSpeedMultiplier, value, percent);
                break;

            case EnhancementScrollTargetStat.GatheringGrit:
                if (target.IsTool)
                    target.toolStats.gatheringGrit = Mathf.Clamp01(ApplyValue(target.toolStats.gatheringGrit, value, percent));
                break;

            case EnhancementScrollTargetStat.StaminaEfficiency:
                if (target.IsTool)
                    target.toolStats.staminaEfficiency = Mathf.Clamp01(ApplyValue(target.toolStats.staminaEfficiency, value, percent));
                break;

            case EnhancementScrollTargetStat.PoisonChance:
                target.bonusStats.poisonChance = ApplyValue(target.bonusStats.poisonChance, value, percent);
                break;

            case EnhancementScrollTargetStat.PoisonMultiplier:
                target.bonusStats.poisonMultiplier = ApplyValue(target.bonusStats.poisonMultiplier, value, percent);
                break;

            case EnhancementScrollTargetStat.BurnChance:
                target.bonusStats.burnChance = Mathf.Clamp01(ApplyValue(target.bonusStats.burnChance, value, percent));
                break;

            case EnhancementScrollTargetStat.ChillChance:
                target.bonusStats.chillChance = Mathf.Clamp01(ApplyValue(target.bonusStats.chillChance, value, percent));
                break;

            case EnhancementScrollTargetStat.ShockChance:
                target.bonusStats.shockChance = Mathf.Clamp01(ApplyValue(target.bonusStats.shockChance, value, percent));
                break;

            case EnhancementScrollTargetStat.BurnMultiplier:
                target.bonusStats.burnExplosionMultiplierBonus = ApplyValue(target.bonusStats.burnExplosionMultiplierBonus, value, percent);
                break;

            case EnhancementScrollTargetStat.EnergyEfficiency:
                if (target.IsArmour)
                    target.armourStats.energyEfficiency = Mathf.Clamp01(
                        ApplyValue(target.armourStats.energyEfficiency, value, percent));
                else
                    target.bonusStats.energyEfficiency = Mathf.Clamp01(
                        ApplyValue(target.bonusStats.energyEfficiency, value, percent));
                break;

            case EnhancementScrollTargetStat.FlatGuard:
                if (target.IsArmour)
                    target.armourStats.flatGuard = ApplyIntValue(target.armourStats.flatGuard, value, percent);
                break;

            case EnhancementScrollTargetStat.ManaRegen:
                target.bonusStats.manaRegen = ApplyValue(target.bonusStats.manaRegen, value, percent);
                break;

            case EnhancementScrollTargetStat.SpellDamage:
                target.bonusStats.spellDamagePercent = ApplyValue(target.bonusStats.spellDamagePercent, value, percent);
                break;

            case EnhancementScrollTargetStat.FireDamagePercent:
                target.bonusStats.fireSkillDamagePercent = ApplyValue(target.bonusStats.fireSkillDamagePercent, value, percent);
                break;

            case EnhancementScrollTargetStat.IceDamagePercent:
                target.bonusStats.iceSkillDamagePercent = ApplyValue(target.bonusStats.iceSkillDamagePercent, value, percent);
                break;

            case EnhancementScrollTargetStat.LightningDamagePercent:
                target.bonusStats.lightningSkillDamagePercent = ApplyValue(target.bonusStats.lightningSkillDamagePercent, value, percent);
                break;

            case EnhancementScrollTargetStat.UpgradeSlotReduction:
                int slotsToReduce = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(value)));
                target.usedUpgradeSlots = Mathf.Clamp(
                    target.usedUpgradeSlots - slotsToReduce,
                    0,
                    Mathf.Max(0, target.MaxUpgradeSlots));
                break;
        }
    }

    private static void LogRejectedScrollTargetAttempt(
        ItemDefinition scrollDef,
        ItemDefinition targetDef,
        EnhancementOptionEntry option)
    {
        if (!scrollDef || scrollDef.itemKind != ItemKind.EnhancementScroll || !targetDef)
            return;

        string itemName = !string.IsNullOrWhiteSpace(targetDef.displayName) ? targetDef.displayName.Trim() : "Item";
        string scrollName = !string.IsNullOrWhiteSpace(scrollDef.displayName) ? scrollDef.displayName.Trim() : "Scroll";

        if (option == null)
        {
            GameLog.Add($"Cannot use {scrollName}: no matching enhancement option in database", GameLog.ItemLostColor);
            return;
        }

        if (!EnhancementOptionResolver.ScrollMatchesDatabase(scrollDef, option))
        {
            GameLog.Add($"Cannot use {scrollName}: scroll data does not match enhancement database", GameLog.ItemLostColor);
            return;
        }

        if (IsSlotReductionOption(option))
        {
            if (!option.CanApplyToGear(targetDef, SkillsManager.Instance))
                GameLog.Add($"Cannot use {scrollName} on {itemName}", GameLog.ItemLostColor);
            else if (targetDef.UsedUpgradeSlots <= 0)
                GameLog.Add($"Cannot reduce slots, no used slots: {itemName}", GameLog.ItemLostColor);
            return;
        }

        if (targetDef.HasReachedEnhancementCap)
        {
            GameLog.Add($"Cannot enhance further, already at max: {itemName}", GameLog.ItemLostColor);
            return;
        }

        if (!option.CanApplyToGear(targetDef, SkillsManager.Instance))
        {
            GameLog.Add($"Cannot use {scrollName} on {itemName}", GameLog.ItemLostColor);
            return;
        }

        if (!targetDef.HasBaseStatForEnhancementScroll(option.targetStat))
        {
            string statName = ItemDefinition.GetEnhancementScrollTargetStatDisplayName(option.targetStat);
            GameLog.Add($"Cannot use {scrollName} on {itemName}: item has no {statName} to enhance", GameLog.ItemLostColor);
            return;
        }

        if (!targetDef.HasAvailableUpgradeSlot)
            GameLog.Add($"Cannot enhance, no upgrade slots available: {itemName}", GameLog.ItemLostColor);
    }

    private static float ApplyValue(float current, float value, bool percent)
    {
        return current + value;
    }

    private static int ApplyIntValue(int current, float value, bool percent)
    {
        return Mathf.RoundToInt(ApplyValue(current, value, percent));
    }

    private static void ApplyEnhancedDisplayName(ItemDefinition target, ItemDefinition previousTargetDef)
    {
        string baseName = ResolveBaseDisplayName(previousTargetDef);
        target.displayName = $"{baseName} +{target.successfulEnhancements}";
    }

    private static void LogResult(
        ItemDefinition target,
        bool success,
        bool slotReductionScroll,
        int usedSlotsBefore,
        int usedSlotsAfter,
        int enhancementBefore,
        int enhancementAfter)
    {
        string itemName = target && !string.IsNullOrWhiteSpace(target.displayName) ? target.displayName.Trim() : "Item";
        if (slotReductionScroll)
        {
            if (success)
            {
                string baseForSlot = target ? ResolveBaseDisplayName(target) : "Item";
                string beforeWord = usedSlotsBefore == 1 ? "slot" : "slots";
                string afterWord = usedSlotsAfter == 1 ? "slot" : "slots";
                GameLog.Add(
                    $"Successful Slot Reduction: {baseForSlot} ({usedSlotsBefore} {beforeWord} to {usedSlotsAfter} {afterWord})",
                    GameLog.ItemGainColor);
            }
            else
                GameLog.Add($"Failed Reduction: {itemName}", GameLog.ItemLostColor);
            return;
        }

        string baseName = target ? ResolveBaseDisplayName(target) : "Item";
        if (success)
        {
            if (enhancementAfter > enhancementBefore)
                GameLog.Add($"Successful Enhancement: {baseName} ({enhancementBefore} to {enhancementAfter})", GameLog.ItemGainColor);
            else
                GameLog.Add($"Successful Enhancement: {baseName}", GameLog.ItemGainColor);
        }
        else
            GameLog.Add($"Failed Enhancement: {itemName}", GameLog.ItemLostColor);
    }

    private static string ResolveBaseDisplayName(ItemDefinition def)
    {
        string displayName = def && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName.Trim() : "Item";
        return EnhancementSuffixRegex.Replace(displayName, "");
    }

    private static void ApplyFailureOutcome(Action destroyTarget, EnhancementScrollStats scroll)
    {
        if (scroll.failureOutcome != EnhancementScrollFailureOutcome.DestroyItem)
            return;

        if (scroll.destroyChanceOnFailure <= 0f)
            return;

        if (UnityEngine.Random.value > scroll.destroyChanceOnFailure)
            return;

        destroyTarget?.Invoke();
    }
}
