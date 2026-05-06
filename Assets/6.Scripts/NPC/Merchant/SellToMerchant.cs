using UnityEngine;
using UnityEngine.EventSystems;

public class SellToMerchant : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private int slotIndex;
    [SerializeField] private Inventory inventory;
    [SerializeField] private CurrencyWallet wallet;

    [Header("Optional FX")]
    [SerializeField] private bool showGoldPopup = true;

    private void Awake()
    {
        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!wallet)
            wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
    }

    public void SetSlotIndex(int index) => slotIndex = index;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // Selling requires active merchant mode and an open shop window.
        if (!MerchantClick.MerchantModeOpen || !MerchantClick.IsShopOpen)
            return;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrl)
            return;

        if (!inventory || !wallet)
        {
            Debug.LogError("[InvSlotSell] Missing inventory or wallet reference.");
            return;
        }

        var slot = inventory.GetSlot(slotIndex);
        if (slot.IsEmpty) return;

        string itemName = ResolveItemDisplayName(slot.itemId);

        if (MerchantClick.TryGetActiveMerchant(out var activeMerchantRef) &&
            activeMerchantRef != null &&
            activeMerchantRef.TryRejectUnsellableItemWithPopup(slot.itemId))
        {
            eventData.Use();
            return;
        }

        int valuePerItem = inventory.GetItemValue(slot.itemId);
        if (valuePerItem <= 0) return;

        // Remove exactly 1 from THIS slot
        int removed = inventory.RemoveAmountAtSlot(slotIndex, 1);
        if (removed <= 0) return;

        int goldGained = valuePerItem * removed;
        wallet.AddGold(goldGained);

        Merchant saleMerchant = null;
        int stockAdded = 0;
        if (MerchantClick.TryGetActiveMerchant(out var activeMerchant))
        {
            saleMerchant = activeMerchant;
            activeMerchant.TryReplenishStockFromPlayerSale(slot.itemId, removed, out stockAdded);
        }

        if (showGoldPopup)
        {
            var spawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
            if (spawner) spawner.ShowGoldGained(goldGained);
        }

        SaleUndoManager.Instance?.RecordSale(slot.itemId, removed, goldGained, saleMerchant, stockAdded);
        GameLog.SoldItem(itemName, removed, goldGained);

        Debug.Log($"[InvSlotSell] Sold 1x {slot.itemId} for {goldGained} gold (slot {slotIndex}).");

        // Stops other click logic (drag/tooltip) if needed
        eventData.Use();
    }

    private string ResolveItemDisplayName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "";

        ItemDefinition def = inventory ? inventory.GetItemDef(itemId) : null;
        return def && !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName : itemId;
    }
}