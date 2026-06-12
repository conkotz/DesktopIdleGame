using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class EquipmentStatsPanelUI : MonoBehaviour
{
    private bool _refreshQueued;
    private bool _statsChangePending;
    private bool _statTooltipsWired;
    private bool _displayPrewarmed;
    private float _nextStatsRefreshAllowedAt;
    private int _gatheringToolLiveStamp = int.MinValue;
    private int _livingInfernoLiveStamp = int.MinValue;
    private static readonly Color BleedAilmentColor = new Color(0.78f, 0.12f, 0.12f);
    private static readonly Color PoisonAilmentColor = new Color(0.12f, 0.58f, 0.18f);
    private static readonly Color BurnAilmentColor = new Color(0.82f, 0.32f, 0.05f);
    private static readonly Color ShockAilmentColor = new Color(0.75f, 0.60f, 0.08f);
    private static readonly Color ChillAilmentColor = new Color(0.15f, 0.55f, 0.78f);
    private static readonly Color GlobalMagicBonusColor = new Color(0.12f, 0.42f, 0.78f);
    private static readonly Color RangedStyleBonusColor = new Color(0.14f, 0.52f, 0.18f);
    /// <summary>Dimmed ailment block when apply chance is 0% (matches prior elemental inactive styling).</summary>
    private static readonly Color AilmentInactiveGrey = new Color(0.48f, 0.52f, 0.5f);

    [Tooltip("Minimum seconds between stat-line repaints while buffs change in combat. Equipment swaps refresh immediately.")]
    [SerializeField, Min(0.05f)] private float statsRefreshMinInterval = 0.15f;

    [Header("Refs")]
    [SerializeField] private CharacterStats stats;
    [SerializeField] private SharedTooltipUI ailmentSharedTooltip;
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ToolbeltManager toolbelt;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private TMP_Text statsHeaderText;

    // -------------------------
    // Defensive
    // -------------------------
    [Header("Defensive Text")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text armorText;
    [SerializeField] private TMP_Text mrText;
    [SerializeField] private TMP_Text corruptionResistText;
    [SerializeField] private TMP_Text blockText;
    [SerializeField] private TMP_Text blockMitigationText;
    [SerializeField] private TMP_Text parryText;
    [SerializeField] private TMP_Text parryMitigationText;
    [SerializeField] private TMP_Text guardFlatText;
    [SerializeField] private TMP_Text maxGuardPercentText;

    [Header("Defensive (NEW)")]
    [SerializeField] private TMP_Text moveSpeedText;
    [SerializeField] private TMP_Text lifeRegenText;
    [FormerlySerializedAs("energyRegenText")]
    [SerializeField] private TMP_Text energyEfficiencyText;
    [SerializeField] private TMP_Text manaRegenText;
    [SerializeField] private TMP_Text abilityPowerText;

    // -------------------------
    // Offensive
    // -------------------------
    [Header("Offensive Text")]
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text damageSplitText;
    [SerializeField] private TMP_Text atkSpeedText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text critChanceText;
    [SerializeField] private TMP_Text critDamageText;
    [SerializeField] private TMP_Text cooldownReductionText;
    [SerializeField] private TMP_Text lifeStealText;
    [SerializeField] private TMP_Text stunChanceText;

    [Header("Minions (owner scaling — no DPS yet)")]
    [Tooltip("Optional. Assign TMP in offence tab; wire GameObject names MinionDamageText / MinionAttackSpeedText / MinionCritChanceText / MinionMaxLifeText for hover copy.")]
    [SerializeField] private TMP_Text minionDamageText;
    [SerializeField] private TMP_Text minionAttackSpeedText;
    [SerializeField] private TMP_Text minionCritChanceText;
    [SerializeField] private TMP_Text minionMaxLifeText;

    [Header("Offence — section titles (optional)")]
    [Tooltip("e.g. \"Global bonuses\". Shown above phys/magic/corruption lines.")]
    [SerializeField] private TMP_Text offenceGlobalBonusesHeaderText;
    [Tooltip("e.g. \"By weapon\". Shown above melee/ranged lines.")]
    [SerializeField] private TMP_Text offenceStyleBonusesHeaderText;

    [Header("Global bonuses (gear + supports)")]
    [FormerlySerializedAs("physDamageScalingText")]
    [SerializeField] private TMP_Text globalPhysicalAllText;
    [FormerlySerializedAs("magicDamageScalingText")]
    [SerializeField] private TMP_Text globalMagicAllText;
    [FormerlySerializedAs("corruptionDamageScalingText")]
    [SerializeField] private TMP_Text globalCorruptionAllText;
    [FormerlySerializedAs("fireSkillScalingText")]
    [SerializeField] private TMP_Text globalFireBonusText;
    [FormerlySerializedAs("iceSkillScalingText")]
    [SerializeField] private TMP_Text globalIceBonusText;
    [FormerlySerializedAs("lightningSkillScalingText")]
    [SerializeField] private TMP_Text globalLightningBonusText;

    [Header("Style bonuses (melee vs ranged — all basic-attack damage types)")]
    [FormerlySerializedAs("conditionalMeleePhysicalText")]
    [SerializeField] private TMP_Text meleeDamageBonusText;
    [FormerlySerializedAs("conditionalRangedPhysicalText")]
    [SerializeField] private TMP_Text rangedDamageBonusText;

    [Header("Ailments — section titles (overview tooltip)")]
    [FormerlySerializedAs("bleedText")]
    [SerializeField] private TMP_Text bleedSectionTitleText;
    [FormerlySerializedAs("poisonText")]
    [SerializeField] private TMP_Text poisonSectionTitleText;
    [FormerlySerializedAs("burnText")]
    [SerializeField] private TMP_Text burnSectionTitleText;
    [FormerlySerializedAs("shockText")]
    [SerializeField] private TMP_Text shockSectionTitleText;
    [FormerlySerializedAs("chillText")]
    [SerializeField] private TMP_Text chillSectionTitleText;

    [Header("Ailments — Bleed")]
    [SerializeField] private TMP_Text bleedChanceLineText;
    [SerializeField] private TMP_Text bleedMultiplierLineText;
    [SerializeField] private TMP_Text bleedDurationLineText;
    [SerializeField] private TMP_Text bleedMaxStacksLineText;

    [Header("Ailments — Poison")]
    [SerializeField] private TMP_Text poisonChanceLineText;
    [SerializeField] private TMP_Text poisonMultiplierLineText;
    [SerializeField] private TMP_Text poisonDurationLineText;
    [SerializeField] private TMP_Text poisonMaxStacksLineText;

    [Header("Ailments — Burn")]
    [SerializeField] private TMP_Text burnChanceLineText;
    [SerializeField] private TMP_Text burnMultiplierLineText;
    [SerializeField] private TMP_Text burnDurationLineText;
    [SerializeField] private TMP_Text burnStacksLineText;

    [Header("Ailments — Shock")]
    [SerializeField] private TMP_Text shockChanceLineText;
    [SerializeField] private TMP_Text shockDamageAmountLineText;
    [SerializeField] private TMP_Text shockDurationLineText;

    [Header("Ailments — Chill")]
    [SerializeField] private TMP_Text chillChanceLineText;
    [SerializeField] private TMP_Text chillEffectLineText;
    [SerializeField] private TMP_Text chillDurationLineText;
    [SerializeField] private TMP_Text chillStacksLineText;

    // -------------------------
    // DPS
    // -------------------------
    [Header("DPS (Bottom)")]
    [SerializeField] private TMP_Text dpsText;
    [Tooltip("Optional second line for (weapon, bleed, poison, …). Use a smaller font size here; if unset, the breakdown is appended under DPS with a reduced font via rich text.")]
    [SerializeField] private TMP_Text dpsBreakdownText;
    [Tooltip("When dpsBreakdownText is not assigned: breakdown font size = dpsText.fontSize × this.")]
    [SerializeField, Range(0.55f, 1f)] private float dpsBreakdownInlineFontScale = 0.82f;

    // -------------------------
    // Tools
    // -------------------------
    [Header("Tools (extra panel fields)")]
    [SerializeField] private TMP_Text pickaxeTitleText;
    [SerializeField] private TMP_Text pickaxeSpeedText;
    [SerializeField] private TMP_Text pickaxeGritText;
    [SerializeField] private TMP_Text pickaxeBonusFindText;
    [SerializeField] private TMP_Text pickaxeStaminaEfficiencyText;

    [SerializeField] private TMP_Text axeTitleText;
    [SerializeField] private TMP_Text axeSpeedText;
    [SerializeField] private TMP_Text axeGritText;
    [SerializeField] private TMP_Text axeBonusFindText;
    [SerializeField] private TMP_Text axeStaminaEfficiencyText;

    [SerializeField] private TMP_Text rodTitleText;
    [SerializeField] private TMP_Text rodSpeedText;
    [SerializeField] private TMP_Text rodGritText;
    [SerializeField] private TMP_Text rodBonusFindText;
    [SerializeField] private TMP_Text rodStaminaEfficiencyText;

    private void Awake()
    {
        ResolveRefsIfNeeded();
    }

    private void ResolveRefsIfNeeded()
    {
        if (!player) player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        // Prefer components from the player object so this panel never binds to enemy/NPC stats.
        if (player)
        {
            if (!stats) stats = player.GetComponent<CharacterStats>();
            if (!equipment) equipment = player.GetComponent<EquipmentManager>();
            if (!inventory) inventory = player.GetComponent<Inventory>();
            if (!toolbelt) toolbelt = player.GetComponent<ToolbeltManager>();
            if (!abilityController) abilityController = player.GetComponent<PlayerAbilityController>();
        }

        // Fallbacks (kept for safety in unusual setup scenes).
        if (!stats) stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
        if (!equipment) equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!toolbelt) toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);
    }

    /// <summary>Builds stat lines and TMP meshes while the menu is hidden during load.</summary>
    public void PrewarmForDisplay()
    {
        ResolveRefsIfNeeded();
        WireStatTooltipsOnce();
        _statsChangePending = false;
        _nextStatsRefreshAllowedAt = 0f;
        Refresh();
        ForceMeshUpdateAllStatText();
        _displayPrewarmed = true;
    }

    private void OnEnable()
    {
        ResolveRefsIfNeeded();

        if (equipment != null)
        {
            equipment.OnMainHandChanged += HandleRefresh;
            equipment.OnOffHandChanged += HandleRefresh;
            equipment.OnUISlotChanged += HandleUISlotChanged;
            equipment.OnActiveSetChanged += HandleActiveSetChanged;
        }

        if (toolbelt != null)
            toolbelt.OnToolSlotChanged += HandleToolChanged;

        if (stats != null)
        {
            stats.OnStatsChanged += HandleStatsChanged;
            stats.OnManaChanged += HandleVitalsChangedForEnergyInfusionDisplay;
            stats.OnEnergyChanged += HandleVitalsChangedForEnergyInfusionDisplay;
        }

        PlayerSprintInput.SprintStateChanged += HandleSprintStateChanged;

        WireStatTooltipsOnce();
        _statsChangePending = false;
        _nextStatsRefreshAllowedAt = 0f;

        if (MainMenuUIPrewarm.UseBatchedInstantiation)
            return;

        if (!_displayPrewarmed)
            Refresh();
    }

    private void ForceMeshUpdateAllStatText()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i])
                texts[i].ForceMeshUpdate(true);
        }
    }

    private void OnDisable()
    {
        if (equipment != null)
        {
            equipment.OnMainHandChanged -= HandleRefresh;
            equipment.OnOffHandChanged -= HandleRefresh;
            equipment.OnUISlotChanged -= HandleUISlotChanged;
            equipment.OnActiveSetChanged -= HandleActiveSetChanged;
        }

        if (toolbelt != null)
            toolbelt.OnToolSlotChanged -= HandleToolChanged;

        if (stats != null)
        {
            stats.OnStatsChanged -= HandleStatsChanged;
            stats.OnManaChanged -= HandleVitalsChangedForEnergyInfusionDisplay;
            stats.OnEnergyChanged -= HandleVitalsChangedForEnergyInfusionDisplay;
        }

        PlayerSprintInput.SprintStateChanged -= HandleSprintStateChanged;
    }

    private void HandleSprintStateChanged(bool _) => QueueImmediateRefresh();

    private void HandleVitalsChangedForEnergyInfusionDisplay(float _, float __)
    {
        if (abilityController != null && abilityController.IsEnergyInfusionActive)
            QueueStatsRefresh();
    }

    private void HandleRefresh(string _) => QueueImmediateRefresh();
    private void HandleUISlotChanged(EquipmentUISlotType _, string __) => QueueImmediateRefresh();
    private void HandleToolChanged(int _, string __) => QueueImmediateRefresh();
    private void HandleActiveSetChanged(int _)
    {
        // Weapon swap can suppress per-slot UI events; repaint immediately and again next frame.
        Refresh();
        QueueImmediateRefresh();
    }

    private void WireStatTooltipsOnce()
    {
        if (_statTooltipsWired)
            return;

        _statTooltipsWired = true;
        EnsureGuardStatTextRefs();
        EnsureParryStatTextRefs();
        EnsureStunChanceTextRef();
        EnsureOffenceStatTextRefs();
        EnsureOffenceBonusLineTooltips();
        BindAilmentLineTooltips();
        EnsureParryStatTooltips();
        EnsureToolStatLineTooltips();
    }

    private static string FormatSignedPercentFrom01(float value01)
    {
        float pct = value01 * 100f;
        return $"{pct:+0.#;-0.#;0}%";
    }

    private static string FormatSignedPercentPoints(float percentPoints)
    {
        return $"{percentPoints:+0.#;-0.#;0}%";
    }

    private static string FormatPoisonDurationLine(CharacterStats characterStats)
    {
        if (characterStats == null)
            return "Poison Duration: —";

        float seconds = characterStats.IsMasterOfVenomsLethalCompoundSelected()
            ? characterStats.GetEffectivePoisonDurationSeconds()
            : characterStats.PoisonDuration;
        return $"Poison Duration: {seconds:0.#}s";
    }

    private static string BuildMoveSpeedLine(CharacterStats characterStats)
    {
        if (characterStats == null)
            return "Move Speed: —";

        string basePct = FormatSignedPercentFrom01(characterStats.MoveSpeedBonusPercent);
        float baseSpeed = characterStats.FinalMoveSpeed;

        if (!PlayerSprintInput.ShouldApplyMoveSpeedBonus() && !PlayerSprintInput.IsSprinting)
            return $"Move Speed: {basePct} ({baseSpeed:0.#})";

        float sprintingSpeed = PlayerSprintInput.ApplySprintBonus(baseSpeed);
        float sprintPct = PlayerSprintInput.GetSprintBonusPercentOfBase(baseSpeed);
        return
            $"Move Speed: {basePct} ({baseSpeed:0.#} → {sprintingSpeed:0.#}, +{sprintPct:0.#}% sprint)";
    }

    private void HandleStatsChanged()
    {
        QueueStatsRefresh();
    }

    private void LateUpdate()
    {
        if (_refreshQueued)
        {
            _refreshQueued = false;
            _statsChangePending = false;
            _nextStatsRefreshAllowedAt = Time.unscaledTime + statsRefreshMinInterval;
            Refresh();
        }
        else if (_statsChangePending && Time.unscaledTime >= _nextStatsRefreshAllowedAt)
        {
            _statsChangePending = false;
            _nextStatsRefreshAllowedAt = Time.unscaledTime + statsRefreshMinInterval;
            Refresh();
        }

        PollGatheringToolsLiveRefresh();
        PollLivingInfernoLiveRefresh();
    }

    private void QueueImmediateRefresh()
    {
        _refreshQueued = true;
    }

    private void QueueStatsRefresh()
    {
        _statsChangePending = true;
    }

    public void Refresh()
    {
        if (!stats) return;

        // -------------------------
        // Defensive
        // -------------------------
        if (hpText) hpText.text = $"Max HP: {stats.MaxHP}";
        if (energyText) energyText.text = $"Energy: {stats.MaxEnergy}";
        if (manaText) manaText.text = $"Mana: {stats.MaxMana}";
        if (armorText) armorText.text = $"Armour: {stats.Armor} ({stats.PhysicalReductionFromArmorPercent:0.#}% Phys DR)";
        if (mrText) mrText.text = $"Magic Res: {stats.MagicResist} ({stats.MagicReductionFromMrPercent:0.#}% Mag DR)";
        if (corruptionResistText)
            corruptionResistText.text = $"Corr Res: {stats.CorruptionResist} ({stats.CorruptionReductionFromResistPercent:0.#}% Corr DR)";
        if (blockText) blockText.text = $"Phys Block: {stats.PhysBlockChancePercent:0.#}%";
        if (blockMitigationText)
            blockMitigationText.text = $"Block Mitigation: {stats.PhysBlockMitigationPercent:0.#}%";
        if (parryText)
            parryText.text = $"Parry: {stats.GetParryChancePercent():0.#}%";
        if (parryMitigationText)
            parryMitigationText.text = $"Parry Mitigation: {stats.GetParryMitigationPercent():0.#}%";

        if (guardFlatText)
            guardFlatText.text = $"Guard (flat): {stats.GearFlatGuardSum}";
        if (maxGuardPercentText)
        {
            float pctPts = stats.GearMaxGuardPercentSum * 100f;
            maxGuardPercentText.text =
                $"Max Guard: +{pctPts:0.#}% (Guard ceiling {stats.NaturalGuardHpCeilingFromGear:0.#})";
        }

        if (moveSpeedText)
            moveSpeedText.text = BuildMoveSpeedLine(stats);

        if (lifeRegenText) lifeRegenText.text = $"Life Regen: {stats.LifeRegenPerSecond:0.##}/s";
        if (manaRegenText) manaRegenText.text = $"Mana Regen: {stats.ManaRegenPerSecond:0.##}/s";
        if (energyEfficiencyText)
            energyEfficiencyText.text = $"Energy Efficiency: {stats.EnergyEfficiencyPercentPoints:0.#}%";
        if (abilityPowerText)
        {
            float ap = stats.AbilityPower;
            float combatApMult = stats.CombatAbilityPowerMultiplier;
            if (combatApMult > 1.001f)
                ap *= combatApMult;
            float abilityPotionPct = stats.AbilityDamageBoostConsumablePercentPoints;
            abilityPowerText.text = abilityPotionPct > 0.001f
                ? $"Ability Power: +{ap:0.#}% (potion {abilityPotionPct:+0.#;-0.#;0}%)"
                : ap > 0.001f
                    ? $"Ability Power: +{ap:0.#}%"
                    : "Ability Power: 0%";
        }
        if (statsHeaderText) statsHeaderText.text = $"Stats (CP: {stats.CombatPowerRounded})";

        if (offenceGlobalBonusesHeaderText)
            offenceGlobalBonusesHeaderText.text = "Global bonuses";
        if (offenceStyleBonusesHeaderText)
            offenceStyleBonusesHeaderText.text = "Style bonuses";

        // -------------------------
        // Offensive
        // -------------------------
        if (damageText)
        {
            bool meleeProfile = stats.CurrentAttackSkill == AttackSkill.Melee;
            string damageLabel = meleeProfile ? "Melee Damage" : "Damage";
            damageText.text = $"{damageLabel}: {stats.MinDamage}-{stats.MaxDamage}";
        }

        if (damageSplitText)
        {
            SplitDamage min = stats.MinSplitDamage;
            SplitDamage max = stats.MaxSplitDamage;

            bool hasPhys = max.physical > 0f;
            bool hasMag = max.magic > 0f;
            bool hasCorruption = max.corruptionDamage > 0f;

            bool hasAnyDamage = stats.MaxDamage > 0 || stats.MinDamage > 0;
            string typeLabel = stats.BuildAttackDamageTypeLabel();

            string split = "";

            if (hasPhys)
                split += $"P {Mathf.FloorToInt(min.physical)}-{Mathf.CeilToInt(max.physical)}";

            if (hasMag)
            {
                if (!string.IsNullOrEmpty(split)) split += " | ";
                split += $"M {Mathf.FloorToInt(min.magic)}-{Mathf.CeilToInt(max.magic)}";
            }

            if (hasCorruption)
            {
                if (!string.IsNullOrEmpty(split)) split += " | ";
                split += $"C {Mathf.FloorToInt(min.corruptionDamage)}-{Mathf.CeilToInt(max.corruptionDamage)}";
            }

            string colouredTypeLabel = hasAnyDamage
                ? BuildColouredTypeLabel(typeLabel)
                : typeLabel;

            damageSplitText.richText = true;
            damageSplitText.text = $"Type: {colouredTypeLabel}\nSplit: {split}";
        }

        if (atkSpeedText) atkSpeedText.text = $"Attack Speed: {stats.AttacksPerSecond:0.00}/s";
        if (rangeText) rangeText.text = $"Range: {stats.Range:0.##}";
        if (critChanceText) critChanceText.text = $"Crit: {stats.StatsPanelCritChancePercent:0.#}%";

        if (critDamageText)
            critDamageText.text = $"Crit Damage: {stats.StatsPanelCritDamageBonusPercent:+0.#;-0.#;0}%";

        if (cooldownReductionText)
            cooldownReductionText.text =
                $"Cooldown Reduction: {stats.FinalAbilityCooldownReductionPercentPoints:0.#}%";

        if (lifeStealText)
        {
            float lsPct = Mathf.Clamp01(stats.LifeSteal) * 100f;
            lifeStealText.text = $"Life Steal: {lsPct:0.#}% of damage";
        }

        if (stunChanceText)
            stunChanceText.text = $"Stun Chance: {stats.StunChancePercentForStatsPanel:0.#}%";

        if (minionDamageText)
            minionDamageText.text =
                $"Minion Damage: {FormatSignedPercentPoints(stats.FinalMinionDamagePercentPoints)}";
        if (minionAttackSpeedText)
            minionAttackSpeedText.text =
                $"Minion Attack Speed: {FormatSignedPercentPoints(stats.FinalMinionAttackSpeedPercentPoints)}";
        if (minionCritChanceText)
            minionCritChanceText.text =
                $"Minion Crit Chance: {stats.FinalMinionCritChancePercentPoints:0.#}%";
        if (minionMaxLifeText)
            minionMaxLifeText.text =
                $"Minion Max Life: {FormatSignedPercentPoints(stats.FinalMinionMaxLifePercentPoints)}";

        if (globalPhysicalAllText)
            globalPhysicalAllText.text = $"Physical: {FormatSignedPercentPoints(stats.GlobalPhysicalDamageBonusPercentPoints)}";
        if (globalMagicAllText)
            globalMagicAllText.text = $"Magic: {FormatSignedPercentPoints(stats.GlobalMagicDamageBonusPercentPoints)}";
        if (globalCorruptionAllText)
            globalCorruptionAllText.text = $"Corruption: {FormatSignedPercentPoints(stats.GlobalCorruptionDamageBonusPercentPoints)}";
        if (globalFireBonusText)
            globalFireBonusText.text = $"Fire: {FormatSignedPercentPoints(stats.FireSkillDamageTotalScalingPercentPoints)}";
        if (globalIceBonusText)
            globalIceBonusText.text = $"Ice: {FormatSignedPercentPoints(stats.IceSkillDamageTotalScalingPercentPoints)}";
        if (globalLightningBonusText)
            globalLightningBonusText.text = $"Lightning: {FormatSignedPercentPoints(stats.LightningSkillDamageTotalScalingPercentPoints)}";

        if (meleeDamageBonusText)
            meleeDamageBonusText.text =
                $"Melee total: {FormatSignedPercentPoints(stats.MeleePhysicalConditionalBonusPercentPoints)}";
        if (rangedDamageBonusText)
            rangedDamageBonusText.text =
                $"Ranged total: {FormatSignedPercentPoints(stats.RangedTotalDamageBonusPercentPoints)}";

        PopulateDetailedAilmentLines();
        ApplyOffenceBonusLineColors();

        // -------------------------
        // DPS
        // -------------------------
        if (dpsText)
        {
            float sheetDps = stats.DPS;
            float weaponDps = stats.WeaponDpsComponent;
            float bleedDps = stats.ExpectedBleedDPS;
            float poisonDps = stats.ExpectedPoisonDPS;
            float burnDps = stats.ExpectedBurnDPS;
            float abilityDps = AbilityCombatPower.EstimateTotalSlottedAbilityDps(stats);
            float totalWithAbilities = sheetDps + Mathf.Max(0f, abilityDps);

            float headlineTotal = abilityDps > 0.01f ? totalWithAbilities : sheetDps;
            string breakdown = BuildDpsBreakdownLine(weaponDps, bleedDps, poisonDps, burnDps, abilityDps);

            if (dpsBreakdownText)
            {
                dpsText.text = $"DPS: {headlineTotal:0.#}";
                dpsBreakdownText.text = $"({breakdown})";
            }
            else
            {
                dpsText.richText = true;
                int subSize = Mathf.Max(8, Mathf.RoundToInt(dpsText.fontSize * dpsBreakdownInlineFontScale));
                dpsText.text = $"DPS: {headlineTotal:0.#}\n<size={subSize}>({breakdown})</size>";
            }
        }

        // -------------------------
        // Tools
        // -------------------------
        PopulateGatheringToolsSection();

        if (player != null)
            _gatheringToolLiveStamp = HashCode.Combine(player.GetWoodcuttingStatsPanelStamp(), player.GetFishingStatsPanelStamp());
    }

    private void PollGatheringToolsLiveRefresh()
    {
        if (!player || !stats)
            return;

        int stamp = HashCode.Combine(player.GetWoodcuttingStatsPanelStamp(), player.GetFishingStatsPanelStamp());
        if (stamp == _gatheringToolLiveStamp)
            return;

        _gatheringToolLiveStamp = stamp;
        PopulateGatheringToolsSection();
    }

    private void PollLivingInfernoLiveRefresh()
    {
        if (!stats || stats.GetPhoenixSoulEnhancementPick() != 1)
        {
            _livingInfernoLiveStamp = int.MinValue;
            return;
        }

        if (!abilityController && player)
            abilityController = player.GetComponent<PlayerAbilityController>();
        if (!abilityController)
            return;

        int stamp = HashCode.Combine(
            abilityController.GetPhoenixLivingInfernoNearbyBurningCount(),
            Mathf.RoundToInt(abilityController.GetPhoenixLivingInfernoMeleeDamageBonusFraction() * 1000f));
        if (stamp == _livingInfernoLiveStamp)
            return;

        _livingInfernoLiveStamp = stamp;
        QueueStatsRefresh();
    }

    private void PopulateGatheringToolsSection()
    {
        if (!stats)
            return;

        if (pickaxeTitleText) pickaxeTitleText.text = "Pickaxe";
        if (pickaxeSpeedText) pickaxeSpeedText.text = $"Mining Speed: {stats.PickaxeSpeedMult:0.##}x";
        if (pickaxeGritText) pickaxeGritText.text = $"Mining Grit: {stats.PickaxeGrit * 100f:0.#}%";
        if (pickaxeBonusFindText) pickaxeBonusFindText.text = $"Bonus Find: +{stats.PickaxeBonusFindChance * 100f:0.#}%";
        if (pickaxeStaminaEfficiencyText) pickaxeStaminaEfficiencyText.text = $"Stamina Eff: +{stats.PickaxeStaminaEfficiency * 100f:0.#}%";

        if (axeTitleText) axeTitleText.text = "Axe";

        PlayerAbilityController ability = null;
        if (player != null)
            ability = player.GetComponent<PlayerAbilityController>();

        float baseAxeSpeed = stats.AxeSpeedMult;
        if (axeSpeedText)
        {
            float frenSpd = 0f, ffSpd = 0f, majSpd = 0f;
            if (player != null)
                player.TryGetWoodcuttingLiveBuffInfo(out frenSpd, out ffSpd, out _, out majSpd, out _);

            float avatarFlat = ability != null ? ability.GetAvatarOfTheForestWoodcuttingSpeedMultiplierFlatAdd() : 0f;
            float displayAxeSpeed = baseAxeSpeed * (1f + frenSpd + ffSpd + majSpd) + avatarFlat;

            axeSpeedText.richText = false;
            axeSpeedText.text = $"Woodcutting Speed: {displayAxeSpeed:0.##}x";
        }

        if (axeGritText)
        {
            float grit = stats.AxeGrit;
            if (player != null && player.TryGetWoodcuttingMajorFlowDeepFocusGritBonus(out float add))
                grit = Mathf.Clamp01(grit + add);
            axeGritText.text = $"Woodcutting Grit: {grit * 100f:0.#}%";
        }
        if (axeBonusFindText)
        {
            float avatarBfMul = ability != null ? ability.GetAvatarOfTheForestBonusFindFinalMultiplier() : 1f;
            float displayBf = Mathf.Clamp01(stats.AxeBonusFindChance * avatarBfMul);
            axeBonusFindText.text = $"Bonus Find: +{displayBf * 100f:0.#}%";
        }

        float baseAxeStam = stats.AxeStaminaEfficiency;
        if (axeStaminaEfficiencyText)
        {
            float displayAxeStam = baseAxeStam;
            if (player != null &&
                player.TryGetWoodcuttingLiveBuffInfo(out _, out _, out float ffStam, out _, out float majStam) &&
                (ffStam > 0f || majStam > 0f))
                displayAxeStam = Mathf.Clamp01(baseAxeStam + ffStam + majStam);

            axeStaminaEfficiencyText.richText = false;
            axeStaminaEfficiencyText.text = $"Stamina Eff: +{displayAxeStam * 100f:0.#}%";
        }

        if (rodTitleText) rodTitleText.text = "Rod";

        float baseRodSpeed = stats.RodSpeedMult;
        if (rodSpeedText)
        {
            float displayRodSpeed = baseRodSpeed;
            if (player != null &&
                player.TryGetFishingLiveBuffInfo(out float frenSpd, out float calmSpd, out _) &&
                (frenSpd > 0f || calmSpd > 0f))
                displayRodSpeed = baseRodSpeed * (1f + frenSpd + calmSpd);

            rodSpeedText.richText = false;
            rodSpeedText.text = $"Fishing Speed: {displayRodSpeed:0.##}x";
        }

        if (rodGritText) rodGritText.text = $"Fishing Grit: {stats.RodGrit * 100f:0.#}%";
        if (rodBonusFindText) rodBonusFindText.text = $"Bonus Find: +{stats.RodBonusFindChance * 100f:0.#}%";

        float baseRodStam = stats.RodStaminaEfficiency;
        if (rodStaminaEfficiencyText)
        {
            float displayRodStam = baseRodStam;
            if (player != null &&
                player.TryGetFishingLiveBuffInfo(out _, out _, out float calmStam) &&
                calmStam > 0f)
                displayRodStam = Mathf.Clamp01(baseRodStam + calmStam);

            rodStaminaEfficiencyText.richText = false;
            rodStaminaEfficiencyText.text = $"Stamina Eff: +{displayRodStam * 100f:0.#}%";
        }
    }

    private static string BuildDpsBreakdownLine(
        float weaponDps,
        float bleedDps,
        float poisonDps,
        float burnDps,
        float abilityDps)
    {
        const float eps = 0.01f;
        var parts = new List<string>(5) { $"{weaponDps:0.#} weapon" };
        if (bleedDps > eps)
            parts.Add($"{bleedDps:0.#} bleed");
        if (poisonDps > eps)
            parts.Add($"{poisonDps:0.#} poison");
        if (burnDps > eps)
            parts.Add($"{burnDps:0.#} burn");
        if (abilityDps > eps)
            parts.Add($"{abilityDps:0.#} ability");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Binds defence guard lines when inspector refs were not saved (e.g. older scenes) or names differ.
    /// </summary>
    private void EnsureGuardStatTextRefs()
    {
        if (guardFlatText && maxGuardPercentText)
            return;

        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string key = GameTooltipTexts.NormalizeUiElementName(tmp.gameObject.name);
            if (!guardFlatText &&
                key.Equals("GuardFlatText", StringComparison.OrdinalIgnoreCase))
                guardFlatText = tmp;
            else if (!maxGuardPercentText &&
                     (key.Equals("MaxGuardPercentText", StringComparison.OrdinalIgnoreCase) ||
                      key.Equals("MaxGuardText", StringComparison.OrdinalIgnoreCase)))
                maxGuardPercentText = tmp;
        }
    }

    private void EnsureStunChanceTextRef()
    {
        if (stunChanceText)
            return;

        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string key = GameTooltipTexts.NormalizeUiElementName(tmp.gameObject.name);
            if (key.Equals("StunChanceText", StringComparison.OrdinalIgnoreCase))
            {
                stunChanceText = tmp;
                return;
            }
        }
    }

    private void EnsureOffenceStatTextRefs()
    {
        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string key = GameTooltipTexts.NormalizeUiElementName(tmp.gameObject.name);
            if (!abilityPowerText && key.Equals("AbilityPowerText", StringComparison.OrdinalIgnoreCase))
                abilityPowerText = tmp;
            else if (!cooldownReductionText && key.Equals("CooldownReductionText", StringComparison.OrdinalIgnoreCase))
                cooldownReductionText = tmp;
            else if (!minionDamageText && (key.Equals("MinionDamageText", StringComparison.OrdinalIgnoreCase) ||
                                           key.Equals("ConditionalMinionDmgText", StringComparison.OrdinalIgnoreCase)))
                minionDamageText = tmp;
            else if (!minionAttackSpeedText && (key.Equals("MinionAttackSpeedText", StringComparison.OrdinalIgnoreCase) ||
                                                key.Equals("ConditionalMinionAtkSpeedText", StringComparison.OrdinalIgnoreCase)))
                minionAttackSpeedText = tmp;
            else if (!minionCritChanceText && (key.Equals("MinionCritChanceText", StringComparison.OrdinalIgnoreCase) ||
                                               key.Equals("ConditionalMinionCritRateText", StringComparison.OrdinalIgnoreCase)))
                minionCritChanceText = tmp;
            else if (!minionMaxLifeText && (key.Equals("MinionMaxLifeText", StringComparison.OrdinalIgnoreCase) ||
                                            key.Equals("ConditionalMinionMaxLifeText", StringComparison.OrdinalIgnoreCase)))
                minionMaxLifeText = tmp;
        }
    }

    private void EnsureParryStatTextRefs()
    {
        if (parryText && parryMitigationText)
            return;

        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string key = GameTooltipTexts.NormalizeUiElementName(tmp.gameObject.name);
            if (!parryText && key.Equals("ParryText", StringComparison.OrdinalIgnoreCase))
                parryText = tmp;
            else if (!parryMitigationText && key.Equals("ParryMitigationText", StringComparison.OrdinalIgnoreCase))
                parryMitigationText = tmp;
        }
    }

    private SharedTooltipUI ResolveAilmentSharedTooltip()
    {
        if (ailmentSharedTooltip)
            return ailmentSharedTooltip;

        SharedTooltipUI[] all =
            FindObjectsByType<SharedTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (SharedTooltipUI t in all)
        {
            if (t && t.name == "SharedToolTipInfoPanel")
                return t;
        }

        foreach (SharedTooltipUI t in all)
        {
            if (t && t.name != "HUDToolInfoPanel")
                return t;
        }

        return null;
    }

    /// <summary>
    /// Ensures hover tooltips on global / style bonus lines (uses <see cref="GameTooltipTexts"/> by GameObject name).
    /// Binds the same <see cref="SharedTooltipUI"/> as ailment rows and rescans children so duplicate rows (e.g. extra tab layouts) still work.
    /// </summary>
    private void EnsureOffenceBonusLineTooltips()
    {
        SharedTooltipUI tip = ResolveAilmentSharedTooltip();

        void Wire(TMP_Text tmp)
        {
            if (!tmp)
                return;
            tmp.raycastTarget = true;

            EquipmentAilmentLineTooltip ailmentOnly = tmp.GetComponent<EquipmentAilmentLineTooltip>();
            if (ailmentOnly)
                Destroy(ailmentOnly);

            UIHoverTooltip hover = tmp.GetComponent<UIHoverTooltip>();
            if (!hover)
                hover = tmp.gameObject.AddComponent<UIHoverTooltip>();
            if (tip)
                hover.ConfigureForEquipmentStats(tip);
        }

        void WireFixedGuardLine(TMP_Text tmp, string gameTooltipKey)
        {
            if (!tmp || !tip)
                return;
            if (!GameTooltipTexts.TryGetForUiElement(gameTooltipKey, out string title, out string desc))
                return;
            tmp.raycastTarget = true;
            EquipmentAilmentLineTooltip ailmentOnly = tmp.GetComponent<EquipmentAilmentLineTooltip>();
            if (ailmentOnly)
                Destroy(ailmentOnly);
            UIHoverTooltip hover = tmp.GetComponent<UIHoverTooltip>();
            if (!hover)
                hover = tmp.gameObject.AddComponent<UIHoverTooltip>();
            hover.ConfigureForEquipmentStatsFixedCopy(tip, title, desc);
        }

        Wire(globalPhysicalAllText);
        Wire(globalMagicAllText);
        Wire(globalCorruptionAllText);
        Wire(globalFireBonusText);
        Wire(globalIceBonusText);
        Wire(globalLightningBonusText);
        Wire(meleeDamageBonusText);
        Wire(rangedDamageBonusText);
        Wire(abilityPowerText);
        Wire(cooldownReductionText);
        Wire(minionDamageText);
        Wire(minionAttackSpeedText);
        Wire(minionCritChanceText);
        Wire(minionMaxLifeText);
        WireFixedGuardLine(guardFlatText, "GuardFlatText");
        WireFixedGuardLine(maxGuardPercentText, "MaxGuardPercentText");

        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string rowName = GameTooltipTexts.NormalizeUiElementName(tmp.gameObject.name);
            if (rowName.Equals("GuardFlatText", StringComparison.OrdinalIgnoreCase))
            {
                WireFixedGuardLine(tmp, "GuardFlatText");
                continue;
            }

            if (rowName.Equals("MaxGuardPercentText", StringComparison.OrdinalIgnoreCase) ||
                rowName.Equals("MaxGuardText", StringComparison.OrdinalIgnoreCase))
            {
                WireFixedGuardLine(tmp, "MaxGuardPercentText");
                continue;
            }

            switch (rowName)
            {
                case "AbilityPowerText":
                case "CooldownReductionText":
                case "PhysicalBonusText":
                case "GlobalPhysicalBonusText":
                case "MagBonusText":
                case "GlobalMagBonusText":
                case "CorruptionBonusText":
                case "GlobalCorruptionBonusText":
                case "FireBonusText":
                case "IceBonusText":
                case "LightningBonusText":
                case "MeleePhysBonusText":
                case "MeleeDamageBonusText":
                case "ConditionalMeleePhysBonusText":
                case "RangedPhysBonusText":
                case "RangedDamageBonusText":
                case "ConditionalRangedPhysBonusText":
                case "MinionDamageText":
                case "ConditionalMinionDmgText":
                case "MinionAttackSpeedText":
                case "ConditionalMinionAtkSpeedText":
                case "MinionCritChanceText":
                case "ConditionalMinionCritRateText":
                case "MinionMaxLifeText":
                case "ConditionalMinionMaxLifeText":
                    Wire(tmp);
                    break;
            }
        }
    }

    private void BindAilmentLineTooltips()
    {
        if (!stats)
            return;

        SharedTooltipUI tip = ResolveAilmentSharedTooltip();
        if (!tip)
            return;

        void Wire(TMP_Text tmp, EquipmentAilmentLineTooltip.LineId lineId)
        {
            if (!tmp)
                return;
            tmp.raycastTarget = true;
            EquipmentAilmentLineTooltip lineTip = tmp.GetComponent<EquipmentAilmentLineTooltip>();
            if (!lineTip)
                lineTip = tmp.gameObject.AddComponent<EquipmentAilmentLineTooltip>();
            lineTip.SetLineId(lineId);
            lineTip.Bind(stats, tip);
        }

        Wire(bleedSectionTitleText, EquipmentAilmentLineTooltip.LineId.BleedOverview);
        Wire(bleedChanceLineText, EquipmentAilmentLineTooltip.LineId.BleedChance);
        Wire(bleedMultiplierLineText, EquipmentAilmentLineTooltip.LineId.BleedMultiplier);
        Wire(bleedDurationLineText, EquipmentAilmentLineTooltip.LineId.BleedDuration);
        Wire(bleedMaxStacksLineText, EquipmentAilmentLineTooltip.LineId.BleedMaxStacks);

        Wire(poisonSectionTitleText, EquipmentAilmentLineTooltip.LineId.PoisonOverview);
        Wire(poisonChanceLineText, EquipmentAilmentLineTooltip.LineId.PoisonChance);
        Wire(poisonMultiplierLineText, EquipmentAilmentLineTooltip.LineId.PoisonMultiplier);
        Wire(poisonDurationLineText, EquipmentAilmentLineTooltip.LineId.PoisonDuration);
        Wire(poisonMaxStacksLineText, EquipmentAilmentLineTooltip.LineId.PoisonMaxStacks);

        Wire(burnSectionTitleText, EquipmentAilmentLineTooltip.LineId.BurnOverview);
        Wire(burnChanceLineText, EquipmentAilmentLineTooltip.LineId.BurnChance);
        Wire(burnMultiplierLineText, EquipmentAilmentLineTooltip.LineId.BurnMultiplier);
        Wire(burnDurationLineText, EquipmentAilmentLineTooltip.LineId.BurnDuration);
        Wire(burnStacksLineText, EquipmentAilmentLineTooltip.LineId.BurnStacks);

        Wire(shockSectionTitleText, EquipmentAilmentLineTooltip.LineId.ShockOverview);
        Wire(shockChanceLineText, EquipmentAilmentLineTooltip.LineId.ShockChance);
        Wire(shockDamageAmountLineText, EquipmentAilmentLineTooltip.LineId.ShockDamageAmount);
        Wire(shockDurationLineText, EquipmentAilmentLineTooltip.LineId.ShockDuration);

        Wire(chillSectionTitleText, EquipmentAilmentLineTooltip.LineId.ChillOverview);
        Wire(chillChanceLineText, EquipmentAilmentLineTooltip.LineId.ChillChance);
        Wire(chillEffectLineText, EquipmentAilmentLineTooltip.LineId.ChillEffect);
        Wire(chillDurationLineText, EquipmentAilmentLineTooltip.LineId.ChillDuration);
        Wire(chillStacksLineText, EquipmentAilmentLineTooltip.LineId.ChillStacks);

        foreach (EquipmentAilmentLineTooltip extra in GetComponentsInChildren<EquipmentAilmentLineTooltip>(true))
            extra.Bind(stats, tip);
    }

    private void PopulateDetailedAilmentLines()
    {
        if (stats == null)
            return;

        if (bleedChanceLineText)
            bleedChanceLineText.text = $"Bleed Chance: {stats.BleedChancePercentForStatsPanel:0.#}%";
        if (bleedMultiplierLineText)
            bleedMultiplierLineText.text = $"Bleed Multiplier: {FormatSignedPercentPoints(stats.BleedMultiplier * 100f)}";
        if (bleedDurationLineText)
            bleedDurationLineText.text = $"Bleed Duration: {stats.BleedDuration:0.#}s";
        if (bleedMaxStacksLineText)
            bleedMaxStacksLineText.text = $"Bleed Max Stacks: {stats.BleedMaxStacks}";

        if (poisonChanceLineText)
            poisonChanceLineText.text = $"Poison Chance: {stats.PoisonChancePercent:0.#}%";
        if (poisonMultiplierLineText)
            poisonMultiplierLineText.text = $"Poison Multiplier: {FormatSignedPercentPoints(stats.PoisonMultiplier * 100f)}";
        if (poisonDurationLineText)
            poisonDurationLineText.text = FormatPoisonDurationLine(stats);
        if (poisonMaxStacksLineText)
            poisonMaxStacksLineText.text = $"Poison Max Stacks: {stats.PoisonMaxStacks}";

        if (burnChanceLineText)
            burnChanceLineText.text = $"Burn Chance: {stats.BurnApplyChancePercentForStatsPanel:0.#}%";
        if (burnMultiplierLineText)
            burnMultiplierLineText.text =
                $"Burn Multiplier: {FormatSignedPercentPoints((stats.BurnDamageMultiplier - 1f) * 100f)}";
        if (burnDurationLineText)
            burnDurationLineText.text = $"Burn Duration: {stats.BurnDotDurationSeconds:0.#}s";
        if (burnStacksLineText)
            burnStacksLineText.text = $"Burn Stacks: {stats.BurnHitsToExplode}";

        if (shockChanceLineText)
            shockChanceLineText.text = $"Shock Chance: {stats.ShockApplyChancePercentForStatsPanel:0.#}%";
        if (shockDamageAmountLineText)
            shockDamageAmountLineText.text = $"Shock Damage Amount: {stats.ShockDamageTakenMultiplier * 100f:0.#}%";
        if (shockDurationLineText)
            shockDurationLineText.text = $"Shock Duration: {stats.ShockDuration:0.#}s";

        if (chillChanceLineText)
            chillChanceLineText.text = $"Chill Chance: {stats.ChillApplyChancePercentForStatsPanel:0.#}%";
        if (chillEffectLineText)
            chillEffectLineText.text = $"Chill Effect: {stats.ChillSlowPerStack * 100f:0.#}% slow / stack";
        if (chillDurationLineText)
            chillDurationLineText.text = $"Chill Duration: {stats.ChillDuration:0.#}s";
        if (chillStacksLineText)
            chillStacksLineText.text = $"Chill Stacks: {stats.ChillMaxStacks}";

        ApplyAilmentLineColors();
    }

    private void ApplyAilmentLineColors()
    {
        if (!stats)
            return;

        void Colorize(TMP_Text t, Color c)
        {
            if (t)
                t.color = c;
        }

        bool bleedApplies = stats.GetEffectiveBleedChanceForProcs() > 0f;
        bool poisonApplies = stats.PoisonChance > 0f;
        bool burnApplies = stats.BurnApplyChancePercentForStatsPanel > 0f;
        bool shockApplies = stats.ShockApplyChancePercentForStatsPanel > 0f;
        bool chillApplies = stats.ChillApplyChancePercentForStatsPanel > 0f;

        Color bleedC = bleedApplies ? BleedAilmentColor : AilmentInactiveGrey;
        Color poisonC = poisonApplies ? PoisonAilmentColor : AilmentInactiveGrey;
        Color burnC = burnApplies ? BurnAilmentColor : AilmentInactiveGrey;
        Color shockC = shockApplies ? ShockAilmentColor : AilmentInactiveGrey;
        Color chillC = chillApplies ? ChillAilmentColor : AilmentInactiveGrey;

        Colorize(bleedSectionTitleText, bleedC);
        Colorize(bleedChanceLineText, bleedC);
        Colorize(bleedMultiplierLineText, bleedC);
        Colorize(bleedDurationLineText, bleedC);
        Colorize(bleedMaxStacksLineText, bleedC);

        Colorize(poisonSectionTitleText, poisonC);
        Colorize(poisonChanceLineText, poisonC);
        Colorize(poisonMultiplierLineText, poisonC);
        Colorize(poisonDurationLineText, poisonC);
        Colorize(poisonMaxStacksLineText, poisonC);

        Colorize(burnSectionTitleText, burnC);
        Colorize(burnChanceLineText, burnC);
        Colorize(burnMultiplierLineText, burnC);
        Colorize(burnDurationLineText, burnC);
        Colorize(burnStacksLineText, burnC);

        Colorize(shockSectionTitleText, shockC);
        Colorize(shockChanceLineText, shockC);
        Colorize(shockDamageAmountLineText, shockC);
        Colorize(shockDurationLineText, shockC);

        Colorize(chillSectionTitleText, chillC);
        Colorize(chillChanceLineText, chillC);
        Colorize(chillEffectLineText, chillC);
        Colorize(chillDurationLineText, chillC);
        Colorize(chillStacksLineText, chillC);
    }

    private void ApplyOffenceBonusLineColors()
    {
        void Colorize(TMP_Text t, Color c)
        {
            if (t)
                t.color = c;
        }

        Colorize(globalMagicAllText, GlobalMagicBonusColor);
        Colorize(rangedDamageBonusText, RangedStyleBonusColor);

        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            string rowName = GameTooltipTexts.NormalizeUiElementName(tmp.gameObject.name);
            if (rowName.Equals("GlobalMagBonusText", StringComparison.OrdinalIgnoreCase) ||
                rowName.Equals("MagBonusText", StringComparison.OrdinalIgnoreCase))
                Colorize(tmp, GlobalMagicBonusColor);
            else if (rowName.Equals("RangedDamageBonusText", StringComparison.OrdinalIgnoreCase) ||
                     rowName.Equals("RangedPhysBonusText", StringComparison.OrdinalIgnoreCase) ||
                     rowName.Equals("ConditionalRangedPhysBonusText", StringComparison.OrdinalIgnoreCase))
                Colorize(tmp, RangedStyleBonusColor);
        }
    }

    private static string BoldColoredTypeWord(string hex, string word) =>
        $"<color={hex}><b>{word}</b></color>";

    private static string BuildColouredTypeLabel(string plainTypeLabel)
    {
        if (string.IsNullOrWhiteSpace(plainTypeLabel) || plainTypeLabel == "-")
            return plainTypeLabel;

        const string phys = "#CC3333";
        const string mag = "#2070B8";
        const string corr = "#5020A0";
        const string fire = "#CC4400";
        const string ice = "#2E7BB8";
        const string lightning = "#B88600";

        return plainTypeLabel
            .Replace("Physical", BoldColoredTypeWord(phys, "Physical"))
            .Replace("Magic (mixed)", BoldColoredTypeWord(mag, "Magic (mixed)"))
            .Replace("Corruption", BoldColoredTypeWord(corr, "Corruption"))
            .Replace("Lightning", BoldColoredTypeWord(lightning, "Lightning"))
            .Replace("Fire", BoldColoredTypeWord(fire, "Fire"))
            .Replace("Ice", BoldColoredTypeWord(ice, "Ice"))
            .Replace("Magic", BoldColoredTypeWord(mag, "Magic"));
    }

    private void EnsureToolStatLineTooltips()
    {
        SharedTooltipUI tip = ResolveAilmentSharedTooltip();
        if (!tip)
            return;

        void Wire(TMP_Text tmp, string title, string body)
        {
            if (!tmp)
                return;

            tmp.raycastTarget = true;

            EquipmentAilmentLineTooltip ailmentOnly = tmp.GetComponent<EquipmentAilmentLineTooltip>();
            if (ailmentOnly)
                Destroy(ailmentOnly);

            UIHoverTooltip hover = tmp.GetComponent<UIHoverTooltip>();
            if (!hover)
                hover = tmp.gameObject.AddComponent<UIHoverTooltip>();
            hover.ConfigureForEquipmentStatsFixedCopy(tip, title, body);
        }

        const string speedBody =
            "How quickly this tool performs gathering actions.\n\n" +
            "Higher speed means faster gathering cycles and more resources over time.";
        const string gritBody =
            "Chance to double the base gather yield.\n\n" +
            "Only doubles the main/base resource roll and does not duplicate bonus-find drops.";
        const string bonusFindBody =
            "Extra chance to find bonus resources while gathering with this tool.\n\n" +
            "Applies per gather action and stacks with other bonus find sources.";
        float hiddenRevealPct = player != null ? player.GetWoodcuttingHiddenRevealChanceFlatBonus() * 100f : 0f;
        bool axeInBelt = UnityEngine.Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include) is { } pac &&
                         pac.HasAxeInToolbelt();
        string hiddenExplain =
            "Hidden items can roll after a bonus resource proc on the same gather tick (base 0% chance before bonuses).";
        string hiddenValueLine = $"+{hiddenRevealPct:0.#}% chance to find hidden resources.";
        if (axeInBelt)
            hiddenValueLine = $"<color=#55DD55>{hiddenValueLine}</color>";
        string axeBonusFindBody = bonusFindBody + "\n\n" + hiddenExplain + "\n\n" + hiddenValueLine;
        const string staminaEffBody =
            "Reduces stamina/energy cost pressure while gathering.\n\n" +
            "Higher efficiency lets you gather longer before running out of stamina.";

        Wire(pickaxeSpeedText, "Mining Speed", speedBody);
        Wire(pickaxeGritText, "Mining Grit", gritBody);
        Wire(pickaxeBonusFindText, "Mining Bonus Find", bonusFindBody);
        Wire(pickaxeStaminaEfficiencyText, "Mining Stamina Efficiency", staminaEffBody);

        Wire(axeSpeedText, "Woodcutting Speed", speedBody);
        Wire(axeGritText, "Woodcutting Grit", gritBody);
        Wire(axeBonusFindText, "Woodcutting Bonus Find", axeBonusFindBody);
        Wire(axeStaminaEfficiencyText, "Woodcutting Stamina Efficiency", staminaEffBody);

        Wire(rodSpeedText, "Fishing Speed", speedBody);
        Wire(rodGritText, "Fishing Grit", gritBody);
        Wire(rodBonusFindText, "Fishing Bonus Find", bonusFindBody);
        Wire(rodStaminaEfficiencyText, "Fishing Stamina Efficiency", staminaEffBody);
    }

    private void EnsureParryStatTooltips()
    {
        SharedTooltipUI tip = ResolveAilmentSharedTooltip();
        if (!tip)
            return;

        void Wire(TMP_Text tmp, string key)
        {
            if (!tmp)
                return;
            if (!GameTooltipTexts.TryGetForUiElement(key, out string title, out string desc))
                return;

            tmp.raycastTarget = true;
            EquipmentAilmentLineTooltip ailmentOnly = tmp.GetComponent<EquipmentAilmentLineTooltip>();
            if (ailmentOnly)
                Destroy(ailmentOnly);

            UIHoverTooltip hover = tmp.GetComponent<UIHoverTooltip>();
            if (!hover)
                hover = tmp.gameObject.AddComponent<UIHoverTooltip>();
            hover.ConfigureForEquipmentStatsFixedCopy(tip, title, desc);
        }

        Wire(parryText, "ParryText");
        Wire(parryMitigationText, "ParryMitigationText");
    }
}