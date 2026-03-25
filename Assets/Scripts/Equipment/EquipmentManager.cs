using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Inventory))]
public class EquipmentManager : MonoBehaviour, ISaveable
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;

    [Header("Weapon Set 1 (saved)")]
    [SerializeField] private string mainHand1ItemId;
    [SerializeField] private string offHand1ItemId;
    [SerializeField] private int offHand1StackAmount = 0;

    [Header("Weapon Set 2 (saved)")]
    [SerializeField] private string mainHand2ItemId;
    [SerializeField] private string offHand2ItemId;
    [SerializeField] private int offHand2StackAmount = 0;

    [Header("Active Weapon Set")]
    [SerializeField] private int activeWeaponSetIndex = 0; // 0 = set 1, 1 = set 2

    [Header("Other Equipped Item IDs (saved)")]
    [SerializeField] private string helmetItemId;
    [SerializeField] private string bodyItemId;
    [SerializeField] private string bootsItemId;
    [SerializeField] private string trinketItemId;
    [SerializeField] private string pendantItemId;
    [SerializeField] private string ring1ItemId;
    [SerializeField] private string ring2ItemId;

    public event Action<EquipmentUISlotType, string> OnUISlotChanged;

    // Events used by UI + equippers
    public event Action<string> OnMainHandChanged;
    public event Action<string> OnOffHandChanged;
    public event Action OnVisualsChanged;

    public int ActiveWeaponSetIndex => activeWeaponSetIndex;

    // These always return the ACTIVE set
    public string MainHandItemId => GetMainHandForSet(activeWeaponSetIndex);
    public string OffHandItemId => GetOffHandForSet(activeWeaponSetIndex);
    public int OffHandStackAmount => GetOffHandStackForSet(activeWeaponSetIndex);

    public string BodyItemId => bodyItemId;
    public string HelmetItemId => helmetItemId;
    public Inventory Inventory => inventory;

    // Tool visual override (NOT saved - temporary)
    private string _mainHandVisualOverrideItemId;
    public bool HasMainHandVisualOverride => !string.IsNullOrWhiteSpace(_mainHandVisualOverrideItemId);

    [SerializeField] private GameObject mainHandVisualRoot;
    [SerializeField] private GameObject offHandVisualRoot;
    private bool _hideBothHandsOverride;
    public bool HideBothHandsOverride => _hideBothHandsOverride;

    public bool ForceUnarmed { get; private set; }

    [Header("Auto-Return Kicked Items")]
    [Tooltip("When 2H rules auto-unequip the other slot, return that item to inventory (or drop if full).")]
    [SerializeField] private bool autoReturnKickedItems = true;

    private void Awake()
    {
        if (!inventory)
            inventory = GetComponent<Inventory>();

        if (!mainHandVisualRoot)
        {
            var weaponSocket = transform.Find("Visuals/VisualOffset/Soldier/HandSocket/WeaponSocket");
            if (weaponSocket) mainHandVisualRoot = weaponSocket.gameObject;
        }

        if (!offHandVisualRoot)
        {
            var offhandSocket = transform.Find("Visuals/VisualOffset/Soldier/Back arm/Off hand");
            if (offhandSocket) offHandVisualRoot = offhandSocket.gameObject;
        }

        _mainHandVisualOverrideItemId = null;
        OnVisualsChanged?.Invoke();
    }

    // -------------------------
    // Set helpers
    // -------------------------
    private int NormalizeSetIndex(int setIndex)
    {
        return setIndex == 1 ? 1 : 0;
    }

    private int InactiveWeaponSetIndex => activeWeaponSetIndex == 0 ? 1 : 0;

    private string GetMainHandForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? mainHand1ItemId : mainHand2ItemId;
    }

    private string GetOffHandForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? offHand1ItemId : offHand2ItemId;
    }

    private int GetOffHandStackForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? offHand1StackAmount : offHand2StackAmount;
    }

    private void SetMainHandForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;

        if (setIndex == 0) mainHand1ItemId = next;
        else mainHand2ItemId = next;
    }

    private void SetOffHandForSet(int setIndex, string itemId, int stackAmount)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        int nextAmount = string.IsNullOrWhiteSpace(next) ? 0 : Mathf.Max(1, stackAmount);

        if (setIndex == 0)
        {
            offHand1ItemId = next;
            offHand1StackAmount = nextAmount;
        }
        else
        {
            offHand2ItemId = next;
            offHand2StackAmount = nextAmount;
        }
    }

    public string GetMainHandItemIdForSet(int setIndex) => GetMainHandForSet(setIndex);
    public string GetOffHandItemIdForSet(int setIndex) => GetOffHandForSet(setIndex);
    public int GetOffHandStackAmountForSet(int setIndex) => GetOffHandStackForSet(setIndex);

    public string GetInactiveMainHandItemId() => GetMainHandForSet(InactiveWeaponSetIndex);
    public string GetInactiveOffHandItemId() => GetOffHandForSet(InactiveWeaponSetIndex);
    public int GetInactiveOffHandStackAmount() => GetOffHandStackForSet(InactiveWeaponSetIndex);



    // -------------------------
    // Internal notify helpers
    // -------------------------
    private void NotifyMainHandChanged()
    {
        OnMainHandChanged?.Invoke(MainHandItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.MainHand, MainHandItemId);
        OnVisualsChanged?.Invoke();
    }

    private void NotifyOffHandChanged()
    {
        OnOffHandChanged?.Invoke(OffHandItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.OffHand, OffHandItemId);
        OnVisualsChanged?.Invoke();
    }

    private void NotifyWeaponSetChanged()
    {
        NotifyMainHandChanged();
        NotifyOffHandChanged();
    }

    // -------------------------
    // Item definition helpers
    // -------------------------
    private ItemDefinition GetDef(string itemId)
    {
        if (!inventory) return null;
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        return inventory.GetItemDef(itemId);
    }

    private bool IsTwoHandedWeapon(string itemId)
    {
        var def = GetDef(itemId);
        return def && def.IsWeapon && def.weaponStats.handedness == Handedness.TwoHanded;
    }

    private bool MainHandRequiresSupport(string itemId)
    {
        var def = GetDef(itemId);
        return def && def.RequiresOffhandSupport;
    }

    private CombatSupportType GetMainHandRequiredSupportType(string itemId)
    {
        var def = GetDef(itemId);
        return def ? def.RequiredSupportType : CombatSupportType.None;
    }

    private bool IsCombatSupport(string itemId)
    {
        var def = GetDef(itemId);
        return def && def.IsCombatSupport;
    }

    private CombatSupportType GetSupportType(string itemId)
    {
        var def = GetDef(itemId);
        return def ? def.SupportType : CombatSupportType.None;
    }

    private bool IsOffHandArmor(string itemId)
    {
        var def = GetDef(itemId);
        return def && def.itemKind == ItemKind.Armor && def.equipSlot == EquipSlot.OffHand;
    }

    // -------------------------
    // Compatibility helpers
    // -------------------------
    private bool CanMainHandUseCurrentOffHand(string mainHandId, string offHandId)
    {
        if (string.IsNullOrWhiteSpace(mainHandId))
            return true;

        var mainDef = GetDef(mainHandId);
        if (!mainDef)
            return false;

        if (mainDef.RequiresOffhandSupport)
        {
            if (string.IsNullOrWhiteSpace(offHandId))
                return true;

            var offDef = GetDef(offHandId);
            if (!offDef)
                return false;

            if (offDef.IsCombatSupport && offDef.SupportType == mainDef.RequiredSupportType)
                return true;

            return true;
        }

        if (mainDef.IsTwoHandedWeapon)
            return string.IsNullOrWhiteSpace(offHandId);

        if (string.IsNullOrWhiteSpace(offHandId))
            return true;

        var off = GetDef(offHandId);
        if (!off)
            return false;

        if (off.itemKind == ItemKind.Armor && off.equipSlot == EquipSlot.OffHand)
            return true;

        if (off.IsWeapon &&
            off.weaponStats.handedness == Handedness.OneHanded &&
            off.weaponStats.canEquipInOffHand)
            return true;

        if (off.IsCombatSupport && off.equipSlot == EquipSlot.OffHand)
            return true;

        return false;
    }

    private bool CanOffHandUseCurrentMainHand(string offHandId, string mainHandId)
    {
        if (string.IsNullOrWhiteSpace(offHandId))
            return true;

        var offDef = GetDef(offHandId);
        if (!offDef)
            return false;

        if (string.IsNullOrWhiteSpace(mainHandId))
        {
            if (offDef.IsCombatSupport && offDef.equipSlot == EquipSlot.OffHand)
                return true;

            if (offDef.itemKind == ItemKind.Armor && offDef.equipSlot == EquipSlot.OffHand)
                return true;

            if (offDef.IsWeapon &&
                offDef.weaponStats.handedness == Handedness.OneHanded &&
                offDef.weaponStats.canEquipInOffHand)
                return true;

            return false;
        }

        var mainDef = GetDef(mainHandId);
        if (!mainDef)
            return false;

        if (mainDef.RequiresOffhandSupport)
        {
            return offDef.IsCombatSupport &&
                   offDef.SupportType == mainDef.RequiredSupportType;
        }

        if (mainDef.IsTwoHandedWeapon)
            return false;

        if (offDef.itemKind == ItemKind.Armor && offDef.equipSlot == EquipSlot.OffHand)
            return true;

        if (offDef.IsWeapon &&
            offDef.weaponStats.handedness == Handedness.OneHanded &&
            offDef.weaponStats.canEquipInOffHand)
            return true;

        if (offDef.IsCombatSupport && offDef.equipSlot == EquipSlot.OffHand)
            return true;

        return false;
    }

    // -------------------------
    // Inventory return/drop
    // -------------------------
    private void ReturnOrDrop(string itemId, int amount = 1)
    {
        if (!autoReturnKickedItems) return;
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (!inventory) return;

        amount = Mathf.Max(1, amount);

        if (inventory.Add(itemId, amount))
            return;

        var def = GetDef(itemId);
        if (DropManager.Instance != null)
        {
            DropManager.Instance.Spawn(itemId, amount, def ? def.icon : null);
            return;
        }

        Debug.LogWarning($"[EquipmentManager] Inventory full and no DropManager. Lost item '{itemId}' x{amount}.", this);
    }

    // -------------------------
    // Active-set offhand internals
    // -------------------------
    private void ClearOffHandInternal(bool save = true)
    {
        SetOffHandForSet(activeWeaponSetIndex, null, 0);
        NotifyOffHandChanged();

        if (save)
            RequestImmediateSave();
    }

    private void KickOffHandToInventoryOrDrop(bool save = true)
    {
        string currentOffHand = OffHandItemId;
        int currentAmount = Mathf.Max(1, OffHandStackAmount);

        if (string.IsNullOrWhiteSpace(currentOffHand))
            return;

        SetOffHandForSet(activeWeaponSetIndex, null, 0);
        NotifyOffHandChanged();

        ReturnOrDrop(currentOffHand, currentAmount);

        if (save)
            RequestImmediateSave();
    }

    // -------------------------
    // Equip / Unequip (ACTIVE SET ONLY)
    // -------------------------
    public void EquipMainHand(string itemId)
    {
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        string currentMain = MainHandItemId;
        string currentOff = OffHandItemId;
        int currentOffAmount = OffHandStackAmount;

        if (currentMain == next)
            return;

        if (!string.IsNullOrWhiteSpace(next))
        {
            var newMain = GetDef(next);

            if (newMain)
            {
                if (newMain.IsTwoHandedWeapon && !newMain.RequiresOffhandSupport)
                {
                    if (!string.IsNullOrWhiteSpace(currentOff))
                    {
                        KickOffHandToInventoryOrDrop(save: false);
                        currentOff = null;
                        currentOffAmount = 0;
                    }
                }
                else if (newMain.RequiresOffhandSupport)
                {
                    if (!string.IsNullOrWhiteSpace(currentOff))
                    {
                        var offDef = GetDef(currentOff);
                        bool validSupport =
                            offDef &&
                            offDef.IsCombatSupport &&
                            offDef.SupportType == newMain.RequiredSupportType;

                        if (!validSupport)
                        {
                            SetOffHandForSet(activeWeaponSetIndex, null, 0);
                            OnOffHandChanged?.Invoke(OffHandItemId);
                            OnUISlotChanged?.Invoke(EquipmentUISlotType.OffHand, OffHandItemId);
                            ReturnOrDrop(currentOff, Mathf.Max(1, currentOffAmount));
                            currentOff = null;
                            currentOffAmount = 0;
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(currentOff))
                    {
                        var offDef = GetDef(currentOff);
                        bool valid =
                            (offDef && offDef.itemKind == ItemKind.Armor && offDef.equipSlot == EquipSlot.OffHand) ||
                            (offDef && offDef.IsWeapon && offDef.weaponStats.handedness == Handedness.OneHanded && offDef.weaponStats.canEquipInOffHand);

                        if (!valid)
                        {
                            SetOffHandForSet(activeWeaponSetIndex, null, 0);
                            OnOffHandChanged?.Invoke(OffHandItemId);
                            OnUISlotChanged?.Invoke(EquipmentUISlotType.OffHand, OffHandItemId);
                            ReturnOrDrop(currentOff, Mathf.Max(1, currentOffAmount));
                            currentOff = null;
                            currentOffAmount = 0;
                        }
                    }
                }
            }
        }

        SetMainHandForSet(activeWeaponSetIndex, next);
        NotifyMainHandChanged();
        RequestImmediateSave();
    }

    public void UnequipMainHand()
    {
        if (string.IsNullOrWhiteSpace(MainHandItemId))
            return;

        SetMainHandForSet(activeWeaponSetIndex, null);
        NotifyMainHandChanged();
        RequestImmediateSave();
    }

    public void EquipOffHand(string itemId, int amount = 1)
    {
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        amount = Mathf.Max(1, amount);

        if (!string.IsNullOrWhiteSpace(next) && !CanOffHandUseCurrentMainHand(next, MainHandItemId))
            return;

        var nextDef = GetDef(next);
        string currentOff = OffHandItemId;
        int currentOffAmount = OffHandStackAmount;

        if (!string.IsNullOrWhiteSpace(next) &&
            nextDef != null &&
            nextDef.IsCombatSupport &&
            currentOff == next)
        {
            SetOffHandForSet(activeWeaponSetIndex, currentOff, currentOffAmount + amount);
            NotifyOffHandChanged();
            RequestImmediateSave();
            return;
        }

        if (currentOff == next &&
            (!nextDef || !nextDef.IsCombatSupport) &&
            currentOffAmount == amount)
        {
            return;
        }

        SetOffHandForSet(activeWeaponSetIndex, next, string.IsNullOrWhiteSpace(next) ? 0 : amount);
        NotifyOffHandChanged();
        RequestImmediateSave();
    }

    public void UnequipOffHand()
    {
        if (string.IsNullOrWhiteSpace(OffHandItemId))
            return;

        ClearOffHandInternal(save: true);
    }

    /// <summary>
    /// Equip an item from an inventory slot into an equipment slot.
    /// Only the ACTIVE main/off hand can be changed.
    /// </summary>
    public bool EquipFromInventorySwap(Inventory inv, int fromSlotIndex, string itemId, EquipSlot slot)
    {
        if (!inv) return false;
        if (fromSlotIndex < 0) return false;
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        var def = inv.GetItemDef(itemId);
        if (!def) return false;

        if (!CanEquip(itemId, slot))
            return false;

        string currentlyEquipped = GetEquippedItemId(slot);

        if (!string.IsNullOrWhiteSpace(currentlyEquipped) && currentlyEquipped == itemId)
            return false;

        if (inv.RemoveAmountAtSlot(fromSlotIndex, 1) != 1)
            return false;

        if (!string.IsNullOrWhiteSpace(currentlyEquipped))
        {
            int returnAmount = 1;

            if (slot == EquipSlot.OffHand)
            {
                var equippedDef = inv.GetItemDef(currentlyEquipped);
                bool isSupport = equippedDef && equippedDef.IsCombatSupport;
                returnAmount = isSupport ? Mathf.Max(1, OffHandStackAmount) : 1;
            }

            bool returned = inv.Add(currentlyEquipped, returnAmount);
            if (!returned)
            {
                inv.Add(itemId, 1);
                return false;
            }
        }

        ForceEquip(slot, itemId);
        return true;
    }

    public string GetEquippedItemId(EquipSlot slot, int index = 0)
    {
        return slot switch
        {
            EquipSlot.MainHand => MainHandItemId,
            EquipSlot.OffHand => OffHandItemId,

            EquipSlot.Helmet => helmetItemId,
            EquipSlot.Body => bodyItemId,
            EquipSlot.Boots => bootsItemId,

            EquipSlot.Trinket => trinketItemId,
            EquipSlot.Pendant => pendantItemId,

            EquipSlot.Ring => index == 0 ? ring1ItemId : ring2ItemId,

            _ => null
        };
    }

    private void SetEquippedItemId(EquipSlot slot, string itemId, int index = 0)
    {
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;

        switch (slot)
        {
            case EquipSlot.Helmet:
                helmetItemId = next;
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Helmet, helmetItemId);
                break;

            case EquipSlot.Body:
                bodyItemId = next;
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Body, bodyItemId);
                break;

            case EquipSlot.Boots:
                bootsItemId = next;
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Boots, bootsItemId);
                break;

            case EquipSlot.Trinket:
                trinketItemId = next;
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Trinket, trinketItemId);
                break;

            case EquipSlot.Pendant:
                pendantItemId = next;
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Pendant, pendantItemId);
                break;

            case EquipSlot.Ring:
                if (index == 0)
                {
                    ring1ItemId = next;
                    OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring1, ring1ItemId);
                }
                else
                {
                    ring2ItemId = next;
                    OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring2, ring2ItemId);
                }
                break;
        }

        OnVisualsChanged?.Invoke();
        RequestImmediateSave();
    }

    private void ForceEquip(EquipSlot slot, string itemId, int index = 0)
    {
        if (slot == EquipSlot.MainHand) { EquipMainHand(itemId); return; }
        if (slot == EquipSlot.OffHand) { EquipOffHand(itemId); return; }

        SetEquippedItemId(slot, itemId, index);
    }

    public void SetMainHandVisible(bool visible)
    {
        if (mainHandVisualRoot)
            mainHandVisualRoot.SetActive(visible);
    }

    public void Unequip(EquipSlot slot, int index = 0)
    {
        if (slot == EquipSlot.MainHand) { UnequipMainHand(); return; }
        if (slot == EquipSlot.OffHand) { UnequipOffHand(); return; }

        if (string.IsNullOrWhiteSpace(GetEquippedItemId(slot, index)))
            return;

        SetEquippedItemId(slot, null, index);
    }

    // -------------------------
    // Weapon set swap
    // -------------------------
    public void ToggleWeaponSet()
    {
        activeWeaponSetIndex = activeWeaponSetIndex == 0 ? 1 : 0;
        NotifyWeaponSetChanged();
        RequestImmediateSave();
    }

    public void SetActiveWeaponSet(int setIndex)
    {
        int next = NormalizeSetIndex(setIndex);
        if (activeWeaponSetIndex == next)
            return;

        activeWeaponSetIndex = next;
        NotifyWeaponSetChanged();
        RequestImmediateSave();
    }

    // -------------------------
    // Visual Override (NOT saved)
    // -------------------------
    public void SetMainHandVisualOverride(string itemId)
    {
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (_mainHandVisualOverrideItemId == next) return;

        _mainHandVisualOverrideItemId = next;
        OnVisualsChanged?.Invoke();
    }

    public void SetOffHandVisible(bool visible)
    {
        if (offHandVisualRoot)
            offHandVisualRoot.SetActive(visible);
    }

    public void ClearMainHandVisualOverride()
    {
        if (string.IsNullOrWhiteSpace(_mainHandVisualOverrideItemId)) return;

        _mainHandVisualOverrideItemId = null;
        OnVisualsChanged?.Invoke();
    }

    public void SetHideBothHandsOverride(bool hide)
    {
        if (_hideBothHandsOverride == hide) return;

        _hideBothHandsOverride = hide;
        OnVisualsChanged?.Invoke();
    }

    public void ClearHideBothHandsOverride()
    {
        if (!_hideBothHandsOverride) return;

        _hideBothHandsOverride = false;
        OnVisualsChanged?.Invoke();
    }

    // -------------------------
    // Queries for equippers
    // -------------------------
    public ToolKey GetVisualMainHandToolKey()
    {
        if (ForceUnarmed) return ToolKey.None;
        if (!inventory) return ToolKey.None;

        if (!string.IsNullOrWhiteSpace(_mainHandVisualOverrideItemId))
        {
            var def = inventory.GetItemDef(_mainHandVisualOverrideItemId);
            if (def) return def.handVisualKey;
        }

        if (!string.IsNullOrWhiteSpace(MainHandItemId))
        {
            var def = inventory.GetItemDef(MainHandItemId);
            if (def && def.handVisualKey == ToolKey.Weapon)
                return ToolKey.Weapon;
        }

        return ToolKey.None;
    }

    public string GetEquippedOffHandItemId() => OffHandItemId;

    // -------------------------
    // Debug / reset
    // -------------------------
    public void ForceUnequipAll(bool save = true)
    {
        bool changed = false;

        if (!string.IsNullOrWhiteSpace(mainHand1ItemId)) { mainHand1ItemId = null; changed = true; }
        if (!string.IsNullOrWhiteSpace(offHand1ItemId)) { offHand1ItemId = null; offHand1StackAmount = 0; changed = true; }

        if (!string.IsNullOrWhiteSpace(mainHand2ItemId)) { mainHand2ItemId = null; changed = true; }
        if (!string.IsNullOrWhiteSpace(offHand2ItemId)) { offHand2ItemId = null; offHand2StackAmount = 0; changed = true; }

        if (!string.IsNullOrWhiteSpace(_mainHandVisualOverrideItemId))
            _mainHandVisualOverrideItemId = null;

        if (changed)
        {
            NotifyWeaponSetChanged();

            if (save)
                RequestImmediateSave();
        }
        else
        {
            OnVisualsChanged?.Invoke();
        }
    }

    public void SetMainHandUnarmedOverride()
    {
        ForceUnarmed = true;
        _mainHandVisualOverrideItemId = null;
        OnVisualsChanged?.Invoke();
    }

    public void ClearMainHandUnarmedOverride()
    {
        ForceUnarmed = false;
        OnVisualsChanged?.Invoke();
    }

    // -------------------------
    // Save / Load
    // -------------------------
    public void SaveInto(SaveData data)
    {
        data.equippedMainHand1ItemId = mainHand1ItemId;
        data.equippedOffHand1ItemId = offHand1ItemId;
        data.equippedOffHand1StackAmount = offHand1StackAmount;

        data.equippedMainHand2ItemId = mainHand2ItemId;
        data.equippedOffHand2ItemId = offHand2ItemId;
        data.equippedOffHand2StackAmount = offHand2StackAmount;

        data.activeWeaponSetIndex = activeWeaponSetIndex;

        data.equippedHelmetItemId = helmetItemId;
        data.equippedBodyItemId = bodyItemId;
        data.equippedBootsItemId = bootsItemId;

        data.equippedTrinketItemId = trinketItemId;
        data.equippedPendantItemId = pendantItemId;
        data.equippedRing1ItemId = ring1ItemId;
        data.equippedRing2ItemId = ring2ItemId;
    }

    public void LoadFrom(SaveData data)
    {
        _mainHandVisualOverrideItemId = null;

        mainHand1ItemId = string.IsNullOrWhiteSpace(data?.equippedMainHand1ItemId) ? null : data.equippedMainHand1ItemId;
        offHand1ItemId = string.IsNullOrWhiteSpace(data?.equippedOffHand1ItemId) ? null : data.equippedOffHand1ItemId;
        offHand1StackAmount = Mathf.Max(0, data?.equippedOffHand1StackAmount ?? 0);

        if (string.IsNullOrWhiteSpace(offHand1ItemId))
            offHand1StackAmount = 0;
        else if (offHand1StackAmount <= 0)
            offHand1StackAmount = 1;

        mainHand2ItemId = string.IsNullOrWhiteSpace(data?.equippedMainHand2ItemId) ? null : data.equippedMainHand2ItemId;
        offHand2ItemId = string.IsNullOrWhiteSpace(data?.equippedOffHand2ItemId) ? null : data.equippedOffHand2ItemId;
        offHand2StackAmount = Mathf.Max(0, data?.equippedOffHand2StackAmount ?? 0);

        if (string.IsNullOrWhiteSpace(offHand2ItemId))
            offHand2StackAmount = 0;
        else if (offHand2StackAmount <= 0)
            offHand2StackAmount = 1;

        activeWeaponSetIndex = NormalizeSetIndex(data?.activeWeaponSetIndex ?? 0);

        helmetItemId = string.IsNullOrWhiteSpace(data?.equippedHelmetItemId) ? null : data.equippedHelmetItemId;
        bodyItemId = string.IsNullOrWhiteSpace(data?.equippedBodyItemId) ? null : data.equippedBodyItemId;
        bootsItemId = string.IsNullOrWhiteSpace(data?.equippedBootsItemId) ? null : data.equippedBootsItemId;

        trinketItemId = string.IsNullOrWhiteSpace(data?.equippedTrinketItemId) ? null : data.equippedTrinketItemId;
        pendantItemId = string.IsNullOrWhiteSpace(data?.equippedPendantItemId) ? null : data.equippedPendantItemId;
        ring1ItemId = string.IsNullOrWhiteSpace(data?.equippedRing1ItemId) ? null : data.equippedRing1ItemId;
        ring2ItemId = string.IsNullOrWhiteSpace(data?.equippedRing2ItemId) ? null : data.equippedRing2ItemId;

        EnforceWeaponSetCompatibility(0);
        EnforceWeaponSetCompatibility(1);

        NotifyWeaponSetChanged();

        OnUISlotChanged?.Invoke(EquipmentUISlotType.Helmet, helmetItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Body, bodyItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Boots, bootsItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Trinket, trinketItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Pendant, pendantItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring1, ring1ItemId);
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring2, ring2ItemId);

        OnVisualsChanged?.Invoke();
    }

    private void EnforceWeaponSetCompatibility(int setIndex)
    {
        string mainId = GetMainHandForSet(setIndex);
        string offId = GetOffHandForSet(setIndex);

        var mainDef = GetDef(mainId);
        if (!mainDef)
            return;

        if (mainDef.IsTwoHandedWeapon && !mainDef.RequiresOffhandSupport)
        {
            SetOffHandForSet(setIndex, null, 0);
        }
        else if (mainDef.RequiresOffhandSupport)
        {
            var offDef = GetDef(offId);
            bool validSupport =
                offDef &&
                offDef.IsCombatSupport &&
                offDef.SupportType == mainDef.RequiredSupportType;

            if (!validSupport)
                SetOffHandForSet(setIndex, null, 0);
        }
    }

    // -------------------------
    // Save helper
    // -------------------------
    private void RequestImmediateSave()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

    // -------------------------
    // Equip validation
    // -------------------------
    public bool CanEquip(string itemId, EquipSlot slot)
    {
        if (!inventory) return false;

        var def = inventory.GetItemDef(itemId);
        if (!def) return false;

        if (slot == EquipSlot.MainHand)
        {
            if (def.itemKind != ItemKind.Weapon)
                return false;

            return true;
        }

        if (slot == EquipSlot.OffHand)
        {
            if (def.itemKind == ItemKind.Armor && def.equipSlot == EquipSlot.OffHand)
                return CanOffHandUseCurrentMainHand(itemId, MainHandItemId);

            if (def.itemKind == ItemKind.Weapon &&
                def.weaponStats.handedness == Handedness.OneHanded &&
                def.weaponStats.canEquipInOffHand)
            {
                return CanOffHandUseCurrentMainHand(itemId, MainHandItemId);
            }

            if (def.itemKind == ItemKind.CombatSupport && def.equipSlot == EquipSlot.OffHand)
                return CanOffHandUseCurrentMainHand(itemId, MainHandItemId);

            return false;
        }

        return def.equipSlot == slot;
    }

    public string VisualMainHandItemId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_mainHandVisualOverrideItemId))
                return _mainHandVisualOverrideItemId;

            return MainHandItemId;
        }
    }

    public void EquipGear(EquipSlot slot, string itemId, int index = 0)
    {
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;

        if (slot == EquipSlot.MainHand) { EquipMainHand(next); return; }
        if (slot == EquipSlot.OffHand) { EquipOffHand(next); return; }

        if (slot == EquipSlot.Ring)
        {
            if (index == 0) ring1ItemId = next;
            else ring2ItemId = next;

            OnUISlotChanged?.Invoke(index == 0 ? EquipmentUISlotType.Ring1 : EquipmentUISlotType.Ring2, next);
            RequestImmediateSave();
            OnVisualsChanged?.Invoke();
            return;
        }

        SetEquippedItemId(slot, next);
        OnUISlotChanged?.Invoke(MapEquipSlotToUi(slot), next);
        OnVisualsChanged?.Invoke();
    }

    private EquipmentUISlotType MapEquipSlotToUi(EquipSlot slot)
    {
        return slot switch
        {
            EquipSlot.MainHand => EquipmentUISlotType.MainHand,
            EquipSlot.OffHand => EquipmentUISlotType.OffHand,
            EquipSlot.Helmet => EquipmentUISlotType.Helmet,
            EquipSlot.Body => EquipmentUISlotType.Body,
            EquipSlot.Boots => EquipmentUISlotType.Boots,
            EquipSlot.Trinket => EquipmentUISlotType.Trinket,
            EquipSlot.Pendant => EquipmentUISlotType.Pendant,
            _ => EquipmentUISlotType.None
        };
    }

    public bool ConsumeOffHandSupport(int amount)
    {
        string currentOff = OffHandItemId;
        int currentAmount = OffHandStackAmount;

        if (string.IsNullOrWhiteSpace(currentOff)) return false;

        var def = GetDef(currentOff);
        if (!def || !def.IsCombatSupport) return false;

        amount = Mathf.Max(1, amount);
        if (currentAmount < amount) return false;

        currentAmount -= amount;

        if (currentAmount <= 0)
            SetOffHandForSet(activeWeaponSetIndex, null, 0);
        else
            SetOffHandForSet(activeWeaponSetIndex, currentOff, currentAmount);

        NotifyOffHandChanged();
        RequestImmediateSave();
        return true;
    }



    public ItemDefinition GetOffHandDef()
    {
        return GetDef(OffHandItemId);
    }

    public string GetRing1ItemId() => ring1ItemId;
    public string GetRing2ItemId() => ring2ItemId;

    public void SetRing1(string itemId) => SetEquippedItemId(EquipSlot.Ring, itemId, 0);
    public void SetRing2(string itemId) => SetEquippedItemId(EquipSlot.Ring, itemId, 1);
}

