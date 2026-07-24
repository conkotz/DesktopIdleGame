using TMPro;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public enum ActionBarSlotType
{
    Any,
    Food,
    Potion,
    Ability
}

public class ActionBarSlotUI : MonoBehaviour,
    IDropHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Slot")]
    [SerializeField] private ActionBarSlotType slotType = ActionBarSlotType.Any;
    [Tooltip("Stable id for save/load and (when 0–6) which ActionBar1–7 hotkey this row uses. Set 0,1,2… on each prefab instance so slots do not share the same key or save data.")]
    [SerializeField] private int slotIndex;

    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hotkeyText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private GameObject activeOverlay;
    [SerializeField] private TMP_Text activeTimerText;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private Button button;
    [Tooltip("Shown on the Label when this slot has no ability/item assigned. Change per prefab instance (e.g. Ability 1, Ability 2).")]
    [SerializeField] private string defaultTitle = "Empty";

    [Header("Display")]
    [SerializeField] private string emptyLabel = "Empty";
    [SerializeField] private Image primedBackgroundImage;
    [SerializeField] private Color primedBackgroundColor = new Color(1f, 0.94f, 0.45f, 0.35f);
    [SerializeField] private bool autoCreatePrimedBackground = true;
    [SerializeField] private Image noStockBackgroundImage;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Left;
    [SerializeField] private bool autoSyncTooltipFromPeerSlots = true;

    [Header("Auto Battle Border")]
    [SerializeField] private RectTransform autoBattleBorder;
    [SerializeField] private Graphic autoBattleBorderGraphic;
    [SerializeField, Min(0f)] private float autoBattleDashScrollSpeed = 1.5f;
    [SerializeField] private bool showBorderWhenEmpty = false;
    [SerializeField] private bool autoCreateBorderIfMissing = true;
    [SerializeField] private Color autoBattleBorderColor = new Color(1f, 0.86f, 0.15f, 1f);
    [SerializeField, Min(1)] private int runtimeBorderThicknessPx = 4;
    [SerializeField, Min(1)] private int runtimeDashLengthPx = 14;
    [SerializeField, Min(1)] private int runtimeDashGapPx = 10;
    [SerializeField] private float borderInset = 4f;
    [SerializeField] private PlayerCombatController combatController;

    [Header("Runtime")]
    [SerializeField] private ActionBarAssignment assignedAction;
    [SerializeField] private int assignedItemAmount;
    [SerializeField] private AbilityDatabase abilityDatabase;

    [Header("Ability Drag")]
    [SerializeField] private Vector2 abilityDragIconSize = new Vector2(48f, 48f);

    private Inventory inventory;
    private bool isPointerOver;
    private Canvas _rootCanvas;
    private CanvasGroup _dragCanvasGroup;
    private GameObject _abilityDragIconGO;
    private RectTransform _abilityDragIconRT;
    private Image _abilityDragIconImage;

    public ActionBarSlotType SlotType => slotType;
    public int SlotIndex => slotIndex;
    public ActionBarAssignment AssignedAction => assignedAction;
    public int AssignedItemAmount => assignedAction != null && assignedAction.IsItem ? Mathf.Max(0, assignedItemAmount) : 0;

    private System.Action<ActionBarSlotUI> onPressed;
    private System.Action<ActionBarSlotUI> onAssignmentChanged;
    private System.Action<ActionBarSlotUI> onSlottedAmountChanged;
    private ActionBarUI actionBarOwner;
    private Vector3 originalScale;
    private bool isAutoBattleActive;
    private bool abilityWeaponCompatible = true;
    private float autoBattleDashPhase;
    private int _lastStackAmount = int.MinValue;
    private float _lastCooldownFill = -1f;
    private bool _lastCooldownOverlayEnabled;
    private string _lastCooldownText;
    private bool _lastPrimed;
    private bool _lastNoStock;
    private bool _lastBuffOverlay;
    private bool _lastBuffTimerVisible;
    private int _lastBuffTimerSeconds = int.MinValue;
    private bool _weaponCompatibilityInitialized;
    private RawImage topEdge;
    private RawImage rightEdge;
    private RawImage bottomEdge;
    private RawImage leftEdge;
    private static readonly Dictionary<string, Texture2D> RuntimeDashTextureCache = new();

    private void Awake()
    {
        originalScale = transform.localScale;

        inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!tooltip)
            tooltip = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);

        if (activeOverlay == null)
        {
            Transform t = transform.Find("ActiveOverlay");
            if (t != null)
                activeOverlay = t.gameObject;
        }

        if (activeTimerText == null)
        {
            Transform tTimer = transform.Find("ActiveTimerText");
            if (tTimer != null)
                activeTimerText = tTimer.GetComponent<TMP_Text>();
        }

        if (activeOverlay != null)
            activeOverlay.SetActive(false);

        if (activeTimerText != null)
            activeTimerText.gameObject.SetActive(false);

        SyncTooltipReferenceFromPeerSlots();

        if (combatController == null)
            combatController = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        SyncTooltipReferenceFromPeerSlots();
        if (abilityDatabase == null)
            abilityDatabase = AbilityDatabase.LoadDefault();

        EnsureAutoBattleBorderExists();

        if (autoBattleBorderGraphic == null && autoBattleBorder != null)
            autoBattleBorderGraphic = autoBattleBorder.GetComponent<Graphic>();

        SetAutoBattleBorderVisible(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncEmptySlotTitleDisplay();
        if (Application.isPlaying || !gameObject.scene.IsValid())
            return;
        ActionBarUI bar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar != null)
            bar.RefreshHotkeyLabels();
    }
#endif

    private void OnEnable()
    {
        if (combatController == null)
            combatController = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        EnsureAutoBattleBorderExists();

        if (combatController != null)
            combatController.OnIdleCombatChanged += HandleIdleCombatChanged;

        HandleIdleCombatChanged(combatController != null && combatController.IdleCombatEnabled);
    }

    private void OnDisable()
    {
        if (combatController != null)
            combatController.OnIdleCombatChanged -= HandleIdleCombatChanged;

        isAutoBattleActive = false;
        SetAutoBattleBorderVisible(false);
    }

    private void Update()
    {
        if (autoBattleBorder == null || !autoBattleBorder.gameObject.activeSelf || autoBattleDashScrollSpeed <= 0f)
            return;

        autoBattleDashPhase += autoBattleDashScrollSpeed * Time.unscaledDeltaTime;
        RefreshDashUv();
    }

    public void Initialize(
        System.Action<ActionBarSlotUI> triggerCallback,
        System.Action<ActionBarSlotUI> assignmentChangedCallback = null,
        ActionBarUI ownerBar = null,
        System.Action<ActionBarSlotUI> slottedAmountChangedCallback = null)
    {
        onPressed = triggerCallback;
        onAssignmentChanged = assignmentChangedCallback;
        onSlottedAmountChanged = slottedAmountChangedCallback;
        actionBarOwner = ownerBar;

        if (button != null)
            SyncAbilityPressInputMode();

        RefreshUI();
        ResetRuntimeVisualCache();
        SetStackText(0);
        SetCooldownVisual(0f);
        SetPrimedVisual(false);
        SetNoStockVisual(false);
        SetAbilityWeaponCompatibility(true);
        SetAbilityBuffActiveOverlay(false);
        SetAbilityBuffTimerDisplay(false, 0f);
    }

    private void ResetRuntimeVisualCache()
    {
        _lastStackAmount = int.MinValue;
        _lastCooldownFill = -1f;
        _lastCooldownOverlayEnabled = false;
        _lastCooldownText = null;
        _lastPrimed = false;
        _lastNoStock = false;
        _lastBuffOverlay = false;
        _lastBuffTimerVisible = false;
        _lastBuffTimerSeconds = int.MinValue;
        _weaponCompatibilityInitialized = false;
        ApplyRuntimeVisualDefaults();
    }

    /// <summary>Clears transient slot overlays so dirty-check setters cannot leave stale visuals after assignment changes.</summary>
    private void ApplyRuntimeVisualDefaults()
    {
        if (cooldownOverlay != null)
        {
            cooldownOverlay.enabled = false;
            cooldownOverlay.fillAmount = 0f;
        }

        if (cooldownText != null)
            cooldownText.text = string.Empty;

        EnsurePrimedBackgroundExists();
        if (primedBackgroundImage != null)
        {
            primedBackgroundImage.enabled = false;
            primedBackgroundImage.gameObject.SetActive(false);
        }

        if (activeOverlay != null)
            activeOverlay.SetActive(false);

        if (activeTimerText != null)
            activeTimerText.gameObject.SetActive(false);

        if (noStockBackgroundImage != null)
        {
            noStockBackgroundImage.enabled = false;
            noStockBackgroundImage.gameObject.SetActive(false);
        }
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
        {
            if (slotType != ActionBarSlotType.Ability && slotType != ActionBarSlotType.Any)
                return false;

            AbilityDefinition abilityDef = null;
            if (abilityDatabase == null)
                abilityDatabase = AbilityDatabase.LoadDefault();
            if (abilityDatabase != null && !string.IsNullOrWhiteSpace(newAssignment.id))
                abilityDef = abilityDatabase.Get(newAssignment.id);

            if (actionBarOwner != null && !actionBarOwner.CanSlotAcceptGatheringAbility(abilityDef))
                return false;

            if (slotType == ActionBarSlotType.Any)
                return true;

            return slotType == ActionBarSlotType.Ability;
        }

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
        if (assignedAction == null || !assignedAction.IsItem)
            assignedItemAmount = 0;
        else if (assignedItemAmount <= 0)
            assignedItemAmount = 1;

        bool deferVisualRefresh = actionBarOwner != null && actionBarOwner.ShouldDeferLoadoutSlotVisualRefresh;
        if (!deferVisualRefresh)
        {
            RefreshUI();
            ResetRuntimeVisualCache();
            SetStackText(0);
            SetCooldownVisual(0f);
            SetPrimedVisual(false);
            SetNoStockVisual(false);
            SetAbilityWeaponCompatibility(true);
            SetAbilityBuffActiveOverlay(false);
            SetAbilityBuffTimerDisplay(false, 0f);
        }

        if (isPointerOver)
            ShowTooltip();

        if (notify)
            onAssignmentChanged?.Invoke(this);
    }

    public void ClearAssignment(bool notify = true)
    {
        assignedAction = null;
        assignedItemAmount = 0;

        bool deferVisualRefresh = actionBarOwner != null && actionBarOwner.ShouldDeferLoadoutSlotVisualRefresh;
        if (!deferVisualRefresh)
        {
            RefreshUI();
            ResetRuntimeVisualCache();
            SetStackText(0);
            SetCooldownVisual(0f);
            SetPrimedVisual(false);
            SetNoStockVisual(false);
            SetAbilityWeaponCompatibility(true);
            SetAbilityBuffActiveOverlay(false);
            SetAbilityBuffTimerDisplay(false, 0f);
            tooltip?.Hide();
        }

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

        SyncAbilityPressInputMode();
        SyncEmptySlotTitleDisplay();

        if (iconImage != null)
        {
            iconImage.enabled = hasAssigned && assignedAction.icon != null;
            iconImage.sprite = hasAssigned ? assignedAction.icon : null;
            iconImage.preserveAspect = true;
        }

        RefreshAutoBattleBorder();
    }

    private bool IsAssignedSnipeAbility()
    {
        return assignedAction != null &&
               assignedAction.IsAbility &&
               string.Equals(assignedAction.id, AbilityCombatPower.SnipeAbilityId, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncAbilityPressInputMode()
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        if (!IsAssignedSnipeAbility())
            button.onClick.AddListener(Press);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (!IsAssignedSnipeAbility())
            return;

        Press();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (!IsAssignedSnipeAbility())
            return;

        PlayerAbilityController abilityController =
            FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
        abilityController?.SetSnipeActionBarHeld(false);
    }

    /// <summary>Label for an empty slot: <see cref="defaultTitle"/> (or <see cref="emptyLabel"/> when blank).</summary>
    private void SyncEmptySlotTitleDisplay()
    {
        if (titleText == null)
            return;

        bool hasAssigned = assignedAction != null && assignedAction.IsAssigned;
        if (hasAssigned)
        {
            titleText.gameObject.SetActive(false);
            return;
        }

        titleText.gameObject.SetActive(true);
        titleText.text = string.IsNullOrWhiteSpace(defaultTitle) ? emptyLabel : defaultTitle;
    }

    public void SetStackText(int amount)
    {
        if (stackText == null || amount == _lastStackAmount)
            return;

        _lastStackAmount = amount;
        stackText.text = amount > 0 ? amount.ToString() : "";
    }

    public void SetCooldownVisual(float normalizedRemaining, float secondsRemaining = 0f)
    {
        normalizedRemaining = Mathf.Clamp01(normalizedRemaining);

        bool overlayEnabled = normalizedRemaining > 0f;
        if (cooldownOverlay != null &&
            (overlayEnabled != _lastCooldownOverlayEnabled ||
             !Mathf.Approximately(normalizedRemaining, _lastCooldownFill)))
        {
            _lastCooldownOverlayEnabled = overlayEnabled;
            _lastCooldownFill = normalizedRemaining;
            cooldownOverlay.enabled = overlayEnabled;
            cooldownOverlay.fillAmount = normalizedRemaining;
        }

        if (cooldownText != null)
        {
            string nextText = string.Empty;
            if (secondsRemaining > 0.05f)
            {
                nextText = secondsRemaining >= 1f
                    ? Mathf.CeilToInt(secondsRemaining).ToString()
                    : secondsRemaining.ToString("0.#");
            }

            if (nextText != _lastCooldownText)
            {
                _lastCooldownText = nextText;
                cooldownText.text = nextText;
            }
        }
    }

    public void SetPrimedVisual(bool primed)
    {
        if (primed == _lastPrimed)
            return;

        _lastPrimed = primed;
        EnsurePrimedBackgroundExists();
        if (primedBackgroundImage == null)
            return;

        primedBackgroundImage.color = primedBackgroundColor;
        primedBackgroundImage.enabled = primed;
        primedBackgroundImage.gameObject.SetActive(primed);
    }

    /// <summary>Shows when this slotted ability has an active timed / swing / minion buff (same source as the buff HUD strip).</summary>
    public void SetAbilityBuffActiveOverlay(bool active)
    {
        if (activeOverlay == null || active == _lastBuffOverlay)
            return;

        _lastBuffOverlay = active;
        activeOverlay.SetActive(active);
        if (active)
            EnsureActiveBuffTimerDrawsAboveOverlay();
    }

    /// <summary>Countdown for the HUD ability buff window (hidden for indefinite minion buffs with no duration row).</summary>
    public void SetAbilityBuffTimerDisplay(bool show, float remainingSeconds)
    {
        if (activeTimerText == null)
            return;

        int displaySeconds = show ? Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds)) : 0;
        if (show == _lastBuffTimerVisible && (!show || displaySeconds == _lastBuffTimerSeconds))
            return;

        _lastBuffTimerVisible = show;
        _lastBuffTimerSeconds = displaySeconds;
        activeTimerText.gameObject.SetActive(show);
        if (!show)
            return;

        activeTimerText.text = displaySeconds.ToString();
        EnsureActiveBuffTimerDrawsAboveOverlay();
    }

    /// <summary>
    /// UGUI draws siblings in hierarchy order — the active buff overlay must sort immediately before the timer so the
    /// countdown paints crisply on top (without a nested Canvas). Call whenever either control is shown.
    /// </summary>
    private void EnsureActiveBuffTimerDrawsAboveOverlay()
    {
        if (activeTimerText == null || !activeTimerText.gameObject.activeSelf)
            return;

        if (activeOverlay != null && activeOverlay.gameObject.activeSelf)
            activeOverlay.transform.SetAsLastSibling();
        activeTimerText.transform.SetAsLastSibling();
    }

    public void SetNoStockVisual(bool noStock)
    {
        if (noStockBackgroundImage == null || noStock == _lastNoStock)
            return;

        _lastNoStock = noStock;
        noStockBackgroundImage.enabled = noStock;
        noStockBackgroundImage.gameObject.SetActive(noStock);
    }

    public void SetAbilityWeaponCompatibility(bool canUseWithCurrentWeapon)
    {
        if (_weaponCompatibilityInitialized && canUseWithCurrentWeapon == abilityWeaponCompatible)
            return;

        _weaponCompatibilityInitialized = true;
        abilityWeaponCompatible = canUseWithCurrentWeapon;
        RefreshAutoBattleBorder();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (assignedAction == null || !assignedAction.IsAbility)
            return;

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            return;

        _dragCanvasGroup = GetComponent<CanvasGroup>();
        if (_dragCanvasGroup == null)
            _dragCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        AbilityDragState.BeginDrag(
            assignedAction.id,
            assignedAction.icon,
            assignedAction.displayName,
            assignedAction.description,
            this);

        CreateAbilityDragIcon();
        UpdateAbilityDragIconPosition(eventData);
        _dragCanvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateAbilityDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyAbilityDragIcon();
        if (_dragCanvasGroup != null)
            _dragCanvasGroup.blocksRaycasts = true;

        if (AbilityDragState.SourceActionBarSlot != this)
        {
            AbilityDragState.EndDrag();
            return;
        }

        if (!IsPointerOverActionBarDropTarget(eventData))
        {
            TryClearSourceSlotAfterMissedDrop();
            AbilityDragState.EndDrag();
            return;
        }

        StartCoroutine(CoDeferredEndAbilityDragFromBar(eventData));
    }

    private IEnumerator CoDeferredEndAbilityDragFromBar(PointerEventData eventData)
    {
        yield return null;

        if (AbilityDragState.SourceActionBarSlot == this)
            TryClearSourceSlotAfterMissedDrop(eventData);

        AbilityDragState.EndDrag();
    }

    private void TryClearSourceSlotAfterMissedDrop(PointerEventData eventData = null)
    {
        if (AbilityDragState.DropWasHandled)
            return;

        if (eventData != null && IsPointerOverActionBarDropTarget(eventData))
            return;

        ClearAssignment();
    }

    private static bool IsPointerOverActionBarDropTarget(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null)
            return false;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponentInParent<ActionBarSlotUI>() != null)
                return true;
        }

        return false;
    }

    private void CreateAbilityDragIcon()
    {
        DestroyAbilityDragIcon();
        if (_rootCanvas == null || assignedAction == null)
            return;

        _abilityDragIconGO = new GameObject("ActionBarAbilityDragIcon");
        _abilityDragIconGO.transform.SetParent(_rootCanvas.transform, false);

        _abilityDragIconRT = _abilityDragIconGO.AddComponent<RectTransform>();
        _abilityDragIconImage = _abilityDragIconGO.AddComponent<Image>();
        _abilityDragIconImage.raycastTarget = false;
        _abilityDragIconImage.sprite = assignedAction.icon;
        _abilityDragIconImage.preserveAspect = true;
        _abilityDragIconRT.sizeDelta = abilityDragIconSize;
    }

    private void UpdateAbilityDragIconPosition(PointerEventData eventData)
    {
        if (_abilityDragIconRT == null || _rootCanvas == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        _abilityDragIconRT.anchoredPosition = localPoint;
    }

    private void DestroyAbilityDragIcon()
    {
        if (_abilityDragIconGO != null)
            Destroy(_abilityDragIconGO);
        _abilityDragIconGO = null;
        _abilityDragIconRT = null;
        _abilityDragIconImage = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (AbilityDragState.HasDrag)
        {
            string abilityId = AbilityDragState.AbilityId;
            if (string.IsNullOrWhiteSpace(abilityId))
                return;

            AbilityDefinition abilityDef = GetAbilityDefinition(abilityId);
            if (!abilityDef)
            {
                Debug.LogWarning($"[ActionBar] Could not resolve AbilityDefinition for '{abilityId}'");
                return;
            }

            Sprite incomingIcon = AbilityDragState.AbilityIcon
                ? AbilityDragState.AbilityIcon
                : SkillsAbilityPresentationResolver.ResolveAbilityIcon(abilityDef);
            string incomingName = string.IsNullOrWhiteSpace(AbilityDragState.AbilityDisplayName)
                ? SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(abilityDef)
                : AbilityDragState.AbilityDisplayName;
            string incomingDescription = string.IsNullOrWhiteSpace(AbilityDragState.AbilityDescription)
                ? ResolveAbilityActionBarBodyText(abilityDef)
                : AbilityDragState.AbilityDescription;

            ActionBarAssignment abilityAssignment = ActionBarAssignment.CreateAbility(
                abilityDef.abilityId,
                incomingName,
                incomingIcon,
                incomingDescription
            );

            if (!CanAccept(abilityAssignment))
                return;

            HandleAbilityDropWithUniqueSwap(abilityAssignment);
            AbilityDragState.MarkDropHandled();
            AbilityDragState.EndDrag();
            return;
        }

        if (!InventoryDragState.HasDrag)
            return;
        if (InventoryDragState.Source != InventoryDragState.SourceKind.Inventory)
            return;

        int fromSlotIndex = InventoryDragState.FromSlotIndex;
        int requestedAmount = InventoryDragState.IsSplit
            ? InventoryDragState.CarriedAmount
            : (inventory != null && fromSlotIndex >= 0 ? inventory.GetSlot(fromSlotIndex).amount : 0);

        string itemId = InventoryDragState.ItemId;
        if (string.IsNullOrWhiteSpace(itemId) || requestedAmount <= 0)
            return;

        if (!inventory)
            return;

        ItemDefinition itemDef = inventory.GetItemDef(itemId);
        if (!itemDef)
            return;

        if (!itemDef.IsConsumable)
            return;

        if (TryStoreConsumableFromInventorySlot(fromSlotIndex, requestedAmount))
            InventoryDragState.EndDrag();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Consumables live on the bar as real stacks — never ClearAssignment on a failed return
            // or the entire stack is destroyed. Abilities can be cleared freely.
            if (assignedAction != null && assignedAction.IsItem)
                TryReturnStoredConsumableToInventory();
            else
                ClearAssignment();
        }
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

            tooltip.SetAnchor(transform);
            tooltip.ShowText(
                string.IsNullOrWhiteSpace(def.displayName) ? "Consumable" : def.displayName,
                BuildConsumableActionBarTooltip(def)
            );
        }
        else if (assignedAction.kind == ActionBarAssignmentKind.Ability)
        {
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

            AbilityDefinition def = GetAbilityDefinition(assignedAction.id);
            string title = def != null
                ? SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def)
                : (string.IsNullOrWhiteSpace(assignedAction.displayName) ? "Ability" : assignedAction.displayName);
            tooltip.SetAnchor(transform);
            tooltip.ShowText(
                title,
                BuildAbilityActionBarTooltip(def, assignedAction)
            );
        }
    }

    private static string BuildConsumableActionBarTooltip(ItemDefinition def)
    {
        if (!def)
            return "<color=#FFB347>Consumable</color>";

        CharacterStats stats = ConsumablePassiveModifiers.ResolveLocalPlayerStats();
        List<string> lines = new List<string>(6);
        string type = def.IsFood ? "Food" : def.IsPotion ? "Potion" : "Consumable";
        lines.Add($"<color=#FFB347>Consumable: {type}</color>");

        int displayHeal = ConsumablePassiveModifiers.GetEffectiveHealAmount(def, stats);
        if (displayHeal > 0)
            lines.Add($"<color=#FFB347>Heal: {displayHeal}</color>");

        if (def.EnergyAmount > 0)
            lines.Add($"<color=#FFB347>Energy: {def.EnergyAmount}</color>");

        if (def.HasGrantedEffect)
        {
            ConsumableGrantedEffect effect = ConsumablePassiveModifiers.GetEffectiveGrantedEffect(def, stats);
            lines.Add($"<color=#FFB347>Effect: {ConsumableEffectTooltip.Format(effect)}</color>");
        }

        if (def.HasFoodTimedBuffs || (def.IsFood && ConsumablePassiveModifiers.IsAlchemistsBoonActive(stats)))
        {
            string food = def.GetFoodTimedBuffSummaryText(stats);
            if (!string.IsNullOrWhiteSpace(food))
            {
                foreach (string part in food.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                        lines.Add($"<color=#FFB347>{part}</color>");
                }
            }
        }

        float displayCooldown = ConsumablePassiveModifiers.GetEffectiveUseCooldown(def, stats);
        if (displayCooldown > 0f)
            lines.Add($"<color=#FFB347>Cooldown: {displayCooldown:0.#}s</color>");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Action bar only: name is the <see cref="SharedTooltipUI"/> title; body is type tag (e.g. Minion), description, and weapon reqs — no effects/cost/enhancement block.
    /// </summary>
    private static string BuildAbilityActionBarTooltip(AbilityDefinition def, ActionBarAssignment assignment)
    {
        if (def == null)
            return string.IsNullOrWhiteSpace(assignment?.description) ? "Ability" : assignment.description.Trim();

        string tagLine = AbilityTooltipDamagePreview.BuildAbilityTooltipTagLine(def, orangeMarkup: true);
        string desc = ResolveAbilityActionBarBodyText(def, assignment);
        if (!string.IsNullOrEmpty(tagLine))
            desc = $"{tagLine}\n\n{desc}";

        CharacterStats previewStats = AbilityTooltipDamagePreview.FindLocalPlayerStats();
        string weaponLine = AbilityTooltipDamagePreview.BuildWeaponRequirementRichLine(def, previewStats, accentWhenOk: true);
        string afterDesc = string.IsNullOrEmpty(weaponLine) ? "" : $"\n\n{weaponLine}";

        return $"{desc}{afterDesc}";
    }

    /// <summary>Action bar body line: presentation/league intro when available, else stored assignment text, else legacy ability description.</summary>
    private static string ResolveAbilityActionBarBodyText(AbilityDefinition def)
    {
        return ResolveAbilityActionBarBodyText(def, null);
    }

    private static string ResolveAbilityActionBarBodyText(AbilityDefinition def, ActionBarAssignment assignment)
    {
        if (def != null &&
            AbilityTooltipDamagePreview.TryBuildActionBarCompactBody(def, SkillsManager.Instance, out string compactEffects))
            return compactEffects;

        if (def != null)
        {
            string intro = SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def);
            if (!string.IsNullOrWhiteSpace(intro) && intro != "No description.")
                return intro;
        }

        if (assignment != null && !string.IsNullOrWhiteSpace(assignment.description))
            return assignment.description.Trim();

        if (def != null && !string.IsNullOrWhiteSpace(def.presentation?.ShortDescription))
            return def.presentation.ShortDescription.Trim();

        string fb = SkillsAbilityPresentationResolver.ResolveAbilityPrimaryDescription(def);
        return fb != "No description." ? fb.Trim() : "Ability";
    }

    private System.Collections.IEnumerator ClickFeedback()
    {
        transform.localScale = originalScale * 0.9f;
        yield return new WaitForSeconds(0.08f);
        transform.localScale = originalScale;
    }

    private void HandleIdleCombatChanged(bool enabled)
    {
        isAutoBattleActive = enabled;
        RefreshAutoBattleBorder();
    }

    private void RefreshAutoBattleBorder()
    {
        bool hasAssigned = assignedAction != null && assignedAction.IsAssigned;
        bool abilityBlockedByWeapon = hasAssigned &&
                                      assignedAction != null &&
                                      assignedAction.IsAbility &&
                                      !abilityWeaponCompatible;
        bool abilityEligibleForAuto = true;
        if (hasAssigned && assignedAction != null && assignedAction.IsAbility)
        {
            AbilityDefinition def = GetAbilityDefinition(assignedAction.id);
            if (def != null && def.tag == AbilityTag.ToggleBuff && PlayerAbilityController.IsToggleBuffActive(def))
                abilityEligibleForAuto = true;
            else
                abilityEligibleForAuto = PlayerAbilityController.CanAbilityBeUsedByAutoBattle(def);
        }

        bool shouldShow = isAutoBattleActive &&
                          IsAutoUseSlotType() &&
                          !abilityBlockedByWeapon &&
                          abilityEligibleForAuto &&
                          (showBorderWhenEmpty || hasAssigned);
        SetAutoBattleBorderVisible(shouldShow);
    }

    private bool IsAutoUseSlotType()
    {
        return slotType == ActionBarSlotType.Ability ||
               slotType == ActionBarSlotType.Food ||
               slotType == ActionBarSlotType.Potion;
    }

    private void SetAutoBattleBorderVisible(bool visible)
    {
        if (autoBattleBorder != null)
            autoBattleBorder.gameObject.SetActive(visible);

        if (autoBattleBorderGraphic != null)
            autoBattleBorderGraphic.enabled = visible;
    }

    private void EnsurePrimedBackgroundExists()
    {
        if (primedBackgroundImage != null || !autoCreatePrimedBackground || iconImage == null)
            return;

        Transform existing = transform.Find("PrimedBackground");
        if (existing != null)
            primedBackgroundImage = existing.GetComponent<Image>();

        if (primedBackgroundImage == null)
        {
            GameObject go = new GameObject("PrimedBackground", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(iconImage.transform.parent != null ? iconImage.transform.parent : transform, false);
            go.transform.SetSiblingIndex(Mathf.Max(0, iconImage.transform.GetSiblingIndex()));

            RectTransform rt = go.GetComponent<RectTransform>();
            RectTransform iconRt = iconImage.rectTransform;
            rt.anchorMin = iconRt.anchorMin;
            rt.anchorMax = iconRt.anchorMax;
            rt.pivot = iconRt.pivot;
            rt.anchoredPosition = iconRt.anchoredPosition;
            rt.sizeDelta = iconRt.sizeDelta;
            rt.localScale = Vector3.one;

            primedBackgroundImage = go.GetComponent<Image>();
            primedBackgroundImage.raycastTarget = false;
            primedBackgroundImage.sprite = null;
            primedBackgroundImage.color = primedBackgroundColor;
            primedBackgroundImage.enabled = false;
            primedBackgroundImage.gameObject.SetActive(false);
        }
    }

    /// <summary>Assigns an ability from the skills list / palette; same swap rules as drag-drop onto this slot.</summary>
    public bool TryPaletteAssignAbilityWithUniqueSwap(ActionBarAssignment incoming)
    {
        if (incoming == null || !incoming.IsAbility)
            return false;
        if (!CanAccept(incoming))
            return false;
        HandleAbilityDropWithUniqueSwap(incoming);
        return true;
    }

    private void HandleAbilityDropWithUniqueSwap(ActionBarAssignment newAbilityAssignment)
    {
        if (newAbilityAssignment == null || !newAbilityAssignment.IsAbility)
        {
            Assign(newAbilityAssignment);
            return;
        }

        ActionBarSlotUI dragSourceSlot = AbilityDragState.SourceActionBarSlot;
        if (dragSourceSlot != null && dragSourceSlot != this)
        {
            SwapAbilityAssignmentsWithSlot(dragSourceSlot, newAbilityAssignment);
            return;
        }

        ActionBarSlotUI existingAbilitySlot = FindSlotWithAbilityId(newAbilityAssignment.id);
        if (existingAbilitySlot == null || existingAbilitySlot == this)
        {
            Assign(newAbilityAssignment);
            return;
        }

        ActionBarAssignment displaced = CopyAbilityAssignment(assignedAction);
        if (displaced != null && displaced.IsAssigned && existingAbilitySlot.CanAccept(displaced))
            existingAbilitySlot.Assign(displaced);
        else
            existingAbilitySlot.ClearAssignment();

        Assign(newAbilityAssignment);
    }

    private void SwapAbilityAssignmentsWithSlot(ActionBarSlotUI otherSlot, ActionBarAssignment incomingToThis)
    {
        if (otherSlot == null || otherSlot == this)
        {
            Assign(incomingToThis);
            return;
        }

        ActionBarAssignment displaced = CopyAbilityAssignment(assignedAction);
        if (displaced != null && displaced.IsAssigned && otherSlot.CanAccept(displaced))
            otherSlot.Assign(displaced);
        else
            otherSlot.ClearAssignment();

        Assign(incomingToThis);
    }

    private static ActionBarAssignment CopyAbilityAssignment(ActionBarAssignment source)
    {
        if (source == null || !source.IsAssigned || !source.IsAbility)
            return null;

        return ActionBarAssignment.CreateAbility(
            source.id,
            source.displayName,
            source.icon,
            source.description);
    }

    private ActionBarSlotUI FindSlotWithAbilityId(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;

        ActionBarUI actionBar = actionBarOwner != null
            ? actionBarOwner
            : FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (actionBar == null)
            return null;

        foreach (ActionBarSlotUI slot in actionBar.GetSlots())
        {
            if (slot == null)
                continue;

            ActionBarAssignment action = slot.AssignedAction;
            if (action == null || !action.IsAbility)
                continue;

            if (string.Equals(action.id, abilityId, StringComparison.OrdinalIgnoreCase))
                return slot;
        }

        return null;
    }

    private ItemDefinition ResolveItemDefForAssignment(ActionBarAssignment action)
    {
        if (action == null || !action.IsItem || string.IsNullOrWhiteSpace(action.id))
            return null;

        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        return inventory ? inventory.GetItemDef(action.id) : null;
    }

    public bool TryStoreConsumableFromInventorySlot(int sourceSlotIndex, int amountToMove)
    {
        if (sourceSlotIndex < 0)
            return false;

        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (!inventory)
            return false;

        Inventory.Slot source = inventory.GetSlot(sourceSlotIndex);
        if (source.IsEmpty || string.IsNullOrWhiteSpace(source.itemId))
            return false;

        ItemDefinition sourceDef = inventory.GetItemDef(source.itemId);
        if (!sourceDef || !sourceDef.IsConsumable)
            return false;

        ActionBarAssignment incoming = ActionBarAssignment.CreateItem(sourceDef);
        if (incoming == null || !incoming.IsAssigned || !CanAccept(incoming, sourceDef))
            return false;

        int toMove = Mathf.Clamp(amountToMove, 1, source.amount);
        int removed = inventory.RemoveAmountAtSlot(sourceSlotIndex, toMove);
        if (removed <= 0)
            return false;

        string incomingId = Inventory.RemapLegacyItemId(source.itemId);
        string existingId = assignedAction != null && assignedAction.IsItem
            ? Inventory.RemapLegacyItemId(assignedAction.id)
            : null;
        int existingAmount = AssignedItemAmount;

        if (!string.IsNullOrWhiteSpace(existingId) &&
            !string.Equals(existingId, incomingId, StringComparison.OrdinalIgnoreCase) &&
            existingAmount > 0)
        {
            // Inventory.Add can partially succeed and still return false — only drop the remainder.
            int returned = inventory.AddPartial(existingId, existingAmount, notifyItemGainPopup: false);
            int left = existingAmount - returned;
            if (left > 0)
            {
                ItemDefinition existingDef = inventory.GetItemDef(existingId);
                if (DropManager.Instance != null)
                    DropManager.Instance.Spawn(existingId, left, existingDef ? existingDef.icon : null);
                else
                    PendingLootRecoveryStore.Enqueue(existingId, left);
            }
        }

        if (string.Equals(existingId, incomingId, StringComparison.OrdinalIgnoreCase))
            assignedItemAmount += removed;
        else
        {
            assignedAction = incoming;
            assignedItemAmount = removed;
        }

        RefreshUI();
        if (isPointerOver)
            ShowTooltip();
        onAssignmentChanged?.Invoke(this);
        return true;
    }

    public bool TryConsumeStoredItem(int amount)
    {
        if (amount <= 0 || assignedAction == null || !assignedAction.IsItem)
            return false;

        if (assignedItemAmount < amount)
            return false;

        assignedItemAmount -= amount;
        if (assignedItemAmount <= 0)
            ClearAssignment(notify: true);
        else
        {
            RefreshUI();
            if (isPointerOver)
                ShowTooltip();
            onSlottedAmountChanged?.Invoke(this);
        }
        return true;
    }

    public void SetAssignedItemAmountFromSave(int amount, bool notify)
    {
        if (assignedAction == null || !assignedAction.IsItem)
        {
            assignedItemAmount = 0;
            return;
        }

        assignedItemAmount = Mathf.Max(0, amount);
        if (assignedItemAmount <= 0)
        {
            ClearAssignment(notify);
            return;
        }

        RefreshUI();
        if (isPointerOver)
            ShowTooltip();
        if (notify)
            onAssignmentChanged?.Invoke(this);
    }

    private bool TryReturnStoredConsumableToInventory()
    {
        if (assignedAction == null || !assignedAction.IsItem || assignedItemAmount <= 0)
            return false;

        string id = Inventory.RemapLegacyItemId(assignedAction.id);
        int amount = Mathf.Max(0, assignedItemAmount);
        if (amount <= 0 || string.IsNullOrWhiteSpace(id))
            return false;

        if (!inventory)
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (!inventory)
        {
            // Bar still holds real items — park them for recovery rather than wiping the assignment.
            PendingLootRecoveryStore.Enqueue(id, amount);
            ClearAssignment(notify: true);
            SaveManager.Instance?.NotifyInventoryChangedDebounced();
            return true;
        }

        // Inventory.Add can partially succeed and still return false — only drop the remainder.
        int returned = inventory.AddPartial(id, amount, notifyItemGainPopup: false);
        int left = amount - returned;
        if (left > 0)
        {
            ItemDefinition def = inventory.GetItemDef(id);
            if (DropManager.Instance != null)
                DropManager.Instance.Spawn(id, left, def ? def.icon : null);
            else
                PendingLootRecoveryStore.Enqueue(id, left);
        }

        ClearAssignment(notify: true);
        return true;
    }

    private AbilityDefinition GetAbilityDefinition(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;
        if (abilityDatabase == null)
            abilityDatabase = AbilityDatabase.LoadDefault();
        return abilityDatabase ? abilityDatabase.Get(abilityId) : null;
    }

    private void SyncTooltipReferenceFromPeerSlots()
    {
        if (!autoSyncTooltipFromPeerSlots)
            return;

        ActionBarUI actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (actionBar == null)
            return;

        foreach (ActionBarSlotUI slot in actionBar.GetSlots())
        {
            if (slot == null || slot == this || slot.tooltip == null)
                continue;

            bool isConsumableSlot = slot.slotType == ActionBarSlotType.Food || slot.slotType == ActionBarSlotType.Potion;
            if (!isConsumableSlot)
                continue;

            tooltip = slot.tooltip;
            tooltipHeightRect = slot.tooltipHeightRect;
            preferredSide = slot.preferredSide;
            return;
        }
    }

    private void EnsureAutoBattleBorderExists()
    {
        if (autoBattleBorder == null)
        {
            if (!autoCreateBorderIfMissing)
                return;

            GameObject borderGO = new GameObject("AutoBattleBorder", typeof(RectTransform));
            borderGO.transform.SetParent(transform, false);
            borderGO.transform.SetAsLastSibling();

            RectTransform rt = borderGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(borderInset, borderInset);
            rt.offsetMax = new Vector2(-borderInset, -borderInset);
            autoBattleBorder = rt;
        }

        if (topEdge == null) topEdge = EnsureEdge("TopEdge");
        if (rightEdge == null) rightEdge = EnsureEdge("RightEdge");
        if (bottomEdge == null) bottomEdge = EnsureEdge("BottomEdge");
        if (leftEdge == null) leftEdge = EnsureEdge("LeftEdge");

        LayoutEdges();
        ApplyDashVisuals();
    }

    private RawImage EnsureEdge(string edgeName)
    {
        if (autoBattleBorder == null)
            return null;

        Transform t = autoBattleBorder.Find(edgeName);
        RawImage edge = t ? t.GetComponent<RawImage>() : null;
        if (edge == null)
        {
            GameObject go = new GameObject(edgeName, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(autoBattleBorder, false);
            edge = go.GetComponent<RawImage>();
            edge.raycastTarget = false;
        }

        return edge;
    }

    private void LayoutEdges()
    {
        int thickness = Mathf.Max(1, runtimeBorderThicknessPx);
        SetupTopBottom(topEdge, true, thickness);
        SetupTopBottom(bottomEdge, false, thickness);
        SetupLeftRight(leftEdge, false, thickness);
        SetupLeftRight(rightEdge, true, thickness);
    }

    private static void SetupTopBottom(RawImage edge, bool top, int thickness)
    {
        if (edge == null) return;
        RectTransform rt = edge.rectTransform;
        rt.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
        rt.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
        rt.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, thickness);
    }

    private static void SetupLeftRight(RawImage edge, bool right, int thickness)
    {
        if (edge == null) return;
        RectTransform rt = edge.rectTransform;
        rt.anchorMin = right ? new Vector2(1f, 0f) : Vector2.zero;
        rt.anchorMax = right ? Vector2.one : new Vector2(0f, 1f);
        rt.pivot = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(thickness, 0f);
    }

    private void ApplyDashVisuals()
    {
        Texture2D horizontalDash = GetOrCreateDashTexture(false);
        Texture2D verticalDash = GetOrCreateDashTexture(true);

        ApplyEdgeTexture(topEdge, horizontalDash);
        ApplyEdgeTexture(bottomEdge, horizontalDash);
        ApplyEdgeTexture(leftEdge, verticalDash);
        ApplyEdgeTexture(rightEdge, verticalDash);

        RefreshDashUv();
    }

    private void ApplyEdgeTexture(RawImage edge, Texture2D texture)
    {
        if (edge == null || texture == null)
            return;

        edge.texture = texture;
        edge.color = autoBattleBorderColor;
    }

    private Texture2D GetOrCreateDashTexture(bool vertical)
    {
        int thickness = Mathf.Max(1, runtimeBorderThicknessPx);
        int dash = Mathf.Max(1, runtimeDashLengthPx);
        int gap = Mathf.Max(1, runtimeDashGapPx);
        int pattern = dash + gap;
        string key = $"{(vertical ? "v" : "h")}_{thickness}_{dash}_{gap}";

        if (RuntimeDashTextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;

        int width = vertical ? thickness : pattern;
        int height = vertical ? pattern : thickness;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.name = $"AutoBattleDash_{key}";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color32[] pixels = new Color32[width * height];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int patternPos = vertical ? y : x;
                bool filled = patternPos < dash;
                pixels[y * width + x] = filled ? white : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        RuntimeDashTextureCache[key] = tex;
        return tex;
    }

    private void RefreshDashUv()
    {
        if (autoBattleBorder == null)
            return;

        float width = Mathf.Max(1f, autoBattleBorder.rect.width);
        float height = Mathf.Max(1f, autoBattleBorder.rect.height);
        float pattern = Mathf.Max(1f, runtimeDashLengthPx + runtimeDashGapPx);
        float repeatX = width / pattern;
        float repeatY = height / pattern;
        float phase = autoBattleDashPhase;

        if (topEdge != null) topEdge.uvRect = new Rect(phase, 0f, repeatX, 1f);
        if (rightEdge != null) rightEdge.uvRect = new Rect(0f, -phase, 1f, repeatY);
        if (bottomEdge != null) bottomEdge.uvRect = new Rect(-phase, 0f, repeatX, 1f);
        if (leftEdge != null) leftEdge.uvRect = new Rect(0f, phase, 1f, repeatY);
    }

}