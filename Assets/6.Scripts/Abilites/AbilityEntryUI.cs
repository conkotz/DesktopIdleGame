using System;
using System.Collections;
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
    [SerializeField] private GameObject notSelectedRoot;

    [Header("Drag")]
    [SerializeField] private Vector2 dragIconSize = new Vector2(48f, 48f);

    private AbilityDefinition _def;
    private bool _unlocked;
    private bool _isAvailablePlaceholder;

    private SharedTooltipUI _tooltip;
    private Canvas _rootCanvas;
    private RectTransform _tooltipBoundsRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Right;

    private GameObject _dragIconGO;
    private RectTransform _dragIconRT;
    private Image _dragIconImage;

    private System.Action<AbilityDefinition> _onDoubleClickAssign;
    private System.Action<int> _onRightClickRow;
    private int _rowLevel;

    public bool IsAvailablePlaceholder => _isAvailablePlaceholder;
    public int RowLevel => _rowLevel;
    public AbilityDefinition BoundAbility => _def;

    private Outline _committedListRowOutline;
    private Button _rowButton;
    private float _defaultNameFontSize = -1f;
    private float _defaultRowPreferredHeight = -1f;
    private float _defaultNamePreferredHeight = -1f;
    private TextWrappingModes _defaultNameWrapMode;
    private HorizontalLayoutGroup _rowGroupLayout;
    private LayoutElement _rowLayoutElement;
    private LayoutElement _nameLayoutElement;

    [SerializeField] private float compactNameFontSize = 16f;
    [SerializeField] private float rowExpandedVerticalPadding = 10f;

    private const float NotSelectedFadeSeconds = 0.35f;
    private const float NotSelectedHoldOpaqueSeconds = 2f;
    private CanvasGroup _notSelectedCanvasGroup;
    private Image _notSelectedImage;
    private Color _notSelectedImageBaseColor = Color.white;
    private Coroutine _notSelectedFlashRoutine;

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
        {
            nameText.text = def ? SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(def) : "—";
            CaptureDefaultNameFontSize();
        }

        if (reqText)
            reqText.text = def ? $"Lv {def.unlockLevel}" : "";

        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = unlocked ? 1f : 0.55f;

        SetCommittedAbilityListRowOutline(true);
        RegisterRootRowScrollClick(onRowClickScrollToTree);
        EnsureChildGraphicsIgnoreRaycasts();
        SetNotSelectedPrompt(false);
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
        EnsureChildGraphicsIgnoreRaycasts();
        SetNotSelectedPrompt(false);
    }

    private void OnDisable() => SetNotSelectedPrompt(false);

    /// <summary>Pulses <see cref="notSelectedRoot"/> when enhancement choices are pending (matches timeline nodes).</summary>
    public void SetNotSelectedPrompt(bool show)
    {
        EnsureNotSelectedReference();

        if (!show)
        {
            if (_notSelectedFlashRoutine != null)
            {
                StopCoroutine(_notSelectedFlashRoutine);
                _notSelectedFlashRoutine = null;
            }

            ApplyNotSelectedAlpha(0f);
            if (notSelectedRoot != null)
                notSelectedRoot.SetActive(false);
            return;
        }

        if (notSelectedRoot == null)
            return;

        if (_notSelectedCanvasGroup == null && _notSelectedImage == null)
            CacheNotSelectedVisualDriver();
        if (_notSelectedCanvasGroup == null && _notSelectedImage == null)
            return;

        notSelectedRoot.SetActive(true);
        if (_notSelectedFlashRoutine == null && isActiveAndEnabled)
            _notSelectedFlashRoutine = StartCoroutine(NotSelectedFlashLoop());
    }

    private void EnsureNotSelectedReference()
    {
        if (notSelectedRoot != null)
            return;

        Transform t = transform.Find("RowGroup/NotSelected") ?? transform.Find("NotSelected");
        if (t != null)
            notSelectedRoot = t.gameObject;
    }

    private void CacheNotSelectedVisualDriver()
    {
        if (notSelectedRoot == null)
            return;

        _notSelectedCanvasGroup = notSelectedRoot.GetComponent<CanvasGroup>();
        _notSelectedImage = notSelectedRoot.GetComponent<Image>();
        if (_notSelectedImage != null)
        {
            _notSelectedImageBaseColor = _notSelectedImage.color;
            _notSelectedImage.raycastTarget = false;
        }
    }

    private void ApplyNotSelectedAlpha(float a)
    {
        a = Mathf.Clamp01(a);
        if (_notSelectedCanvasGroup != null)
            _notSelectedCanvasGroup.alpha = a;
        else if (_notSelectedImage != null)
        {
            Color c = _notSelectedImageBaseColor;
            c.a = a * _notSelectedImageBaseColor.a;
            _notSelectedImage.color = c;
        }
    }

    private IEnumerator NotSelectedFlashLoop()
    {
        ApplyNotSelectedAlpha(0f);

        while (true)
        {
            yield return FadeNotSelectedAlpha(0f, 1f, NotSelectedFadeSeconds);
            float hold = 0f;
            while (hold < NotSelectedHoldOpaqueSeconds)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return FadeNotSelectedAlpha(1f, 0f, NotSelectedFadeSeconds);
            yield return null;
        }
    }

    private IEnumerator FadeNotSelectedAlpha(float from, float to, float duration)
    {
        if (duration <= 0.0001f)
        {
            ApplyNotSelectedAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            ApplyNotSelectedAlpha(Mathf.Lerp(from, to, u));
            yield return null;
        }

        ApplyNotSelectedAlpha(to);
    }

    public void SetRowLevelContext(int rowLevel, System.Action<int> onRightClickRow)
    {
        _rowLevel = rowLevel;
        _onRightClickRow = onRightClickRow;
    }

    private void EnsureChildGraphicsIgnoreRaycasts()
    {
        if (icon)
            icon.raycastTarget = false;
        if (nameText)
            nameText.raycastTarget = false;
        if (reqText)
            reqText.raycastTarget = false;
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

    public void SetNameLayoutCompact(bool compact)
    {
        if (nameText == null)
            return;

        EnsureRowLayoutRefs();
        CaptureDefaultNameFontSize();
        CaptureDefaultRowLayout();

        nameText.fontSize = compact ? compactNameFontSize : _defaultNameFontSize;
        nameText.textWrappingMode = compact ? TextWrappingModes.Normal : _defaultNameWrapMode;
        nameText.overflowMode = TextOverflowModes.Overflow;

        if (_rowGroupLayout != null)
            _rowGroupLayout.childControlHeight = compact;

        if (_nameLayoutElement != null)
            _nameLayoutElement.preferredHeight = compact ? -1f : _defaultNamePreferredHeight;

        ApplyRowHeight(compact);
    }

    private void EnsureRowLayoutRefs()
    {
        if (_rowLayoutElement == null)
            TryGetComponent(out _rowLayoutElement);

        if (_nameLayoutElement == null && nameText != null)
            nameText.TryGetComponent(out _nameLayoutElement);

        if (_rowGroupLayout != null)
            return;

        Transform rowGroup = transform.Find("RowGroup");
        if (rowGroup != null)
            rowGroup.TryGetComponent(out _rowGroupLayout);
    }

    private void CaptureDefaultNameFontSize()
    {
        if (nameText == null || _defaultNameFontSize >= 0f)
            return;

        _defaultNameFontSize = nameText.fontSize;
        _defaultNameWrapMode = nameText.textWrappingMode;
    }

    private void CaptureDefaultRowLayout()
    {
        if (_defaultRowPreferredHeight >= 0f)
            return;

        if (_rowLayoutElement != null && _rowLayoutElement.preferredHeight > 0f)
            _defaultRowPreferredHeight = _rowLayoutElement.preferredHeight;
        else if (transform is RectTransform rt && rt.sizeDelta.y > 0f)
            _defaultRowPreferredHeight = rt.sizeDelta.y;
        else
            _defaultRowPreferredHeight = 60f;

        if (_nameLayoutElement != null && _nameLayoutElement.preferredHeight > 0f)
            _defaultNamePreferredHeight = _nameLayoutElement.preferredHeight;
        else
            _defaultNamePreferredHeight = 50f;
    }

    private void ApplyRowHeight(bool expandedNarrowColumn)
    {
        float height = _defaultRowPreferredHeight;

        if (expandedNarrowColumn)
        {
            float nameWidth = ResolveNameTextLayoutWidth();
            RectTransform nameRt = nameText.rectTransform;
            if (nameRt != null && nameWidth > 1f)
                nameRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, nameWidth);

            nameText.ForceMeshUpdate();
            float textHeight = nameText.preferredHeight;
            float iconHeight = 0f;
            if (icon != null && icon.transform is RectTransform iconRt)
                iconHeight = iconRt.rect.height > 1f ? iconRt.rect.height : iconRt.sizeDelta.y;

            height = Mathf.Max(_defaultRowPreferredHeight, Mathf.Max(textHeight, iconHeight) + rowExpandedVerticalPadding);
        }

        if (_rowLayoutElement != null)
        {
            _rowLayoutElement.preferredHeight = height;
            _rowLayoutElement.minHeight = expandedNarrowColumn ? _defaultRowPreferredHeight : -1f;
        }

        if (transform is RectTransform rowRt)
            rowRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        Transform rowGroup = transform.Find("RowGroup");
        if (rowGroup is RectTransform rowGroupRt)
            rowGroupRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private float ResolveNameTextLayoutWidth()
    {
        if (nameText == null)
            return 0f;

        RectTransform nameRt = nameText.rectTransform;
        if (nameRt != null)
        {
            float layoutWidth = LayoutUtility.GetPreferredWidth(nameRt);
            if (layoutWidth > 1f)
                return layoutWidth;

            float rectWidth = nameRt.rect.width;
            if (rectWidth > 1f)
                return rectWidth;
        }

        if (_nameLayoutElement != null && _nameLayoutElement.preferredWidth > 0f)
            return _nameLayoutElement.preferredWidth;

        return 131f;
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
        RectTransform rowRect = transform as RectTransform;
        RectTransform measure = rowRect != null ? rowRect : (_tooltipBoundsRect ? _tooltipBoundsRect : transform.root as RectTransform);
        // Full row width + PreferredSide.Right → tooltip opens in the empty space to the right of the list entry (not on the icon).
        _tooltip.ShowTextAt(
            measure != null ? measure : transform,
            SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(_def),
            body,
            measureRect: measure,
            heightRect: measure,
            preferredSide: _preferredSide,
            useHudTooltipScale: false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            _tooltip?.Hide();
            _onRightClickRow?.Invoke(_rowLevel);
            return;
        }

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

        // OnDrop on the action bar may run after EndDrag; defer clearing so swaps still see the drag payload.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            StartCoroutine(CoDeferredEndAbilityDrag());
            return;
        }

        AbilityDragState.EndDrag();
    }

    private static IEnumerator CoDeferredEndAbilityDrag()
    {
        yield return null;
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
        string weaponLine = AbilityTooltipDamagePreview.BuildAbilityRequirementsRichText(def, stats, accentWhenOk: true);
        string flavorDesc = BuildAbilityDescription(def);
        string scalingSection = AbilityTooltipDamagePreview.BuildAbilityTooltipScalingSection(
            def, stats, skillsManager, orangeMarkup: false);
        string statsSection = AbilityTooltipDamagePreview.BuildAbilityTooltipStatsSection(
            def, stats, skillsManager, orangeMarkup: false);
        string choiceLine = BuildActiveEnhancementLineForAbility(def, skillsManager);

        return AbilityTooltipDamagePreview.AssembleLeagueStyleAbilityTooltipBody(
            tagLine, weaponLine, flavorDesc, scalingSection, statsSection, choiceLine);
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

        if (string.Equals(def.abilityId, AbilityCombatPower.TripleShotAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
            if (selected < 0)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, AbilityCombatPower.TripleShotEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.SnipeAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
            if (selected < 0)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, AbilityCombatPower.SnipeEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.HawkCompanionAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
            if (selected < 0)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, AbilityCombatPower.HawkCompanionEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.StaticArrowsAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
            if (selected < 0)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Ranged, AbilityCombatPower.StaticArrowsEnhancementParentSpineNodeId, -1);
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

        if (string.Equals(def.abilityId, AbilityCombatPower.GuardiansHammerAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.GuardiansHammerEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, "soulforged_weapon", System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 35, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.FinalSeveranceAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.FinalSeveranceEnhancementParentSpineNodeId, -1);
            if (selected < 0)
                selected = skillsManager.GetSkillChoiceSelection(SkillType.Melee, 45, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.ExecutionersDescentAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.ExecutionersDescentEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.BladestormAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.BladestormEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.ShadowStrikeAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.ShadowStrikeEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.EnergyInfusionAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.EnergyInfusionEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.FlameChargeAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.FlameChargeEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.CrusaderStrikeAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.CrusaderStrikeEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.WarBannerAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.WarBannerEnhancementParentSpineNodeId, -1);
            return BuildActiveEnhancementLine(def, selected);
        }

        if (string.Equals(def.abilityId, AbilityCombatPower.HammerTempestAbilityId, System.StringComparison.OrdinalIgnoreCase))
        {
            int selected = skillsManager.GetSkillChoiceSelection(
                SkillType.Melee, AbilityCombatPower.HammerTempestEnhancementParentSpineNodeId, -1);
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

