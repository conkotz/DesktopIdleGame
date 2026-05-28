using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates the NEW skills page Skills column with <see cref="SkillListEntryUI"/> rows for the active category.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilitySkillsListPanelUI : MonoBehaviour
{
    private const string RowsContainerName = "SkillListRows";
    private const string ScrollViewName = "ScrollView";
    private const float HeaderHeight = 40f;
    private const float RowSpacing = 6f;

    [SerializeField] private Transform listContent;
    [SerializeField] private SkillListEntryUI entryPrefab;

    private SkillDatabase _skillDatabase;
    private SkillsManager _skillsManager;
    private Action<SkillDefinition> _onSkillClicked;
    private Action<SkillDefinition> _onHoverAcknowledge;
    private SkillCategory _visibleCategory = SkillCategory.Combat;
    private readonly Dictionary<SkillType, SkillListEntryUI> _entryBySkillType = new();
    private readonly List<SkillListEntryUI> _rows = new();

    public void Configure(
        SkillDatabase database,
        SkillsManager skillsManager,
        Action<SkillDefinition> onSkillClicked,
        Action<SkillDefinition> onHoverAcknowledge = null)
    {
        _skillDatabase = database;
        _skillsManager = skillsManager;
        _onSkillClicked = onSkillClicked;
        _onHoverAcknowledge = onHoverAcknowledge;
    }

    public void SetEntryPrefab(SkillListEntryUI prefab)
    {
        if (prefab != null)
            entryPrefab = prefab;
    }

    public void SetVisibleCategory(SkillCategory category)
    {
        if (_visibleCategory == category && _rows.Count > 0)
            return;

        _visibleCategory = category;
        RebuildList();
    }

    public void RebuildList()
    {
        EnsureReferences();
        ClearRows();
        PurgeStaleListChildren();

        if (_skillDatabase == null || entryPrefab == null || listContent == null)
            return;

        PreferRuntimeSkillsManager();

        foreach (SkillDefinition skill in CollectSkillsForCategory(_visibleCategory))
        {
            int level = _skillsManager != null ? _skillsManager.GetLevel(skill.skillType) : 1;
            float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skill.skillType) : 0f;
            SkillListEntryUI entry = Instantiate(entryPrefab, listContent);
            entry.gameObject.SetActive(true);
            entry.Setup(skill, level, progress01, false, HandleEntryClicked, _onHoverAcknowledge);
            _entryBySkillType[skill.skillType] = entry;
            _rows.Add(entry);
        }

        if (listContent is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    public void ShowUnlockGlowForSkill(SkillType skillType)
    {
        if (_entryBySkillType.TryGetValue(skillType, out SkillListEntryUI entry) && entry != null)
            entry.ShowUnlockGlow();
    }

    public void ApplyPendingEntryGlows(IEnumerable<SkillType> pendingSkillTypes)
    {
        if (pendingSkillTypes == null)
            return;

        foreach (SkillType skillType in pendingSkillTypes)
            ShowUnlockGlowForSkill(skillType);
    }

    public void RefreshSelection(SkillDefinition selectedSkill)
    {
        foreach (KeyValuePair<SkillType, SkillListEntryUI> pair in _entryBySkillType)
        {
            bool selected = selectedSkill != null && selectedSkill.skillType == pair.Key;
            pair.Value.SetSelected(selected);
        }
    }

    public void RefreshLevelsForSkill(SkillType skillType)
    {
        if (!_entryBySkillType.TryGetValue(skillType, out SkillListEntryUI entry) || entry == null)
            return;

        PreferRuntimeSkillsManager();
        int level = _skillsManager != null ? _skillsManager.GetLevel(skillType) : 1;
        float progress01 = _skillsManager != null ? _skillsManager.GetProgress01(skillType) : 0f;
        entry.SetLevel(level);
        entry.SetProgress(progress01);
    }

    /// <summary>Updates every visible row from <see cref="SkillsManager"/> (e.g. after save load).</summary>
    public void RefreshAllLevels()
    {
        PreferRuntimeSkillsManager();

        if (_entryBySkillType.Count == 0)
        {
            RebuildList();
            return;
        }

        foreach (SkillType skillType in _entryBySkillType.Keys)
            RefreshLevelsForSkill(skillType);
    }

    private void HandleEntryClicked(SkillDefinition skill)
    {
        _onSkillClicked?.Invoke(skill);
    }

    private void ClearRows()
    {
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i] != null)
                Destroy(_rows[i].gameObject);
        }

        _rows.Clear();
        _entryBySkillType.Clear();
    }

    private void EnsureReferences()
    {
        Transform scroll = transform.Find(ScrollViewName);
        if (scroll != null)
            PurgeAbilityRowsFromScroll(scroll);

        if (listContent == null)
        {
            if (scroll == null)
                scroll = BuildScrollView();

            listContent = scroll != null
                ? scroll.Find("Viewport/Content")
                : null;
        }

        if (listContent != null)
            listContent = EnsureRowsContainer();

        if (entryPrefab == null)
        {
            SkillsAbilityPageNewUI page =
                GetComponentInParent<SkillsAbilityPageNewUI>(true);
            if (page == null)
                page = FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);

            if (page != null && page.SkillEntryPrefab != null)
                entryPrefab = page.SkillEntryPrefab;
        }

        if (entryPrefab == null)
        {
            SkillsAbilitiesPageUI legacy = FindFirstObjectByType<SkillsAbilitiesPageUI>(FindObjectsInactive.Include);
            if (legacy != null)
            {
                SkillListEntryUI[] templates = legacy.GetComponentsInChildren<SkillListEntryUI>(true);
                if (templates != null && templates.Length > 0)
                    entryPrefab = templates[0];
            }
        }
    }

    private Transform BuildScrollView()
    {
        Transform header = transform.Find("CurrentSelectionBar");

        var scrollGo = new GameObject(ScrollViewName, typeof(RectTransform), typeof(ScrollRect));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(transform, false);
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.pivot = new Vector2(0.5f, 0.5f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;
        if (header is RectTransform headerRt)
            scrollRt.offsetMax = new Vector2(0f, -(headerRt.rect.height > 1f ? headerRt.rect.height : HeaderHeight));

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.SetParent(scrollRt, false);
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(viewportRt, false);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        scrollGo.transform.SetAsLastSibling();
        return scrollRt;
    }

    private Transform EnsureRowsContainer()
    {
        Transform rows = listContent.Find(RowsContainerName);
        if (rows != null)
            return listContent = rows;

        var rowsGo = new GameObject(RowsContainerName, typeof(RectTransform));
        rows = rowsGo.transform;
        rows.SetParent(listContent, false);
        var rt = (RectTransform)rows;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = rowsGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = RowSpacing;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        rowsGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        PurgeStaleListChildren(rows);

        return listContent = rows;
    }

    private void PurgeStaleListChildren(Transform rowsParent = null)
    {
        Transform parent = rowsParent != null ? listContent : transform;
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (rowsParent != null && child.name == RowsContainerName)
                continue;

            if (child.name == ScrollViewName || child.name == "CurrentSelectionBar")
                continue;

            if (child.GetComponent<AbilityEntryUI>() != null
                || child.GetComponentInChildren<AbilityEntryUI>(true) != null)
            {
                Destroy(child.gameObject);
            }
        }

        Transform scroll = transform.Find(ScrollViewName);
        if (scroll != null)
            PurgeAbilityRowsFromScroll(scroll);
    }

    private static void PurgeAbilityRowsFromScroll(Transform scroll)
    {
        Transform content = scroll.Find("Viewport/Content");
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child.name == RowsContainerName)
                continue;

            if (child.GetComponent<AbilityEntryUI>() != null
                || child.GetComponentInChildren<AbilityEntryUI>(true) != null
                || child.name.Contains("ActiveAbilities", StringComparison.OrdinalIgnoreCase))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private List<SkillDefinition> CollectSkillsForCategory(SkillCategory category)
    {
        var list = new List<SkillDefinition>();
        if (_skillDatabase?.Skills == null)
            return list;

        for (int i = 0; i < _skillDatabase.Skills.Count; i++)
        {
            SkillDefinition s = _skillDatabase.Skills[i];
            if (s != null && s.category == category)
                list.Add(s);
        }

        if (category == SkillCategory.Gathering)
            list.Sort(CompareGatheringSkillRowOrder);
        else
            list.Sort(CompareCombatSkillRowOrder);

        return list;
    }

    private static int CompareGatheringSkillRowOrder(SkillDefinition a, SkillDefinition b)
    {
        if (a == null || b == null)
            return 0;

        int Rank(SkillType t) => t switch
        {
            SkillType.Woodcutting => 0,
            SkillType.Mining => 1,
            SkillType.Fishing => 2,
            _ => 99
        };

        int cmp = Rank(a.skillType).CompareTo(Rank(b.skillType));
        if (cmp != 0)
            return cmp;

        return a.listSortOrder.CompareTo(b.listSortOrder);
    }

    private static int CompareCombatSkillRowOrder(SkillDefinition a, SkillDefinition b)
    {
        if (a == null || b == null)
            return 0;

        int cmp = a.listSortOrder.CompareTo(b.listSortOrder);
        return cmp != 0 ? cmp : a.skillType.CompareTo(b.skillType);
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            _skillsManager = SkillsManager.Instance;
        else if (_skillsManager == null)
            _skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }
}
