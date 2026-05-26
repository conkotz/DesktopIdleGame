using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-contained controller for the buffs/debuffs panel that lives outside HUD_Left.
/// Spawns ailment debuff icons (bleed/poison/burn/chill/shock) and active buff icons
/// (consumable buffs + ability HUD buffs) into their own containers and keeps their
/// timers and tooltips in sync.
///
/// Attach this component to the panel GameObject and wire the container/prefab/sprite
/// references in the inspector. It auto-finds the player's AilmentController,
/// PlayerBuffController and Inventory on Awake.
/// </summary>
public class BuffsDebuffsPanel : MonoBehaviour
{
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

    [Header("Woodcutting — Flow State HUD")]
    [Tooltip("Icon for Lv15 Flow State while its bonus is active (no timer; cleared when Flow ends).")]
    [SerializeField] private Sprite woodcuttingFlowStateHudIcon;

    [Header("Fishing — Calm Waters (Lv15 Major) HUD")]
    [Tooltip("Icon for Fishing Lv15 Calm Waters stacks (shows stack count on the buff strip).")]
    [SerializeField] private Sprite fishingCalmWatersMajorHudIcon;

    [Header("Melee — Shadow Hunter (Lv20 Major) HUD")]
    [Tooltip("Icon for Predator's Instinct Shadow Hunter attack speed after crits.")]
    [SerializeField] private Sprite shadowHunterHudIcon;

    [Header("Melee — Battle Engine Overload (Lv30 Major) HUD")]
    [Tooltip("Icon for Battle Engine Overload stacks after ability casts.")]
    [SerializeField] private Sprite battleEngineOverloadHudIcon;

    [Header("Melee — Phoenix Soul Ashen Rebirth (Lv40) HUD")]
    [Tooltip("Icon for Ashen Rebirth damage immunity after proc.")]
    [SerializeField] private Sprite ashenRebirthHudIcon;

    [Header("Melee — Tactician Duality (Lv30 Major) HUD")]
    [Tooltip("Icon for Duality after swapping weapon sets (8 second window).")]
    [SerializeField] private Sprite tacticianDualityHudIcon;

    [Header("Movement — Sprint HUD")]
    [Tooltip("Icon while sprinting (hold sprint key while moving). Falls back to attack speed icon.")]
    [SerializeField] private Sprite sprintHudIcon;

    [Header("Shared Tooltip")]
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide tooltipPreferredSide = FlipInsideBounds.PreferredSide.Right;
    [SerializeField] private SharedTooltipUI panelTooltip;

    [Header("Refs (auto-resolved if blank)")]
    [SerializeField] private PlayerController player;
    [SerializeField] private AilmentController ailments;
    [SerializeField] private PlayerBuffController buffs;
    [SerializeField] private Inventory inventory;

    [Header("Refresh")]
    [Tooltip("How often (seconds) the buff icon timers tick down on the HUD. Display only — does not affect actual buff expiry.")]
    [SerializeField, Min(0.05f)] private float buffTimerRefreshInterval = 0.2f;

    private readonly List<GameObject> spawnedDebuffIcons = new();
    private readonly List<GameObject> spawnedBuffIcons = new();
    private readonly List<BuffIconUI> spawnedBuffIconUis = new();
    private readonly List<BuffIconVisualSnapshot> buffIconSnapshots = new();
    private AbilityDatabase _abilityDatabase;
    private float _nextBuffTimerRefreshAt;

    private struct BuffIconVisualSnapshot
    {
        public string key;
        public int displayStacks;
        public string valueLabel;
        public string title;
        public string body;
        public Sprite sprite;
        public float totalDurationSeconds;
        public bool persistActiveOverlay;
    }

    private void Awake()
    {
        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (!ailments && player)
            ailments = player.GetComponent<AilmentController>();
        if (!buffs && player)
            buffs = player.GetComponent<PlayerBuffController>();
        if (!inventory && player)
            inventory = player.GetComponent<Inventory>();

        if (!ailments)
            ailments = FindFirstObjectByType<AilmentController>(FindObjectsInactive.Include);
        if (!buffs)
            buffs = FindFirstObjectByType<PlayerBuffController>(FindObjectsInactive.Include);
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        _abilityDatabase = AbilityDatabase.LoadDefault();

        if (!panelTooltip)
            panelTooltip = GameObject.Find("HUDToolTipInfoPanel")?.GetComponent<SharedTooltipUI>();
        if (!panelTooltip)
            panelTooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);

        // Default tooltip rects to this panel if not provided.
        RectTransform selfRect = transform as RectTransform;
        if (!tooltipMeasureRect && selfRect) tooltipMeasureRect = selfRect;
        if (!tooltipHeightRect && selfRect) tooltipHeightRect = selfRect;
    }

    private void OnEnable()
    {
        if (ailments != null)
            ailments.OnAilmentsChanged += HandleAilmentsChanged;
        if (buffs != null)
            buffs.OnBuffsChanged += HandleBuffsChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (ailments != null)
            ailments.OnAilmentsChanged -= HandleAilmentsChanged;
        if (buffs != null)
            buffs.OnBuffsChanged -= HandleBuffsChanged;
    }

    private void Update()
    {
        if (buffs == null)
            return;

        if (Time.time < _nextBuffTimerRefreshAt)
            return;

        _nextBuffTimerRefreshAt = Time.time + Mathf.Max(0.05f, buffTimerRefreshInterval);
        UpdateBuffTimers();
    }

    private void HandleAilmentsChanged() => RefreshDebuffs();
    private void HandleBuffsChanged() => RefreshBuffs();

    public void RefreshAll()
    {
        RefreshDebuffs();
        RefreshBuffs();
    }

    public void RefreshDebuffs()
    {
        ClearDebuffs();

        if (ailments == null || debuffContainer == null || debuffIconPrefab == null)
            return;

        if (ailments.HasBleed)
            SpawnDebuffIcon(
                bleedIcon,
                GameTooltipTexts.BleedTitle,
                1,
                GameTooltipTexts.BleedTitle,
                BuildPlayerBleedBody(ailments.BleedDamagePerSecond));

        if (ailments.HasPoison)
            SpawnDebuffIcon(
                poisonIcon,
                GameTooltipTexts.PoisonTitle,
                ailments.PoisonStacks,
                GameTooltipTexts.PoisonTitle,
                BuildPlayerPoisonBody(ailments.PoisonDamagePerSecond));

        if (ailments.HasBurn)
            SpawnDebuffIcon(
                burnIcon,
                GameTooltipTexts.BurnTitle,
                ailments.BurnStacks,
                GameTooltipTexts.BurnTitle,
                BuildPlayerBurnBody(ailments.BurnDamagePerSecond));

        if (ailments.HasChill)
            SpawnDebuffIcon(
                chillIcon,
                GameTooltipTexts.ChillTitle,
                ailments.ChillStacks,
                GameTooltipTexts.ChillTitle,
                BuildPlayerChillBody(ailments.ChillSlowPercent));

        if (ailments.HasShock)
            SpawnDebuffIcon(
                shockIcon,
                GameTooltipTexts.ShockTitle,
                1,
                GameTooltipTexts.ShockTitle,
                BuildPlayerShockBody(ailments.ShockDamageTakenBonusPercent));
    }

    private static string BuildPlayerBleedBody(int damagePerSecond)
    {
        if (damagePerSecond > 0)
            return $"Taking physical damage over time.\nTaking {damagePerSecond} damage per second.";
        return "Taking physical damage over time.";
    }

    private static string BuildPlayerPoisonBody(int damagePerSecond)
    {
        if (damagePerSecond > 0)
            return $"Taking poison damage over time.\nTaking {damagePerSecond} damage per second.";
        return "Taking poison damage over time.";
    }

    private static string BuildPlayerBurnBody(int damagePerSecond)
    {
        if (damagePerSecond > 0)
            return $"Taking fire damage over time.\nTaking {damagePerSecond} damage per second.";
        return "Taking fire damage over time.";
    }

    private static string BuildPlayerChillBody(float slowPercent)
    {
        if (slowPercent > 0f)
            return $"You are slowed.\nMove speed reduced by {slowPercent:0.#}%.";
        return "You are slowed.";
    }

    private static string BuildPlayerShockBody(float damageTakenBonusPercent)
    {
        if (damageTakenBonusPercent > 0f)
            return $"You are shocked, taking extra damage.\n+{damageTakenBonusPercent:0.#}% damage taken.";
        return "You are shocked, taking extra damage.";
    }

    public void RefreshBuffs()
    {
        if (buffs == null || buffContainer == null || buffIconPrefab == null)
        {
            ClearBuffs();
            return;
        }

        IReadOnlyList<PlayerBuffController.ActiveBuff> activeBuffs = buffs.ActiveBuffs;
        if (activeBuffs == null || activeBuffs.Count == 0)
        {
            ClearBuffs();
            return;
        }

        while (spawnedBuffIcons.Count > activeBuffs.Count)
        {
            int last = spawnedBuffIcons.Count - 1;
            if (spawnedBuffIcons[last] != null)
                Destroy(spawnedBuffIcons[last]);
            spawnedBuffIcons.RemoveAt(last);
            if (spawnedBuffIconUis.Count > last)
                spawnedBuffIconUis.RemoveAt(last);
            if (buffIconSnapshots.Count > last)
                buffIconSnapshots.RemoveAt(last);
        }

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            PlayerBuffController.ActiveBuff buff = activeBuffs[i];
            if (buff == null)
                continue;

            BuffIconVisualSnapshot snapshot = BuildBuffIconSnapshot(buff);
            if (i >= spawnedBuffIcons.Count)
            {
                SpawnBuffIcon(snapshot, buff.RemainingSeconds);
                continue;
            }

            if (!BuffIconSnapshotEquals(buffIconSnapshots[i], snapshot))
                ApplyBuffIconSnapshot(i, snapshot, buff.RemainingSeconds);
        }
    }

    private BuffIconVisualSnapshot BuildBuffIconSnapshot(PlayerBuffController.ActiveBuff buff) =>
        new BuffIconVisualSnapshot
        {
            key = GetBuffIconKey(buff),
            displayStacks = buff.displayStacks,
            valueLabel = GetBuffValueLabel(buff),
            title = GetBuffTitle(buff),
            body = GetBuffBody(buff),
            sprite = GetBuffSpriteFromItem(buff),
            totalDurationSeconds = buff.duration,
            persistActiveOverlay = buff.hudPersistActiveOverlay
        };

    private static bool BuffIconSnapshotEquals(BuffIconVisualSnapshot a, BuffIconVisualSnapshot b) =>
        a.key == b.key &&
        a.displayStacks == b.displayStacks &&
        a.valueLabel == b.valueLabel &&
        a.title == b.title &&
        a.body == b.body &&
        a.sprite == b.sprite &&
        Mathf.Approximately(a.totalDurationSeconds, b.totalDurationSeconds) &&
        a.persistActiveOverlay == b.persistActiveOverlay;

    private void ApplyBuffIconSnapshot(int index, BuffIconVisualSnapshot snapshot, float remainingSeconds)
    {
        if (index < 0 || index >= spawnedBuffIconUis.Count)
            return;

        BuffIconUI iconUI = spawnedBuffIconUis[index];
        if (iconUI == null)
            return;

        iconUI.SetData(
            snapshot.sprite != null ? snapshot.sprite : defaultBuffIcon,
            snapshot.valueLabel,
            remainingSeconds,
            snapshot.title,
            snapshot.body,
            panelTooltip,
            tooltipMeasureRect,
            tooltipHeightRect,
            tooltipPreferredSide,
            snapshot.displayStacks,
            snapshot.displayStacks > 0,
            snapshot.totalDurationSeconds,
            snapshot.persistActiveOverlay);

        if (spawnedBuffIcons[index] != null)
            spawnedBuffIcons[index].name = $"HUDBuff_{snapshot.key}";

        buffIconSnapshots[index] = snapshot;
    }

    private void SpawnBuffIcon(BuffIconVisualSnapshot snapshot, float remainingSeconds)
    {
        if (snapshot.sprite == null && defaultBuffIcon == null)
            return;

        GameObject icon = Instantiate(buffIconPrefab, buffContainer);
        icon.name = $"HUDBuff_{snapshot.key}";

        BuffIconUI iconUI = icon.GetComponent<BuffIconUI>();
        if (iconUI != null)
        {
            iconUI.SetData(
                snapshot.sprite != null ? snapshot.sprite : defaultBuffIcon,
                snapshot.valueLabel,
                remainingSeconds,
                snapshot.title,
                snapshot.body,
                panelTooltip,
                tooltipMeasureRect,
                tooltipHeightRect,
                tooltipPreferredSide,
                snapshot.displayStacks,
                snapshot.displayStacks > 0,
                snapshot.totalDurationSeconds,
                snapshot.persistActiveOverlay);
        }

        spawnedBuffIcons.Add(icon);
        spawnedBuffIconUis.Add(iconUI);
        buffIconSnapshots.Add(snapshot);
    }

    private void UpdateBuffTimers()
    {
        if (buffs == null)
            return;

        IReadOnlyList<PlayerBuffController.ActiveBuff> activeBuffs = buffs.ActiveBuffs;
        if (activeBuffs == null)
            return;

        int count = Mathf.Min(activeBuffs.Count, spawnedBuffIconUis.Count);
        for (int i = 0; i < count; i++)
        {
            if (activeBuffs[i] == null)
                continue;

            BuffIconUI iconUI = spawnedBuffIconUis[i];
            if (iconUI != null)
                iconUI.UpdateTimer(activeBuffs[i].RemainingSeconds);
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
        spawnedBuffIconUis.Clear();
        buffIconSnapshots.Clear();
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
                panelTooltip,
                tooltipMeasureRect,
                tooltipHeightRect,
                tooltipPreferredSide);
        }

        spawnedDebuffIcons.Add(icon);
    }

    private Sprite GetBuffSpriteFromItem(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, PlayerController.WoodcuttingFlowStateHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (woodcuttingFlowStateHudIcon != null)
                return woodcuttingFlowStateHudIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, PlayerController.FishingCalmWatersMajorHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (fishingCalmWatersMajorHudIcon != null)
                return fishingCalmWatersMajorHudIcon;
            if (woodcuttingFlowStateHudIcon != null)
                return woodcuttingFlowStateHudIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (shadowHunterHudIcon != null)
                return shadowHunterHudIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (battleEngineOverloadHudIcon != null)
                return battleEngineOverloadHudIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, CharacterStats.PhoenixSoulAshenRebirthImmunityHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (ashenRebirthHudIcon != null)
                return ashenRebirthHudIcon;
            if (magicDamageBuffIcon != null)
                return magicDamageBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, CharacterStats.TacticianDualityHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (tacticianDualityHudIcon != null)
                return tacticianDualityHudIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (sprintHudIcon != null)
                return sprintHudIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, PlayerAbilityController.CrusaderStrikeFireBalanceHudBuffId, StringComparison.OrdinalIgnoreCase) &&
            _abilityDatabase != null)
        {
            AbilityDefinition crusader = _abilityDatabase.Get(AbilityCombatPower.CrusaderStrikeAbilityId);
            if (crusader != null)
            {
                Sprite spr = SkillsAbilityPresentationResolver.ResolveAbilityIcon(crusader);
                if (spr != null)
                    return spr;
            }
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff && _abilityDatabase != null &&
            !string.IsNullOrWhiteSpace(buff.id))
        {
            AbilityDefinition adef = _abilityDatabase.Get(buff.id);
            if (adef != null)
            {
                Sprite spr = SkillsAbilityPresentationResolver.ResolveAbilityIcon(adef);
                if (spr != null)
                    return spr;
            }
        }

        if (inventory != null && !string.IsNullOrWhiteSpace(buff.id))
        {
            ItemDefinition def = inventory.GetItemDef(buff.id);
            if (def != null && def.icon != null)
                return def.icon;
        }

        return GetBuffSprite(buff.type);
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

    private static string GetBuffIconKey(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
            return string.IsNullOrWhiteSpace(buff.id) ? "HudAbilityBuff" : buff.id;

        return GetBuffIconNameConsumable(buff.type);
    }

    private static string GetBuffIconNameConsumable(ConsumableEffectType type)
    {
        return type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => "PhysicalDamageBoost",
            ConsumableEffectType.MagicDamageBoost => "MagicDamageBoost",
            ConsumableEffectType.AttackSpeed => "AttackSpeed",
            ConsumableEffectType.FoodHealOverTime => "FoodRegen",
            ConsumableEffectType.FoodMoveSpeed => "FoodSwiftness",
            ConsumableEffectType.FoodOverheal => "FoodOverheal",
            ConsumableEffectType.FoodFocused => "FoodFocused",
            _ => "Buff"
        };
    }

    private string GetBuffValueLabel(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
        {
            if (string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "+10%";

            if (string.Equals(buff.id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.FormatOverloadValueLabel(buff.displayStacks);

            if (string.Equals(buff.id, CharacterStats.TacticianDualityHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "×3";

            if (string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase))
            {
                CharacterStats playerStats = player != null ? player.GetComponent<CharacterStats>() : null;
                float baseSpeed = playerStats != null ? playerStats.FinalMoveSpeed : 0f;
                return $"+{PlayerSprintInput.GetSprintBonusPercentOfBase(baseSpeed):0.#}%";
            }

            if (string.Equals(buff.id, PlayerAbilityController.CrusaderStrikeFireBalanceHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "+30%";

            return "";
        }

        float pct = buff.magnitude * 100f;

        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => $"+{pct:0.#}%",
            ConsumableEffectType.MagicDamageBoost => $"+{pct:0.#}%",
            ConsumableEffectType.AttackSpeed => $"+{pct:0.#}%",
            ConsumableEffectType.MoveSpeed => $"+{pct:0.#}%",
            ConsumableEffectType.FoodMoveSpeed => $"+{pct:0.#}%",
            ConsumableEffectType.AbilityDamageBoost => $"+{pct:0.#}%",
            ConsumableEffectType.DamageReduction => $"-{pct:0.#}%",

            ConsumableEffectType.HealOverTime =>
                $"{(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#}/s",

            ConsumableEffectType.FoodHealOverTime =>
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

            ConsumableEffectType.FoodOverheal => $"+{buff.magnitude:0}",
            ConsumableEffectType.FoodFocused => $"+{pct:0.#}%",

            _ => ""
        };
    }

    private string GetBuffTitle(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
        {
            if (string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "Shadow Hunter";

            if (string.Equals(buff.id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.BattleEngineOverloadTitle;

            if (string.Equals(buff.id, CharacterStats.TacticianDualityHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.TacticianDualityHudBuffTitle;

            if (string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "Sprint";

            if (AbilityTooltipDamagePreview.TryBuildHudBuffTooltip(
                    buff.id, buff.displayStacks, SkillsManager.Instance, _abilityDatabase, out string hudTitle, out _))
                return hudTitle;

            if (_abilityDatabase != null && !string.IsNullOrWhiteSpace(buff.id))
            {
                AbilityDefinition def = _abilityDatabase.Get(buff.id);
                if (def != null)
                {
                    string name = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def);
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }

            return string.IsNullOrWhiteSpace(buff.id) ? "Ability" : buff.id;
        }

        return buff.type switch
        {
            ConsumableEffectType.PhysicalDamageBoost => "Physical Damage Boost",
            ConsumableEffectType.MagicDamageBoost => "Magic Damage Boost",
            ConsumableEffectType.AttackSpeed => "Attack Speed Boost",
            ConsumableEffectType.HealOverTime => "Regeneration",
            ConsumableEffectType.FoodHealOverTime => "Food Regeneration",
            ConsumableEffectType.ManaRegenOverTime => "Mana Regeneration",
            ConsumableEffectType.EnergyRegen => "Energy Regeneration",
            ConsumableEffectType.MoveSpeed => "Move Speed",
            ConsumableEffectType.FoodMoveSpeed => "Swiftness",
            ConsumableEffectType.AbilityDamageBoost => "Ability Power Boost",
            ConsumableEffectType.DefenseBoost => "Defence Boost",
            ConsumableEffectType.ArmorBoost => "Armour Boost",
            ConsumableEffectType.MagicResistBoost => "Magic Resist Boost",
            ConsumableEffectType.DamageReduction => "Damage Reduction",
            ConsumableEffectType.PoisonImmunity => "Poison Immunity",
            ConsumableEffectType.BleedImmunity => "Bleed Immunity",
            ConsumableEffectType.FoodOverheal => "Overheal",
            ConsumableEffectType.FoodFocused => "Focused",
            _ => "Buff"
        };
    }

    private string GetBuffBody(PlayerBuffController.ActiveBuff buff)
    {
        if (buff.type == ConsumableEffectType.HudAbilityBuff)
        {
            if (string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "+10% Attack Speed for 7 seconds. Refreshes when you land a critical hit.";

            if (string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase))
            {
                CharacterStats playerStats = player != null ? player.GetComponent<CharacterStats>() : null;
                float baseSpeed = playerStats != null ? playerStats.FinalMoveSpeed : 0f;
                float sprintSpeed = PlayerSprintInput.ApplySprintBonus(baseSpeed);
                return
                    $"Adds +{PlayerSprintInput.SprintSpeedBonusFlat:0.#} move speed while moving ({baseSpeed:0.#} → {sprintSpeed:0.#}). " +
                    "Costs 20% max energy per second and pauses energy regen.";
            }

            if (MeleeMajorPassiveTooltipText.TryGetHudBuffTooltip(buff.id, buff.displayStacks, out _, out string overloadBody))
                return overloadBody;

            if (AbilityTooltipDamagePreview.TryBuildHudBuffTooltip(
                    buff.id, buff.displayStacks, SkillsManager.Instance, _abilityDatabase, out _, out string hudBody))
                return hudBody;

            string core = "Temporary ability effect.";
            if (_abilityDatabase != null && !string.IsNullOrWhiteSpace(buff.id))
            {
                AbilityDefinition def = _abilityDatabase.Get(buff.id);
                if (def != null)
                {
                    string resolved = SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def);
                    core = resolved != "No description." ? resolved : core;
                }
            }

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

            ConsumableEffectType.FoodHealOverTime =>
                $"Heals {(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#} HP per second (food)",

            ConsumableEffectType.ManaRegenOverTime =>
                $"Restores {(buff.duration > 0f ? buff.magnitude / buff.duration : 0f):0.#} mana per second",

            ConsumableEffectType.EnergyRegen =>
                $"+{buff.magnitude:0.#} energy per second",

            ConsumableEffectType.MoveSpeed =>
                $"+{pct:0.#}% movement speed",

            ConsumableEffectType.FoodMoveSpeed =>
                $"+{pct:0.#}% movement speed (food)",

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

            ConsumableEffectType.FoodOverheal =>
                $"Effective max HP increased by {buff.magnitude:0} above your normal max",

            ConsumableEffectType.FoodFocused =>
                $"+{pct:0.#}% to basic-attack minimum and maximum damage",

            _ => "Temporary buff"
        };
    }
}
