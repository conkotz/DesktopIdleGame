using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estimates how much combat power action-bar abilities add, using the same building blocks as
/// <see cref="PlayerAbilityController"/> (weapon-average physical hit, ability multipliers, AP, crit, cooldown).
/// Only combat loadout abilities (action bar slots 1–5) count; starter Attack abilities are excluded (weapon DPS).
/// Gathering-strip UI uses the frozen combat row. Duplicate ids share one cooldown estimate.
/// </summary>
public static class AbilityCombatPower
{
    /// <summary>Matches <see cref="PlayerAbilityController"/> Power Slash id — attack-queued bonus, not raw / cooldown.</summary>
    public const string PowerSlashAbilityId = "power_slash";
    public const string CrusaderStrikeAbilityId = "crusader_strike";
    public const string WhirlwindAbilityId = "whirlwind";
    public const string RendAbilityId = "rend";
    public const string EnvenomAbilityId = "envenom";
    public const string CleavingStrikesAbilityId = "cleaving_strikes";
    public const string CrescentSlashAbilityId = "crescent_slash";
    public const float CrescentSlashReach = 12f;
    public const string GuardiansHammerAbilityId = "guardians_hammer";
    public const string SoulforgedWeaponAbilityId = "soulforged_weapon";
    public const string SoulforgedWarriorAbilityId = "soulforged_warrior";
    public const string SoulforgedWarriorOutgoingSourceLabel = "Soulforged Warrior";
    public const string SoulforgedWarriorEnhancementParentSpineNodeId = "Lv45_3";
    public const string SoulforgedWarriorWarcryBuffEffectId = "soulforged_warrior_warcry";
    public const int SoulforgedWarriorFuriousSlamChoiceIndex = 0;
    public const int SoulforgedWarriorTauntingShoutChoiceIndex = 1;
    public const float SoulforgedWarriorWarcryFirstDelaySeconds = 5f;
    public const float SoulforgedWarriorWarcryIntervalSeconds = 10f;
    public const float SoulforgedWarriorWarcryBuffDurationSeconds = 5f;
    public const float SoulforgedWarriorWarcryPhysicalDamageBonus = 0.20f;
    public const float SoulforgedWarriorWarcryAllyRange = 20f;
    public const float SoulforgedWarriorTauntRange = 8f;
    public const float SoulforgedWarriorTauntingShoutOutgoingDamageReduction = 0.10f;
    /// <summary>Matches <see cref="SoulforgedWarriorWarcryIntervalSeconds"/> so taunted enemies stay debuffed between warcries.</summary>
    public const float SoulforgedWarriorTauntingShoutDebuffDurationSeconds = SoulforgedWarriorWarcryIntervalSeconds;
    public const float SoulforgedWarriorFuriousSlamRange = 6f;
    public const float SoulforgedWarriorFuriousSlamDamageMultiplier = 1.5f;
    public const float SoulforgedWarriorFuriousSlamCooldownSeconds = 8f;
    public const float SoulforgedWarriorMaxLeashDistance = 20f;

    public const string SoulforgedWeaponEnhancementParentSpineNodeId = "Lv35_0";
    public const int SoulforgedWeaponEnhancementSourceLevel = 35;
    public const int SoulforgedWeaponSwarmChoiceIndex = 0;
    public const int SoulforgedWeaponExtendedDurationChoiceIndex = 1;
    public const int SoulforgedWeaponSwarmCount = 3;
    public const float SoulforgedWeaponSwarmDurationSeconds = 30f;
    public const float SoulforgedWeaponSwarmDamageMultiplier = 0.85f;
    public const float SoulforgedWeaponExtendedDurationSeconds = 90f;
    public const string LumberFrenzyAbilityId = "lumber_frenzy";
    public const string FishingFrenzyAbilityId = "fishing_frenzy";
    public const string CleavingChopAbilityId = "cleaving_chop";
    public const string SpectralAxeAbilityId = "spectral_axe";
    public const string AvatarOfTheForestAbilityId = "avatar_of_the_forest";
    public const string FinalSeveranceAbilityId = "final_severance";
    public const string ExecutionersDescentAbilityId = "executioners_descent";
    public const string BladestormAbilityId = "bladestorm";
    public const string ShadowStrikeAbilityId = "shadow_strike";
    public const string EnergyInfusionAbilityId = "energy_infusion";
    public const string FlameChargeAbilityId = "flame_charge";
    public const string WarBannerAbilityId = "war_banner";

    public const float WarBannerBaseDurationSeconds = 20f;
    public const float WarBannerAllyRange = 15f;
    public const float WarBannerBaseAttackSpeedBonus = 0.05f;
    public const float WarBannerBaseDamageReductionFraction = 0.05f;
    public const float WarBannerBaseGlobalPhysicalDamageBonus = 0.05f;
    public const float WarBannerStackIntervalSeconds = 1f;
    public const int WarBannerBaseMaxStacks = 10;
    public const float WarBannerStackBonusPerStat = 0.01f;
    public const float WarBannerDescentSeconds = 1f;
    public const float WarBannerSpawnHeightAboveTarget = 6f;
    public const float WarBannerMinimumHeightAboveTarget = 0f;
    public const float WarBannerSpawnHoldSeconds = 0f;

    public const float WarBannerEnh1CooldownReduction = 0.15f;
    public const float WarBannerEnh2MaxStacksHealFraction = 0.15f;
    public const float WarBannerEnh2MoveSpeedBonus = 0.15f;
    public const int WarBannerEnh3KillMaxStackBonus = 1;
    public const float WarBannerEnh3KillDurationExtensionSeconds = 3f;
    public const int WarBannerEnh3MaxKillProcs = 5;

    /// <summary>Enhancement choices for War Banner (Melee Lv35 slot 1).</summary>
    public const string WarBannerEnhancementParentSpineNodeId = "Lv35_1";
    public const string HammerTempestAbilityId = "hammer_tempest";

    public const float HammerTempestBaseDurationSeconds = 25f;
    public const int HammerTempestBaseHammerCount = 3;
    public const int HammerTempestSacredArsenalBonusHammerCount = 3;
    /// <summary>Per-hit damage multiplier with Sacred Arsenal (-10%).</summary>
    public const float HammerTempestSacredArsenalDamageMultiplier = 0.90f;
    /// <summary>Hit interval multiplier with Sacred Arsenal (0.5 = twice as often).</summary>
    public const float HammerTempestSacredArsenalHitIntervalMultiplier = 0.5f;
    /// <summary>Sacred Arsenal shortens Hammer Tempest by 10 seconds.</summary>
    public const float HammerTempestSacredArsenalDurationPenaltySeconds = 10f;
    public const float HammerTempestCrushingMomentumDamagePerStack = 0.10f;
    public const int HammerTempestCrushingMomentumMaxStacks = 10;
    public const int HammerTempestSacredArsenalChoiceIndex = 0;
    public const int HammerTempestCrushingMomentumChoiceIndex = 1;
    /// <summary>Slow-reference APS — hit damage stays at the base weapon-% scale (1.0×).</summary>
    public const float HammerTempestWeaponSpeedSlowReferenceAps = 0.5f;
    /// <summary>Fast-reference APS — hit damage reaches <see cref="HammerTempestWeaponSpeedMaxHitDamageBonusFraction"/> bonus.</summary>
    public const float HammerTempestWeaponSpeedFastReferenceAps = 1f;
    /// <summary>Max extra per-hit damage for fast weapons (1.5× at fast reference APS).</summary>
    public const float HammerTempestWeaponSpeedMaxHitDamageBonusFraction = 0.5f;

    public static float GetHammerTempestWeaponSpeedHitDamageMultiplier(float attacksPerSecond)
    {
        float aps = Mathf.Max(0.01f, attacksPerSecond);
        float t = Mathf.Clamp01(Mathf.InverseLerp(
            HammerTempestWeaponSpeedSlowReferenceAps,
            HammerTempestWeaponSpeedFastReferenceAps,
            aps));
        return Mathf.Lerp(1f, 1f + HammerTempestWeaponSpeedMaxHitDamageBonusFraction, t);
    }

    public static int GetHammerTempestWeaponSpeedHitDamageBonusPercent(float attacksPerSecond)
    {
        return Mathf.RoundToInt((GetHammerTempestWeaponSpeedHitDamageMultiplier(attacksPerSecond) - 1f) * 100f);
    }


    /// <summary>Melee Lv30 major passive — Battle Engine.</summary>
    public const int BattleEngineMajorPassiveLevel = 30;
    public const float BattleEngineEnergyOnAbilityHit = 5f;
    public const float BattleEngineRapidCastingCooldownReductionSeconds = 0.5f;
    public const float BattleEngineOverloadCostPerStack = 0.10f;
    public const float BattleEngineOverloadDamagePerStack = 0.05f;
    public const int BattleEngineOverloadMaxStacks = 5;

    /// <summary>Melee Lv30 major passive — Tactician (skill tree slot 1 at level 30).</summary>
    public const string TacticianMajorPassiveSpineNodeId = "Lv30_1";
    public const string BattleEngineEnhancementParentSpineNodeId = "Lv30_0";

    /// <summary>Fraction of physical damage prevented when a block succeeds (player and enemies).</summary>
    public const float BasePhysBlockMitigation = 0.70f;

    public const float TacticianOneHandedAttackSpeedPercent = 0.05f;
    public const float TacticianOneHandedPoisonChance = 0.05f;
    public const float TacticianOneHandedBurnChance = 0.05f;
    public const float TacticianOneHandedCritChance = 0.05f;
    public const float TacticianTwoHandedBleedMultiplierBonus = 0.05f;
    public const float TacticianTwoHandedArmorPenetration = 0.05f;
    public const float TacticianTwoHandedStunChance = 0.05f;
    public const float TacticianTwoHandedBlockChance = 0.05f;
    public const float TacticianShieldBlockChanceBonus = 0.05f;
    public const int TacticianShieldFlatResistBonus = 10;
    public const float TacticianShieldBlockMitigationBonus = 0.10f;
    public const float TacticianStunDurationSeconds = 3f;
    public const float TacticianDualityRecentSwapSeconds = 8f;

    public const int TacticianEnhancementPerfectForm = 0;
    /// <summary>Skill-tree index; displayed as Secondary Specialist.</summary>
    public const int TacticianEnhancementBulwark = 1;
    public const int TacticianEnhancementDuality = 2;
    public const int TacticianSecondarySpecialistDualWieldHitInterval = 5;
    public const float TacticianSecondarySpecialistDualWieldFollowUpDamageFraction = 0.5f;
    public const string TacticianSecondarySpecialistDoubleHitSourceLabel = "Secondary Specialist";

    /// <summary>Melee Lv10 major passive — Parry (skill tree slot 1 at level 10).</summary>
    public const string ParryMajorPassiveSpineNodeId = "Lv10_1";
    public const int ParryMajorPassiveLevel = 10;
    public const float ParryMeleeRange = 3f;
    public const float ParryBaseChance = 0.20f;
    public const float ParryImprovedParryChanceBonus = 0.05f;
    public const float ParryDamageReductionFraction = 0.25f;
    public const float ParryImprovedMitigationBonus = 0.15f;
    public const float ParryRiposteCooldownSeconds = 3f;
    public const string ParryRiposteOutgoingSourceLabel = "Parry — Riposte";
    public const string ParryReflectOutgoingSourceLabel = "Parry";

    /// <summary>Melee Lv40 major passive — Phoenix Soul.</summary>
    public const int PhoenixSoulMajorPassiveLevel = 40;
    public const float PhoenixSoulNearbyRadius = 10f;
    public const int PhoenixSoulMaxNearbyBurningEnemies = 5;
    public const float PhoenixSoulBurnRegenIntervalSeconds = 3f;
    public const float PhoenixSoulLifePerBurningEnemy = 1f;
    public const float PhoenixSoulEnergyPerBurningEnemy = 1f;
    public const float PhoenixSoulBurnChanceBonus = 0.05f;
    public const float PhoenixSoulLivingInfernoMeleeDamagePerBurningEnemy = 0.02f;
    /// <summary>Cap at <see cref="PhoenixSoulMaxNearbyBurningEnemies"/> × per-enemy bonus (5 × 2% = 10%).</summary>
    public const float PhoenixSoulLivingInfernoMaxMeleeDamageBonusFraction = 0.10f;
    public const float PhoenixSoulAshenRebirthHealthFraction = 0.25f;
    public const float PhoenixSoulAshenRebirthImmunitySeconds = 3f;
    public const float PhoenixSoulAshenRebirthCooldownSeconds = 300f;
    public const float PhoenixSoulAshenRebirthExplosionFlatFireDamage = 20f;
    /// <summary>Guaranteed burn stack roll on Ashen Rebirth explosion fire hits.</summary>
    public const float PhoenixSoulAshenRebirthExplosionBurnApplyChance = 1f;
    public const float PhoenixSoulAshenRebirthPhoenixVfxDurationSeconds = 2f;

    /// <summary>Melee Lv40 major passive — Master of Venoms (skill tree slot 1 at level 40).</summary>
    public const string MasterOfVenomsEnhancementParentSpineNodeId = "Lv40_1";
    public const float MasterOfVenomsPoisonCritFractionOfCritDamage = 0.5f;
    public const float MasterOfVenomsPoisonChanceBonus = 0.05f;
    public const float MasterOfVenomsNeurotoxinOutgoingDamageReduction = 0.15f;
    public const float MasterOfVenomsNeurotoxinMoveSlowPerPoisonStack = 0.03f;
    public const float MasterOfVenomsLethalCompoundDurationReductionSeconds = 0.5f;
    public const int MasterOfVenomsLethalCompoundMaxStacksBonus = 3;

    /// <summary>Phoenix Soul major passive spine (slot 0 at level 40).</summary>
    public const string PhoenixSoulEnhancementParentSpineNodeId = "Lv40_0";

    /// <summary>Melee Lv40 major passive — Bloodbath (skill tree slot 2 at level 40).</summary>
    public const string BloodbathEnhancementParentSpineNodeId = "Lv40_2";
    public const int BloodbathMaxStacks = 5;
    public const float BloodbathBleedChanceBonus = 0.05f;
    public const float BloodbathPhysicalDamagePerStack = 0.03f;
    public const float BloodbathStackDurationSeconds = 12f;
    public const float BloodbathCarnageBleedMultiplierPerStack = 0.02f;
    public const float BloodbathButcheryBleedChancePerStack = 0.03f;
    public const string BloodbathHudBuffId = "bloodbath";

    /// <summary>Melee Lv40 major passive — Opportunistic (skill tree slot 3 at level 40).</summary>
    public const string OpportunisticEnhancementParentSpineNodeId = "Lv40_3";
    public const int OpportunisticExtendedOpeningChoiceIndex = 0;
    public const int OpportunisticFinishingBlowChoiceIndex = 1;
    public const float OpportunisticFullHealthThreshold01 = 0.999f;
    public const float OpportunisticFullHealthCritChanceBonus = 0.10f;
    public const float OpportunisticAbilityDamageBonus = 0.15f;
    public const float OpportunisticAbilityDamageHighHpThreshold01 = 0.70f;
    public const float OpportunisticAbilityDamageLowHpThreshold01 = 0.40f;
    public const float OpportunisticExtendedOpeningHighHpThreshold01 = 0.80f;
    public const float OpportunisticExtendedOpeningLowHpThreshold01 = 0.30f;
    public const float OpportunisticFinishingBlowLowHpThreshold01 = 0.30f;
    public const float OpportunisticFinishingBlowCritMultiplierBonus = 0.15f;

    /// <summary>Melee capstone passive spine (slot 0 at level 50).</summary>
    public const string MeleeCapstoneSpineNodeId = "Lv50_0";

    public const int MeleeCapstoneWayOfTheBerserkerChoiceIndex = 0;
    public const int MeleeCapstoneWayOfTheCrusaderChoiceIndex = 1;
    public const int MeleeCapstoneWayOfTheAssassinChoiceIndex = 2;
    public const int MeleeCapstoneWayOfTheGladiatorChoiceIndex = 3;
    public const int MeleeCapstoneWayOfTheSlayerChoiceIndex = 4;
    public const int MeleeCapstoneWayOfTheBladeDancerChoiceIndex = 5;

    public const float WayOfTheSlayerExecuteHpThreshold01 = 0.10f;
    public const string WayOfTheSlayerExecuteOutgoingSourceLabel = "Way of the Slayer";
    public const string WayOfTheSlayerExecuteStatusPopupLabel = "EXECUTED";

    public const int WayOfTheBladeDancerTripleHitInterval = 3;
    public const float WayOfTheBladeDancerTripleHitDamageFraction = 1f;
    public const float WayOfTheBladeDancerKillCritChanceBonus = 0.20f;
    public const float WayOfTheBladeDancerKillCritDurationSeconds = 5f;
    public const float WayOfTheBladeDancerDashMaxDistance = 6f;
    public const float WayOfTheBladeDancerDashCooldownSeconds = 1.25f;
    public const string WayOfTheBladeDancerTripleHitSourceLabel = "Way of the Blade Dancer";
    public const string WayOfTheBladeDancerKillCritHudBuffId = "way_of_the_blade_dancer_kill_crit";

    public const int WayOfTheAssassinPoisonMaxStacksBonus = 5;
    public const float WayOfTheAssassinLowHpPoisonMultiplierBonus = 0.50f;
    public const float WayOfTheAssassinPoisonDurationBonusSeconds = 2f;
    public const float WayOfTheAssassinCorruptionResistReductionPerPoisonTick = 0.05f;
    public const int WayOfTheGladiatorBleedMaxStacksBonus = 1;
    public const float WayOfTheGladiatorArmorReductionPerBleedTick = 0.05f;
    public const float WayOfTheGladiatorIncomingBleedChanceReduction = 0.30f;
    public const int WayOfTheCrusaderMaxHolySeals = 3;
    public const float WayOfTheCrusaderHolySealGainIntervalSeconds = 4f;
    public const float WayOfTheCrusaderHealMaxHpFraction = 0.03f;
    public const float WayOfTheCrusaderExtraFireDamageFraction = 0.20f;
    public const int WayOfTheBerserkerMaxStacks = 15;
    public const float WayOfTheBerserkerStackDurationSeconds = 15f;
    public const float WayOfTheBerserkerAttackSpeedPerStack = 0.02f;
    public const float WayOfTheBerserkerCritChancePerStack = 0.01f;
    public const float WayOfTheBerserkerMeleeDamagePerStack = 0.01f;
    public const float WayOfTheBerserkerMoveSpeedPerStack = 0.01f;
    public const float WayOfTheBerserkerDamageTakenPerStack = 0.02f;
    public const int WayOfTheBerserkerSlowImmunityMinStacks = 10;
    public const float WayOfTheBerserkerLowHpLeechFraction = 0.25f;
    public const float WayOfTheBerserkerLowHpLeechDurationSeconds = 15f;
    public const float WayOfTheBerserkerLowHpLeechCooldownSeconds = 120f;

    /// <summary>Enhancement choices for Hammer Tempest (Melee Lv35 slot 2).</summary>
    public const string HammerTempestEnhancementParentSpineNodeId = "Lv35_2";

    /// <summary>Enhancement choices for Shadow Strike (Melee Lv25 slot 0).</summary>
    public const string ShadowStrikeEnhancementParentSpineNodeId = "Lv25_0";

    /// <summary>Enhancement choices for Energy Infusion / Arcane Battery (Melee Lv25 slot 1).</summary>
    public const string EnergyInfusionEnhancementParentSpineNodeId = "Lv25_1";

    /// <summary>Enhancement choices for Flame Charge (Melee Lv25 slot 2).</summary>
    public const string FlameChargeEnhancementParentSpineNodeId = "Lv25_2";

    /// <summary>Enhancement choices for Crusader Strike (Melee Lv5 slot 3).</summary>
    public const string CrusaderStrikeEnhancementParentSpineNodeId = "Lv5_3";
    /// <summary>Enhancement choices for Guardian's Hammer (Melee Lv15 slot 3).</summary>
    public const string GuardiansHammerEnhancementParentSpineNodeId = "Lv15_3";

    public const float FlameChargeDashDistance = 5f;
    public const float FlameChargeDashDurationSeconds = 0.35f;
    public const float FlameChargeTrailDurationSeconds = 4f;
    public const float FlameChargeTrailRadius = 1.15f;
    public const float FlameChargeTrailTickIntervalSeconds = 0.5f;
    public const float FlameChargeTrailSegmentSpacing = 0.55f;
    /// <summary>Total flat Fire damage per enemy standing in a trail segment (spread across <see cref="FlameChargeTrailDurationSeconds"/>).</summary>
    public const float FlameChargeTrailTotalFlatFireDamage = 20f;
    public const float FlameChargeVolcanicExplosionFlatFireDamage = 20f;
    public const float FlameChargeVolcanicExplosionRadius = 5f;

    /// <summary>Base share of a melee energy ability's cost that is paid with mana while Energy Infusion is active.</summary>
    public const float EnergyInfusionBaseManaCostFraction = 0.30f;
    public const float EnergyInfusionEfficientConversionAdditionalManaCostFraction = 0.10f;
    public const float EnergyInfusionEfficientConversionFlatManaRegenPerSecond = 5f;
    public const float EnergyInfusionOverchargedManaCostFraction = 0.20f;
    public const float EnergyInfusionOverchargedAbilityPowerPercentBonus = 10f;

    public const float ShadowStrikeForwardReach = 15f;
    public const float ShadowStrikeLandBehindTargetDistance = 2f;
    public const float ShadowStrikeLethalCritBonusFraction = 0.8f;
    public const float ShadowStrikeExecutionCooldownRefundSeconds = 4f;
    public const float GuardiansHammerForwardReach = 8f;
    public const float GuardiansHammerVerticalHalfHeight = 2.4f;
    public const float GuardiansHammerImpactWidth = 2.6f;
    public const float GuardiansHammerProtectorResolveGuardPerHitFractionMaxHealth = 0.04f;
    public const int GuardiansHammerProtectorResolveMaxEnemyHits = 3;
    public const float GuardiansHammerProtectorResolveStunDurationSeconds = 3f;
    public const float GuardiansHammerProtectorResolveGuardDurationSeconds = 5f;
    public const float GuardiansHammerBurningVerdictExplosionRadius = 3f;
    public const int GuardiansHammerBurningVerdictTicksWorth = 3;

    /// <summary>Enhancement choices for Final Severance (Melee Lv45 slot 0).</summary>
    public const string FinalSeveranceEnhancementParentSpineNodeId = "Lv45_0";

    /// <summary>Enhancement choices for Executioner's Descent (Melee Lv45 slot 1).</summary>
    public const string ExecutionersDescentEnhancementParentSpineNodeId = "Lv45_1";

    /// <summary>Enhancement choices for Bladestorm (Melee Lv45 slot 2).</summary>
    public const string BladestormEnhancementParentSpineNodeId = "Lv45_2";

    public const float BladestormChannelSeconds = 3f;
    /// <summary>500% increased attack speed → 5× strike rate during the channel.</summary>
    public const float BladestormAttackSpeedMultiplier = 5f;
    public const float BladestormNormalHitWeaponMultiplier = 0.5f;
    public const float BladestormFinaleHitWeaponMultiplier = 1.5f;
    /// <summary>50% reduced incoming damage during the channel (0.5× damage taken).</summary>
    public const float BladestormChannelDamageTakenMultiplier = 0.5f;
    public const float BladestormForwardReach = 12f;
    /// <summary>Max seconds to walk into melee range before Bladestorm channel begins.</summary>
    public const float BladestormApproachTimeoutSeconds = 8f;

    public const float ExecutionersDescentDescentSeconds = 3f;
    /// <summary>When the locked target dies mid-descent, the axe drops from its current position to the impact point over this duration.</summary>
    public const float ExecutionersDescentTargetDiedRushSeconds = 0.5f;
    /// <summary>Default shockwave weapon scale (primary hit uses <see cref="AbilityDefinition.weaponDamageMultiplier"/> on the asset).</summary>
    public const float ExecutionersDescentShockwaveWeaponMultiplier = 3f;
    public const float ExecutionersDescentShockwaveRadius = 10f;
    public const float ExecutionersDescentClaimCooldownReductionFraction = 0.5f;
    public const float ExecutionersDescentSunderingArmorMrMultiplier = 0.5f;
    public const float ExecutionersDescentSunderingDebuffSeconds = 5f;
    public const float ExecutionersDescentAxeSpawnHeight = 6f;
    /// <summary>Extra horizontal reach beyond melee weapon edge gap for casting and tracking during descent.</summary>
    public const float ExecutionersDescentCastRangeBeyondMelee = 18f;
    /// <summary>When descent loses all in-range targets, ability ends early with shockwave only and this fixed CD.</summary>
    public const float ExecutionersDescentNoTargetInRangeCooldownSeconds = 10f;
    /// <summary>Executioner's Continuum — axe lingers at cast position and pulses shockwaves.</summary>
    public const float ExecutionersDescentContinuumDurationSeconds = 15f;
    public const float ExecutionersDescentContinuumShockwaveIntervalSeconds = 2.5f;
    /// <summary>Continuum shockwave hits use 100% weapon damage (base descent shockwave stays at 300%).</summary>
    public const float ExecutionersDescentContinuumShockwaveWeaponMultiplier = 1f;

    public static int GetExecutionersDescentContinuumShockwaveCount()
    {
        float interval = ExecutionersDescentContinuumShockwaveIntervalSeconds;
        if (interval <= 0.0001f)
            return 1;

        return Mathf.FloorToInt(ExecutionersDescentContinuumDurationSeconds / interval) + 1;
    }

    public const float FinalSeveranceChannelSeconds = 2f;
    public const float FinalSeveranceHitRangeHalfWidth = 25f;
    public const int FinalSeveranceMaxTargets = 8;
    /// <summary>Enemy HP / MaxHP must be at or above this to count as full life for Worldbreaker.</summary>
    public const float FinalSeveranceWorldbreakerFullHealthThreshold01 = 0.999f;
    public const float FinalSeveranceWorldbreakerBonusMultiplier = 1.2f;
    public const int FinalSeveranceThousandCutsHitCount = 4;
    public const float FinalSeveranceThousandCutsHitFraction = 0.3f;

    /// <summary>
    /// Parent spine node id where Avatar enhancement choices are stored (<see cref="SkillsManager.GetSkillChoiceSelection"/>).
    /// Must match the Woodcutting skill-tree row for this ability (Lv45 slot 0 — not Lv50, which is a different node).
    /// </summary>
    public const string AvatarOfTheForestEnhancementParentSpineNodeId = "Lv45_0";

    /// <summary>
    /// Flat add to the effective woodcutting speed multiplier while Avatar of the Forest is active (e.g. 1.52x → 1.62x).
    /// Not multiplied into <c>sheet × (1 + Forest Flow + …)</c>; keep in sync with ability tooltip copy (+10%).
    /// </summary>
    public const float AvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd = 0.10f;

    public const float WhirlwindChannelVfxIntervalSeconds = 0.2f;
    public const float WhirlwindExpansiveRangePerStage = 0.1f;
    public const float WhirlwindExpansiveDamagePerSecond = 0.05f;
    public const float WhirlwindBaseMoveSpeedPenaltyFraction = 0.25f;
    public const float WhirlwindAutoBattleMinEnergyFraction = 0.5f;
    public const float WhirlwindAutoBattleStopIfNoEnemyWithinDistance = 10f;
    public const float WhirlwindGaleforceTwisterIntervalSeconds = 2f;
    public const float WhirlwindGaleforceChannelEnergyCostPerSecond = 5f;
    public const float WhirlwindGaleforceTwisterDamageMultiplier = 0.3f;
    public const float WhirlwindGaleforceTwisterRadiusScale = 0.45f;
    public const float WhirlwindGaleforceTwisterMoveSpeed = 1.5f;
    public const float WhirlwindGaleforceTwisterMinDirectionSeconds = 2f;
    public const float WhirlwindGaleforceTwisterMaxDirectionSeconds = 5f;
    public const float WhirlwindGaleforceTwisterLingerAfterChannelSeconds = 5f;
    public const string WhirlwindTwistersOutgoingDamageSourceLabel = "Twisters (Whirlwind)";

    public static float GetWhirlwindBaseChannelEnergyPerSecond(float baseAbilityEnergyCost, int galeforceChoiceIndex)
    {
        float cost = Mathf.Max(0f, baseAbilityEnergyCost);
        if (galeforceChoiceIndex == 0)
            cost += WhirlwindGaleforceChannelEnergyCostPerSecond;
        return cost;
    }
    public const float CrusaderStrikeFirstHitWeaponMultiplier = 1.1f;
    public const float CrusaderStrikeSecondHitWeaponMultiplier = 1.2f;
    public const float CrusaderStrikeFinalHitWeaponMultiplier = 1.5f;
    public const float CrusaderStrikeHealFractionOfMaxHealth = 0.05f;
    public const float CrusaderStrikeSacredRestorationHealFractionOfMaxHealth = 0.10f;
    public const float CrusaderStrikeFireBalanceBuffDurationSeconds = 10f;
    public const float CrusaderStrikeFireBalancePhysicalToFireFraction = 0.30f;
    public const float CrusaderStrikeFireBalanceFinalStrikeFireMultiplier = 1.20f;
    public const float CrusaderStrikeComboTransitionSeconds = 0.6f;

    /// <summary>Cleaving Strikes secondary hits: fraction of rolled weapon split (sync with <see cref="PlayerAbilityController.BuildCleavingSecondarySplit"/>).</summary>
    public const float CleavingStrikesSecondaryHitWeaponDamageFraction = 0.6f;
    public const float CleavingStrikesMinMeleeReach = 3f;
    public const float CleavingStrikesCleaveRadiusFromAnchor = 3f;
    public const int CleavingStrikesBaseExtraTargets = 2;
    public const int CleavingStrikesBaseEmpoweredHits = 7;
    public const float CleavingStrikesBaseDurationSeconds = 8f;
    public const int CleavingStrikesGreaterCleaveBonusTargets = 1;
    public const int CleavingStrikesLastingMomentumEmpoweredHits = 10;
    public const float CleavingStrikesLastingMomentumDurationSeconds = 14f;

    /// <summary>Combat-power heuristic: extra enemies credited for Crimson Spread / Contagion full radial payloads.</summary>
    private const float AilmentRadialSpreadAssumedExtraTargets = 2.5f;
    private const int SoulforgedWeaponChoiceSourceLevel = SoulforgedWeaponEnhancementSourceLevel;
    private const int SoulforgedWeaponSwarmChoiceIndexLocal = SoulforgedWeaponSwarmChoiceIndex;
    private const int SoulforgedWeaponExtendedDurationChoiceIndexLocal = SoulforgedWeaponExtendedDurationChoiceIndex;
    private const float SoulforgedWeaponSwarmCountLocal = SoulforgedWeaponSwarmCount;
    private const float SoulforgedWeaponSwarmDamageMultiplierLocal = SoulforgedWeaponSwarmDamageMultiplier;
    private const float SoulforgedWeaponSwarmDurationSecondsLocal = SoulforgedWeaponSwarmDurationSeconds;

    private static int _slottedAbilityDpsCacheFrame = -1;
    private static CharacterStats _slottedAbilityDpsCacheStats;
    private static float _slottedAbilityDpsCacheValue;

    public static float EstimateTotalSlottedAbilityDps(CharacterStats stats, bool logDiagnostics = false)
    {
        if (!stats)
            return 0f;

        if (!logDiagnostics
            && _slottedAbilityDpsCacheFrame == Time.frameCount
            && _slottedAbilityDpsCacheStats == stats)
            return _slottedAbilityDpsCacheValue;

        bool log = logDiagnostics;

        ActionBarUI bar = ActionBarUI.FindForCharacterStats(stats);
        if (!bar)
        {
            if (log)
                Debug.LogWarning(
                    "[AbilityCombatPower] No ActionBarUI in loaded scenes. Ability DPS = 0.",
                    stats);
            return 0f;
        }

        PlayerAbilityController abilityController =
            stats.GetComponent<PlayerAbilityController>() ?? stats.GetComponentInParent<PlayerAbilityController>();

        AbilityDatabase barDb = bar.GetAbilityDatabaseOrDefault();
        AbilityDatabase playerDb = abilityController != null ? abilityController.GetDatabaseOrDefault() : null;

        if (log)
        {
            Debug.Log(
                "[AbilityCombatPower] --- begin ---" +
                $"\n  stats: {stats.gameObject.name} path={BuildHierarchyPath(stats.transform)}" +
                $"\n  actionBar: {bar.gameObject.name} path={BuildHierarchyPath(bar.transform)}" +
                $"\n  playerAbilityController: {(abilityController ? abilityController.gameObject.name : "NULL")}" +
                $"\n  barDb: {DescribeDb(barDb)}  playerDb: {DescribeDb(playerDb)}",
                stats);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        float total = 0f;
        int slotIndex = -1;
        SkillDatabase skillDb = SkillDatabase.LoadDefault();
        SkillsManager skillsMgr = SkillsManager.Instance;

        foreach (string abilityId in bar.EnumerateCombatLoadoutAbilityIdsForCombatPower())
        {
            slotIndex++;
            if (string.IsNullOrWhiteSpace(abilityId))
                continue;

            if (!seen.Add(abilityId))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] loadout[{slotIndex}]: duplicate id '{abilityId}' (skipped — already counted)",
                        stats);
                continue;
            }

            AbilityDefinition def = ResolveAbilityDefinition(abilityId, barDb, playerDb, out string resolution);
            if (!def)
            {
                if (log)
                    Debug.LogWarning(
                        $"[AbilityCombatPower] loadout[{slotIndex}]: could not resolve AbilityDefinition for id='{abilityId}'.",
                        stats);
                continue;
            }

            if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] loadout[{slotIndex}]: id='{abilityId}' skipped (starter attack — already in weapon DPS).",
                        stats);
                continue;
            }

            if (ActionBarUI.IsGatheringSkillType(def.sourceSkill))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] loadout[{slotIndex}]: id='{abilityId}' skipped (gathering ability).",
                        stats);
                continue;
            }

            SkillDefinition skillDef = skillDb != null ? skillDb.Get(def.sourceSkill) : null;
            if (!SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skillDef, def, skillsMgr))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] loadout[{slotIndex}]: id='{abilityId}' skipped (not committed / level-locked on skill tree).",
                        stats);
                continue;
            }

            if (!stats.IsAbilityUsableWithEquippedWeapon(def))
            {
                if (log)
                    Debug.Log(
                        $"[AbilityCombatPower] loadout[{slotIndex}]: id='{abilityId}' skipped (wrong equipped weapon for {def.requiredWeaponType}).",
                        stats);
                continue;
            }

            float dps = EstimateAbilityDps(def, stats);
            total += dps;

            if (log)
            {
                Debug.Log(
                    $"[AbilityCombatPower] loadout[{slotIndex}]: id='{abilityId}' → def={def.displayName} ({def.abilityId}) " +
                    $"resolve={resolution}  estDps={dps:F4}",
                    stats);
            }
        }

        if (log)
            Debug.Log($"[AbilityCombatPower] --- end --- totalAbilityDps={total:F4}", stats);

        float result = Mathf.Max(0f, total);
        if (!logDiagnostics)
        {
            _slottedAbilityDpsCacheFrame = Time.frameCount;
            _slottedAbilityDpsCacheStats = stats;
            _slottedAbilityDpsCacheValue = result;
        }

        return result;
    }

    private static string DescribeDb(AbilityDatabase db)
    {
        if (!db)
            return "null";
        return $"{db.name} ({db.GetInstanceID()})";
    }

    private static string BuildHierarchyPath(Transform t)
    {
        if (!t)
            return "";
        var stack = new System.Collections.Generic.List<string>(8);
        Transform walk = t;
        while (walk != null)
        {
            stack.Add(walk.name);
            walk = walk.parent;
        }
        stack.Reverse();
        return string.Join("/", stack);
    }

    private static AbilityDefinition ResolveAbilityDefinition(
        string abilityId,
        AbilityDatabase barDb,
        AbilityDatabase playerDb,
        out string resolutionSource)
    {
        resolutionSource = "none";
        AbilityDefinition def = barDb ? barDb.Get(abilityId) : null;
        if (def)
        {
            resolutionSource = "barDb";
            return def;
        }

        def = playerDb ? playerDb.Get(abilityId) : null;
        if (def)
        {
            resolutionSource = "playerDb";
            return def;
        }

        def = AbilityDatabase.FindDefinitionById(abilityId);
        if (def)
        {
            resolutionSource = "FindDefinitionById";
            return def;
        }

        return null;
    }

    /// <summary>
    /// Single-ability DPS estimate (for tooling / UI). Uses crit on the full hit like instant abilities in <see cref="PlayerAbilityController.TryUseAbility"/>.
    /// </summary>
    public static float EstimateAbilityDps(AbilityDefinition def, CharacterStats stats)
    {
        if (!def || !stats)
            return 0f;

        // Lv1 Attack abilities trigger weapon swings; direct weapon DPS already covers them.
        if (CombatStarterAttackAbility.IsCombatStarterAttack(def))
            return 0f;

        if (def.minionSpawnDefinition)
        {
            float mdps = MinionRuntimeStatsCalculator.EstimateMinionDamagePerSecond(
                stats,
                def.minionSpawnDefinition.combatConfig);
            float summonDur = Mathf.Max(0.1f, def.minionSpawnDefinition.summonDuration);
            float cooldown = Mathf.Max(0.01f, def.cooldown);

            if (string.Equals(def.abilityId, SoulforgedWeaponAbilityId, StringComparison.OrdinalIgnoreCase))
            {
                int selected = GetSoulforgedWeaponSelectedChoice();
                if (selected == SoulforgedWeaponSwarmChoiceIndexLocal)
                {
                    mdps *= SoulforgedWeaponSwarmCountLocal * SoulforgedWeaponSwarmDamageMultiplierLocal;
                    summonDur = SoulforgedWeaponSwarmDurationSecondsLocal;
                    if (def.tooltipBuffMinionDurationSeconds > 0.01f)
                        summonDur = def.tooltipBuffMinionDurationSeconds;
                }
                else if (selected == SoulforgedWeaponExtendedDurationChoiceIndexLocal)
                {
                    summonDur = SoulforgedWeaponExtendedDurationSeconds;
                }
            }

            // Summon cooldown starts after the summon expires, so the average cycle is active duration + cooldown.
            float cycleSeconds = summonDur + cooldown;
            return Mathf.Max(0f, mdps * (summonDur / cycleSeconds));
        }

        float weaponMult = def.weaponDamageMultiplier;
        float cd = Mathf.Max(0.01f, def.cooldown);
        ApplyPowerSlashChoiceAdjustments(def, ref weaponMult, ref cd);
        float critFactor = GetCritFactor(stats);

        float avgPhys = (stats.MinSplitDamage.physical + stats.MaxSplitDamage.physical) * 0.5f;
        float avgMag = (stats.MinSplitDamage.magic + stats.MaxSplitDamage.magic) * 0.5f;
        float avgCorruption = (stats.MinSplitDamage.corruptionDamage + stats.MaxSplitDamage.corruptionDamage) * 0.5f;

        // Power Slash: rolled hit × multipliers (+ extras), not a second additive copy of the roll.
        if (string.Equals(def.abilityId, PowerSlashAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float psAllM = def.GetEffectiveAllDamageMultiplier();
            float apM = stats.GetAbilityPowerDamageMultiplier();
            float weaponEff = weaponMult <= 0f ? 1f : weaponMult;
            float elementBonus = AbilityElementScaling.GetElementDamageBonus(def, stats);
            float ailmentBonus = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
            float physExtra = (avgPhys * weaponEff + ailmentBonus) * apM * psAllM - avgPhys;
            float magExtra = (avgMag * weaponEff + elementBonus) * apM * psAllM - avgMag;
            float corrExtra = (avgCorruption * weaponEff) * apM * psAllM - avgCorruption;
            float rawBonus = physExtra + magExtra + corrExtra;
            float perEnhancedHit = rawBonus * critFactor;
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            return Mathf.Max(0f, perEnhancedHit * procRate);
        }

        // Rend: exclusive bleed on proc — value scales with how much "guaranteed bleed" improves over baseline chance.
        // Duration barely moves CP (longer DoT spreads the same pool in runtime; avoid inflating from +3s / stat duration).
        if (string.Equals(def.abilityId, RendAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            float baseDur = Mathf.Max(1f, stats.BleedBaseDuration);
            float duration = Mathf.Max(1f, stats.BleedDuration) + 3f;
            int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
            float durRatio = ticks / baseDur;
            durRatio = Mathf.Clamp(durRatio, 1f, 1.1f);

            float totalBleedDamage = Mathf.Max(0f, avgPhys * (1f + stats.BleedMultiplier) * durRatio);

            float marginalBleedProc = 1f - Mathf.Clamp01(stats.BleedChance);
            float reliabilityWeight = Mathf.Lerp(0.22f, 1f, marginalBleedProc);
            totalBleedDamage *= reliabilityWeight;

            int selected = GetRendSelectedChoiceForCombatPower();
            if (selected == 0)
                totalBleedDamage *= 1.05f; // Hemorrhaging Rush: same total, faster — small throughput bump.
            else if (selected == 1)
                totalBleedDamage *= 1f + 0.12f * AilmentRadialSpreadAssumedExtraTargets; // Crimson Spread: all in radius (~2.5 targets).

            return Mathf.Max(0f, totalBleedDamage * procRate);
        }

        // Envenom: poison from corruption on the quick strike — marginal value drops if poison already applies often; stacks matter.
        if (string.Equals(def.abilityId, EnvenomAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);

            float poisonSourceCorruption = Mathf.Max(0f, avgCorruption);
            if (poisonSourceCorruption <= 0f)
                return 0f;

            int selected = GetEnvenomSelectedChoiceForCombatPower();
            int baseStacks = Mathf.Max(1, stats.PoisonMaxStacks);
            int stackCount = baseStacks;
            if (selected == 0)
                stackCount = baseStacks + 2; // Potent Venom: extra stacks are a large part of the upgrade.

            float poisonPerStackTotal =
                poisonSourceCorruption * stats.PoisonPoolFractionOfCorruptionDamage *
                (1f + Mathf.Max(0f, stats.PoisonMultiplier));
            // Runtime Envenom splits one hit's poison pool across all stacks (stack count does not multiply tick DPS).
            float totalPoisonDamage = poisonPerStackTotal;

            float marginalPoisonProc = 1f - Mathf.Clamp01(stats.PoisonChance);
            float reliabilityWeight = Mathf.Lerp(0.28f, 1f, marginalPoisonProc);
            totalPoisonDamage *= reliabilityWeight;

            if (selected == 0)
                totalPoisonDamage *= 1.12f; // Potent Venom emphasis (extra cap stacks, same total pool).
            else if (selected == 1)
                totalPoisonDamage *= 1f + 0.12f * AilmentRadialSpreadAssumedExtraTargets; // Contagion: all in radius on death.

            return Mathf.Max(0f, totalPoisonDamage * procRate);
        }

        if (string.Equals(def.abilityId, CleavingStrikesAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = stats.AttacksPerSecond;
            float procRate = aps <= 0f ? (1f / cd) : Mathf.Min(aps, 1f / cd);
            int extraTargets = AbilityCombatPower.CleavingStrikesBaseExtraTargets;
            int empoweredHits = AbilityCombatPower.CleavingStrikesBaseEmpoweredHits;
            float duration = AbilityCombatPower.CleavingStrikesBaseDurationSeconds;
            int selected = GetCleavingStrikesSelectedChoiceForCombatPower();
            if (selected == 0)
            {
                extraTargets = AbilityCombatPower.CleavingStrikesBaseExtraTargets
                               + AbilityCombatPower.CleavingStrikesGreaterCleaveBonusTargets;
            }
            else if (selected == 1)
            {
                empoweredHits = AbilityCombatPower.CleavingStrikesLastingMomentumEmpoweredHits;
                duration = AbilityCombatPower.CleavingStrikesLastingMomentumDurationSeconds;
            }

            float activeWindow = Mathf.Min(duration, aps > 0f ? (empoweredHits / aps) : duration);
            float uptime = activeWindow / Mathf.Max(0.01f, cd);
            uptime = Mathf.Clamp01(uptime);
            float cleaveExtraScale = extraTargets * CleavingStrikesSecondaryHitWeaponDamageFraction;
            float baseHit = Mathf.Max(0f, avgPhys + avgMag + avgCorruption) * critFactor;
            return baseHit * cleaveExtraScale * procRate * uptime;
        }

        if (string.Equals(def.abilityId, CrusaderStrikeAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float crusaderAvgWeapon = stats.GetMeleeAverageWeaponPhysicalOrFireDamagePerHit();
            if (crusaderAvgWeapon <= 0f)
                return 0f;

            // Queued next-swing ability: base auto-attack DPS already counts the weapon hit, so CP only credits
            // the extra damage gained over a normal physical swing across the 3-part combo.
            float comboBonusScale =
                Mathf.Max(0f, CrusaderStrikeFirstHitWeaponMultiplier - 1f) +
                Mathf.Max(0f, CrusaderStrikeSecondHitWeaponMultiplier - 1f) +
                Mathf.Max(0f, CrusaderStrikeFinalHitWeaponMultiplier - 1f);
            float cycleSeconds = Mathf.Max(0.01f, cd + CrusaderStrikeComboTransitionSeconds);
            float fireSkillScale =
                1f + Mathf.Max(0f, stats.ElementSkillDamageScalingFractionFor(MagicAttackType.Fire)) *
                Mathf.Max(0f, def.fireDamageMultiplier);
            if (GetCrusaderStrikeSelectedChoiceForCombatPower() == PlayerAbilityController.CrusaderStrikeFireBalanceChoiceIndex)
                fireSkillScale *= CrusaderStrikeFireBalanceFinalStrikeFireMultiplier;
            float finalHitPortion = crusaderAvgWeapon * CrusaderStrikeFinalHitWeaponMultiplier * (fireSkillScale - 1f);
            float perCombo = crusaderAvgWeapon * comboBonusScale * critFactor + Mathf.Max(0f, finalHitPortion);
            return Mathf.Max(0f, perCombo / cycleSeconds);
        }

        // Default: instant cast (Whirlwind, Crescent Slash, etc.).
        float wEff = def.GetWeaponHitScalingMultiplier();
        float allM = def.GetEffectiveAllDamageMultiplier();
        float elementBonusInstant = AbilityElementScaling.GetElementDamageBonus(def, stats);
        float ailmentBonusInstant = AbilityElementScaling.GetPoisonBleedBonusForInstantAbility(def, stats);
        float apMInstant = stats.GetAbilityPowerDamageMultiplier();
        float elemM = AbilityElementScaling.GetElementSkillDamageMultiplier(stats);
        float raw =
            (avgPhys * wEff + ailmentBonusInstant) * allM * apMInstant
            + (avgMag * wEff * elemM + elementBonusInstant * elemM) * allM * apMInstant
            + (avgCorruption * wEff) * allM * apMInstant;
        float perCast = raw * critFactor;
        if (string.Equals(def.abilityId, WhirlwindAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float aps = Mathf.Max(0.01f, stats.AttacksPerSecond);
            return perCast * aps * 1.08f; // Slight AoE coverage on top of APS-scaled per-target damage.
        }

        if (string.Equals(def.abilityId, HammerTempestAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            float interval = 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);
            int choice = GetHammerTempestSelectedChoiceForCombatPower();
            if (choice == HammerTempestSacredArsenalChoiceIndex)
                interval *= HammerTempestSacredArsenalHitIntervalMultiplier;

            float duration = def.tooltipBuffMinionDurationSeconds > 0.01f
                ? def.tooltipBuffMinionDurationSeconds
                : HammerTempestBaseDurationSeconds;
            if (choice == HammerTempestSacredArsenalChoiceIndex)
                duration = Mathf.Max(0.1f, duration - HammerTempestSacredArsenalDurationPenaltySeconds);
            float hitsPerTarget = duration / Mathf.Max(0.01f, interval);
            float perHit = perCast * GetHammerTempestWeaponSpeedHitDamageMultiplier(stats.AttacksPerSecond);
            if (choice == HammerTempestSacredArsenalChoiceIndex)
                perHit *= HammerTempestSacredArsenalDamageMultiplier;
            if (choice == HammerTempestCrushingMomentumChoiceIndex)
                perHit *= 1f + HammerTempestCrushingMomentumDamagePerStack * 5f; // mid-stack estimate

            return Mathf.Max(0f, perHit * hitsPerTarget * 1.12f / cd);
        }

        float dps = Mathf.Max(0f, perCast / cd);

        if (string.Equals(def.abilityId, CrescentSlashAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            int selected = GetCrescentSlashSelectedChoiceForCombatPower();
            float expectedExtraHits = selected == 1 ? 2.5f : 2f; // soft estimate of extra enemies over primary
            float aoeLift = 1f + 0.055f * expectedExtraHits;
            if (selected == 0)
                aoeLift *= 1.04f; // Elemental Crescent: minor utility.
            return dps * aoeLift;
        }

        if (string.Equals(def.abilityId, GuardiansHammerAbilityId, StringComparison.OrdinalIgnoreCase))
        {
            int selected = GetGuardiansHammerSelectedChoiceForCombatPower();
            float aoeLift = 1.20f; // Large frontal slam.
            if (selected == 0)
                aoeLift *= 1.03f; // Protector's Resolve adds a defensive cushion.
            else if (selected == 1)
                aoeLift *= 1.07f; // Burning Verdict gains extra burn-pop coverage.
            return dps * aoeLift;
        }

        return dps;
    }

    private static int GetSoulforgedWeaponSelectedChoice()
    {
        SkillsManager skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(SkillType.Melee, SoulforgedWeaponChoiceSourceLevel, -1);
    }

    private static float GetCritFactor(CharacterStats stats)
    {
        float cc = Mathf.Clamp01(stats.CritChance);
        float cm = Mathf.Max(1f, stats.CritMultiplier);
        return 1f + cc * (cm - 1f);
    }

    private static void ApplyPowerSlashChoiceAdjustments(AbilityDefinition def, ref float weaponDamageMultiplier, ref float cooldownSeconds)
    {
        if (!def || !string.Equals(def.abilityId, PowerSlashAbilityId, StringComparison.OrdinalIgnoreCase))
            return;

        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return;

        int selected = sm.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
        if (selected == 0)
        {
            weaponDamageMultiplier += 0.30f; // Brutal Cut
        }
        else if (selected == 1)
        {
            cooldownSeconds = Mathf.Max(0.01f, cooldownSeconds - 3f); // Relentless Flow
        }
    }

    private static int GetRendSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    private static int GetEnvenomSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
    }

    private static int GetCleavingStrikesSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, "Lv15_1", -1);
    }

    private static int GetCrescentSlashSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, "Lv15_2", -1);
    }

    private static int GetGuardiansHammerSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, GuardiansHammerEnhancementParentSpineNodeId, -1);
    }

    private static int GetHammerTempestSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, HammerTempestEnhancementParentSpineNodeId, -1);
    }

    private static int GetCrusaderStrikeSelectedChoiceForCombatPower()
    {
        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return -1;

        return sm.GetSkillChoiceSelection(SkillType.Melee, CrusaderStrikeEnhancementParentSpineNodeId, -1);
    }
}
