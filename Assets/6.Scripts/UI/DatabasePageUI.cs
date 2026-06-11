using System;
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

    [Header("Prefabs / data")]
    [Tooltip("Drag Assets/2.Prefabs/UI/DatabaseEnemyWindowEntryRow here.")]
    [SerializeField] private GameObject enemyRowPrefab;
    [Tooltip("Drag Assets/2.Prefabs/UI/EnemyCombatProfileRow here.")]
    [SerializeField] private GameObject combatProfileRowPrefab;
    [SerializeField] private EnemyDatabase enemyDatabase;
    [SerializeField] private SharedTooltipUI sharedTooltip;

    [Header("Enemy sub-tab visuals")]
    [SerializeField] private Color activeEnemySubtabColor = new Color32(247, 225, 190, 255);
    [SerializeField] private Color inactiveEnemySubtabColor = new Color32(168, 152, 118, 200);

    private DatabaseSection _activeSection = DatabaseSection.Enemies;
    private DatabaseEnemySubtab _activeEnemySubtab = DatabaseEnemySubtab.GeneralInformation;
    /// <summary>Runtime clone source — never parented under the live enemy list.</summary>
    private GameObject _enemyRowTemplate;
    private readonly List<RegionDefinition> _databaseRegions = new();
    private RegionDefinition _selectedRegion;

    private void Awake()
    {
        ResolveReferences();
        EnsureContentScrollConfigured();
        CacheEnemyRowTemplate();
        EnsureDatabases();
        WireFilterButtons();
    }

    private void OnEnable()
    {
        WireRegionDropdown();
        RefreshActiveSection();
    }

    private void OnDisable()
    {
        if (regionDropdown)
            regionDropdown.onValueChanged.RemoveListener(OnRegionDropdownChanged);
        UnwireEnemySubtabButtons();
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

        bool showRegionFilter = _activeSection == DatabaseSection.Enemies;
        if (regionsPanel)
            regionsPanel.SetActive(showRegionFilter);

        if (showRegionFilter)
            RebuildRegionsDropdown();

        ClearContent();

        switch (_activeSection)
        {
            case DatabaseSection.Enemies:
                SetEnemySubsectionChromeVisible(true);
                WireEnemySubtabButtons();
                ApplyEnemySubtab();
                break;
            default:
                SetEnemySubsectionChromeVisible(false);
                break;
        }

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

    private void ClearContent()
    {
        ClearEnemyListRows();

        if (_activeSection == DatabaseSection.Enemies)
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

    private void RefreshEnemySubtabButtonVisuals()
    {
        ApplyEnemySubtabButtonVisual(generalInformationTabButton, _activeEnemySubtab == DatabaseEnemySubtab.GeneralInformation);
        ApplyEnemySubtabButtonVisual(enemyInformationTabButton, _activeEnemySubtab == DatabaseEnemySubtab.EnemyInformation);
    }

    private void ApplyEnemySubtabButtonVisual(Button button, bool isActive)
    {
        if (!button)
            return;

        if (button.TryGetComponent(out Image image))
            image.color = isActive ? activeEnemySubtabColor : inactiveEnemySubtabColor;
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
            _ => false,
        };
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

        Canvas.ForceUpdateCanvases();

        if (enemyListContentRoot && enemyListContentRoot.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyListContentRoot);

        if (generalEnemyInformationContent && generalEnemyInformationContent.activeInHierarchy)
        {
            RectTransform generalRect = generalEnemyInformationContent.transform as RectTransform;
            if (generalRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(generalRect);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        Canvas.ForceUpdateCanvases();

        if (contentScrollRect)
            contentScrollRect.verticalNormalizedPosition = 1f;
    }

    private bool UsesEnemySubtabs()
    {
        if (enemyTabButtonRow != null)
            return true;

        return contentRoot != null && contentRoot.Find("GeneralEnemyInformationContent") != null;
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
        rect.pivot = new Vector2(0.5f, 1f);
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
            DatabaseSection.General => "General",
            DatabaseSection.Enemies => "Enemies",
            DatabaseSection.Items => "Items",
            _ => section.ToString(),
        };
    }
}
