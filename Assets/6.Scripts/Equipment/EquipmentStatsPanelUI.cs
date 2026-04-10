using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class EquipmentStatsPanelUI : MonoBehaviour
{
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

    [Header("Global bonuses (gear + supports)")]
    [SerializeField] private TMP_Text globalPhysicalAllText;
    [SerializeField] private TMP_Text globalMagicAllText;
    [SerializeField] private TMP_Text globalCorruptionAllText;
    [FormerlySerializedAs("conditionalFireText")]
    [SerializeField] private TMP_Text globalFireBonusText;
    [FormerlySerializedAs("conditionalIceText")]
    [SerializeField] private TMP_Text globalIceBonusText;
    [FormerlySerializedAs("conditionalLightningText")]
    [SerializeField] private TMP_Text globalLightningBonusText;

    [Header("Conditional bonuses (gear + tree / passives where noted)")]
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
        }

        if (toolbelt != null)
            toolbelt.OnToolSlotChanged -= HandleToolChanged;

        if (stats != null)
            stats.OnStatsChanged -= HandleStatsChanged;
    }

    private void HandleRefresh(string _) => Refresh();
    private void HandleUISlotChanged(EquipmentUISlotType _, string __) => Refresh();
    private void HandleToolChanged(int _, string __) => Refresh();

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
        Refresh();
    }

    public void Refresh()
    {
        if (!stats) return;

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

        if (moveSpeedText)
            moveSpeedText.text = $"Move Speed: {FormatSignedPercentFrom01(stats.MoveSpeedBonusPercent)}";

        if (lifeRegenText) lifeRegenText.text = $"Life Regen: {stats.LifeRegenPerSecond:0.##}/s";
        if (energyRegenText) energyRegenText.text = $"Energy Regen: {stats.EnergyRegenPerSecond:0.##}/s";
        if (abilityPowerText) abilityPowerText.text = $"Ability Power: {stats.AbilityPower:0.##}";
        if (statsHeaderText) statsHeaderText.text = $"Stats (CP: {stats.CombatPowerRounded})";

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

        if (globalPhysicalAllText)
            globalPhysicalAllText.text = $"Physical (All): {FormatSignedPercentPoints(stats.GlobalPhysicalDamageBonusPercentPoints)}";
        if (globalMagicAllText)
            globalMagicAllText.text = $"Magic (All): {FormatSignedPercentPoints(stats.GlobalMagicDamageBonusPercentPoints)}";
        if (globalCorruptionAllText)
            globalCorruptionAllText.text = $"Corruption (All): {FormatSignedPercentPoints(stats.GlobalCorruptionDamageBonusPercentPoints)}";
        if (globalFireBonusText)
            globalFireBonusText.text = $"Fire: {FormatSignedPercentPoints(stats.FireSkillDamageTotalScalingPercentPoints)}";
        if (globalIceBonusText)
            globalIceBonusText.text = $"Ice: {FormatSignedPercentPoints(stats.IceSkillDamageTotalScalingPercentPoints)}";
        if (globalLightningBonusText)
            globalLightningBonusText.text = $"Lightning: {FormatSignedPercentPoints(stats.LightningSkillDamageTotalScalingPercentPoints)}";

        if (meleeDamageBonusText)
            meleeDamageBonusText.text = $"Melee Damage: {FormatSignedPercentPoints(stats.MeleePhysicalConditionalBonusPercentPoints)}";
        if (rangedDamageBonusText)
            rangedDamageBonusText.text = $"Ranged Damage: {FormatSignedPercentPoints(stats.RangedPhysicalDamageBonusPercentPoints)}";

        PopulateDetailedAilmentLines();
        BindAilmentLineTooltips();

        // -------------------------
        // DPS
        // -------------------------
        if (dpsText)
        {
            float sheetDps = stats.DPS;
            float weaponDps = stats.WeaponDpsComponent;
            float ailmentDps = stats.AilmentDpsComponent;
            float abilityDps = AbilityCombatPower.EstimateTotalSlottedAbilityDps(stats);
            float totalWithAbilities = sheetDps + Mathf.Max(0f, abilityDps);

            if (abilityDps > 0.01f && ailmentDps > 0.01f)
                dpsText.text = $"DPS: {totalWithAbilities:0.#} ({weaponDps:0.#} weapon, {ailmentDps:0.#} ailment, {abilityDps:0.#} ability)";
            else if (abilityDps > 0.01f)
                dpsText.text = $"DPS: {totalWithAbilities:0.#} ({weaponDps:0.#} weapon, {abilityDps:0.#} ability)";
            else if (ailmentDps > 0.01f)
                dpsText.text = $"DPS: {sheetDps:0.#} ({weaponDps:0.#} weapon, {ailmentDps:0.#} ailment)";
            else
                dpsText.text = $"DPS: {sheetDps:0.##} ({weaponDps:0.##} weapon)";
        }

        // -------------------------
        // Tools
        // -------------------------
        if (pickaxeTitleText) pickaxeTitleText.text = "Pickaxe";
        if (pickaxeSpeedText) pickaxeSpeedText.text = $"Mining Speed: {stats.PickaxeSpeedMult:0.##}x";
        if (pickaxeGritText) pickaxeGritText.text = $"Mining Grit: {stats.PickaxeGrit * 100f:0.#}%";
        if (pickaxeBonusFindText) pickaxeBonusFindText.text = $"Bonus Find: +{stats.PickaxeBonusFindChance * 100f:0.#}%";
        if (pickaxeStaminaEfficiencyText) pickaxeStaminaEfficiencyText.text = $"Stamina Eff: +{stats.PickaxeStaminaEfficiency * 100f:0.#}%";

        if (axeTitleText) axeTitleText.text = "Axe";
        if (axeSpeedText) axeSpeedText.text = $"Woodcut Speed: {stats.AxeSpeedMult:0.##}x";
        if (axeGritText) axeGritText.text = $"Woodcut Grit: {stats.AxeGrit * 100f:0.#}%";
        if (axeBonusFindText) axeBonusFindText.text = $"Bonus Find: +{stats.AxeBonusFindChance * 100f:0.#}%";
        if (axeStaminaEfficiencyText) axeStaminaEfficiencyText.text = $"Stamina Eff: +{stats.AxeStaminaEfficiency * 100f:0.#}%";

        if (rodTitleText) rodTitleText.text = "Rod";
        if (rodSpeedText) rodSpeedText.text = $"Fishing Speed: {stats.RodSpeedMult:0.##}x";
        if (rodGritText) rodGritText.text = $"Fishing Grit: {stats.RodGrit * 100f:0.#}%";
        if (rodBonusFindText) rodBonusFindText.text = $"Bonus Find: +{stats.RodBonusFindChance * 100f:0.#}%";
        if (rodStaminaEfficiencyText) rodStaminaEfficiencyText.text = $"Stamina Eff: +{stats.RodStaminaEfficiency * 100f:0.#}%";
    }

    private string GetCurrentMagicTypeLabel()
    {
        if (stats == null)
            return "";
        if (stats.CurrentAttackSkill != AttackSkill.Magic)
            return "";

        return stats.CurrentMagicAttackType.ToString();
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
}