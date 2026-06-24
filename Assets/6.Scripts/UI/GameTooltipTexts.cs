using UnityEngine;

/// <summary>
/// Single source of truth for shared UI / item / HUD tooltip copy.
/// <see cref="UIHoverTooltip"/> resolves text by <c>GameObject.name</c> unless disabled.
/// </summary>
public static class GameTooltipTexts
{
    public const string BurnTitle = "Burn";

    private const string InheritedMinionScalingNote = "\n\n(50% reduced for inherited minions)";

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
                    "Your total expected damage per second on a single enemy.\n\n" +
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
                    "Chance for your hits to critically strike.\n\n" +
                    "Applies to basic attacks, abilities, and spells. Ailment damage (Bleed, Poison, Burn ticks) never crits.";
                return true;

            case "CritDMGText":
                title = "Critical damage";
                description =
                    "How much extra damage your critical hits deal.\n\n" +
                    "Applies to basic attacks, abilities, and spells. Ailment damage never crits.\n\n" +
                    "Base crit damage is +50%.";
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
                description =
                    "Percentage bonus to ability damage only.\n\n" +
                    "Each point adds the same percent to ability hits (+25% ability power = +25% ability damage). " +
                    "Does not affect basic attacks or minions.";
                return true;

            case "CooldownReductionText":
                title = "Cooldown reduction";
                description =
                    "Reduces ability cooldowns by the listed percentage.\n\n" +
                    "Stacks additively from gear and passives.";
                return true;

            case "ConditionalMinionDmgText":
            case "MinionDamageText":
                title = "Minion damage";
                description =
                    "Percentage bonus damage for your minions and summons." +
                    InheritedMinionScalingNote;
                return true;

            case "ConditionalMinionAtkSpeedText":
            case "MinionAttackSpeedText":
                title = "Minion attack speed";
                description =
                    "Percentage bonus attack speed for your minions. " +
                    "Total minion attack speed cannot fall below 10% of base." +
                    InheritedMinionScalingNote;
                return true;

            case "ConditionalMinionCritRateText":
            case "MinionCritChanceText":
                title = "Minion critical chance";
                description =
                    "Additive critical strike chance for minion hits. " +
                    "Minion crits always deal ×1.5 total damage (+50% bonus); that multiplier is fixed." +
                    InheritedMinionScalingNote;
                return true;

            case "ConditionalMinionMaxLifeText":
            case "MinionMaxLifeText":
                title = "Minion max HP";
                description =
                    "Percentage bonus to minion and summon maximum HP." +
                    InheritedMinionScalingNote;
                return true;

            case "PhysicalBonusText":
            case "GlobalPhysicalBonusText":
                title = OffenseBonusDisplayNames.PhysicalDamagePercent;
                description =
                    "Bonus to all Physical damage you deal — both melee and ranged weapon physical portions.\n\n" +
                    "Stacks with weapon-class bonuses (Melee Damage / Ranged Damage).";
                return true;

            case "MagBonusText":
            case "GlobalMagBonusText":
                title = OffenseBonusDisplayNames.MagicDamagePercent;
                description =
                    "Bonus to Magic damage while using magic weapons or magic-tagged attacks.\n\n" +
                    "Included in your weapon damage range on elemental gear.";
                return true;

            case "CorruptionBonusText":
            case "GlobalCorruptionBonusText":
                title = OffenseBonusDisplayNames.CorruptionDamagePercent;
                description =
                    "Bonus to Corruption damage on hits that include a corruption portion.\n\n" +
                    "Applies to weapon corruption splits and corruption-tagged skills.";
                return true;

            case "FireBonusText":
                title = OffenseBonusDisplayNames.FireDamagePercent;
                description = "Bonus fire damage on hits and skills that use fire.";
                return true;

            case "IceBonusText":
                title = OffenseBonusDisplayNames.IceDamagePercent;
                description = "Bonus ice damage on hits and skills that use ice.";
                return true;

            case "LightningBonusText":
                title = OffenseBonusDisplayNames.LightningDamagePercent;
                description =
                    "Bonus lightning damage on hits and skills that use lightning.\n\n" +
                    "Ranged skill-tree lightning bonuses only apply while a bow is equipped.";
                return true;

            case "GlobalSpellBonusText":
                title = OffenseBonusDisplayNames.SpellDamagePercent;
                description =
                    "Percentage bonus to spell damage only (starter spells, slotted spells).\n\n" +
                    "Does not affect basic wand attacks or non-spell abilities.";
                return true;

            case "MeleePhysBonusText":
            case "MeleeDamageBonusText":
            case "ConditionalMeleePhysBonusText":
                title = OffenseBonusDisplayNames.MeleeDamage;
                description =
                    "Bonus damage while using a melee weapon or melee-tagged attacks.\n\n" +
                    "Applies to the full hit (physical, magic/elemental, and corruption portions). " +
                    "Only active when a melee weapon is equipped.";
                return true;

            case "RangedPhysBonusText":
            case "RangedDamageBonusText":
            case "ConditionalRangedPhysBonusText":
                title = OffenseBonusDisplayNames.RangedDamage;
                description =
                    "Bonus damage while using a ranged weapon or ranged-tagged attacks.\n\n" +
                    "Applies to the full hit (physical, magic/elemental, and corruption portions). " +
                    "Only active when a ranged weapon is equipped.";
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
                    "Your total health pool after flat bonuses and percentage increases.\n\n" +
                    "When this reaches 0, you are defeated. Higher health improves survivability against all damage types.";
                return true;

            case "Hp%Text":
            case "HpPercentText":
                title = "Max HP %";
                description =
                    "Increases max health by a percentage of your flat max HP (base, gear flat health, and endurance flat health).\n\n" +
                    "Example: +2% with 100 flat max HP adds 2 HP; with 1,000 flat max HP it adds 20 HP.";
                return true;

            case "EnergyText":
                title = "Energy";
                description =
                    "Resource used for non magic abilities. Regenerate 10 energy per second.\n\n" +
                    "For gathering, this stat will act as stamina required to perform actions.";
                return true;

            case "HpRegenText":
                title = "Health Regeneration";
                description =
                    "Health restored per second.\n\n" +
                    "Provides steady recovery between and during fights.";
                return true;

            case "EnergyRegenText":
            case "CombatEnergyEfficiency":
            case "EnergyEfficiencyText":
                title = "Energy Efficiency";
                description = "Reduces the energy cost for using non magic abilities";
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
                    "Max guard baseline is equal to your max HP.\n\n" +
                    "Gaining max guard increases the guard you can obtain above your max HP.\n\n" +
                    "Sources include armour max guard % and Endurance Bulwark passives.";
                return true;

            case "ThornsDmgText":
                title = "Thorns Damage";
                description =
                    "Flat physical damage dealt back to attackers when they hit you.\n\n" +
                    "Thorns only triggers when the attacker is within 4 range.\n\n" +
                    "Comes from gear, the Endurance Thorns major passive, and other sources. The rolled range is then multiplied by your Thorns Damage Inc %.";
                return true;

            case "ThornsDmgIncText":
                title = "Thorns Damage Inc";
                description =
                    "Percent bonus applied to your thorns damage range.\n\n" +
                    "Stacks from gear, Endurance Reflective Plate passives, and the Thorns major passive enhancement.";
                return true;

            case "EvadeText":
                title = "Evade";
                description =
                    "Chance to fully avoid a non-magic attack.\n\n" +
                    "Stacks from gear, Hunter's Swiftness, and other passives. " +
                    AbilityCombatPower.HuntersSwiftnessEvadeTooltipNote;
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
                    "When block succeeds, damage is reduced by your Block Mitigation % instead of being negated entirely.\n\n" +
                    "Sources include gear, Tactician passives (shield), and Endurance Shield Training I while a shield is equipped.";
                return true;

            case "BlockMitigationText":
                title = "Block Mitigation";
                description =
                    "Percent of physical damage prevented when a block succeeds.\n\n" +
                    "Base mitigation is 70%. Additional sources include:\n" +
                    "• Tactician Secondary Specialist (shield): +10%\n" +
                    "• Endurance Shield Training II (shield equipped): +5%";
                return true;

            case "ParryText":
                title = "Parry";
                description =
                    "Reduce incoming damage by your parry mitigation amount and return that damage back to the attacker.";
                return true;

            case "ParryMitigationText":
                title = "Parry Mitigation";
                description =
                    "The amount of incoming damage reduced, and the portion of that damage returned to the attacker.";
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
