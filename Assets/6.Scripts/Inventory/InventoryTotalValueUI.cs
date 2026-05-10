using TMPro;
using UnityEngine;

/// <summary>
/// Drives a TMP label that displays the combined value of every item currently in the player's inventory.
/// Gold is held in <see cref="CurrencyWallet"/> (separate from <see cref="Inventory"/>) so it is excluded automatically.
/// The label's pre-authored text (e.g. "Inventory Unsold Value:") is preserved and the total is appended after it.
/// </summary>
[DisallowMultipleComponent]
public class InventoryTotalValueUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Inventory inventory;

    [Tooltip("Optional. When assigned, the displayed total reflects the grid's active category filter.")]
    [SerializeField] private InventoryGridUI inventoryGrid;

    [Tooltip("Optional override for the prefix. If left blank, the TMP_Text's authored text at Awake is used.")]
    [SerializeField] private string prefixOverride = "";

    [Tooltip("Numeric format passed to int.ToString. Default groups thousands.")]
    [SerializeField] private string numberFormat = "N0";

    private string _prefix;
    private bool _subscribed;

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!inventoryGrid) inventoryGrid = FindFirstObjectByType<InventoryGridUI>(FindObjectsInactive.Include);

        _prefix = !string.IsNullOrEmpty(prefixOverride)
            ? prefixOverride
            : (label != null ? label.text : string.Empty);
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;
        if (inventoryGrid != null)
            inventoryGrid.OnFilterChanged += Refresh;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
        if (inventoryGrid != null)
            inventoryGrid.OnFilterChanged -= Refresh;
        _subscribed = false;
    }

    private void Refresh()
    {
        if (!label) return;

        int value = ResolveDisplayedValue();
        string formatted = value.ToString(string.IsNullOrEmpty(numberFormat) ? "N0" : numberFormat);

        string prefix = _prefix ?? string.Empty;
        bool needsSpace = prefix.Length > 0 && !prefix.EndsWith(" ") && !prefix.EndsWith("\t");
        label.text = needsSpace ? prefix + " " + formatted : prefix + formatted;
    }

    private int ResolveDisplayedValue()
    {
        if (inventoryGrid != null)
            return inventoryGrid.GetVisibleInventoryValue();
        if (inventory != null)
            return inventory.GetTotalInventoryValue();
        return 0;
    }
}
