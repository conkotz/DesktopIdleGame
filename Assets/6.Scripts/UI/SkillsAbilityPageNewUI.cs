using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skills &amp; Abilities page (NEW layout): skill tab selection and horizontal timeline display.
/// Display-only for the timeline; does not change save data, unlock rules, or the vertical tree.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillsAbilityPageNewUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private SkillsManager skillsManager;

    [Header("Skill selection")]
    [Tooltip("Parent of per-skill tab buttons (e.g. SkillsTabBar). Button GameObject names must match SkillType.")]
    [SerializeField] private Transform skillTabBarRoot;

    [Header("Timeline")]
    [SerializeField] private HorizontalSkillTreeScaffoldUI horizontalSkillTimeline;

    [Header("Optional labels")]
    [SerializeField] private TMP_Text selectedSkillTitleText;

    private SkillDefinition _selectedSkill;
    private readonly Dictionary<SkillType, Button> _tabButtonBySkillType = new();
    private readonly Dictionary<SkillType, UnityEngine.Events.UnityAction> _tabClickHandlers = new();
    private bool _skillsEventsSubscribed;

    // Matches SkillsTabBar button styling in GamePlay.unity (Melee selected / Magic normal).
    private static readonly Color TabSelectedImageColor = new Color(0.36078432f, 0.26666668f, 0.12941177f, 1f);
    private static readonly Color TabNormalImageColor = new Color(0.18431373f, 0.16078432f, 0.13725491f, 1f);
    private static readonly Color TabSelectedOutlineColor = new Color(0.6039216f, 0.48235294f, 0.2627451f, 0.5f);
    private static readonly Vector2 TabSelectedOutlineDistance = new Vector2(4f, -4f);

    /// <summary>Currently selected skill for horizontal timeline build.</summary>
    public SkillDefinition SelectedSkill => _selectedSkill;

    public SkillDatabase SkillDatabase => skillDatabase;

    private void Awake()
    {
        PreferRuntimeSkillsManager();
        EnsureHorizontalTimelineReference();
    }

    private void OnEnable()
    {
        PreferRuntimeSkillsManager();
        WireSkillTabButtons();
        TrySubscribeSkillsEvents();
        SelectFirstSkillIfNeeded();
        RefreshView();
        RefreshTabSelectionVisuals();
    }

    private void OnDisable()
    {
        TryUnsubscribeSkillsEvents();
    }

    public void SelectSkill(SkillDefinition skill)
    {
        if (skill == null)
            return;

        _selectedSkill = skill;
        RefreshView();
        RefreshTabSelectionVisuals();
    }

    public void SelectSkill(SkillType skillType)
    {
        SkillDefinition skill = GetSkillByType(skillType);
        if (skill != null)
            SelectSkill(skill);
    }

    private void RefreshView()
    {
        PreferRuntimeSkillsManager();

        if (_selectedSkill == null)
        {
            if (selectedSkillTitleText != null)
                selectedSkillTitleText.text = "No Skill Selected";

            EnsureHorizontalTimelineReference();
            horizontalSkillTimeline?.Build(null);
            return;
        }

        int level = skillsManager != null ? skillsManager.GetLevel(_selectedSkill.skillType) : 1;
        string displayName = SkillsAbilityPresentationResolver.ResolveSkillDisplayName(_selectedSkill);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = _selectedSkill.skillType.ToString();

        if (selectedSkillTitleText != null)
            selectedSkillTitleText.text = $"{displayName} (lv {level})";

        EnsureHorizontalTimelineReference();
        if (horizontalSkillTimeline != null)
        {
            horizontalSkillTimeline.SetSelectedSkill(_selectedSkill);
            horizontalSkillTimeline.Build(_selectedSkill);
        }
    }

    private void WireSkillTabButtons()
    {
        _tabButtonBySkillType.Clear();
        if (skillTabBarRoot == null)
            return;

        Button[] buttons = skillTabBarRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            if (!Enum.TryParse(button.gameObject.name, ignoreCase: true, out SkillType skillType))
                continue;

            _tabButtonBySkillType[skillType] = button;
            if (_tabClickHandlers.TryGetValue(skillType, out UnityEngine.Events.UnityAction existing))
                button.onClick.RemoveListener(existing);

            SkillType captured = skillType;
            UnityEngine.Events.UnityAction handler = () => OnSkillTabClicked(captured);
            _tabClickHandlers[skillType] = handler;
            button.onClick.AddListener(handler);
        }
    }

    private void OnSkillTabClicked(SkillType skillType)
    {
        if (skillsManager != null)
            skillsManager.SetActiveXpDisplay(skillType, string.Empty);

        SelectSkill(skillType);
    }

    private void RefreshTabSelectionVisuals()
    {
        foreach (KeyValuePair<SkillType, Button> pair in _tabButtonBySkillType)
            ApplySkillTabVisual(pair.Value, _selectedSkill != null && _selectedSkill.skillType == pair.Key);
    }

    private static void ApplySkillTabVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();

        if (image != null)
            image.color = selected ? TabSelectedImageColor : TabNormalImageColor;

        // Keep white tint so Image.color drives the look (matches scene Button setup).
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (selected)
        {
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();

            outline.effectColor = TabSelectedOutlineColor;
            outline.effectDistance = TabSelectedOutlineDistance;
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }
    }

    private void SelectFirstSkillIfNeeded()
    {
        if (_selectedSkill != null)
            return;

        if (skillDatabase == null)
            return;

        PreferRuntimeSkillsManager();

        if (skillsManager != null)
        {
            SkillDefinition fromActiveXp = GetSkillByType(skillsManager.ActiveSkill);
            if (fromActiveXp != null)
            {
                _selectedSkill = fromActiveXp;
                return;
            }
        }

        _selectedSkill = GetSkillByType(SkillType.Melee)
                         ?? GetFirstSkillInCategory(SkillCategory.Combat)
                         ?? GetFirstSkillInCategory(SkillCategory.Gathering);
    }

    private SkillDefinition GetSkillByType(SkillType type)
    {
        if (skillDatabase == null || skillDatabase.Skills == null)
            return null;

        for (int i = 0; i < skillDatabase.Skills.Count; i++)
        {
            SkillDefinition s = skillDatabase.Skills[i];
            if (s != null && s.skillType == type)
                return s;
        }

        return null;
    }

    private SkillDefinition GetFirstSkillInCategory(SkillCategory category)
    {
        if (skillDatabase == null || skillDatabase.Skills == null)
            return null;

        for (int i = 0; i < skillDatabase.Skills.Count; i++)
        {
            SkillDefinition s = skillDatabase.Skills[i];
            if (s != null && s.category == category)
                return s;
        }

        return null;
    }

    private void EnsureHorizontalTimelineReference()
    {
        if (horizontalSkillTimeline == null)
            horizontalSkillTimeline = GetComponentInChildren<HorizontalSkillTreeScaffoldUI>(true);
    }

    private void PreferRuntimeSkillsManager()
    {
        if (SkillsManager.Instance != null)
            skillsManager = SkillsManager.Instance;
        else if (skillsManager == null)
            skillsManager = FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    private void TrySubscribeSkillsEvents()
    {
        if (_skillsEventsSubscribed)
            return;

        PreferRuntimeSkillsManager();
        if (skillsManager == null)
            return;

        skillsManager.OnLevelUp += HandleSkillsLevelChanged;
        skillsManager.OnSkillLevelDecreased += HandleSkillsLevelChanged;
        _skillsEventsSubscribed = true;
    }

    private void TryUnsubscribeSkillsEvents()
    {
        if (skillsManager == null)
            return;

        skillsManager.OnLevelUp -= HandleSkillsLevelChanged;
        skillsManager.OnSkillLevelDecreased -= HandleSkillsLevelChanged;
        _skillsEventsSubscribed = false;
    }

    private void HandleSkillsLevelChanged(SkillType type, int _)
    {
        if (!isActiveAndEnabled || _selectedSkill == null)
            return;

        if (_selectedSkill.skillType != type)
            return;

        RefreshView();
    }
}
