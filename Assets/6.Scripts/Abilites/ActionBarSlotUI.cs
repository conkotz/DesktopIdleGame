using TMPro;
using System;
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
    [SerializeField] private AbilityDatabase abilityDatabase;

    private Inventory inventory;
    private bool isPointerOver;

    public ActionBarSlotType SlotType => slotType;
    public int SlotIndex => slotIndex;
    public ActionBarAssignment AssignedAction => assignedAction;

    private System.Action<ActionBarSlotUI> onPressed;
    private System.Action<ActionBarSlotUI> onAssignmentChanged;
    private Vector3 originalScale;
    private bool isAutoBattleActive;
    private bool abilityWeaponCompatible = true;
    private float autoBattleDashPhase;
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
            iconImage.preserveAspect = true;
        }

        RefreshAutoBattleBorder();
    }

    public void SetStackText(int amount)
    {
        if (stackText == null) return;
        stackText.text = amount > 0 ? amount.ToString() : "";
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

    public void SetPrimedVisual(bool primed)
    {
        EnsurePrimedBackgroundExists();
        if (primedBackgroundImage == null)
            return;

        primedBackgroundImage.color = primedBackgroundColor;
        primedBackgroundImage.enabled = primed;
        primedBackgroundImage.gameObject.SetActive(primed);
    }

    public void SetNoStockVisual(bool noStock)
    {
        if (noStockBackgroundImage == null)
            return;

        noStockBackgroundImage.enabled = noStock;
        noStockBackgroundImage.gameObject.SetActive(noStock);
    }

    public void SetAbilityWeaponCompatibility(bool canUseWithCurrentWeapon)
    {
        abilityWeaponCompatible = canUseWithCurrentWeapon;
        RefreshAutoBattleBorder();
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

            Sprite incomingIcon = AbilityDragState.AbilityIcon ? AbilityDragState.AbilityIcon : abilityDef.icon;
            string incomingName = string.IsNullOrWhiteSpace(AbilityDragState.AbilityDisplayName)
                ? abilityDef.displayName
                : AbilityDragState.AbilityDisplayName;
            string incomingDescription = string.IsNullOrWhiteSpace(AbilityDragState.AbilityDescription)
                ? abilityDef.description
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
            AbilityDragState.EndDrag();
            return;
        }

        if (!InventoryDragState.HasDrag)
            return;

        string itemId = InventoryDragState.ItemId;
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (!inventory)
            return;

        ItemDefinition itemDef = inventory.GetItemDef(itemId);
        if (!itemDef)
            return;

        if (!itemDef.IsConsumable)
            return;

        ActionBarAssignment itemAssignment = ActionBarAssignment.CreateItem(itemDef);

        if (!CanAccept(itemAssignment, itemDef))
            return;

        Assign(itemAssignment);
        InventoryDragState.EndDrag();
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
            tooltip.SetAnchor(transform);
            tooltip.ShowText(
                assignedAction.displayName,
                BuildAbilityActionBarTooltip(def, assignedAction)
            );
        }
    }

    private static string BuildConsumableActionBarTooltip(ItemDefinition def)
    {
        if (!def)
            return "<color=#FFB347>Consumable</color>";

        List<string> lines = new List<string>(6);
        string type = def.IsFood ? "Food" : def.IsPotion ? "Potion" : "Consumable";
        lines.Add($"<color=#FFB347>Consumable: {type}</color>");

        if (def.HealAmount > 0)
            lines.Add($"<color=#FFB347>Heal: {def.HealAmount}</color>");

        if (def.EnergyAmount > 0)
            lines.Add($"<color=#FFB347>Energy: {def.EnergyAmount}</color>");

        if (def.HasGrantedEffect)
            lines.Add($"<color=#FFB347>Effect: {ConsumableEffectTooltip.Format(def.GrantedEffect)}</color>");

        if (def.UseCooldown > 0f)
            lines.Add($"<color=#FFB347>Cooldown: {def.UseCooldown:0.#}s</color>");

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
        string desc = string.IsNullOrWhiteSpace(assignment?.description) ? "Ability" : assignment.description.Trim();
        if (!string.IsNullOrEmpty(tagLine))
            desc = $"{tagLine}\n\n{desc}";

        CharacterStats previewStats = AbilityTooltipDamagePreview.FindLocalPlayerStats();
        string weaponLine = AbilityTooltipDamagePreview.BuildWeaponRequirementRichLine(def, previewStats, orangeWhenOk: true);
        string afterDesc = string.IsNullOrEmpty(weaponLine) ? "" : $"\n\n{weaponLine}";

        return $"{desc}{afterDesc}";
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
        bool shouldShow = isAutoBattleActive &&
                          IsAutoUseSlotType() &&
                          !abilityBlockedByWeapon &&
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

    private void HandleAbilityDropWithUniqueSwap(ActionBarAssignment newAbilityAssignment)
    {
        if (newAbilityAssignment == null || !newAbilityAssignment.IsAbility)
        {
            Assign(newAbilityAssignment);
            return;
        }

        ActionBarSlotUI existingAbilitySlot = FindSlotWithAbilityId(newAbilityAssignment.id);
        if (existingAbilitySlot == null || existingAbilitySlot == this)
        {
            Assign(newAbilityAssignment);
            return;
        }

        ActionBarAssignment targetOldAssignment = assignedAction;
        bool canSwapBack = existingAbilitySlot.CanAccept(targetOldAssignment, ResolveItemDefForAssignment(targetOldAssignment));

        if (canSwapBack)
        {
            existingAbilitySlot.Assign(targetOldAssignment);
        }
        else
        {
            existingAbilitySlot.ClearAssignment();
        }

        Assign(newAbilityAssignment);
    }

    private ActionBarSlotUI FindSlotWithAbilityId(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;

        ActionBarUI actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
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