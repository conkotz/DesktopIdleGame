using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Narrows the details panel to one prefab column width; the other three bottom panels stay visible
/// and share the remaining bar width equally. Expands details back for ability / major / capstone nodes.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilityBottomPanelLayoutUI : MonoBehaviour
{
    private const float CollapsedSidePanelFlex = 1f;

    [SerializeField] private RectTransform bottomPanelBar;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private GameObject abilityListPanel;
    [SerializeField] private GameObject unlocksPanel;
    [SerializeField] private GameObject skillsProgressPanel;

    [Header("Widths")]
    [Tooltip("DetailsPanel width when collapsed (≈ one column of SkillNodeDetailsPanelUI).")]
    [SerializeField] private float collapsedDetailsPreferredWidth = 302f;

    private LayoutElement _detailsLayout;
    private LayoutElement _abilityListLayout;
    private LayoutElement _unlocksLayout;
    private LayoutElement _skillsProgressLayout;

    private float _expandedDetailsFlex = 0.7f;
    private float _expandedAbilityListFlex = 0.2f;
    private float _expandedUnlocksFlex = 0.3f;
    private float _expandedSkillsProgressFlex = 0.2f;

    private bool _isExpanded;

    public bool IsExpanded => _isExpanded;

    /// <summary>Raised after <see cref="SetExpanded"/> applies layout (argument = expanded).</summary>
    public event System.Action<bool> ExpandedChanged;

    private void Awake()
    {
        if (collapsedDetailsPreferredWidth < 1f)
            collapsedDetailsPreferredWidth = SkillNodeDetailsPanelUI.PanelWidth / 3f;

        EnsureReferences();
        CacheExpandedFlexWeights();
        SetExpanded(false, force: true);
    }

    public void ApplyForBinding(SkillTimelineNodeBinding binding)
    {
        SetExpanded(SkillsAbilityBottomPanelLayoutRules.ShouldExpandBottomBar(binding));
    }

    public void SetExpanded(bool expanded, bool force = false)
    {
        EnsureReferences();
        if (!force && _isExpanded == expanded)
            return;

        _isExpanded = expanded;

        if (expanded)
            ApplyExpandedLayout();
        else
            ApplyCollapsedLayout();

        if (bottomPanelBar != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottomPanelBar);

        RefreshDetailsPanelColumns();
        ExpandedChanged?.Invoke(_isExpanded);
    }

    private void RefreshDetailsPanelColumns()
    {
        if (detailsPanel == null)
            return;

        SkillNodeDetailsPanelUI details =
            detailsPanel.GetComponentInChildren<SkillNodeDetailsPanelUI>(true);
        details?.RefreshColumnsLayout();
    }

    private void ApplyCollapsedLayout()
    {
        EnsureSidePanelsVisible();

        float narrowWidth = Mathf.Max(200f, collapsedDetailsPreferredWidth);

        if (_detailsLayout != null)
        {
            _detailsLayout.flexibleWidth = 0f;
            _detailsLayout.preferredWidth = narrowWidth;
            _detailsLayout.minWidth = narrowWidth;
        }

        ApplyEqualFlexToSidePanels();
    }

    private void ApplyExpandedLayout()
    {
        EnsureSidePanelsVisible();

        if (_detailsLayout != null)
        {
            _detailsLayout.flexibleWidth = _expandedDetailsFlex;
            _detailsLayout.preferredWidth = -1f;
            _detailsLayout.minWidth = -1f;
        }

        if (_abilityListLayout != null)
        {
            _abilityListLayout.flexibleWidth = _expandedAbilityListFlex;
            _abilityListLayout.preferredWidth = -1f;
            _abilityListLayout.minWidth = -1f;
        }

        if (_unlocksLayout != null)
        {
            _unlocksLayout.flexibleWidth = _expandedUnlocksFlex;
            _unlocksLayout.preferredWidth = -1f;
            _unlocksLayout.minWidth = -1f;
        }

        if (_skillsProgressLayout != null)
        {
            _skillsProgressLayout.flexibleWidth = _expandedSkillsProgressFlex;
            _skillsProgressLayout.preferredWidth = -1f;
            _skillsProgressLayout.minWidth = -1f;
        }
    }

    private void EnsureSidePanelsVisible()
    {
        if (abilityListPanel != null)
            abilityListPanel.SetActive(true);
        if (unlocksPanel != null)
            unlocksPanel.SetActive(true);
        if (skillsProgressPanel != null)
            skillsProgressPanel.SetActive(true);
    }

    private void ApplyEqualFlexToSidePanels()
    {
        if (_abilityListLayout != null)
        {
            _abilityListLayout.flexibleWidth = CollapsedSidePanelFlex;
            _abilityListLayout.preferredWidth = -1f;
            _abilityListLayout.minWidth = -1f;
        }

        if (_unlocksLayout != null)
        {
            _unlocksLayout.flexibleWidth = CollapsedSidePanelFlex;
            _unlocksLayout.preferredWidth = -1f;
            _unlocksLayout.minWidth = -1f;
        }

        if (_skillsProgressLayout != null)
        {
            _skillsProgressLayout.flexibleWidth = CollapsedSidePanelFlex;
            _skillsProgressLayout.preferredWidth = -1f;
            _skillsProgressLayout.minWidth = -1f;
        }
    }

    private void EnsureReferences()
    {
        if (bottomPanelBar == null)
            bottomPanelBar = transform as RectTransform;

        Transform root = bottomPanelBar != null ? bottomPanelBar : transform;

        if (detailsPanel == null)
            detailsPanel = root.Find("DetailsPanel")?.gameObject;
        if (abilityListPanel == null)
            abilityListPanel = root.Find("AbilityList")?.gameObject;
        if (unlocksPanel == null)
            unlocksPanel = root.Find("Unlocks")?.gameObject;
        if (skillsProgressPanel == null)
            skillsProgressPanel = root.Find("SkillsProgress")?.gameObject;

        _detailsLayout = GetOrAddLayoutElement(detailsPanel);
        _abilityListLayout = GetOrAddLayoutElement(abilityListPanel);
        _unlocksLayout = GetOrAddLayoutElement(unlocksPanel);
        _skillsProgressLayout = GetOrAddLayoutElement(skillsProgressPanel);
    }

    private void CacheExpandedFlexWeights()
    {
        if (_detailsLayout != null && _detailsLayout.flexibleWidth > 0f)
            _expandedDetailsFlex = _detailsLayout.flexibleWidth;
        if (_abilityListLayout != null && _abilityListLayout.flexibleWidth > 0f)
            _expandedAbilityListFlex = _abilityListLayout.flexibleWidth;
        if (_unlocksLayout != null && _unlocksLayout.flexibleWidth > 0f)
            _expandedUnlocksFlex = _unlocksLayout.flexibleWidth;
        if (_skillsProgressLayout != null && _skillsProgressLayout.flexibleWidth > 0f)
            _expandedSkillsProgressFlex = _skillsProgressLayout.flexibleWidth;
    }

    private static LayoutElement GetOrAddLayoutElement(GameObject go)
    {
        if (go == null)
            return null;

        LayoutElement layout = go.GetComponent<LayoutElement>();
        if (layout == null)
            layout = go.AddComponent<LayoutElement>();
        return layout;
    }
}
