using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI row/button for an ability. Supports drag/drop into the action bar.
/// </summary>
public class AbilityEntryUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text reqText;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Shown when the tier is unlocked but no ability is picked yet (e.g. green + row).")]
    [SerializeField] private GameObject selectAbilityRoot;
    [SerializeField] private Button selectButton;

    [Header("Drag")]
    [SerializeField] private Vector2 dragIconSize = new Vector2(48f, 48f);

    private AbilityDefinition _def;
    private bool _unlocked;
    private bool _isAvailablePlaceholder;

    private SharedTooltipUI _tooltip;
    private Canvas _rootCanvas;
    private RectTransform _tooltipBoundsRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Left;

    private GameObject _dragIconGO;
    private RectTransform _dragIconRT;
    private Image _dragIconImage;

    private System.Action<AbilityDefinition> _onDoubleClickAssign;

    private Outline _committedListRowOutline;
    private Button _rowButton;

    private void Awake()
    {
        _rowButton = GetComponent<Button>();
        if (_rowButton != null)
        {
            Navigation n = _rowButton.navigation;
            n.mode = Navigation.Mode.None;
            _rowButton.navigation = n;
        }

        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            _committedListRowOutline = rootImage.GetComponent<Outline>();
            if (_committedListRowOutline == null)
                _committedListRowOutline = rootImage.gameObject.AddComponent<Outline>();
            _committedListRowOutline.effectColor = new Color(1f, 1f, 1f, 0.95f);
            _committedListRowOutline.effectDistance = new Vector2(2f, 2f);
            _committedListRowOutline.useGraphicAlpha = false;
            _committedListRowOutline.enabled = false;
        }
    }

    // Called by SkillsAbilitiesPageUI when creating runtime rows (no prefab).
    // Uses SendMessage to avoid making fields public.
    private void EditorAutoWire(object[] args)
    {
        if (args == null || args.Length < 4) return;
        icon = args[0] as Image;
        nameText = args[1] as TMP_Text;
        reqText = args[2] as TMP_Text;
        canvasGroup = args[3] as CanvasGroup;
    }

    public void Bind(AbilityDefinition def, bool unlocked, SharedTooltipUI tooltip, Canvas rootCanvas, System.Action onRowClickScrollToTree = null)
    {
        ClearAbilityRowClickListeners();
        _isAvailablePlaceholder = false;

        _def = def;
        _unlocked = unlocked;
        _tooltip = tooltip;
        _rootCanvas = rootCanvas;
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            _rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (selectAbilityRoot)
            selectAbilityRoot.SetActive(false);

        if (icon)
        {
            icon.gameObject.SetActive(true);
            Sprite spr = def ? SkillsAbilityPresentationResolver.ResolveAbilityIcon(def) : null;
            icon.enabled = def != null && spr != null;
            icon.sprite = spr;
            icon.preserveAspect = true;
        }

        if (nameText)
            nameText.text = def ? SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def) : "—";

        if (reqText)
            reqText.text = def ? $"Lv {def.unlockLevel}" : "";

        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = unlocked ? 1f : 0.55f;

        SetCommittedAbilityListRowOutline(true);
        RegisterRootRowScrollClick(onRowClickScrollToTree);
    }

    /// <summary>
    /// Right-panel row: skill level reached the tier but the player has not committed a tree pick for that row yet.
    /// </summary>
    public void BindAvailableAbilityTier(int rowLevel, SharedTooltipUI tooltip, Canvas rootCanvas, System.Action onSelectScrollTree)
    {
        ClearAbilityRowClickListeners();
        _isAvailablePlaceholder = true;
        _def = null;
        _unlocked = false;
        _tooltip = tooltip;
        _rootCanvas = rootCanvas;
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            _rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (selectAbilityRoot)
            selectAbilityRoot.SetActive(selectButton != null);

        if (icon)
        {
            icon.gameObject.SetActive(false);
            icon.sprite = null;
            icon.enabled = false;
        }

        if (nameText)
            nameText.text = "Ability Available";

        if (reqText)
            reqText.text = $"Lv {rowLevel}";

        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (selectButton != null)
        {
            selectButton.interactable = true;
            if (onSelectScrollTree != null)
                selectButton.onClick.AddListener(() => onSelectScrollTree());
        }

        SetCommittedAbilityListRowOutline(false);
        RegisterRootRowScrollClick(onSelectScrollTree);
    }

    private void SetCommittedAbilityListRowOutline(bool enabled)
    {
        if (_committedListRowOutline != null)
            _committedListRowOutline.enabled = enabled;
    }

    private void ClearAbilityRowClickListeners()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveAllListeners();
        if (_rowButton != null)
            _rowButton.onClick.RemoveAllListeners();
    }

    private void RegisterRootRowScrollClick(System.Action scrollAction)
    {
        if (_rowButton == null)
            _rowButton = GetComponent<Button>();
        if (_rowButton == null || scrollAction == null)
            return;

        _rowButton.onClick.AddListener(() => scrollAction());
    }

    public void SetDoubleClickAssignHandler(System.Action<AbilityDefinition> handler)
    {
        _onDoubleClickAssign = handler;
    }

    public void SetTooltipDocking(RectTransform tooltipBoundsRect, FlipInsideBounds.PreferredSide preferredSide)
    {
        _tooltipBoundsRect = tooltipBoundsRect;
        _preferredSide = preferredSide;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isAvailablePlaceholder || _tooltip == null || _def == null) return;

        string body = BuildLeagueStyleTooltip(_def, SkillsManager.Instance, AbilityTooltipDamagePreview.FindLocalPlayerStats());
        RectTransform measure = _tooltipBoundsRect ? _tooltipBoundsRect : transform.root as RectTransform;
        Transform anchor = icon != null ? icon.transform : transform;
        _tooltip.ShowTextAt(
            anchor,
            SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(_def),
            body,
            measureRect: measure,
            heightRect: measure,
            preferredSide: _preferredSide
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isAvailablePlaceholder)
            return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (eventData.clickCount >= 2)
        {
            if (!_unlocked || _def == null)
                return;
            _onDoubleClickAssign?.Invoke(_def);
            return;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isAvailablePlaceholder || !_unlocked || _def == null) return;
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            return;

        string dragName = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(_def);
        string dragDesc = ResolveAbilityDragDescription(_def);
        AbilityDragState.BeginDrag(_def.abilityId, SkillsAbilityPresentationResolver.ResolveAbilityIcon(_def), dragName, dragDesc);
        CreateDragIcon();
        UpdateDragIconPosition(eventData);
        if (canvasGroup) canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRT == null) return;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyDragIcon();
        if (canvasGroup) canvasGroup.blocksRaycasts = true;
        AbilityDragState.EndDrag();
    }

    private void CreateDragIcon()
    {
        DestroyDragIcon();

        _dragIconGO = new GameObject("AbilityDragIcon");
        _dragIconGO.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRT = _dragIconGO.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGO.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = _def != null ? SkillsAbilityPresentationResolver.ResolveAbilityIcon(_def) : null;
        _dragIconImage.preserveAspect = true;

        _dragIconRT.sizeDelta = dragIconSize;
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (_dragIconRT == null || _rootCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        _dragIconRT.anchoredPosition = localPoint;
    }

    private void DestroyDragIcon()
    {
        if (_dragIconGO) Destroy(_dragIconGO);
        _dragIconGO = null;
        _dragIconRT = null;
        _dragIconImage = null;
    }

    private static string BuildLeagueStyleTooltip(AbilityDefinition def, SkillsManager skillsManager, CharacterStats stats)
    {
        if (!def) return "";

        string tagLine = AbilityTooltipDamagePreview.BuildAbilityTooltipTagLine(def, orangeMarkup: false);
        string desc = BuildAbilityDescription(def);
        if (!string.IsNullOrEmpty(tagLine))
            desc = $"{tagLine}\n\n{desc}";

        string weaponLine = AbilityTooltipDamagePreview.BuildWeaponRequirementRichLine(def, stats, accentWhenOk: true);
        string afterDesc = string.IsNullOrEmpty(weaponLine) ? "" : $"\n\n{weaponLine}";

        string choiceLine = BuildActiveEnhancementLineForAbility(def, skillsManager);
        string statsSection = AbilityTooltipDamagePreview.BuildAbilityTooltipStatsSection(def, stats, skillsManager, orangeMarkup: false);

        return $"{desc}{afterDesc}\n\n{statsSection}{choiceLine}";
    }

    private static string BuildActiveEnhancementLineForAbility(AbilityDefinition def, SkillsManager skillsManager)
    {
        if (!def || skillsManager == null)
            return string.Empty;

        if (string.Equals(def.abilityId, "power_slash", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.WhirlwindAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_0", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.RendAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.EnvenomAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 5, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, "cleaving_strikes", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_1", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, "crescent_slash", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, "Lv15_2", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, "soulforged_weapon", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 35, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        // Woodcutting tree: each ability sits in its own spine row, so the choice is keyed by
        // the spine ID rather than the level (e.g. Cleaving Chop and Spectral Axe share Lv25 and
        // would otherwise collide on the legacy int key).
        if (string.Equals(def.abilityId, AbilityCombatPower.LumberFrenzyAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv5_0", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.FishingFrenzyAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Fishing, "Lv5_0", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.CleavingChopAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv25_0", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.SpectralAxeAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Woodcutting, "Lv25_1", -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.AvatarOfTheForestAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Woodcutting, AbilityCombatPower.AvatarOfTheForestEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        return string.Empty;
    }

    private static string BuildAbilityDescription(AbilityDefinition def)
    {
        if (!def) return "No description.";
        string intro = SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def);
        return string.IsNullOrWhiteSpace(intro) || intro == "No description." ? "No description." : intro;
    }

    private static string ResolveAbilityDragDescription(AbilityDefinition def)
    {
        if (!def)
            return "Ability";

        string intro = SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def);
        if (!string.IsNullOrWhiteSpace(intro) && intro != "No description.")
            return intro;

        string fb = SkillsAbilityPresentationResolver.ResolveAbilityPrimaryDescription(def);
        return string.IsNullOrWhiteSpace(fb) || fb == "No description." ? "Ability" : fb.Trim();
    }

    private static string BuildActiveEnhancementLine(AbilityDefinition def, int selectedIndex) =>
        AbilityTooltipDamagePreview.FormatActiveEnhancementLine(def, selectedIndex);
}

