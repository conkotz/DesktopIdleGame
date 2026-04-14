using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Quests main-menu page: regions (same data/rules as level select), quest rows per region, and detail text.
/// </summary>
public class QuestPageUI : MonoBehaviour
{
    [Header("Theme — Region Buttons (match Level Select)")]
    [SerializeField] private Color regionUnlockedColor = new Color32(104, 111, 122, 255);
    [SerializeField] private Color regionLockedColor = new Color32(132, 68, 68, 255);
    [SerializeField] private Color regionHoverColor = new Color32(126, 133, 146, 255);
    [SerializeField] private Color regionPressedColor = new Color32(92, 99, 113, 255);

    [Header("Data")]
    [SerializeField] private WorldMapDefinition worldMap;
    [SerializeField] private QuestDatabase questDatabase;

    [Header("Left — Regions")]
    [SerializeField] private Transform regionListParent;
    [SerializeField] private GameObject regionRowPrefab;
    [SerializeField] private string regionSelectedChildName = "Selected";

    [Header("Center — Quest list")]
    [SerializeField] private Transform questListParent;
    [SerializeField] private QuestListRowUI questRowPrefab;
    [Tooltip("Alpha for rows whose objective is complete.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float completedRowAlpha = 0.45f;

    [Header("Right — Details (optional; created at runtime if missing)")]
    [SerializeField] private Transform detailsContentRoot;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text progressSectionLabelText;
    [SerializeField] private TMP_Text progressKindLabelText;
    [SerializeField] private TMP_Text progressValueText;
    [SerializeField] private TMP_Text rewardsLabelText;
    [SerializeField] private TMP_Text rewardsValueText;

    private readonly List<GameObject> _regionRows = new();
    private readonly List<RegionDefinition> _regionRowRegions = new();
    private readonly List<QuestListRowUI> _questRows = new();
    private readonly List<QuestDefinition> _scratchQuests = new();

    private RegionDefinition _selectedRegion;
    private QuestDefinition _selectedQuest;
    private QuestProgressManager _progressEventsTarget;
    private WorldMapProgressManager _worldProgressEventsTarget;
    private bool _detailWidgetsBuilt;

    private static WorldMapProgressManager FindWorldProgress()
    {
        if (WorldMapProgressManager.Instance != null)
            return WorldMapProgressManager.Instance;
        return FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
    }

    private static QuestProgressManager FindQuestProgress()
    {
        if (QuestProgressManager.Instance != null)
            return QuestProgressManager.Instance;
        return FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
    }

    private static ItemDatabase FindItemDatabase()
    {
        return FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        EnsureDetailWidgets();
    }

    private void OnEnable()
    {
        TrySubscribeQuestProgress();
        TrySubscribeWorldProgress();
        ResolveWorldMap();

        if (worldMap)
            ApplyDefaultRegionSelection();

        RebuildRegionList();
        RebuildQuestList();
        RefreshDetails();
    }

    private void OnDisable()
    {
        UnsubscribeQuestProgress();
        UnsubscribeWorldProgress();
    }

    private void Update()
    {
        if (_progressEventsTarget == null)
        {
            TrySubscribeQuestProgress();
            if (_progressEventsTarget != null)
            {
                RebuildQuestList();
                RefreshDetails();
            }
        }
    }

    private void TrySubscribeQuestProgress()
    {
        QuestProgressManager p = FindQuestProgress();
        if (!p || p == _progressEventsTarget)
            return;

        UnsubscribeQuestProgress();
        _progressEventsTarget = p;
        _progressEventsTarget.ProgressChanged += OnQuestProgressChanged;
    }

    private void UnsubscribeQuestProgress()
    {
        if (_progressEventsTarget != null)
        {
            _progressEventsTarget.ProgressChanged -= OnQuestProgressChanged;
            _progressEventsTarget = null;
        }
    }

    private void OnQuestProgressChanged()
    {
        if (!isActiveAndEnabled)
            return;
        RebuildQuestList();
        RefreshDetails();
    }

    private void TrySubscribeWorldProgress()
    {
        WorldMapProgressManager w = FindWorldProgress();
        if (!w || w == _worldProgressEventsTarget)
            return;

        UnsubscribeWorldProgress();
        _worldProgressEventsTarget = w;
        _worldProgressEventsTarget.ProgressChanged += OnWorldProgressChanged;
    }

    private void UnsubscribeWorldProgress()
    {
        if (_worldProgressEventsTarget != null)
        {
            _worldProgressEventsTarget.ProgressChanged -= OnWorldProgressChanged;
            _worldProgressEventsTarget = null;
        }
    }

    private void OnWorldProgressChanged()
    {
        if (!isActiveAndEnabled)
            return;
        RebuildRegionList();
        RebuildQuestList();
        RefreshDetails();
    }

    private void ResolveWorldMap()
    {
        if (!worldMap)
        {
            WorldMapProgressManager p = FindWorldProgress();
            if (p)
                worldMap = p.WorldMap;
        }
    }

    private void ApplyDefaultRegionSelection()
    {
        _selectedRegion = null;
        if (worldMap.regions == null || worldMap.regions.Count == 0)
            return;

        WorldMapProgressManager progress = FindWorldProgress();

        if (!string.IsNullOrEmpty(worldMap.startingRegionId))
        {
            RegionDefinition preferred = worldMap.FindRegionById(worldMap.startingRegionId);
            if (preferred && IsRegionAvailable(preferred, progress))
                _selectedRegion = preferred;
        }

        if (!_selectedRegion)
        {
            for (int i = 0; i < worldMap.regions.Count; i++)
            {
                RegionDefinition r = worldMap.regions[i];
                if (r && IsRegionAvailable(r, progress))
                {
                    _selectedRegion = r;
                    break;
                }
            }
        }

        if (!_selectedRegion)
            _selectedRegion = worldMap.regions[0];
    }

    private void RebuildRegionList()
    {
        ClearRegionRows();

        if (!regionListParent || !regionRowPrefab || !worldMap || worldMap.regions == null)
            return;

        WorldMapProgressManager progress = FindWorldProgress();

        for (int i = 0; i < worldMap.regions.Count; i++)
        {
            RegionDefinition region = worldMap.regions[i];
            if (!region) continue;

            GameObject row = Instantiate(regionRowPrefab, regionListParent);
            _regionRows.Add(row);
            _regionRowRegions.Add(region);

            WorldMapRegionRowUI rowUI = row.GetComponent<WorldMapRegionRowUI>();
            Button b = rowUI != null ? rowUI.Button : row.GetComponentInChildren<Button>(true);
            TMP_Text label = rowUI != null ? rowUI.NameText : row.GetComponentInChildren<TMP_Text>(true);
            bool unlocked = IsRegionAvailable(region, progress);
            if (label)
                label.text = unlocked ? region.displayName : $"{region.displayName} (Locked)";

            RegionDefinition captured = region;
            if (b)
            {
                b.interactable = unlocked;
                b.onClick.AddListener(() => OnRegionClicked(captured));
            }

            SetRegionRowSelected(row, unlocked, unlocked && region == _selectedRegion);
        }
    }

    private void ClearRegionRows()
    {
        for (int i = 0; i < _regionRows.Count; i++)
        {
            if (_regionRows[i])
                Destroy(_regionRows[i]);
        }

        _regionRows.Clear();
        _regionRowRegions.Clear();
    }

    private void SetRegionRowSelected(GameObject row, bool unlocked, bool selected)
    {
        if (!row) return;

        Button b = row.GetComponentInChildren<Button>(true);
        ApplyRegionButtonTheme(b, unlocked);

        WorldMapRegionRowUI rowUI = row.GetComponent<WorldMapRegionRowUI>();
        if (rowUI != null)
        {
            rowUI.SetSelected(selected);
            return;
        }

        if (!string.IsNullOrEmpty(regionSelectedChildName))
        {
            Transform t = row.transform.Find(regionSelectedChildName);
            if (t)
                t.gameObject.SetActive(selected);
        }
    }

    private void ApplyRegionButtonTheme(Button button, bool unlocked)
    {
        if (!button)
            return;

        Color baseCol = unlocked ? regionUnlockedColor : regionLockedColor;
        ColorBlock cb = button.colors;
        cb.normalColor = baseCol;
        cb.highlightedColor = regionHoverColor;
        cb.selectedColor = regionHoverColor;
        cb.pressedColor = regionPressedColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        if (button.targetGraphic)
            button.targetGraphic.color = baseCol;
    }

    private void OnRegionClicked(RegionDefinition region)
    {
        WorldMapProgressManager progress = FindWorldProgress();
        if (!IsRegionAvailable(region, progress))
            return;

        if (_selectedRegion == region)
            return;

        _selectedRegion = region;
        _selectedQuest = null;

        for (int i = 0; i < _regionRows.Count && i < _regionRowRegions.Count; i++)
        {
            bool unlocked = IsRegionAvailable(_regionRowRegions[i], progress);
            bool selected = unlocked && _regionRowRegions[i] == _selectedRegion;
            SetRegionRowSelected(_regionRows[i], unlocked, selected);
        }

        RebuildQuestList();
        RefreshDetails();
    }

    private void RebuildQuestList()
    {
        ClearQuestRows();

        if (!questListParent || !questRowPrefab || !questDatabase || !_selectedRegion)
            return;

        WorldMapProgressManager mapProgress = FindWorldProgress();
        if (!IsRegionAvailable(_selectedRegion, mapProgress))
        {
            _selectedQuest = null;
            RefreshDetails();
            return;
        }

        _scratchQuests.Clear();
        questDatabase.CollectForRegion(_selectedRegion.regionId, _scratchQuests);
        if (_scratchQuests.Count == 0)
        {
            _selectedQuest = null;
            RefreshDetails();
            return;
        }

        QuestProgressManager qProg = FindQuestProgress();
        _scratchQuests.Sort((a, b) => CompareQuestRows(a, b, qProg));

        for (int i = 0; i < _scratchQuests.Count; i++)
        {
            QuestDefinition q = _scratchQuests[i];
            if (!q) continue;

            QuestListRowUI row = Instantiate(questRowPrefab, questListParent);
            _questRows.Add(row);

            int amt = qProg ? qProg.GetProgress(q.questId) : 0;
            bool done = q.objectiveKind != QuestObjectiveKind.None && q.IsComplete(amt);
            string status = !string.IsNullOrEmpty(q.listStatusOverride) ? q.listStatusOverride : "Unlocked";
            row.Bind(q, q.listCategoryLabel, status, _selectedQuest == q, done, completedRowAlpha, OnQuestClicked);
        }

        if (_selectedQuest != null && !_scratchQuests.Contains(_selectedQuest))
            _selectedQuest = null;
        if (!_selectedQuest)
            _selectedQuest = _scratchQuests[0];

        RefreshQuestSelectionVisuals();

        Canvas.ForceUpdateCanvases();
        if (questListParent is RectTransform listRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRt);
    }

    private int CompareQuestRows(QuestDefinition a, QuestDefinition b, QuestProgressManager qProg)
    {
        bool ca = IsQuestCompleteForSort(a, qProg);
        bool cb = IsQuestCompleteForSort(b, qProg);
        if (ca != cb)
            return ca ? 1 : -1;
        int o = a.sortOrder.CompareTo(b.sortOrder);
        if (o != 0)
            return o;
        return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
    }

    private static bool IsQuestCompleteForSort(QuestDefinition q, QuestProgressManager qProg)
    {
        if (!q || q.objectiveKind == QuestObjectiveKind.None)
            return false;
        int amt = qProg ? qProg.GetProgress(q.questId) : 0;
        return q.IsComplete(amt);
    }

    private void ClearQuestRows()
    {
        for (int i = 0; i < _questRows.Count; i++)
        {
            if (_questRows[i])
                Destroy(_questRows[i].gameObject);
        }

        _questRows.Clear();
    }

    private void OnQuestClicked(QuestDefinition quest)
    {
        _selectedQuest = quest;
        RefreshQuestSelectionVisuals();
        RefreshDetails();
    }

    private void RefreshQuestSelectionVisuals()
    {
        for (int i = 0; i < _questRows.Count && i < _scratchQuests.Count; i++)
        {
            QuestDefinition q = _scratchQuests[i];
            bool sel = q && _selectedQuest == q;
            _questRows[i].SetSelected(sel);
        }
    }

    private void RefreshDetails()
    {
        EnsureDetailWidgets();
        ItemDatabase items = FindItemDatabase();
        QuestProgressManager qProg = FindQuestProgress();

        QuestDefinition q = _selectedQuest;
        if (!q || _selectedRegion == null || !IsRegionAvailable(_selectedRegion, FindWorldProgress()))
            q = null;

        if (detailNameText)
            detailNameText.text = q ? q.displayName : "—";

        if (detailDescriptionText)
            detailDescriptionText.text = q ? q.description : "";

        bool showProgress = q && q.objectiveKind != QuestObjectiveKind.None;
        int amt = q && qProg ? qProg.GetProgress(q.questId) : 0;

        if (progressSectionLabelText)
            progressSectionLabelText.gameObject.SetActive(showProgress);
        if (progressKindLabelText)
        {
            progressKindLabelText.gameObject.SetActive(showProgress);
            if (showProgress && q != null)
            {
                progressKindLabelText.text = q.objectiveKind switch
                {
                    QuestObjectiveKind.KillCount => "Kills",
                    QuestObjectiveKind.GatherItem => "Items",
                    _ => ""
                };
            }
        }

        if (progressValueText)
        {
            progressValueText.gameObject.SetActive(showProgress);
            if (showProgress)
                progressValueText.text = FormatProgressLine(q, amt, items);
        }

        if (rewardsLabelText)
            rewardsLabelText.gameObject.SetActive(q != null);
        if (rewardsValueText)
        {
            rewardsValueText.gameObject.SetActive(q != null);
            if (q != null)
                rewardsValueText.text = FormatRewardsLine(q, items);
        }

        if (detailsContentRoot is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private static string FormatProgressLine(QuestDefinition q, int current, ItemDatabase db)
    {
        int target = Mathf.Max(1, q.targetCount);
        current = Mathf.Clamp(current, 0, target);

        switch (q.objectiveKind)
        {
            case QuestObjectiveKind.KillCount:
                return $"{current}/{target} kills";
            case QuestObjectiveKind.GatherItem:
            {
                string itemName = ResolveItemName(q.gatherItemId, null, db);
                return $"{current} {itemName}/{target} {itemName}";
            }
            default:
                return "";
        }
    }

    private static string FormatRewardsLine(QuestDefinition q, ItemDatabase db)
    {
        var parts = new List<string>();
        if (q.rewardGold > 0)
            parts.Add($"{q.rewardGold}g");

        if (q.rewardItem)
            parts.Add(q.rewardItem.displayName);
        else
        {
            string nm = ResolveItemName(q.rewardItemId, null, db);
            if (!string.IsNullOrEmpty(nm))
                parts.Add(nm);
        }

        if (!string.IsNullOrWhiteSpace(q.rewardNotes))
            parts.Add(q.rewardNotes.Trim());

        return parts.Count > 0 ? string.Join(" / ", parts) : "—";
    }

    private static string ResolveItemName(string itemId, ItemDefinition direct, ItemDatabase db)
    {
        if (direct)
            return direct.displayName;
        if (string.IsNullOrWhiteSpace(itemId))
            return "items";
        ItemDefinition d = db ? db.Get(itemId) : null;
        return d ? d.displayName : itemId;
    }

    private void EnsureDetailWidgets()
    {
        if (_detailWidgetsBuilt)
            return;

        if (!detailsContentRoot)
            return;

        TMP_Text MakeText(string name, int fontSize, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(detailsContentRoot, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = new Color32(43, 33, 24, 255);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            return tmp;
        }

        if (!detailNameText)
            detailNameText = MakeText("QuestDetailName", 22, FontStyles.Bold);
        if (!detailDescriptionText)
            detailDescriptionText = MakeText("QuestDetailBody", 18);
        if (!progressSectionLabelText)
            progressSectionLabelText = MakeText("ProgressSectionLabel", 16, FontStyles.Bold);
        if (!progressKindLabelText)
            progressKindLabelText = MakeText("ProgressKindLabel", 16, FontStyles.Italic);
        if (!progressValueText)
            progressValueText = MakeText("ProgressValue", 18);
        if (!rewardsLabelText)
        {
            rewardsLabelText = MakeText("RewardsLabel", 16, FontStyles.Bold);
            rewardsLabelText.text = "Rewards";
        }

        if (!rewardsValueText)
            rewardsValueText = MakeText("RewardsValue", 18);

        if (progressSectionLabelText && string.IsNullOrEmpty(progressSectionLabelText.text))
            progressSectionLabelText.text = "Progress";

        _detailWidgetsBuilt = true;
    }

    private static bool IsRegionAvailable(RegionDefinition region, WorldMapProgressManager progress)
    {
        if (!region)
            return false;
        return region.IsRegionUnlocked(progress);
    }
}
