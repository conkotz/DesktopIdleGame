using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DatabaseSection
{
    General = 0,
    Enemies = 1,
    Items = 2,
}

public enum DatabaseEnemySubtab
{
    GeneralInformation = 0,
    EnemyInformation = 1,
}

/// <summary>Main menu database / bestiary page.</summary>
[DisallowMultipleComponent]
public sealed class DatabasePageUI : MonoBehaviour
{
    private const string EnemyDatabaseResourcePath = "Databases/EnemyDatabase";
    private const string ItemDatabaseResourcePath = "Databases/ItemDatabase";
    private const string WorldMapResourcePath = "Databases/WorldMap_Main";

    [Header("Left filter buttons")]
    [SerializeField] private Button generalButton;
    [SerializeField] private Button enemyButton;
    [SerializeField] private Button itemButton;

    [Header("Right panel")]
    [SerializeField] private TMP_Text headerLabel;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private ScrollRect contentScrollRect;

    [Header("Region filter (enemies only)")]
    [SerializeField] private GameObject regionsPanel;
    [SerializeField] private TMP_Dropdown regionDropdown;
    [SerializeField] private TMP_Text regionNameLabel;
    [SerializeField] private WorldMapDefinition worldMap;

    [Header("Enemy sub-tabs")]
    [Tooltip("Pinned above the scroll view (RightPanel/EnemyTabButtonRow).")]
    [SerializeField] private GameObject enemyTabButtonRow;
    [SerializeField] private Button generalInformationTabButton;
    [SerializeField] private Button enemyInformationTabButton;
    [SerializeField] private GameObject generalEnemyInformationContent;
    [Tooltip("Optional. Enemy rows spawn here. Auto-created under DatabaseContent when missing.")]
    [SerializeField] private RectTransform enemyListContentRoot;

    [Header("Map areas")]
    [Tooltip("Pinned above the scroll view (RightPanel/MapAreasTabButtonRow).")]
    [SerializeField] private GameObject mapAreasTabButtonRow;
    [SerializeField] private Button generalMapInformationTabButton;
    [SerializeField] private GameObject generalMapInformationContent;

    [Header("Item sub-tabs")]
    [Tooltip("Pinned above the scroll view (RightPanel/ItemTabButtonRow).")]
    [SerializeField] private GameObject itemTabButtonRow;
    [SerializeField] private Button resourceTabButton;
    [SerializeField] private Button equipmentTabButton;
    [SerializeField] private Button consumablesTabButton;
    [SerializeField] private Button enhancementTabButton;
    [SerializeField] private GameObject resourceContent;
    [SerializeField] private GameObject equipmentContent;
    [SerializeField] private GameObject consumablesContent;
    [SerializeField] private GameObject enhancementContent;

    [Header("Prefabs / data")]
    [Tooltip("Drag Assets/2.Prefabs/UI/DatabaseEnemyWindowEntryRow here.")]
    [SerializeField] private GameObject enemyRowPrefab;
    [Tooltip("Drag Assets/2.Prefabs/UI/DatabaseItemWindowEntryRow here.")]
    [SerializeField] private GameObject itemRowPrefab;
    [Tooltip("Drag Assets/2.Prefabs/UI/EnemyCombatProfileRow here.")]
    [SerializeField] private GameObject combatProfileRowPrefab;
    [SerializeField] private EnemyDatabase enemyDatabase;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private SharedTooltipUI sharedTooltip;

    [Header("Search")]
    [SerializeField] private GameObject searchPanel;
    [SerializeField] private TMP_Text searchLabelText;
    [SerializeField] private TMP_InputField searchField;

    [Header("Enemy sub-tab visuals")]
    [SerializeField] private Color activeEnemySubtabColor = new Color32(247, 225, 190, 255);
    [SerializeField] private Color inactiveEnemySubtabColor = new Color32(168, 152, 118, 200);

    private DatabaseSection _activeSection = DatabaseSection.Enemies;
    private string _searchQuery = string.Empty;
    private DatabaseEnemySubtab _activeEnemySubtab = DatabaseEnemySubtab.GeneralInformation;
    private DatabaseItemSubtab _activeItemSubtab = DatabaseItemSubtab.Resources;
    /// <summary>Runtime clone source — never parented under the live enemy list.</summary>
    private GameObject _enemyRowTemplate;
    private GameObject _itemRowTemplate;
    private readonly List<RegionDefinition> _databaseRegions = new();
    private RegionDefinition _selectedRegion;
    private Coroutine _lookupRoutine;

    private void Awake()
    {
        ResolveReferences();
        EnsureContentScrollConfigured();
        CacheEnemyRowTemplate();
        CacheItemRowTemplate();
        EnsureDatabases();
        WireFilterButtons();
    }

    private void OnEnable()
    {
        WireRegionDropdown();
        WireSearchField();
        RefreshActiveSection();
    }

    private void OnDisable()
    {
        if (regionDropdown)
            regionDropdown.onValueChanged.RemoveListener(OnRegionDropdownChanged);
        UnwireEnemySubtabButtons();
        UnwireItemSubtabButtons();
        UnwireSearchField();
        sharedTooltip?.Hide();
    }

    public void SelectSection(DatabaseSection section)
    {
        if (_activeSection == section)
        {
            RefreshActiveSection();
            return;
        }

        _activeSection = section;
        RefreshActiveSection();
    }

    /// <summary>Opens the Items section, correct sub-tab, and scrolls the matching row into view.</summary>
    public void LookupItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (_lookupRoutine != null)
            StopCoroutine(_lookupRoutine);

        _lookupRoutine = StartCoroutine(LookupItemRoutine(itemId));
    }

    private IEnumerator LookupItemRoutine(string itemId)
    {
        EnsureDatabases();
        sharedTooltip?.Hide();

        if (!DatabaseItemCatalog.TryResolveDatabaseLookup(
                itemDatabase,
                itemId,
                out ItemDefinition listItem,
                out DatabaseItemSubtab subtab))
        {
            GameLog.Add("This item is not listed in the database.", GameLog.CannotMessageColor);
            _lookupRoutine = null;
            yield break;
        }

        ClearSearchQuery();
        NavigateToItemSubtab(subtab);

        string targetItemId = listItem.itemId;
        yield return null;
        Canvas.ForceUpdateCanvases();
        RefreshScrollContentLayout();
        yield return null;

        RectTransform row = FindItemRowTransform(targetItemId);
        if (row)
            ScrollToCenterRow(row);

        _lookupRoutine = null;
    }

    private void NavigateToItemSubtab(DatabaseItemSubtab subtab)
    {
        bool sectionChanged = _activeSection != DatabaseSection.Items;
        bool subtabChanged = _activeItemSubtab != subtab;

        _activeSection = DatabaseSection.Items;
        _activeItemSubtab = subtab;

        if (!sectionChanged && !subtabChanged)
        {
            ApplyItemSubtab();
            return;
        }

        RefreshActiveSection();
    }

    private void ClearSearchQuery()
    {
        _searchQuery = string.Empty;
        if (searchField)
            searchField.SetTextWithoutNotify(string.Empty);
    }

    private RectTransform FindItemRowTransform(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        RectTransform listRoot = GetActiveItemListContentRoot();
        if (!listRoot)
            return null;

        for (int i = 0; i < listRoot.childCount; i++)
        {
            Transform child = listRoot.GetChild(i);
            if (!child)
                continue;

            DatabaseItemEntryRowUI row = child.GetComponent<DatabaseItemEntryRowUI>();
            if (row == null || string.IsNullOrWhiteSpace(row.ItemId))
                continue;

            if (string.Equals(row.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                return child as RectTransform;
        }

        return null;
    }

    private void ScrollToCenterRow(RectTransform row)
    {
        if (!contentScrollRect || !row || !contentScrollRect.content)
            return;

        RectTransform viewport = contentScrollRect.viewport;
        if (!viewport)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentScrollRect.content);

        Vector3[] rowCorners = new Vector3[4];
        Vector3[] contentCorners = new Vector3[4];
        row.GetWorldCorners(rowCorners);
        contentScrollRect.content.GetWorldCorners(contentCorners);

        float contentTop = contentCorners[1].y;
        float contentBottom = contentCorners[0].y;
        float contentHeight = contentTop - contentBottom;
        float viewportHeight = viewport.rect.height;
        float scrollable = contentHeight - viewportHeight;
        if (scrollable <= 0.01f)
            return;

        float rowCenterY = (rowCorners[0].y + rowCorners[1].y) * 0.5f;
        float rowCenterFromContentTop = contentTop - rowCenterY;
        float targetOffset = Mathf.Clamp(rowCenterFromContentTop - viewportHeight * 0.5f, 0f, scrollable);
        contentScrollRect.verticalNormalizedPosition = 1f - targetOffset / scrollable;
    }

    private void WireFilterButtons()
    {
        BindFilterButton(generalButton, DatabaseSection.General);
        BindFilterButton(enemyButton, DatabaseSection.Enemies);
        BindFilterButton(itemButton, DatabaseSection.Items);
    }

    private void BindFilterButton(Button button, DatabaseSection section)
    {
        if (!button)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectSection(section));
    }

    private void RefreshActiveSection()
    {
        ResolveReferences();

        if (headerLabel)
            headerLabel.text = GetSectionTitle(_activeSection);

        RefreshSectionButtonVisuals();

        bool showRegionFilter = _activeSection == DatabaseSection.Enemies;
        if (regionsPanel)
            regionsPanel.SetActive(showRegionFilter);

        if (showRegionFilter)
            RebuildRegionsDropdown();

        ClearContent();

        switch (_activeSection)
        {
            case DatabaseSection.Enemies:
                SetMapAreasSubsectionChromeVisible(false);
                SetItemSubsectionChromeVisible(false);
                SetEnemySubsectionChromeVisible(true);
                WireEnemySubtabButtons();
                ApplyEnemySubtab();
                break;
            case DatabaseSection.Items:
                SetMapAreasSubsectionChromeVisible(false);
                SetEnemySubsectionChromeVisible(false);
                SetItemSubsectionChromeVisible(true);
                WireItemSubtabButtons();
                ApplyItemSubtab();
                break;
            case DatabaseSection.General:
                SetEnemySubsectionChromeVisible(false);
                SetItemSubsectionChromeVisible(false);
                SetMapAreasSubsectionChromeVisible(true);
                ApplyMapAreasSection();
                break;
            default:
                SetEnemySubsectionChromeVisible(false);
                SetItemSubsectionChromeVisible(false);
                SetMapAreasSubsectionChromeVisible(false);
                break;
        }

        RefreshSearchPanelVisibility();
        ResetScrollPosition();
    }

    private void WireRegionDropdown()
    {
        if (!regionDropdown)
            return;

        regionDropdown.onValueChanged.RemoveListener(OnRegionDropdownChanged);
        regionDropdown.onValueChanged.AddListener(OnRegionDropdownChanged);
    }

    private void RebuildRegionsDropdown()
    {
        _databaseRegions.Clear();
        EnsureWorldMap();
        if (!worldMap)
            return;

        WorldMapProgressManager progress = FindProgressManager();
        string activeNodeId = ResolveActiveGameplayNodeId();
        _databaseRegions.AddRange(DatabaseRegionEnemyCatalog.GetListableRegions(worldMap, progress, activeNodeId));
        if (_databaseRegions.Count == 0)
            return;

        if (_selectedRegion == null || !_databaseRegions.Contains(_selectedRegion))
            _selectedRegion = DatabaseRegionEnemyCatalog.ResolveDefaultRegion(_databaseRegions, worldMap);

        if (!regionDropdown)
        {
            RefreshRegionCaption();
            return;
        }

        regionDropdown.ClearOptions();
        var options = new List<string>(_databaseRegions.Count);
        for (int i = 0; i < _databaseRegions.Count; i++)
            options.Add(_databaseRegions[i].displayName);
        regionDropdown.AddOptions(options);

        int idx = Mathf.Max(0, _databaseRegions.IndexOf(_selectedRegion));
        regionDropdown.SetValueWithoutNotify(idx);
        RefreshRegionCaption();
    }

    private void OnRegionDropdownChanged(int idx)
    {
        if (idx < 0 || idx >= _databaseRegions.Count)
            return;

        _selectedRegion = _databaseRegions[idx];
        RefreshRegionCaption();

        if (_activeSection != DatabaseSection.Enemies)
            return;

        if (_activeEnemySubtab != DatabaseEnemySubtab.EnemyInformation)
            return;

        ClearEnemyListRows();
        RebuildEnemyRows();
        ResetScrollPosition();
    }

    private void RefreshRegionCaption()
    {
        string caption = _selectedRegion ? _selectedRegion.displayName : string.Empty;
        if (regionDropdown && regionDropdown.captionText)
            regionDropdown.captionText.text = caption;
        if (regionNameLabel)
            regionNameLabel.text = caption;
    }

    private void RebuildEnemyRows()
    {
        RectTransform listRoot = EnsureEnemyListContentRoot();
        if (!listRoot)
            return;

        ClearEnemyListRows();

        if (!_enemyRowTemplate)
            CacheEnemyRowTemplate();

        if (!_enemyRowTemplate)
        {
            Debug.LogWarning(
                $"[{nameof(DatabasePageUI)}] Assign Enemy Row Prefab on DatabasePage.",
                this);
            return;
        }

        EnsureDatabases();
        if (enemyDatabase == null)
        {
            Debug.LogWarning($"[{nameof(DatabasePageUI)}] EnemyDatabase not found.", this);
            return;
        }

        sharedTooltip ??= FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);

        HashSet<string> allowedEnemyIds = null;
        if (_selectedRegion != null)
            allowedEnemyIds = DatabaseRegionEnemyCatalog.CollectEnemyIds(_selectedRegion);

        var enemies = enemyDatabase.GetAllSortedByDisplayName();
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyDefinition enemy = enemies[i];
            if (enemy == null)
                continue;

            if (allowedEnemyIds != null)
            {
                string enemyId = string.IsNullOrWhiteSpace(enemy.enemyId) ? string.Empty : enemy.enemyId.Trim();
                if (string.IsNullOrEmpty(enemyId) || !allowedEnemyIds.Contains(enemyId))
                    continue;
            }

            if (!EnemyPassesSearch(enemy, _selectedRegion))
                continue;

            GameObject rowGo = Instantiate(_enemyRowTemplate, listRoot);
            rowGo.SetActive(true);
            DatabaseEnemyEntryRowUI row = rowGo.GetComponent<DatabaseEnemyEntryRowUI>();
            if (!row)
                row = rowGo.AddComponent<DatabaseEnemyEntryRowUI>();
            row.Bind(enemy, sharedTooltip, worldMap, _selectedRegion);
        }

        RefreshScrollContentLayout();
    }

    private void CacheEnemyRowTemplate()
    {
        if (_enemyRowTemplate)
            return;

        if (enemyRowPrefab)
            _enemyRowTemplate = enemyRowPrefab;
    }

    private void CacheItemRowTemplate()
    {
        if (_itemRowTemplate)
            return;

        if (itemRowPrefab)
            _itemRowTemplate = itemRowPrefab;

#if UNITY_EDITOR
        if (!_itemRowTemplate)
        {
            const string prefabPath = "Assets/2.Prefabs/UI/DatabaseItemWindowEntryRow.prefab";
            _itemRowTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
#endif
    }

    private void ClearContent()
    {
        ClearEnemyListRows();

        if (_activeSection == DatabaseSection.Enemies ||
            _activeSection == DatabaseSection.Items ||
            _activeSection == DatabaseSection.General)
            return;

        if (!contentRoot)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (!child || IsPreservedDatabaseContentChild(child))
                continue;

            Destroy(child.gameObject);
        }
    }

    private void ClearEnemyListRows()
    {
        RectTransform listRoot = enemyListContentRoot;
        if (!listRoot && contentRoot)
            listRoot = contentRoot.Find("EnemyInformationContent") as RectTransform;

        if (!listRoot)
            return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);
    }

    private void WireEnemySubtabButtons()
    {
        if (generalInformationTabButton)
        {
            generalInformationTabButton.onClick.RemoveListener(ShowGeneralEnemyInformation);
            generalInformationTabButton.onClick.AddListener(ShowGeneralEnemyInformation);
        }

        if (enemyInformationTabButton)
        {
            enemyInformationTabButton.onClick.RemoveListener(ShowEnemyInformation);
            enemyInformationTabButton.onClick.AddListener(ShowEnemyInformation);
        }
    }

    private void UnwireEnemySubtabButtons()
    {
        if (generalInformationTabButton)
            generalInformationTabButton.onClick.RemoveListener(ShowGeneralEnemyInformation);
        if (enemyInformationTabButton)
            enemyInformationTabButton.onClick.RemoveListener(ShowEnemyInformation);
    }

    private void ShowGeneralEnemyInformation()
    {
        if (_activeEnemySubtab == DatabaseEnemySubtab.GeneralInformation)
            return;

        _activeEnemySubtab = DatabaseEnemySubtab.GeneralInformation;
        sharedTooltip?.Hide();
        ApplyEnemySubtab();
    }

    private void ShowEnemyInformation()
    {
        if (_activeEnemySubtab == DatabaseEnemySubtab.EnemyInformation)
            return;

        _activeEnemySubtab = DatabaseEnemySubtab.EnemyInformation;
        ApplyEnemySubtab();
    }

    private void ApplyEnemySubtab()
    {
        bool showGeneral = _activeEnemySubtab == DatabaseEnemySubtab.GeneralInformation;

        if (generalEnemyInformationContent)
            generalEnemyInformationContent.SetActive(showGeneral);

        if (generalMapInformationContent)
            generalMapInformationContent.SetActive(false);

        RectTransform listRoot = EnsureEnemyListContentRoot();
        if (listRoot)
            listRoot.gameObject.SetActive(!showGeneral);

        RefreshEnemySubtabButtonVisuals();

        if (showGeneral)
        {
            ClearEnemyListRows();
            sharedTooltip?.Hide();
            RefreshGeneralEnemyInformationContent();
        }
        else
        {
            RebuildEnemyRows();
        }

        RefreshSearchPanelVisibility();
        RefreshScrollContentLayout();
    }

    private void RefreshGeneralEnemyInformationContent()
    {
        if (!generalEnemyInformationContent)
            return;

        DatabaseGeneralEnemyInformationUI binder =
            generalEnemyInformationContent.GetComponent<DatabaseGeneralEnemyInformationUI>();
        if (!binder)
            binder = generalEnemyInformationContent.AddComponent<DatabaseGeneralEnemyInformationUI>();

        if (!combatProfileRowPrefab)
            combatProfileRowPrefab = ResolveCombatProfileRowPrefab();

        binder.RebuildCombatProfileRows();
    }

    internal static GameObject ResolveCombatProfileRowPrefab()
    {
#if UNITY_EDITOR
        const string prefabPath = "Assets/2.Prefabs/UI/EnemyCombatProfileRow.prefab";
        GameObject fromAssetDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (fromAssetDatabase)
            return fromAssetDatabase;
#endif

        DatabasePageUI[] pages = FindObjectsByType<DatabasePageUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < pages.Length; i++)
        {
            DatabasePageUI page = pages[i];
            if (page != null && page.combatProfileRowPrefab)
                return page.combatProfileRowPrefab;
        }

        return null;
    }

    private void RefreshSectionButtonVisuals()
    {
        ApplySectionButtonVisual(generalButton, _activeSection == DatabaseSection.General);
        ApplySectionButtonVisual(enemyButton, _activeSection == DatabaseSection.Enemies);
        ApplySectionButtonVisual(itemButton, _activeSection == DatabaseSection.Items);
    }

    private void RefreshEnemySubtabButtonVisuals()
    {
        ApplySectionButtonVisual(generalInformationTabButton, _activeEnemySubtab == DatabaseEnemySubtab.GeneralInformation);
        ApplySectionButtonVisual(enemyInformationTabButton, _activeEnemySubtab == DatabaseEnemySubtab.EnemyInformation);
    }

    private void ApplySectionButtonVisual(Button button, bool isActive)
    {
        if (!button)
            return;

        if (button.TryGetComponent(out Image image))
            image.color = isActive ? activeEnemySubtabColor : inactiveEnemySubtabColor;
    }

    private void ApplyMapAreasSection()
    {
        if (generalEnemyInformationContent)
            generalEnemyInformationContent.SetActive(false);

        if (enemyListContentRoot)
            enemyListContentRoot.gameObject.SetActive(false);

        ClearEnemyListRows();
        SetAllItemContentPanelsActive(false);

        if (generalMapInformationContent)
            generalMapInformationContent.SetActive(true);

        RefreshMapAreasTabButtonVisuals();
        RefreshSearchPanelVisibility();
        RefreshScrollContentLayout();
    }

    private void SetMapAreasSubsectionChromeVisible(bool visible)
    {
        if (mapAreasTabButtonRow)
            mapAreasTabButtonRow.SetActive(visible);

        if (!visible && generalMapInformationContent)
            generalMapInformationContent.SetActive(false);
    }

    private void RefreshMapAreasTabButtonVisuals()
    {
        ApplySectionButtonVisual(generalMapInformationTabButton, true);
    }

    private void SetEnemySubsectionChromeVisible(bool visible)
    {
        if (enemyTabButtonRow)
            enemyTabButtonRow.SetActive(visible);

        if (visible)
            return;

        if (generalEnemyInformationContent)
            generalEnemyInformationContent.SetActive(false);

        if (enemyListContentRoot)
            enemyListContentRoot.gameObject.SetActive(false);

        ClearEnemyListRows();
    }

    private void WireItemSubtabButtons()
    {
        BindItemSubtabButton(resourceTabButton, ShowResourceItems);
        BindItemSubtabButton(equipmentTabButton, ShowEquipmentItems);
        BindItemSubtabButton(consumablesTabButton, ShowConsumableItems);
        BindItemSubtabButton(enhancementTabButton, ShowEnhancementItems);
    }

    private void UnwireItemSubtabButtons()
    {
        UnbindItemSubtabButton(resourceTabButton, ShowResourceItems);
        UnbindItemSubtabButton(equipmentTabButton, ShowEquipmentItems);
        UnbindItemSubtabButton(consumablesTabButton, ShowConsumableItems);
        UnbindItemSubtabButton(enhancementTabButton, ShowEnhancementItems);
    }

    private static void BindItemSubtabButton(Button button, UnityEngine.Events.UnityAction handler)
    {
        if (!button)
            return;

        button.onClick.RemoveListener(handler);
        button.onClick.AddListener(handler);
    }

    private static void UnbindItemSubtabButton(Button button, UnityEngine.Events.UnityAction handler)
    {
        if (button)
            button.onClick.RemoveListener(handler);
    }

    private void ShowResourceItems() => SelectItemSubtab(DatabaseItemSubtab.Resources);
    private void ShowEquipmentItems() => SelectItemSubtab(DatabaseItemSubtab.Equipment);
    private void ShowConsumableItems() => SelectItemSubtab(DatabaseItemSubtab.Consumables);
    private void ShowEnhancementItems() => SelectItemSubtab(DatabaseItemSubtab.Enhancement);

    private void SelectItemSubtab(DatabaseItemSubtab subtab)
    {
        if (_activeItemSubtab == subtab)
        {
            ApplyItemSubtab();
            return;
        }

        _activeItemSubtab = subtab;
        sharedTooltip?.Hide();
        ApplyItemSubtab();
    }

    private void ApplyItemSubtab()
    {
        bool showResources = _activeItemSubtab == DatabaseItemSubtab.Resources;
        bool showEquipment = _activeItemSubtab == DatabaseItemSubtab.Equipment;
        bool showConsumables = _activeItemSubtab == DatabaseItemSubtab.Consumables;
        bool showEnhancement = _activeItemSubtab == DatabaseItemSubtab.Enhancement;

        if (resourceContent)
            resourceContent.SetActive(showResources);
        if (equipmentContent)
            equipmentContent.SetActive(showEquipment);
        if (consumablesContent)
            consumablesContent.SetActive(showConsumables);
        if (enhancementContent)
            enhancementContent.SetActive(showEnhancement);

        if (generalEnemyInformationContent)
            generalEnemyInformationContent.SetActive(false);

        if (generalMapInformationContent)
            generalMapInformationContent.SetActive(false);

        if (enemyListContentRoot)
            enemyListContentRoot.gameObject.SetActive(false);

        ClearEnemyListRows();
        ClearInactiveItemListRows();
        RebuildItemRows();
        RefreshItemSubtabButtonVisuals();
        RefreshSearchPanelVisibility();
        RefreshScrollContentLayout();
    }

    private void SetItemSubsectionChromeVisible(bool visible)
    {
        if (itemTabButtonRow)
            itemTabButtonRow.SetActive(visible);

        if (!visible)
        {
            SetAllItemContentPanelsActive(false);
            ClearItemListRows();
        }
    }

    private void RebuildItemRows()
    {
        RectTransform listRoot = GetActiveItemListContentRoot();
        if (!listRoot)
            return;

        ClearItemListRows(listRoot);

        if (!_itemRowTemplate)
            CacheItemRowTemplate();

        if (!_itemRowTemplate)
        {
            Debug.LogWarning($"[{nameof(DatabasePageUI)}] Assign Item Row Prefab on DatabasePage.", this);
            return;
        }

        EnsureDatabases();
        if (itemDatabase == null)
        {
            Debug.LogWarning($"[{nameof(DatabasePageUI)}] ItemDatabase not found.", this);
            return;
        }

        sharedTooltip ??= FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
        DatabaseItemSourceCatalog.Invalidate();

        List<ItemDefinition> items = DatabaseItemCatalog.CollectForSubtab(itemDatabase.GetAll(), _activeItemSubtab);
        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            if (!item)
                continue;

            if (!ItemPassesSearch(item, _activeItemSubtab))
                continue;

            GameObject rowGo = Instantiate(_itemRowTemplate, listRoot);
            rowGo.SetActive(true);
            DatabaseItemEntryRowUI row = rowGo.GetComponent<DatabaseItemEntryRowUI>();
            if (!row)
            {
                if (rowGo.TryGetComponent(out DatabaseEnemyEntryRowUI wrongRow))
                    Destroy(wrongRow);
                row = rowGo.AddComponent<DatabaseItemEntryRowUI>();
            }

            row.Bind(item, sharedTooltip, _activeItemSubtab);
        }

        EnsureItemListContentLayout(listRoot);
    }

    private RectTransform GetActiveItemListContentRoot()
    {
        GameObject panel = _activeItemSubtab switch
        {
            DatabaseItemSubtab.Resources => resourceContent,
            DatabaseItemSubtab.Equipment => equipmentContent,
            DatabaseItemSubtab.Consumables => consumablesContent,
            DatabaseItemSubtab.Enhancement => enhancementContent,
            _ => resourceContent,
        };

        return panel != null ? panel.transform as RectTransform : null;
    }

    private void ClearInactiveItemListRows()
    {
        RectTransform active = GetActiveItemListContentRoot();
        TryClearItemListIfInactive(resourceContent, active);
        TryClearItemListIfInactive(equipmentContent, active);
        TryClearItemListIfInactive(consumablesContent, active);
        TryClearItemListIfInactive(enhancementContent, active);
    }

    private static void TryClearItemListIfInactive(GameObject panel, RectTransform activeRoot)
    {
        if (!panel)
            return;

        RectTransform rect = panel.transform as RectTransform;
        if (!rect || rect == activeRoot)
            return;

        ClearItemListRows(rect);
    }

    private void ClearItemListRows()
    {
        ClearItemListRows(GetActiveItemListContentRoot());
    }

    private static void ClearItemListRows(RectTransform listRoot)
    {
        if (!listRoot)
            return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);
    }

    private static void EnsureItemListContentLayout(RectTransform listRoot)
    {
        if (!listRoot)
            return;

        VerticalLayoutGroup layout = listRoot.GetComponent<VerticalLayoutGroup>();
        if (!layout)
            layout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        ContentSizeFitter fitter = listRoot.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        PrepareLayoutChild(listRoot);
    }

    private void RefreshItemSubtabButtonVisuals()
    {
        ApplySectionButtonVisual(resourceTabButton, _activeItemSubtab == DatabaseItemSubtab.Resources);
        ApplySectionButtonVisual(equipmentTabButton, _activeItemSubtab == DatabaseItemSubtab.Equipment);
        ApplySectionButtonVisual(consumablesTabButton, _activeItemSubtab == DatabaseItemSubtab.Consumables);
        ApplySectionButtonVisual(enhancementTabButton, _activeItemSubtab == DatabaseItemSubtab.Enhancement);
    }

    private RectTransform EnsureEnemyListContentRoot()
    {
        if (enemyListContentRoot)
            return enemyListContentRoot;

        if (!contentRoot)
            return null;

        Transform existing = contentRoot.Find("EnemyInformationContent");
        if (existing != null)
        {
            enemyListContentRoot = existing as RectTransform;
            return enemyListContentRoot;
        }

        var go = new GameObject("EnemyInformationContent", typeof(RectTransform));
        enemyListContentRoot = go.GetComponent<RectTransform>();
        enemyListContentRoot.SetParent(contentRoot, false);
        ConfigureEnemyListContentRoot(enemyListContentRoot);

        enemyListContentRoot.SetSiblingIndex(
            generalEnemyInformationContent != null
                ? generalEnemyInformationContent.transform.GetSiblingIndex() + 1
                : contentRoot.childCount - 1);

        ConfigureSubtabPanelLayout(enemyListContentRoot.gameObject);
        return enemyListContentRoot;
    }

    private static void ConfigureEnemyListContentRoot(RectTransform root)
    {
        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static bool IsPreservedDatabaseContentChild(Transform child)
    {
        if (!child)
            return false;

        return child.name switch
        {
            "GeneralEnemyInformationContent" => true,
            "EnemyInformationContent" => true,
            "ResourceContent" => true,
            "EquipmentContent" => true,
            "ConsumablesContent" => true,
            "EnhancementContent" => true,
            "GeneralMapInformationContent" => true,
            _ => false,
        };
    }

    private void SetAllItemContentPanelsActive(bool active)
    {
        if (resourceContent)
            resourceContent.SetActive(active);
        if (equipmentContent)
            equipmentContent.SetActive(active);
        if (consumablesContent)
            consumablesContent.SetActive(active);
        if (enhancementContent)
            enhancementContent.SetActive(active);
    }

    private void ResetScrollPosition()
    {
        RefreshScrollContentLayout();
    }

    private void RefreshScrollContentLayout()
    {
        if (!contentRoot)
            return;

        if (UsesEnemySubtabs())
            EnsureEnemySubtabScrollLayout();
        else if (UsesItemSubtabs())
            EnsureItemSubtabScrollLayout();
        else if (UsesMapAreasSection())
            EnsureMapAreasScrollLayout();

        Canvas.ForceUpdateCanvases();

        if (enemyListContentRoot && enemyListContentRoot.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyListContentRoot);

        if (generalEnemyInformationContent && generalEnemyInformationContent.activeInHierarchy)
        {
            RectTransform generalRect = generalEnemyInformationContent.transform as RectTransform;
            if (generalRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(generalRect);
        }

        if (generalMapInformationContent && generalMapInformationContent.activeInHierarchy)
        {
            RectTransform mapRect = generalMapInformationContent.transform as RectTransform;
            if (mapRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(mapRect);
        }

        RectTransform activeItemList = GetActiveItemListContentRoot();
        if (activeItemList && activeItemList.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeItemList);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        Canvas.ForceUpdateCanvases();

        if (contentScrollRect)
            contentScrollRect.verticalNormalizedPosition = 1f;
    }

    private bool UsesEnemySubtabs()
    {
        if (_activeSection != DatabaseSection.Enemies)
            return false;

        if (enemyTabButtonRow != null)
            return true;

        return contentRoot != null && contentRoot.Find("GeneralEnemyInformationContent") != null;
    }

    private bool UsesItemSubtabs()
    {
        if (_activeSection != DatabaseSection.Items)
            return false;

        if (itemTabButtonRow != null)
            return true;

        return contentRoot != null && contentRoot.Find("ResourceContent") != null;
    }

    private bool UsesMapAreasSection()
    {
        if (_activeSection != DatabaseSection.General)
            return false;

        if (mapAreasTabButtonRow != null)
            return true;

        return contentRoot != null && contentRoot.Find("GeneralMapInformationContent") != null;
    }

    private void EnsureEnemySubtabScrollLayout()
    {
        if (!contentRoot)
            return;

        VerticalLayoutGroup rootLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (!rootLayout)
            rootLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.enabled = true;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.spacing = 0f;
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;

        ContentSizeFitter rootFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (!rootFitter)
            rootFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        rootFitter.enabled = true;
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;

        ConfigureSubtabPanelLayout(generalEnemyInformationContent);

        RectTransform listRoot = enemyListContentRoot;
        if (!listRoot)
            listRoot = contentRoot.Find("EnemyInformationContent") as RectTransform;
        if (listRoot)
            ConfigureSubtabPanelLayout(listRoot.gameObject);
    }

    private void EnsureItemSubtabScrollLayout()
    {
        if (!contentRoot)
            return;

        VerticalLayoutGroup rootLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (!rootLayout)
            rootLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.enabled = true;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.spacing = 0f;
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;

        ContentSizeFitter rootFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (!rootFitter)
            rootFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        rootFitter.enabled = true;
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;

        ConfigureSubtabPanelLayout(resourceContent);
        ConfigureSubtabPanelLayout(equipmentContent);
        ConfigureSubtabPanelLayout(consumablesContent);
        ConfigureSubtabPanelLayout(enhancementContent);
    }

    private void EnsureMapAreasScrollLayout()
    {
        if (!contentRoot)
            return;

        VerticalLayoutGroup rootLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (!rootLayout)
            rootLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.enabled = true;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.spacing = 0f;
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;

        ContentSizeFitter rootFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (!rootFitter)
            rootFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        rootFitter.enabled = true;
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;

        ConfigureSubtabPanelLayout(generalMapInformationContent);
    }

    private static void ConfigureSubtabPanelLayout(GameObject panel)
    {
        if (!panel)
            return;

        PrepareLayoutChild(panel.transform as RectTransform);

        LayoutElement layoutElement = panel.GetComponent<LayoutElement>();
        if (!layoutElement)
            layoutElement = panel.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
        layoutElement.minHeight = 0f;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.enabled = true;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void PrepareLayoutChild(RectTransform rect)
    {
        if (!rect)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
    }

    private void EnsureContentScrollConfigured()
    {
        if (!contentScrollRect)
            return;

        contentScrollRect.horizontal = false;
        contentScrollRect.vertical = true;
        contentScrollRect.movementType = ScrollRect.MovementType.Clamped;

        if (contentRoot != null)
            contentScrollRect.content = contentRoot;

        RectTransform viewport = contentScrollRect.viewport;
        if (viewport)
        {
            if (!viewport.TryGetComponent(out RectMask2D _))
                viewport.gameObject.AddComponent<RectMask2D>();

            Graphic viewportGraphic = viewport.GetComponent<Graphic>();
            if (viewportGraphic == null)
            {
                var image = viewport.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;
            }
            else
            {
                viewportGraphic.raycastTarget = true;
            }
        }

        if (!contentRoot)
            return;

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = new Vector2(0f, contentRoot.sizeDelta.y);

        if (UsesEnemySubtabs())
        {
            EnsureEnemySubtabScrollLayout();
            return;
        }

        if (UsesItemSubtabs())
        {
            EnsureItemSubtabScrollLayout();
            return;
        }

        if (UsesMapAreasSection())
        {
            EnsureMapAreasScrollLayout();
            return;
        }

        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (!layout)
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.enabled = true;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureDatabases()
    {
        if (!enemyDatabase)
            enemyDatabase = Resources.Load<EnemyDatabase>(EnemyDatabaseResourcePath);
        if (!itemDatabase)
            itemDatabase = Resources.Load<ItemDatabase>(ItemDatabaseResourcePath);
        EnsureWorldMap();
    }

    private void EnsureWorldMap()
    {
        if (worldMap)
            return;

        WorldMapProgressManager progress = FindProgressManager();
        if (progress != null)
            worldMap = progress.WorldMap;

        if (!worldMap)
            worldMap = Resources.Load<WorldMapDefinition>(WorldMapResourcePath);
    }

    private static WorldMapProgressManager FindProgressManager()
    {
        WorldMapProgressManager progress = WorldMapProgressManager.Instance;
        if (progress != null)
            return progress;

        return FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
    }

    private static string ResolveActiveGameplayNodeId()
    {
        if (GameplayLevelBootstrapper.Instance != null && GameplayLevelBootstrapper.Instance.ActiveDefinition != null)
            return GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId;
        if (ActiveLevelContext.Current != null)
            return ActiveLevelContext.Current.nodeId;
        return null;
    }

    private void WireSearchField()
    {
        ResolveSearchReferences();

        if (!searchField)
            return;

        searchField.onValueChanged.RemoveListener(OnSearchQueryChanged);
        searchField.onValueChanged.AddListener(OnSearchQueryChanged);
        _searchQuery = searchField.text ?? string.Empty;
    }

    private void UnwireSearchField()
    {
        if (searchField)
            searchField.onValueChanged.RemoveListener(OnSearchQueryChanged);
    }

    private void OnSearchQueryChanged(string value)
    {
        _searchQuery = value ?? string.Empty;
        RefreshSearchResults();
    }

    private void RefreshSearchResults()
    {
        if (_activeSection == DatabaseSection.Enemies &&
            _activeEnemySubtab == DatabaseEnemySubtab.EnemyInformation)
        {
            RebuildEnemyRows();
        }
        else if (_activeSection == DatabaseSection.Items)
        {
            RebuildItemRows();
        }

        ResetScrollPosition();
    }

    private bool ShouldShowSearchPanel()
    {
        if (_activeSection == DatabaseSection.Items)
            return true;

        return _activeSection == DatabaseSection.Enemies &&
               _activeEnemySubtab == DatabaseEnemySubtab.EnemyInformation;
    }

    private void RefreshSearchPanelVisibility()
    {
        ResolveSearchReferences();

        bool show = ShouldShowSearchPanel();
        if (searchLabelText)
            searchLabelText.gameObject.SetActive(show);
        if (searchField)
            searchField.gameObject.SetActive(show);

        if (show)
            return;

        _searchQuery = string.Empty;
        if (searchField)
            searchField.SetTextWithoutNotify(string.Empty);
    }

    private void ResolveSearchReferences()
    {
        Transform sectionsGroup = transform.Find("SectionsGroup");
        Transform rightPanel = sectionsGroup != null ? sectionsGroup.Find("RightPanel") : null;
        if (!rightPanel)
            return;

        if (!searchPanel)
            searchPanel = rightPanel.Find("SearchPanel")?.gameObject;

        Transform searchRoot = searchPanel != null ? searchPanel.transform : rightPanel.Find("SearchPanel");
        if (!searchRoot)
            return;

        if (!searchLabelText)
            searchLabelText = searchRoot.Find("SearchLabelText")?.GetComponent<TMP_Text>();
        if (!searchField)
            searchField = searchRoot.Find("SearchField")?.GetComponent<TMP_InputField>();
    }

    private bool EnemyPassesSearch(EnemyDefinition enemy, RegionDefinition regionFilter)
    {
        if (enemy == null || string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        string query = _searchQuery.Trim();
        if (string.IsNullOrEmpty(query))
            return true;

        return BuildEnemySearchHaystack(enemy, regionFilter)
            .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool ItemPassesSearch(ItemDefinition item, DatabaseItemSubtab subtab)
    {
        if (item == null || string.IsNullOrWhiteSpace(_searchQuery))
            return true;

        string query = _searchQuery.Trim();
        if (string.IsNullOrEmpty(query))
            return true;

        return BuildItemSearchHaystack(item, subtab)
            .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string BuildEnemySearchHaystack(EnemyDefinition enemy, RegionDefinition regionFilter)
    {
        if (!enemy)
            return string.Empty;

        string enemyId = string.IsNullOrWhiteSpace(enemy.enemyId) ? string.Empty : enemy.enemyId.Trim();
        string locations = string.IsNullOrEmpty(enemyId)
            ? string.Empty
            : DatabaseRegionEnemyCatalog.FormatMapLocationsForEnemy(enemyId, worldMap, regionFilter);

        return $"{enemy.displayName} {enemyId} {locations}";
    }

    private string BuildItemSearchHaystack(ItemDefinition item, DatabaseItemSubtab subtab)
    {
        if (!item)
            return string.Empty;

        string displayName = DatabaseItemCatalog.FormatDatabaseDisplayName(item);
        string locations = DatabaseItemCatalog.FormatDatabaseObtainLocations(item, subtab);
        string itemId = item.itemId ?? string.Empty;
        return $"{displayName} {item.displayName} {itemId} {locations}";
    }

    private void ResolveReferences()
    {
        Transform sectionsGroup = transform.Find("SectionsGroup");
        Transform leftPanel = sectionsGroup != null ? sectionsGroup.Find("LeftPanel") : null;
        Transform rightPanel = sectionsGroup != null ? sectionsGroup.Find("RightPanel") : null;

        if (leftPanel != null)
        {
            Transform listContent = leftPanel.Find("ListScrollView/Viewport/ListContent");
            if (!generalButton)
                generalButton = FindChildButton(listContent, "GeneralButton");
            if (!enemyButton)
                enemyButton = FindChildButton(listContent, "EnemyButton");
            if (!itemButton)
                itemButton = FindChildButton(listContent, "ItemButton");
        }

        if (!headerLabel && rightPanel != null)
            headerLabel = rightPanel.Find("HeaderLabel")?.GetComponent<TMP_Text>();

        if (!contentScrollRect && rightPanel != null)
            contentScrollRect = rightPanel.Find("ScrollView")?.GetComponent<ScrollRect>();

        if (!contentRoot)
        {
            if (contentScrollRect != null && contentScrollRect.content != null)
                contentRoot = contentScrollRect.content;
            else if (rightPanel != null)
                contentRoot = rightPanel.Find("ScrollView/Viewport/DatabaseContent") as RectTransform;
        }

        if (!regionsPanel && rightPanel != null)
            regionsPanel = rightPanel.Find("RegionsPanel")?.gameObject;

        if (regionsPanel != null)
        {
            if (!regionDropdown)
                regionDropdown = regionsPanel.transform.Find("Dropdown")?.GetComponent<TMP_Dropdown>();
            if (!regionNameLabel)
                regionNameLabel = regionsPanel.transform.Find("RegionLabel")?.GetComponent<TMP_Text>();
        }

        if (!enemyTabButtonRow && rightPanel != null)
            enemyTabButtonRow = rightPanel.Find("EnemyTabButtonRow")?.gameObject;

        if (contentRoot != null)
        {
            if (!generalEnemyInformationContent)
                generalEnemyInformationContent = contentRoot.Find("GeneralEnemyInformationContent")?.gameObject;
            if (!enemyListContentRoot)
                enemyListContentRoot = contentRoot.Find("EnemyInformationContent") as RectTransform;
        }

        Transform tabRow = enemyTabButtonRow != null
            ? enemyTabButtonRow.transform
            : rightPanel != null ? rightPanel.Find("EnemyTabButtonRow") : null;
        if (tabRow != null)
        {
            if (!enemyTabButtonRow)
                enemyTabButtonRow = tabRow.gameObject;
            if (!generalInformationTabButton)
                generalInformationTabButton = tabRow.Find("GeneralButton")?.GetComponent<Button>();
            if (!enemyInformationTabButton)
                enemyInformationTabButton = tabRow.Find("EnemyInformationButton")?.GetComponent<Button>();
        }

        if (!mapAreasTabButtonRow && rightPanel != null)
            mapAreasTabButtonRow = rightPanel.Find("MapAreasTabButtonRow")?.gameObject;

        if (contentRoot != null && !generalMapInformationContent)
            generalMapInformationContent = contentRoot.Find("GeneralMapInformationContent")?.gameObject;

        Transform mapAreasTabRow = mapAreasTabButtonRow != null
            ? mapAreasTabButtonRow.transform
            : rightPanel != null ? rightPanel.Find("MapAreasTabButtonRow") : null;
        if (mapAreasTabRow != null)
        {
            if (!mapAreasTabButtonRow)
                mapAreasTabButtonRow = mapAreasTabRow.gameObject;
            if (!generalMapInformationTabButton)
            {
                generalMapInformationTabButton = mapAreasTabRow.Find("GeneralButton")?.GetComponent<Button>();
                if (!generalMapInformationTabButton)
                    generalMapInformationTabButton = mapAreasTabRow.Find("GeneralMapInformationButton")?.GetComponent<Button>();
            }
        }

        if (!itemTabButtonRow && rightPanel != null)
            itemTabButtonRow = rightPanel.Find("ItemTabButtonRow")?.gameObject;

        Transform itemTabRow = itemTabButtonRow != null
            ? itemTabButtonRow.transform
            : rightPanel != null ? rightPanel.Find("ItemTabButtonRow") : null;
        if (itemTabRow != null)
        {
            if (!itemTabButtonRow)
                itemTabButtonRow = itemTabRow.gameObject;
            if (!resourceTabButton)
                resourceTabButton = itemTabRow.Find("ResourceButton")?.GetComponent<Button>();
            if (!equipmentTabButton)
                equipmentTabButton = itemTabRow.Find("EquipmentButton")?.GetComponent<Button>();
            if (!consumablesTabButton)
                consumablesTabButton = itemTabRow.Find("ConsumablesButton")?.GetComponent<Button>();
            if (!enhancementTabButton)
            {
                enhancementTabButton = itemTabRow.Find("EnhancementButton")?.GetComponent<Button>();
                if (!enhancementTabButton)
                    enhancementTabButton = itemTabRow.Find("EnhancementButton ")?.GetComponent<Button>();
            }
        }

        if (contentRoot != null)
        {
            if (!resourceContent)
                resourceContent = contentRoot.Find("ResourceContent")?.gameObject;
            if (!equipmentContent)
                equipmentContent = contentRoot.Find("EquipmentContent")?.gameObject;
            if (!consumablesContent)
                consumablesContent = contentRoot.Find("ConsumablesContent")?.gameObject;
            if (!enhancementContent)
                enhancementContent = contentRoot.Find("EnhancementContent")?.gameObject;
        }

        ResolveSearchReferences();
    }

    private static Button FindChildButton(Transform parent, string childName)
    {
        if (!parent || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static string GetSectionTitle(DatabaseSection section)
    {
        return section switch
        {
            DatabaseSection.General => "Map areas",
            DatabaseSection.Enemies => "Enemies",
            DatabaseSection.Items => "Items",
            _ => section.ToString(),
        };
    }
}
