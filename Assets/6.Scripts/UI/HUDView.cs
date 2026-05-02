using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDView : MonoBehaviour
{
    [Header("HUD Left Auto Fade")]
    [SerializeField] private bool fadeWhenPlayerOverlaps = true;
    [SerializeField, Range(0.1f, 1f)] private float overlapAlpha = 0.5f;
    [SerializeField] private Camera overlapCamera;
    [SerializeField] private string overlapCameraName = "StripCamera";

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dpsText;
    [SerializeField] private TMP_Text actionText;

    [Header("HP")]
    [SerializeField] private Image hpFill;
    [Tooltip("Optional overlay; fill is current guard / max(natural cap, current guard).")]
    [SerializeField] private Image guardFill;
    [Tooltip("Optional. Shows current guard / natural cap (e.g. 40/40).")]
    [SerializeField] private TMP_Text guardValueText;
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

    private AbilityDatabase _abilityDatabase;
    private RectTransform _selfRect;
    private CanvasGroup _selfCanvasGroup;
    private PlayerController _player;
    private SpriteRenderer[] _playerRenderers = System.Array.Empty<SpriteRenderer>();
    private Canvas _parentCanvas;
    private Camera _hudRectEventCamera;

    private void Awake()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>();

        _abilityDatabase = AbilityDatabase.LoadDefault();

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

        _selfRect = transform as RectTransform;
        _parentCanvas = GetComponentInParent<Canvas>();
        _hudRectEventCamera = ResolveHudRectEventCamera();
        _selfCanvasGroup = GetComponent<CanvasGroup>();
        if (!_selfCanvasGroup)
            _selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void LateUpdate()
    {
        if (!fadeWhenPlayerOverlaps || _selfRect == null || _selfCanvasGroup == null)
            return;

        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        RefreshPlayerRenderersIfNeeded();
        bool overlaps = IsPlayerSpriteOverHudRect();
        _selfCanvasGroup.alpha = overlaps ? overlapAlpha : 1f;
    }

    private bool IsPlayerSpriteOverHudRect()
    {
        if (_selfRect == null || _player == null || _playerRenderers == null || _playerRenderers.Length == 0)
            return false;

        Camera cam = ResolveOverlapCamera();
        if (cam == null)
            return false;

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            SpriteRenderer sr = _playerRenderers[i];
            if (sr == null || !sr.enabled || sr.sprite == null || !sr.gameObject.activeInHierarchy)
                continue;

            if (IsSpriteRendererOverHudRect(sr, cam))
                return true;
        }

        return false;
    }

    private void RefreshPlayerRenderersIfNeeded()
    {
        if (_player == null)
            return;

        bool needsRefresh = _playerRenderers == null || _playerRenderers.Length == 0;
        if (!needsRefresh)
        {
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                if (_playerRenderers[i] == null)
                {
                    needsRefresh = true;
                    break;
                }
            }
        }

        if (needsRefresh)
            _playerRenderers = _player.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private Camera ResolveOverlapCamera()
    {
        if (overlapCamera != null)
            return overlapCamera;

        if (_parentCanvas != null && _parentCanvas.worldCamera != null)
            return _parentCanvas.worldCamera;

        if (!string.IsNullOrWhiteSpace(overlapCameraName))
        {
            GameObject named = GameObject.Find(overlapCameraName.Trim());
            if (named != null)
            {
                Camera c = named.GetComponent<Camera>();
                if (c != null)
                    return c;
            }
        }

        return Camera.main;
    }

    private Camera ResolveHudRectEventCamera()
    {
        if (_parentCanvas == null)
            return null;

        if (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (_parentCanvas.worldCamera != null)
            return _parentCanvas.worldCamera;

        return Camera.main;
    }

    private bool IsSpriteRendererOverHudRect(SpriteRenderer sr, Camera cam)
    {
        Bounds b = sr.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        Vector3[] points =
        {
            c,
            c + new Vector3(-e.x, -e.y, 0f),
            c + new Vector3(-e.x,  e.y, 0f),
            c + new Vector3( e.x, -e.y, 0f),
            c + new Vector3( e.x,  e.y, 0f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 screen = cam.WorldToScreenPoint(points[i]);
            if (screen.z <= 0f)
                continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(_selfRect, screen, _hudRectEventCamera))
                return true;
        }

        return false;
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

    /// <summary>
    /// Guard uses the same bar rect as HP; fill only occupies (naturalCap / maxHp) of the width at full guard,
    /// so a small flat cap does not read as a full-length bar.
    /// </summary>
    public void SetGuard(float current, float naturalCap, float maxHp)
    {
        if (guardFill)
        {
            if (naturalCap <= 0.0001f)
                guardFill.fillAmount = 0f;
            else
            {
                float hpD = Mathf.Max(1f, maxHp);
                float guardZone01 = Mathf.Clamp01(naturalCap / hpD);
                float guardFill01 = Mathf.Clamp01(current / naturalCap);
                guardFill.fillAmount = Mathf.Clamp01(guardFill01 * guardZone01);
            }
        }

        if (guardValueText)
        {
            if (current <= 0.0001f)
                guardValueText.gameObject.SetActive(false);
            else
            {
                guardValueText.gameObject.SetActive(true);
                guardValueText.text = $"{Mathf.RoundToInt(current)}";
            }
        }
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

    public void SetHealthBarVisible(bool visible)
    {
        if (hpFill) hpFill.gameObject.SetActive(visible);
        if (hpValueText) hpValueText.gameObject.SetActive(visible);
        if (guardFill) guardFill.gameObject.SetActive(visible);
        if (guardValueText) guardValueText.gameObject.SetActive(visible);
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
            SpawnDebuffIcon(bleedIcon, GameTooltipTexts.BleedTitle, 1, GameTooltipTexts.BleedTitle, GameTooltipTexts.BleedDescription);

        if (ailments.HasPoison)
            SpawnDebuffIcon(
                poisonIcon,
                GameTooltipTexts.PoisonTitle,
                ailments.PoisonStacks,
                GameTooltipTexts.PoisonTitle,
                GameTooltipTexts.FormatPoisonHudBody(ailments.PoisonStacks));

        if (ailments.HasBurn)
            SpawnDebuffIcon(
                burnIcon,
                GameTooltipTexts.BurnTitle,
                ailments.BurnStacks,
                GameTooltipTexts.BurnTitle,
                GameTooltipTexts.FormatBurnHudBody(ailments.BurnStacks, ailments.BurnHitsToExplode));

        if (ailments.HasChill)
            SpawnDebuffIcon(
                chillIcon,
                GameTooltipTexts.ChillTitle,
                ailments.ChillStacks,
                GameTooltipTexts.ChillTitle,
                $"Move speed reduced.\nStacks: {ailments.ChillStacks} ({ailments.ChillSlowPercent:0.#}% slow)\n\n" +
                GameTooltipTexts.ChillDescription);

        if (ailments.HasShock)
            SpawnDebuffIcon(shockIcon, GameTooltipTexts.ShockTitle, 1, GameTooltipTexts.ShockTitle, GameTooltipTexts.ShockDescription);
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
                GetBuffIconKey(buff),
                GetBuffValueLabel(buff),
                buff.RemainingSeconds,
                GetBuffTitle(buff),
                GetBuffBody(buff),
                buff.displayStacks,
                buff.displayStacks > 0);
        }
    }

    private Sprite GetBuffSpriteFromItem(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff && _abilityDatabase != null &&
            !string.IsNullOrWhiteSpace(buff.id))
        {
            AbilityDefinition adef = _abilityDatabase.Get(buff.id);
            if (adef != null && adef.icon != null)
                return adef.icon;
        }

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
        string body,
        int stacks = 0,
        bool showStacks = false)
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
                tooltipPreferredSide,
                stacks,
                showStacks);
        }

        spawnedBuffIcons.Add(icon);
    }

    private static string GetBuffIconKey(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
            return string.IsNullOrWhiteSpace(buff.id) ? "HudAbilityBuff" : buff.id;

        return GetBuffIconNameConsumable(buff.type);
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

    private static string GetBuffIconNameConsumable(ConsumableEffectType type)
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
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
            return "";

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

            ConsumableEffectType.ManaRegenOverTime =>
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
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
        {
            if (_abilityDatabase != null && !string.IsNullOrWhiteSpace(buff.id))
            {
                AbilityDefinition def = _abilityDatabase.Get(buff.id);
                if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                    return def.displayName;
            }

            return string.IsNullOrWhiteSpace(buff.id) ? "Ability" : buff.id;
        }

        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => "Physical Damage Boost",
            ConsumableEffectType.MagicDamageBoost => "Magic Damage Boost",
            ConsumableEffectType.AttackSpeed => "Attack Speed Boost",
            ConsumableEffectType.HealOverTime => "Regeneration",
            ConsumableEffectType.ManaRegenOverTime => "Mana Regeneration",
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
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
        {
            string core = "Temporary ability effect.";
            if (_abilityDatabase != null && !string.IsNullOrWhiteSpace(buff.id))
            {
                AbilityDefinition def = _abilityDatabase.Get(buff.id);
                if (def != null && !string.IsNullOrWhiteSpace(def.description))
                    core = def.description;
            }

            if (buff.displayStacks > 0)
                core += $"\n\nSwing charges: {buff.displayStacks}";
            return core;
        }

        float pct = buff.magnitude * 100f;

        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => $"+{pct:0.#}% physical damage",
            ConsumableEffectType.MagicDamageBoost => $"+{pct:0.#}% magic damage",
            ConsumableEffectType.AttackSpeed => $"+{pct:0.#}% attack speed",

            ConsumableEffectType.HealOverTime =>
                $"Heals {(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#} HP per second",

            ConsumableEffectType.ManaRegenOverTime =>
                $"Restores {(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#} mana per second",

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