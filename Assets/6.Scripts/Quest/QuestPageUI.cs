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
    [SerializeField] private Color regionLockedColor = new Color32(56, 58, 62, 255);
    [SerializeField] private Color regionHoverColor = new Color32(126, 133, 146, 255);
    [SerializeField] private Color regionPressedColor = new Color32(92, 99, 113, 255);
    [SerializeField] private Color regionNameUnlockedColor = new Color32(247, 225, 190, 255);
    [SerializeField] private Color regionNameLockedColor = new Color32(160, 155, 148, 200);

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
    [Tooltip("Alpha for rows whose rewards were claimed (non-repeatable COMPLETE).")]
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
    private Inventory _subscribedInventory;
    private PlayerStorage _subscribedStorage;
    private bool _detailWidgetsBuilt;

    private Button _questClaimButton;
    private TMP_Text _questClaimButtonLabel;
    private GameObject _questActionRowRoot;
    private static Sprite _cachedUiWhiteSprite;

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

    private static ItemDatabase _cachedItemDatabase;

    private static ItemDatabase FindItemDatabase()
    {
        if (_cachedItemDatabase)
            return _cachedItemDatabase;

        _cachedItemDatabase = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        return _cachedItemDatabase;
    }

    private void Awake()
    {
        EnsureDetailWidgets();
    }

    private void OnEnable()
    {
        TrySubscribeQuestProgress();
        TrySubscribeWorldProgress();
        TrySubscribeInventoryAndStorage();

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
        UnsubscribeInventoryAndStorage();
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

        if (TrySubscribeInventoryAndStorage())
        {
            RebuildQuestList();
            RefreshDetails();
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

    /// <returns>True if a new inventory or storage reference was bound (subscribe list changed).</returns>
    private bool TrySubscribeInventoryAndStorage()
    {
        bool changed = false;

        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv != _subscribedInventory)
        {
            if (_subscribedInventory)
                _subscribedInventory.OnInventoryChanged -= OnInventoryOrStorageChanged;
            _subscribedInventory = inv;
            if (_subscribedInventory)
                _subscribedInventory.OnInventoryChanged += OnInventoryOrStorageChanged;
            changed = true;
        }

        PlayerStorage st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (st != _subscribedStorage)
        {
            if (_subscribedStorage)
                _subscribedStorage.OnStorageChanged -= OnInventoryOrStorageChanged;
            _subscribedStorage = st;
            if (_subscribedStorage)
                _subscribedStorage.OnStorageChanged += OnInventoryOrStorageChanged;
            changed = true;
        }

        return changed;
    }

    private void UnsubscribeInventoryAndStorage()
    {
        if (_subscribedInventory != null)
        {
            _subscribedInventory.OnInventoryChanged -= OnInventoryOrStorageChanged;
            _subscribedInventory = null;
        }

        if (_subscribedStorage != null)
        {
            _subscribedStorage.OnStorageChanged -= OnInventoryOrStorageChanged;
            _subscribedStorage = null;
        }
    }

    private void OnInventoryOrStorageChanged()
    {
        if (!isActiveAndEnabled)
            return;
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
        ApplyRegionNameStyle(row, rowUI, unlocked);

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

    private void ApplyRegionNameStyle(GameObject row, WorldMapRegionRowUI rowUI, bool unlocked)
    {
        TMP_Text label = rowUI != null ? rowUI.NameText : row.GetComponentInChildren<TMP_Text>(true);
        if (label)
            label.color = unlocked ? regionNameUnlockedColor : regionNameLockedColor;
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

            int amt = qProg && q ? qProg.GetDisplayProgress(q) : 0;
            bool permanentlyDone = qProg && qProg.IsPermanentlyComplete(q);
            string status = BuildQuestListStatus(q, qProg, amt);
            row.Bind(q, q.listCategoryLabel, status, _selectedQuest == q, permanentlyDone, completedRowAlpha, OnQuestClicked);
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
        if (!q)
            return false;
        return qProg && qProg.IsPermanentlyComplete(q);
    }

    private static string BuildQuestListStatus(QuestDefinition q, QuestProgressManager mgr, int amt)
    {
        if (mgr != null && mgr.IsPermanentlyComplete(q))
            return "COMPLETE";
        if (!string.IsNullOrEmpty(q.listStatusOverride))
            return q.listStatusOverride;
        if (q.IsComplete(amt))
            return "Ready";
        if (amt > 0)
            return "In progress";
        return "Available";
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
        int amt = q && qProg ? qProg.GetDisplayProgress(q) : 0;

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
            {
                bool permanent = qProg && qProg.IsPermanentlyComplete(q);
                progressValueText.text = FormatProgressLine(q, amt, items, permanent);
            }
        }

        if (rewardsLabelText)
            rewardsLabelText.gameObject.SetActive(q != null);
        if (rewardsValueText)
        {
            rewardsValueText.gameObject.SetActive(q != null);
            if (q != null)
                rewardsValueText.text = FormatRewardsLine(q, items);
        }

        RefreshQuestClaimButton(q, qProg);

        if (detailsContentRoot is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private void RefreshQuestClaimButton(QuestDefinition q, QuestProgressManager qProg)
    {
        EnsureDetailWidgets();
        if (!_questActionRowRoot || !_questClaimButton)
            return;

        bool show = q != null && q.objectiveKind != QuestObjectiveKind.None;
        _questActionRowRoot.SetActive(show);
        if (!show)
            return;

        bool permanent = qProg && qProg.IsPermanentlyComplete(q);
        bool canClaim = qProg && qProg.CanClaimReward(q);

        if (permanent)
        {
            if (_questClaimButtonLabel)
                _questClaimButtonLabel.text = "Completed";
            _questClaimButton.interactable = false;
            return;
        }

        if (canClaim)
        {
            if (_questClaimButtonLabel)
                _questClaimButtonLabel.text = "Complete Quest";
            _questClaimButton.interactable = true;
            return;
        }

        if (_questClaimButtonLabel)
            _questClaimButtonLabel.text = "In Progress";
        _questClaimButton.interactable = false;
    }

    private void OnQuestClaimClicked()
    {
        QuestProgressManager mgr = FindQuestProgress();
        if (!_selectedQuest || mgr == null || !mgr.TryClaimQuestReward(_selectedQuest))
            return;

        RebuildQuestList();
        RefreshDetails();
    }

    private static string FormatProgressLine(QuestDefinition q, int current, ItemDatabase db, bool permanentlyComplete)
    {
        int target = Mathf.Max(1, q.targetCount);

        if (permanentlyComplete)
        {
            return q.objectiveKind switch
            {
                QuestObjectiveKind.GatherItem =>
                    $"Completed - {target} {ResolveItemName(q.gatherItemId, null, db)}",
                QuestObjectiveKind.KillCount => $"Completed - {target} kills",
                _ => "Completed"
            };
        }

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
        if (d)
            return d.displayName;
        return FormatItemIdAsFallbackName(itemId.Trim());
    }

    /// <summary>When an id is missing from the database, show readable words instead of raw snake_case.</summary>
    private static string FormatItemIdAsFallbackName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "items";
        string[] parts = raw.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (p.Length == 0)
                continue;
            parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "");
        }

        return string.Join(" ", parts);
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

        EnsureQuestClaimWidgets();
        _detailWidgetsBuilt = true;
    }

    private void EnsureQuestClaimWidgets()
    {
        if (_questClaimButton || !detailsContentRoot)
            return;

        var row = new GameObject("QuestDetailActionRow", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(detailsContentRoot, false);
        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 56f;
        rowLe.minHeight = 56f;
        rowLe.flexibleHeight = 0f;
        rowLe.minWidth = 0f;
        rowLe.flexibleWidth = 1f;

        var btnGo = new GameObject("QuestClaimButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(row.transform, false);
        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0f, 0f);
        btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.offsetMin = new Vector2(0f, 6f);
        btnRt.offsetMax = new Vector2(0f, -6f);
        var img = btnGo.GetComponent<Image>();
        img.sprite = GetUiWhiteSprite();
        img.type = Image.Type.Simple;
        img.color = new Color(0.4f, 0.44f, 0.5f, 1f);

        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f);
        cb.selectedColor = new Color(0.92f, 0.92f, 0.92f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(OnQuestClaimClicked);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 17;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = "In Progress";
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = false;

        _questClaimButton = btn;
        _questClaimButtonLabel = tmp;
        _questActionRowRoot = row;
    }

    private static Sprite GetUiWhiteSprite()
    {
        if (_cachedUiWhiteSprite)
            return _cachedUiWhiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _cachedUiWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return _cachedUiWhiteSprite;
    }

    private static bool IsRegionAvailable(RegionDefinition region, WorldMapProgressManager progress)
    {
        if (!region)
            return false;
        return region.IsRegionUnlocked(progress);
    }
}
