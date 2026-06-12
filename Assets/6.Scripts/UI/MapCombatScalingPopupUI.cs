using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>World-map popup for selecting combat map scaling tier (0–7).</summary>
[DisallowMultipleComponent]
public sealed class MapCombatScalingPopupUI : MonoBehaviour
{
    private const int CanvasSortOrder = 10100;
    private const int TooltipSortOrder = CanvasSortOrder + 100;
    private const int UiVersion = 13;
    private const float DetailsScrollbarWidth = 14f;
    private const int SliderStepCount = MapCombatScaling.SliderMax - MapCombatScaling.SliderMin + 1;

    private static MapCombatScalingPopupUI _instance;

    private static readonly Color SliderFillColor = new(0.95f, 0.82f, 0.18f, 1f);
    private static readonly Color SliderTrackColor = new(0.18f, 0.16f, 0.14f, 1f);
    private static readonly Color SliderLockedTrackColor = new(0.12f, 0.11f, 0.1f, 0.85f);
    private static readonly Color TickAvailableColor = new(0.75f, 0.62f, 0.28f, 1f);
    private static readonly Color TickLockedColor = new(0.38f, 0.35f, 0.32f, 1f);
    private static readonly Color TickSelectedColor = new(0.98f, 0.88f, 0.35f, 1f);

    private static readonly Color TickPreviewSelectedColor = new(0.72f, 0.58f, 0.24f, 1f);

    private RectTransform _panelRoot;
    private Slider _slider;
    private Button _confirmButton;
    private RectTransform _customFillBar;
    private RectTransform _lockedTrackOverlay;
    private Image[] _tickMarks;
    private TMP_Text[] _tickLabels;
    private TMP_Text _scaleValueText;
    private TMP_Text _reenterWarningText;
    private TMP_Text _detailsText;
    private ScrollRect _detailsScrollRect;
    private Scrollbar _detailsVerticalScrollbar;
    private readonly Image[] _enhancementSlotIcons = new Image[MapEnhancementService.SlotCount];
    private MapEnhancementScalingSlotUI[] _enhancementSlots;
    private TMP_Text _appliedEffectsText;
    private TMP_Text _enhancementReloadWarningText;
    private SharedTooltipUI _sharedTooltip;
    private MapNodeDefinition _node;
    private WorldMapProgressManager _progress;
    private Action _onClosed;
    private int _maxSelectableSlider;
    private int _currentSliderValue;
    private int _builtUiVersion;
    private int _hoveredEnhancementSlotIndex = -1;
    private Transform _hoveredEnhancementAnchor;

    public static void Show(MapNodeDefinition node, WorldMapProgressManager progress, Action onClosed = null)
    {
        if (node == null || !node.IsMapCombatScalingEnabled())
            return;

        MapCombatScalingPopupUI popup = EnsureInstance();
        popup.Open(node, progress, onClosed);
    }

    public static void RefreshEnhancementSlotsIfOpen()
    {
        if (_instance != null && _instance._panelRoot != null && _instance._panelRoot.gameObject.activeSelf)
            _instance.RefreshEnhancementSlots();
    }

    public static void RefreshEnhancementReloadWarningIfOpen(string nodeId)
    {
        if (_instance == null || _instance._panelRoot == null || !_instance._panelRoot.gameObject.activeSelf)
            return;
        if (_instance._node == null || string.IsNullOrWhiteSpace(nodeId))
            return;
        if (!string.Equals(_instance._node.nodeId, nodeId.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        _instance.RefreshEnhancementReloadWarning();
    }

    /// <summary>Closes the popup without saving (same as Cancel).</summary>
    public static void CancelIfOpen()
    {
        if (_instance == null || _instance._panelRoot == null || !_instance._panelRoot.gameObject.activeSelf)
            return;

        _instance.HideImmediate();
    }

    public static MapCombatScalingPopupUI EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        _instance = FindFirstObjectByType<MapCombatScalingPopupUI>(FindObjectsInactive.Include);
        if (_instance != null)
            return _instance;

        var host = new GameObject(nameof(MapCombatScalingPopupUI), typeof(MapCombatScalingPopupUI));
        DontDestroyOnLoad(host);
        _instance = host.GetComponent<MapCombatScalingPopupUI>();
        _instance.BuildUi();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (_panelRoot == null)
            BuildUi();
        HideImmediate();
        MapEnhancementTooltipHover.RegisterRefresh(RefreshHoveredEnhancementSlotTooltip);
    }

    private void OnDestroy()
    {
        MapEnhancementTooltipHover.UnregisterRefresh(RefreshHoveredEnhancementSlotTooltip);
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (_panelRoot == null || !_panelRoot.gameObject.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            HideImmediate();
    }

    private void Open(MapNodeDefinition node, WorldMapProgressManager progress, Action onClosed)
    {
        EnsureUiBuilt();

        _node = node;
        _progress = progress;
        _onClosed = onClosed;
        _sharedTooltip ??= FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);

        int kills = progress != null ? progress.GetEnemyKillsOnNode(node.nodeId) : 0;
        int unlocked = MapCombatScaling.GetUnlockedLevel(kills);
        _maxSelectableSlider = MapCombatScaling.GetMaxSelectableSliderValue(unlocked, kills);

        int selected = progress != null
            ? progress.GetCombatMapScalingSelectedTier(node.nodeId)
            : MapCombatScaling.SliderMin;
        selected = Mathf.Clamp(selected, MapCombatScaling.SliderMin, _maxSelectableSlider);

        if (_slider != null)
        {
            _slider.minValue = MapCombatScaling.SliderMin;
            _slider.maxValue = MapCombatScaling.SliderMax;
            _slider.wholeNumbers = true;
            _slider.SetValueWithoutNotify(selected);
            _slider.onValueChanged.RemoveListener(HandleSliderChanged);
            _slider.onValueChanged.AddListener(HandleSliderChanged);
        }

        _currentSliderValue = selected;
        RefreshSliderVisuals();
        RefreshDetails(selected);
        StartCoroutine(CoRefreshDetailsScrollNextFrame());
        RefreshLockedTierWarning(selected);
        RefreshConfirmButtonState();
        RefreshEnhancementSlots();
        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(true);

        StartCoroutine(CoCenterOnMainMenuWindow());
    }

    private IEnumerator CoCenterOnMainMenuWindow()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        CenterOnMainMenuWindow();
    }

    private void CenterOnMainMenuWindow()
    {
        if (_panelRoot == null)
            return;

        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu == null || !menu.TryGetMenuWindowRect(out RectTransform menuRect) || !menuRect)
            return;

        UIPinNextToMenuWindow.AlignCenterTo(_panelRoot, menuRect);
    }

    private void HandleSliderChanged(float value)
    {
        int sliderValue = Mathf.Clamp(Mathf.RoundToInt(value), MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        if (_slider != null && Mathf.Abs(_slider.value - sliderValue) > 0.01f)
            _slider.SetValueWithoutNotify(sliderValue);

        _currentSliderValue = sliderValue;
        RefreshSliderVisuals();
        RefreshDetails(sliderValue);
        RefreshLockedTierWarning(sliderValue);
        RefreshConfirmButtonState();
    }

    private void RefreshSliderVisuals()
    {
        if (_customFillBar != null)
        {
            float fillEnd = (float)(_currentSliderValue + 0.5f) / SliderStepCount;
            _customFillBar.anchorMin = new Vector2(0f, 0f);
            _customFillBar.anchorMax = new Vector2(fillEnd, 1f);
            _customFillBar.offsetMin = Vector2.zero;
            _customFillBar.offsetMax = Vector2.zero;
        }

        if (_lockedTrackOverlay != null)
        {
            float lockedStart = (float)(_maxSelectableSlider + 1) / SliderStepCount;
            _lockedTrackOverlay.anchorMin = new Vector2(lockedStart, 0f);
            _lockedTrackOverlay.anchorMax = new Vector2(1f, 1f);
            _lockedTrackOverlay.offsetMin = Vector2.zero;
            _lockedTrackOverlay.offsetMax = Vector2.zero;
            _lockedTrackOverlay.gameObject.SetActive(_maxSelectableSlider < MapCombatScaling.SliderMax);
        }

        if (_tickMarks == null || _tickLabels == null)
            return;

        for (int i = 0; i < _tickMarks.Length; i++)
        {
            bool unlocked = i <= _maxSelectableSlider;
            bool selected = i == _currentSliderValue;
            Color tickColor = selected
                ? unlocked ? TickSelectedColor : TickPreviewSelectedColor
                : unlocked ? TickAvailableColor : TickLockedColor;
            if (_tickMarks[i] != null)
                _tickMarks[i].color = tickColor;

            if (_tickLabels[i] != null)
                _tickLabels[i].color = selected
                    ? unlocked ? TickSelectedColor : TickPreviewSelectedColor
                    : unlocked
                        ? new Color(0.9f, 0.86f, 0.78f, 1f)
                        : new Color(0.45f, 0.42f, 0.38f, 1f);
        }
    }

    private void RefreshConfirmButtonState()
    {
        if (_confirmButton == null)
            return;

        bool canConfirm = _currentSliderValue <= _maxSelectableSlider;
        _confirmButton.interactable = canConfirm;
    }

    private void RefreshDetails(int sliderValue)
    {
        sliderValue = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        bool previewLockedTier = sliderValue > _maxSelectableSlider;
        if (_scaleValueText != null)
        {
            _scaleValueText.text = previewLockedTier
                ? $"Map Scaling: {sliderValue} (locked — preview only)"
                : $"Map Scaling: {sliderValue}";
        }

        if (_detailsText == null || _node == null)
            return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(MapCombatScaling.BuildUnlockTiersText(_node, _progress));
        sb.AppendLine();
        sb.AppendLine(MapCombatScaling.BuildBonusesText(sliderValue));
        sb.AppendLine();
        sb.AppendLine(MapCombatScaling.BuildSpecialLootText(_node, sliderValue));

        _detailsText.text = sb.ToString().TrimEnd();
        RefreshDetailsScrollLayout();
    }

    private void RefreshDetailsScrollLayout()
    {
        if (_detailsScrollRect == null || _detailsText == null)
            return;

        Canvas.ForceUpdateCanvases();
        RectTransform content = _detailsScrollRect.content;
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        _detailsScrollRect.verticalNormalizedPosition = 1f;
        UpdateDetailsScrollbarVisibility();
    }

    private void UpdateDetailsScrollbarVisibility()
    {
        if (_detailsScrollRect == null)
            return;

        RectTransform viewport = _detailsScrollRect.viewport;
        RectTransform content = _detailsScrollRect.content;
        if (!viewport || !content)
            return;

        bool scrollable = content.rect.height > viewport.rect.height + 1f;
        if (_detailsVerticalScrollbar)
            _detailsVerticalScrollbar.gameObject.SetActive(scrollable);
    }

    private IEnumerator CoRefreshDetailsScrollNextFrame()
    {
        yield return null;
        RefreshDetailsScrollLayout();
    }

    private void Confirm()
    {
        if (_currentSliderValue > _maxSelectableSlider)
            return;

        if (_node != null && _progress != null && _slider != null)
        {
            int value = Mathf.Clamp(Mathf.RoundToInt(_slider.value), MapCombatScaling.SliderMin, _maxSelectableSlider);
            _progress.SetCombatMapScalingSelectedTier(_node.nodeId, value);
        }

        Action cb = _onClosed;
        HideImmediate();
        cb?.Invoke();
    }

    private void RefreshLockedTierWarning(int sliderValue)
    {
        if (_reenterWarningText == null)
            return;

        bool showLocked = sliderValue > _maxSelectableSlider;
        _reenterWarningText.gameObject.SetActive(showLocked);
        if (showLocked)
            _reenterWarningText.text = "SCALE LEVEL NOT YET UNLOCKED";
    }

    private void HideImmediate()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveListener(HandleSliderChanged);

        _sharedTooltip?.Hide();
        _node = null;
        _progress = null;
        _onClosed = null;
        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(false);
    }

    private void EnsureUiBuilt()
    {
        if (_panelRoot != null && _tickMarks != null && _customFillBar != null && _builtUiVersion == UiVersion)
            return;

        if (transform.childCount > 0)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        _panelRoot = null;
        _slider = null;
        _customFillBar = null;
        _lockedTrackOverlay = null;
        _tickMarks = null;
        _tickLabels = null;
        _scaleValueText = null;
        _reenterWarningText = null;
        _detailsText = null;
        _detailsScrollRect = null;
        _detailsVerticalScrollbar = null;
        _confirmButton = null;
        _appliedEffectsText = null;
        _enhancementSlots = null;
        _enhancementReloadWarningText = null;
        _builtUiVersion = 0;
        BuildUi();
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("MapCombatScalingCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasSortOrder;

        RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
        Stretch(canvasRt, 0f, 0f, 1f, 1f);

        _panelRoot = CreateChild(canvasRt, "Panel");
        Stretch(_panelRoot, 0.3f, 0.22f, 0.7f, 0.78f);

        Image panelBg = _panelRoot.gameObject.AddComponent<Image>();
        panelBg.color = new Color(0.24f, 0.2f, 0.16f, 0.98f);

        Outline outline = _panelRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.6f, 0.32f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        VerticalLayoutGroup vlg = _panelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateLabel(_panelRoot, "Map scaling", 16, FontStyles.Bold);
        _scaleValueText = CreateLabel(_panelRoot, "Map Scaling: 0", 14, FontStyles.Normal);

        RectTransform sliderColumn = CreateChild(_panelRoot, "SliderColumn");
        LayoutElement sliderColumnLe = sliderColumn.gameObject.AddComponent<LayoutElement>();
        sliderColumnLe.minHeight = 58f;

        VerticalLayoutGroup sliderColumnVlg = sliderColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        sliderColumnVlg.spacing = 6;
        sliderColumnVlg.childAlignment = TextAnchor.UpperCenter;
        sliderColumnVlg.childControlWidth = true;
        sliderColumnVlg.childControlHeight = true;
        sliderColumnVlg.childForceExpandWidth = true;
        sliderColumnVlg.childForceExpandHeight = false;

        RectTransform sliderRt = CreateChild(sliderColumn, "Slider");
        LayoutElement sliderLe = sliderRt.gameObject.AddComponent<LayoutElement>();
        sliderLe.minHeight = 24f;

        Image sliderBg = sliderRt.gameObject.AddComponent<Image>();
        sliderBg.color = SliderTrackColor;

        _customFillBar = CreateChild(sliderRt, "CustomFill");
        _customFillBar.anchorMin = new Vector2(0f, 0f);
        _customFillBar.anchorMax = new Vector2(0f, 1f);
        _customFillBar.pivot = new Vector2(0f, 0.5f);
        _customFillBar.offsetMin = Vector2.zero;
        _customFillBar.offsetMax = Vector2.zero;
        Image customFillImg = _customFillBar.gameObject.AddComponent<Image>();
        customFillImg.color = SliderFillColor;
        customFillImg.raycastTarget = false;

        _lockedTrackOverlay = CreateChild(sliderRt, "LockedOverlay");
        Stretch(_lockedTrackOverlay, 0f, 0f, 1f, 1f);
        Image lockedOverlayImg = _lockedTrackOverlay.gameObject.AddComponent<Image>();
        lockedOverlayImg.color = SliderLockedTrackColor;
        lockedOverlayImg.raycastTarget = false;

        RectTransform handleArea = CreateChild(sliderRt, "Handle Slide Area");
        ApplySliderStepInsets(handleArea);
        RectTransform handle = CreateChild(handleArea, "Handle");
        handle.sizeDelta = new Vector2(14f, 24f);
        Image handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.98f, 0.9f, 0.45f, 1f);

        _slider = sliderRt.gameObject.AddComponent<Slider>();
        _slider.direction = Slider.Direction.LeftToRight;
        _slider.fillRect = null;
        _slider.handleRect = handle;
        _slider.targetGraphic = handleImg;

        BuildSliderTicks(sliderColumn);

        _reenterWarningText = CreateLabel(_panelRoot, string.Empty, 12, FontStyles.Bold);
        _reenterWarningText.color = new Color(1f, 0.15f, 0.15f, 1f);
        _reenterWarningText.alignment = TextAlignmentOptions.Center;
        _reenterWarningText.gameObject.SetActive(false);

        _detailsText = CreateSummaryArea(_panelRoot);
        BuildMapEnhancementsSection(_panelRoot);

        RectTransform buttonRow = CreateChild(_panelRoot, "Buttons");
        HorizontalLayoutGroup row = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        CreateButton(buttonRow, "Cancel", HideImmediate);
        _confirmButton = CreateButton(buttonRow, "OK", Confirm);
        _builtUiVersion = UiVersion;
    }

    private void BuildMapEnhancementsSection(RectTransform parent)
    {
        const float slotSize = 64f;

        RectTransform section = CreateChild(parent, "MapEnhancementsSection");
        LayoutElement sectionLe = section.gameObject.AddComponent<LayoutElement>();
        sectionLe.minHeight = 152f;

        VerticalLayoutGroup sectionVlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        sectionVlg.spacing = 8;
        sectionVlg.childAlignment = TextAnchor.UpperLeft;
        sectionVlg.childControlWidth = true;
        sectionVlg.childControlHeight = true;
        sectionVlg.childForceExpandWidth = true;
        sectionVlg.childForceExpandHeight = false;

        CreateLabel(section, "Map Enhancements", 13, FontStyles.Bold);

        RectTransform contentRow = CreateChild(section, "EnhancementContentRow");
        LayoutElement contentLe = contentRow.gameObject.AddComponent<LayoutElement>();
        contentLe.minHeight = slotSize + 24f;

        HorizontalLayoutGroup contentHlg = contentRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        contentHlg.spacing = 16;
        contentHlg.childAlignment = TextAnchor.UpperLeft;
        contentHlg.childControlWidth = true;
        contentHlg.childControlHeight = true;
        contentHlg.childForceExpandWidth = false;
        contentHlg.childForceExpandHeight = false;

        RectTransform slotsColumn = CreateChild(contentRow, "EnhancementSlotsColumn");
        LayoutElement slotsColumnLe = slotsColumn.gameObject.AddComponent<LayoutElement>();
        slotsColumnLe.minWidth = slotSize * MapEnhancementService.SlotCount + 16f;

        VerticalLayoutGroup slotsColumnVlg = slotsColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        slotsColumnVlg.spacing = 6;
        slotsColumnVlg.childAlignment = TextAnchor.UpperLeft;
        slotsColumnVlg.childControlWidth = true;
        slotsColumnVlg.childControlHeight = true;
        slotsColumnVlg.childForceExpandWidth = true;
        slotsColumnVlg.childForceExpandHeight = false;

        RectTransform slotsRow = CreateChild(slotsColumn, "EnhancementSlots");
        LayoutElement slotsLe = slotsRow.gameObject.AddComponent<LayoutElement>();
        slotsLe.minHeight = slotSize;

        HorizontalLayoutGroup slotsHlg = slotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        slotsHlg.spacing = 10;
        slotsHlg.childAlignment = TextAnchor.MiddleLeft;
        slotsHlg.childControlWidth = true;
        slotsHlg.childControlHeight = true;
        slotsHlg.childForceExpandWidth = false;
        slotsHlg.childForceExpandHeight = false;

        _enhancementSlots = new MapEnhancementScalingSlotUI[MapEnhancementService.SlotCount];
        for (int i = 0; i < MapEnhancementService.SlotCount; i++)
            _enhancementSlots[i] = BuildEnhancementSlot(slotsRow, i, slotSize);

        RectTransform helpRt = CreateChild(slotsColumn, "EnhancementHelp");
        TMP_Text helpText = helpRt.gameObject.AddComponent<TextMeshProUGUI>();
        helpText.text = "Equip map enhancements from inventory. Right-click a slot to remove.";
        helpText.fontSize = 11f;
        helpText.fontStyle = FontStyles.Italic;
        helpText.color = new Color(0.82f, 0.78f, 0.72f, 1f);
        helpText.alignment = TextAlignmentOptions.TopLeft;
        helpText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform reloadWarningRt = CreateChild(slotsColumn, "EnhancementReloadWarning");
        _enhancementReloadWarningText = reloadWarningRt.gameObject.AddComponent<TextMeshProUGUI>();
        _enhancementReloadWarningText.text = string.Empty;
        _enhancementReloadWarningText.fontSize = 11f;
        _enhancementReloadWarningText.fontStyle = FontStyles.Bold;
        _enhancementReloadWarningText.color = new Color(1f, 0.15f, 0.15f, 1f);
        _enhancementReloadWarningText.alignment = TextAlignmentOptions.TopLeft;
        _enhancementReloadWarningText.textWrappingMode = TextWrappingModes.Normal;
        _enhancementReloadWarningText.gameObject.SetActive(false);

        RectTransform appliedColumn = CreateChild(contentRow, "AppliedEffectsColumn");
        LayoutElement appliedColumnLe = appliedColumn.gameObject.AddComponent<LayoutElement>();
        appliedColumnLe.flexibleWidth = 1f;
        appliedColumnLe.minWidth = 180f;
        appliedColumnLe.minHeight = slotSize + 24f;

        VerticalLayoutGroup appliedVlg = appliedColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        appliedVlg.spacing = 4;
        appliedVlg.childAlignment = TextAnchor.UpperLeft;
        appliedVlg.childControlWidth = true;
        appliedVlg.childControlHeight = true;
        appliedVlg.childForceExpandWidth = true;
        appliedVlg.childForceExpandHeight = false;

        CreateLabel(appliedColumn, "Applied Effects", 12, FontStyles.Bold);

        RectTransform appliedTextRt = CreateChild(appliedColumn, "AppliedEffectsText");
        _appliedEffectsText = appliedTextRt.gameObject.AddComponent<TextMeshProUGUI>();
        _appliedEffectsText.text = "None";
        _appliedEffectsText.fontSize = 11f;
        _appliedEffectsText.color = new Color(0.88f, 0.84f, 0.78f, 1f);
        _appliedEffectsText.alignment = TextAlignmentOptions.TopLeft;
        _appliedEffectsText.textWrappingMode = TextWrappingModes.Normal;
        _appliedEffectsText.lineSpacing = 2f;
    }

    private MapEnhancementScalingSlotUI BuildEnhancementSlot(RectTransform parent, int slotIndex, float slotSize)
    {
        RectTransform slotRt = CreateChild(parent, $"EnhancementSlot{slotIndex}");
        LayoutElement le = slotRt.gameObject.AddComponent<LayoutElement>();
        le.minWidth = slotSize;
        le.minHeight = slotSize;
        le.preferredWidth = slotSize;
        le.preferredHeight = slotSize;

        Image bg = slotRt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.14f, 0.12f, 0.1f, 1f);

        RectTransform iconRt = CreateChild(slotRt, "Icon");
        Stretch(iconRt, 0.12f, 0.12f, 0.88f, 0.88f);
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        _enhancementSlotIcons[slotIndex] = icon;

        var slotUi = slotRt.gameObject.AddComponent<MapEnhancementScalingSlotUI>();
        slotUi.Initialize(this, slotIndex);
        return slotUi;
    }

    private void RefreshEnhancementSlots()
    {
        if (_node == null)
            return;

        Inventory inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        IReadOnlyList<string> slots = _progress != null
            ? _progress.GetMapEnhancementSlots(_node.nodeId)
            : Array.Empty<string>();

        for (int i = 0; i < MapEnhancementService.SlotCount; i++)
        {
            string itemId = i < slots.Count ? slots[i] : null;
            ItemDefinition def = null;
            if (!string.IsNullOrWhiteSpace(itemId) && inventory != null)
                def = inventory.GetItemDef(itemId);

            if (_enhancementSlotIcons[i] == null)
                continue;

            bool has = def != null && def.icon != null;
            _enhancementSlotIcons[i].sprite = has ? def.icon : null;
            _enhancementSlotIcons[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            _enhancementSlotIcons[i].enabled = true;
        }

        if (_appliedEffectsText != null)
            _appliedEffectsText.text = MapEnhancementService.BuildAggregateEffectsText(_node.nodeId);

        RefreshEnhancementReloadWarning();
    }

    private void RefreshEnhancementReloadWarning()
    {
        if (_enhancementReloadWarningText == null || _node == null)
            return;

        bool show = MapEnhancementSessionState.ShouldShowReenterWarningForNode(_node.nodeId);
        _enhancementReloadWarningText.gameObject.SetActive(show);
        if (show)
            _enhancementReloadWarningText.text = MapEnhancementSessionState.ReenterWarningMessage;
    }

    internal void ShowEnhancementSlotTooltip(int slotIndex, Transform anchor)
    {
        if (_node == null || anchor == null)
            return;

        IReadOnlyList<string> slots = _progress != null
            ? _progress.GetMapEnhancementSlots(_node.nodeId)
            : Array.Empty<string>();
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        string itemId = slots[slotIndex];
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        _hoveredEnhancementSlotIndex = slotIndex;
        _hoveredEnhancementAnchor = anchor;

        string body = MapEnhancementService.BuildSlotTooltipBody(itemId, ItemTooltipAdvancedInput.IsHeld);
        if (string.IsNullOrWhiteSpace(body))
            return;

        _sharedTooltip ??= FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
        if (_sharedTooltip == null)
            return;

        var anchorRect = anchor as RectTransform;
        if (anchorRect != null && _panelRoot != null)
        {
            _sharedTooltip.ConfigureDocking(
                anchorRect,
                _panelRoot,
                FlipInsideBounds.PreferredSide.Right,
                _panelRoot);
        }

        _sharedTooltip.PushOverlaySortOrder(TooltipSortOrder);
        _sharedTooltip.ShowTextAt(
            anchor,
            string.Empty,
            body,
            measureRect: anchorRect,
            heightRect: _panelRoot,
            preferredSide: FlipInsideBounds.PreferredSide.Right,
            useHudTooltipScale: false);
    }

    internal void HideEnhancementSlotTooltip()
    {
        _hoveredEnhancementSlotIndex = -1;
        _hoveredEnhancementAnchor = null;
        _sharedTooltip?.Hide();
    }

    private void RefreshHoveredEnhancementSlotTooltip()
    {
        if (_hoveredEnhancementSlotIndex < 0 || _hoveredEnhancementAnchor == null)
            return;

        ShowEnhancementSlotTooltip(_hoveredEnhancementSlotIndex, _hoveredEnhancementAnchor);
    }

    internal void TryRemoveEnhancementSlot(int slotIndex)
    {
        if (_node == null)
            return;

        Inventory inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inventory == null)
            return;

        if (!MapEnhancementService.TryRemoveToInventory(_node.nodeId, slotIndex, inventory))
            return;

        RefreshEnhancementSlots();
        RefreshEnhancementReloadWarning();
    }

    private static void ApplySliderStepInsets(RectTransform rt)
    {
        float inset = 0.5f / SliderStepCount;
        rt.anchorMin = new Vector2(inset, 0f);
        rt.anchorMax = new Vector2(1f - inset, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static TMP_Text CreateLabel(RectTransform parent, string text, int size, FontStyles style)
    {
        RectTransform rt = CreateChild(parent, "Label");
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = size + 8f;
        TMP_Text label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = new Color(0.92f, 0.88f, 0.8f, 1f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private TMP_Text CreateSummaryArea(RectTransform parent)
    {
        RectTransform scrollRoot = CreateChild(parent, "DetailsScroll");
        LayoutElement scrollLe = scrollRoot.gameObject.AddComponent<LayoutElement>();
        scrollLe.minHeight = 180f;
        scrollLe.flexibleHeight = 1f;
        scrollLe.preferredHeight = -1f;

        Image scrollBg = scrollRoot.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.16f, 0.14f, 0.12f, 0.98f);
        scrollBg.raycastTarget = true;

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.inertia = true;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        _detailsScrollRect = scroll;

        RectTransform viewport = CreateChild(scrollRoot, "Viewport");
        Stretch(viewport, 0f, 0f, 1f, 1f);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        _detailsVerticalScrollbar = CreateDetailsVerticalScrollbar(scrollRoot);
        scroll.verticalScrollbar = _detailsVerticalScrollbar;

        RectTransform content = CreateChild(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(12, 12, 10, 10);
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        scroll.content = content;

        var textGo = new GameObject("DetailsText", typeof(RectTransform));
        textGo.transform.SetParent(content, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(0f, 0f);

        LayoutElement textLayout = textGo.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.minHeight = 0f;
        textLayout.preferredHeight = -1f;

        TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 12f;
        text.color = new Color(0.88f, 0.84f, 0.76f, 1f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.lineSpacing = 4f;
        text.paragraphSpacing = 6f;
        text.raycastTarget = false;

        ContentSizeFitter textFitter = textGo.AddComponent<ContentSizeFitter>();
        textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return text;
    }

    private static Scrollbar CreateDetailsVerticalScrollbar(RectTransform scrollRoot)
    {
        RectTransform scrollbarRt = CreateChild(scrollRoot, "ScrollbarVertical");
        scrollbarRt.anchorMin = new Vector2(1f, 0f);
        scrollbarRt.anchorMax = new Vector2(1f, 1f);
        scrollbarRt.pivot = new Vector2(1f, 0.5f);
        scrollbarRt.anchoredPosition = Vector2.zero;
        scrollbarRt.sizeDelta = new Vector2(DetailsScrollbarWidth, 0f);

        Image trackImage = scrollbarRt.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.1f, 0.09f, 0.08f, 0.95f);
        trackImage.raycastTarget = true;

        Scrollbar scrollbar = scrollbarRt.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.ColorTint;

        RectTransform slidingArea = CreateChild(scrollbarRt, "SlidingArea");
        Stretch(slidingArea, 0.08f, 0.02f, 0.92f, 0.98f);

        RectTransform handle = CreateChild(slidingArea, "Handle");
        Stretch(handle, 0f, 0f, 1f, 1f);

        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.72f, 0.62f, 0.38f, 0.95f);
        handleImage.raycastTarget = true;

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;

        return scrollbar;
    }

    private void BuildSliderTicks(RectTransform parent)
    {
        RectTransform tickRow = CreateChild(parent, "TickRow");
        LayoutElement tickRowLe = tickRow.gameObject.AddComponent<LayoutElement>();
        tickRowLe.minHeight = 22f;

        HorizontalLayoutGroup tickHlg = tickRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tickHlg.spacing = 0;
        tickHlg.childAlignment = TextAnchor.MiddleCenter;
        tickHlg.childControlWidth = true;
        tickHlg.childControlHeight = true;
        tickHlg.childForceExpandWidth = true;
        tickHlg.childForceExpandHeight = false;

        int tickCount = MapCombatScaling.SliderMax - MapCombatScaling.SliderMin + 1;
        _tickMarks = new Image[tickCount];
        _tickLabels = new TMP_Text[tickCount];

        for (int i = MapCombatScaling.SliderMin; i <= MapCombatScaling.SliderMax; i++)
        {
            RectTransform tickCell = CreateChild(tickRow, $"Tick{i}");
            LayoutElement cellLe = tickCell.gameObject.AddComponent<LayoutElement>();
            cellLe.flexibleWidth = 1f;
            cellLe.minHeight = 22f;

            VerticalLayoutGroup cellVlg = tickCell.gameObject.AddComponent<VerticalLayoutGroup>();
            cellVlg.spacing = 2;
            cellVlg.childAlignment = TextAnchor.MiddleCenter;
            cellVlg.childControlWidth = true;
            cellVlg.childControlHeight = true;
            cellVlg.childForceExpandWidth = true;
            cellVlg.childForceExpandHeight = false;

            RectTransform tickMarkRt = CreateChild(tickCell, "Mark");
            LayoutElement markLe = tickMarkRt.gameObject.AddComponent<LayoutElement>();
            markLe.minWidth = 4f;
            markLe.minHeight = 8f;
            Image tickImg = tickMarkRt.gameObject.AddComponent<Image>();
            tickImg.color = TickAvailableColor;

            RectTransform labelRt = CreateChild(tickCell, "Label");
            LayoutElement labelLe = labelRt.gameObject.AddComponent<LayoutElement>();
            labelLe.minHeight = 12f;
            TMP_Text label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = i.ToString();
            label.fontSize = 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.9f, 0.86f, 0.78f, 1f);

            int index = i - MapCombatScaling.SliderMin;
            _tickMarks[index] = tickImg;
            _tickLabels[index] = label;
        }
    }

    private static Button CreateButton(RectTransform parent, string label, Action onClick)
    {
        RectTransform rt = CreateChild(parent, label + "Button");
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 32f;
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.34f, 0.28f, 0.2f, 1f);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        RectTransform textRt = CreateChild(rt, "Text");
        Stretch(textRt, 0f, 0f, 1f, 1f);
        TMP_Text text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 14f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.92f, 0.88f, 0.8f, 1f);

        button.onClick.AddListener(() => onClick?.Invoke());
        return button;
    }
}

[DisallowMultipleComponent]
internal sealed class MapEnhancementScalingSlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private MapCombatScalingPopupUI _owner;
    private int _slotIndex;

    public void Initialize(MapCombatScalingPopupUI owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        _owner?.ShowEnhancementSlotTooltip(_slotIndex, transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideEnhancementSlotTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;

        eventData.Use();
        _owner?.TryRemoveEnhancementSlot(_slotIndex);
    }
}
