using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ToggleSettingsRowUI : MonoBehaviour
{
    [SerializeField] private ToggleSettingId settingId = ToggleSettingId.ShowPlayerHealthBarOutOfCombat;

    public ToggleSettingId SettingIdValue => settingId;
    [Tooltip("When true, the Toggle is bound to the inverse of the stored setting (e.g. Show X vs Disable X).")]
    [SerializeField] private bool invertMeaningForUi;
    [SerializeField] private TMP_Text settingNameText;
    [SerializeField] private Toggle toggle;

    private bool _refreshing;

    private void Awake()
    {
        if (!settingNameText)
            settingNameText = transform.Find("SettingName")?.GetComponent<TMP_Text>();
        if (!toggle)
            toggle = GetComponentInChildren<Toggle>(true);
    }

    private void OnEnable()
    {
        if (toggle)
            toggle.onValueChanged.AddListener(OnToggleChanged);

        ToggleSettingsStore.Changed += OnSettingChanged;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (toggle)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);

        ToggleSettingsStore.Changed -= OnSettingChanged;
    }

    public void ConfigureSetting(ToggleSettingId id)
    {
        settingId = id;
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        _refreshing = true;

        if (settingNameText)
            settingNameText.text = ToggleSettingsStore.GetDisplayName(settingId);
        if (toggle)
        {
            bool stored = ToggleSettingsStore.Get(settingId);
            toggle.isOn = invertMeaningForUi ? !stored : stored;
        }

        _refreshing = false;
    }

    private void OnToggleChanged(bool value)
    {
        if (_refreshing)
            return;

        ToggleSettingsStore.Set(settingId, invertMeaningForUi ? !value : value);
    }

    private void OnSettingChanged(ToggleSettingId changedSetting, bool _)
    {
        if (changedSetting == settingId)
            RefreshDisplay();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (settingNameText)
            settingNameText.text = ToggleSettingsStore.GetDisplayName(settingId);
    }
#endif
}
