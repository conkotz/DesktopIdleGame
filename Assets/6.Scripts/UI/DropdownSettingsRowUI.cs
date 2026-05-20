using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DropdownSettingsRowUI : MonoBehaviour
{
    [SerializeField] private DropdownSettingId settingId = DropdownSettingId.Unassigned;
    [SerializeField] private TMP_Text settingNameText;
    [SerializeField] private TMP_Dropdown dropdown;

    [Tooltip("Optional labels for this row instance. When set, overrides DropdownSettingsStore options.")]
    [SerializeField] private string[] optionLabelsOverride;

    [Tooltip("Used when settingId is Unassigned but optionLabelsOverride is set.")]
    [SerializeField] private string standalonePrefsKey = "Settings.Dropdown.Custom";

    [SerializeField] private int standaloneDefaultIndex;

    private bool _refreshing;
    private bool _usesStandalonePrefs;
    private string _activePrefsKey;
    private readonly List<string> _resolvedLabels = new();

    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();

        if (!TryResolveOptions(out _, out _))
        {
            gameObject.SetActive(false);
            return;
        }

        if (dropdown)
            dropdown.onValueChanged.AddListener(OnDropdownChanged);

        DropdownSettingsStore.Changed += OnGlobalSettingChanged;
        GlobalUserSettings.RestoredDefaults += OnGlobalRestoredDefaults;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (dropdown)
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);

        DropdownSettingsStore.Changed -= OnGlobalSettingChanged;
        GlobalUserSettings.RestoredDefaults -= OnGlobalRestoredDefaults;
    }

    private void OnGlobalRestoredDefaults() => RefreshDisplay();

    public void RefreshDisplay()
    {
        _refreshing = true;

        if (!TryResolveOptions(out IReadOnlyList<string> labels, out int storedIndex))
        {
            _refreshing = false;
            return;
        }

        if (settingNameText)
            settingNameText.text = ResolveSettingLabel();

        if (dropdown)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(labels));
            dropdown.SetValueWithoutNotify(Mathf.Clamp(storedIndex, 0, labels.Count - 1));
            dropdown.RefreshShownValue();
        }

        _refreshing = false;
    }

    private string ResolveSettingLabel()
    {
        if (settingId != DropdownSettingId.Unassigned)
            return DropdownSettingsStore.GetDisplayName(settingId);

        if (settingNameText && !string.IsNullOrWhiteSpace(settingNameText.text))
            return settingNameText.text;

        return "Dropdown setting";
    }

    private bool TryResolveOptions(out IReadOnlyList<string> labels, out int storedIndex)
    {
        labels = _resolvedLabels;
        _resolvedLabels.Clear();
        _usesStandalonePrefs = false;
        _activePrefsKey = null;

        if (optionLabelsOverride != null)
        {
            for (int i = 0; i < optionLabelsOverride.Length; i++)
            {
                string label = optionLabelsOverride[i];
                if (!string.IsNullOrWhiteSpace(label))
                    _resolvedLabels.Add(label.Trim());
            }
        }

        if (_resolvedLabels.Count == 0 && settingId != DropdownSettingId.Unassigned)
        {
            IReadOnlyList<string> storeLabels = DropdownSettingsStore.GetOptionLabels(settingId);
            for (int i = 0; i < storeLabels.Count; i++)
                _resolvedLabels.Add(storeLabels[i]);
        }

        if (_resolvedLabels.Count == 0)
        {
            storedIndex = 0;
            return false;
        }

        if (settingId != DropdownSettingId.Unassigned && DropdownSettingsStore.HasOptions(settingId))
        {
            storedIndex = DropdownSettingsStore.Get(settingId);
            return true;
        }

        _usesStandalonePrefs = true;
        _activePrefsKey = string.IsNullOrWhiteSpace(standalonePrefsKey)
            ? "Settings.Dropdown.Custom"
            : standalonePrefsKey.Trim();
        storedIndex = DropdownSettingsStore.GetStandalone(
            _activePrefsKey,
            standaloneDefaultIndex,
            _resolvedLabels.Count);
        return true;
    }

    private void OnDropdownChanged(int index)
    {
        if (_refreshing)
            return;

        if (_usesStandalonePrefs)
        {
            DropdownSettingsStore.SetStandalone(
                _activePrefsKey,
                index,
                _resolvedLabels.Count,
                standaloneDefaultIndex);
            return;
        }

        DropdownSettingsStore.Set(settingId, index);
    }

    private void OnGlobalSettingChanged(DropdownSettingId changedSetting, int _)
    {
        if (!_usesStandalonePrefs && changedSetting == settingId)
            RefreshDisplay();
    }

    private void ResolveReferences()
    {
        if (!settingNameText)
            settingNameText = transform.Find("SettingName")?.GetComponent<TMP_Text>();
        if (!settingNameText)
            settingNameText = transform.Find("ActionLabelText")?.GetComponent<TMP_Text>();

        if (!dropdown)
            dropdown = GetComponentInChildren<TMP_Dropdown>(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        if (settingNameText && settingId != DropdownSettingId.Unassigned)
            settingNameText.text = DropdownSettingsStore.GetDisplayName(settingId);
    }
#endif
}
