using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ToggleSettingsRowUI : MonoBehaviour
{
    [SerializeField] private ToggleSettingId settingId = ToggleSettingId.ShowPlayerHealthBarOutOfCombat;
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

    public void RefreshDisplay()
    {
        _refreshing = true;

        if (settingNameText)
            settingNameText.text = ToggleSettingsStore.GetDisplayName(settingId);
        if (toggle)
            toggle.isOn = ToggleSettingsStore.Get(settingId);

        _refreshing = false;
    }

    private void OnToggleChanged(bool value)
    {
        if (_refreshing)
            return;

        ToggleSettingsStore.Set(settingId, value);
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
