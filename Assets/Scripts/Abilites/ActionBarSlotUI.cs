using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum ActionBarSlotType
{
    Any,
    Food,
    Potion,
    Ability
}

public class ActionBarSlotUI : MonoBehaviour,
    IDropHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Slot")]
    [SerializeField] private ActionBarSlotType slotType = ActionBarSlotType.Any;
    [SerializeField] private int slotIndex;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hotkeyText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private Button button;
    [SerializeField] private string defaultTitle = "Empty";

    [Header("Display")]
    [SerializeField] private string emptyLabel = "Empty";

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;

    [Header("Runtime")]
    [SerializeField] private ActionBarAssignment assignedAction;

    private Inventory inventory;
    private bool isPointerOver;

    public ActionBarSlotType SlotType => slotType;
    public int SlotIndex => slotIndex;
    public ActionBarAssignment AssignedAction => assignedAction;

    private System.Action<ActionBarSlotUI> onPressed;
    private System.Action<ActionBarSlotUI> onAssignmentChanged;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;

        inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!tooltip)
            tooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    public void Initialize(
        System.Action<ActionBarSlotUI> triggerCallback,
        System.Action<ActionBarSlotUI> assignmentChangedCallback = null)
    {
        onPressed = triggerCallback;
        onAssignmentChanged = assignmentChangedCallback;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Press);
        }

        RefreshUI();
        SetStackText(0);
        SetCooldownVisual(0f);
    }

    public void SetHotkeyLabel(string text)
    {
        if (hotkeyText != null)
            hotkeyText.text = text;
    }

    public bool CanAccept(ActionBarAssignment newAssignment, ItemDefinition def = null)
    {
        if (newAssignment == null || !newAssignment.IsAssigned)
            return true;

        if (slotType == ActionBarSlotType.Any)
            return true;

        if (newAssignment.kind == ActionBarAssignmentKind.Ability)
            return slotType == ActionBarSlotType.Ability;

        if (newAssignment.kind == ActionBarAssignmentKind.Item)
        {
            if (def == null)
                return slotType == ActionBarSlotType.Food || slotType == ActionBarSlotType.Potion;

            if (slotType == ActionBarSlotType.Food)
                return def.IsFood;

            if (slotType == ActionBarSlotType.Potion)
                return def.IsPotion;
        }

        return false;
    }

    public void Assign(ActionBarAssignment newAssignment, bool notify = true)
    {
        assignedAction = newAssignment;
        RefreshUI();

        if (isPointerOver)
            ShowTooltip();

        if (notify)
            onAssignmentChanged?.Invoke(this);
    }

    public void ClearAssignment(bool notify = true)
    {
        assignedAction = null;
        RefreshUI();
        SetStackText(0);
        SetCooldownVisual(0f);
        tooltip?.Hide();

        if (notify)
            onAssignmentChanged?.Invoke(this);
    }

    public void Press()
    {
        StopAllCoroutines();
        StartCoroutine(ClickFeedback());
        onPressed?.Invoke(this);
    }

    public void RefreshUI()
    {
        bool hasAssigned = assignedAction != null && assignedAction.IsAssigned;

        if (titleText != null)
        {
            if (hasAssigned)
            {
                // Hide the label entirely when something is slotted
                titleText.gameObject.SetActive(false);
            }
            else
            {
                titleText.gameObject.SetActive(true);
                titleText.text = string.IsNullOrWhiteSpace(defaultTitle) ? emptyLabel : defaultTitle;
            }
        }

        if (iconImage != null)
        {
            iconImage.enabled = hasAssigned && assignedAction.icon != null;
            iconImage.sprite = hasAssigned ? assignedAction.icon : null;
        }
    }

    public void SetStackText(int amount)
    {
        if (stackText == null) return;
        stackText.text = amount > 1 ? amount.ToString() : "";
    }

    public void SetCooldownVisual(float normalizedRemaining, float secondsRemaining = 0f)
    {
        normalizedRemaining = Mathf.Clamp01(normalizedRemaining);

        if (cooldownOverlay != null)
        {
            cooldownOverlay.enabled = normalizedRemaining > 0f;
            cooldownOverlay.fillAmount = normalizedRemaining;
        }

        if (cooldownText != null)
                cooldownText.text = secondsRemaining >= 1f
        ? Mathf.CeilToInt(secondsRemaining).ToString()
        : "";
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!InventoryDragState.HasDrag)
            return;

        string itemId = InventoryDragState.ItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (!inventory)
            return;

        ItemDefinition def = inventory.GetItemDef(itemId);
        if (!def)
            return;

        if (!def.IsConsumable)
        {
            Debug.Log("[ActionBar] Only consumables allowed");
            return;
        }

        ActionBarAssignment assignment = ActionBarAssignment.CreateItem(def);

        if (!CanAccept(assignment, def))
        {
            Debug.Log("[ActionBar] Item not valid for this slot");
            return;
        }

        Assign(assignment);
        InventoryDragState.EndDrag();

        Debug.Log($"[ActionBar] Assigned {def.displayName} to slot {SlotIndex}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            ClearAssignment();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        tooltip?.Hide();
    }

    private void ShowTooltip()
    {
        if (tooltip == null || assignedAction == null || !assignedAction.IsAssigned)
            return;

        if (assignedAction.kind == ActionBarAssignmentKind.Item)
        {
            if (!inventory)
                inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

            if (!inventory || string.IsNullOrWhiteSpace(assignedAction.id))
                return;

            ItemDefinition def = inventory.GetItemDef(assignedAction.id);
            if (!def)
                return;

            var flipper = tooltip.GetComponent<FlipInsideBounds>();
            if (flipper)
            {
                flipper.SetPreferredSide(preferredSide);

                RectTransform measureRect = tooltipHeightRect ? tooltipHeightRect : transform.root as RectTransform;
                if (measureRect)
                {
                    flipper.SetMeasureRect(measureRect);
                    flipper.SetHeightRect(measureRect);
                }
            }

            int amount = 1;
            tooltip.ShowAt(transform, def, amount, compact: false);
        }
        else if (assignedAction.kind == ActionBarAssignmentKind.Ability)
        {
            tooltip.ShowText(
                assignedAction.displayName,
                string.IsNullOrWhiteSpace(assignedAction.description) ? "Ability" : assignedAction.description
            );
        }
    }

    private System.Collections.IEnumerator ClickFeedback()
    {
        transform.localScale = originalScale * 0.9f;
        yield return new WaitForSeconds(0.08f);
        transform.localScale = originalScale;
    }
}