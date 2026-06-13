using System.Text;
using UnityEngine;

/// <summary>
/// Hides rolled random affixes behind ?? until the player identifies an item from inventory.
/// </summary>
public static class ItemRandomStatIdentification
{
    public static readonly Color ActivityLogColor = new Color(0.72f, 0.58f, 1f, 1f);

    public static bool IsPending(ItemDatabase db, string itemId)
    {
        if (!db || string.IsNullOrWhiteSpace(itemId))
            return false;

        ItemDefinition def = db.Get(itemId);
        return def && def.randomStatsPendingIdentification;
    }

    public static string BuildMaskedAppendix(ItemDatabase db, string itemId)
    {
        if (!IsPending(db, itemId))
            return "";

        string baseId = db.GetBaseItemId(itemId);
        ItemDefinition baseDef = string.IsNullOrWhiteSpace(baseId) ? null : db.Get(baseId);
        return baseDef != null ? baseDef.BuildMaskedRandomStatTooltipAppendix() : "";
    }

    public static bool TryIdentify(ItemDatabase db, string itemId, out string activityLogMessage)
    {
        activityLogMessage = null;
        if (!db || string.IsNullOrWhiteSpace(itemId))
            return false;

        ItemDefinition rolled = db.Get(itemId);
        if (!rolled || !rolled.randomStatsPendingIdentification)
            return false;

        string baseId = db.GetBaseItemId(itemId);
        ItemDefinition baseline = string.IsNullOrWhiteSpace(baseId) ? null : db.Get(baseId);
        string summary = BuildRollSummary(rolled, baseline);
        string itemName = string.IsNullOrWhiteSpace(rolled.displayName) ? rolled.itemId : rolled.displayName;

        rolled.randomStatsPendingIdentification = false;
        activityLogMessage = string.IsNullOrWhiteSpace(summary)
            ? $"Identified {itemName}"
            : $"Identified {itemName} — {summary}";
        return true;
    }

    public static string BuildRollSummary(ItemDefinition rolled, ItemDefinition baseline)
    {
        if (!rolled || baseline == null || ReferenceEquals(rolled, baseline))
            return "";

        var sb = new StringBuilder();
        CompareBonusStats(sb, rolled.bonusStats, baseline.bonusStats);

        if (rolled.IsWeapon)
            CompareWeaponStats(sb, rolled, rolled.weaponStats, baseline.weaponStats);

        if (rolled.IsArmor)
            CompareArmorStats(sb, rolled.armorStats, baseline.armorStats);

        if (!Mathf.Approximately(rolled.miscEffects.enemyRespawnTimeReductionSeconds,
                baseline.miscEffects.enemyRespawnTimeReductionSeconds))
        {
            TryAppendFloatDelta(
                sb,
                "Enemy Respawn Reduction",
                rolled.miscEffects.enemyRespawnTimeReductionSeconds,
                baseline.miscEffects.enemyRespawnTimeReductionSeconds,
                suffix: "s");
        }

        return sb.ToString();
    }

    private static void CompareBonusStats(StringBuilder sb, BonusStats cur, BonusStats baseline)
    {
        TryAppendIntDelta(sb, "Health", cur.bonusHealth, baseline.bonusHealth);
        TryAppendIntDelta(sb, "Energy", cur.bonusEnergy, baseline.bonusEnergy);
        TryAppendIntDelta(sb, "Mana", cur.bonusMana, baseline.bonusMana);
        TryAppendIntDelta(sb, "Armour", cur.armor, baseline.armor);
        TryAppendIntDelta(sb, "Magic Res", cur.magicResist, baseline.magicResist);
        TryAppendIntDelta(sb, "Corruption Res", cur.corruptionResist, baseline.corruptionResist);
        TryAppendPercent01Delta(sb, "Phys Block", cur.physBlockChance, baseline.physBlockChance);
        TryAppendFloatDelta(sb, "Life Regen", cur.lifeRegen, baseline.lifeRegen, suffix: "/s");
        TryAppendFloatDelta(sb, "Energy Regen", cur.energyRegen, baseline.energyRegen, suffix: "/s");
        TryAppendFloatDelta(sb, "Mana Regen", cur.manaRegen, baseline.manaRegen, suffix: "/s");
        TryAppendPercent01Delta(sb, "Energy Efficiency", cur.energyEfficiency, baseline.energyEfficiency);
        TryAppendPercent01Delta(sb, "Life Steal", cur.lifeSteal, baseline.lifeSteal);
        TryAppendPercent01Delta(sb, "Move Speed", cur.moveSpeedPercent, baseline.moveSpeedPercent);
        TryAppendFloatDelta(sb, "Physical Damage", cur.physicalDamage, baseline.physicalDamage);
        TryAppendPercent01Delta(sb, "Melee Physical Damage", cur.meleePhysicalDamagePercent, baseline.meleePhysicalDamagePercent);
        TryAppendPercent01Delta(sb, "Global Physical Damage", cur.globalPhysicalDamagePercent, baseline.globalPhysicalDamagePercent);
        TryAppendPercent01Delta(sb, "Ranged Physical Damage", cur.rangedPhysicalDamagePercent, baseline.rangedPhysicalDamagePercent);
        TryAppendFloatDelta(sb, "Magic Damage", cur.magicDamage, baseline.magicDamage);
        TryAppendPercent01Delta(sb, "Magic Damage %", cur.magicDamagePercent, baseline.magicDamagePercent);
        TryAppendPercent01Delta(sb, "Fire Skill Damage", cur.fireSkillDamagePercent, baseline.fireSkillDamagePercent);
        TryAppendPercent01Delta(sb, "Ice Skill Damage", cur.iceSkillDamagePercent, baseline.iceSkillDamagePercent);
        TryAppendPercent01Delta(sb, "Lightning Skill Damage", cur.lightningSkillDamagePercent, baseline.lightningSkillDamagePercent);
        TryAppendPercent01Delta(sb, "Corruption Damage %", cur.corruptionDamagePercent, baseline.corruptionDamagePercent);
        TryAppendFloatDelta(sb, "Corruption Damage", cur.corruptionDamage, baseline.corruptionDamage);
        TryAppendPercentPointsDelta(sb, "Ability Power", cur.abilityPower, baseline.abilityPower);
        TryAppendPercent01Delta(sb, "Attack Speed", cur.attackSpeedPercent, baseline.attackSpeedPercent);
        TryAppendPercent01Delta(sb, "Cooldown Reduction", cur.abilityCooldownReductionFraction, baseline.abilityCooldownReductionFraction);
        TryAppendPercent01Delta(sb, "Minion Damage", cur.minionDamagePercent, baseline.minionDamagePercent);
        TryAppendPercent01Delta(sb, "Minion Attack Speed", cur.minionAttackSpeedPercent, baseline.minionAttackSpeedPercent);
        TryAppendPercent01Delta(sb, "Minion Crit Chance", cur.minionCritChance, baseline.minionCritChance);
        TryAppendPercent01Delta(sb, "Minion Max Life", cur.minionMaxLifePercent, baseline.minionMaxLifePercent);
        TryAppendPercent01Delta(sb, "Crit Chance", cur.critChanceBonus, baseline.critChanceBonus);
        TryAppendPercent01Delta(sb, "Crit Multi", cur.critMultiplierBonus, baseline.critMultiplierBonus);
        TryAppendFloatDelta(sb, "Range", cur.attackRangeBonus, baseline.attackRangeBonus);
        TryAppendPercent01Delta(sb, "Bleed Chance", cur.bleedChance, baseline.bleedChance);
        TryAppendPercent01Delta(sb, "Bleed Multi", cur.bleedMultiplier, baseline.bleedMultiplier);
        TryAppendPercent01Delta(sb, "Poison Chance", cur.poisonChance, baseline.poisonChance);
        TryAppendPercent01Delta(sb, "Poison Multi", cur.poisonMultiplier, baseline.poisonMultiplier);
        TryAppendFloatDelta(sb, "Poison Duration", cur.poisonDurationBonus, baseline.poisonDurationBonus, suffix: "s");
        TryAppendIntDelta(sb, "Poison Max Stacks", cur.poisonMaxStacksBonus, baseline.poisonMaxStacksBonus);
        TryAppendPercent01Delta(sb, "Burn Chance", cur.burnChance, baseline.burnChance);
        TryAppendPercent01Delta(sb, "Burn Multiplier", cur.burnExplosionMultiplierBonus, baseline.burnExplosionMultiplierBonus);
        TryAppendPercent01Delta(sb, "Chill Chance", cur.chillChance, baseline.chillChance);
        TryAppendFloatDelta(sb, "Chill Effect", cur.chillSlowPerStackBonus, baseline.chillSlowPerStackBonus, suffix: "% slow / stack");
        TryAppendPercent01Delta(sb, "Shock Chance", cur.shockChance, baseline.shockChance);
        TryAppendFloatDelta(sb, "Shock Damage Amount", cur.shockDamageTakenMultiplierBonus, baseline.shockDamageTakenMultiplierBonus, suffix: "%");
        TryAppendPercent01Delta(sb, "Parry Chance", cur.parryChance, baseline.parryChance);
        TryAppendPercent01Delta(sb, "Stun Chance", cur.stunChance, baseline.stunChance);
    }

    private static void CompareWeaponStats(
        StringBuilder sb,
        ItemDefinition rolled,
        WeaponStats cur,
        WeaponStats baseline)
    {
        TryAppendIntDelta(sb, "Min Physical Damage", cur.minPhysicalDamage, baseline.minPhysicalDamage);
        TryAppendIntDelta(sb, "Max Physical Damage", cur.maxPhysicalDamage, baseline.maxPhysicalDamage);
        TryAppendIntDelta(sb, "Min Fire Damage", cur.minFireDamage, baseline.minFireDamage);
        TryAppendIntDelta(sb, "Max Fire Damage", cur.maxFireDamage, baseline.maxFireDamage);
        TryAppendIntDelta(sb, "Min Ice Damage", cur.minIceDamage, baseline.minIceDamage);
        TryAppendIntDelta(sb, "Max Ice Damage", cur.maxIceDamage, baseline.maxIceDamage);
        TryAppendIntDelta(sb, "Min Lightning Damage", cur.minLightningDamage, baseline.minLightningDamage);
        TryAppendIntDelta(sb, "Max Lightning Damage", cur.maxLightningDamage, baseline.maxLightningDamage);
        TryAppendIntDelta(sb, "Min Corruption Damage", cur.minCorruptionDamage, baseline.minCorruptionDamage);
        TryAppendIntDelta(sb, "Max Corruption Damage", cur.maxCorruptionDamage, baseline.maxCorruptionDamage);
        TryAppendFloatDelta(sb, "Attack Speed", cur.attacksPerSecond, baseline.attacksPerSecond, suffix: " atk/s");
        TryAppendPercent01Delta(sb, "Crit Chance", cur.critChance, baseline.critChance);
        TryAppendFloatDelta(sb, "Crit Multi", cur.critMultiplier, baseline.critMultiplier);
        TryAppendFloatDelta(sb, "Range", cur.attackRange, baseline.attackRange);
        string magicAilmentLabel = ItemRandomStatRoller.GetRandomStatDisplayName(
            RandomItemStatType.WeaponMagicAilmentApplyChance,
            rolled);
        TryAppendPercent01Delta(sb, magicAilmentLabel, cur.magicAilmentApplyChance, baseline.magicAilmentApplyChance);
    }

    private static void CompareArmorStats(StringBuilder sb, ArmorStats cur, ArmorStats baseline)
    {
        TryAppendIntDelta(sb, "Armour", cur.armor, baseline.armor);
        TryAppendIntDelta(sb, "Magic Res", cur.magicResist, baseline.magicResist);
        TryAppendIntDelta(sb, "Corruption Res", cur.corruptionResist, baseline.corruptionResist);
        TryAppendPercent01Delta(sb, "Phys Block", cur.physBlockChance, baseline.physBlockChance);
        TryAppendIntDelta(sb, "Health", cur.bonusHealth, baseline.bonusHealth);
        TryAppendIntDelta(sb, "Energy", cur.bonusEnergy, baseline.bonusEnergy);
        TryAppendPercent01Delta(sb, "Energy Efficiency", cur.energyEfficiency, baseline.energyEfficiency);
        TryAppendIntDelta(sb, "Guard", cur.flatGuard, baseline.flatGuard);
        TryAppendPercent01Delta(sb, "Max Guard", cur.maxGuardPercent, baseline.maxGuardPercent);
    }

    private static void TryAppendIntDelta(StringBuilder sb, string label, int current, int baseline)
    {
        int delta = current - baseline;
        if (delta == 0)
            return;

        AppendPart(sb, FormatSignedInt(delta, label));
    }

    private static void TryAppendFloatDelta(
        StringBuilder sb,
        string label,
        float current,
        float baseline,
        string suffix = "")
    {
        if (Mathf.Approximately(current, baseline))
            return;

        float delta = current - baseline;
        AppendPart(sb, $"{label} {FormatSignedFloat(delta)}{suffix}");
    }

    private static void TryAppendPercent01Delta(StringBuilder sb, string label, float current, float baseline)
    {
        if (Mathf.Approximately(current, baseline))
            return;

        float deltaPct = (current - baseline) * 100f;
        AppendPart(sb, $"{label} {FormatSignedFloat(deltaPct)}%");
    }

    private static void TryAppendPercentPointsDelta(StringBuilder sb, string label, float current, float baseline)
    {
        if (Mathf.Approximately(current, baseline))
            return;

        float delta = current - baseline;
        AppendPart(sb, $"{label} {FormatSignedFloat(delta)}%");
    }

    private static void AppendPart(StringBuilder sb, string part)
    {
        if (string.IsNullOrWhiteSpace(part))
            return;

        if (sb.Length > 0)
            sb.Append(", ");

        sb.Append(part);
    }

    private static string FormatSignedInt(int delta, string label) =>
        delta > 0 ? $"{label} +{delta}" : $"{label} {delta}";

    private static string FormatSignedFloat(float value)
    {
        float abs = Mathf.Abs(value);
        string text = abs >= 10f ? value.ToString("0.#") : value.ToString("0.##");
        return value > 0f ? $"+{text}" : text;
    }
}
