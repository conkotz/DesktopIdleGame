using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dpsText;
    [SerializeField] private TMP_Text actionText;

    [Header("HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpValueText;

    [Header("Energy")]
    [SerializeField] private Image energyFill;
    [SerializeField] private TMP_Text energyValueText;

    [Header("Mana")]
    [SerializeField] private Image manaFill;
    [SerializeField] private TMP_Text manaValueText;

    [Header("Attack Delay")]
    [SerializeField] private Image attackDelayFill;
    [SerializeField] private TMP_Text attackDelayValueText;

    [Header("Gather Debuff")]
    [SerializeField] private GameObject gatherDebuffRoot;
    [SerializeField] private TMP_Text gatherDebuffText;

    [Header("Ailment Debuffs")]
    [SerializeField] private Transform debuffContainer;
    [SerializeField] private GameObject debuffIconPrefab;

    [Header("Buffs")]
    [SerializeField] private Transform buffContainer;
    [SerializeField] private GameObject buffIconPrefab;

    [Header("Debuff Sprites")]
    [SerializeField] private Sprite bleedIcon;
    [SerializeField] private Sprite poisonIcon;
    [SerializeField] private Sprite burnIcon;
    [SerializeField] private Sprite chillIcon;
    [SerializeField] private Sprite shockIcon;

    [Header("Buff Sprites")]
    [SerializeField] private Sprite physicalDamageBuffIcon;
    [SerializeField] private Sprite magicDamageBuffIcon;
    [SerializeField] private Sprite attackSpeedBuffIcon;
    [SerializeField] private Sprite defaultBuffIcon;

    [Header("Shared Tooltip")]
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide tooltipPreferredSide = FlipInsideBounds.PreferredSide.Right;
    [SerializeField] private SharedTooltipUI hudTooltip;   

    private readonly List<GameObject> spawnedDebuffIcons = new();
    private readonly List<GameObject> spawnedBuffIcons = new();

    [SerializeField] private Inventory inventory;


    private void Awake()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>();

        if (!hudTooltip)
            hudTooltip = GameObject.Find("HUDToolTipInfoPanel")?.GetComponent<SharedTooltipUI>();

        if (!hudTooltip)
            hudTooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);

        if (!tooltipMeasureRect || !tooltipHeightRect)
        {
            RectTransform selfRect = transform as RectTransform;
            if (!tooltipMeasureRect) tooltipMeasureRect = selfRect;
            if (!tooltipHeightRect) tooltipHeightRect = selfRect;
        }
    }

    public void SetNameAndCombatPower(string displayName, float combatPower)
    {
        if (nameText)
            nameText.text = $"{displayName}  <size=65%>CP:{Mathf.RoundToInt(combatPower)}</size>";
    }

    public void SetAction(string action)
    {
        if (actionText) actionText.text = action ?? "";
    }

    public void SetDps(float dps)
    {
        if (!dpsText)
            return;

        if (dps <= 0f)
        {
            dpsText.text = "0 DPS";
            return;
        }

        dpsText.text = $"{dps:0.#} DPS";
    }

    public void SetHP(float current, float max)
    {
        if (hpFill) hpFill.fillAmount = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        if (hpValueText) hpValueText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    public void SetEnergy(float current, float max)
    {
        if (energyFill) energyFill.fillAmount = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        if (energyValueText) energyValueText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    public void SetMana(float current, float max)
    {
        if (manaFill) manaFill.fillAmount = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        if (manaValueText) manaValueText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    public void SetAttackDelay(float normalizedCycle, float attacksPerSecond)
    {
        if (attackDelayFill)
            attackDelayFill.fillAmount = Mathf.Clamp01(normalizedCycle);

        if (attackDelayValueText)
            attackDelayValueText.text = attacksPerSecond > 0f ? $"{attacksPerSecond:0.##} APS" : "0 APS";
    }

    public void SetGatherDebuff(bool active, float speedMultiplier)
    {
        if (!gatherDebuffRoot) return;

        gatherDebuffRoot.SetActive(active);

        if (!active) return;

        if (gatherDebuffText)
        {
            int percent = Mathf.RoundToInt((1f - speedMultiplier) * 100f);
            gatherDebuffText.text = $"-{percent}% gather speed";
        }
    }

    public void RefreshDebuffs(AilmentController ailments)
    {
        ClearDebuffs();

        if (ailments == null || debuffContainer == null || debuffIconPrefab == null)
            return;

        if (ailments.HasBleed)
            SpawnDebuffIcon(bleedIcon, "Bleed", 1, "Bleed", "Taking physical damage over time.");

        if (ailments.HasPoison)
            SpawnDebuffIcon(poisonIcon, "Poison", ailments.PoisonStacks, "Poison", $"Taking poison damage over time.\nStacks: {ailments.PoisonStacks}");

        if (ailments.HasBurn)
            SpawnDebuffIcon(burnIcon, "Burn", 1, "Burn", "Taking fire damage over time.");

        if (ailments.HasChill)
            SpawnDebuffIcon(chillIcon, "Chill", 1, "Chill", "Movement and/or attack speed reduced.");

        if (ailments.HasShock)
            SpawnDebuffIcon(shockIcon, "Shock", 1, "Shock", "Electrified and vulnerable to follow-up effects.");
    }

    public void RefreshBuffs(PlayerBuffController buffController)
    {
        ClearBuffs();

        if (buffController == null || buffContainer == null || buffIconPrefab == null)
            return;

        var buffs = buffController.ActiveBuffs;
        if (buffs == null || buffs.Count == 0)
            return;

        for (int i = 0; i < buffs.Count; i++)
        {
            var buff = buffs[i];
            if (buff == null)
                continue;

            SpawnBuffIcon(
            GetBuffSpriteFromItem(buff),
            GetBuffIconName(buff.type),
            GetBuffValueLabel(buff),
            buff.RemainingSeconds,
            GetBuffTitle(buff),
            GetBuffBody(buff)
);
        }
    }

    private Sprite GetBuffSpriteFromItem(PlayerBuffController.ActiveBuff buff)
    {
        if (inventory != null && !string.IsNullOrWhiteSpace(buff.id))
        {
            var def = inventory.GetItemDef(buff.id);
            if (def != null && def.icon != null)
                return def.icon;
        }

        return defaultBuffIcon;
    }

    public void UpdateBuffTimers(PlayerBuffController buffController)
    {
        if (buffController == null)
            return;

        var buffs = buffController.ActiveBuffs;
        if (buffs == null)
            return;

        int count = Mathf.Min(buffs.Count, spawnedBuffIcons.Count);

        for (int i = 0; i < count; i++)
        {
            if (spawnedBuffIcons[i] == null || buffs[i] == null)
                continue;

            BuffIconUI iconUI = spawnedBuffIcons[i].GetComponent<BuffIconUI>();
            if (iconUI != null)
                iconUI.UpdateTimer(buffs[i].RemainingSeconds);
        }
    }

    public void ClearDebuffs()
    {
        for (int i = 0; i < spawnedDebuffIcons.Count; i++)
        {
            if (spawnedDebuffIcons[i] != null)
                Destroy(spawnedDebuffIcons[i]);
        }

        spawnedDebuffIcons.Clear();
    }

    public void ClearBuffs()
    {
        for (int i = 0; i < spawnedBuffIcons.Count; i++)
        {
            if (spawnedBuffIcons[i] != null)
                Destroy(spawnedBuffIcons[i]);
        }

        spawnedBuffIcons.Clear();
    }

    private void SpawnDebuffIcon(Sprite sprite, string iconName, int stacks, string title, string body)
    {
        if (sprite == null) return;

        GameObject icon = Instantiate(debuffIconPrefab, debuffContainer);
        icon.name = $"HUDDebuff_{iconName}";

        DebuffIconUI iconUI = icon.GetComponent<DebuffIconUI>();
        if (iconUI != null)
        {
            iconUI.SetData(
                sprite,
                stacks,
                true,
                title,
                body,
                hudTooltip,
                tooltipMeasureRect,
                tooltipHeightRect,
                tooltipPreferredSide);
        }

        spawnedDebuffIcons.Add(icon);
    }

    private void SpawnBuffIcon(
        Sprite sprite,
        string iconName,
        string valueLabel,
        float remainingSeconds,
        string title,
        string body)
    {
        if (sprite == null)
            sprite = defaultBuffIcon;

        if (sprite == null)
            return;

        GameObject icon = Instantiate(buffIconPrefab, buffContainer);
        icon.name = $"HUDBuff_{iconName}";

        BuffIconUI iconUI = icon.GetComponent<BuffIconUI>();
        if (iconUI != null)
        {
            iconUI.SetData(
                sprite,
                valueLabel,
                remainingSeconds,
                title,
                body,
                hudTooltip,
                tooltipMeasureRect,
                tooltipHeightRect,
                tooltipPreferredSide);
        }

        spawnedBuffIcons.Add(icon);
    }

    private Sprite GetBuffSprite(ConsumableEffectType type)
    {
        return type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => physicalDamageBuffIcon ? physicalDamageBuffIcon : defaultBuffIcon,
            ConsumableEffectType.MagicDamageBoost => magicDamageBuffIcon ? magicDamageBuffIcon : defaultBuffIcon,
            ConsumableEffectType.AttackSpeed => attackSpeedBuffIcon ? attackSpeedBuffIcon : defaultBuffIcon,
            _ => defaultBuffIcon
        };
    }

    private string GetBuffIconName(ConsumableEffectType type)
    {
        return type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => "PhysicalDamageBoost",
            ConsumableEffectType.MagicDamageBoost => "MagicDamageBoost",
            ConsumableEffectType.AttackSpeed => "AttackSpeed",
            _ => "Buff"
        };
    }

    private string GetBuffValueLabel(PlayerBuffController.ActiveBuff buff)
    {
        float pct = buff.magnitude * 100f;

        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => $"+{pct:0.#}%",
            ConsumableEffectType.MagicDamageBoost => $"+{pct:0.#}%",
            ConsumableEffectType.AttackSpeed => $"+{pct:0.#}%",
            ConsumableEffectType.MoveSpeed => $"+{pct:0.#}%",
            ConsumableEffectType.AbilityDamageBoost => $"+{pct:0.#}%",
            ConsumableEffectType.DamageReduction => $"-{pct:0.#}%",

            ConsumableEffectType.HealOverTime =>
              $"{(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#}/s",

            ConsumableEffectType.EnergyRegen =>
                $"{buff.magnitude:0.#}/s",

            ConsumableEffectType.ArmorBoost =>
                $"+{buff.magnitude:0}",

            ConsumableEffectType.MagicResistBoost =>
                $"+{buff.magnitude:0}",

            ConsumableEffectType.PoisonImmunity => "IMM",
            ConsumableEffectType.BleedImmunity => "IMM",

            _ => ""
        };
    }

    private string GetBuffTitle(PlayerBuffController.ActiveBuff buff)
    {
        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => "Physical Damage Boost",
            ConsumableEffectType.MagicDamageBoost => "Magic Damage Boost",
            ConsumableEffectType.AttackSpeed => "Attack Speed Boost",
            ConsumableEffectType.HealOverTime => "Regeneration",
            ConsumableEffectType.EnergyRegen => "Energy Regeneration",
            ConsumableEffectType.MoveSpeed => "Move Speed",
            ConsumableEffectType.AbilityDamageBoost => "Ability Power Boost",
            ConsumableEffectType.DefenseBoost => "Defence Boost",
            ConsumableEffectType.ArmorBoost => "Armour Boost",
            ConsumableEffectType.MagicResistBoost => "Magic Resist Boost",
            ConsumableEffectType.DamageReduction => "Damage Reduction",
            ConsumableEffectType.PoisonImmunity => "Poison Immunity",
            ConsumableEffectType.BleedImmunity => "Bleed Immunity",
            _ => "Buff"
        };
    }

    private string GetBuffBody(PlayerBuffController.ActiveBuff buff)
    {
        float pct = buff.magnitude * 100f;

        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => $"+{pct:0.#}% physical damage",
            ConsumableEffectType.MagicDamageBoost => $"+{pct:0.#}% magic damage",
            ConsumableEffectType.AttackSpeed => $"+{pct:0.#}% attack speed",

            ConsumableEffectType.HealOverTime =>
                $"Heals {(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#} HP per second",

            ConsumableEffectType.EnergyRegen =>
                $"+{buff.magnitude:0.#} energy per second",

            ConsumableEffectType.MoveSpeed =>
                $"+{pct:0.#}% movement speed",

            ConsumableEffectType.AbilityDamageBoost =>
                $"+{pct:0.#}% ability damage",

            ConsumableEffectType.DefenseBoost =>
                $"+{pct:0.#}% defence",

            ConsumableEffectType.ArmorBoost =>
                $"+{buff.magnitude:0} armour",

            ConsumableEffectType.MagicResistBoost =>
                $"+{buff.magnitude:0} magic resist",

            ConsumableEffectType.DamageReduction =>
                $"-{pct:0.#}% damage taken",

            ConsumableEffectType.PoisonImmunity =>
                "Immune to poison",

            ConsumableEffectType.BleedImmunity =>
                "Immune to bleed",

            _ => "Temporary buff"
        };
    }
}