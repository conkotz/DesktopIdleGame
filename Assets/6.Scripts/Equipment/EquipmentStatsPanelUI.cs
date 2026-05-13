using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class EquipmentStatsPanelUI : MonoBehaviour
{
    private bool _refreshQueued;
    private int _gatheringToolLiveStamp = int.MinValue;
    private static readonly Color BleedAilmentColor = new Color(0.996f, 0.361f, 0.361f);
    private static readonly Color PoisonAilmentColor = new Color(0.298f, 0.686f, 0.314f);
    private static readonly Color BurnAilmentColor = new Color(1f, 0.478f, 0.137f);
    private static readonly Color ShockAilmentColor = new Color(0.945f, 0.831f, 0.204f);
    private static readonly Color ChillAilmentColor = new Color(0.384f, 0.773f, 0.996f);
    /// <summary>Dimmed ailment block when apply chance is 0% (matches prior elemental inactive styling).</summary>
    private static readonly Color AilmentInactiveGrey = new Color(0.48f, 0.52f, 0.5f);

    [Header("Refs")]
    [SerializeField] private CharacterStats stats;
    [SerializeField] private SharedTooltipUI ailmentSharedTooltip;
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ToolbeltManager toolbelt;
    [SerializeField] private PlayerController player;
    [SerializeField] private TMP_Text statsHeaderText;

    // -------------------------
    // Defensive
    // -------------------------
    [Header("Defensive Text")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text armorText;
    [SerializeField] private TMP_Text mrText;
    [SerializeField] private TMP_Text corruptionResistText;
    [SerializeField] private TMP_Text blockText;
    [SerializeField] private TMP_Text guardFlatText;
    [SerializeField] private TMP_Text maxGuardPercentText;

    [Header("Defensive (NEW)")]
    [SerializeField] private TMP_Text moveSpeedText;
    [SerializeField] private TMP_Text lifeRegenText;
    [SerializeField] private TMP_Text energyRegenText;
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
    [SerializeField] private TMP_Text lifeStealText;

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
        if (!player) player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        // Prefer components from the player object so this panel never binds to enemy/NPC stats.
        if (player)
        {
            if (!stats) stats = player.GetComponent<CharacterStats>();
            if (!equipment) equipment = player.GetComponent<EquipmentManager>();
            if (!inventory) inventory = player.GetComponent<Inventory>();
            if (!toolbelt) toolbelt = player.GetComponent<ToolbeltManager>();
        }

        // Fallbacks (kept for safety in unusual setup scenes).
        if (!stats) stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
        if (!equipment) equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!toolbelt) toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
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
            stats.OnStatsChanged += HandleStatsChanged;

        Refresh();
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
            stats.OnStatsChanged -= HandleStatsChanged;
    }

    private void HandleRefresh(string _) => QueueRefresh();
    private void HandleUISlotChanged(EquipmentUISlotType _, string __) => QueueRefresh();
    private void HandleToolChanged(int _, string __) => QueueRefresh();
    private void HandleActiveSetChanged(int _) => QueueRefresh();

    private static string FormatSignedPercentFrom01(float value01)
    {
        float pct = value01 * 100f;
        return $"{pct:+0.#;-0.#;0}%";
    }

    private static string FormatSignedPercentPoints(float percentPoints)
    {
        return $"{percentPoints:+0.#;-0.#;0}%";
    }

    private void HandleStatsChanged()
    {
        QueueRefresh();
    }

    private void LateUpdate()
    {
        if (_refreshQueued)
        {
            _refreshQueued = false;
            Refresh();
        }

        PollGatheringToolsLiveRefresh();
    }

    private void QueueRefresh()
    {
        _refreshQueued = true;
    }

    public void Refresh()
    {
        if (!stats) return;

        EnsureGuardStatTextRefs();

        // -------------------------
        // Defensive
        // -------------------------
        if (hpText) hpText.text = $"Max HP: {stats.MaxHP}";
        if (energyText) energyText.text = $"Energy: {stats.MaxEnergy}";
        if (armorText) armorText.text = $"Armour: {stats.Armor} ({stats.PhysicalReductionFromArmorPercent:0.#}% Phys DR)";
        if (mrText) mrText.text = $"Magic Res: {stats.MagicResist} ({stats.MagicReductionFromMrPercent:0.#}% Mag DR)";
        if (corruptionResistText)
            corruptionResistText.text = $"Corr Res: {stats.CorruptionResist} ({stats.CorruptionReductionFromResistPercent:0.#}% Corr DR)";
        if (blockText) blockText.text = $"Phys Block: {stats.PhysBlockChancePercent:0.#}%";

        if (guardFlatText)
            guardFlatText.text = $"Guard (flat): {stats.GearFlatGuardSum}";
        if (maxGuardPercentText)
        {
            float pctPts = stats.GearMaxGuardPercentSum * 100f;
            maxGuardPercentText.text =
                $"Max Guard: +{pctPts:0.#}% (Guard ceiling {stats.NaturalGuardHpCeilingFromGear:0.#})";
        }

        if (moveSpeedText)
            moveSpeedText.text = $"Move Speed: {FormatSignedPercentFrom01(stats.MoveSpeedBonusPercent)}";

        if (lifeRegenText) lifeRegenText.text = $"Life Regen: {stats.LifeRegenPerSecond:0.##}/s";
        if (energyRegenText) energyRegenText.text = $"Energy Regen: {stats.EnergyRegenPerSecond:0.##}/s";
        if (abilityPowerText)
        {
            float ap = stats.AbilityPower;
            float abilityPotionPct = stats.AbilityDamageBoostConsumablePercentPoints;
            abilityPowerText.text = abilityPotionPct > 0.001f
                ? $"Ability Power: {ap:0.##} (abilities {abilityPotionPct:+0.#;-0.#;0}%)"
                : $"Ability Power: {ap:0.##}";
        }
        if (statsHeaderText) statsHeaderText.text = $"Stats (CP: {stats.CombatPowerRounded})";

        if (offenceGlobalBonusesHeaderText)
            offenceGlobalBonusesHeaderText.text = "Global bonuses";
        if (offenceStyleBonusesHeaderText)
            offenceStyleBonusesHeaderText.text = "Conditional bonuses";

        // -------------------------
        // Offensive
        // -------------------------
        if (damageText)
            damageText.text = $"Damage: {stats.MinDamage}-{stats.MaxDamage}";

        if (damageSplitText)
        {
            SplitDamage min = stats.MinSplitDamage;
            SplitDamage max = stats.MaxSplitDamage;

            bool hasPhys = max.physical > 0f;
            bool hasMag = max.magic > 0f;
            bool hasCorruption = max.corruptionDamage > 0f;

            bool hasAnyDamage = stats.MaxDamage > 0 || stats.MinDamage > 0;

            int types =
                (hasPhys ? 1 : 0) +
                (hasMag ? 1 : 0) +
                (hasCorruption ? 1 : 0);

            string typeLabel;

            if (!hasAnyDamage)
            {
                typeLabel = "-";
            }
            else if (types == 1)
            {
                if (hasPhys) typeLabel = "Physical";
                else if (hasMag) typeLabel = "Magic";
                else typeLabel = "Corruption";
            }
            else if (types == 2)
            {
                if (hasPhys && hasMag) typeLabel = "Physical + Magic";
                else if (hasPhys && hasCorruption) typeLabel = "Physical + Corruption";
                else typeLabel = "Magic + Corruption";
            }
            else
            {
                typeLabel = "Hybrid";
            }

            // If current attack skill is magic, show selected magic subtype in the type line.
            if (hasAnyDamage && stats.CurrentAttackSkill == AttackSkill.Magic)
            {
                string magicTypeLabel = GetCurrentMagicTypeLabel();
                if (!string.IsNullOrWhiteSpace(magicTypeLabel) && typeLabel.Contains("Magic"))
                    typeLabel = typeLabel.Replace("Magic", $"Magic ({magicTypeLabel})");
            }

            string split = "";

            if (hasPhys)
                split += $"P {Mathf.RoundToInt(min.physical)}-{Mathf.RoundToInt(max.physical)}";

            if (hasMag)
            {
                if (!string.IsNullOrEmpty(split)) split += " | ";
                split += $"M {Mathf.RoundToInt(min.magic)}-{Mathf.RoundToInt(max.magic)}";
            }

            if (hasCorruption)
            {
                if (!string.IsNullOrEmpty(split)) split += " | ";
                split += $"C {Mathf.RoundToInt(min.corruptionDamage)}-{Mathf.RoundToInt(max.corruptionDamage)}";
            }

            string colouredTypeLabel = hasAnyDamage
                ? BuildColouredTypeLabel(typeLabel, hasPhys, hasMag, hasCorruption)
                : typeLabel;

            damageSplitText.text = $"Type: {colouredTypeLabel}\nSplit: {split}";
        }

        if (atkSpeedText) atkSpeedText.text = $"Attack Speed: {stats.AttacksPerSecond:0.00}/s";
        if (rangeText) rangeText.text = $"Range: {stats.Range:0.##}";
        if (critChanceText) critChanceText.text = $"Crit: {stats.StatsPanelCritChancePercent:0.#}%";

        if (critDamageText)
        {
            float critBonusPct = stats.HasCrittableDirectDamage ? (stats.CritMultiplier - 1f) * 100f : 0f;
            critDamageText.text = $"Crit Damage: {critBonusPct:+0.#;-0.#;0}%";
        }

        if (lifeStealText)
        {
            float lsPct = Mathf.Clamp01(stats.LifeSteal) * 100f;
            lifeStealText.text = $"Life Steal: {lsPct:0.#}% of damage";
        }

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
        EnsureOffenceBonusLineTooltips();
        BindAilmentLineTooltips();

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

        EnsureToolStatLineTooltips();
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

        float baseAxeSpeed = stats.AxeSpeedMult;
        if (axeSpeedText)
        {
            float displayAxeSpeed = baseAxeSpeed;
            if (player != null &&
                player.TryGetWoodcuttingLiveBuffInfo(out float frenSpd, out float ffSpd, out _, out float majSpd, out _) &&
                (frenSpd > 0f || ffSpd > 0f || majSpd > 0f))
                displayAxeSpeed = baseAxeSpeed * (1f + frenSpd + ffSpd + majSpd);

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
        if (axeBonusFindText) axeBonusFindText.text = $"Bonus Find: +{stats.AxeBonusFindChance * 100f:0.#}%";

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

    private string GetCurrentMagicTypeLabel()
    {
        if (stats == null)
            return "";
        if (stats.CurrentAttackSkill != AttackSkill.Magic)
            return "";

        return stats.CurrentMagicAttackType.ToString();
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

            switch (tmp.gameObject.name)
            {
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
                case "MinionAttackSpeedText":
                case "MinionCritChanceText":
                case "MinionMaxLifeText":
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
            bleedChanceLineText.text = $"Bleed Chance: {stats.BleedChancePercent:0.#}%";
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
            poisonDurationLineText.text = $"Poison Duration: {stats.PoisonDuration:0.#}s";
        if (poisonMaxStacksLineText)
            poisonMaxStacksLineText.text = $"Poison Max Stacks: {stats.PoisonMaxStacks}";

        if (burnChanceLineText)
            burnChanceLineText.text = $"Burn Chance: {stats.BurnApplyChance * 100f:0.#}%";
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

        bool bleedApplies = stats.BleedChance > 0f;
        bool poisonApplies = stats.PoisonChance > 0f;
        bool burnApplies = stats.BurnApplyChance > 0f;
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

    private static string BuildColouredTypeLabel(string plainTypeLabel, bool hasPhys, bool hasMag, bool hasCorruption)
    {
        const string phys = "#FF5C5C";
        const string mag = "#4DB8FF";
        const string corr = "#7040C0";

        if (hasPhys || hasMag || hasCorruption)
        {
            string result = "";
            if (hasPhys)
                result += $"<color={phys}>Physical</color>";

            if (hasMag)
            {
                if (!string.IsNullOrEmpty(result)) result += " + ";
                result += $"<color={mag}>Magic</color>";
            }

            if (hasCorruption)
            {
                if (!string.IsNullOrEmpty(result)) result += " + ";
                result += $"<color={corr}>Corruption</color>";
            }

            return result;
        }

        // Fallback for unexpected/empty cases.
        return plainTypeLabel;
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
}