using System.Collections.Generic;
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
    [SerializeField] private PlayerConsumableController consumableController;
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private AbilityDatabase abilityDatabase;
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;

    [Header("Saved State (backing fields)")]
    private List<SavedSlotState> savedSlots = new();
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
        bool blockHotkeyPoll = HotkeySettingsRowUI.IsRebinding ||
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

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();

        NotifyPlayerStatsCombatPowerRelevantChange();

        if (debugLogs && slot != null)
        {
            string name = slot.AssignedAction != null && slot.AssignedAction.IsAssigned
                ? slot.AssignedAction.displayName
                : "Empty";
        }
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
                id = action.id
            });
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

                if (inventory == null || string.IsNullOrWhiteSpace(id))
                    return null;

                ItemDefinition def = inventory.GetItemDef(id);
                if (!def)
                {
                    if (debugLogs)
                        Debug.LogWarning($"[ActionBar] Could not find ItemDefinition for '{id}'");
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
                }

                return;
            }

            slot.SetCooldownVisual(0f, 0f);
            slot.SetPrimedVisual(false);
            slot.SetNoStockVisual(false);
            slot.SetAbilityWeaponCompatibility(true);
            return;
        }

        int count = 0;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var invSlot = inventory.GetSlot(i);
            if (!invSlot.IsEmpty && invSlot.itemId == action.id)
                count += invSlot.amount;
        }

        slot.SetStackText(count);
        bool noStock = count <= 0 &&
                       (slot.SlotType == ActionBarSlotType.Food || slot.SlotType == ActionBarSlotType.Potion);
        slot.SetNoStockVisual(noStock);
        slot.SetAbilityWeaponCompatibility(true);

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

        data.actionBarSlotIndexes.Clear();
        data.actionBarKinds.Clear();
        data.actionBarIds.Clear();

        for (int i = 0; i < savedSlots.Count; i++)
        {
            data.actionBarSlotIndexes.Add(savedSlots[i].slotIndex);
            data.actionBarKinds.Add(savedSlots[i].kind);
            data.actionBarIds.Add(savedSlots[i].id);
        }
    }

    public void LoadFrom(SaveData data)
    {
        savedSlots.Clear();

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
                id = data.actionBarIds[i]
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
        }

        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (consumableController == null)
            consumableController = FindFirstObjectByType<PlayerConsumableController>(FindObjectsInactive.Include);

        if (abilityController == null)
            abilityController = FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);

        if (abilityDatabase == null)
            abilityDatabase = AbilityDatabase.LoadDefault();

        if (skillDatabase == null)
            skillDatabase = SkillDatabase.LoadDefault();

        if (skillsManager == null)
            skillsManager = SkillsManager.Instance;
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