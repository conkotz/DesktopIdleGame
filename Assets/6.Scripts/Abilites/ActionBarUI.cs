using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    public string GetHotkeyDisplayString(KeyCode key) => HotkeyBindingManager.GetDisplayString(key);

    public IEnumerable<ActionBarSlotUI> GetSlots()
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            if (slotBindings[i] != null && slotBindings[i].slot != null)
                yield return slotBindings[i].slot;
        }
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

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private PlayerConsumableController consumableController;
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private PlayerBuffController buffController;
    [SerializeField] private AbilityDatabase abilityDatabase;
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;

    [Header("Saved State (backing fields)")]
    private List<SavedSlotState> savedSlots = new();
    private List<SavedSlotState> secondarySavedSlots = new();
    [SerializeField] private int activeCombatLoadoutSetIndex = 0; // 0 = set 1, 1 = set 2
    private bool suppressSaveForLoadoutSwap;
    private bool pendingSavedStateApply;
    private float nextSavedStateApplyTime;
    private int savedStateApplyAttempts;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugEmptySlots = false;

    private void Awake()
    {
        ResolveCoreRefs();

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            binding.slot.Initialize(OnSlotTriggered, OnSlotAssignmentChanged);
        }

        SyncHotkeysFromManager();
    }

    private void OnEnable()
    {
        if (HotkeyBindingManager.Instance != null)
            HotkeyBindingManager.Instance.OnBindingsChanged += SyncHotkeysFromManager;
    }

    private void OnDisable()
    {
        if (HotkeyBindingManager.Instance != null)
            HotkeyBindingManager.Instance.OnBindingsChanged -= SyncHotkeysFromManager;
    }

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

    private void SyncHotkeysFromManager()
    {
        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            KeyCode k;
            if (i < HotkeyBindIds.ActionBarSlotCount)
            {
                k = mgr != null
                    ? mgr.GetBinding(HotkeyBindIds.FromActionBarOrder(i))
                    : HotkeyBindingManager.GetDefaultKey(HotkeyBindIds.FromActionBarOrder(i));
            }
            else
            {
                k = binding.defaultKey;
            }

            binding.currentKey = k;
            binding.slot.SetHotkeyLabel(HotkeyBindingManager.GetDisplayString(k));
        }
    }

    private void Update()
    {
        // Allow action bar hotkeys even when windows are open.
        // Only block while rebinding, on suppress frame, or when typing into a text field.
        bool blockHotkeyPoll = HelperGameplayController.BlocksStripGameplay ||
                               HotkeySettingsRowUI.IsRebinding ||
                               IsTypingIntoInputField() ||
                               Time.frameCount == HotkeySettingsRowUI.SuppressActionBarHotkeyPollFrame;

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            if (!blockHotkeyPoll &&
                binding.currentKey != KeyCode.None &&
                Input.GetKeyDown(binding.currentKey))
            {
                binding.slot.Press();
            }

            RefreshSlotRuntime(binding.slot);
        }

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
            if (debugLogs && debugEmptySlots)
                Debug.Log($"[ActionBar] {slot.SlotType} slot {slot.SlotIndex} is empty.");
            return;
        }

        switch (action.kind)
        {
            case ActionBarAssignmentKind.Ability:
                ResolveCoreRefs();
                if (abilityController == null)
                {
                    Debug.LogWarning("[ActionBar] No PlayerAbilityController found.");
                    return;
                }

                bool usedAbility = abilityController.TryUseAbility(action.id);

                if (debugLogs)
                    Debug.Log(usedAbility
                        ? $"[ActionBar] Used ability '{action.displayName}'"
                        : $"[ActionBar] Failed to use ability '{action.displayName}'");

                RefreshSlotRuntime(slot);
                break;

            case ActionBarAssignmentKind.Item:
                if (consumableController == null)
                {
                    Debug.LogWarning("[ActionBar] No PlayerConsumableController found.");
                    return;
                }

                if (slot.AssignedItemAmount <= 0)
                {
                    slot.SetNoStockVisual(true);
                    return;
                }

                bool used = consumableController.TryUseItem(action.id);

                if (debugLogs)
                {
                    Debug.Log(used
                        ? $"[ActionBar] Used item '{action.displayName}'"
                        : $"[ActionBar] Failed to use item '{action.displayName}'");
                }

                RefreshSlotRuntime(slot);
                break;
        }
    }

    private void OnSlotAssignmentChanged(ActionBarSlotUI slot)
    {
        CaptureSlotsToSavedState();

        if (!suppressSaveForLoadoutSwap && SaveManager.Instance != null)
            SaveManager.Instance.Save();

        NotifyPlayerStatsCombatPowerRelevantChange();

        if (debugLogs && slot != null)
        {
            string name = slot.AssignedAction != null && slot.AssignedAction.IsAssigned
                ? slot.AssignedAction.displayName
                : "Empty";
        }
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
                if (debugLogs)
                    Debug.LogWarning($"[ActionBar] No slot found for slotIndex={saved.slotIndex}");
                continue;
            }

            ActionBarAssignment assignment = ResolveAssignment(saved.kind, saved.id);
            if (assignment == null || !assignment.IsAssigned)
            {
                if (debugLogs)
                    Debug.LogWarning($"[ActionBar] Failed to resolve slotIndex={saved.slotIndex} id={saved.id}");
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
                    if (debugLogs)
                        Debug.LogWarning($"[ActionBar] Could not find ItemDefinition for '{id}' (remapped='{resolvedId}')");
                    return null;
                }

                return ActionBarAssignment.CreateItem(def);

            case ActionBarAssignmentKind.Ability:
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                AbilityDefinition ability = GetAbilityDefinition(id);
                if (!ability)
                {
                    if (debugLogs)
                        Debug.LogWarning($"[ActionBar] Could not find AbilityDefinition for '{id}'");
                    return null;
                }

                return ActionBarAssignment.CreateAbility(
                    ability.abilityId,
                    ability.displayName,
                    ability.icon,
                    ability.description
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
                abilityStackCount = abilityController.GetAbilityStackCountDisplay(action.id);
            slot.SetStackText(abilityStackCount);

            if (action.IsAbility)
            {
                ResolveCoreRefs();
                AbilityDefinition abilityDef = GetAbilityDefinition(action.id);
                bool abilityLocked = abilityDef == null || !SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(
                    skillDatabase != null ? skillDatabase.Get(abilityDef.sourceSkill) : null,
                    abilityDef,
                    skillsManager);

                if (abilityController != null)
                {
                    float abilityNorm = abilityController.GetCooldownNormalized(action.id);
                    abilityController.IsOnCooldown(action.id, out float abilitySecs);

                    float gcdNorm = abilityController.GetGlobalCooldownNormalized();
                    abilityController.IsOnGlobalCooldown(out float gcdSecs);

                    // Show whichever lockout is currently stronger/longer.
                    if (abilityNorm >= gcdNorm)
                        slot.SetCooldownVisual(abilityNorm, abilitySecs);
                    else
                        slot.SetCooldownVisual(gcdNorm, gcdSecs);

                    slot.SetPrimedVisual(abilityController.IsAbilityPrimed(action.id));
                    bool weaponOk = abilityController.CanUseAbilityWithCurrentWeapon(action.id);
                    slot.SetAbilityWeaponCompatibility(weaponOk);
                    // Same red overlay as "not available" when skill-locked or wrong weapon type.
                    slot.SetNoStockVisual(abilityLocked || !weaponOk);

                    bool buffHud = buffController != null && buffController.IsHudAbilityBuffActive(action.id);
                    slot.SetAbilityBuffActiveOverlay(buffHud);

                    float buffRemain = 0f;
                    bool showBuffTimer = buffController != null &&
                                         buffController.ShouldDisplayHudAbilityBuffCountdown(action.id, out buffRemain);
                    slot.SetAbilityBuffTimerDisplay(showBuffTimer, buffRemain);
                }
                else
                {
                    slot.SetCooldownVisual(0f, 0f);
                    slot.SetPrimedVisual(false);
                    bool weaponOkNoController = true;
                    var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                    CharacterStats cs = player != null ? player.GetComponent<CharacterStats>() : null;
                    if (cs != null && abilityDef != null)
                        weaponOkNoController = cs.IsAbilityUsableWithEquippedWeapon(abilityDef);
                    slot.SetAbilityWeaponCompatibility(weaponOkNoController);
                    slot.SetNoStockVisual(abilityLocked || !weaponOkNoController);

                    bool buffHudNoAb = buffController != null && buffController.IsHudAbilityBuffActive(action.id);
                    slot.SetAbilityBuffActiveOverlay(buffHudNoAb);

                    float buffRemainNoAb = 0f;
                    bool showBuffTimerNoAb = buffController != null &&
                                             buffController.ShouldDisplayHudAbilityBuffCountdown(action.id, out buffRemainNoAb);
                    slot.SetAbilityBuffTimerDisplay(showBuffTimerNoAb, buffRemainNoAb);
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
    }

    public void LoadFrom(SaveData data)
    {
        savedSlots.Clear();
        secondarySavedSlots.Clear();

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

        QueueSavedStateApply();
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
        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            var playerInventory = player.GetComponent<Inventory>();
            if (playerInventory != null)
                inventory = playerInventory;

            var playerConsumables = player.GetComponent<PlayerConsumableController>();
            if (playerConsumables != null)
                consumableController = playerConsumables;

            var playerAbilities = player.GetComponent<PlayerAbilityController>();
            if (playerAbilities == null)
                playerAbilities = player.gameObject.AddComponent<PlayerAbilityController>();
            if (playerAbilities != null)
                abilityController = playerAbilities;

            var playerBuffs = player.GetComponent<PlayerBuffController>();
            if (playerBuffs != null)
                buffController = playerBuffs;
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
        int order = GetSlotOrderIndex(slot);
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