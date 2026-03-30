using System.Collections.Generic;
using UnityEngine;

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

    public IEnumerable<ActionBarSlotUI> GetSlots()
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            if (slotBindings[i] != null && slotBindings[i].slot != null)
                yield return slotBindings[i].slot;
        }
    }

    [Header("Slots")]
    [SerializeField] private List<SlotBinding> slotBindings = new();

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerConsumableController consumableController;
    [SerializeField] private PlayerAbilityController abilityController;

    [Header("Saved State (backing fields)")]
    private List<SavedSlotState> savedSlots = new();
    private bool pendingSavedStateApply;
    private float nextSavedStateApplyTime;
    private int savedStateApplyAttempts;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugEmptySlots = false;

    private void Awake()
    {                           
        ResolveCoreRefs();

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            binding.currentKey = binding.defaultKey;
            binding.slot.Initialize(OnSlotTriggered, OnSlotAssignmentChanged);
            binding.slot.SetHotkeyLabel(GetKeyLabel(binding.currentKey));
        }
    } 

    private void Start()
    {
        QueueSavedStateApply();

        if (SaveManager.Instance != null &&
            SaveManager.Instance.TryGetLastLoadedData(out SaveData data))
        {
            LoadFrom(data);
        }
    }

    private void Update()
    {
        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot == null)
                continue;

            if (binding.currentKey != KeyCode.None && Input.GetKeyDown(binding.currentKey))
                binding.slot.Press();

            RefreshSlotRuntime(binding.slot);
        }

        TryApplyPendingSavedState();
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

                AbilityDefinition ability = AbilityLibrary.Get(id);
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
            return;
        }

        if (!action.IsItem || inventory == null)
        {
            slot.SetStackText(0);

            if (action.IsAbility)
            {
                ResolveCoreRefs();
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
                }
                else
                {
                    slot.SetCooldownVisual(0f, 0f);
                }

                return;
            }

            slot.SetCooldownVisual(0f, 0f);
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

        if (consumableController != null)
        {
            float remainingNorm = consumableController.GetCooldownNormalized(action.id);
            consumableController.IsOnCooldown(action.id, out float remainingSecs);
            slot.SetCooldownVisual(remainingNorm, remainingSecs);
        }
        else
        {
            slot.SetCooldownVisual(0f, 0f);
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
            return;
        }

        savedStateApplyAttempts++;
        // Keep retrying quietly; this avoids intermittent load order races.
        nextSavedStateApplyTime = Time.unscaledTime + 0.2f;
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

    public void RebindKey(ActionBarSlotUI slot, KeyCode newKey)
    {
        if (slot == null)
            return;

        for (int i = 0; i < slotBindings.Count; i++)
        {
            SlotBinding binding = slotBindings[i];
            if (binding == null || binding.slot != slot)
                continue;

            binding.currentKey = newKey;
            binding.slot.SetHotkeyLabel(GetKeyLabel(newKey));
            return;
        }
    }

    private string GetKeyLabel(KeyCode key)
    {
        if (key == KeyCode.None)
            return "";

        return key switch
        {
            KeyCode.Alpha1 => "1",
            KeyCode.Alpha2 => "2",
            KeyCode.Alpha3 => "3",
            KeyCode.Alpha4 => "4",
            KeyCode.Alpha5 => "5",
            KeyCode.Alpha6 => "6",
            KeyCode.Alpha7 => "7",
            KeyCode.Alpha8 => "8",
            KeyCode.Alpha9 => "9",
            KeyCode.Alpha0 => "0",
            _ => key.ToString()
        };
    }
}