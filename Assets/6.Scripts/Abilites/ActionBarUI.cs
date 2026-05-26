using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// How <see cref="ActionBarUI.ShowGatheringBarForSkill"/> interacts with a player-pinned W/M/F strip.
/// </summary>
public enum GatheringBarDriveKind
{
    /// <summary>World / player action sync. Blocked while manual W/M/F lock is on, unless gather activity changes skill type.</summary>
    AutomaticGameplay,
    /// <summary>W/M/F bar buttons — persists until combat, Tab, skills, or a different gather activity.</summary>
    ManualStripButton,
    /// <summary>Skills UI — clears manual lock and follows the selected skill.</summary>
    SkillsMenuSelection,
}

/// <summary>
/// First <see cref="HotkeyBindIds.ActionBarSlotCount"/> slots use <see cref="HotkeyBindingManager"/> (list order =
/// ActionBar1…7). Extra slots use <see cref="SlotBinding.defaultKey"/> only.
/// </summary>

public class ActionBarUI : MonoBehaviour, ISaveable
{
    [System.Serializable]
    public class SlotBinding
    {
        public ActionBarSlotUI slot;
        public KeyCode defaultKey = KeyCode.None;
        [HideInInspector] public KeyCode currentKey = KeyCode.None;
    }

    [System.Serializable]
    public class SavedSlotState
    {
        public int slotIndex;
        public int kind;
        public string id;
        public int amount;
    }

    public IReadOnlyList<SlotBinding> SlotBindings => slotBindings;

    public static bool IsGatheringSkillType(SkillType skillType) =>
        skillType == SkillType.Woodcutting || skillType == SkillType.Mining || skillType == SkillType.Fishing;

    public bool IsGatheringBarActive => gatheringUiActive;

    public SkillType? CurrentGatheringBarSkill => gatheringUiActive ? gatheringSkillShown : (SkillType?)null;

    public bool IsManualGatheringStripLocked => _manualGatheringStripLocked;

    public bool CanSlotAcceptGatheringAbility(AbilityDefinition def)
    {
        // Gathering rows are layout presets only; any unlocked ability may be slotted (e.g. mobility while on a wood strip).
        return true;
    }

    /// <summary>
    /// Shows the saved gathering-only row for Wood/Mining/Fishing (abilities 1–5). Does not change equipment or consumable slots.
    /// </summary>
    /// <param name="skillType">Woodcutting, Mining, or Fishing.</param>
    /// <param name="driveKind">
    /// <see cref="GatheringBarDriveKind.ManualStripButton"/> pins the strip until combat, Tab, skills, or a different gather activity.
    /// </param>
    public void ShowGatheringBarForSkill(SkillType skillType, GatheringBarDriveKind driveKind = GatheringBarDriveKind.AutomaticGameplay)
    {
        if (!IsGatheringSkillType(skillType))
            return;

        switch (driveKind)
        {
            case GatheringBarDriveKind.ManualStripButton:
                _manualGatheringStripLocked = true;
                break;
            case GatheringBarDriveKind.SkillsMenuSelection:
                _manualGatheringStripLocked = false;
                break;
            case GatheringBarDriveKind.AutomaticGameplay:
                if (_manualGatheringStripLocked)
                {
                    if (!gatheringUiActive)
                    {
                        _manualGatheringStripLocked = false;
                    }
                    else if (gatheringSkillShown == skillType)
                    {
                        ApplyGatheringStripTheme(skillType);
                        return;
                    }
                    else
                    {
                        // Began gathering a different resource type — overrides a manual W/M/F choice.
                        _manualGatheringStripLocked = false;
                    }
                }

                break;
        }

        if (gatheringUiActive && gatheringSkillShown == skillType)
        {
            ApplyGatheringStripTheme(skillType);
            return;
        }

        ResolveCoreRefs();

        if (gatheringUiActive)
            RebuildGatheringListFromUi(GetGatheringListForSkill(gatheringSkillShown));

        if (!gatheringUiActive)
        {
            CaptureSlotsToSavedState();
            frozenCombatFiveAbilities.Clear();
            foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
            {
                SavedSlotState cap = CaptureSlotState(slot);
                if (cap != null)
                    frozenCombatFiveAbilities.Add(CloneSavedState(cap));
            }
        }

        gatheringUiActive = true;
        gatheringSkillShown = skillType;
        ApplyGatheringAbilityRowsToUi(GetGatheringListForSkill(skillType));
        ApplyGatheringStripTheme(skillType);
        NotifyPlayerStatsCombatPowerRelevantChange();
    }

    /// <summary>
    /// Restores combat ability slots (set 1/2 + potion/food) on the action bar. Call before Tab weapon swap if needed.
    /// </summary>
    public void ExitGatheringBarToCombat()
    {
        _manualGatheringStripLocked = false;

        if (!gatheringUiActive)
            return;

        RebuildGatheringListFromUi(GetGatheringListForSkill(gatheringSkillShown));
        WriteCombatSavedSlotsFromFrozenAbilitiesAndRestOfBarFromUi();
        gatheringUiActive = false;
        frozenCombatFiveAbilities.Clear();
        ApplySavedStateToSlots();
        RestoreGatheringStripCombatTheme();
        NotifyPlayerStatsCombatPowerRelevantChange();
    }

    private void CacheGatheringStripVisualDefaults()
    {
        if (_gatheringVisualDefaultsCached)
            return;

        if (actionBarBackgroundImage == null)
            actionBarBackgroundImage = GetComponent<Image>();

        if (actionBarBackgroundImage != null)
            _combatBackgroundColor = actionBarBackgroundImage.color;

        if (gatheringWoodSetGraphic != null)
            _combatWoodGraphicColor = gatheringWoodSetGraphic.color;
        if (gatheringMiningSetGraphic != null)
            _combatMiningGraphicColor = gatheringMiningSetGraphic.color;
        if (gatheringFishingSetGraphic != null)
            _combatFishGraphicColor = gatheringFishingSetGraphic.color;

        _gatheringVisualDefaultsCached = true;
    }

    private void ApplyGatheringStripTheme(SkillType active)
    {
        CacheGatheringStripVisualDefaults();

        Color wT = skillColourWoodcutting;
        Color mT = skillColourMining;
        Color fT = skillColourFishing;

        float bgBlend = Mathf.Clamp01(gatheringBackgroundTintStrength);
        if (actionBarBackgroundImage != null)
        {
            Color main = active == SkillType.Woodcutting ? wT : active == SkillType.Mining ? mT : fT;
            actionBarBackgroundImage.color = Color.Lerp(_combatBackgroundColor, main, bgBlend);
        }

        void PaintStripButton(Graphic g, Color theme, SkillType forSkill)
        {
            if (g == null)
                return;

            Color rgb = forSkill == active
                ? theme
                : Color.Lerp(theme, Color.gray, 0.55f);
            float a = forSkill == active
                ? Mathf.Clamp01(gatheringStripButtonActiveAlpha)
                : Mathf.Clamp01(gatheringStripButtonInactiveAlpha);
            g.color = new Color(rgb.r, rgb.g, rgb.b, a);
        }

        PaintStripButton(gatheringWoodSetGraphic, wT, SkillType.Woodcutting);
        PaintStripButton(gatheringMiningSetGraphic, mT, SkillType.Mining);
        PaintStripButton(gatheringFishingSetGraphic, fT, SkillType.Fishing);
    }

    private void RestoreGatheringStripCombatTheme()
    {
        CacheGatheringStripVisualDefaults();
        if (actionBarBackgroundImage != null)
            actionBarBackgroundImage.color = _combatBackgroundColor;
        if (gatheringWoodSetGraphic != null)
            gatheringWoodSetGraphic.color = _combatWoodGraphicColor;
        if (gatheringMiningSetGraphic != null)
            gatheringMiningSetGraphic.color = _combatMiningGraphicColor;
        if (gatheringFishingSetGraphic != null)
            gatheringFishingSetGraphic.color = _combatFishGraphicColor;
    }

    public string GetHotkeyDisplayString(KeyCode key) => HotkeyBindingManager.GetDisplayString(key);

    public IEnumerable<ActionBarSlotUI> GetSlots()
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            if (slotBindings[i] != null && slotBindings[i].slot != null)
                yield return slotBindings[i].slot;
        }
    }

    /// <summary>First five combat ability loadout slots (hotkeys 1–5). Excludes potion/food and extra bar slots.</summary>
    public IEnumerable<ActionBarSlotUI> EnumerateCombatLoadoutAbilitySlots() => EnumerateFirstFiveLoadoutAbilitySlots();

    /// <summary>
    /// Ability ids on the combat loadout used for CP / DPS breakdown. While the gathering strip is shown, uses the
    /// frozen combat row instead of the gathering abilities currently painted on the bar.
    /// </summary>
    public IEnumerable<string> EnumerateCombatLoadoutAbilityIdsForCombatPower()
    {
        if (gatheringUiActive)
        {
            for (int i = 0; i < frozenCombatFiveAbilities.Count; i++)
            {
                SavedSlotState st = frozenCombatFiveAbilities[i];
                if (st == null || string.IsNullOrWhiteSpace(st.id))
                    continue;
                if (st.kind != (int)ActionBarAssignmentKind.Ability)
                    continue;
                yield return st.id;
            }

            yield break;
        }

        foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
        {
            ActionBarAssignment a = slot != null ? slot.AssignedAction : null;
            if (a == null || !a.IsAssigned || !a.IsAbility || string.IsNullOrWhiteSpace(a.id))
                continue;
            yield return a.id;
        }
    }

    /// <summary>
    /// Prefer the player's action bar when duplicates exist (persistent shell + scene UI). Matches save dedupe scoring.
    /// </summary>
    public static ActionBarUI FindForCharacterStats(CharacterStats stats)
    {
        ActionBarUI[] bars = FindObjectsByType<ActionBarUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (bars == null || bars.Length == 0)
            return null;
        if (bars.Length == 1)
            return bars[0];

        PlayerController player = stats != null
            ? stats.GetComponent<PlayerController>() ?? stats.GetComponentInParent<PlayerController>()
            : null;
        Transform playerRoot = player != null ? player.transform : stats != null ? stats.transform : null;

        ActionBarUI best = null;
        int bestScore = -1;
        for (int i = 0; i < bars.Length; i++)
        {
            ActionBarUI candidate = bars[i];
            if (!candidate)
                continue;

            int score = candidate.ComputeSavePriorityScore();
            if (playerRoot != null && candidate.transform.IsChildOf(playerRoot))
                score += 10000;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best != null ? best : bars[0];
    }

    /// <summary>
    /// Puts an unlocked ability into the first empty ability-compatible slot (same ordering as <see cref="slotBindings"/>).
    /// If the ability is already on the bar, returns true without moving it.
    /// </summary>
    public bool TryAssignAbilityToFirstEmptySlot(AbilityDefinition def)
    {
        if (def == null)
            return false;

        ResolveCoreRefs();

        SkillDefinition skill = skillDatabase != null ? skillDatabase.Get(def.sourceSkill) : null;
        if (!SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, def, skillsManager))
            return false;

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            ActionBarSlotUI slot = binding.slot;
            if (slot.SlotType != ActionBarSlotType.Ability && slot.SlotType != ActionBarSlotType.Any)
                continue;

            if (slot.AssignedAction != null && slot.AssignedAction.IsAssigned &&
                slot.AssignedAction.IsAbility &&
                string.Equals(slot.AssignedAction.id, def.abilityId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        ActionBarAssignment assignment = ActionBarAssignment.CreateAbility(
            def.abilityId,
            SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def),
            SkillsAbilityPresentationResolver.ResolveAbilityIcon(def),
            SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def) ?? string.Empty);

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            ActionBarSlotUI slot = binding.slot;
            if (slot.SlotType != ActionBarSlotType.Ability && slot.SlotType != ActionBarSlotType.Any)
                continue;

            if (slot.AssignedAction != null && slot.AssignedAction.IsAssigned)
                continue;

            if (!slot.CanAccept(assignment))
                continue;

            if (slot.TryPaletteAssignAbilityWithUniqueSwap(assignment))
            {
                NotifyPlayerStatsCombatPowerRelevantChange();
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes the first bar slot that holds <paramref name="abilityId"/>.</summary>
    public bool TryClearAbilityFromBar(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        foreach (ActionBarSlotUI slot in GetSlots())
        {
            if (slot == null || slot.AssignedAction == null || !slot.AssignedAction.IsAssigned)
                continue;

            if (!slot.AssignedAction.IsAbility)
                continue;

            if (!string.Equals(slot.AssignedAction.id, abilityId, StringComparison.OrdinalIgnoreCase))
                continue;

            slot.ClearAssignment(notify: true);
            NotifyPlayerStatsCombatPowerRelevantChange();
            return true;
        }

        return false;
    }

    public bool IsAbilityOnBar(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        foreach (ActionBarSlotUI slot in GetSlots())
        {
            if (slot == null || slot.AssignedAction == null || !slot.AssignedAction.IsAssigned)
                continue;

            if (!slot.AssignedAction.IsAbility)
                continue;

            if (string.Equals(slot.AssignedAction.id, abilityId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the first five ability loadout slots and assigns <paramref name="abilitiesInOrder"/> top-to-bottom (slot 1, 2, …).
    /// Extra slots stay empty. Does not change potion/food slots.
    /// </summary>
    public int ReplaceLoadoutAbilitiesInOrder(IReadOnlyList<AbilityDefinition> abilitiesInOrder)
    {
        if (abilitiesInOrder == null || abilitiesInOrder.Count == 0)
            return 0;

        ResolveCoreRefs();

        var loadoutSlots = new List<ActionBarSlotUI>();
        foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
            loadoutSlots.Add(slot);

        if (loadoutSlots.Count == 0)
            return 0;

        for (int i = 0; i < loadoutSlots.Count; i++)
            loadoutSlots[i].ClearAssignment(false);

        int assigned = 0;
        int abilityIndex = 0;
        for (int slotIndex = 0; slotIndex < loadoutSlots.Count; slotIndex++)
        {
            ActionBarSlotUI slot = loadoutSlots[slotIndex];
            while (abilityIndex < abilitiesInOrder.Count)
            {
                AbilityDefinition def = abilitiesInOrder[abilityIndex++];
                if (def == null || string.IsNullOrWhiteSpace(def.abilityId))
                    continue;

                SkillDefinition skill = skillDatabase != null ? skillDatabase.Get(def.sourceSkill) : null;
                if (!SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, def, skillsManager))
                    continue;

                ActionBarAssignment assignment = ActionBarAssignment.CreateAbility(
                    def.abilityId,
                    SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def),
                    SkillsAbilityPresentationResolver.ResolveAbilityIcon(def),
                    SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def) ?? string.Empty);

                if (!slot.CanAccept(assignment))
                    continue;

                slot.Assign(assignment, false);
                assigned++;
                break;
            }
        }

        for (int i = 0; i < slotBindings.Count; i++)
        {
            ActionBarSlotUI s = slotBindings[i]?.slot;
            if (s != null)
                RefreshSlotRuntime(s);
        }

        CaptureSlotsToSavedState();
        if (!suppressSaveForLoadoutSwap && SaveManager.Instance != null)
            SaveManager.Instance.Save();
        NotifyPlayerStatsCombatPowerRelevantChange();
        return assigned;
    }

    /// <summary>
    /// Moves consumables from an inventory slot into the matching action-bar consumable slot.
    /// Same item stacks in-bar; different item swaps back into inventory.
    /// </summary>
    public bool TryMoveConsumableFromInventorySlot(int inventorySlotIndex, int amountToMove = int.MaxValue)
    {
        ResolveCoreRefs();
        if (inventory == null || inventorySlotIndex < 0)
            return false;

        Inventory.Slot src = inventory.GetSlot(inventorySlotIndex);
        if (src.IsEmpty || string.IsNullOrWhiteSpace(src.itemId))
            return false;

        ItemDefinition def = inventory.GetItemDef(src.itemId);
        if (def == null || (!def.IsFood && !def.IsPotion))
            return false;

        ActionBarSlotUI target = FindConsumableSlot(def);
        if (target == null)
            return false;

        int move = Mathf.Clamp(amountToMove, 1, src.amount);
        return target.TryStoreConsumableFromInventorySlot(inventorySlotIndex, move);
    }

    public bool TryAssignConsumableFromItemDefinition(ItemDefinition def)
    {
        if (def == null || (!def.IsFood && !def.IsPotion))
            return false;

        ResolveCoreRefs();
        if (inventory == null)
            return false;
        ActionBarSlotUI target = FindConsumableSlot(def);

        if (target == null)
            return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            Inventory.Slot slot = inventory.GetSlot(i);
            if (slot.IsEmpty || string.IsNullOrWhiteSpace(slot.itemId))
                continue;

            string remapped = Inventory.RemapLegacyItemId(slot.itemId);
            if (!string.Equals(remapped, def.itemId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return target.TryStoreConsumableFromInventorySlot(i, slot.amount);
        }

        return false;
    }

    /// <summary>Inspector or runtime default — same DB used to resolve saved bar slots and tooltips.</summary>
    public AbilityDatabase GetAbilityDatabaseOrDefault()
    {
        if (abilityDatabase == null)
            abilityDatabase = AbilityDatabase.LoadDefault();
        return abilityDatabase;
    }

    [Header("Slots")]
    [SerializeField] private List<SlotBinding> slotBindings = new();

    [Header("Runtime refresh")]
    [Tooltip("How often ability cooldowns, GCD, and buff overlays refresh on the bar. Hotkeys still poll every frame.")]
    [SerializeField, Min(0.02f)] private float slotRuntimeRefreshInterval = 0.1f;

    private float _nextSlotRuntimeRefreshAt;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private PlayerConsumableController consumableController;
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private PlayerBuffController buffController;
    [SerializeField] private AbilityDatabase abilityDatabase;
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;

    private PlayerController _player;

    [Header("Gathering strip visuals (optional)")]
    [Tooltip("e.g. ActionBarWindow Image — tinted toward the active gathering skill colour below; combat restores the cached color.")]
    [SerializeField] private Image actionBarBackgroundImage;

    [Tooltip("W / M / F strip controls (each button's Image or other Graphic). Tinted with the skill colours below.")]
    [SerializeField] private Graphic gatheringWoodSetGraphic;

    [SerializeField] private Graphic gatheringMiningSetGraphic;

    [SerializeField] private Graphic gatheringFishingSetGraphic;

    [Header("Gathering strip — selection buttons (optional)")]
    [Tooltip("Assign WoodcuttingSetButton, MiningSetButton, FishingSetButton. Clicks are wired in code — leave each Button’s On Click () list empty.")]
    [SerializeField] private Button gatheringWoodStripButton;

    [SerializeField] private Button gatheringMiningStripButton;

    [SerializeField] private Button gatheringFishingStripButton;

    [Tooltip("Colours for the gathering strip (match your HUD / strip “Skill Colours”). Used for W/M/F and bar background tint.")]
    [SerializeField] private Color skillColourMining = new Color(0.72f, 0.72f, 0.75f);

    [SerializeField] private Color skillColourWoodcutting = new Color(0.22f, 0.78f, 0.28f);

    [SerializeField] private Color skillColourFishing = new Color(0.28f, 0.62f, 0.98f);

    [Tooltip("How strongly the bar background lerps toward the active gathering colour (0 = combat only).")]
    [SerializeField, Range(0f, 1f)]
    private float gatheringBackgroundTintStrength = 0.48f;

    [Tooltip("W/M/F button alpha when this strip is the one shown (1 = fully opaque).")]
    [SerializeField, Range(0f, 1f)]
    private float gatheringStripButtonActiveAlpha = 1f;

    [Tooltip("W/M/F button alpha for the two strips that are not selected.")]
    [SerializeField, Range(0f, 1f)]
    private float gatheringStripButtonInactiveAlpha = 0.42f;

    [Header("Combat loadout set buttons (Set 1 / Set 2)")]
    [Tooltip("Background / button colours while that weapon set is active. Tweaked by LoadoutSetButtonBinder.")]
    [SerializeField]
    private Color combatSetActiveBackgroundColor = new Color(0.97f, 0.82f, 0.34f, 1f);

    [SerializeField]
    private Color combatSetInactiveBackgroundColor = new Color(1f, 1f, 1f, 0.45f);

    [SerializeField]
    private Color combatSetActiveTextColor = new Color(0.12f, 0.10f, 0.05f, 1f);

    [SerializeField]
    private Color combatSetInactiveTextColor = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("Optional explicit Set 1 button reference for LoadoutSetButtonBinder.")]
    [SerializeField]
    private Button combatSetOneButton;

    [Tooltip("Optional explicit Set 2 button reference for LoadoutSetButtonBinder.")]
    [SerializeField]
    private Button combatSetTwoButton;

    internal Color CombatSetActiveBackgroundColor => combatSetActiveBackgroundColor;
    internal Color CombatSetInactiveBackgroundColor => combatSetInactiveBackgroundColor;
    internal Color CombatSetActiveTextColor => combatSetActiveTextColor;
    internal Color CombatSetInactiveTextColor => combatSetInactiveTextColor;
    internal Button CombatSetOneButton => combatSetOneButton;
    internal Button CombatSetTwoButton => combatSetTwoButton;

    [Header("Saved State (backing fields)")]
    private List<SavedSlotState> savedSlots = new();
    private List<SavedSlotState> secondarySavedSlots = new();
    [SerializeField] private int activeCombatLoadoutSetIndex = 0; // 0 = set 1, 1 = set 2
    private bool suppressSaveForLoadoutSwap;
    private bool pendingSavedStateApply;
    private float nextSavedStateApplyTime;
    private int savedStateApplyAttempts;

    private readonly List<SavedSlotState> gatheringSlotsWoodcutting = new();
    private readonly List<SavedSlotState> gatheringSlotsMining = new();
    private readonly List<SavedSlotState> gatheringSlotsFishing = new();
    private bool gatheringUiActive;
    private SkillType gatheringSkillShown;
    private readonly List<SavedSlotState> frozenCombatFiveAbilities = new();
    private bool _manualGatheringStripLocked;

    private readonly Dictionary<string, bool> _abilityUnlockCache = new(StringComparer.OrdinalIgnoreCase);

    private bool _gatheringVisualDefaultsCached;
    private Color _combatBackgroundColor = Color.white;
    private Color _combatWoodGraphicColor = Color.white;
    private Color _combatMiningGraphicColor = Color.white;
    private Color _combatFishGraphicColor = Color.white;

    private void Awake()
    {
        ResolveCoreRefs();

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            binding.slot.Initialize(OnSlotTriggered, OnSlotAssignmentChanged, this);
        }

        SyncHotkeysFromManager();
        CacheGatheringStripVisualDefaults();
    }

    private void OnEnable()
    {
        if (HotkeyBindingManager.Instance != null)
            HotkeyBindingManager.Instance.OnBindingsChanged += SyncHotkeysFromManager;

        WireGatheringStripSelectionButtons();
        ResolveCoreRefs();
        SubscribeSkillUnlockCacheInvalidation();
    }

    private void OnDisable()
    {
        UnwireGatheringStripSelectionButtons();

        if (HotkeyBindingManager.Instance != null)
            HotkeyBindingManager.Instance.OnBindingsChanged -= SyncHotkeysFromManager;

        UnsubscribeSkillUnlockCacheInvalidation();
    }

    private void SubscribeSkillUnlockCacheInvalidation()
    {
        if (skillsManager == null)
            return;

        skillsManager.OnSkillAbilityRowPickChanged += HandleSkillAbilityRowPickChanged;
        skillsManager.OnSkillAbilityRowPickChanged += HandleSkillUnlockCacheInvalidated;
        skillsManager.OnSkillChoiceSelectionChanged += HandleSkillUnlockCacheInvalidated;
        skillsManager.OnLevelUp += HandleSkillUnlockCacheInvalidated;
        skillsManager.OnSkillLevelDecreased += HandleSkillUnlockCacheInvalidated;
    }

    private void UnsubscribeSkillUnlockCacheInvalidation()
    {
        if (skillsManager == null)
            return;

        skillsManager.OnSkillAbilityRowPickChanged -= HandleSkillAbilityRowPickChanged;
        skillsManager.OnSkillAbilityRowPickChanged -= HandleSkillUnlockCacheInvalidated;
        skillsManager.OnSkillChoiceSelectionChanged -= HandleSkillUnlockCacheInvalidated;
        skillsManager.OnLevelUp -= HandleSkillUnlockCacheInvalidated;
        skillsManager.OnSkillLevelDecreased -= HandleSkillUnlockCacheInvalidated;
    }

    private void HandleSkillUnlockCacheInvalidated(SkillType skillType, int _) => InvalidateAbilityUnlockCache();

    private void HandleSkillUnlockCacheInvalidated(SkillType skillType, int _, int __) => InvalidateAbilityUnlockCache();

    private void InvalidateAbilityUnlockCache() => _abilityUnlockCache.Clear();

    private bool IsAbilityLockedOnBar(AbilityDefinition abilityDef)
    {
        if (abilityDef == null || string.IsNullOrWhiteSpace(abilityDef.abilityId))
            return true;

        if (_abilityUnlockCache.TryGetValue(abilityDef.abilityId, out bool cached))
            return cached;

        SkillDefinition skill = skillDatabase != null ? skillDatabase.Get(abilityDef.sourceSkill) : null;
        bool locked = !SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, abilityDef, skillsManager);
        _abilityUnlockCache[abilityDef.abilityId] = locked;
        return locked;
    }

    private void HandleSkillAbilityRowPickChanged(SkillType skillType, int requiredLevel, int pickIndex)
    {
        if (pickIndex < 0)
            return;

        TryAutoEquipRowPickToStaleLoadoutSlot(skillType, requiredLevel, pickIndex);
    }

    /// <summary>
    /// When a skill-tree row pick changes, replace the loadout slot that held another sibling on that tier (or a stale unlearned ability).
    /// </summary>
    private bool TryAutoEquipRowPickToStaleLoadoutSlot(SkillType skillType, int requiredLevel, int pickIndex)
    {
        ResolveCoreRefs();
        if (skillDatabase == null || skillsManager == null)
            return false;

        SkillDefinition skill = skillDatabase.Get(skillType);
        if (skill == null)
            return false;

        List<AbilityDefinition> siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(skill, requiredLevel);
        if (pickIndex < 0 || pickIndex >= siblings.Count)
            return false;

        AbilityDefinition def = siblings[pickIndex];
        if (def == null || string.IsNullOrWhiteSpace(def.abilityId))
            return false;

        if (!SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(skill, def, skillsManager))
            return false;

        int tierIndex = SkillAbilityCommitRules.GetAbilityTierLoadoutIndex(skill, requiredLevel);
        if (tierIndex < 0)
            return false;

        if (IsGatheringSkillType(skillType))
        {
            if (gatheringUiActive && gatheringSkillShown == skillType)
                return TryReplaceStaleLoadoutSlotAtTierIndex(tierIndex, def, skill, requiredLevel);

            return TryReplaceStaleSavedGatheringSlot(skillType, tierIndex, def, skill, requiredLevel);
        }

        if (gatheringUiActive)
            return TryReplaceStaleFrozenCombatSlotAtTierIndex(tierIndex, def, skill, requiredLevel);

        return TryReplaceStaleLoadoutSlotAtTierIndex(tierIndex, def, skill, requiredLevel);
    }

    private bool TryReplaceStaleLoadoutSlotAtTierIndex(int tierIndex, AbilityDefinition def, SkillDefinition skill, int requiredLevel)
    {
        ActionBarSlotUI slot = FindLoadoutSlotHoldingRowSibling(skill, requiredLevel)
            ?? GetLoadoutSlotAtTierIndex(tierIndex);
        if (slot == null || !ShouldReplaceLoadoutSlotForRowPick(slot, skill, requiredLevel))
            return false;

        ActionBarAssignment assignment = BuildAbilityAssignment(def);
        if (!slot.CanAccept(assignment))
            return false;

        if (!slot.TryPaletteAssignAbilityWithUniqueSwap(assignment))
            return false;

        RefreshSlotRuntime(slot);
        return true;
    }

    private bool TryReplaceStaleFrozenCombatSlotAtTierIndex(int tierIndex, AbilityDefinition def, SkillDefinition skill, int requiredLevel)
    {
        int frozenIndex = FindFrozenCombatSlotIndexHoldingRowSibling(skill, requiredLevel);
        if (frozenIndex < 0)
            frozenIndex = tierIndex;

        if (frozenIndex < 0 || frozenIndex >= frozenCombatFiveAbilities.Count)
            return false;

        SavedSlotState st = frozenCombatFiveAbilities[frozenIndex];
        if (st == null || string.IsNullOrWhiteSpace(st.id))
            return false;

        if (!ShouldReplaceSavedAbilityForRowPick(st.id, skill, requiredLevel))
            return false;

        st.id = def.abilityId;
        st.kind = (int)ActionBarAssignmentKind.Ability;
        st.amount = 0;
        return true;
    }

    private bool TryReplaceStaleSavedGatheringSlot(SkillType skillType, int tierIndex, AbilityDefinition def, SkillDefinition skill, int requiredLevel)
    {
        List<SavedSlotState> list = GetGatheringListForSkill(skillType);
        if (list == null)
            return false;

        int savedIndex = FindSavedGatheringSlotIndexHoldingRowSibling(list, skill, requiredLevel);
        if (savedIndex < 0)
            savedIndex = tierIndex;

        if (savedIndex < 0 || savedIndex >= list.Count)
            return false;

        SavedSlotState st = list[savedIndex];
        if (st == null || string.IsNullOrWhiteSpace(st.id))
            return false;

        if (!ShouldReplaceSavedAbilityForRowPick(st.id, skill, requiredLevel))
            return false;

        st.id = def.abilityId;
        st.kind = (int)ActionBarAssignmentKind.Ability;
        st.amount = 0;
        return true;
    }

    private ActionBarSlotUI GetLoadoutSlotAtTierIndex(int tierIndex)
    {
        if (tierIndex < 0)
            return null;

        int ordinal = 0;
        foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
        {
            if (ordinal == tierIndex)
                return slot;
            ordinal++;
        }

        return null;
    }

    private bool ShouldReplaceLoadoutSlotForRowPick(ActionBarSlotUI slot, SkillDefinition rowSkill, int requiredLevel)
    {
        if (slot == null || rowSkill == null)
            return false;

        ActionBarAssignment action = slot.AssignedAction;
        if (action == null || !action.IsAssigned || !action.IsAbility)
            return false;

        if (IsSavedAbilityStaleFromUnlearn(action.id, rowSkill))
            return true;

        return IsSavedAbilitySiblingOnRow(action.id, rowSkill, requiredLevel);
    }

    private bool ShouldReplaceSavedAbilityForRowPick(string abilityId, SkillDefinition rowSkill, int requiredLevel)
    {
        if (IsSavedAbilityStaleFromUnlearn(abilityId, rowSkill))
            return true;

        return IsSavedAbilitySiblingOnRow(abilityId, rowSkill, requiredLevel);
    }

    private static bool IsSavedAbilitySiblingOnRow(string abilityId, SkillDefinition rowSkill, int requiredLevel)
    {
        if (string.IsNullOrWhiteSpace(abilityId) || rowSkill == null)
            return false;

        List<AbilityDefinition> siblings = SkillAbilityCommitRules.GetAbilitySiblingsOnSkillRow(rowSkill, requiredLevel);
        if (siblings.Count < 2)
            return false;

        for (int i = 0; i < siblings.Count; i++)
        {
            AbilityDefinition sibling = siblings[i];
            if (sibling == null)
                continue;

            if (string.Equals(sibling.abilityId, abilityId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private ActionBarSlotUI FindLoadoutSlotHoldingRowSibling(SkillDefinition skill, int requiredLevel)
    {
        if (skill == null)
            return null;

        foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
        {
            ActionBarAssignment action = slot?.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsAbility)
                continue;

            if (IsSavedAbilitySiblingOnRow(action.id, skill, requiredLevel))
                return slot;
        }

        return null;
    }

    private int FindFrozenCombatSlotIndexHoldingRowSibling(SkillDefinition skill, int requiredLevel)
    {
        for (int i = 0; i < frozenCombatFiveAbilities.Count; i++)
        {
            SavedSlotState st = frozenCombatFiveAbilities[i];
            if (st == null || string.IsNullOrWhiteSpace(st.id))
                continue;

            if (IsSavedAbilitySiblingOnRow(st.id, skill, requiredLevel))
                return i;
        }

        return -1;
    }

    private static int FindSavedGatheringSlotIndexHoldingRowSibling(List<SavedSlotState> list, SkillDefinition skill, int requiredLevel)
    {
        if (list == null || skill == null)
            return -1;

        for (int i = 0; i < list.Count; i++)
        {
            SavedSlotState st = list[i];
            if (st == null || string.IsNullOrWhiteSpace(st.id))
                continue;

            if (IsSavedAbilitySiblingOnRow(st.id, skill, requiredLevel))
                return i;
        }

        return -1;
    }

    private bool IsSavedAbilityStaleFromUnlearn(string abilityId, SkillDefinition rowSkill)
    {
        if (string.IsNullOrWhiteSpace(abilityId) || rowSkill == null)
            return false;

        AbilityDefinition abilityDef = GetAbilityDefinition(abilityId);
        if (abilityDef == null)
            return false;

        SkillDefinition abilitySkill = skillDatabase != null ? skillDatabase.Get(abilityDef.sourceSkill) : null;
        if (abilitySkill == null || abilitySkill.skillType != rowSkill.skillType)
            return false;

        return !SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(abilitySkill, abilityDef, skillsManager);
    }

    private static ActionBarAssignment BuildAbilityAssignment(AbilityDefinition def)
    {
        return ActionBarAssignment.CreateAbility(
            def.abilityId,
            SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def),
            SkillsAbilityPresentationResolver.ResolveAbilityIcon(def),
            SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def) ?? string.Empty);
    }

    private void WireGatheringStripSelectionButtons()
    {
        WireGatheringButton(gatheringWoodStripButton, OnGatheringWoodStripButtonClicked);
        WireGatheringButton(gatheringMiningStripButton, OnGatheringMiningStripButtonClicked);
        WireGatheringButton(gatheringFishingStripButton, OnGatheringFishStripButtonClicked);
    }

    private void UnwireGatheringStripSelectionButtons()
    {
        UnwireGatheringButton(gatheringWoodStripButton, OnGatheringWoodStripButtonClicked);
        UnwireGatheringButton(gatheringMiningStripButton, OnGatheringMiningStripButtonClicked);
        UnwireGatheringButton(gatheringFishingStripButton, OnGatheringFishStripButtonClicked);
    }

    private static void WireGatheringButton(Button button, UnityEngine.Events.UnityAction handler)
    {
        if (button == null || handler == null)
            return;
        button.onClick.RemoveListener(handler);
        button.onClick.AddListener(handler);
    }

    private static void UnwireGatheringButton(Button button, UnityEngine.Events.UnityAction handler)
    {
        if (button == null || handler == null)
            return;
        button.onClick.RemoveListener(handler);
    }

    private void OnGatheringWoodStripButtonClicked() =>
        ShowGatheringBarForSkill(SkillType.Woodcutting, GatheringBarDriveKind.ManualStripButton);

    private void OnGatheringMiningStripButtonClicked() =>
        ShowGatheringBarForSkill(SkillType.Mining, GatheringBarDriveKind.ManualStripButton);

    private void OnGatheringFishStripButtonClicked() =>
        ShowGatheringBarForSkill(SkillType.Fishing, GatheringBarDriveKind.ManualStripButton);

    private void Start()
    {
        QueueSavedStateApply();

        if (SaveManager.Instance != null &&
            SaveManager.Instance.TryGetLastLoadedData(out SaveData data))
        {
            LoadFrom(data);
        }

        SyncHotkeysFromManager();
        EquipmentManager equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (equipment != null)
            activeCombatLoadoutSetIndex = equipment.ActiveWeaponSetIndex == 1 ? 1 : 0;
    }

    /// <summary>Re-read hotkey manager and push labels to all slots (call after changing <see cref="ActionBarSlotUI"/> slot indices in the editor).</summary>
    public void RefreshHotkeyLabels() => SyncHotkeysFromManager();

    private void SyncHotkeysFromManager()
    {
        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            int hotkeyOrder = ResolveHotkeyOrderIndex(i, binding.slot);
            KeyCode k;
            if (hotkeyOrder < HotkeyBindIds.ActionBarSlotCount)
            {
                k = mgr != null
                    ? mgr.GetBinding(HotkeyBindIds.FromActionBarOrder(hotkeyOrder))
                    : HotkeyBindingManager.GetDefaultKey(HotkeyBindIds.FromActionBarOrder(hotkeyOrder));
            }
            else
            {
                k = binding.defaultKey;
            }

            binding.currentKey = k;
            binding.slot.SetHotkeyLabel(HotkeyBindingManager.GetDisplayString(k));
        }
    }

    /// <summary>
    /// Uses <see cref="ActionBarSlotUI.SlotIndex"/> for ActionBar1–7 when it is in range and unique among earlier bindings;
    /// otherwise falls back to list order (preserves older scenes where every slot used slot index 0).
    /// </summary>
    private int ResolveHotkeyOrderIndex(int listIndex, ActionBarSlotUI slot)
    {
        if (slot == null)
            return listIndex;

        int s = slot.SlotIndex;
        if (s < 0 || s >= HotkeyBindIds.ActionBarSlotCount)
            return listIndex;

        for (int j = 0; j < listIndex; j++)
        {
            ActionBarSlotUI earlier = slotBindings[j] != null ? slotBindings[j].slot : null;
            if (earlier != null && earlier.SlotIndex == s)
                return listIndex;
        }

        return s;
    }

    private void Update()
    {
        // Allow action bar hotkeys even when windows are open.
        // Only block while rebinding, on suppress frame, or when typing into a text field.
        bool blockHotkeyPoll = HelperGameplayController.BlocksStripGameplay ||
                               HotkeySettingsRowUI.IsRebinding ||
                               IsTypingIntoInputField() ||
                               Time.frameCount == HotkeySettingsRowUI.SuppressActionBarHotkeyPollFrame;

        bool refreshRuntimeVisuals = Time.time >= _nextSlotRuntimeRefreshAt;
        if (refreshRuntimeVisuals)
        {
            _nextSlotRuntimeRefreshAt = Time.time + Mathf.Max(0.02f, slotRuntimeRefreshInterval);
            ResolveCoreRefs();
        }

        bool whirlwindHeld = false;
        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            ActionBarAssignment action = binding.slot.AssignedAction;
            bool boundWhirlwind = action != null &&
                                  action.IsAbility &&
                                  string.Equals(action.id, AbilityCombatPower.WhirlwindAbilityId, StringComparison.OrdinalIgnoreCase);
            if (boundWhirlwind &&
                !blockHotkeyPoll &&
                binding.currentKey != KeyCode.None &&
                Input.GetKey(binding.currentKey))
            {
                whirlwindHeld = true;
            }

            if (!blockHotkeyPoll &&
                binding.currentKey != KeyCode.None &&
                Input.GetKeyDown(binding.currentKey))
            {
                binding.slot.Press();
            }

            if (refreshRuntimeVisuals)
                RefreshSlotRuntime(binding.slot);
        }

        if (abilityController != null)
            abilityController.SetWhirlwindActionBarHeld(whirlwindHeld);

        TryApplyPendingSavedState();
    }

    private static bool IsTypingIntoInputField()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponent<InputField>() != null;
    }

    private void OnSlotTriggered(ActionBarSlotUI slot)
    {
        if (slot == null)
            return;

        ActionBarAssignment action = slot.AssignedAction;

        if (action == null || !action.IsAssigned)
        {
            return;
        }

        switch (action.kind)
        {
            case ActionBarAssignmentKind.Ability:
                ResolveCoreRefs();
                if (abilityController == null)
                    return;

                AbilityDefinition abilityDef = GetAbilityDefinition(action.id);
                if (!abilityController.TryPrepareKeyboardModeAbilityTarget(action.id))
                    return;

                bool usedAbility = abilityController.TryUseAbility(action.id);
                if (usedAbility &&
                    abilityDef != null &&
                    IsGatheringSkillType(abilityDef.sourceSkill))
                {
                    if (_player != null)
                        _player.ClearActionOverride();
                    ShowGatheringBarForSkill(abilityDef.sourceSkill, GatheringBarDriveKind.AutomaticGameplay);
                }

                RefreshSlotRuntime(slot);
                break;

            case ActionBarAssignmentKind.Item:
                if (consumableController == null)
                    return;

                if (slot.AssignedItemAmount <= 0)
                {
                    slot.SetNoStockVisual(true);
                    return;
                }

                bool used = consumableController.TryUseItem(action.id);

                RefreshSlotRuntime(slot);
                break;
        }
    }

    private void OnSlotAssignmentChanged(ActionBarSlotUI slot)
    {
        InvalidateAbilityUnlockCache();
        CaptureSlotsToSavedState();

        if (!suppressSaveForLoadoutSwap && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        NotifyPlayerStatsCombatPowerRelevantChange();
    }

    /// <summary>
    /// Used when multiple <see cref="ActionBarUI"/> instances exist (DDOL + scene HUD): <see cref="SaveManager"/>
    /// keeps the richest snapshot so an empty duplicate cannot wipe consumables on disk.
    /// </summary>
    public int ComputeSavePriorityScore()
    {
        CaptureSlotsToSavedState();
        int score = 0;
        for (int i = 0; i < savedSlots.Count; i++)
        {
            SavedSlotState e = savedSlots[i];
            if (string.IsNullOrWhiteSpace(e.id))
                continue;
            if (e.kind == (int)ActionBarAssignmentKind.Item)
                score += Mathf.Max(1, e.amount);
            else
                score += 1;
        }

        return score;
    }

    private void CaptureSlotsToSavedState()
    {
        if (gatheringUiActive)
        {
            RebuildGatheringListFromUi(GetGatheringListForSkill(gatheringSkillShown));
            WriteCombatSavedSlotsFromFrozenAbilitiesAndRestOfBarFromUi();
            return;
        }

        savedSlots.Clear();

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            ActionBarAssignment action = binding.slot.AssignedAction;
            if (action == null || !action.IsAssigned)
                continue;

            savedSlots.Add(new SavedSlotState
            {
                slotIndex = binding.slot.SlotIndex,
                kind = (int)action.kind,
                id = action.id,
                amount = action.IsItem ? binding.slot.AssignedItemAmount : 0
            });
        }
    }

    private List<SavedSlotState> GetGatheringListForSkill(SkillType skillType)
    {
        switch (skillType)
        {
            case SkillType.Woodcutting:
                return gatheringSlotsWoodcutting;
            case SkillType.Mining:
                return gatheringSlotsMining;
            case SkillType.Fishing:
                return gatheringSlotsFishing;
            default:
                return gatheringSlotsWoodcutting;
        }
    }

    private void RebuildGatheringListFromUi(List<SavedSlotState> target)
    {
        if (target == null)
            return;

        target.Clear();
        foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
        {
            SavedSlotState cap = CaptureSlotState(slot);
            if (cap != null)
                target.Add(CloneSavedState(cap));
        }
    }

    private void ApplyGatheringAbilityRowsToUi(List<SavedSlotState> fromList)
    {
        var byIndex = new Dictionary<int, SavedSlotState>();
        if (fromList != null)
        {
            for (int i = 0; i < fromList.Count; i++)
            {
                SavedSlotState e = fromList[i];
                if (e == null || string.IsNullOrWhiteSpace(e.id))
                    continue;
                byIndex[e.slotIndex] = e;
            }
        }

        foreach (ActionBarSlotUI slot in EnumerateFirstFiveLoadoutAbilitySlots())
        {
            if (byIndex.TryGetValue(slot.SlotIndex, out SavedSlotState st))
                ApplySavedStateToSlot(slot, st);
            else
                slot.ClearAssignment(false);
        }

        for (int i = 0; i < slotBindings.Count; i++)
        {
            ActionBarSlotUI s = slotBindings[i]?.slot;
            if (s != null)
                RefreshSlotRuntime(s);
        }
    }

    private IEnumerable<ActionBarSlotUI> EnumerateFirstFiveLoadoutAbilitySlots()
    {
        int abilityOrdinal = 0;
        for (int i = 0; i < slotBindings.Count; i++)
        {
            ActionBarSlotUI slot = slotBindings[i]?.slot;
            if (slot == null || slot.SlotType != ActionBarSlotType.Ability)
                continue;

            if (abilityOrdinal < 5)
            {
                yield return slot;
                abilityOrdinal++;
            }
        }
    }

    private void WriteCombatSavedSlotsFromFrozenAbilitiesAndRestOfBarFromUi()
    {
        savedSlots.Clear();

        for (int i = 0; i < frozenCombatFiveAbilities.Count; i++)
        {
            SavedSlotState e = frozenCombatFiveAbilities[i];
            if (e != null)
                savedSlots.Add(CloneSavedState(e));
        }

        for (int i = 0; i < slotBindings.Count; i++)
        {
            ActionBarSlotUI slot = slotBindings[i]?.slot;
            if (slot == null)
                continue;

            if (IsLoadoutAbilitySlot(slot))
                continue;

            ActionBarAssignment action = slot.AssignedAction;
            if (action == null || !action.IsAssigned)
                continue;

            savedSlots.Add(new SavedSlotState
            {
                slotIndex = slot.SlotIndex,
                kind = (int)action.kind,
                id = action.id,
                amount = action.IsItem ? slot.AssignedItemAmount : 0
            });
        }
    }

    private static SavedSlotState CloneSavedState(SavedSlotState src)
    {
        if (src == null)
            return null;

        return new SavedSlotState
        {
            slotIndex = src.slotIndex,
            kind = src.kind,
            id = src.id,
            amount = src.amount
        };
    }

    private SavedSlotState CaptureSlotState(ActionBarSlotUI slot)
    {
        if (slot == null)
            return null;

        ActionBarAssignment action = slot.AssignedAction;
        if (action == null || !action.IsAssigned)
            return null;

        return new SavedSlotState
        {
            slotIndex = slot.SlotIndex,
            kind = (int)action.kind,
            id = action.id,
            amount = action.IsItem ? slot.AssignedItemAmount : 0
        };
    }

    private bool IsLoadoutAbilitySlot(ActionBarSlotUI slot)
    {
        if (slot == null || slot.SlotType != ActionBarSlotType.Ability)
            return false;

        int abilityOrdinal = 0;
        for (int i = 0; i < slotBindings.Count; i++)
        {
            ActionBarSlotUI candidate = slotBindings[i]?.slot;
            if (candidate == null || candidate.SlotType != ActionBarSlotType.Ability)
                continue;
            if (candidate == slot)
                return abilityOrdinal < 5;
            abilityOrdinal++;
        }

        return false;
    }

    private bool IsLoadoutPotionSlot(ActionBarSlotUI slot)
    {
        if (slot == null || slot.SlotType != ActionBarSlotType.Potion)
            return false;

        for (int i = 0; i < slotBindings.Count; i++)
        {
            ActionBarSlotUI candidate = slotBindings[i]?.slot;
            if (candidate == null || candidate.SlotType != ActionBarSlotType.Potion)
                continue;
            return candidate == slot;
        }

        return false;
    }

    private bool IsSwappableCombatLoadoutSlot(ActionBarSlotUI slot)
    {
        return IsLoadoutAbilitySlot(slot) || IsLoadoutPotionSlot(slot);
    }

    private void ApplySavedStateToSlot(ActionBarSlotUI slot, SavedSlotState state)
    {
        if (slot == null)
            return;

        if (state == null)
        {
            slot.ClearAssignment(false);
            return;
        }

        ActionBarAssignment assignment = ResolveAssignment(state.kind, state.id);
        if (assignment == null || !assignment.IsAssigned)
        {
            slot.ClearAssignment(false);
            return;
        }

        slot.Assign(assignment, false);
        if (assignment.IsItem)
        {
            int savedAmount = Mathf.Max(0, state.amount);
            if (savedAmount <= 0)
                savedAmount = 1;
            slot.SetAssignedItemAmountFromSave(savedAmount, notify: false);
        }
    }

    public void ToggleCombatLoadoutSet()
    {
        int nextSet = activeCombatLoadoutSetIndex == 0 ? 1 : 0;
        SetCombatLoadoutSet(nextSet);
    }

    public void SetCombatLoadoutSet(int setIndex)
    {
        ExitGatheringBarToCombat();

        int nextSet = setIndex == 1 ? 1 : 0;
        if (nextSet == activeCombatLoadoutSetIndex)
            return;

        ResolveCoreRefs();
        suppressSaveForLoadoutSwap = true;
        try
        {

            var swappableSlots = slotBindings
            .Select(b => b?.slot)
            .Where(s => s != null && IsSwappableCombatLoadoutSlot(s))
            .ToList();
            if (swappableSlots.Count <= 0)
                return;

            var currentBySlotIndex = new Dictionary<int, SavedSlotState>();
            for (int i = 0; i < swappableSlots.Count; i++)
            {
                ActionBarSlotUI slot = swappableSlots[i];
                currentBySlotIndex[slot.SlotIndex] = CaptureSlotState(slot);
            }

            var secondaryBySlotIndex = new Dictionary<int, SavedSlotState>();
            for (int i = 0; i < secondarySavedSlots.Count; i++)
            {
                SavedSlotState entry = secondarySavedSlots[i];
                if (entry == null)
                    continue;
                secondaryBySlotIndex[entry.slotIndex] = entry;
            }

            secondarySavedSlots.Clear();
            for (int i = 0; i < swappableSlots.Count; i++)
            {
                ActionBarSlotUI slot = swappableSlots[i];
                currentBySlotIndex.TryGetValue(slot.SlotIndex, out SavedSlotState currentState);
                if (currentState != null)
                    secondarySavedSlots.Add(CloneSavedState(currentState));
            }

            for (int i = 0; i < swappableSlots.Count; i++)
            {
                ActionBarSlotUI slot = swappableSlots[i];
                secondaryBySlotIndex.TryGetValue(slot.SlotIndex, out SavedSlotState secondaryState);
                ApplySavedStateToSlot(slot, secondaryState);
            }

            CaptureSlotsToSavedState();
            activeCombatLoadoutSetIndex = nextSet;
        }
        finally
        {
            suppressSaveForLoadoutSwap = false;
        }
    }

    private int ApplySavedStateToSlots()
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            if (slotBindings[i]?.slot != null)
                slotBindings[i].slot.ClearAssignment(false);
        }

        int unresolved = 0;

        for (int i = 0; i < savedSlots.Count; i++)
        {
            SavedSlotState saved = savedSlots[i];
            ActionBarSlotUI slot = GetSlotByIndex(saved.slotIndex);
            if (slot == null)
            {
                continue;
            }

            ActionBarAssignment assignment = ResolveAssignment(saved.kind, saved.id);
            if (assignment == null || !assignment.IsAssigned)
            {
                unresolved++;
                continue;
            }

            slot.Assign(assignment, false);
            if (assignment.IsItem)
            {
                int savedAmount = Mathf.Max(0, saved.amount);
                if (savedAmount <= 0)
                    savedAmount = 1; // legacy saves had ids only; keep slot usable
                slot.SetAssignedItemAmountFromSave(savedAmount, notify: false);
            }
        }

        return unresolved;
    }

    private ActionBarAssignment ResolveAssignment(int kindInt, string id)
    {
        ActionBarAssignmentKind kind = (ActionBarAssignmentKind)kindInt;

        switch (kind)
        {
            case ActionBarAssignmentKind.Item:
                ResolveCoreRefs();

                if (string.IsNullOrWhiteSpace(id))
                    return null;

                string resolvedId = Inventory.RemapLegacyItemId(id);
                ItemDefinition def = inventory != null ? inventory.GetItemDef(resolvedId) : null;
                if (!def)
                {
                    if (itemDatabase == null)
                        itemDatabase = ResolveItemDatabase();
                    if (itemDatabase != null)
                        def = itemDatabase.Get(resolvedId);
                }
                if (!def)
                {
                    return null;
                }

                return ActionBarAssignment.CreateItem(def);

            case ActionBarAssignmentKind.Ability:
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                AbilityDefinition ability = GetAbilityDefinition(id);
                if (!ability)
                {
                    return null;
                }

                return ActionBarAssignment.CreateAbility(
                    ability.abilityId,
                    SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(ability),
                    SkillsAbilityPresentationResolver.ResolveAbilityIcon(ability),
                    SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(ability) ?? string.Empty
                );
        }

        return null;
    }

    private void RefreshSlotRuntime(ActionBarSlotUI slot)
    {
        if (slot == null)
            return;

        ActionBarAssignment action = slot.AssignedAction;
        if (action == null || !action.IsAssigned)
        {
            slot.SetStackText(0);
            slot.SetCooldownVisual(0f, 0f);
            slot.SetPrimedVisual(false);
            slot.SetNoStockVisual(false);
            slot.SetAbilityWeaponCompatibility(true);
            slot.SetAbilityBuffActiveOverlay(false);
            slot.SetAbilityBuffTimerDisplay(false, 0f);
            return;
        }

        if (!action.IsItem || inventory == null)
        {
            int abilityStackCount = 0;
            if (action.IsAbility && abilityController != null)
            {
                if (!abilityController.IsOnCooldown(action.id, out _))
                    abilityStackCount = abilityController.GetAbilityStackCountDisplay(action.id);
            }
            slot.SetStackText(abilityStackCount);

            if (action.IsAbility)
            {
                ResolveCoreRefs();
                AbilityDefinition abilityDef = GetAbilityDefinition(action.id);
                bool abilityLocked = IsAbilityLockedOnBar(abilityDef);

                if (abilityController != null)
                {
                    bool abilityOnCooldown = abilityController.IsOnCooldown(action.id, out float abilitySecs);
                    float abilityNorm = abilityOnCooldown
                        ? abilityController.GetCooldownNormalizedFromRemaining(action.id, abilitySecs)
                        : 0f;
                    float gcdNorm = abilityController.GetGlobalCooldownNormalized();
                    abilityController.IsOnGlobalCooldown(out float gcdSecs);

                    // Ability CD drives overlay + timer; GCD only when the ability itself is not on cooldown.
                    // (Previously picking max(norm) replaced a 6s/12s timer with the short GCD overlay.)
                    float overlayNorm;
                    float timerSeconds;
                    if (abilityOnCooldown)
                    {
                        overlayNorm = abilityNorm;
                        timerSeconds = abilitySecs;
                    }
                    else if (gcdNorm > 0f)
                    {
                        overlayNorm = gcdNorm;
                        timerSeconds = gcdSecs;
                    }
                    else
                    {
                        overlayNorm = 0f;
                        timerSeconds = 0f;
                    }

                    slot.SetCooldownVisual(overlayNorm, timerSeconds);

                    slot.SetPrimedVisual(abilityController.IsAbilityPrimed(action.id));
                    bool weaponOk = abilityController.CanUseAbilityWithCurrentWeapon(action.id);
                    slot.SetAbilityWeaponCompatibility(weaponOk);
                    // Same red overlay as "not available" when skill-locked or wrong weapon type.
                    slot.SetNoStockVisual(abilityLocked || !weaponOk);

                    float buffRemain = 0f;
                    bool buffTimedPresentation = !abilityOnCooldown &&
                                                 buffController != null &&
                                                 buffController.ShouldDisplayHudAbilityBuffTimedPresentation(
                                                     action.id, out buffRemain);
                    slot.SetAbilityBuffActiveOverlay(buffTimedPresentation);
                    slot.SetAbilityBuffTimerDisplay(buffTimedPresentation, buffRemain);
                }
                else
                {
                    slot.SetCooldownVisual(0f, 0f);
                    slot.SetPrimedVisual(false);
                    bool weaponOkNoController = true;
                    if (abilityDef != null && _player != null)
                    {
                        CharacterStats cs = _player.GetComponent<CharacterStats>();
                        if (cs != null)
                            weaponOkNoController = cs.IsAbilityUsableWithEquippedWeapon(abilityDef);
                    }
                    slot.SetAbilityWeaponCompatibility(weaponOkNoController);
                    slot.SetNoStockVisual(abilityLocked || !weaponOkNoController);

                    float buffRemainNoAb = 0f;
                    bool buffTimedNoAb = buffController != null &&
                                         buffController.ShouldDisplayHudAbilityBuffTimedPresentation(
                                             action.id, out buffRemainNoAb);
                    slot.SetAbilityBuffActiveOverlay(buffTimedNoAb);
                    slot.SetAbilityBuffTimerDisplay(buffTimedNoAb, buffRemainNoAb);
                }

                return;
            }

            slot.SetCooldownVisual(0f, 0f);
            slot.SetPrimedVisual(false);
            slot.SetNoStockVisual(false);
            slot.SetAbilityWeaponCompatibility(true);
            slot.SetAbilityBuffActiveOverlay(false);
            slot.SetAbilityBuffTimerDisplay(false, 0f);
            return;
        }

        int count = slot.AssignedItemAmount;
        slot.SetStackText(count);
        bool noStock = count <= 0 &&
                       (slot.SlotType == ActionBarSlotType.Food || slot.SlotType == ActionBarSlotType.Potion);
        slot.SetNoStockVisual(noStock);
        slot.SetAbilityWeaponCompatibility(true);
        slot.SetAbilityBuffActiveOverlay(false);
        slot.SetAbilityBuffTimerDisplay(false, 0f);

        if (consumableController != null)
        {
            float remainingNorm = consumableController.GetCooldownNormalized(action.id);
            consumableController.IsOnCooldown(action.id, out float remainingSecs);
            slot.SetCooldownVisual(remainingNorm, remainingSecs);
            slot.SetPrimedVisual(false);
        }
        else
        {
            slot.SetCooldownVisual(0f, 0f);
            slot.SetPrimedVisual(false);
        }
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        // Always snapshot live slot assignments right before serialization so level transitions
        // cannot persist stale cached state.
        CaptureSlotsToSavedState();

        data.actionBarSlotIndexes ??= new List<int>();
        data.actionBarKinds ??= new List<int>();
        data.actionBarIds ??= new List<string>();
        data.actionBarItemAmounts ??= new List<int>();

        data.actionBarSlotIndexes.Clear();
        data.actionBarKinds.Clear();
        data.actionBarIds.Clear();
        data.actionBarItemAmounts.Clear();
        data.actionBarSecondarySlotIndexes ??= new List<int>();
        data.actionBarSecondaryKinds ??= new List<int>();
        data.actionBarSecondaryIds ??= new List<string>();
        data.actionBarSecondaryItemAmounts ??= new List<int>();
        data.actionBarSecondarySlotIndexes.Clear();
        data.actionBarSecondaryKinds.Clear();
        data.actionBarSecondaryIds.Clear();
        data.actionBarSecondaryItemAmounts.Clear();

        for (int i = 0; i < savedSlots.Count; i++)
        {
            data.actionBarSlotIndexes.Add(savedSlots[i].slotIndex);
            data.actionBarKinds.Add(savedSlots[i].kind);
            data.actionBarIds.Add(savedSlots[i].id);
            data.actionBarItemAmounts.Add(Mathf.Max(0, savedSlots[i].amount));
        }

        for (int i = 0; i < secondarySavedSlots.Count; i++)
        {
            SavedSlotState e = secondarySavedSlots[i];
            if (e == null)
                continue;
            data.actionBarSecondarySlotIndexes.Add(e.slotIndex);
            data.actionBarSecondaryKinds.Add(e.kind);
            data.actionBarSecondaryIds.Add(e.id);
            data.actionBarSecondaryItemAmounts.Add(Mathf.Max(0, e.amount));
        }

        if (data.actionBarGatherWoodcutting == null)
            data.actionBarGatherWoodcutting = new SaveData.GatheringActionBarSaveBlock();
        if (data.actionBarGatherMining == null)
            data.actionBarGatherMining = new SaveData.GatheringActionBarSaveBlock();
        if (data.actionBarGatherFishing == null)
            data.actionBarGatherFishing = new SaveData.GatheringActionBarSaveBlock();

        WriteGatheringSaveBlock(data.actionBarGatherWoodcutting, gatheringSlotsWoodcutting);
        WriteGatheringSaveBlock(data.actionBarGatherMining, gatheringSlotsMining);
        WriteGatheringSaveBlock(data.actionBarGatherFishing, gatheringSlotsFishing);
    }

    public void LoadFrom(SaveData data)
    {
        savedSlots.Clear();
        secondarySavedSlots.Clear();
        gatheringSlotsWoodcutting.Clear();
        gatheringSlotsMining.Clear();
        gatheringSlotsFishing.Clear();
        gatheringUiActive = false;
        frozenCombatFiveAbilities.Clear();

        if (data == null)
            return;

        int count = Mathf.Min(
            data.actionBarSlotIndexes != null ? data.actionBarSlotIndexes.Count : 0,
            data.actionBarKinds != null ? data.actionBarKinds.Count : 0,
            data.actionBarIds != null ? data.actionBarIds.Count : 0
        );

        for (int i = 0; i < count; i++)
        {
            savedSlots.Add(new SavedSlotState
            {
                slotIndex = data.actionBarSlotIndexes[i],
                kind = data.actionBarKinds[i],
                id = data.actionBarIds[i],
                amount = data.actionBarItemAmounts != null && i < data.actionBarItemAmounts.Count
                    ? Mathf.Max(0, data.actionBarItemAmounts[i])
                    : 0
            });
        }

        int secondaryCount = Mathf.Min(
            data.actionBarSecondarySlotIndexes != null ? data.actionBarSecondarySlotIndexes.Count : 0,
            data.actionBarSecondaryKinds != null ? data.actionBarSecondaryKinds.Count : 0,
            data.actionBarSecondaryIds != null ? data.actionBarSecondaryIds.Count : 0
        );

        for (int i = 0; i < secondaryCount; i++)
        {
            secondarySavedSlots.Add(new SavedSlotState
            {
                slotIndex = data.actionBarSecondarySlotIndexes[i],
                kind = data.actionBarSecondaryKinds[i],
                id = data.actionBarSecondaryIds[i],
                amount = data.actionBarSecondaryItemAmounts != null && i < data.actionBarSecondaryItemAmounts.Count
                    ? Mathf.Max(0, data.actionBarSecondaryItemAmounts[i])
                    : 0
            });
        }

        ReadGatheringSaveBlock(data.actionBarGatherWoodcutting, gatheringSlotsWoodcutting);
        ReadGatheringSaveBlock(data.actionBarGatherMining, gatheringSlotsMining);
        ReadGatheringSaveBlock(data.actionBarGatherFishing, gatheringSlotsFishing);

        QueueSavedStateApply();
    }

    private static void WriteGatheringSaveBlock(SaveData.GatheringActionBarSaveBlock block, List<SavedSlotState> list)
    {
        if (block == null)
            return;

        block.slotIndexes ??= new List<int>();
        block.kinds ??= new List<int>();
        block.ids ??= new List<string>();
        block.itemAmounts ??= new List<int>();

        block.slotIndexes.Clear();
        block.kinds.Clear();
        block.ids.Clear();
        block.itemAmounts.Clear();

        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            SavedSlotState e = list[i];
            if (e == null || string.IsNullOrWhiteSpace(e.id))
                continue;
            block.slotIndexes.Add(e.slotIndex);
            block.kinds.Add(e.kind);
            block.ids.Add(e.id);
            block.itemAmounts.Add(Mathf.Max(0, e.amount));
        }
    }

    private static void ReadGatheringSaveBlock(SaveData.GatheringActionBarSaveBlock block, List<SavedSlotState> target)
    {
        if (target == null)
            return;

        target.Clear();
        if (block == null)
            return;

        int count = Mathf.Min(
            block.slotIndexes != null ? block.slotIndexes.Count : 0,
            block.kinds != null ? block.kinds.Count : 0,
            block.ids != null ? block.ids.Count : 0,
            block.itemAmounts != null ? block.itemAmounts.Count : 0);

        for (int i = 0; i < count; i++)
        {
            target.Add(new SavedSlotState
            {
                slotIndex = block.slotIndexes[i],
                kind = block.kinds[i],
                id = block.ids[i],
                amount = block.itemAmounts != null && i < block.itemAmounts.Count
                    ? Mathf.Max(0, block.itemAmounts[i])
                    : 0
            });
        }
    }

    private void QueueSavedStateApply()
    {
        pendingSavedStateApply = true;
        nextSavedStateApplyTime = 0f;
        savedStateApplyAttempts = 0;
    }

    private void TryApplyPendingSavedState()
    {
        if (!pendingSavedStateApply)
            return;

        if (Time.unscaledTime < nextSavedStateApplyTime)
            return;

        // Reacquire refs if scene/bootstrap order delayed setup.
        ResolveCoreRefs();

        int unresolved = ApplySavedStateToSlots();
        if (unresolved <= 0)
        {
            pendingSavedStateApply = false;
            NotifyPlayerStatsCombatPowerRelevantChange();
            return;
        }

        savedStateApplyAttempts++;
        // Keep retrying quietly; this avoids intermittent load order races.
        nextSavedStateApplyTime = Time.unscaledTime + 0.2f;
    }

    /// <summary>
    /// Slotted abilities affect <see cref="CharacterStats"/> combat power but not vitals; listeners (HUD, stats panel)
    /// only refresh when <see cref="CharacterStats.NotifyStatsChanged"/> runs.
    /// </summary>
    private void NotifyPlayerStatsCombatPowerRelevantChange()
    {
        ResolveCoreRefs();
        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc == null)
            return;
        CharacterStats s = pc.GetComponent<CharacterStats>();
        if (s != null)
            s.NotifyStatsChanged();
    }

    private void ResolveCoreRefs()
    {
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (_player != null)
        {
            if (inventory == null)
            {
                var playerInventory = _player.GetComponent<Inventory>();
                if (playerInventory != null)
                    inventory = playerInventory;
            }

            if (consumableController == null)
            {
                var playerConsumables = _player.GetComponent<PlayerConsumableController>();
                if (playerConsumables != null)
                    consumableController = playerConsumables;
            }

            if (abilityController == null)
            {
                var playerAbilities = _player.GetComponent<PlayerAbilityController>();
                if (playerAbilities == null)
                    playerAbilities = _player.gameObject.AddComponent<PlayerAbilityController>();
                if (playerAbilities != null)
                    abilityController = playerAbilities;
            }

            if (buffController == null)
            {
                var playerBuffs = _player.GetComponent<PlayerBuffController>();
                if (playerBuffs != null)
                    buffController = playerBuffs;
            }
        }

        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (itemDatabase == null)
            itemDatabase = ResolveItemDatabase();

        if (consumableController == null)
            consumableController = FindFirstObjectByType<PlayerConsumableController>(FindObjectsInactive.Include);

        if (abilityController == null)
            abilityController = FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);

        if (buffController == null)
            buffController = FindFirstObjectByType<PlayerBuffController>(FindObjectsInactive.Include);

        if (abilityDatabase == null)
            abilityDatabase = AbilityDatabase.LoadDefault();

        if (skillDatabase == null)
            skillDatabase = SkillDatabase.LoadDefault();

        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
    }

    private static ItemDatabase ResolveItemDatabase()
    {
        ItemDatabase db = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (db != null)
            return db;

        db = Resources.Load<ItemDatabase>("ItemDatabase");
        if (db != null)
            return db;

        ItemDatabase[] loaded = Resources.FindObjectsOfTypeAll<ItemDatabase>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null)
                return loaded[i];
        }

        return null;
    }

    private AbilityDefinition GetAbilityDefinition(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        if (abilityDatabase == null)
            abilityDatabase = AbilityDatabase.LoadDefault();
        return abilityDatabase ? abilityDatabase.Get(id) : null;
    }

    private ActionBarSlotUI GetSlotByIndex(int slotIndex)
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding != null && binding.slot != null && binding.slot.SlotIndex == slotIndex)
                return binding.slot;
        }

        return null;
    }

    private ActionBarSlotUI FindConsumableSlot(ItemDefinition def)
    {
        if (def == null)
            return null;

        ActionBarSlotType wantType = def.IsFood ? ActionBarSlotType.Food : ActionBarSlotType.Potion;
        foreach (ActionBarSlotUI slot in GetSlots())
        {
            if (slot != null && slot.SlotType == wantType)
                return slot;
        }

        return null;
    }

    public int CountSlottedItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        string remapped = Inventory.RemapLegacyItemId(itemId);
        int total = 0;
        foreach (ActionBarSlotUI slot in GetSlots())
        {
            if (slot == null)
                continue;
            ActionBarAssignment action = slot.AssignedAction;
            if (action == null || !action.IsItem || string.IsNullOrWhiteSpace(action.id))
                continue;
            if (string.Equals(Inventory.RemapLegacyItemId(action.id), remapped, System.StringComparison.OrdinalIgnoreCase))
                total += slot.AssignedItemAmount;
        }
        return total;
    }

    public bool TryConsumeSlottedItem(string itemId, int amount = 1)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return false;

        string remapped = Inventory.RemapLegacyItemId(itemId);
        foreach (ActionBarSlotUI slot in GetSlots())
        {
            if (slot == null)
                continue;
            ActionBarAssignment action = slot.AssignedAction;
            if (action == null || !action.IsItem || string.IsNullOrWhiteSpace(action.id))
                continue;
            if (!string.Equals(Inventory.RemapLegacyItemId(action.id), remapped, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (slot.AssignedItemAmount < amount)
                continue;

            return slot.TryConsumeStoredItem(amount);
        }
        return false;
    }

    /// <summary>Legacy API: rebinds by <b>slot list order</b> (same as <see cref="HotkeyBindId"/> for indices 0–6).</summary>
    public void RebindKey(ActionBarSlotUI slot, KeyCode newKey)
    {
        int listIndex = GetSlotOrderIndex(slot);
        if (listIndex < 0)
            return;

        int order = ResolveHotkeyOrderIndex(listIndex, slot);
        if (order < 0 || order >= HotkeyBindIds.ActionBarSlotCount)
            return;

        HotkeyBindingManager.Instance?.TrySetBinding(HotkeyBindIds.FromActionBarOrder(order), newKey);
    }

    private int GetSlotOrderIndex(ActionBarSlotUI slot)
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            if (slotBindings[i]?.slot == slot)
                return i;
        }

        return -1;
    }
}