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

    [Tooltip("Suffix appended after the formatted number (e.g. 'g' for gold).")]
    [SerializeField] private string valueSuffix = "g";

    private string _prefix;
    private bool _subscribed;
    private bool _valueLabelDirty;

    private void Awake()
    {
        if (!label) label = GetComponent<TMP_Text>();
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
        if (!inventory) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!inventoryGrid)
            inventoryGrid = GetComponentInParent<InventoryGridUI>();
        if (!inventoryGrid)
        {
            Transform walk = transform;
            while (walk != null && !inventoryGrid)
            {
                inventoryGrid = walk.GetComponentInChildren<InventoryGridUI>(true);
                walk = walk.parent;
            }
        }

        _prefix = !string.IsNullOrEmpty(prefixOverride)
            ? prefixOverride
            : (label != null ? label.text : string.Empty);
    }

    private void OnEnable()
    {
        Subscribe();
        _valueLabelDirty = false;

        if (MainMenuUIPrewarm.UseBatchedInstantiation)
            return;

        RefreshNow();
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
            inventory.OnInventoryChanged += QueueRefresh;
        if (inventoryGrid != null)
            inventoryGrid.OnFilterChanged += QueueRefresh;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (inventory != null)
            inventory.OnInventoryChanged -= QueueRefresh;
        if (inventoryGrid != null)
            inventoryGrid.OnFilterChanged -= QueueRefresh;
        _subscribed = false;
        _valueLabelDirty = false;
    }

    private void QueueRefresh()
    {
        if (!isActiveAndEnabled || label == null || !label.gameObject.activeInHierarchy)
            return;
        _valueLabelDirty = true;
        InventoryUiRefreshCoordinator.MarkValueLabelDirty(this);
    }

    internal void FlushCoalescedRefresh()
    {
        if (!_valueLabelDirty)
            return;
        _valueLabelDirty = false;
        RefreshNow();
    }

    private void RefreshNow()
    {
        if (!label || !isActiveAndEnabled || !label.gameObject.activeInHierarchy)
            return;

        int value = ResolveDisplayedValue();
        string formatted = value.ToString(string.IsNullOrEmpty(numberFormat) ? "N0" : numberFormat);
        string body = formatted + (valueSuffix ?? string.Empty);

        string prefix = _prefix ?? string.Empty;
        bool needsSpace = prefix.Length > 0 && !prefix.EndsWith(" ") && !prefix.EndsWith("\t");
        label.text = needsSpace ? prefix + " " + body : prefix + body;
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
