using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings row: enable toggle plus Set 1 / Set 2 conditional dropdowns.
/// Attach to <c>SettingsConditionalDropdownrow</c> prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConditionalAutoBattleSettingsRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text enableLabelText;
    [SerializeField] private Toggle enableToggle;
    [SerializeField] private TMP_Text set1LabelText;
    [SerializeField] private TMP_Dropdown set1Dropdown;
    [SerializeField] private TMP_Text set2LabelText;
    [SerializeField] private TMP_Dropdown set2Dropdown;

    private bool _refreshing;
    private readonly List<string> _optionLabels = new();

    private void Awake()
    {
        ResolveReferences();
        ConfigureDropdownLayouts();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureDropdownLayouts();

        if (enableToggle)
            enableToggle.onValueChanged.AddListener(OnEnableToggleChanged);
        if (set1Dropdown)
            set1Dropdown.onValueChanged.AddListener(OnSet1DropdownChanged);
        if (set2Dropdown)
            set2Dropdown.onValueChanged.AddListener(OnSet2DropdownChanged);

        ConditionalAutoBattleSettingsStore.Changed += OnStoreChanged;
        GlobalUserSettings.RestoredDefaults += OnGlobalRestoredDefaults;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (enableToggle)
            enableToggle.onValueChanged.RemoveListener(OnEnableToggleChanged);
        if (set1Dropdown)
            set1Dropdown.onValueChanged.RemoveListener(OnSet1DropdownChanged);
        if (set2Dropdown)
            set2Dropdown.onValueChanged.RemoveListener(OnSet2DropdownChanged);

        ConditionalAutoBattleSettingsStore.Changed -= OnStoreChanged;
        GlobalUserSettings.RestoredDefaults -= OnGlobalRestoredDefaults;
    }

    public void RefreshDisplay()
    {
        _refreshing = true;

        if (enableLabelText)
            enableLabelText.text = ConditionalAutoBattleSettingsStore.DisplayName;

        if (set1LabelText)
            set1LabelText.text = "Set 1";
        if (set2LabelText)
            set2LabelText.text = "Set 2";

        BuildOptionLabels();
        ApplyDropdownOptions(set1Dropdown, ConditionalAutoBattleSettingsStore.GetSet1Condition());
        ApplyDropdownOptions(set2Dropdown, ConditionalAutoBattleSettingsStore.GetSet2Condition());

        if (enableToggle)
            enableToggle.SetIsOnWithoutNotify(ConditionalAutoBattleSettingsStore.Enabled);

        ApplyDropdownInteractable();

        _refreshing = false;
    }

    private void BuildOptionLabels()
    {
        _optionLabels.Clear();
        for (int i = 0; i < ConditionalAutoBattleSettingsStore.ConditionCount; i++)
        {
            var condition = (ConditionalAutoBattleCondition)i;
            _optionLabels.Add(ConditionalAutoBattleSettingsStore.GetDisplayLabel(condition));
        }
    }

    private void ApplyDropdownOptions(TMP_Dropdown dropdown, ConditionalAutoBattleCondition selected)
    {
        if (!dropdown)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(_optionLabels));
        dropdown.SetValueWithoutNotify((int)selected);
        dropdown.RefreshShownValue();
    }

    private void ApplyDropdownInteractable()
    {
        bool enabled = enableToggle != null && enableToggle.isOn;
        if (set1Dropdown)
            set1Dropdown.interactable = enabled;
        if (set2Dropdown)
            set2Dropdown.interactable = enabled;
    }

    private void OnEnableToggleChanged(bool value)
    {
        if (_refreshing)
            return;

        ConditionalAutoBattleSettingsStore.Enabled = value;
        ApplyDropdownInteractable();
    }

    private void OnSet1DropdownChanged(int index)
    {
        if (_refreshing)
            return;

        ConditionalAutoBattleSettingsStore.SetSet1Condition((ConditionalAutoBattleCondition)index);
        RefreshDropdownSelectionsOnly();
    }

    private void OnSet2DropdownChanged(int index)
    {
        if (_refreshing)
            return;

        ConditionalAutoBattleSettingsStore.SetSet2Condition((ConditionalAutoBattleCondition)index);
        RefreshDropdownSelectionsOnly();
    }

    private void RefreshDropdownSelectionsOnly()
    {
        _refreshing = true;
        if (set1Dropdown)
        {
            set1Dropdown.SetValueWithoutNotify((int)ConditionalAutoBattleSettingsStore.GetSet1Condition());
            set1Dropdown.RefreshShownValue();
        }

        if (set2Dropdown)
        {
            set2Dropdown.SetValueWithoutNotify((int)ConditionalAutoBattleSettingsStore.GetSet2Condition());
            set2Dropdown.RefreshShownValue();
        }

        _refreshing = false;
    }

    private void OnStoreChanged() => RefreshDisplay();

    private void OnGlobalRestoredDefaults() => RefreshDisplay();

    private void ConfigureDropdownLayouts()
    {
        TmpDropdownListLayoutUtility.ApplyTallListItems(set1Dropdown);
        TmpDropdownListLayoutUtility.ApplyTallListItems(set2Dropdown);
    }

    private void ResolveReferences()
    {
        if (!enableLabelText)
            enableLabelText = transform.Find("ToggleRow/ActionLabelText")?.GetComponent<TMP_Text>();

        if (!enableToggle)
            enableToggle = transform.Find("ToggleRow/Toggle")?.GetComponent<Toggle>();

        if (!set1LabelText)
            set1LabelText = transform.Find("DropdownsRow/Set1Column/ColumnLabel")?.GetComponent<TMP_Text>();
        if (!set1Dropdown)
            set1Dropdown = transform.Find("DropdownsRow/Set1Column/Dropdown")?.GetComponent<TMP_Dropdown>();

        if (!set2LabelText)
            set2LabelText = transform.Find("DropdownsRow/Set2Column/ColumnLabel")?.GetComponent<TMP_Text>();
        if (!set2Dropdown)
            set2Dropdown = transform.Find("DropdownsRow/Set2Column/Dropdown")?.GetComponent<TMP_Dropdown>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (enableLabelText)
            enableLabelText.text = ConditionalAutoBattleSettingsStore.DisplayName;
        if (set1LabelText)
            set1LabelText.text = "Set 1";
        if (set2LabelText)
            set2LabelText.text = "Set 2";
    }
#endif
}
