using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Upgrade menu page: visual upgradable-gear inventory, selected item slot, and enhancement scroll options list.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(40)]
public sealed class UpgradePageUI : MonoBehaviour
{
    private const string UpgradeListEntryPrefabPath = "Assets/2.Prefabs/UI/UpgradeListEntryUI.prefab";
    private const string EnhancementOptionDatabasePath = "Assets/Resources/Databases/EnhancementOptionDatabase.asset";
    private const string TickSpritePath = "Assets/5.Art/Sprites/UI/icons8-tick-48.png";
    private const string CrossSpritePath = "Assets/5.Art/Sprites/UI/icons8-cross-48.png";

    [Header("Inventory")]
    [SerializeField] private UpgradeInventoryGridUI inventoryGrid;

    [Header("Selected item")]
    [SerializeField] private Image upgradeItemSlotImage;
    [SerializeField] private TMP_Text itemLabelText;

    [Header("Enhancement apply")]
    [SerializeField] private TMP_Text enhancementSelectedText;
    [SerializeField] private Button applyButton;
    [SerializeField] private TMP_Text errorLabelText;
    [SerializeField] private TMP_Text slotsAvailableText;

    [Header("Selected option details")]
    [SerializeField] private GameObject optionDetailBar;
    [SerializeField] private Button collapseDetailBarButton;
    [SerializeField] private TMP_Text optionNameText;
    [SerializeField] private TMP_Text optionCostText;
    [SerializeField] private TMP_Text optionValueText;
    [SerializeField] private TMP_Text optionAvailableItemsText;
    [SerializeField] private TMP_Text optionChanceText;
    [SerializeField] private TMP_Text optionAdditionalText;

    [Header("Enhancement options list")]
    [SerializeField] private RectTransform upgradeListContent;
    [SerializeField] private ScrollRect upgradeListScrollRect;
    [SerializeField] private UpgradeListEntryUI upgradeListEntryPrefab;
    [SerializeField] private EnhancementOptionDatabase enhancementOptionDatabase;
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Payment availability icons")]
    [SerializeField] private Sprite hasScrollSprite;
    [SerializeField] private Sprite missingScrollSprite;

    [Header("Option filters")]
    [SerializeField] private Button basicFilterButton;
    [SerializeField] private Button intermediateFilterButton;
    [SerializeField] private Button advancedFilterButton;
    [SerializeField] private Button chaosFilterButton;
    [SerializeField] private Button specialFilterButton;
    [SerializeField] private TMP_InputField enhanceSearchField;

    private const float ListTitleHeight = 35f;
    private const float FilterRowHeight = 30f;
    private const float SearchRowHeight = 30f;
    private const float ListSectionGap = 8f;
    private const float BottomBarHeight = 400f;
    private static readonly Color FilterButtonTextColor = new(0.68235294f, 0.63529412f, 0.58039216f, 1f);
    private static readonly Color TierTooLowTextColor = new(1f, 0.36078432f, 0.36078432f, 1f);

    private readonly List<UpgradeListEntryUI> _optionRows = new(64);
    private readonly List<UpgradeListSectionHeaderUI> _sectionRows = new(8);
    private readonly List<UpgradeListHeaderUI> _columnHeaders = new(4);
    private readonly List<UpgradeDisplayRow> _displayRows = new(64);

    private TMP_Text _tierTooLowLabel;

    private Inventory _inventory;
    private EquipmentManager _equipment;
    private ToolbeltManager _toolbelt;
    private int _selectedGearSlotIndex = -1;
    private bool _selectedFromEquipment;
    private EquipmentUISlotType _selectedEquipmentSlot = EquipmentUISlotType.None;
    private string _selectedGearItemId;
    private EnhancementOptionEntry _selectedOption;
    private UpgradeOptionFilter _activeFilter = UpgradeOptionFilter.None;
    private string _searchQuery = string.Empty;
    private bool _initialized;

    private enum UpgradeOptionFilter
    {
        None = 0,
        Basic = 1,
        Intermediate = 2,
        Advanced = 3,
        Chaos = 4,
        Special = 5,
    }

    private sealed class UpgradeDisplayRow
    {
        public bool IsSection;
        public string SectionTitle;
        public EnhancementOptionEntry Option;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        EnsureOptionDetailBarResolved();
        ConfigureDetailBarInteraction();
        WireCollapseButton();
        ResolveSearchField();
        WireSearchField();
        HideLegacyListHeader();
        ConfigureListLayout();
        ConfigureUpgradeSectionListLayout();
        ResolveFilterButtons();
        WireFilterButtons();
        RefreshFilterButtonVisuals();
        ClearApplyError();
        TrySubscribeInventory();
        TrySubscribeEquipment();
        TrySubscribeToolbelt();
        DisableLegacyInventoryGrids();
        RefreshAll();
    }

    private void OnDisable()
    {
        HideSelectedGearTooltip();
        UnwireFilterButtons();
        UnwireSearchField();
        UnwireCollapseButton();
        UnsubscribeInventory();
        UnsubscribeEquipment();
        UnsubscribeToolbelt();
        ClearSelectedOption();
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        ResolveEditorAssetRefs();
        ResolveSpriteRefs();
        ResolveSceneRefs();
        DisableLegacyInventoryGrids();
        EnsureUpgradeInventoryGrid();
        EnsureUpgradeItemSlotHandler();
        FixScrollRectContent();
        ConfigureListLayout();
        HideLegacyListHeader();
        ConfigureUpgradeSectionListLayout();
        ResolveFilterButtons();
        WireFilterButtons();
        WireSearchField();
        WireApplyButton();
        WireCollapseButton();
        BuildDisplayRows();
        _initialized = true;
    }

    private Inventory _subscribedInventory;

    private void TrySubscribeInventory()
    {
        Inventory inv = ResolveInventory();
        if (inv == _subscribedInventory)
            return;

        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged -= OnInventoryChanged;

        _subscribedInventory = inv;
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged += OnInventoryChanged;

        if (itemDatabase == null && inv != null)
            itemDatabase = inv.GetItemDatabase();

        BuildDisplayRows();
    }

    private void UnsubscribeInventory()
    {
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryChanged -= OnInventoryChanged;
        _subscribedInventory = null;
    }

    private void OnInventoryChanged()
    {
        RefreshSelectedGearSlot();
        RefreshInventoryGrid();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
    }

    public void SelectGearFromSlot(int inventorySlotIndex)
    {
        Inventory inv = ResolveInventory();
        if (inv == null || inventorySlotIndex < 0 || inventorySlotIndex >= inv.SlotCount)
            return;

        Inventory.Slot slot = inv.GetSlot(inventorySlotIndex);
        if (slot.IsEmpty)
            return;

        ItemDefinition def = inv.GetItemDef(slot.itemId);
        if (def == null && itemDatabase != null)
            def = itemDatabase.Get(slot.itemId);

        if (def == null || !IsUpgradableGear(def))
            return;

        _selectedFromEquipment = false;
        _selectedEquipmentSlot = EquipmentUISlotType.None;
        _selectedGearSlotIndex = inventorySlotIndex;
        _selectedGearItemId = slot.itemId;
        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        RefreshInventoryGrid();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
        ClearApplyError();
    }

    public void SelectGearFromEquipmentSlot(EquipmentUISlotType equipmentSlot)
    {
        if (equipmentSlot == EquipmentUISlotType.None)
            return;

        Inventory inv = ResolveInventory();
        EquipmentManager eq = ResolveEquipment();
        ToolbeltManager belt = ResolveToolbelt();
        if (inv == null)
            return;

        string itemId = EquipmentSlotUI.GetItemIdForSlot(equipmentSlot, eq, belt);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        ItemDefinition def = inv.GetItemDef(itemId);
        if (def == null && itemDatabase != null)
            def = itemDatabase.Get(itemId);

        if (def == null || !IsUpgradableGear(def) || def.IsCombatSupport)
            return;

        _selectedFromEquipment = true;
        _selectedEquipmentSlot = equipmentSlot;
        _selectedGearSlotIndex = -1;
        _selectedGearItemId = itemId;
        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        RefreshInventoryGrid();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
        ClearApplyError();
    }

    public void ClearSelectedGear()
    {
        _selectedFromEquipment = false;
        _selectedEquipmentSlot = EquipmentUISlotType.None;
        _selectedGearSlotIndex = -1;
        _selectedGearItemId = null;
        ValidateSelectedOptionForGear(null);
        RefreshSelectedGearSlotImage(null);
        RefreshItemLabel(null);
        SyncInventoryGridSelection();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
        ClearApplyError();
    }

    public void ApplyEnhancement()
    {
        string error = TryGetApplyErrorMessage();
        if (!string.IsNullOrWhiteSpace(error))
        {
            ShowApplyError(error);
            return;
        }

        ClearApplyError();

        Inventory inv = ResolveInventory();
        ItemDefinition gear = GetSelectedGearDefinition();
        EnhancementOptionPayment payment = EnhancementOptionPayment.Resolve(inv, _selectedOption, gear);

        bool success = false;
        bool attempted;
        if (_selectedFromEquipment)
        {
            attempted = EnhancementUpgradeService.TryApplyOptionOnEquippedItem(
                inv,
                ResolveEquipment(),
                ResolveToolbelt(),
                _selectedEquipmentSlot,
                _selectedOption,
                payment,
                out success);
        }
        else
        {
            attempted = EnhancementUpgradeService.TryApplyOptionOnInventorySlot(
                inv,
                _selectedGearSlotIndex,
                _selectedOption,
                payment,
                out success);
        }

        if (attempted)
        {
            EnhancementFlashUI.Flash(success);
            if (success)
            {
                ClearApplyError();
                SyncSelectedGearItemIdFromSlot();
            }

            RefreshSelectedGearSlot();
            RefreshEnhancementOptionsList();
            RefreshEnhancementSelectedLabel();
            RefreshOptionDetailPanel();
            RefreshSlotsAvailableLabel();
        }
        else
            ShowApplyError("Unable to apply this upgrade.");
    }

    private void ResolveSceneRefs()
    {
        if (!inventoryGrid)
            inventoryGrid = GetComponentInChildren<UpgradeInventoryGridUI>(true);

        if (!upgradeItemSlotImage)
        {
            Transform slot = FindUnderGearSection("UpgradeItemSlot");
            if (slot)
                upgradeItemSlotImage = slot.GetComponent<Image>();
        }

        if (!itemLabelText)
        {
            Transform itemLabel = FindUnderGearSection("ItemLabel");
            if (itemLabel)
            {
                Transform text = FindDeepChild(itemLabel, "ItemLabelText");
                if (text)
                    itemLabelText = text.GetComponent<TMP_Text>();
            }
        }

        if (!enhancementSelectedText)
        {
            Transform enhancementSelected = FindUnderGearSection("EnhancementSelected");
            if (!enhancementSelected)
                enhancementSelected = FindUnderGearSection("EnhacementSelected");
            if (enhancementSelected)
            {
                Transform text = FindDeepChild(enhancementSelected, "SelectedText");
                if (text)
                    enhancementSelectedText = text.GetComponent<TMP_Text>();
            }
        }

        if (!applyButton)
        {
            Transform apply = FindUnderGearSection("ApplyButton");
            if (apply)
                applyButton = apply.GetComponent<Button>();
        }

        if (!errorLabelText)
        {
            Transform errorLabel = FindUnderGearSection("ErrorLabel");
            if (errorLabel)
                errorLabelText = errorLabel.GetComponent<TMP_Text>();
        }

        if (!slotsAvailableText)
        {
            Transform slotsAvailable = FindUnderOptionsSection("SlotsAvailableText");
            if (!slotsAvailable)
                slotsAvailable = FindUnderGearSection("SlotsAvailableText");
            if (slotsAvailable)
                slotsAvailableText = slotsAvailable.GetComponent<TMP_Text>();
        }

        Transform bottomBar = FindUnderOptionsSection("BottomBar");
        if (bottomBar)
        {
            if (!optionDetailBar)
                optionDetailBar = bottomBar.gameObject;

            if (!collapseDetailBarButton)
                collapseDetailBarButton = FindDeepChild(bottomBar, "CollapseButton")?.GetComponent<Button>();

            if (!optionNameText)
                optionNameText = FindDeepChild(bottomBar, "NameText")?.GetComponent<TMP_Text>();
            if (!optionCostText)
            {
                optionCostText = FindDeepChild(bottomBar, "EnhanceCostText")?.GetComponent<TMP_Text>();
                if (!optionCostText)
                    optionCostText = FindDeepChild(bottomBar, "CostText")?.GetComponent<TMP_Text>();
            }
            if (!optionValueText)
                optionValueText = FindDeepChild(bottomBar, "ValueText")?.GetComponent<TMP_Text>();
            if (!optionAvailableItemsText)
            {
                optionAvailableItemsText = FindDeepChild(bottomBar, "ItemTypeText")?.GetComponent<TMP_Text>();
                if (!optionAvailableItemsText)
                    optionAvailableItemsText = FindDeepChild(bottomBar, "AvailableItems")?.GetComponent<TMP_Text>();
            }
            if (!optionChanceText)
                optionChanceText = FindDeepChild(bottomBar, "ChanceText")?.GetComponent<TMP_Text>();
            if (!optionAdditionalText)
                optionAdditionalText = FindDeepChild(bottomBar, "AdditionalText")?.GetComponent<TMP_Text>();
        }

        EnsureOptionDetailBarResolved();

        if (!upgradeListContent)
        {
            Transform content = FindUnderOptionsSection("Content");
            if (!content && upgradeListScrollRect != null)
                content = FindDeepChild(upgradeListScrollRect.transform, "Content");
            if (content)
                upgradeListContent = content as RectTransform;
        }

        if (!upgradeListScrollRect)
        {
            Transform scrollView = FindUnderOptionsSection("Scroll View");
            if (scrollView)
                upgradeListScrollRect = scrollView.GetComponent<ScrollRect>();
        }

        if (!itemDatabase)
        {
            Inventory inv = ResolveInventory();
            if (inv != null)
                itemDatabase = inv.GetItemDatabase();
        }

        ResolveFilterButtons();
        ResolveSearchField();
        ConfigureDetailBarInteraction();
    }

    private void ResolveSearchField()
    {
        if (enhanceSearchField)
            return;

        Transform searchField = FindUnderOptionsSection("EnhanceSearchField");
        if (searchField)
            enhanceSearchField = searchField.GetComponent<TMP_InputField>();
    }

    private void ConfigureDetailBarInteraction()
    {
        EnsureOptionDetailBarResolved();
        if (!optionDetailBar)
            return;

        if (!collapseDetailBarButton)
            collapseDetailBarButton = FindDeepChild(optionDetailBar.transform, "CollapseButton")?.GetComponent<Button>();

        if (collapseDetailBarButton)
            collapseDetailBarButton.transform.SetAsLastSibling();

        foreach (TMP_Text label in optionDetailBar.GetComponentsInChildren<TMP_Text>(true))
            label.raycastTarget = false;
    }

    private void ResolveFilterButtons()
    {
        Transform filterRow = FindUnderOptionsSection("FilterRow");
        if (!filterRow)
            return;

        if (!basicFilterButton)
            basicFilterButton = FindDeepChild(filterRow, "BasicFilterButton")?.GetComponent<Button>();
        if (!intermediateFilterButton)
            intermediateFilterButton = FindDeepChild(filterRow, "IntermediateFilterButton")?.GetComponent<Button>();
        if (!advancedFilterButton)
            advancedFilterButton = FindDeepChild(filterRow, "AdvancedFilterButton")?.GetComponent<Button>();
        if (!chaosFilterButton)
            chaosFilterButton = FindDeepChild(filterRow, "ChaosFilterButton")?.GetComponent<Button>();
        if (!specialFilterButton)
            specialFilterButton = FindDeepChild(filterRow, "SpecialFilterButton")?.GetComponent<Button>();
    }

    private void DisableLegacyInventoryGrids()
    {
        Transform inventorySection = FindDeepChild(transform, "InventorySection");
        if (!inventorySection)
            return;

        InventoryGridUI[] legacyGrids = inventorySection.GetComponentsInChildren<InventoryGridUI>(true);
        for (int i = 0; i < legacyGrids.Length; i++)
        {
            if (legacyGrids[i] != null)
                legacyGrids[i].enabled = false;
        }
    }

    private void EnsureUpgradeInventoryGrid()
    {
        Transform slotsGrid = FindUnderChild(transform, "InventorySection", "SlotsGrid");
        if (!slotsGrid)
            slotsGrid = FindDeepChild(transform, "SlotsGrid");

        if (!slotsGrid)
            return;

        if (!inventoryGrid)
            inventoryGrid = slotsGrid.GetComponent<UpgradeInventoryGridUI>();
        if (!inventoryGrid)
            inventoryGrid = slotsGrid.gameObject.AddComponent<UpgradeInventoryGridUI>();

        if (upgradeItemSlotImage != null)
            inventoryGrid.SetUpgradeDropTarget(upgradeItemSlotImage.rectTransform);

        inventoryGrid.GearSelected -= OnGearSelected;
        inventoryGrid.GearSelected += OnGearSelected;
    }

    private void EnsureUpgradeItemSlotHandler()
    {
        if (upgradeItemSlotImage == null)
            return;

        if (!upgradeItemSlotImage.GetComponent<UpgradeItemSlotUI>())
            upgradeItemSlotImage.gameObject.AddComponent<UpgradeItemSlotUI>();
    }

    private void WireApplyButton()
    {
        if (!applyButton)
            return;

        applyButton.onClick.RemoveListener(ApplyEnhancement);
        applyButton.onClick.AddListener(ApplyEnhancement);
    }

    private void FixScrollRectContent()
    {
        if (upgradeListScrollRect == null || upgradeListContent == null)
            return;

        if (upgradeListScrollRect.content == null)
            upgradeListScrollRect.content = upgradeListContent;
    }

    private void ConfigureListLayout()
    {
        if (upgradeListContent == null)
            return;

        VerticalLayoutGroup layout = upgradeListContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        layout.padding = new RectOffset(0, UpgradeListHeaderUI.ListRightPadding, 0, 0);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = true;
    }

    private void ConfigureUpgradeSectionListLayout()
    {
        Transform sectionList = FindOptionsSectionList();
        if (!sectionList)
            return;

        Transform sectionLabel = FindDeepChild(sectionList, "EnhanceLabel");
        if (!sectionLabel)
            sectionLabel = FindDeepChild(sectionList, "UpgradeLabel");
        Transform filterRow = FindDeepChild(sectionList, "FilterRow");
        Transform scrollView = FindDeepChild(sectionList, "Scroll View");

        if (sectionLabel != null && filterRow != null)
            filterRow.SetSiblingIndex(sectionLabel.GetSiblingIndex() + 1);

        if (filterRow is RectTransform filterRect)
        {
            filterRect.anchorMin = new Vector2(0f, 1f);
            filterRect.anchorMax = new Vector2(1f, 1f);
            filterRect.pivot = new Vector2(0f, 1f);
            filterRect.anchoredPosition = new Vector2(0f, -(ListTitleHeight + ListSectionGap));
            filterRect.sizeDelta = new Vector2(0f, FilterRowHeight);
        }

        if (scrollView is RectTransform scrollRect)
        {
            float topInset = ListTitleHeight + ListSectionGap + FilterRowHeight + ListSectionGap +
                             SearchRowHeight + ListSectionGap;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = Vector2.zero;
            scrollRect.sizeDelta = Vector2.zero;
            scrollRect.offsetMax = new Vector2(0f, -topInset);
            RefreshListScrollInsets(scrollRect);
        }
    }

    private void RefreshListScrollInsets(RectTransform scrollRect = null)
    {
        if (!scrollRect)
        {
            Transform scrollView = FindUnderOptionsSection("Scroll View");
            scrollRect = scrollView as RectTransform;
        }

        if (!scrollRect)
            return;

        float bottomInset = GetActiveDetailBarHeight();
        scrollRect.offsetMin = new Vector2(0f, bottomInset);
    }

    private float GetActiveDetailBarHeight()
    {
        if (optionDetailBar == null || !optionDetailBar.activeSelf)
            return 0f;

        if (optionDetailBar.transform is not RectTransform rect)
            return BottomBarHeight;

        float height = rect.rect.height;
        if (height > 1f)
            return height;

        return rect.sizeDelta.y > 1f ? rect.sizeDelta.y : BottomBarHeight;
    }

    private void WireFilterButtons()
    {
        WireFilterButton(basicFilterButton, UpgradeOptionFilter.Basic);
        WireFilterButton(intermediateFilterButton, UpgradeOptionFilter.Intermediate);
        WireFilterButton(advancedFilterButton, UpgradeOptionFilter.Advanced);
        WireFilterButton(chaosFilterButton, UpgradeOptionFilter.Chaos);
        WireFilterButton(specialFilterButton, UpgradeOptionFilter.Special);
    }

    private void UnwireFilterButtons()
    {
        if (basicFilterButton) basicFilterButton.onClick.RemoveAllListeners();
        if (intermediateFilterButton) intermediateFilterButton.onClick.RemoveAllListeners();
        if (advancedFilterButton) advancedFilterButton.onClick.RemoveAllListeners();
        if (chaosFilterButton) chaosFilterButton.onClick.RemoveAllListeners();
        if (specialFilterButton) specialFilterButton.onClick.RemoveAllListeners();
    }

    private void WireFilterButton(Button button, UpgradeOptionFilter filter)
    {
        if (!button)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnFilterClicked(filter));
    }

    private void OnFilterClicked(UpgradeOptionFilter filter)
    {
        _activeFilter = _activeFilter == filter ? UpgradeOptionFilter.None : filter;
        ValidateSelectedOptionForFilter();
        RefreshFilterButtonVisuals();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
    }

    private void RefreshFilterButtonVisuals()
    {
        ApplyFilterButtonVisual(basicFilterButton, _activeFilter == UpgradeOptionFilter.Basic);
        ApplyFilterButtonVisual(intermediateFilterButton, _activeFilter == UpgradeOptionFilter.Intermediate);
        ApplyFilterButtonVisual(advancedFilterButton, _activeFilter == UpgradeOptionFilter.Advanced);
        ApplyFilterButtonVisual(chaosFilterButton, _activeFilter == UpgradeOptionFilter.Chaos);
        ApplyFilterButtonVisual(specialFilterButton, _activeFilter == UpgradeOptionFilter.Special);
    }

    private static void ApplyFilterButtonVisual(Button button, bool active)
    {
        UITabBarButtonVisuals.Apply(button, active);
        if (!button)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = FilterButtonTextColor;
    }

    private void ValidateSelectedOptionForFilter()
    {
        if (_selectedOption != null && !OptionIsVisible(_selectedOption))
            _selectedOption = null;
    }

    private void WireSearchField()
    {
        if (!enhanceSearchField)
            return;

        enhanceSearchField.onValueChanged.RemoveListener(OnSearchQueryChanged);
        enhanceSearchField.onValueChanged.AddListener(OnSearchQueryChanged);
        _searchQuery = enhanceSearchField.text ?? string.Empty;
    }

    private void UnwireSearchField()
    {
        if (enhanceSearchField)
            enhanceSearchField.onValueChanged.RemoveListener(OnSearchQueryChanged);
    }

    private void OnSearchQueryChanged(string value)
    {
        _searchQuery = value ?? string.Empty;
        ValidateSelectedOptionForFilter();
        RefreshEnhancementOptionsList();
        RefreshOptionDetailPanel();
        RefreshEnhancementSelectedLabel();

        if (upgradeListScrollRect)
            upgradeListScrollRect.verticalNormalizedPosition = 1f;
    }

    private bool OptionIsVisible(EnhancementOptionEntry option)
    {
        if (!OptionPassesFilter(option) || !OptionPassesSearchFilter(option))
            return false;

        ItemDefinition gear = GetSelectedGearDefinition();
        if (gear == null)
            return true;

        SkillsManager skills = SkillsManager.Instance;
        return option != null && !option.IsUnavailableForSelection(gear, skills);
    }

    private bool OptionPassesSearchFilter(EnhancementOptionEntry option)
    {
        if (option == null || string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        string query = _searchQuery.Trim();
        if (string.IsNullOrEmpty(query))
            return true;

        return BuildOptionSearchHaystack(option).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string BuildOptionSearchHaystack(EnhancementOptionEntry option)
    {
        if (option == null)
            return string.Empty;

        string type = UpgradeOptionDisplay.FormatOptionType(option);
        string value = UpgradeOptionDisplay.FormatOptionValue(option);
        string scroll = UpgradeOptionDisplay.FormatOptionScrollName(option, itemDatabase);
        string optionId = option.optionId ?? string.Empty;
        string displayName = option.displayName ?? string.Empty;
        return $"{displayName} {optionId} {type} {value} {scroll}";
    }

    private bool OptionPassesFilter(EnhancementOptionEntry option)
    {
        if (option == null)
            return false;

        switch (_activeFilter)
        {
            case UpgradeOptionFilter.None:
                return true;
            case UpgradeOptionFilter.Chaos:
                return option.track == EnhancementTrack.Corruption;
            case UpgradeOptionFilter.Special:
                return option.track == EnhancementTrack.Special;
            case UpgradeOptionFilter.Basic:
                return option.track == EnhancementTrack.Standard && option.tier == EnhancementTier.Basic;
            case UpgradeOptionFilter.Intermediate:
                return option.track == EnhancementTrack.Standard && option.tier == EnhancementTier.Intermediate;
            case UpgradeOptionFilter.Advanced:
                return option.track == EnhancementTrack.Standard && option.tier == EnhancementTier.Advanced;
            default:
                return true;
        }
    }

    private bool SectionHasVisibleOptions(int sectionDisplayIndex)
    {
        for (int i = sectionDisplayIndex + 1; i < _displayRows.Count; i++)
        {
            UpgradeDisplayRow row = _displayRows[i];
            if (row.IsSection)
                break;

            if (OptionIsVisible(row.Option))
                return true;
        }

        return false;
    }

    private void HideLegacyListHeader()
    {
        if (upgradeListContent == null)
            return;

        Transform legacy = upgradeListContent.Find("UpgradeListHeader");
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }

    private void EnsureColumnHeaderPool(int needed)
    {
        while (_columnHeaders.Count < needed)
            _columnHeaders.Add(UpgradeListHeaderUI.Create(upgradeListContent));
    }

    private void EnsureEnhancementOptionDatabase()
    {
        if (enhancementOptionDatabase != null)
            return;

        enhancementOptionDatabase = Resources.Load<EnhancementOptionDatabase>("Databases/EnhancementOptionDatabase");
        if (enhancementOptionDatabase != null)
            return;

        enhancementOptionDatabase = ScriptableObject.CreateInstance<EnhancementOptionDatabase>();
        enhancementOptionDatabase.EnsureDefaults();
    }

    private void BuildDisplayRows()
    {
        _displayRows.Clear();
        EnsureEnhancementOptionDatabase();
        if (enhancementOptionDatabase == null)
            return;

        enhancementOptionDatabase.EnsureDefaults();
        AppendTrackRows(EnhancementTrack.Standard);
        AppendTrackRows(EnhancementTrack.Corruption);
        AppendTrackRows(EnhancementTrack.Special);
    }

    private void AppendTrackRows(EnhancementTrack track)
    {
        List<EnhancementOptionEntry> trackOptions = enhancementOptionDatabase.GetForTrack(track);
        if (trackOptions.Count == 0)
            return;

        _displayRows.Add(new UpgradeDisplayRow
        {
            IsSection = true,
            SectionTitle = UpgradeOptionDisplay.FormatSectionTitle(track),
        });

        for (int i = 0; i < trackOptions.Count; i++)
        {
            EnhancementOptionEntry option = trackOptions[i];
            if (option == null)
                continue;

            _displayRows.Add(new UpgradeDisplayRow { Option = option });
        }
    }

    private void RefreshAll()
    {
        BuildDisplayRows();
        RefreshInventoryGrid();
        RefreshSelectedGearSlot();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
    }

    private void RefreshInventoryGrid()
    {
        if (inventoryGrid == null)
            return;

        if (upgradeItemSlotImage != null)
            inventoryGrid.SetUpgradeDropTarget(upgradeItemSlotImage.rectTransform);

        inventoryGrid.MarkDirty();
        inventoryGrid.SetSelectedSourceSlot(_selectedGearSlotIndex);
        inventoryGrid.Rebuild();
    }

    private void OnGearSelected(int sourceSlotIndex, ItemDefinition def, string itemId)
    {
        _selectedFromEquipment = false;
        _selectedEquipmentSlot = EquipmentUISlotType.None;
        _selectedGearSlotIndex = sourceSlotIndex;
        _selectedGearItemId = itemId;
        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
        SyncInventoryGridSelection();
    }

    private void OnEnhancementOptionSelected(EnhancementOptionEntry option)
    {
        _selectedOption = option;
        ClearApplyError();
        RefreshEnhancementSelectedLabel();
        RefreshEnhancementOptionsList();
        RefreshOptionDetailPanel();
    }

    private void RefreshSelectedGearSlot()
    {
        ItemDefinition def = null;

        if (_selectedFromEquipment)
        {
            Inventory inv = ResolveInventory();
            string itemId = EquipmentSlotUI.GetItemIdForSlot(
                _selectedEquipmentSlot,
                ResolveEquipment(),
                ResolveToolbelt());
            if (!string.IsNullOrWhiteSpace(itemId) && inv != null)
            {
                def = inv.GetItemDef(itemId);
                if (def == null && itemDatabase != null)
                    def = itemDatabase.Get(itemId);

                if (def != null && IsUpgradableGear(def) && !def.IsCombatSupport)
                    _selectedGearItemId = itemId;
                else
                    def = null;
            }
        }
        else if (_selectedGearSlotIndex >= 0 && _inventory != null &&
            _selectedGearSlotIndex < _inventory.SlotCount)
        {
            Inventory.Slot slot = _inventory.GetSlot(_selectedGearSlotIndex);
            if (!slot.IsEmpty)
            {
                def = _inventory.GetItemDef(slot.itemId);
                if (def == null && itemDatabase != null)
                    def = itemDatabase.Get(slot.itemId);

                if (def != null && IsUpgradableGear(def))
                    _selectedGearItemId = slot.itemId;
                else
                    def = null;
            }
        }

        if (def == null)
        {
            _selectedFromEquipment = false;
            _selectedEquipmentSlot = EquipmentUISlotType.None;
            _selectedGearSlotIndex = -1;
            _selectedGearItemId = null;
            ValidateSelectedOptionForGear(null);
            RefreshSelectedGearSlotImage(null);
            RefreshItemLabel(null);
            SyncInventoryGridSelection();
            RefreshOptionDetailPanel();
            RefreshSlotsAvailableLabel();
            return;
        }

        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        SyncInventoryGridSelection();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
    }

    private void SyncInventoryGridSelection()
    {
        if (inventoryGrid != null)
            inventoryGrid.SetSelectedSourceSlot(_selectedGearSlotIndex);
    }

    private ItemDefinition GetSelectedGearDefinition()
    {
        Inventory inv = ResolveInventory();
        if (inv == null)
            return null;

        if (_selectedFromEquipment)
        {
            if (_selectedEquipmentSlot == EquipmentUISlotType.None)
                return null;

            string itemId = EquipmentSlotUI.GetItemIdForSlot(
                _selectedEquipmentSlot,
                ResolveEquipment(),
                ResolveToolbelt());
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            ItemDefinition equippedDef = inv.GetItemDef(itemId);
            if (equippedDef == null && itemDatabase != null)
                equippedDef = itemDatabase.Get(itemId);

            if (!IsUpgradableGear(equippedDef) || equippedDef.IsCombatSupport)
                return null;

            _selectedGearItemId = itemId;
            return equippedDef;
        }

        if (_selectedGearSlotIndex < 0 || _inventory == null)
            return null;

        if (_selectedGearSlotIndex >= _inventory.SlotCount)
            return null;

        Inventory.Slot slot = _inventory.GetSlot(_selectedGearSlotIndex);
        if (slot.IsEmpty)
            return null;

        ItemDefinition def = _inventory.GetItemDef(slot.itemId);
        if (def == null && itemDatabase != null)
            def = itemDatabase.Get(slot.itemId);

        if (!IsUpgradableGear(def))
            return null;

        _selectedGearItemId = slot.itemId;
        return def;
    }

    private void ValidateSelectedOptionForGear(ItemDefinition gearDef)
    {
        if (_selectedOption == null)
            return;

        if (!OptionIsVisible(_selectedOption))
            _selectedOption = null;
    }

    private void SyncSelectedGearItemIdFromSlot()
    {
        if (_selectedFromEquipment)
        {
            string itemId = EquipmentSlotUI.GetItemIdForSlot(
                _selectedEquipmentSlot,
                ResolveEquipment(),
                ResolveToolbelt());
            if (!string.IsNullOrWhiteSpace(itemId))
                _selectedGearItemId = itemId;
            return;
        }

        if (_inventory == null || _selectedGearSlotIndex < 0 ||
            _selectedGearSlotIndex >= _inventory.SlotCount)
            return;

        Inventory.Slot slot = _inventory.GetSlot(_selectedGearSlotIndex);
        if (!slot.IsEmpty)
            _selectedGearItemId = slot.itemId;
    }

    public void ShowSelectedGearTooltip(RectTransform anchor)
    {
        ItemDefinition def = GetSelectedGearDefinition();
        SharedTooltipUI tooltip = ResolveEnhanceTooltip();
        if (def == null || tooltip == null || anchor == null)
            return;

        ConfigureEnhanceTooltipFlip(tooltip);
        tooltip.ShowAt(anchor, def, 1, compact: false, itemId: _selectedGearItemId);
    }

    public void HideSelectedGearTooltip()
    {
        ResolveEnhanceTooltip()?.Hide();
    }

    private SharedTooltipUI ResolveEnhanceTooltip()
    {
        if (inventoryGrid != null && inventoryGrid.Tooltip)
            return inventoryGrid.Tooltip;

        return GetComponentInChildren<SharedTooltipUI>(true);
    }

    private void ConfigureEnhanceTooltipFlip(SharedTooltipUI tooltip)
    {
        if (tooltip == null || inventoryGrid == null)
            return;

        FlipInsideBounds flipper = tooltip.GetComponent<FlipInsideBounds>();
        if (!flipper)
            return;

        flipper.SetPreferredSide(inventoryGrid.TooltipPreferredSide);
        if (inventoryGrid.TooltipHeightRect)
        {
            flipper.SetMeasureRect(inventoryGrid.TooltipHeightRect);
            flipper.SetHeightRect(inventoryGrid.TooltipHeightRect);
        }
    }

    private static bool IsUpgradableGear(ItemDefinition def)
    {
        if (def == null || !def.HasUpgradeSlots)
            return false;

        return def.IsWeapon || def.IsArmor || def.IsTool;
    }

    private void RefreshSelectedGearSlotImage(ItemDefinition def)
    {
        if (!upgradeItemSlotImage)
            return;

        bool hasIcon = def != null && def.icon != null;
        upgradeItemSlotImage.sprite = hasIcon ? def.icon : null;
        upgradeItemSlotImage.preserveAspect = true;
        upgradeItemSlotImage.color = hasIcon
            ? Color.white
            : new Color(0.2679245f, 0.26351428f, 0.2562976f, 1f);
    }

    private void RefreshItemLabel(ItemDefinition def)
    {
        if (!itemLabelText)
            return;

        if (def == null)
        {
            itemLabelText.text = "Item: None";
            return;
        }

        string name = !string.IsNullOrWhiteSpace(def.displayName) ? def.displayName.Trim() : def.itemId;
        itemLabelText.text = $"Item: {name}";
    }

    private void RefreshEnhancementSelectedLabel()
    {
        if (!enhancementSelectedText)
            return;

        ItemDefinition gear = GetSelectedGearDefinition();
        Inventory inv = ResolveInventory();
        EnhancementOptionPayment payment = _selectedOption != null && gear != null
            ? EnhancementOptionPayment.Resolve(inv, _selectedOption, gear)
            : EnhancementOptionPayment.None;
        enhancementSelectedText.text = UpgradeOptionDisplay.FormatSelectedEnhancementLine(
            _selectedOption,
            payment,
            itemDatabase);
    }

    private void ClearSelectedOption()
    {
        if (_selectedOption == null && (optionDetailBar == null || !optionDetailBar.activeSelf))
            return;

        _selectedOption = null;
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
    }

    private void OnCollapseDetailBarClicked() => ClearSelectedOption();

    private void WireCollapseButton()
    {
        if (!collapseDetailBarButton)
            return;

        collapseDetailBarButton.onClick.RemoveListener(OnCollapseDetailBarClicked);
        collapseDetailBarButton.onClick.AddListener(OnCollapseDetailBarClicked);
    }

    private void UnwireCollapseButton()
    {
        if (collapseDetailBarButton)
            collapseDetailBarButton.onClick.RemoveListener(OnCollapseDetailBarClicked);
    }

    private void RefreshOptionDetailPanel()
    {
        EnsureOptionDetailBarResolved();

        bool showDetails = _selectedOption != null;
        if (optionDetailBar != null && optionDetailBar.activeSelf != showDetails)
            optionDetailBar.SetActive(showDetails);

        if (showDetails)
            ConfigureDetailBarInteraction();

        RefreshListScrollInsets();

        if (!showDetails)
        {
            SetOptionDetailText(optionNameText, string.Empty);
            SetOptionDetailText(optionCostText, string.Empty, richText: false);
            SetOptionDetailText(optionValueText, string.Empty);
            SetOptionDetailText(optionAvailableItemsText, string.Empty);
            SetOptionDetailText(optionChanceText, string.Empty);
            SetOptionDetailText(optionAdditionalText, string.Empty);
            return;
        }

        ItemDefinition gear = GetSelectedGearDefinition();
        Inventory inv = ResolveInventory();

        SetOptionDetailText(optionNameText, UpgradeOptionDisplay.FormatOptionDetailName(_selectedOption));
        SetOptionDetailText(
            optionCostText,
            UpgradeOptionDisplay.FormatLabeledEnhanceCost(_selectedOption, gear, itemDatabase, inv),
            richText: true);
        SetOptionDetailText(optionValueText, UpgradeOptionDisplay.FormatLabeledValue(_selectedOption));
        SetOptionDetailText(optionAvailableItemsText, UpgradeOptionDisplay.FormatLabeledItemType(_selectedOption));
        SetOptionDetailText(optionChanceText, UpgradeOptionDisplay.FormatLabeledSuccessChance(_selectedOption, gear));
        SetOptionDetailText(optionAdditionalText, UpgradeOptionDisplay.FormatLabeledAdditionalInfo(_selectedOption));
    }

    private const string MaxRankErrorMessage = "ITEM IS AT MAX RANK";
    private const string AllSlotsUsedErrorMessage = "ALL UPGRADE SLOTS USED";

    private void RefreshSlotsAvailableLabel()
    {
        ItemDefinition gear = GetSelectedGearDefinition();

        if (slotsAvailableText)
            slotsAvailableText.text = UpgradeOptionDisplay.FormatAvailableUpgradeSlots(gear);

        if (gear == null || !gear.HasUpgradeSlots)
        {
            ClearSlotLimitApplyError();
            return;
        }

        if (gear.HasReachedEnhancementCap)
        {
            ShowApplyError(MaxRankErrorMessage);
            return;
        }

        if (!gear.HasAvailableUpgradeSlot)
        {
            ShowApplyError(AllSlotsUsedErrorMessage);
            return;
        }

        ClearSlotLimitApplyError();
    }

    private void ClearSlotLimitApplyError()
    {
        if (errorLabelText == null)
            return;

        string current = errorLabelText.text ?? string.Empty;
        if (string.Equals(current, MaxRankErrorMessage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(current, AllSlotsUsedErrorMessage, StringComparison.OrdinalIgnoreCase))
            ClearApplyError();
    }

    private static void SetOptionDetailText(TMP_Text text, string value, bool richText = false)
    {
        if (text == null)
            return;

        text.richText = richText;
        text.text = value ?? string.Empty;
    }

    private void ShowApplyError(string message)
    {
        if (!errorLabelText)
            return;

        errorLabelText.text = message ?? string.Empty;
        if (!errorLabelText.gameObject.activeSelf)
            errorLabelText.gameObject.SetActive(true);
    }

    private void ClearApplyError()
    {
        if (!errorLabelText)
            return;

        errorLabelText.text = string.Empty;
        if (errorLabelText.gameObject.activeSelf)
            errorLabelText.gameObject.SetActive(false);
    }

    private string TryGetApplyErrorMessage()
    {
        if (_selectedOption == null)
            return "Select an upgrade option.";

        if (!HasSelectedGear())
            return "Select an item to upgrade.";

        Inventory inv = ResolveInventory();
        ItemDefinition gear = GetSelectedGearDefinition();
        if (inv == null || gear == null)
            return "Select a valid item to upgrade.";

        SkillsManager skills = SkillsManager.Instance;
        if (!_selectedOption.PlayerMeetsTierSkillRequirement(skills, gear))
        {
            SkillType gateSkill = _selectedOption.ResolveGateSkill(gear);
            int requiredLevel = EnhancementTierRules.GetRequiredPlayerSkillLevel(_selectedOption.tier);
            return $"Requires {gateSkill} level {requiredLevel}.";
        }

        if (!_selectedOption.TargetsGear(gear))
            return "This upgrade cannot be applied to the selected item.";

        if (!EnhancementTierRules.GearAllowsEnhancementTier(gear.GetEquipmentTierRank(), _selectedOption.tier))
            return "This item's tier is too low for that upgrade.";

        EnhancementScrollStats stats = _selectedOption.ToScrollStats();
        if (stats.targetStat == EnhancementScrollTargetStat.UpgradeSlotReduction)
        {
            if (gear.UsedUpgradeSlots <= 0)
                return "This item has no used upgrade slots to recover.";
        }
        else
        {
            if (gear.HasReachedEnhancementCap)
                return MaxRankErrorMessage;

            if (!gear.HasAvailableUpgradeSlot)
                return AllSlotsUsedErrorMessage;

            if (!gear.HasBaseStatForEnhancementScroll(stats.targetStat))
                return "This item does not have the required stat for that upgrade.";
        }

        EnhancementOptionPayment payment = EnhancementOptionPayment.Resolve(inv, _selectedOption, gear);
        if (payment.Kind == EnhancementPaymentKind.None)
        {
            if (EnhancementOptionPayment.RequiresScrollOnlyPayment(_selectedOption))
                return "You need the required scroll for this upgrade.";

            return "Not enough scrolls or materials.";
        }

        return null;
    }

    private void RefreshEnhancementOptionsList()
    {
        if (upgradeListContent == null || upgradeListEntryPrefab == null)
            return;

        if (_displayRows.Count == 0)
            BuildDisplayRows();

        int optionNeeded = 0;
        int sectionNeeded = 0;
        for (int i = 0; i < _displayRows.Count; i++)
        {
            UpgradeDisplayRow row = _displayRows[i];
            if (row.IsSection)
            {
                if (SectionHasVisibleOptions(i))
                    sectionNeeded++;
            }
            else if (OptionIsVisible(row.Option))
            {
                optionNeeded++;
            }
        }

        EnsureSectionRowPool(sectionNeeded);
        EnsureColumnHeaderPool(sectionNeeded);
        EnsureOptionRowPool(optionNeeded);

        Inventory inv = ResolveInventory();
        ItemDefinition selectedGear = GetSelectedGearDefinition();

        int optionIndex = 0;
        int sectionIndex = 0;
        int columnHeaderIndex = 0;
        int visibleChildIndex = 0;
        for (int i = 0; i < _displayRows.Count; i++)
        {
            UpgradeDisplayRow displayRow = _displayRows[i];
            if (displayRow.IsSection)
            {
                if (!SectionHasVisibleOptions(i))
                    continue;

                UpgradeListSectionHeaderUI section = _sectionRows[sectionIndex++];
                section.gameObject.SetActive(true);
                section.SetTitle(displayRow.SectionTitle);
                section.transform.SetSiblingIndex(visibleChildIndex++);

                UpgradeListHeaderUI columnHeader = _columnHeaders[columnHeaderIndex++];
                columnHeader.gameObject.SetActive(true);
                columnHeader.BuildIfNeeded();
                columnHeader.transform.SetSiblingIndex(visibleChildIndex++);
                continue;
            }

            if (!OptionIsVisible(displayRow.Option))
                continue;

            UpgradeListEntryUI row = _optionRows[optionIndex++];
            row.gameObject.SetActive(true);
            row.transform.SetSiblingIndex(visibleChildIndex++);

            EnhancementOptionEntry option = displayRow.Option;
            bool canPay = EnhancementOptionPayment.HasAnyPayment(inv, option, selectedGear);
            bool dimForMissingPayment = selectedGear != null && !canPay;
            bool selected = _selectedOption != null &&
                            string.Equals(_selectedOption.optionId, option.optionId, StringComparison.OrdinalIgnoreCase);
            row.BindOption(option, canPay, selected, dimForMissingPayment, hasScrollSprite, missingScrollSprite,
                OnEnhancementOptionSelected);
        }

        for (int i = optionIndex; i < _optionRows.Count; i++)
            _optionRows[i].gameObject.SetActive(false);
        for (int i = sectionIndex; i < _sectionRows.Count; i++)
            _sectionRows[i].gameObject.SetActive(false);
        for (int i = columnHeaderIndex; i < _columnHeaders.Count; i++)
            _columnHeaders[i].gameObject.SetActive(false);

        RefreshTierTooLowLabel(optionIndex);
    }

    private void RefreshTierTooLowLabel(int visibleOptionCount)
    {
        if (visibleOptionCount > 0 || !TryBuildGearTierTooLowMessage(out string message))
        {
            if (_tierTooLowLabel)
                _tierTooLowLabel.gameObject.SetActive(false);
            return;
        }

        EnsureTierTooLowLabel();
        _tierTooLowLabel.gameObject.SetActive(true);
        _tierTooLowLabel.text = message;
        _tierTooLowLabel.transform.SetSiblingIndex(0);
    }

    private bool TryBuildGearTierTooLowMessage(out string message)
    {
        message = null;
        ItemDefinition gear = GetSelectedGearDefinition();
        if (gear == null || !TryGetActiveTierFilter(out EnhancementTier filterTier))
            return false;

        if (EnhancementTierRules.GearAllowsEnhancementTier(gear.GetEquipmentTierRank(), filterTier))
            return false;

        int minTier = EnhancementTierRules.GetMinimumGearTierDisplayNumber(filterTier);
        message = $"This items tier is too low - must be tier {minTier} or higher";
        return true;
    }

    private bool TryGetActiveTierFilter(out EnhancementTier enhancementTier)
    {
        switch (_activeFilter)
        {
            case UpgradeOptionFilter.Basic:
                enhancementTier = EnhancementTier.Basic;
                return true;
            case UpgradeOptionFilter.Intermediate:
                enhancementTier = EnhancementTier.Intermediate;
                return true;
            case UpgradeOptionFilter.Advanced:
                enhancementTier = EnhancementTier.Advanced;
                return true;
            default:
                enhancementTier = EnhancementTier.Basic;
                return false;
        }
    }

    private void EnsureTierTooLowLabel()
    {
        if (_tierTooLowLabel || upgradeListContent == null)
            return;

        GameObject root = new GameObject("TierTooLowLabel", typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(upgradeListContent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 36f);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 36f;
        layout.flexibleWidth = 1f;

        TextMeshProUGUI tmp = root.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14f;
        tmp.color = TierTooLowTextColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.margin = new Vector4(8f, 8f, 8f, 8f);
        tmp.raycastTarget = false;

        _tierTooLowLabel = tmp;
    }

    private void EnsureOptionRowPool(int needed)
    {
        while (_optionRows.Count < needed)
        {
            UpgradeListEntryUI created = Instantiate(upgradeListEntryPrefab, upgradeListContent);
            created.gameObject.SetActive(true);
            _optionRows.Add(created);
        }
    }

    private void EnsureSectionRowPool(int needed)
    {
        while (_sectionRows.Count < needed)
            _sectionRows.Add(UpgradeListSectionHeaderUI.Create(upgradeListContent, string.Empty));
    }

    private bool HasSelectedGear() =>
        (_selectedFromEquipment && _selectedEquipmentSlot != EquipmentUISlotType.None) ||
        _selectedGearSlotIndex >= 0;

    private Inventory ResolveInventory()
    {
        if (_inventory != null)
            return _inventory;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _inventory = player.GetComponent<Inventory>();

        _inventory ??= FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        return _inventory;
    }

    private EquipmentManager ResolveEquipment()
    {
        if (_equipment != null)
            return _equipment;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _equipment = player.GetComponent<EquipmentManager>();

        _equipment ??= FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        return _equipment;
    }

    private ToolbeltManager ResolveToolbelt()
    {
        if (_toolbelt != null)
            return _toolbelt;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            _toolbelt = player.GetComponent<ToolbeltManager>();

        _toolbelt ??= FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);
        return _toolbelt;
    }

    private EquipmentManager _subscribedEquipment;
    private ToolbeltManager _subscribedToolbelt;

    private void TrySubscribeEquipment()
    {
        EquipmentManager eq = ResolveEquipment();
        if (eq == _subscribedEquipment)
            return;

        if (_subscribedEquipment != null)
            _subscribedEquipment.OnUISlotChanged -= OnEquipmentSlotChanged;

        _subscribedEquipment = eq;
        if (_subscribedEquipment != null)
            _subscribedEquipment.OnUISlotChanged += OnEquipmentSlotChanged;
    }

    private void UnsubscribeEquipment()
    {
        if (_subscribedEquipment != null)
            _subscribedEquipment.OnUISlotChanged -= OnEquipmentSlotChanged;
        _subscribedEquipment = null;
    }

    private void TrySubscribeToolbelt()
    {
        ToolbeltManager belt = ResolveToolbelt();
        if (belt == _subscribedToolbelt)
            return;

        if (_subscribedToolbelt != null)
            _subscribedToolbelt.OnToolSlotChanged -= OnToolbeltSlotChanged;

        _subscribedToolbelt = belt;
        if (_subscribedToolbelt != null)
            _subscribedToolbelt.OnToolSlotChanged += OnToolbeltSlotChanged;
    }

    private void UnsubscribeToolbelt()
    {
        if (_subscribedToolbelt != null)
            _subscribedToolbelt.OnToolSlotChanged -= OnToolbeltSlotChanged;
        _subscribedToolbelt = null;
    }

    private void OnEquipmentSlotChanged(EquipmentUISlotType slot, string _)
    {
        if (!_selectedFromEquipment || slot != _selectedEquipmentSlot)
            return;

        RefreshSelectedGearSlot();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
    }

    private void OnToolbeltSlotChanged(int index, string _)
    {
        if (!_selectedFromEquipment)
            return;

        int selectedIndex = _selectedEquipmentSlot switch
        {
            EquipmentUISlotType.Toolbelt0 => 0,
            EquipmentUISlotType.Toolbelt1 => 1,
            EquipmentUISlotType.Toolbelt2 => 2,
            EquipmentUISlotType.Toolbelt3 => 3,
            _ => -1
        };

        if (selectedIndex != index)
            return;

        RefreshSelectedGearSlot();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        RefreshOptionDetailPanel();
        RefreshSlotsAvailableLabel();
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (!root)
            return null;

        if (string.Equals(root.name, childName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found)
                return found;
        }

        return null;
    }

    private Transform FindOptionsSectionList()
    {
        Transform section = FindDeepChild(transform, "EnhanceSectionList");
        if (!section)
            section = FindDeepChild(transform, "UpgradeSectionList");
        return section;
    }

    private Transform FindGearSection()
    {
        Transform section = FindDeepChild(transform, "EnhanceSection");
        if (!section)
            section = FindDeepChild(transform, "UpgradeSection");
        return section;
    }

    private Transform FindUnderOptionsSection(string childName)
    {
        Transform section = FindOptionsSectionList();
        return section != null ? FindDeepChild(section, childName) : null;
    }

    private Transform FindUnderGearSection(string childName)
    {
        Transform section = FindGearSection();
        return section != null ? FindDeepChild(section, childName) : null;
    }

    private void EnsureOptionDetailBarResolved()
    {
        if (optionDetailBar)
            return;

        Transform bottomBar = FindUnderOptionsSection("BottomBar");
        if (bottomBar)
        {
            optionDetailBar = bottomBar.gameObject;
            return;
        }

        if (!optionNameText)
            return;

        Transform ancestor = optionNameText.transform;
        while (ancestor != null && ancestor != transform)
        {
            if (string.Equals(ancestor.name, "BottomBar", StringComparison.Ordinal))
            {
                optionDetailBar = ancestor.gameObject;
                return;
            }

            ancestor = ancestor.parent;
        }
    }

    private static Transform FindUnderChild(Transform root, string sectionName, string childName)
    {
        Transform section = FindDeepChild(root, sectionName);
        return section != null ? FindDeepChild(section, childName) : null;
    }

    private static bool ItemIdEquals(string a, string b) =>
        string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : string.Equals(a, b, StringComparison.Ordinal);

    private void ResolveEditorAssetRefs()
    {
#if UNITY_EDITOR
        if (!upgradeListEntryPrefab)
            upgradeListEntryPrefab = AssetDatabase.LoadAssetAtPath<UpgradeListEntryUI>(UpgradeListEntryPrefabPath);
        if (!itemDatabase)
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
                itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (!enhancementOptionDatabase)
            enhancementOptionDatabase = AssetDatabase.LoadAssetAtPath<EnhancementOptionDatabase>(EnhancementOptionDatabasePath);
#endif

        enhancementOptionDatabase ??= Resources.Load<EnhancementOptionDatabase>("Databases/EnhancementOptionDatabase");
    }

    private void ResolveSpriteRefs()
    {
#if UNITY_EDITOR
        if (!hasScrollSprite)
            hasScrollSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TickSpritePath);
        if (!missingScrollSprite)
            missingScrollSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CrossSpritePath);
#endif
    }
}
