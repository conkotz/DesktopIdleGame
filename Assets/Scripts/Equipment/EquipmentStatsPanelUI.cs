using TMPro;
using UnityEngine;

public class EquipmentStatsPanelUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterStats stats;
    [SerializeField] private EquipmentManager equipment;
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

    [Header("Ailments (Compact)")]
    [SerializeField] private TMP_Text bleedText;
    [SerializeField] private TMP_Text poisonText;

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
    [SerializeField] private TMP_Text pickaxePowerText;
    [SerializeField] private TMP_Text pickaxeSpeedText;

    [SerializeField] private TMP_Text axeTitleText;
    [SerializeField] private TMP_Text axePowerText;
    [SerializeField] private TMP_Text axeSpeedText;

    [SerializeField] private TMP_Text rodTitleText;
    [SerializeField] private TMP_Text rodPowerText;
    [SerializeField] private TMP_Text rodSpeedText;

    private void Awake()
    {
        if (!player) player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        // Prefer components from the player object so this panel never binds to enemy/NPC stats.
        if (player)
        {
            if (!stats) stats = player.GetComponent<CharacterStats>();
            if (!equipment) equipment = player.GetComponent<EquipmentManager>();
            if (!toolbelt) toolbelt = player.GetComponent<ToolbeltManager>();
        }

        // Fallbacks (kept for safety in unusual setup scenes).
        if (!stats) stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
        if (!equipment) equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
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
        if (mrText) mrText.text = $"Magic Res: {stats.MagicResist} ({stats.MagicalReductionFromMrPercent:0.#}% Mag DR)";
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
            bool hasMag = max.magical > 0f;
            bool hasTrue = max.trueDamage > 0f;

            int types =
                (hasPhys ? 1 : 0) +
                (hasMag ? 1 : 0) +
                (hasTrue ? 1 : 0);

            string typeLabel;

            if (types == 1)
            {
                if (hasPhys) typeLabel = "Physical";
                else if (hasMag) typeLabel = "Magical";
                else typeLabel = "True";
            }
            else if (types == 2)
            {
                if (hasPhys && hasMag) typeLabel = "Physical + Magical";
                else if (hasPhys && hasTrue) typeLabel = "Physical + True";
                else typeLabel = "Magical + True";
            }
            else
            {
                typeLabel = "Hybrid";
            }

            string split = "";

            if (hasPhys)
                split += $"P {Mathf.RoundToInt(min.physical)}-{Mathf.RoundToInt(max.physical)}";

            if (hasMag)
            {
                if (!string.IsNullOrEmpty(split)) split += " | ";
                split += $"M {Mathf.RoundToInt(min.magical)}-{Mathf.RoundToInt(max.magical)}";
            }

            if (hasTrue)
            {
                if (!string.IsNullOrEmpty(split)) split += " | ";
                split += $"T {Mathf.RoundToInt(min.trueDamage)}-{Mathf.RoundToInt(max.trueDamage)}";
            }

            damageSplitText.text = $"Type: {typeLabel}\nSplit: {split}";
        }

        if (atkSpeedText) atkSpeedText.text = $"Attack Speed: {stats.AttacksPerSecond:0.00}/s";
        if (rangeText) rangeText.text = $"Range: {stats.Range:0.##}";
        if (critChanceText) critChanceText.text = $"Crit: {stats.CritChancePercent:0.#}%";

        if (critDamageText)
        {
            float critBonusPct = (stats.CritMultiplier - 1f) * 100f;
            critDamageText.text = $"Crit Damage: {critBonusPct:+0.#;-0.#;0}%";
        }

        if (lifeStealText)
        {
            float lsPct = Mathf.Clamp01(stats.LifeSteal) * 100f;
            lifeStealText.text = $"Life Steal: {lsPct:0.#}% of damage";
        }

        // -------------------------
        // Ailments (Compact)
        // -------------------------
        if (bleedText)
        {
            bleedText.text = (stats.BleedChance > 0f && stats.BleedDPS > 0f)
                ? $"Bleed: {stats.BleedChancePercent:0.#}% | {stats.BleedMultiplier * 100f:+0.#;-0.#;0}% | {stats.BleedDuration:0.#}s"
                : "Bleed: None";
        }

        if (poisonText)
        {
            poisonText.text = stats.PoisonChance > 0f
                ? $"Poison: {stats.PoisonChancePercent:0.#}% | {stats.PoisonMultiplier * 100f:+0.#;-0.#;0}% | {stats.PoisonDuration:0.#}s | {stats.PoisonMaxStacks} stk"
                : "Poison: None";
        }

        // -------------------------
        // DPS
        // -------------------------
        if (dpsText)
        {
            float weaponDps = stats.DPS;

            float ailmentDps = 0f;

            if (stats.BleedChance > 0f)
                ailmentDps += stats.BleedDPS;

            if (stats.PoisonChance > 0f)
                ailmentDps += stats.PoisonMaxDPS;

            float totalDps = weaponDps + ailmentDps;

            if (ailmentDps > 0.01f)
                dpsText.text = $"DPS: {totalDps:0.#} (wep:{weaponDps:0.#}, ailment:{ailmentDps:0.#})";
            else
                dpsText.text = $"DPS: {totalDps:0.##} (wep:{weaponDps:0.##})";
        }

        // -------------------------
        // Tools
        // -------------------------
        if (pickaxeTitleText) pickaxeTitleText.text = "Pickaxe";
        if (pickaxePowerText) pickaxePowerText.text = $"Mining Power: {stats.PickaxePower}";
        if (pickaxeSpeedText) pickaxeSpeedText.text = $"Mining Speed: {stats.PickaxeSpeedMult:0.##}x";

        if (axeTitleText) axeTitleText.text = "Axe";
        if (axePowerText) axePowerText.text = $"Woodcut Power: {stats.AxePower}";
        if (axeSpeedText) axeSpeedText.text = $"Woodcut Speed: {stats.AxeSpeedMult:0.##}x";

        if (rodTitleText) rodTitleText.text = "Rod";
        if (rodPowerText) rodPowerText.text = $"Fishing Power: {stats.RodPower}";
        if (rodSpeedText) rodSpeedText.text = $"Fishing Speed: {stats.RodSpeedMult:0.##}x";
    }
}