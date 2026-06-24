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
    private const string EnergyInfusionBuffId = "energy_infusion";
    private const string WhirlwindBuffId = "whirlwind";
    private const string CrusaderStrikeBuffId = "crusader_strike";
    private const string CleavingStrikesBuffId = "cleaving_strikes";

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

    [Header("Melee — Way of the Berserker (Lv50 Capstone) HUD")]
    [Tooltip("Icon for Way of the Berserker stacking buff after melee auto attacks.")]
    [SerializeField] private Sprite wayOfTheBerserkerHudIcon;

    [Header("Melee — Berserker's Thirst (Lv50 Capstone Leech) HUD")]
    [Tooltip("Icon for the low-HP life steal proc. Falls back to Way of the Berserker icon.")]
    [SerializeField] private Sprite wayOfTheBerserkerLeechHudIcon;

    [Header("Melee — Way of the Crusader (Lv50 Capstone) HUD")]
    [Tooltip("Icon for holy seal stacks after hitting enemies.")]
    [SerializeField] private Sprite wayOfTheCrusaderHudIcon;

    [Header("Melee — Crusader Strike Fire Balance HUD")]
    [Tooltip("Icon for Crusader Strike fire balance window. Falls back to the ability icon.")]
    [SerializeField] private Sprite crusaderStrikeFireBalanceHudIcon;

    [Header("Melee — Bloodbath (Lv40 Major) HUD")]
    [Tooltip("Icon for Bloodbath stacks after applying bleed.")]
    [SerializeField] private Sprite bloodbathHudIcon;

    [Header("Melee — Way of the Blade Dancer (Lv50 Capstone) HUD")]
    [Tooltip("Icon for +crit after kill (Blade Dancer's Focus). Not an inventory item — assign here.")]
    [SerializeField] private Sprite wayOfTheBladeDancerKillCritHudIcon;

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
    [SerializeField] private PlayerAbilityController abilityController;

    [Header("Refresh")]
    [Tooltip("How often (seconds) the buff icon timers tick down on the HUD. Display only — does not affect actual buff expiry.")]
    [SerializeField, Min(0.05f)] private float buffTimerRefreshInterval = 0.2f;

    private readonly List<GameObject> spawnedDebuffIcons = new();
    private readonly Dictionary<string, GameObject> _debuffIconsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _debuffIconKeyOrder = new();
    private readonly List<GameObject> spawnedBuffIcons = new();
    private readonly List<BuffIconUI> spawnedBuffIconUis = new();
    private readonly List<BuffIconVisualSnapshot> buffIconSnapshots = new();
    private readonly List<string> _buffIconKeyOrder = new();
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
        public bool isHudAbilityBuff;
        public bool dismissable;
    }

    /// <summary>
    /// Some HUD buffs have expensive, stack-dependent tooltip builders. Keep the list tight.
    /// Everything else can reuse the previous title/body/sprite on stack updates to avoid GC churn.
    /// </summary>
    private static bool IsHudBuffTooltipDynamic(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        // These tooltips commonly include stack-specific lines.
        if (string.Equals(id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(id, AbilityCombatPower.BloodbathHudBuffId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(id, PlayerCombatController.WayOfTheBerserkerHudBuffId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(id, PlayerCombatController.WayOfTheCrusaderHudBuffId, StringComparison.OrdinalIgnoreCase))
            return true;

        // Most ability tooltips are static; keep these as static too (we still update stacks/labels).
        return false;
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
        if (!abilityController && player)
            abilityController = player.GetComponent<PlayerAbilityController>();
        if (!abilityController)
            abilityController = FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
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

        ResolveBuffDebuffContainerRefs();
    }

    private void ResolveBuffDebuffContainerRefs()
    {
        Transform buff = buffContainer != null ? buffContainer : transform.Find("BuffContainer");
        Transform debuff = debuffContainer != null ? debuffContainer : transform.Find("DebuffContainer");

        if (buff != null && (buffContainer == null || buffContainer == debuffContainer))
            buffContainer = buff;

        if (debuff != null && (debuffContainer == null || debuffContainer == buffContainer))
            debuffContainer = debuff;

        if (buffContainer != null && debuffContainer != null && buffContainer == debuffContainer)
            Debug.LogWarning(
                "[BuffsDebuffsPanel] buffContainer and debuffContainer reference the same Transform. " +
                "Assign BuffContainer (green) and DebuffContainer (red) separately in the inspector.",
                this);
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

    private void HandleAilmentsChanged()
    {
        AilmentUiRebuildCoordinator.MarkBuffsPanelDirty(this);
    }

    internal void FlushCoalescedAilmentUiRebuild() => RefreshDebuffs();
    internal void FlushCoalescedBuffUiRebuild() => RefreshBuffs();
    private void HandleBuffsChanged() => AilmentUiRebuildCoordinator.MarkBuffsPanelDirtyForBuffs(this);

    public void RefreshAll()
    {
        RefreshDebuffs();
        RefreshBuffs();
    }

    public void RefreshDebuffs()
    {
        if (ailments == null || debuffContainer == null || debuffIconPrefab == null)
        {
            ClearDebuffs();
            return;
        }

        var active = new List<(string key, Sprite sprite, int stacks, string title, string body)>(8);
        if (ailments.HasBleed)
        {
            active.Add((
                GameTooltipTexts.BleedTitle,
                bleedIcon,
                1,
                GameTooltipTexts.BleedTitle,
                BuildPlayerBleedBody(ailments.BleedDamagePerSecond)));
        }

        if (ailments.HasPoison)
        {
            active.Add((
                GameTooltipTexts.PoisonTitle,
                poisonIcon,
                ailments.PoisonStacks,
                GameTooltipTexts.PoisonTitle,
                BuildPlayerPoisonBody(ailments.PoisonDamagePerSecond)));
        }

        if (ailments.HasBurn)
        {
            active.Add((
                GameTooltipTexts.BurnTitle,
                burnIcon,
                ailments.BurnStacks,
                GameTooltipTexts.BurnTitle,
                BuildPlayerBurnBody(ailments.BurnDamagePerSecond)));
        }

        if (ailments.HasChill)
        {
            active.Add((
                GameTooltipTexts.ChillTitle,
                chillIcon,
                ailments.ChillStacks,
                GameTooltipTexts.ChillTitle,
                BuildPlayerChillBody(ailments.ChillSlowPercent)));
        }

        if (ailments.HasShock)
        {
            active.Add((
                GameTooltipTexts.ShockTitle,
                shockIcon,
                1,
                GameTooltipTexts.ShockTitle,
                BuildPlayerShockBody(ailments.ShockDamageTakenBonusPercent)));
        }

        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < active.Count; i++)
            activeKeys.Add(active[i].key);

        for (int i = _debuffIconKeyOrder.Count - 1; i >= 0; i--)
        {
            string key = _debuffIconKeyOrder[i];
            if (!activeKeys.Contains(key))
                RemoveHudDebuffIcon(key);
        }

        for (int i = 0; i < active.Count; i++)
        {
            var entry = active[i];
            if (!_debuffIconsByKey.TryGetValue(entry.key, out GameObject icon) || icon == null)
            {
                SpawnDebuffIcon(entry.sprite, entry.key, entry.stacks, entry.title, entry.body);
                icon = spawnedDebuffIcons[spawnedDebuffIcons.Count - 1];
                _debuffIconsByKey[entry.key] = icon;
                _debuffIconKeyOrder.Add(entry.key);
                continue;
            }

            DebuffIconUI iconUI = icon.GetComponent<DebuffIconUI>();
            if (iconUI != null)
            {
                iconUI.SetData(
                    entry.sprite,
                    entry.stacks,
                    entry.title,
                    entry.body,
                    panelTooltip,
                    tooltipMeasureRect,
                    tooltipHeightRect,
                    tooltipPreferredSide);
            }
        }

        SyncHudDebuffSiblingOrder();
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

        var activeByKey = new Dictionary<string, PlayerBuffController.ActiveBuff>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            PlayerBuffController.ActiveBuff buff = activeBuffs[i];
            if (buff == null)
                continue;

            string key = GetBuffIconKey(buff);
            if (string.IsNullOrEmpty(key))
                continue;

            activeByKey[key] = buff;
        }

        for (int i = _buffIconKeyOrder.Count - 1; i >= 0; i--)
        {
            string key = _buffIconKeyOrder[i];
            if (!activeByKey.ContainsKey(key))
                RemoveBuffIconAt(i);
        }

        foreach (KeyValuePair<string, PlayerBuffController.ActiveBuff> kvp in activeByKey)
        {
            if (!_buffIconKeyOrder.Exists(k => string.Equals(k, kvp.Key, StringComparison.OrdinalIgnoreCase)))
                _buffIconKeyOrder.Add(kvp.Key);
        }

        for (int i = 0; i < _buffIconKeyOrder.Count; i++)
        {
            string key = _buffIconKeyOrder[i];
            if (!activeByKey.TryGetValue(key, out PlayerBuffController.ActiveBuff buff) || buff == null)
                continue;

            BuffIconVisualSnapshot snapshot;
            bool canReuse =
                i < buffIconSnapshots.Count &&
                buffIconSnapshots[i].key == key &&
                buff.type == ConsumableEffectType.HudAbilityBuff &&
                !IsHudBuffTooltipDynamic(buff.id);

            if (canReuse)
            {
                BuffIconVisualSnapshot prev = buffIconSnapshots[i];
                snapshot = prev;
                snapshot.displayStacks = buff.displayStacks;
                snapshot.valueLabel = GetBuffValueLabel(buff);
                snapshot.totalDurationSeconds = buff.duration;
                snapshot.persistActiveOverlay = buff.hudPersistActiveOverlay;
            }
            else
            {
                snapshot = BuildBuffIconSnapshot(buff);
            }

            if (i >= spawnedBuffIcons.Count)
            {
                SpawnBuffIcon(snapshot, buff.RemainingSeconds);
                continue;
            }

            if (!BuffIconSnapshotEquals(buffIconSnapshots[i], snapshot))
                ApplyBuffIconSnapshot(i, snapshot, buff.RemainingSeconds);
        }

        SyncBuffIconSiblingOrder();
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
            persistActiveOverlay = buff.hudPersistActiveOverlay,
            isHudAbilityBuff = buff.type == ConsumableEffectType.HudAbilityBuff,
            dismissable = CanDismissBuffFromPanel(buff)
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

        WireBuffDismiss(iconUI, snapshot);

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

        WireBuffDismiss(iconUI, snapshot);

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

        for (int i = 0; i < _buffIconKeyOrder.Count && i < spawnedBuffIconUis.Count; i++)
        {
            string key = _buffIconKeyOrder[i];
            PlayerBuffController.ActiveBuff buff = FindActiveBuffByKey(activeBuffs, key);
            if (buff == null)
                continue;

            BuffIconUI iconUI = spawnedBuffIconUis[i];
            if (iconUI != null)
                iconUI.UpdateTimer(buff.RemainingSeconds);
        }
    }

    private static PlayerBuffController.ActiveBuff FindActiveBuffByKey(
        IReadOnlyList<PlayerBuffController.ActiveBuff> activeBuffs,
        string key)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            PlayerBuffController.ActiveBuff buff = activeBuffs[i];
            if (buff == null)
                continue;
            if (string.Equals(GetBuffIconKey(buff), key, StringComparison.OrdinalIgnoreCase))
                return buff;
        }

        return null;
    }

    private void RemoveHudDebuffIcon(string key)
    {
        if (!_debuffIconsByKey.TryGetValue(key, out GameObject icon))
            return;

        _debuffIconsByKey.Remove(key);
        _debuffIconKeyOrder.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        spawnedDebuffIcons.Remove(icon);
        if (icon != null)
            Destroy(icon);
    }

    private void SyncHudDebuffSiblingOrder()
    {
        for (int i = 0; i < _debuffIconKeyOrder.Count; i++)
        {
            if (!_debuffIconsByKey.TryGetValue(_debuffIconKeyOrder[i], out GameObject icon) || icon == null)
                continue;

            icon.transform.SetSiblingIndex(i);
        }
    }

    private void RemoveBuffIconAt(int index)
    {
        if (index < 0 || index >= _buffIconKeyOrder.Count)
            return;

        _buffIconKeyOrder.RemoveAt(index);

        if (index < spawnedBuffIcons.Count)
        {
            if (spawnedBuffIcons[index] != null)
                Destroy(spawnedBuffIcons[index]);
            spawnedBuffIcons.RemoveAt(index);
        }

        if (index < spawnedBuffIconUis.Count)
            spawnedBuffIconUis.RemoveAt(index);

        if (index < buffIconSnapshots.Count)
            buffIconSnapshots.RemoveAt(index);
    }

    private void SyncBuffIconSiblingOrder()
    {
        for (int i = 0; i < _buffIconKeyOrder.Count; i++)
        {
            if (i >= spawnedBuffIcons.Count)
                break;

            GameObject icon = spawnedBuffIcons[i];
            if (icon != null)
                icon.transform.SetSiblingIndex(i);
        }
    }

    public void ClearDebuffs()
    {
        _debuffIconsByKey.Clear();
        _debuffIconKeyOrder.Clear();
        for (int i = 0; i < spawnedDebuffIcons.Count; i++)
        {
            if (spawnedDebuffIcons[i] != null)
                Destroy(spawnedDebuffIcons[i]);
        }
        spawnedDebuffIcons.Clear();
    }

    public void ClearBuffs()
    {
        _buffIconKeyOrder.Clear();
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
            string.Equals(buff.id, PlayerCombatController.WayOfTheBerserkerHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (wayOfTheBerserkerHudIcon != null)
                return wayOfTheBerserkerHudIcon;
            Sprite capstoneIcon = CapstonePresentationIcons.ResolveWayOfTheBerserkerIcon();
            if (capstoneIcon != null)
                return capstoneIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, PlayerCombatController.WayOfTheBerserkerLeechHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (wayOfTheBerserkerLeechHudIcon != null)
                return wayOfTheBerserkerLeechHudIcon;
            if (wayOfTheBerserkerHudIcon != null)
                return wayOfTheBerserkerHudIcon;
            Sprite capstoneIcon = CapstonePresentationIcons.ResolveWayOfTheBerserkerIcon();
            if (capstoneIcon != null)
                return capstoneIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, PlayerCombatController.WayOfTheCrusaderHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (wayOfTheCrusaderHudIcon != null)
                return wayOfTheCrusaderHudIcon;
            Sprite holySealIcon = CapstonePresentationIcons.ResolveHolySealHudIcon();
            if (holySealIcon != null)
                return holySealIcon;
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
            string.Equals(buff.id, PlayerAbilityController.CrusaderStrikeFireBalanceHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (crusaderStrikeFireBalanceHudIcon != null)
                return crusaderStrikeFireBalanceHudIcon;
            if (_abilityDatabase != null)
            {
                AbilityDefinition crusader = _abilityDatabase.Get(AbilityCombatPower.CrusaderStrikeAbilityId);
                if (crusader != null)
                {
                    Sprite spr = SkillsAbilityPresentationResolver.ResolveAbilityIcon(crusader);
                    if (spr != null)
                        return spr;
                }
            }
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, AbilityCombatPower.BloodbathHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (bloodbathHudIcon != null)
                return bloodbathHudIcon;
            if (physicalDamageBuffIcon != null)
                return physicalDamageBuffIcon;
        }

        if (buff.type == ConsumableEffectType.HudAbilityBuff &&
            string.Equals(buff.id, AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId, StringComparison.OrdinalIgnoreCase))
        {
            if (wayOfTheBladeDancerKillCritHudIcon != null)
                return wayOfTheBladeDancerKillCritHudIcon;
            Sprite capstoneIcon = CapstonePresentationIcons.ResolveWayOfTheBladeDancerIcon();
            if (capstoneIcon != null)
                return capstoneIcon;
            if (attackSpeedBuffIcon != null)
                return attackSpeedBuffIcon;
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

        // Only resolve inventory icons when the buff id is a real item id (consumables set effectId to itemId).
        // Ability/stat buffs with no effectId use ConsumableEffectType.ToString() and are not in ItemDatabase.
        if (buff.type != ConsumableEffectType.HudAbilityBuff &&
            inventory != null &&
            !string.IsNullOrWhiteSpace(buff.id) &&
            !string.Equals(buff.id, buff.type.ToString(), StringComparison.Ordinal))
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
            if (string.Equals(buff.id, EnergyInfusionBuffId, StringComparison.OrdinalIgnoreCase))
                return "INF";

            if (string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "+10%";

            if (string.Equals(buff.id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.FormatOverloadValueLabel(buff.displayStacks);

            if (string.Equals(buff.id, AbilityCombatPower.BloodbathHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.FormatBloodbathValueLabel(buff.displayStacks);

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

            if (string.Equals(buff.id, AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId, StringComparison.OrdinalIgnoreCase))
                return $"+{Mathf.RoundToInt(AbilityCombatPower.WayOfTheBladeDancerKillCritChanceBonus * 100f)}%";

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

            ConsumableEffectType.ArmourBoost =>
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
            if (string.Equals(buff.id, WhirlwindBuffId, StringComparison.OrdinalIgnoreCase))
                return "Whirlwind";

            if (string.Equals(buff.id, CrusaderStrikeBuffId, StringComparison.OrdinalIgnoreCase))
                return "Crusader Strike";

            if (string.Equals(buff.id, CleavingStrikesBuffId, StringComparison.OrdinalIgnoreCase))
                return "Cleaving Strikes";

            if (string.Equals(buff.id, EnergyInfusionBuffId, StringComparison.OrdinalIgnoreCase))
                return "Energy Infusion";

            if (string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "Shadow Hunter";

            if (string.Equals(buff.id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.BattleEngineOverloadTitle;

            if (string.Equals(buff.id, PlayerCombatController.WayOfTheBerserkerHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.WayOfTheBerserkerTitle;

            if (string.Equals(buff.id, PlayerCombatController.WayOfTheBerserkerLeechHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.WayOfTheBerserkerLeechTitle;

            if (string.Equals(buff.id, PlayerCombatController.WayOfTheCrusaderHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.WayOfTheCrusaderTitle;

            if (string.Equals(buff.id, CharacterStats.TacticianDualityHudBuffId, StringComparison.OrdinalIgnoreCase))
                return MeleeMajorPassiveTooltipText.TacticianDualityHudBuffTitle;

            if (string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "Sprint";

            CharacterStats playerStats = GetPlayerStats();
            if (MeleeMajorPassiveTooltipText.TryGetHudBuffTooltip(
                    buff.id, buff.displayStacks, out string meleeHudTitle, out _, playerStats))
                return meleeHudTitle;

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
            ConsumableEffectType.ArmourBoost => "Armour Boost",
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
            CharacterStats playerStats = GetPlayerStats();

            if (string.Equals(buff.id, WhirlwindBuffId, StringComparison.OrdinalIgnoreCase))
                return "Channel a spinning attack that repeatedly hits nearby enemies.\nStacks increase while channeling.";

            if (string.Equals(buff.id, CrusaderStrikeBuffId, StringComparison.OrdinalIgnoreCase))
                return "Combo ability. Each stage advances the strike sequence.\nFinal strike applies fire effects based on upgrades.";

            if (string.Equals(buff.id, CleavingStrikesBuffId, StringComparison.OrdinalIgnoreCase))
                return "Temporary buff that grants additional cleaving strikes.\nStacks show remaining empowered swings.";

            if (string.Equals(buff.id, EnergyInfusionBuffId, StringComparison.OrdinalIgnoreCase))
                return
                    "Toggle an infusion buff that converts a portion of energy cost to mana for melee abilities.\n" +
                    "If mana is insufficient, abilities use full energy cost instead.";

            if (string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase))
                return "+10% Attack Speed for 7 seconds. Refreshes when you land a critical hit.";

            if (string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase))
            {
                float baseSpeed = playerStats != null ? playerStats.FinalMoveSpeed : 0f;
                float sprintSpeed = PlayerSprintInput.ApplySprintBonus(baseSpeed);
                return
                    $"Adds +{PlayerSprintInput.SprintSpeedBonusPercent * 100f:0.#}% move speed while moving ({baseSpeed:0.#} → {sprintSpeed:0.#}). " +
                    "Costs 20% max energy per second and pauses energy regen.";
            }

            if (MeleeMajorPassiveTooltipText.TryGetHudBuffTooltip(
                    buff.id, buff.displayStacks, out _, out string overloadBody, playerStats))
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

            ConsumableEffectType.ArmourBoost =>
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

    private static bool CanDismissBuffFromPanel(PlayerBuffController.ActiveBuff buff)
    {
        if (buff == null)
            return false;

        if (buff.type == ConsumableEffectType.HudAbilityBuff)
        {
            if (string.IsNullOrWhiteSpace(buff.id))
                return false;

            return !string.Equals(buff.id, "crusader_strike", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, PlayerAbilityController.CrusaderStrikeFireBalanceHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, CharacterStats.TacticianDualityHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, CharacterStats.ShadowHunterHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, CharacterStats.BattleEngineOverloadHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, PlayerController.WoodcuttingFlowStateHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, PlayerController.FishingCalmWatersMajorHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, AbilityCombatPower.WayOfTheBladeDancerKillCritHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, PlayerCombatController.WayOfTheBerserkerHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, PlayerCombatController.WayOfTheBerserkerLeechHudBuffId, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(buff.id, PlayerSprintInput.SprintHudBuffId, StringComparison.OrdinalIgnoreCase);
        }

        return buff.type != ConsumableEffectType.None && buff.duration > 0f;
    }

    private void WireBuffDismiss(BuffIconUI iconUI, BuffIconVisualSnapshot snapshot)
    {
        if (!iconUI)
            return;

        if (!snapshot.dismissable)
        {
            iconUI.SetRightClickDismissHandler(null);
            return;
        }

        iconUI.SetRightClickDismissHandler(() => RequestDismissBuff(snapshot.key, snapshot.isHudAbilityBuff));
    }

    private void RequestDismissBuff(string buffKey, bool isHudAbilityBuff)
    {
        if (buffs == null || string.IsNullOrWhiteSpace(buffKey))
            return;

        IReadOnlyList<PlayerBuffController.ActiveBuff> activeBuffs = buffs.ActiveBuffs;
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            PlayerBuffController.ActiveBuff buff = activeBuffs[i];
            if (buff == null || GetBuffIconKey(buff) != buffKey)
                continue;

            if (isHudAbilityBuff)
            {
                if (!abilityController && player)
                    abilityController = player.GetComponent<PlayerAbilityController>();
                abilityController?.TryDismissHudBuffFromPanel(buff.id);
            }
            else
            {
                buffs.TryDismissConsumableBuff(buff);
            }

            return;
        }
    }

    private CharacterStats GetPlayerStats() =>
        player != null ? player.GetComponent<CharacterStats>() : null;
}
