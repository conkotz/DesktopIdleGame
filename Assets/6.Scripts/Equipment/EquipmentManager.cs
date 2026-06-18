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

    [Header("Gear Set 1 (saved)")]
    [SerializeField] private string helmet1ItemId;
    [SerializeField] private string body1ItemId;
    [SerializeField] private string boots1ItemId;
    [SerializeField] private string trinket1ItemId;
    [SerializeField] private string pendant1ItemId;
    [SerializeField] private string ring11ItemId;
    [SerializeField] private string ring21ItemId;

    [Header("Gear Set 2 (saved)")]
    [SerializeField] private string helmet2ItemId;
    [SerializeField] private string body2ItemId;
    [SerializeField] private string boots2ItemId;
    [SerializeField] private string trinket2ItemId;
    [SerializeField] private string pendant2ItemId;
    [SerializeField] private string ring12ItemId;
    [SerializeField] private string ring22ItemId;

    public event Action<EquipmentUISlotType, string> OnUISlotChanged;
    public event Action<int> OnActiveSetChanged;

    // Events used by UI + equippers
    public event Action<string> OnMainHandChanged;
    public event Action<string> OnOffHandChanged;
    public event Action OnVisualsChanged;

    public int ActiveWeaponSetIndex => activeWeaponSetIndex;

    // These always return the ACTIVE set
    public string MainHandItemId => GetMainHandForSet(activeWeaponSetIndex);
    public string OffHandItemId => GetOffHandForSet(activeWeaponSetIndex);
    public int OffHandStackAmount => GetOffHandStackForSet(activeWeaponSetIndex);

    public string BodyItemId => GetBodyForSet(activeWeaponSetIndex);
    public string HelmetItemId => GetHelmetForSet(activeWeaponSetIndex);
    public Inventory Inventory => inventory;

    // Tool visual override (NOT saved - temporary)
    private string _mainHandVisualOverrideItemId;
    public bool HasMainHandVisualOverride => !string.IsNullOrWhiteSpace(_mainHandVisualOverrideItemId);

    [SerializeField] private GameObject mainHandVisualRoot;
    [SerializeField] private GameObject offHandVisualRoot;
    private bool _hideBothHandsOverride;
    public bool HideBothHandsOverride => _hideBothHandsOverride;

    public bool ForceUnarmed { get; private set; }
    private bool _suppressSaveForSetSwap;
    private bool _suppressGearSlotUiEventsForSetSwap;

    public const float WeaponSetSwapCooldownSeconds = 2f;
    private float _weaponSetSwapLockedUntilUnscaled = -999f;
    private bool _weaponSetSwapCooldownBlockedLogged;

    public bool IsWeaponSetSwapOnCooldown => Time.unscaledTime < _weaponSetSwapLockedUntilUnscaled;

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

    private string GetHelmetForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? helmet1ItemId : helmet2ItemId;
    }

    private string GetBodyForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? body1ItemId : body2ItemId;
    }

    private string GetBootsForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? boots1ItemId : boots2ItemId;
    }

    private string GetTrinketForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? trinket1ItemId : trinket2ItemId;
    }

    private string GetPendantForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? pendant1ItemId : pendant2ItemId;
    }

    private string GetRing1ForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? ring11ItemId : ring12ItemId;
    }

    private string GetRing2ForSet(int setIndex)
    {
        setIndex = NormalizeSetIndex(setIndex);
        return setIndex == 0 ? ring21ItemId : ring22ItemId;
    }

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

    private void SetHelmetForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) helmet1ItemId = next;
        else helmet2ItemId = next;
    }

    private void SetBodyForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) body1ItemId = next;
        else body2ItemId = next;
    }

    private void SetBootsForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) boots1ItemId = next;
        else boots2ItemId = next;
    }

    private void SetTrinketForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) trinket1ItemId = next;
        else trinket2ItemId = next;
    }

    private void SetPendantForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) pendant1ItemId = next;
        else pendant2ItemId = next;
    }

    private void SetRing1ForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) ring11ItemId = next;
        else ring12ItemId = next;
    }

    private void SetRing2ForSet(int setIndex, string itemId)
    {
        setIndex = NormalizeSetIndex(setIndex);
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (setIndex == 0) ring21ItemId = next;
        else ring22ItemId = next;
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
        OnActiveSetChanged?.Invoke(activeWeaponSetIndex);
    }

    private void NotifyGearSlotsChanged()
    {
        if (_suppressGearSlotUiEventsForSetSwap)
        {
            OnVisualsChanged?.Invoke();
            return;
        }

        OnUISlotChanged?.Invoke(EquipmentUISlotType.Helmet, GetHelmetForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Body, GetBodyForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Boots, GetBootsForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Trinket, GetTrinketForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Pendant, GetPendantForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring1, GetRing1ForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring2, GetRing2ForSet(activeWeaponSetIndex));
        OnVisualsChanged?.Invoke();
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

    private MainHandWeaponArchetype GetMainHandArchetype(string itemId)
    {
        var def = GetDef(itemId);
        return def ? def.MainHandArchetype : MainHandWeaponArchetype.None;
    }

    private MainHandWeaponArchetype GetSupportRequiredMainHandArchetype(string itemId)
    {
        var def = GetDef(itemId);
        return def ? def.SupportRequiredMainHandArchetype : MainHandWeaponArchetype.None;
    }

    private static bool IsSupportArchetypeCompatible(MainHandWeaponArchetype supportReq, MainHandWeaponArchetype mainArchetype)
    {
        if (supportReq == MainHandWeaponArchetype.None)
            return true;
        if (mainArchetype == MainHandWeaponArchetype.None)
            return false;
        return supportReq == mainArchetype;
    }

    private bool IsMainHandCompatibleWithSupport(ItemDefinition mainDef, ItemDefinition supportDef)
    {
        if (!mainDef || !supportDef || !supportDef.IsCombatSupport)
            return false;

        if (mainDef.RequiresOffhandSupport)
        {
            if (supportDef.SupportType != mainDef.RequiredSupportType)
                return false;
        }

        return IsSupportArchetypeCompatible(
            supportDef.SupportRequiredMainHandArchetype,
            mainDef.MainHandArchetype);
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

        // Supports are always equippable; incompatible main-hands are auto-unequipped in EquipOffHand.
        if (offDef.IsCombatSupport && offDef.equipSlot == EquipSlot.OffHand)
            return true;

        if (string.IsNullOrWhiteSpace(mainHandId))
        {
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
            if (!(offDef.IsCombatSupport && offDef.SupportType == mainDef.RequiredSupportType))
                return false;

            return IsSupportArchetypeCompatible(
                offDef.SupportRequiredMainHandArchetype,
                mainDef.MainHandArchetype);
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
        {
            return IsSupportArchetypeCompatible(
                offDef.SupportRequiredMainHandArchetype,
                mainDef.MainHandArchetype);
        }

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

        if (inventory.Add(itemId, amount, null, notifyItemGainPopup: false))
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

    private void KickMainHandToInventoryOrDrop(bool save = true)
    {
        string currentMainHand = MainHandItemId;
        if (string.IsNullOrWhiteSpace(currentMainHand))
            return;

        SetMainHandForSet(activeWeaponSetIndex, null);
        NotifyMainHandChanged();
        ReturnOrDrop(currentMainHand, 1);

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

        // Supports may be equipped unarmed; if a wrong main-hand is equipped, auto-unequip it first.
        if (!string.IsNullOrWhiteSpace(next) &&
            nextDef != null &&
            nextDef.IsCombatSupport &&
            !string.IsNullOrWhiteSpace(MainHandItemId))
        {
            var mainDef = GetDef(MainHandItemId);
            if (mainDef != null && !IsMainHandCompatibleWithSupport(mainDef, nextDef))
                KickMainHandToInventoryOrDrop(save: false);
        }

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

            bool returned = inv.Add(currentlyEquipped, returnAmount, null, notifyItemGainPopup: false);
            if (!returned)
            {
                inv.Add(itemId, 1, null, notifyItemGainPopup: false);
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

            EquipSlot.Helmet => GetHelmetForSet(activeWeaponSetIndex),
            EquipSlot.Body => GetBodyForSet(activeWeaponSetIndex),
            EquipSlot.Boots => GetBootsForSet(activeWeaponSetIndex),

            EquipSlot.Trinket => GetTrinketForSet(activeWeaponSetIndex),
            EquipSlot.Pendant => GetPendantForSet(activeWeaponSetIndex),

            EquipSlot.Ring => index == 0 ? GetRing1ForSet(activeWeaponSetIndex) : GetRing2ForSet(activeWeaponSetIndex),

            _ => null
        };
    }

    private void SetEquippedItemId(EquipSlot slot, string itemId, int index = 0)
    {
        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;

        switch (slot)
        {
            case EquipSlot.Helmet:
                SetHelmetForSet(activeWeaponSetIndex, next);
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Helmet, GetHelmetForSet(activeWeaponSetIndex));
                break;

            case EquipSlot.Body:
                SetBodyForSet(activeWeaponSetIndex, next);
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Body, GetBodyForSet(activeWeaponSetIndex));
                break;

            case EquipSlot.Boots:
                SetBootsForSet(activeWeaponSetIndex, next);
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Boots, GetBootsForSet(activeWeaponSetIndex));
                break;

            case EquipSlot.Trinket:
                SetTrinketForSet(activeWeaponSetIndex, next);
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Trinket, GetTrinketForSet(activeWeaponSetIndex));
                break;

            case EquipSlot.Pendant:
                SetPendantForSet(activeWeaponSetIndex, next);
                OnUISlotChanged?.Invoke(EquipmentUISlotType.Pendant, GetPendantForSet(activeWeaponSetIndex));
                break;

            case EquipSlot.Ring:
                if (index == 0)
                {
                    SetRing1ForSet(activeWeaponSetIndex, next);
                    OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring1, GetRing1ForSet(activeWeaponSetIndex));
                }
                else
                {
                    SetRing2ForSet(activeWeaponSetIndex, next);
                    OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring2, GetRing2ForSet(activeWeaponSetIndex));
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
    /// <summary>Returns false while the post-swap cooldown is active (logs once per cooldown window).</summary>
    public bool TryToggleWeaponSet()
    {
        if (!CanPerformWeaponSetSwap())
            return false;

        ApplyWeaponSetSwap(activeWeaponSetIndex == 0 ? 1 : 0);
        BeginWeaponSetSwapCooldown();
        return true;
    }

    /// <summary>Returns true if already on that set; false if blocked by cooldown (logs once per cooldown window).</summary>
    public bool TrySetActiveWeaponSet(int setIndex, bool bypassSwapCooldown = false)
    {
        int next = NormalizeSetIndex(setIndex);
        if (activeWeaponSetIndex == next)
            return true;

        if (!bypassSwapCooldown && !CanPerformWeaponSetSwap())
            return false;

        ApplyWeaponSetSwap(next);
        if (!bypassSwapCooldown)
            BeginWeaponSetSwapCooldown();
        return true;
    }

    private bool CanPerformWeaponSetSwap()
    {
        if (!IsWeaponSetSwapOnCooldown)
            return true;

        TryLogWeaponSetSwapBlockedOnce();
        return false;
    }

    private void TryLogWeaponSetSwapBlockedOnce()
    {
        if (_weaponSetSwapCooldownBlockedLogged)
            return;

        _weaponSetSwapCooldownBlockedLogged = true;
        float remaining = Mathf.Max(0.1f, _weaponSetSwapLockedUntilUnscaled - Time.unscaledTime);
        int seconds = Mathf.CeilToInt(remaining);
        GameLog.Add(
            $"Cannot swap gear sets yet ({seconds}s remaining).",
            GameLog.CannotMessageColor);
    }

    private void BeginWeaponSetSwapCooldown()
    {
        _weaponSetSwapCooldownBlockedLogged = false;
        _weaponSetSwapLockedUntilUnscaled = Time.unscaledTime + WeaponSetSwapCooldownSeconds;
    }

    private void ApplyWeaponSetSwap(int nextSetIndex)
    {
        _suppressSaveForSetSwap = true;
        _suppressGearSlotUiEventsForSetSwap = true;
        try
        {
            activeWeaponSetIndex = NormalizeSetIndex(nextSetIndex);
            NotifyWeaponSetChanged();
            NotifyGearSlotsChanged();
        }
        finally
        {
            _suppressGearSlotUiEventsForSetSwap = false;
            _suppressSaveForSetSwap = false;
            NotifyGearSlotsChanged();
        }
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

        data.equippedHelmetItemId = helmet1ItemId;
        data.equippedBodyItemId = body1ItemId;
        data.equippedBootsItemId = boots1ItemId;

        data.equippedTrinketItemId = trinket1ItemId;
        data.equippedPendantItemId = pendant1ItemId;
        data.equippedRing1ItemId = ring11ItemId;
        data.equippedRing2ItemId = ring21ItemId;

        data.equippedHelmet2ItemId = helmet2ItemId;
        data.equippedBody2ItemId = body2ItemId;
        data.equippedBoots2ItemId = boots2ItemId;
        data.equippedTrinket2ItemId = trinket2ItemId;
        data.equippedPendant2ItemId = pendant2ItemId;
        data.equippedRing12ItemId = ring12ItemId;
        data.equippedRing22ItemId = ring22ItemId;
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

        helmet1ItemId = string.IsNullOrWhiteSpace(data?.equippedHelmetItemId) ? null : data.equippedHelmetItemId;
        body1ItemId = string.IsNullOrWhiteSpace(data?.equippedBodyItemId) ? null : data.equippedBodyItemId;
        boots1ItemId = string.IsNullOrWhiteSpace(data?.equippedBootsItemId) ? null : data.equippedBootsItemId;

        trinket1ItemId = string.IsNullOrWhiteSpace(data?.equippedTrinketItemId) ? null : data.equippedTrinketItemId;
        pendant1ItemId = string.IsNullOrWhiteSpace(data?.equippedPendantItemId) ? null : data.equippedPendantItemId;
        ring11ItemId = string.IsNullOrWhiteSpace(data?.equippedRing1ItemId) ? null : data.equippedRing1ItemId;
        ring21ItemId = string.IsNullOrWhiteSpace(data?.equippedRing2ItemId) ? null : data.equippedRing2ItemId;

        helmet2ItemId = string.IsNullOrWhiteSpace(data?.equippedHelmet2ItemId) ? null : data.equippedHelmet2ItemId;
        body2ItemId = string.IsNullOrWhiteSpace(data?.equippedBody2ItemId) ? null : data.equippedBody2ItemId;
        boots2ItemId = string.IsNullOrWhiteSpace(data?.equippedBoots2ItemId) ? null : data.equippedBoots2ItemId;
        trinket2ItemId = string.IsNullOrWhiteSpace(data?.equippedTrinket2ItemId) ? null : data.equippedTrinket2ItemId;
        pendant2ItemId = string.IsNullOrWhiteSpace(data?.equippedPendant2ItemId) ? null : data.equippedPendant2ItemId;
        ring12ItemId = string.IsNullOrWhiteSpace(data?.equippedRing12ItemId) ? null : data.equippedRing12ItemId;
        ring22ItemId = string.IsNullOrWhiteSpace(data?.equippedRing22ItemId) ? null : data.equippedRing22ItemId;

        EnforceWeaponSetCompatibility(0);
        EnforceWeaponSetCompatibility(1);

        NotifyWeaponSetChanged();

        OnUISlotChanged?.Invoke(EquipmentUISlotType.Helmet, GetHelmetForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Body, GetBodyForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Boots, GetBootsForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Trinket, GetTrinketForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Pendant, GetPendantForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring1, GetRing1ForSet(activeWeaponSetIndex));
        OnUISlotChanged?.Invoke(EquipmentUISlotType.Ring2, GetRing2ForSet(activeWeaponSetIndex));

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
        if (_suppressSaveForSetSwap)
            return;
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

            if (def.UsesEquipmentTierGating && !def.MeetsEquipmentTierRequirement(SkillsManager.Instance))
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
                if (def.UsesEquipmentTierGating && !def.MeetsEquipmentTierRequirement(SkillsManager.Instance))
                    return false;
                return CanOffHandUseCurrentMainHand(itemId, MainHandItemId);
            }

            if (def.itemKind == ItemKind.CombatSupport && def.equipSlot == EquipSlot.OffHand)
                return CanOffHandUseCurrentMainHand(itemId, MainHandItemId);

            return false;
        }

        if (slot == EquipSlot.Ring)
        {
            if (def.equipSlot != EquipSlot.Ring)
                return false;
            if (def.IsUniquelyEquippedRing && FindEquippedRingSlotWithSameUniquenessType(itemId) >= 0)
                return false;
            return true;
        }

        return def.equipSlot == slot;
    }

    public bool CanEquipRingToSlot(string itemId, int ringIndex)
    {
        if (!inventory || ringIndex < 0 || ringIndex > 1)
            return false;

        var def = inventory.GetItemDef(itemId);
        if (!def || def.equipSlot != EquipSlot.Ring)
            return false;

        if (!def.IsUniquelyEquippedRing)
            return true;

        return FindEquippedRingSlotWithSameUniquenessType(itemId, excludeRingIndex: ringIndex) < 0;
    }

    public int FindEquippedRingSlotWithSameUniquenessType(string itemId, int excludeRingIndex = -1)
    {
        if (!inventory || string.IsNullOrWhiteSpace(itemId))
            return -1;

        var def = inventory.GetItemDef(itemId);
        if (!def || !def.IsUniquelyEquippedRing)
            return -1;

        for (int i = 0; i < 2; i++)
        {
            if (i == excludeRingIndex)
                continue;

            string equipped = GetEquippedItemId(EquipSlot.Ring, i);
            if (string.IsNullOrWhiteSpace(equipped))
                continue;

            if (SameRingUniquenessType(itemId, equipped))
                return i;
        }

        return -1;
    }

    public int CountEquippedRingInstances(string itemId) =>
        FindEquippedRingSlotWithSameUniquenessType(itemId) >= 0 ? 1 : 0;

    public bool TryEquipRingFromInventorySlot(Inventory inv, int fromSlotIndex, int targetRingIndex = -1)
    {
        if (!inv || !inventory)
            return false;

        var slot = inv.GetSlot(fromSlotIndex);
        if (slot.IsEmpty)
            return false;

        string itemId = slot.itemId;
        var def = inv.GetItemDef(itemId);
        if (!def || def.equipSlot != EquipSlot.Ring)
            return false;

        if (def.UsesEquipmentTierGating && !def.MeetsEquipmentTierRequirement(SkillsManager.Instance))
            return false;

        int equipIndex = ResolveRingEquipIndex(itemId, def, targetRingIndex);
        if (equipIndex < 0)
        {
            GameLog.Add(ItemDefinition.UniquelyEquippedRingActivityLogMessage, GameLog.CannotMessageColor);
            return false;
        }

        string prev = GetEquippedItemId(EquipSlot.Ring, equipIndex);
        if (!string.IsNullOrWhiteSpace(prev) &&
            string.Equals(prev, itemId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (inv.RemoveAmountAtSlot(fromSlotIndex, 1) != 1)
            return false;

        EquipGear(EquipSlot.Ring, itemId, equipIndex);

        if (!string.IsNullOrWhiteSpace(prev) &&
            !string.Equals(prev, itemId, StringComparison.OrdinalIgnoreCase))
        {
            if (!inv.Add(prev, 1, null, notifyItemGainPopup: false))
            {
                EquipGear(EquipSlot.Ring, prev, equipIndex);
                inv.Add(itemId, 1, null, notifyItemGainPopup: false);
                return false;
            }
        }

        return true;
    }

    private int ResolveRingEquipIndex(string itemId, ItemDefinition def, int targetRingIndex)
    {
        if (targetRingIndex >= 0 && targetRingIndex <= 1)
            return CanEquipRingToSlot(itemId, targetRingIndex) ? targetRingIndex : -1;

        if (def.IsUniquelyEquippedRing)
        {
            int conflict = FindEquippedRingSlotWithSameUniquenessType(itemId);
            if (conflict >= 0)
                return conflict;
        }

        if (string.IsNullOrWhiteSpace(GetEquippedItemId(EquipSlot.Ring, 0)))
            return 0;

        if (string.IsNullOrWhiteSpace(GetEquippedItemId(EquipSlot.Ring, 1)))
            return 1;

        return 0;
    }

    private string GetRingUniquenessKey(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        if (!inventory)
            return itemId.Trim();

        var db = inventory.GetItemDatabase();
        return db != null ? db.GetBaseItemId(itemId) : itemId.Trim();
    }

    private bool SameRingUniquenessType(string itemIdA, string itemIdB)
    {
        if (string.IsNullOrWhiteSpace(itemIdA) || string.IsNullOrWhiteSpace(itemIdB))
            return false;

        string keyA = GetRingUniquenessKey(itemIdA);
        string keyB = GetRingUniquenessKey(itemIdB);
        return !string.IsNullOrWhiteSpace(keyA) &&
               string.Equals(keyA, keyB, StringComparison.OrdinalIgnoreCase);
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
            if (!string.IsNullOrWhiteSpace(next))
            {
                var def = inventory?.GetItemDef(next);
                if (def?.IsUniquelyEquippedRing == true && !CanEquipRingToSlot(next, index))
                {
                    GameLog.Add(ItemDefinition.UniquelyEquippedRingActivityLogMessage, GameLog.CannotMessageColor);
                    return;
                }
            }

            if (index == 0) SetRing1ForSet(activeWeaponSetIndex, next);
            else SetRing2ForSet(activeWeaponSetIndex, next);

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

    public string GetRing1ItemId() => GetRing1ForSet(activeWeaponSetIndex);
    public string GetRing2ItemId() => GetRing2ForSet(activeWeaponSetIndex);

    public void SetRing1(string itemId) => SetEquippedItemId(EquipSlot.Ring, itemId, 0);
    public void SetRing2(string itemId) => SetEquippedItemId(EquipSlot.Ring, itemId, 1);

    /// <summary>Swap an equipped item id in-place (e.g. runtime enhancement clone) without inventory movement.</summary>
    public void ReplaceEquippedItemIdForUiSlot(EquipmentUISlotType uiSlot, string newItemId)
    {
        string next = string.IsNullOrWhiteSpace(newItemId) ? null : newItemId;

        switch (uiSlot)
        {
            case EquipmentUISlotType.MainHand:
                SetMainHandForSet(activeWeaponSetIndex, next);
                NotifyMainHandChanged();
                break;

            case EquipmentUISlotType.OffHand:
                SetOffHandForSet(activeWeaponSetIndex, next, string.IsNullOrWhiteSpace(next) ? 0 : Mathf.Max(1, OffHandStackAmount));
                NotifyOffHandChanged();
                break;

            case EquipmentUISlotType.Helmet:
                SetEquippedItemId(EquipSlot.Helmet, next);
                return;

            case EquipmentUISlotType.Body:
                SetEquippedItemId(EquipSlot.Body, next);
                return;

            case EquipmentUISlotType.Boots:
                SetEquippedItemId(EquipSlot.Boots, next);
                return;

            case EquipmentUISlotType.Trinket:
                SetEquippedItemId(EquipSlot.Trinket, next);
                return;

            case EquipmentUISlotType.Pendant:
                SetEquippedItemId(EquipSlot.Pendant, next);
                return;

            case EquipmentUISlotType.Ring1:
                SetEquippedItemId(EquipSlot.Ring, next, 0);
                return;

            case EquipmentUISlotType.Ring2:
                SetEquippedItemId(EquipSlot.Ring, next, 1);
                return;

            default:
                return;
        }

        RequestImmediateSave();
    }
}

