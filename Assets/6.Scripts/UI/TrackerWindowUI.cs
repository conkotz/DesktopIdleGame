using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the in-game session tracker window (XP gained and loot value, broken down by source).
/// Auto-resolves the TMP labels and the Reset button by name so the script can simply be added to
/// the TrackerWindow GameObject in the scene.
///
/// The first source for each section reuses the placeholder rows already authored in the prefab
/// (XPSourceText/TotalXPText/XPPerHourText for XP; LootSourceText/LootText/TotalLootValueText for loot).
/// Additional sources are created by cloning those rows and inserting them right after the previous
/// triplet so the existing VerticalLayoutGroup keeps them stacked.
/// </summary>
[DisallowMultipleComponent]
public class TrackerWindowUI : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text elapsedTimeText;
    [SerializeField] private Button resetButton;

    [Header("Experience Section")]
    [SerializeField] private RectTransform experienceSection;
    [SerializeField] private TMP_Text xpTotalValueText;
    [SerializeField] private TMP_Text xpPerHourText;
    [SerializeField] private TMP_Text xpSourceText;
    [SerializeField] private TMP_Text xpTotalSourceText;
    [SerializeField] private TMP_Text xpPerHourSourceText;

    [Header("Loot Section")]
    [SerializeField] private RectTransform lootSection;
    [SerializeField] private TMP_Text lootTotalValueText;
    [SerializeField] private TMP_Text lootPerHourText;
    [SerializeField] private TMP_Text lootSourceText;
    [SerializeField] private TMP_Text lootItemsText;
    [SerializeField] private TMP_Text lootValueSourceText;

    [Header("Layout Labels")]
    [Tooltip("Prefix appended to the XP total. e.g. 'Total: '. If blank, the placeholder text on the field is used.")]
    [SerializeField] private string xpTotalPrefix = "Total: ";
    [SerializeField] private string xpPerHourPrefix = "Per Hour: ";
    [SerializeField] private string lootTotalPrefix = "Total Gold: ";
    [SerializeField] private string lootPerHourPrefix = "Per Hour: ";
    [SerializeField] private string xpSourceGainedPrefix = "Gained: ";
    [SerializeField] private string lootSourceValuePrefix = "Total Value: ";
    [SerializeField] private string emptySourcePlaceholder = "—";

    [Tooltip("Suffix appended after every loot/gold value (e.g. 'g').")]
    [SerializeField] private string lootCurrencySuffix = "g";

    [Header("Refresh")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private SessionTrackerData _data;
    private float _nextRefreshTime;

    private PlayerCombatController _cachedCombat;
    private PlayerController _cachedPlayer;

    private ScrollRect _scrollRect;
    private bool _scrollViewConfigured;
    private bool _layoutRebuildPending;

    /// <summary>Captures each TMP field's authored font size on first encounter so the smaller-size markup doesn't cascade-shrink across refreshes.</summary>
    private readonly Dictionary<TMP_Text, float> _capturedBaseFontSizeByField = new();

    private readonly List<TMP_Text> _xpSourceRowSourceLabels = new();
    private readonly List<TMP_Text> _xpSourceRowTotalLabels = new();
    private readonly List<TMP_Text> _xpSourceRowPerHourLabels = new();

    private readonly List<TMP_Text> _lootSourceRowSourceLabels = new();
    private readonly List<TMP_Text> _lootSourceRowItemsLabels = new();
    private readonly List<TMP_Text> _lootSourceRowValueLabels = new();

    private bool _captured;

    private void Awake()
    {
        MigrateLegacyPrefixDefaults();
        ResolveReferences();
        CaptureTemplateRows();
    }

    /// <summary>
    /// Older scene/prefab data still has the v1 prefixes ('Value: ' for both XP and loot totals). When we encounter
    /// those exact legacy strings we silently bump them to the v2 wording ('Total: ' / 'Total Gold: ') so the upgrade
    /// applies without forcing the user to re-author the inspector values. Custom prefixes are left untouched.
    /// </summary>
    private void MigrateLegacyPrefixDefaults()
    {
        if (xpTotalPrefix == "Value: ")
            xpTotalPrefix = "Total: ";
        if (lootTotalPrefix == "Value: ")
            lootTotalPrefix = "Total Gold: ";
        if (string.IsNullOrEmpty(lootCurrencySuffix))
            lootCurrencySuffix = "g";
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureTemplateRows();

        _data = SessionTrackerData.EnsureInstance();
        if (_data != null)
            _data.OnDataChanged += MarkDirty;

        WireResetButton();
        // Re-wire one frame later as well: UIWindowCloseButton.Awake on a child GO can run after our OnEnable
        // in some activation paths and would otherwise re-add a CloseWindow listener that we just stripped.
        StartCoroutine(DeferredRewireResetButton());

        EnsureScrollViewMasking();
        Refresh();
    }

    private IEnumerator DeferredRewireResetButton()
    {
        yield return null;
        WireResetButton();
    }

    private void OnDisable()
    {
        if (_data != null)
            _data.OnDataChanged -= MarkDirty;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;
        _nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void MarkDirty() => _nextRefreshTime = 0f;

    private void WireResetButton()
    {
        if (!resetButton)
            return;

        // The ResetButton is typically duplicated from the CloseButton in the prefab, which leaves a UIWindowCloseButton
        // (and sometimes window-toggle scripts) on it. Those scripts add a runtime listener inside their Awake that
        // closes/toggles the window when clicked. RemoveAllListeners + setting enabled=false is not enough because
        // UIWindowCloseButton.Awake can run after our OnEnable on some activation paths, re-adding the listener.
        DestroyConflictingButtonBehaviours(resetButton.gameObject);

        // Replace the entire ButtonClickedEvent with a fresh instance — wipes ALL listeners, persistent or runtime,
        // regardless of who added them or when.
        resetButton.onClick = new Button.ButtonClickedEvent();
        resetButton.onClick.AddListener(HandleResetClicked);
    }

    /// <summary>
    /// Mirrors the DPS window scroll setup so the tracker actually scrolls when content overflows the viewport.
    /// Ensures the viewport masks, the content has a ContentSizeFitter, and that text labels don't trap pointer
    /// wheel/drag input. Idempotent — safe to call from OnEnable and after dynamic row inserts.
    /// </summary>
    private void EnsureScrollViewMasking()
    {
        // Stamp the configured flag immediately so a partial failure cannot make Refresh re-enter every tick.
        _scrollViewConfigured = true;

        try
        {
            if (!_scrollRect)
                _scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (!_scrollRect)
                return;

            _scrollRect.vertical = true;
            _scrollRect.horizontal = false;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = Mathf.Max(_scrollRect.scrollSensitivity, 20f);

            ConfigureViewportSafely();

            RectTransform content = _scrollRect.content;
            if (content == null)
                return;

            // Top-pinned content that grows downward as we add per-source rows.
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            EnsureComponent<ContentSizeFitter>(content.gameObject, fitter =>
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            });

            EnsureComponent<VerticalLayoutGroup>(content.gameObject, vlg =>
            {
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;
            });

            ConfigureSectionForScroll(experienceSection);
            ConfigureSectionForScroll(lootSection);

            DisableRaycastTargetsUnder(content);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TrackerWindowUI] EnsureScrollViewMasking failed: {ex.Message}", this);
        }

        ScheduleLayoutRebuild();
    }

    private void ConfigureViewportSafely()
    {
        if (_scrollRect == null)
            return;

        RectTransform viewport = _scrollRect.viewport;
        if (viewport == null && _scrollRect.transform != null)
            viewport = _scrollRect.transform.Find("Viewport") as RectTransform;
        if (viewport == null)
            return;

        EnsureComponent<RectMask2D>(viewport.gameObject, _ => { });

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        if (_scrollRect.viewport != viewport)
            _scrollRect.viewport = viewport;
    }

    private static void ConfigureSectionForScroll(RectTransform section)
    {
        if (section == null)
            return;

        VerticalLayoutGroup vlg = section.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
        }

        EnsureComponent<ContentSizeFitter>(section.gameObject, fitter =>
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        });
    }

    private static void EnsureComponent<T>(GameObject go, Action<T> configure) where T : Component
    {
        if (go == null)
            return;
        T existing = go.GetComponent<T>();
        if (existing == null)
            existing = go.AddComponent<T>();
        if (existing != null)
            configure?.Invoke(existing);
    }

    private static void DisableRaycastTargetsUnder(RectTransform root)
    {
        if (root == null)
            return;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].raycastTarget = false;
        }
    }

    private void ScheduleLayoutRebuild() => _layoutRebuildPending = true;

    private void LateUpdate()
    {
        if (!_layoutRebuildPending)
            return;
        _layoutRebuildPending = false;

        if (_scrollRect && _scrollRect.content)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
        if (experienceSection)
            LayoutRebuilder.ForceRebuildLayoutImmediate(experienceSection);
        if (lootSection)
            LayoutRebuilder.ForceRebuildLayoutImmediate(lootSection);
    }

    /// <summary>
    /// Destroys any sibling components on the ResetButton GameObject that would auto-wire their own onClick listener
    /// (UIWindowCloseButton, window-toggle scripts). We can't just disable them — their Awake adds a runtime listener
    /// that we cannot reliably strip across script-execution-order edge cases — so we delete the components outright.
    /// </summary>
    private static void DestroyConflictingButtonBehaviours(GameObject go)
    {
        if (!go)
            return;

        UIWindowCloseButton close = go.GetComponent<UIWindowCloseButton>();
        if (close) Destroy(close);

        FullDpsWindowToggleUI fullToggle = go.GetComponent<FullDpsWindowToggleUI>();
        if (fullToggle) Destroy(fullToggle);

        ActivityWindowToggleUI activityToggle = go.GetComponent<ActivityWindowToggleUI>();
        if (activityToggle) Destroy(activityToggle);

        TrackerWindowToggleUI trackerToggle = go.GetComponent<TrackerWindowToggleUI>();
        if (trackerToggle) Destroy(trackerToggle);

        WindowToggleUI genericToggle = go.GetComponent<WindowToggleUI>();
        if (genericToggle) Destroy(genericToggle);
    }

    private void HandleResetClicked()
    {
        SessionTrackerData.EnsureInstance().ResetSession();
        Refresh();
    }

    private void Refresh()
    {
        if (_data == null)
            _data = SessionTrackerData.EnsureInstance();

        int xpRowsBefore = _xpSourceRowSourceLabels.Count;
        int lootRowsBefore = _lootSourceRowSourceLabels.Count;

        RefreshElapsedTime();
        RefreshExperienceSection();
        RefreshLootSection();

        bool grew = _xpSourceRowSourceLabels.Count > xpRowsBefore
                 || _lootSourceRowSourceLabels.Count > lootRowsBefore;

        if (grew)
        {
            // Cloned rows can intercept the scroll-rect drag/wheel — strip raycast targets and rebuild layout.
            DisableRaycastsOnDynamicRows();
            ScheduleLayoutRebuild();
        }

        if (!_scrollViewConfigured)
            EnsureScrollViewMasking();
    }

    private void DisableRaycastsOnDynamicRows()
    {
        DisableRaycastsOn(_xpSourceRowSourceLabels);
        DisableRaycastsOn(_xpSourceRowTotalLabels);
        DisableRaycastsOn(_xpSourceRowPerHourLabels);
        DisableRaycastsOn(_lootSourceRowSourceLabels);
        DisableRaycastsOn(_lootSourceRowItemsLabels);
        DisableRaycastsOn(_lootSourceRowValueLabels);
    }

    private static void DisableRaycastsOn(List<TMP_Text> texts)
    {
        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text t = texts[i];
            if (t != null && t.raycastTarget)
                t.raycastTarget = false;
        }
    }

    private void RefreshElapsedTime()
    {
        if (!elapsedTimeText)
            return;

        float elapsed = _data != null ? _data.ElapsedSeconds : 0f;
        int totalSeconds = Mathf.FloorToInt(elapsed);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        elapsedTimeText.text = hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    private void RefreshExperienceSection()
    {
        int total = _data != null ? _data.TotalXp : 0;
        float perHour = _data != null ? _data.ComputePerHour(total) : 0f;

        if (xpTotalValueText)
            xpTotalValueText.text = $"{xpTotalPrefix}{total:N0} xp";
        if (xpPerHourText)
            xpPerHourText.text = $"{xpPerHourPrefix}{Mathf.RoundToInt(perHour):N0} xp/hr";

        IReadOnlyList<SessionTrackerData.XpSourceEntry> entries = _data != null
            ? _data.XpEntries
            : Array.Empty<SessionTrackerData.XpSourceEntry>();

        EnsureSourceRowCapacity(
            entries.Count,
            xpSourceText,
            xpTotalSourceText,
            xpPerHourSourceText,
            _xpSourceRowSourceLabels,
            _xpSourceRowTotalLabels,
            _xpSourceRowPerHourLabels);

        for (int i = 0; i < _xpSourceRowSourceLabels.Count; i++)
        {
            bool active = i < entries.Count;
            ToggleRowActive(_xpSourceRowSourceLabels[i], active);
            ToggleRowActive(_xpSourceRowTotalLabels[i], active);
            ToggleRowActive(_xpSourceRowPerHourLabels[i], active);

            if (!active)
                continue;

            SessionTrackerData.XpSourceEntry e = entries[i];
            if (_xpSourceRowSourceLabels[i])
                _xpSourceRowSourceLabels[i].text = e.source;
            if (_xpSourceRowTotalLabels[i])
                _xpSourceRowTotalLabels[i].text = BuildXpTotalLine(e, _xpSourceRowTotalLabels[i]);
            if (_xpSourceRowPerHourLabels[i])
            {
                float ratePerHour = _data != null ? _data.ComputePerHour(e.totalXp) : 0f;
                _xpSourceRowPerHourLabels[i].text = $"{Mathf.RoundToInt(ratePerHour):N0} xp/hr";
            }
        }

        if (entries.Count == 0)
        {
            // Show a single placeholder row so the section never collapses to nothing.
            if (xpSourceText) xpSourceText.text = emptySourcePlaceholder;
            if (xpTotalSourceText) xpTotalSourceText.text = $"{xpSourceGainedPrefix}0xp";
            if (xpPerHourSourceText) xpPerHourSourceText.text = "0 xp/hr";
            ToggleRowActive(xpSourceText, true);
            ToggleRowActive(xpTotalSourceText, true);
            ToggleRowActive(xpPerHourSourceText, true);
        }
    }

    /// <summary>
    /// Builds the per-source XP line. The "(... xp)" / "(... xp per damage)" portion is wrapped in a TMP
    /// <c>&lt;size&gt;</c> tag so it renders 2pt smaller than the field's authored font size.
    /// </summary>
    private string BuildXpTotalLine(SessionTrackerData.XpSourceEntry entry, TMP_Text targetField)
    {
        if (entry == null)
            return $"{xpSourceGainedPrefix}0xp";

        string bracket = BuildXpBracket(entry);
        string smaller = WrapBracketAtSmallerSize(bracket, targetField);
        return $"{xpSourceGainedPrefix}{entry.totalXp:N0}xp {smaller}";
    }

    private string WrapBracketAtSmallerSize(string bracketContent, TMP_Text targetField)
    {
        if (string.IsNullOrEmpty(bracketContent))
            return string.Empty;

        if (targetField == null)
            return $"({bracketContent})";

        float baseSize = GetCapturedBaseFontSize(targetField);
        float smallerSize = Mathf.Max(6f, baseSize - 2f);
        return $"<size={smallerSize.ToString("0.##")}>({bracketContent})</size>";
    }

    private float GetCapturedBaseFontSize(TMP_Text field)
    {
        if (field == null)
            return 16f;
        if (_capturedBaseFontSizeByField.TryGetValue(field, out float cached))
            return cached;

        float value = field.fontSize > 0f ? field.fontSize : 16f;
        _capturedBaseFontSizeByField[field] = value;
        return value;
    }

    private string BuildXpBracket(SessionTrackerData.XpSourceEntry entry)
    {
        if (entry == null)
            return "0xp";

        if (SessionTrackerData.IsCombatStyleSkill(entry.lastSkill))
        {
            float perDamage = ResolveCombatXpPerDamage(entry.lastSkill);
            // Trim trailing zeros so 0.10 reads as "0.1"; round to 2dp for compactness.
            return $"{perDamage.ToString("0.##")}xp per damage";
        }

        return $"{entry.lastGainAmount}xp";
    }

    private float ResolveCombatXpPerDamage(SkillType skill)
    {
        if (skill == SkillType.Endurance)
        {
            if (!_cachedPlayer)
                _cachedPlayer = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            return _cachedPlayer ? _cachedPlayer.EnduranceXpPerDamage : 0f;
        }

        if (!_cachedCombat)
            _cachedCombat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
        return _cachedCombat ? _cachedCombat.XpPerDamage : 0f;
    }

    private void RefreshLootSection()
    {
        int total = _data != null ? _data.TotalLootValue : 0;
        float perHour = _data != null ? _data.ComputePerHour(total) : 0f;

        string suffix = lootCurrencySuffix ?? string.Empty;

        if (lootTotalValueText)
            lootTotalValueText.text = $"{lootTotalPrefix}{total:N0}{suffix}";
        if (lootPerHourText)
            lootPerHourText.text = $"{lootPerHourPrefix}{Mathf.RoundToInt(perHour):N0}{suffix}/hr";

        IReadOnlyList<SessionTrackerData.LootSourceEntry> entries = _data != null
            ? _data.LootEntries
            : Array.Empty<SessionTrackerData.LootSourceEntry>();

        EnsureSourceRowCapacity(
            entries.Count,
            lootSourceText,
            lootItemsText,
            lootValueSourceText,
            _lootSourceRowSourceLabels,
            _lootSourceRowItemsLabels,
            _lootSourceRowValueLabels);

        for (int i = 0; i < _lootSourceRowSourceLabels.Count; i++)
        {
            bool active = i < entries.Count;
            ToggleRowActive(_lootSourceRowSourceLabels[i], active);
            ToggleRowActive(_lootSourceRowItemsLabels[i], active);
            ToggleRowActive(_lootSourceRowValueLabels[i], active);

            if (!active)
                continue;

            SessionTrackerData.LootSourceEntry e = entries[i];
            if (_lootSourceRowSourceLabels[i])
                _lootSourceRowSourceLabels[i].text = e.source;
            if (_lootSourceRowItemsLabels[i])
                _lootSourceRowItemsLabels[i].text = BuildLootItemsList(e);
            if (_lootSourceRowValueLabels[i])
                _lootSourceRowValueLabels[i].text = $"{lootSourceValuePrefix}{e.totalValue:N0}{suffix}";
        }

        if (entries.Count == 0)
        {
            if (lootSourceText) lootSourceText.text = emptySourcePlaceholder;
            if (lootItemsText) lootItemsText.text = "—";
            if (lootValueSourceText) lootValueSourceText.text = $"{lootSourceValuePrefix}0{suffix}";
            ToggleRowActive(lootSourceText, true);
            ToggleRowActive(lootItemsText, true);
            ToggleRowActive(lootValueSourceText, true);
        }
    }

    private static string BuildLootItemsList(SessionTrackerData.LootSourceEntry entry)
    {
        if (entry == null || entry.orderedItemIds.Count == 0)
            return "—";

        var sb = new StringBuilder(64);
        for (int i = 0; i < entry.orderedItemIds.Count; i++)
        {
            string itemId = entry.orderedItemIds[i];
            int amount = entry.itemAmounts.TryGetValue(itemId, out int v) ? v : 0;

            string label = ResolveItemDisplayName(itemId);
            if (i > 0)
                sb.Append(", ");
            sb.Append(label);
            if (amount > 1)
            {
                sb.Append(" x");
                sb.Append(amount);
            }
        }
        return sb.ToString();
    }

    private static string ResolveItemDisplayName(string itemId)
    {
        Inventory inv = SessionTrackerData.Instance != null
            ? FindFirstObjectByType<Inventory>(FindObjectsInactive.Include)
            : null;
        if (inv != null)
        {
            ItemDefinition def = inv.GetItemDef(itemId);
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;
        }
        return itemId;
    }

    /// <summary>
    /// Ensures three parallel lists each contain at least <paramref name="needed"/> rows by cloning
    /// the original template TMP fields (which represent the first row already authored in the scene)
    /// and inserting them as siblings right after the previous triplet so the vertical layout stays tidy.
    /// </summary>
    private static void EnsureSourceRowCapacity(
        int needed,
        TMP_Text templateSourceField,
        TMP_Text templateMiddleField,
        TMP_Text templateRightField,
        List<TMP_Text> sourceList,
        List<TMP_Text> middleList,
        List<TMP_Text> rightList)
    {
        if (templateSourceField == null || templateMiddleField == null || templateRightField == null)
            return;

        if (sourceList.Count == 0)
        {
            sourceList.Add(templateSourceField);
            middleList.Add(templateMiddleField);
            rightList.Add(templateRightField);
        }

        while (sourceList.Count < Mathf.Max(needed, 1))
        {
            int newIndex = sourceList.Count;

            TMP_Text src = CloneRowField(templateSourceField, newIndex);
            TMP_Text mid = CloneRowField(templateMiddleField, newIndex);
            TMP_Text right = CloneRowField(templateRightField, newIndex);

            sourceList.Add(src);
            middleList.Add(mid);
            rightList.Add(right);

            // Place the new triplet right after the previous one so the vertical layout group keeps the rows grouped per source.
            int previousLastSibling = rightList[newIndex - 1] != null
                ? rightList[newIndex - 1].transform.GetSiblingIndex()
                : templateRightField.transform.GetSiblingIndex();

            if (src) src.transform.SetSiblingIndex(previousLastSibling + 1);
            if (mid) mid.transform.SetSiblingIndex(previousLastSibling + 2);
            if (right) right.transform.SetSiblingIndex(previousLastSibling + 3);
        }
    }

    private static TMP_Text CloneRowField(TMP_Text template, int rowIndex)
    {
        if (template == null)
            return null;

        var clone = Instantiate(template, template.transform.parent);
        clone.gameObject.name = $"{template.name}_{rowIndex}";
        clone.gameObject.SetActive(true);
        return clone;
    }

    private static void ToggleRowActive(TMP_Text field, bool active)
    {
        if (!field)
            return;
        if (field.gameObject.activeSelf != active)
            field.gameObject.SetActive(active);
    }

    private void CaptureTemplateRows()
    {
        if (_captured)
            return;
        _captured = true;

        if (xpSourceText && xpTotalSourceText && xpPerHourSourceText)
        {
            _xpSourceRowSourceLabels.Add(xpSourceText);
            _xpSourceRowTotalLabels.Add(xpTotalSourceText);
            _xpSourceRowPerHourLabels.Add(xpPerHourSourceText);
        }
        if (lootSourceText && lootItemsText && lootValueSourceText)
        {
            _lootSourceRowSourceLabels.Add(lootSourceText);
            _lootSourceRowItemsLabels.Add(lootItemsText);
            _lootSourceRowValueLabels.Add(lootValueSourceText);
        }
    }

    private void ResolveReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (!elapsedTimeText) elapsedTimeText = FindByName(texts, "ElapsedTimeText");

        if (!experienceSection)
            experienceSection = FindRectByName(transform, "ExperienceSection");
        if (!lootSection)
            lootSection = FindRectByName(transform, "LootSection");

        TMP_Text[] xpTexts = experienceSection ? experienceSection.GetComponentsInChildren<TMP_Text>(true) : texts;
        TMP_Text[] lootTexts = lootSection ? lootSection.GetComponentsInChildren<TMP_Text>(true) : texts;

        if (!xpTotalValueText)
            xpTotalValueText = FindByName(xpTexts, "TotalValueText");
        if (!xpPerHourText)
            xpPerHourText = FindByName(xpTexts, "PerHourText");
        if (!xpSourceText)
            xpSourceText = FindByName(xpTexts, "XPSourceText", "SourceText");
        if (!xpTotalSourceText)
            xpTotalSourceText = FindByName(xpTexts, "TotalXPText");
        if (!xpPerHourSourceText)
            xpPerHourSourceText = FindByName(xpTexts, "XPPerHourText");

        if (!lootTotalValueText)
            lootTotalValueText = FindByName(lootTexts, "TotalValueText");
        if (!lootPerHourText)
            lootPerHourText = FindByName(lootTexts, "PerHourText");
        if (!lootSourceText)
            lootSourceText = FindByName(lootTexts, "LootSourceText", "SourceText");
        if (!lootItemsText)
            lootItemsText = FindByName(lootTexts, "LootText");
        if (!lootValueSourceText)
            lootValueSourceText = FindByName(lootTexts, "TotalLootValueText");

        if (!resetButton)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (!b) continue;
                if (b.name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    resetButton = b;
                    break;
                }
            }
        }
    }

    private static TMP_Text FindByName(TMP_Text[] texts, params string[] candidates)
    {
        if (texts == null || candidates == null || candidates.Length == 0)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!text) continue;
            for (int j = 0; j < candidates.Length; j++)
            {
                string c = candidates[j];
                if (!string.IsNullOrWhiteSpace(c) &&
                    string.Equals(text.name, c, StringComparison.OrdinalIgnoreCase))
                    return text;
            }
        }

        // Loose match fallback: contains the candidate substring.
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!text) continue;
            for (int j = 0; j < candidates.Length; j++)
            {
                string c = candidates[j];
                if (!string.IsNullOrWhiteSpace(c) &&
                    text.name.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0)
                    return text;
            }
        }

        return null;
    }

    private static RectTransform FindRectByName(Transform root, string name)
    {
        if (!root || string.IsNullOrWhiteSpace(name))
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                return t as RectTransform;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            ResolveReferences();
    }
#endif
}
