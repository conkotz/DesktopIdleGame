using System;
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
        LogRejectedScrollTargetAttempt(scrollDef, targetDef);
        if (!IsValidScrollTarget(scrollDef, targetDef))
            return false;

        ItemDefinition enhancedTarget = targetDef;
        if (!inventory.IsRuntimeEnhancedItem(targetSlot.itemId))
        {
            enhancedTarget = inventory.CreateRuntimeEnhancedItem(targetDef);
            if (!enhancedTarget)
                return false;

            targetSlot.itemId = enhancedTarget.itemId;
            targetSlot.amount = 1;
            inventory.ReplaceSlot(targetSlotIndex, targetSlot);
        }

        bool attempted = TryApplyScroll(
            inventory,
            scrollSlotIndex,
            scrollDef,
            enhancedTarget,
            targetDef,
            () => inventory.RemoveStackAtSlot(targetSlotIndex),
            out success);

        if (!attempted)
            return false;

        // Notify UI and save systems after mutating the runtime definition.
        inventory.ReplaceSlot(targetSlotIndex, inventory.GetSlot(targetSlotIndex));
        return true;
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
        LogRejectedScrollTargetAttempt(scrollDef, targetDef);
        if (!IsValidScrollTarget(scrollDef, targetDef))
            return false;

        ItemDefinition enhancedTarget = targetDef;
        if (!inventory.IsRuntimeEnhancedItem(targetItemId))
        {
            enhancedTarget = inventory.CreateRuntimeEnhancedItem(targetDef);
            if (!enhancedTarget)
                return false;

            replaceTargetItemId(enhancedTarget.itemId);
        }

        return TryApplyScroll(
            inventory,
            scrollSlotIndex,
            scrollDef,
            enhancedTarget,
            targetDef,
            clearTargetItem,
            out success);
    }

    private static bool TryApplyScroll(
        Inventory inventory,
        int scrollSlotIndex,
        ItemDefinition scrollDef,
        ItemDefinition enhancedTarget,
        ItemDefinition previousTargetDef,
        Action destroyTarget,
        out bool success)
    {
        success = false;
        bool slotReductionScroll = IsSlotReductionScroll(scrollDef);
        int usedSlotsBefore = enhancedTarget.UsedUpgradeSlots;

        if (inventory.RemoveAmountAtSlot(scrollSlotIndex, 1) != 1)
            return false;

        if (scrollDef.enhancementScrollStats.consumeSlotOnFailure)
        {
            enhancedTarget.usedUpgradeSlots = Mathf.Clamp(
                enhancedTarget.usedUpgradeSlots + 1,
                0,
                Mathf.Max(0, enhancedTarget.MaxUpgradeSlots));
        }

        success = UnityEngine.Random.value <= scrollDef.EnhancementScrollSuccessChance;
        if (success)
        {
            ApplyModifier(enhancedTarget, scrollDef.enhancementScrollStats);
            int usedSlotsAfter = enhancedTarget.UsedUpgradeSlots;

            if (!slotReductionScroll)
            {
                enhancedTarget.successfulEnhancements = Mathf.Clamp(
                    enhancedTarget.successfulEnhancements + 1,
                    0,
                    Mathf.Max(0, enhancedTarget.MaxSuccessfulEnhancements));
                ApplyEnhancedDisplayName(enhancedTarget, previousTargetDef);
            }

            LogResult(enhancedTarget, true, slotReductionScroll, usedSlotsBefore, usedSlotsAfter);
        }
        else
        {
            LogResult(enhancedTarget, false, slotReductionScroll, usedSlotsBefore, usedSlotsBefore);
            ApplyFailureOutcome(destroyTarget, scrollDef.enhancementScrollStats);
        }

        return true;
    }

    private static bool IsValidScrollTarget(ItemDefinition scrollDef, ItemDefinition targetDef)
    {
        if (!scrollDef || !targetDef)
            return false;
        if (scrollDef.itemKind != ItemKind.EnhancementScroll)
            return false;
        if (targetDef.itemKind == ItemKind.EnhancementScroll)
            return false;
        if (targetDef.MaxUpgradeSlots <= 0)
            return false;
        if (!IsSlotReductionScroll(scrollDef) && targetDef.HasReachedEnhancementCap)
            return false;

        return scrollDef.CanUseEnhancementScrollOn(targetDef);
    }

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
                target.bonusStats.bonusHealth = ApplyIntValue(target.bonusStats.bonusHealth, value, percent);
                if (target.IsArmor)
                    target.armorStats.bonusHealth = ApplyIntValue(target.armorStats.bonusHealth, value, percent);
                break;

            case EnhancementScrollTargetStat.Energy:
                target.bonusStats.bonusEnergy = ApplyIntValue(target.bonusStats.bonusEnergy, value, percent);
                if (target.IsArmor)
                    target.armorStats.bonusEnergy = ApplyIntValue(target.armorStats.bonusEnergy, value, percent);
                break;

            case EnhancementScrollTargetStat.Mana:
                target.bonusStats.bonusMana = ApplyIntValue(target.bonusStats.bonusMana, value, percent);
                break;

            case EnhancementScrollTargetStat.Armor:
                target.bonusStats.armor = ApplyIntValue(target.bonusStats.armor, value, percent);
                if (target.IsArmor)
                    target.armorStats.armor = ApplyIntValue(target.armorStats.armor, value, percent);
                break;

            case EnhancementScrollTargetStat.MagicResist:
                target.bonusStats.magicResist = ApplyIntValue(target.bonusStats.magicResist, value, percent);
                if (target.IsArmor)
                    target.armorStats.magicResist = ApplyIntValue(target.armorStats.magicResist, value, percent);
                break;

            case EnhancementScrollTargetStat.CorruptionResist:
                target.bonusStats.corruptionResist = ApplyIntValue(target.bonusStats.corruptionResist, value, percent);
                if (target.IsArmor)
                    target.armorStats.corruptionResist = ApplyIntValue(target.armorStats.corruptionResist, value, percent);
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
                if (target.IsWeapon)
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

            case EnhancementScrollTargetStat.UpgradeSlotReduction:
                int slotsToReduce = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(value)));
                target.usedUpgradeSlots = Mathf.Clamp(
                    target.usedUpgradeSlots - slotsToReduce,
                    0,
                    Mathf.Max(0, target.MaxUpgradeSlots));
                break;
        }
    }

    private static bool IsSlotReductionScroll(ItemDefinition scrollDef)
    {
        return scrollDef &&
               scrollDef.itemKind == ItemKind.EnhancementScroll &&
               scrollDef.enhancementScrollStats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction;
    }

    private static void LogRejectedScrollTargetAttempt(ItemDefinition scrollDef, ItemDefinition targetDef)
    {
        if (!scrollDef || scrollDef.itemKind != ItemKind.EnhancementScroll || !targetDef)
            return;

        string itemName = !string.IsNullOrWhiteSpace(targetDef.displayName) ? targetDef.displayName.Trim() : "Item";
        string scrollName = !string.IsNullOrWhiteSpace(scrollDef.displayName) ? scrollDef.displayName.Trim() : "Scroll";

        if (IsSlotReductionScroll(scrollDef))
        {
            if (!scrollDef.EnhancementScrollCanTarget(targetDef))
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

        if (!scrollDef.EnhancementScrollCanTarget(targetDef))
        {
            GameLog.Add($"Cannot use {scrollName} on {itemName}", GameLog.ItemLostColor);
            return;
        }

        if (!targetDef.HasAvailableUpgradeSlot)
            GameLog.Add($"Cannot enhance, no upgrade slots available: {itemName}", GameLog.ItemLostColor);
    }

    private static float ApplyValue(float current, float value, bool percent)
    {
        return percent ? current * (1f + value) : current + value;
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
        int usedSlotsAfter)
    {
        string itemName = target && !string.IsNullOrWhiteSpace(target.displayName) ? target.displayName.Trim() : "Item";
        if (slotReductionScroll)
        {
            if (success)
                GameLog.Add($"Successful Reduction: {itemName} {usedSlotsBefore}->{usedSlotsAfter}", GameLog.ItemGainColor);
            else
                GameLog.Add($"Failed Reduction: {itemName}", GameLog.ItemLostColor);
            return;
        }

        if (success)
            GameLog.Add($"Successful Enhancement: {itemName}", GameLog.ItemGainColor);
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

        bool destroy = scroll.destroyChanceOnFailure <= 0f || UnityEngine.Random.value <= scroll.destroyChanceOnFailure;
        if (!destroy)
            return;

        destroyTarget?.Invoke();
    }
}
