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

    [Header("Prefabs / data")]
    [Tooltip("Drag Assets/2.Prefabs/UI/DatabaseEnemyWindowEntryRow here. Optional if a row already sits under DatabaseContent.")]
    [SerializeField] private GameObject enemyRowPrefab;
    [SerializeField] private EnemyDatabase enemyDatabase;
    [SerializeField] private SharedTooltipUI sharedTooltip;

    private DatabaseSection _activeSection = DatabaseSection.Enemies;
    /// <summary>Runtime clone source — never a child of <see cref="contentRoot"/> (that list is cleared on refresh).</summary>
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
                RebuildEnemyRows();
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

        ClearContent();
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
        if (!contentRoot)
            return;

        if (!_enemyRowTemplate)
            CacheEnemyRowTemplate();

        if (!_enemyRowTemplate)
        {
            Debug.LogWarning(
                $"[{nameof(DatabasePageUI)}] Assign Enemy Row Prefab on DatabasePage, or place one sample row under DatabaseContent.",
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

            GameObject rowGo = Instantiate(_enemyRowTemplate, contentRoot);
            rowGo.SetActive(true);
            DatabaseEnemyEntryRowUI row = rowGo.GetComponent<DatabaseEnemyEntryRowUI>();
            if (!row)
                row = rowGo.AddComponent<DatabaseEnemyEntryRowUI>();
            row.Bind(enemy, sharedTooltip);
        }

        if (contentRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void CacheEnemyRowTemplate()
    {
        if (_enemyRowTemplate)
            return;

        if (enemyRowPrefab)
        {
            _enemyRowTemplate = enemyRowPrefab;
            return;
        }

        if (!contentRoot || contentRoot.childCount == 0)
            return;

        Transform sampleRow = contentRoot.GetChild(0);
        if (!sampleRow)
            return;

        // Move off DatabaseContent so ClearContent does not destroy the instantiate source.
        sampleRow.SetParent(transform, false);
        sampleRow.gameObject.SetActive(false);
        _enemyRowTemplate = sampleRow.gameObject;
    }

    private void ClearContent()
    {
        if (!contentRoot)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }

    private void ResetScrollPosition()
    {
        if (!contentScrollRect)
            return;

        Canvas.ForceUpdateCanvases();
        contentScrollRect.verticalNormalizedPosition = 1f;
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
