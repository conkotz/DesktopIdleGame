using UnityEngine;

/// <summary>
/// Single source of truth for shared UI / item / HUD tooltip copy.
/// <see cref="UIHoverTooltip"/> resolves text by <c>GameObject.name</c> unless disabled.
/// </summary>
public static class GameTooltipTexts
{
    public const string BurnTitle = "Burn";

    /// <summary>Burn overview (stats hover, debuff icon). Matches equipment panel: legend + one short line.</summary>
    public static readonly string BurnMechanicDescription =
        "chance | tick mult (gear) | duration | stacks to combust\n\n" +
        "Fire hits apply burn stacks. At max stacks, combust deals 10 seconds of your current burn tick damage as magic, then clears.";

    public static string FormatBurnHudBody(int stacks, int stacksToCombust) =>
        $"Stacks: {stacks}/{Mathf.Max(1, stacksToCombust)}\n\n" + BurnMechanicDescription;

    public const string BleedTitle = "Bleed";
    public static readonly string BleedDescription =
        "chance | damage mult | duration\n\n" +
        "Damage over time from physical hits. Damage is based on the damage of the hit that caused the bleed.";

    public const string PoisonTitle = "Poison";
    public static string FormatPoisonHudBody(int stacks) =>
        $"Taking poison damage over time.\nStacks: {stacks}";

    public const string ShockTitle = "Shock";
    public static readonly string ShockDescription =
        "chance | damage taken | duration\n\n" +
        "Target takes increased damage from hits while shocked.";

    public const string ChillTitle = "Chill";
    public static readonly string ChillDescription =
        "chance | slow/stack | duration | max stacks\n\n" +
        "Slows the target; stacks refresh when re-applied.";

    /// <summary>Normalize hierarchy names: trim, strip quotes, collapse spaces (handles <c>'EnergyText '</c>, <c>ArmourText  </c>).</summary>
    public static string NormalizeUiElementName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;
        string t = raw.Trim().Trim('\'', '"');
        while (t.Contains("  "))
            t = t.Replace("  ", " ");
        return t;
    }

    /// <summary>Resolve hover copy for equipment / stats UI objects by <see cref="GameObject.name"/>.</summary>
    public static bool TryGetForUiElement(string gameObjectName, out string title, out string description)
    {
        title = null;
        description = null;
        string key = NormalizeUiElementName(gameObjectName);
        if (string.IsNullOrEmpty(key))
            return false;

        switch (key)
        {
            case "DPSText":
                title = "DPS (Damage Per Second)";
                description =
                    "Your total expected damage per second.\n\n" +
                    "Includes attack speed, critical strikes, damage types, abilities currently slotted (assuming you have energy), and expected ailment damage (Bleed, Poison). " +
                    "Useful as a single offensive summary.";
                return true;

            case "DMGText":
                title = "Damage";
                description =
                    "Rough total damage per hit (min–max), before enemy mitigation.\n\n" +
                    "Combines all damage types on your basic attack profile.";
                return true;

            case "DMGSplitText":
                title = "Damage breakdown";
                description =
                    "How your hit splits between Physical, Magic (elemental total), and Corruption (corruption damage cannot crit).";
                return true;

            case "AttackSpdText":
                title = "Attack Speed";
                description = "How many basic attacks you perform per second.";
                return true;

            case "AttackRangeText":
                title = "Attack Range";
                description =
                    "How far your basic attack reaches in world units.\n\n" +
                    "Ranged and magic weapons typically have larger values than melee.";
                return true;

            case "CritChanceText":
                title = "Critical chance";
                description =
                    "Chance for your hits to critically strike. This also applies to abilities.";
                return true;

            case "CritDMGText":
                title = "Critical damage";
                description =
                    "How much extra damage your critical hits deal. This also applies to abilities.";
                return true;

            case "LifeStealText":
                title = "Life Steal";
                description =
                    "Percentage of damage dealt returned as healing.\n\n" +
                    "Applies to hit damage and does not apply to ailments.";
                return true;

            case "StunChanceText":
                title = "Stun chance";
                description =
                    "Chance on hit to stun enemies for 3 seconds.\n\n" +
                    "Stunned enemies cannot move, attack, or regenerate.";
                return true;

            case "AbilityPowerText":
                title = "Ability Power";
                description = "Scales your abilities to do bonus damage (This does not apply to minions).";
                return true;

            case "MinionDamageText":
                title = "Minion damage";
                description =
                    "Bonus damage to your minions and summons (additive %).\n\n" +
                    "Applies on the owner no matter whether a minion inherits your hit damage or uses pure minion source damage. " +
                    "Does not affect your hero DPS until summon combat is implemented.";
                return true;

            case "MinionAttackSpeedText":
                title = "Minion attack speed";
                description =
                    "Bonus attack speed for your minions (additive %).\n\n" +
                    "Aggregate is floored so minion APS never goes below 10% of base in future combat code. " +
                    "Does not change your hero attack speed.";
                return true;

            case "MinionCritChanceText":
                title = "Minion critical chance";
                description =
                    "Additive crit chance for minion hits (same 0–1 scale as hero crit).\n\n" +
                    "Minion critical strikes always deal ×1.5 total damage (+50% bonus); that multiplier is not scalable.";
                return true;

            case "MinionMaxLifeText":
                title = "Minion max life";
                description =
                    "Bonus maximum life for your minions and summons (additive %).\n\n" +
                    "Applies when minion HP is implemented. Inherited weapon-hit minions gain half as much from this stat as pure minion-source summons.";
                return true;

            case "PhysicalBonusText":
            case "GlobalPhysicalBonusText":
                title = "All physical";
                description = "Increases all Physical damage you deal.";
                return true;

            case "MagBonusText":
            case "GlobalMagBonusText":
                title = "All magic";
                description = "Increases all Magic damage you deal.";
                return true;

            case "CorruptionBonusText":
            case "GlobalCorruptionBonusText":
                title = "All corruption";
                description = "Increases all Corruption damage you deal.";
                return true;

            case "FireBonusText":
                title = "Fire damage";
                description = "Extra fire damage on hits that use fire (gear and supports).";
                return true;

            case "IceBonusText":
                title = "Ice damage";
                description = "Extra ice damage on hits that use ice (gear and supports).";
                return true;

            case "LightningBonusText":
                title = "Lightning damage";
                description = "Extra lightning damage on hits that use lightning (gear and supports).";
                return true;

            case "MeleePhysBonusText":
            case "MeleeDamageBonusText":
            case "ConditionalMeleePhysBonusText":
                title = "Melee damage";
                description =
                    "Increases all damage you deal from melee attacks: Physical, Magic (elemental on the melee weapon), " +
                    "and Corruption portions of the hit.";
                return true;

            case "RangedPhysBonusText":
            case "RangedDamageBonusText":
            case "ConditionalRangedPhysBonusText":
                title = "Ranged damage";
                description =
                    "Increases all damage you deal from ranged attacks: Physical, Magic (elemental on the weapon), " +
                    "and Corruption portions of the hit.";
                return true;

            case "BleedText":
                title = BleedTitle;
                description =
                    "chance | damage mult | duration\n\n" +
                    "Damage over time from physical hits. Damage is based on the damage of the hit that caused the bleed.";
                return true;

            case "PoisonText":
                title = "Poison";
                description =
                    "chance | damage mult | duration | max stacks\n\n" +
                    "Damage over time from corruption. Damage is based on the damage of the hit that caused the poison.";
                return true;

            case "ChillText":
                title = ChillTitle;
                description = ChillDescription;
                return true;

            case "BurnText":
                title = BurnTitle;
                description = BurnMechanicDescription;
                return true;

            case "SockText":
            case "ShockText":
                title = ShockTitle;
                description = ShockDescription;
                return true;

            case "HpText":
                title = "Health";
                description =
                    "Your total health pool.\n\n" +
                    "When this reaches 0, you are defeated. Higher health improves survivability against all damage types.";
                return true;

            case "EnergyText":
                title = "Energy";
                description =
                    "Resource used for gathering, movement abilities, and some actions.\n\n" +
                    "Regenerates over time based on your Energy Regeneration.";
                return true;

            case "HpRegenText":
                title = "Health Regeneration";
                description =
                    "Health restored per second.\n\n" +
                    "Provides steady recovery between and during fights.";
                return true;

            case "EnergyRegenText":
                title = "Energy Regeneration";
                description =
                    "Base regen restores 10% of your max energy per second.\n\n" +
                    "The stat line also shows the resulting amount per second at your current max energy. " +
                    "Increasing max energy increases how much you recover. Gear and buffs can add flat energy per second on top.";
                return true;

            case "GuardFlatText":
                title = "Guard (flat)";
                description =
                    "Total flat guard amount.\n\n" +
                    "Out of combat, guard refills up to your guard amount. This cannot surpass your max guard amount. " +
                    "(Incoming damage is taken by guard before health.";
                return true;

            case "MaxGuardPercentText":
            case "MaxGuardText":
                title = "Max Guard";
                description =
                    "Max guard basine is equal to your max hp.\n\n" +
                    "Gaining max guard increasing the guard you can obtain above your max hp.";
                return true;

            case "MrText":
                title = "Magic Resist";
                description =
                    "Reduces magic damage taken.\n\n" +
                    "Stacks with armour and other mitigation; compare to enemy damage types.";
                return true;

            case "BlockText":
                title = "Block Chance";
                description =
                    "Chance to partially block incoming physical hits.\n\n" +
                    "When block succeeds, damage is reduced by your Block Mitigation % instead of being negated entirely.";
                return true;

            case "BlockMitigationText":
                title = "Block Mitigation";
                description =
                    "Percent of physical damage prevented when a block succeeds.\n\n" +
                    "Base mitigation is 70%. Talents and gear can raise it (e.g. Tactician Secondary Specialist with a shield).";
                return true;

            case "MovespeedText":
                title = "Movement Speed";
                description =
                    "How fast your character moves in the world.\n\n" +
                    "Affected by gear, buffs, and chill/slow effects.";
                return true;

            case "ArmourText":
                title = "Armour";
                description =
                    "Reduces physical damage taken.\n\n" +
                    "Higher armour is stronger against enemies that deal mostly physical damage.";
                return true;

            case "CorruptionResistText":
            case "CorrResText":
                title = "Corruption resist";
                description =
                    "Rating that reduces corruption damage taken (shown with approximate reduction).";
                return true;

            case "PickaxeSpeedText":
            case "AxeSpeedText":
            case "RodSpeedText":
                title = "Tool Speed";
                description =
                    "How quickly this tool performs gathering actions.\n\n" +
                    "Higher speed means faster gathering cycles and more resources over time.";
                return true;

            case "PickaxeGritText":
            case "AxeGritText":
            case "RodGritText":
                title = "Tool Grit";
                description =
                    "Chance to double the base gather yield.\n\n" +
                    "Only doubles the main/base resource roll and does not duplicate bonus-find drops.";
                return true;

            case "PickaxeBonusFindText":
            case "AxeBonusFindText":
            case "RodBonusFindText":
                title = "Bonus Find";
                description =
                    "Extra chance to find bonus resources while gathering.\n\n" +
                    "Applies per gather action and stacks with other bonus find sources.";
                return true;

            case "PickaxeStaminaEfficiencyText":
            case "AxeStaminaEfficiencyText":
            case "RodStaminaEfficiencyText":
                title = "Stamina Efficiency";
                description =
                    "Improves gathering stamina efficiency.\n\n" +
                    "Higher efficiency lets you gather longer before running out of stamina.";
                return true;

            case "StatsLHeaderLabel":
                title = "Combat Power";
                description =
                    "A combined rating of overall combat strength.\n\n" +
                    "Based on offense, defense, sustain, and mobility. Higher is generally stronger.";
                return true;

            default:
                return false;
        }
    }
}
