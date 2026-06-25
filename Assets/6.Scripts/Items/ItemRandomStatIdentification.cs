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

    /// <summary>Pending identification with at least one rolled affix still hidden from the tooltip.</summary>
    public static bool HasUnidentifiedRandomAffixes(ItemDatabase db, string itemId) =>
        IsPending(db, itemId) && CountHiddenRandomAffixes(db, itemId) > 0;

    public static string BuildMaskedAppendix(ItemDatabase db, string itemId)
    {
        int hiddenCount = CountHiddenRandomAffixes(db, itemId);
        return hiddenCount > 0 ? BuildQuestionMarkLines(hiddenCount) : "";
    }

    /// <summary>Base template when rolled clones clear their pool; otherwise <paramref name="def"/>.</summary>
    public static ItemDefinition ResolvePoolDefinition(ItemDatabase db, ItemDefinition def, string itemId)
    {
        if (def != null && def.HasRandomStatPool)
            return def;

        if (!db || string.IsNullOrWhiteSpace(itemId))
            return def;

        string baseId = db.GetBaseItemId(itemId);
        ItemDefinition baseDef = string.IsNullOrWhiteSpace(baseId) ? null : db.Get(baseId);
        if (baseDef != null && baseDef.HasRandomStatPool)
            return baseDef;

        return def;
    }

    public static bool HasRevealableRandomStatPool(ItemDatabase db, ItemDefinition def, string itemId)
    {
        ItemDefinition poolDef = ResolvePoolDefinition(db, def, itemId);
        return poolDef != null && poolDef.HasRandomStatPool;
    }

    public static bool AreRandomAffixesDiscovered(ItemDatabase db, string itemId) =>
        ItemStatDisplayNames.AreRandomAffixesDiscovered(db, itemId);

    public static void GetTooltipRandomStatFlags(
        ItemDatabase db,
        ItemDefinition def,
        string itemId,
        out bool maskUnrolledRandomStats,
        out bool showRandomStatPoolOptions)
    {
        bool hasPool = HasRevealableRandomStatPool(db, def, itemId);
        bool showPool = ItemTooltipAdvancedInput.IsHeld && hasPool && !AreRandomAffixesDiscovered(db, itemId);
        bool isRuntimeClone = db != null && !string.IsNullOrWhiteSpace(itemId) && db.IsRuntimeEnhancedItem(itemId);
        bool shopStyleMask = !isRuntimeClone && def != null && def.HasRandomStatPool;
        maskUnrolledRandomStats = !showPool && shopStyleMask;
        showRandomStatPoolOptions = showPool;
    }

    public static int CountHiddenRandomAffixes(ItemDatabase db, string itemId)
    {
        if (!IsPending(db, itemId))
            return 0;

        ItemDefinition rolled = db.Get(itemId);
        if (!rolled)
            return 0;

        // Deferred rolls: affixes are not on the item until identification.
        if (rolled.HasRandomStatPool)
            return ItemRandomStatRoller.GetRollCountForRarity(rolled.rarity, rolled);

        string baseId = db.GetBaseItemId(itemId);
        ItemDefinition baseline = string.IsNullOrWhiteSpace(baseId) ? null : db.Get(baseId);
        if (baseline == null || ReferenceEquals(rolled, baseline))
            return 0;

        int count = 0;
        CountBonusStats(ref count, rolled.bonusStats, baseline.bonusStats);

        if (rolled.IsWeapon)
            CountWeaponStats(ref count, rolled.weaponStats, baseline.weaponStats);

        if (rolled.IsArmour)
            CountArmourStats(ref count, rolled.armourStats, baseline.armourStats);

        if (!Mathf.Approximately(
                rolled.miscEffects.enemyRespawnTimeReductionSeconds,
                baseline.miscEffects.enemyRespawnTimeReductionSeconds))
            count++;

        return count;
    }

    private static string BuildQuestionMarkLines(int count)
    {
        if (count <= 0)
            return "";

        var lines = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                lines.Append('\n');
            lines.Append("??");
        }

        return lines.ToString();
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

        if (rolled.HasRandomStatPool && baseline != null)
        {
            var pool = new System.Collections.Generic.List<RandomStatPoolEntry>(rolled.RandomStatPoolEntries);
            ItemRandomStatRoller.ApplyRolls(rolled, pool, baseline.rarity);
            rolled.ClearRandomStatPool();
        }

        if (CountHiddenRandomAffixes(db, itemId) <= 0)
        {
            rolled.randomStatsPendingIdentification = false;
            return false;
        }

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

        if (rolled.IsArmour)
            CompareArmourStats(sb, rolled.armourStats, baseline.armourStats);

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
        TryAppendIntDelta(sb, "Armour", cur.armour, baseline.armour);
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
        TryAppendPercent01Delta(sb, "Melee Damage", cur.meleePhysicalDamagePercent, baseline.meleePhysicalDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.PhysicalDamagePercent, cur.globalPhysicalDamagePercent, baseline.globalPhysicalDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.RangedDamage, cur.rangedPhysicalDamagePercent, baseline.rangedPhysicalDamagePercent);
        TryAppendFloatDelta(sb, "Magic Damage", cur.magicDamage, baseline.magicDamage);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.MagicDamagePercent, cur.magicDamagePercent, baseline.magicDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.FireDamagePercent, cur.fireSkillDamagePercent, baseline.fireSkillDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.IceDamagePercent, cur.iceSkillDamagePercent, baseline.iceSkillDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.LightningDamagePercent, cur.lightningSkillDamagePercent, baseline.lightningSkillDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.SpellDamagePercent, cur.spellDamagePercent, baseline.spellDamagePercent);
        TryAppendPercent01Delta(sb, OffenseBonusDisplayNames.CorruptionDamagePercent, cur.corruptionDamagePercent, baseline.corruptionDamagePercent);
        TryAppendFloatDelta(sb, "Corruption Damage", cur.corruptionDamage, baseline.corruptionDamage);
        TryAppendPercentPointsDelta(sb, "Ability Power", cur.abilityPower, baseline.abilityPower);
        TryAppendPercent01Delta(sb, "Attack Speed", cur.attackSpeedPercent, baseline.attackSpeedPercent);
        TryAppendPercent01Delta(sb, "Cooldown Reduction", cur.abilityCooldownReductionFraction, baseline.abilityCooldownReductionFraction);
        TryAppendPercent01Delta(sb, "Minion Damage", cur.minionDamagePercent, baseline.minionDamagePercent);
        TryAppendPercent01Delta(sb, "Minion Attack Speed", cur.minionAttackSpeedPercent, baseline.minionAttackSpeedPercent);
        TryAppendPercent01Delta(sb, "Minion Crit Chance", cur.minionCritChance, baseline.minionCritChance);
        TryAppendPercent01Delta(sb, ItemStatDisplayNames.MinionMaxHp, cur.minionMaxLifePercent, baseline.minionMaxLifePercent);
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
        TryAppendFloatDelta(sb, "Shock Effect", cur.shockDamageTakenMultiplierBonus, baseline.shockDamageTakenMultiplierBonus, suffix: "%");
        TryAppendPercent01Delta(sb, ItemStatDisplayNames.AllElementalAilmentChance, cur.allElementalAilmentChance, baseline.allElementalAilmentChance);
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

    private static void CompareArmourStats(StringBuilder sb, ArmourStats cur, ArmourStats baseline)
    {
        TryAppendIntDelta(sb, "Armour", cur.armour, baseline.armour);
        TryAppendIntDelta(sb, "Magic Res", cur.magicResist, baseline.magicResist);
        TryAppendIntDelta(sb, "Corruption Res", cur.corruptionResist, baseline.corruptionResist);
        TryAppendPercent01Delta(sb, "Phys Block", cur.physBlockChance, baseline.physBlockChance);
        TryAppendIntDelta(sb, "Health", cur.bonusHealth, baseline.bonusHealth);
        TryAppendIntDelta(sb, "Energy", cur.bonusEnergy, baseline.bonusEnergy);
        TryAppendPercent01Delta(sb, "Energy Efficiency", cur.energyEfficiency, baseline.energyEfficiency);
        TryAppendIntDelta(sb, "Guard", cur.flatGuard, baseline.flatGuard);
        TryAppendPercent01Delta(sb, "Max Guard", cur.maxGuardPercent, baseline.maxGuardPercent);
    }

    private static void CountBonusStats(ref int count, BonusStats cur, BonusStats baseline)
    {
        CountIntDelta(ref count, cur.bonusHealth, baseline.bonusHealth);
        CountIntDelta(ref count, cur.bonusEnergy, baseline.bonusEnergy);
        CountIntDelta(ref count, cur.bonusMana, baseline.bonusMana);
        CountIntDelta(ref count, cur.armour, baseline.armour);
        CountIntDelta(ref count, cur.magicResist, baseline.magicResist);
        CountIntDelta(ref count, cur.corruptionResist, baseline.corruptionResist);
        CountFloatDelta(ref count, cur.physBlockChance, baseline.physBlockChance);
        CountFloatDelta(ref count, cur.lifeRegen, baseline.lifeRegen);
        CountFloatDelta(ref count, cur.energyRegen, baseline.energyRegen);
        CountFloatDelta(ref count, cur.manaRegen, baseline.manaRegen);
        CountFloatDelta(ref count, cur.energyEfficiency, baseline.energyEfficiency);
        CountFloatDelta(ref count, cur.lifeSteal, baseline.lifeSteal);
        CountFloatDelta(ref count, cur.moveSpeedPercent, baseline.moveSpeedPercent);
        CountFloatDelta(ref count, cur.physicalDamage, baseline.physicalDamage);
        CountFloatDelta(ref count, cur.meleePhysicalDamagePercent, baseline.meleePhysicalDamagePercent);
        CountFloatDelta(ref count, cur.globalPhysicalDamagePercent, baseline.globalPhysicalDamagePercent);
        CountFloatDelta(ref count, cur.rangedPhysicalDamagePercent, baseline.rangedPhysicalDamagePercent);
        CountFloatDelta(ref count, cur.magicDamage, baseline.magicDamage);
        CountFloatDelta(ref count, cur.magicDamagePercent, baseline.magicDamagePercent);
        CountFloatDelta(ref count, cur.fireSkillDamagePercent, baseline.fireSkillDamagePercent);
        CountFloatDelta(ref count, cur.iceSkillDamagePercent, baseline.iceSkillDamagePercent);
        CountFloatDelta(ref count, cur.lightningSkillDamagePercent, baseline.lightningSkillDamagePercent);
        CountFloatDelta(ref count, cur.corruptionDamagePercent, baseline.corruptionDamagePercent);
        CountFloatDelta(ref count, cur.corruptionDamage, baseline.corruptionDamage);
        CountFloatDelta(ref count, cur.abilityPower, baseline.abilityPower);
        CountFloatDelta(ref count, cur.attackSpeedPercent, baseline.attackSpeedPercent);
        CountFloatDelta(ref count, cur.abilityCooldownReductionFraction, baseline.abilityCooldownReductionFraction);
        CountFloatDelta(ref count, cur.minionDamagePercent, baseline.minionDamagePercent);
        CountFloatDelta(ref count, cur.minionAttackSpeedPercent, baseline.minionAttackSpeedPercent);
        CountFloatDelta(ref count, cur.minionCritChance, baseline.minionCritChance);
        CountFloatDelta(ref count, cur.minionMaxLifePercent, baseline.minionMaxLifePercent);
        CountFloatDelta(ref count, cur.critChanceBonus, baseline.critChanceBonus);
        CountFloatDelta(ref count, cur.critMultiplierBonus, baseline.critMultiplierBonus);
        CountFloatDelta(ref count, cur.attackRangeBonus, baseline.attackRangeBonus);
        CountFloatDelta(ref count, cur.bleedChance, baseline.bleedChance);
        CountFloatDelta(ref count, cur.bleedMultiplier, baseline.bleedMultiplier);
        CountFloatDelta(ref count, cur.poisonChance, baseline.poisonChance);
        CountFloatDelta(ref count, cur.poisonMultiplier, baseline.poisonMultiplier);
        CountFloatDelta(ref count, cur.poisonDurationBonus, baseline.poisonDurationBonus);
        CountIntDelta(ref count, cur.poisonMaxStacksBonus, baseline.poisonMaxStacksBonus);
        CountFloatDelta(ref count, cur.burnChance, baseline.burnChance);
        CountFloatDelta(ref count, cur.burnExplosionMultiplierBonus, baseline.burnExplosionMultiplierBonus);
        CountFloatDelta(ref count, cur.chillChance, baseline.chillChance);
        CountFloatDelta(ref count, cur.chillSlowPerStackBonus, baseline.chillSlowPerStackBonus);
        CountFloatDelta(ref count, cur.shockChance, baseline.shockChance);
        CountFloatDelta(ref count, cur.shockDamageTakenMultiplierBonus, baseline.shockDamageTakenMultiplierBonus);
        CountFloatDelta(ref count, cur.allElementalAilmentChance, baseline.allElementalAilmentChance);
        CountFloatDelta(ref count, cur.parryChance, baseline.parryChance);
        CountFloatDelta(ref count, cur.stunChance, baseline.stunChance);
    }

    private static void CountWeaponStats(ref int count, WeaponStats cur, WeaponStats baseline)
    {
        CountIntDelta(ref count, cur.minPhysicalDamage, baseline.minPhysicalDamage);
        CountIntDelta(ref count, cur.maxPhysicalDamage, baseline.maxPhysicalDamage);
        CountIntDelta(ref count, cur.minFireDamage, baseline.minFireDamage);
        CountIntDelta(ref count, cur.maxFireDamage, baseline.maxFireDamage);
        CountIntDelta(ref count, cur.minIceDamage, baseline.minIceDamage);
        CountIntDelta(ref count, cur.maxIceDamage, baseline.maxIceDamage);
        CountIntDelta(ref count, cur.minLightningDamage, baseline.minLightningDamage);
        CountIntDelta(ref count, cur.maxLightningDamage, baseline.maxLightningDamage);
        CountIntDelta(ref count, cur.minCorruptionDamage, baseline.minCorruptionDamage);
        CountIntDelta(ref count, cur.maxCorruptionDamage, baseline.maxCorruptionDamage);
        CountFloatDelta(ref count, cur.attacksPerSecond, baseline.attacksPerSecond);
        CountFloatDelta(ref count, cur.critChance, baseline.critChance);
        CountFloatDelta(ref count, cur.critMultiplier, baseline.critMultiplier);
        CountFloatDelta(ref count, cur.attackRange, baseline.attackRange);
        CountFloatDelta(ref count, cur.magicAilmentApplyChance, baseline.magicAilmentApplyChance);
    }

    private static void CountArmourStats(ref int count, ArmourStats cur, ArmourStats baseline)
    {
        CountIntDelta(ref count, cur.armour, baseline.armour);
        CountIntDelta(ref count, cur.magicResist, baseline.magicResist);
        CountIntDelta(ref count, cur.corruptionResist, baseline.corruptionResist);
        CountFloatDelta(ref count, cur.physBlockChance, baseline.physBlockChance);
        CountIntDelta(ref count, cur.bonusHealth, baseline.bonusHealth);
        CountIntDelta(ref count, cur.bonusEnergy, baseline.bonusEnergy);
        CountFloatDelta(ref count, cur.energyEfficiency, baseline.energyEfficiency);
        CountIntDelta(ref count, cur.flatGuard, baseline.flatGuard);
        CountFloatDelta(ref count, cur.maxGuardPercent, baseline.maxGuardPercent);
    }

    private static void CountIntDelta(ref int count, int current, int baseline)
    {
        if (current != baseline)
            count++;
    }

    private static void CountFloatDelta(ref int count, float current, float baseline)
    {
        if (!Mathf.Approximately(current, baseline))
            count++;
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
