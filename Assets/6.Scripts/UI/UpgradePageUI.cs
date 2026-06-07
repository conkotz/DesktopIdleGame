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

    [Header("Enhancement options list")]
    [SerializeField] private RectTransform upgradeListContent;
    [SerializeField] private ScrollRect upgradeListScrollRect;
    [SerializeField] private UpgradeListEntryUI upgradeListEntryPrefab;
    [SerializeField] private EnhancementOptionDatabase enhancementOptionDatabase;
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Payment availability icons")]
    [SerializeField] private Sprite hasScrollSprite;
    [SerializeField] private Sprite missingScrollSprite;

    private readonly List<UpgradeListEntryUI> _optionRows = new(64);
    private readonly List<UpgradeListSectionHeaderUI> _sectionRows = new(8);
    private readonly List<UpgradeDisplayRow> _displayRows = new(64);

    private UpgradeListHeaderUI _listHeader;

    private Inventory _inventory;
    private int _selectedGearSlotIndex = -1;
    private string _selectedGearItemId;
    private EnhancementOptionEntry _selectedOption;
    private bool _initialized;

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
        EnsureListHeader();
        ConfigureListLayout();
        TrySubscribeInventory();
        DisableLegacyInventoryGrids();
        RefreshAll();
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
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
        EnsureListHeader();
        WireApplyButton();
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

        _selectedGearSlotIndex = inventorySlotIndex;
        _selectedGearItemId = slot.itemId;
        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        RefreshInventoryGrid();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
    }

    public void ClearSelectedGear()
    {
        _selectedGearSlotIndex = -1;
        _selectedGearItemId = null;
        ValidateSelectedOptionForGear(null);
        RefreshSelectedGearSlotImage(null);
        RefreshItemLabel(null);
        SyncInventoryGridSelection();
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
    }

    public void ApplyEnhancement()
    {
        if (_selectedGearSlotIndex < 0 || _selectedOption == null)
            return;

        Inventory inv = ResolveInventory();
        ItemDefinition gear = GetSelectedGearDefinition();
        if (inv == null || gear == null)
            return;

        EnhancementOptionPayment payment = EnhancementOptionPayment.Resolve(inv, _selectedOption, gear, preferScroll: true);
        if (payment.Kind == EnhancementPaymentKind.None)
            return;

        bool attempted = EnhancementUpgradeService.TryApplyOptionOnInventorySlot(
            inv,
            _selectedGearSlotIndex,
            _selectedOption,
            payment,
            out bool success);

        if (attempted)
            EnhancementFlashUI.Flash(success);
    }

    private void ResolveSceneRefs()
    {
        if (!inventoryGrid)
            inventoryGrid = GetComponentInChildren<UpgradeInventoryGridUI>(true);

        if (!upgradeItemSlotImage)
        {
            Transform slot = FindUnderChild(transform, "UpgradeSection", "UpgradeItemSlot");
            if (slot)
                upgradeItemSlotImage = slot.GetComponent<Image>();
        }

        if (!itemLabelText)
        {
            Transform itemLabel = FindUnderChild(transform, "UpgradeSection", "ItemLabel");
            if (itemLabel)
            {
                Transform text = FindDeepChild(itemLabel, "ItemLabelText");
                if (text)
                    itemLabelText = text.GetComponent<TMP_Text>();
            }
        }

        if (!enhancementSelectedText)
        {
            Transform enhancementSelected = FindUnderChild(transform, "UpgradeSection", "EnhancementSelected");
            if (!enhancementSelected)
                enhancementSelected = FindUnderChild(transform, "UpgradeSection", "EnhacementSelected");
            if (enhancementSelected)
            {
                Transform text = FindDeepChild(enhancementSelected, "SelectedText");
                if (text)
                    enhancementSelectedText = text.GetComponent<TMP_Text>();
            }
        }

        if (!applyButton)
        {
            Transform apply = FindUnderChild(transform, "UpgradeSection", "ApplyButton");
            if (apply)
                applyButton = apply.GetComponent<Button>();
        }

        if (!upgradeListContent)
        {
            Transform content = FindUnderChild(transform, "UpgradeSectionList", "Content");
            if (content)
                upgradeListContent = content as RectTransform;
        }

        if (!upgradeListScrollRect)
        {
            Transform scrollView = FindUnderChild(transform, "UpgradeSectionList", "Scroll View");
            if (scrollView)
                upgradeListScrollRect = scrollView.GetComponent<ScrollRect>();
        }

        if (!itemDatabase)
        {
            Inventory inv = ResolveInventory();
            if (inv != null)
                itemDatabase = inv.GetItemDatabase();
        }
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

    private void EnsureListHeader()
    {
        if (_listHeader != null || upgradeListContent == null)
            return;

        Transform existing = upgradeListContent.Find("UpgradeListHeader");
        if (existing != null)
        {
            _listHeader = existing.GetComponent<UpgradeListHeaderUI>();
            if (_listHeader != null)
            {
                _listHeader.BuildIfNeeded();
                existing.SetAsFirstSibling();
                return;
            }
        }

        GameObject headerGo = new GameObject("UpgradeListHeader", typeof(RectTransform), typeof(UpgradeListHeaderUI));
        headerGo.transform.SetParent(upgradeListContent, false);
        headerGo.transform.SetAsFirstSibling();

        RectTransform headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 30f);

        _listHeader = headerGo.GetComponent<UpgradeListHeaderUI>();
        _listHeader.BuildIfNeeded();
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
        _selectedGearSlotIndex = sourceSlotIndex;
        _selectedGearItemId = itemId;
        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        RefreshEnhancementOptionsList();
        RefreshEnhancementSelectedLabel();
        SyncInventoryGridSelection();
    }

    private void OnEnhancementOptionSelected(EnhancementOptionEntry option)
    {
        ItemDefinition gearDef = GetSelectedGearDefinition();
        SkillsManager skills = SkillsManager.Instance;
        if (option != null && option.IsUnavailableForSelection(gearDef, skills))
            return;

        _selectedOption = option;
        RefreshEnhancementSelectedLabel();
        RefreshEnhancementOptionsList();
    }

    private void RefreshSelectedGearSlot()
    {
        ItemDefinition def = null;

        if (_selectedGearSlotIndex >= 0 && _inventory != null)
        {
            if (_selectedGearSlotIndex < _inventory.SlotCount)
            {
                Inventory.Slot slot = _inventory.GetSlot(_selectedGearSlotIndex);
                if (!slot.IsEmpty && ItemIdEquals(slot.itemId, _selectedGearItemId))
                {
                    def = _inventory.GetItemDef(slot.itemId);
                    if (def == null && itemDatabase != null)
                        def = itemDatabase.Get(slot.itemId);

                    if (def != null && !IsUpgradableGear(def))
                        def = null;
                }
            }
        }

        if (def == null)
        {
            _selectedGearSlotIndex = -1;
            _selectedGearItemId = null;
            ValidateSelectedOptionForGear(null);
            RefreshSelectedGearSlotImage(null);
            RefreshItemLabel(null);
            SyncInventoryGridSelection();
            return;
        }

        ValidateSelectedOptionForGear(def);
        RefreshSelectedGearSlotImage(def);
        RefreshItemLabel(def);
        SyncInventoryGridSelection();
    }

    private void SyncInventoryGridSelection()
    {
        if (inventoryGrid != null)
            inventoryGrid.SetSelectedSourceSlot(_selectedGearSlotIndex);
    }

    private ItemDefinition GetSelectedGearDefinition()
    {
        if (_selectedGearSlotIndex < 0 || _inventory == null)
            return null;

        if (_selectedGearSlotIndex >= _inventory.SlotCount)
            return null;

        Inventory.Slot slot = _inventory.GetSlot(_selectedGearSlotIndex);
        if (slot.IsEmpty || !ItemIdEquals(slot.itemId, _selectedGearItemId))
            return null;

        ItemDefinition def = _inventory.GetItemDef(slot.itemId);
        if (def == null && itemDatabase != null)
            def = itemDatabase.Get(slot.itemId);

        return IsUpgradableGear(def) ? def : null;
    }

    private void ValidateSelectedOptionForGear(ItemDefinition gearDef)
    {
        if (_selectedOption == null)
            return;

        SkillsManager skills = SkillsManager.Instance;
        if (_selectedOption.IsUnavailableForSelection(gearDef, skills) ||
            (gearDef != null && !_selectedOption.CanApplyToGear(gearDef, skills)))
            _selectedOption = null;
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
            ? EnhancementOptionPayment.Resolve(inv, _selectedOption, gear, preferScroll: true)
            : EnhancementOptionPayment.None;
        enhancementSelectedText.text = UpgradeOptionDisplay.FormatSelectedEnhancementLine(
            _selectedOption,
            payment,
            itemDatabase);
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
            if (_displayRows[i].IsSection)
                sectionNeeded++;
            else
                optionNeeded++;
        }

        EnsureSectionRowPool(sectionNeeded);
        EnsureOptionRowPool(optionNeeded);

        Inventory inv = ResolveInventory();
        ItemDefinition selectedGear = GetSelectedGearDefinition();
        SkillsManager skills = SkillsManager.Instance;

        int optionIndex = 0;
        int sectionIndex = 0;
        for (int i = 0; i < _displayRows.Count; i++)
        {
            UpgradeDisplayRow displayRow = _displayRows[i];
            if (displayRow.IsSection)
            {
                UpgradeListSectionHeaderUI section = _sectionRows[sectionIndex++];
                section.gameObject.SetActive(true);
                section.SetTitle(displayRow.SectionTitle);
                section.transform.SetSiblingIndex(GetContentChildIndex(i));
                continue;
            }

            UpgradeListEntryUI row = _optionRows[optionIndex++];
            row.gameObject.SetActive(true);
            row.transform.SetSiblingIndex(GetContentChildIndex(i));

            EnhancementOptionEntry option = displayRow.Option;
            bool unavailableForGear = option != null &&
                                      option.IsUnavailableForSelection(selectedGear, skills);
            bool canPay = !unavailableForGear &&
                          selectedGear != null &&
                          option.CanApplyToGear(selectedGear, skills) &&
                          EnhancementOptionPayment.HasAnyPayment(inv, option, selectedGear);
            bool selected = _selectedOption != null &&
                            string.Equals(_selectedOption.optionId, option.optionId, StringComparison.OrdinalIgnoreCase);
            row.BindOption(option, canPay, selected, unavailableForGear, hasScrollSprite, missingScrollSprite,
                OnEnhancementOptionSelected);
        }

        for (int i = optionIndex; i < _optionRows.Count; i++)
            _optionRows[i].gameObject.SetActive(false);
        for (int i = sectionIndex; i < _sectionRows.Count; i++)
            _sectionRows[i].gameObject.SetActive(false);
    }

    private int GetContentChildIndex(int displayRowIndex)
    {
        int childIndex = _listHeader != null ? 1 : 0;
        for (int i = 0; i < displayRowIndex; i++)
            childIndex++;
        return childIndex;
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
