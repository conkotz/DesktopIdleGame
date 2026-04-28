using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SliderSettingsRowUI : MonoBehaviour
{
    [SerializeField] private SliderSettingId settingId = SliderSettingId.HudResize;
    [SerializeField] private TMP_Text settingNameText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Scrollbar slider;

    private bool _refreshing;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!SliderSettingsStore.IsVisible(settingId))
        {
            gameObject.SetActive(false);
            return;
        }

        ConfigureSlider();

        if (slider)
            slider.onValueChanged.AddListener(OnSliderChanged);

        SliderSettingsStore.Changed += OnSettingChanged;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (slider)
            slider.onValueChanged.RemoveListener(OnSliderChanged);

        SliderSettingsStore.Changed -= OnSettingChanged;
    }

    public void RefreshDisplay()
    {
        _refreshing = true;

        float min = SliderSettingsStore.GetMin(settingId);
        float max = SliderSettingsStore.GetMax(settingId);
        float value = SliderSettingsStore.Get(settingId);

        if (settingNameText)
            settingNameText.text = SliderSettingsStore.GetDisplayName(settingId);

        if (slider)
            slider.SetValueWithoutNotify(Mathf.InverseLerp(min, max, value));

        RefreshValueText(value);
        _refreshing = false;
    }

    private void OnSliderChanged(float normalizedValue)
    {
        if (_refreshing)
            return;

        float min = SliderSettingsStore.GetMin(settingId);
        float max = SliderSettingsStore.GetMax(settingId);
        SliderSettingsStore.Set(settingId, Mathf.Lerp(min, max, normalizedValue));
    }

    private void OnSettingChanged(SliderSettingId changedSetting, float _)
    {
        if (changedSetting == settingId)
            RefreshDisplay();
    }

    private void RefreshValueText(float value)
    {
        if (valueText)
            valueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void ResolveReferences()
    {
        if (!settingNameText)
            settingNameText = transform.Find("SettingName")?.GetComponent<TMP_Text>();
        if (!settingNameText)
            settingNameText = transform.Find("ActionLabelText")?.GetComponent<TMP_Text>();
        if (!valueText)
            valueText = transform.Find("ValueText")?.GetComponent<TMP_Text>();

        if (!slider)
            slider = GetComponentInChildren<Scrollbar>(true);
    }

    private void ConfigureSlider()
    {
        if (!slider)
            return;

        slider.direction = Scrollbar.Direction.LeftToRight;
        slider.numberOfSteps = 0;
        slider.size = 0.06f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();

        if (settingNameText)
            settingNameText.text = SliderSettingsStore.GetDisplayName(settingId);
        RefreshValueText(SliderSettingsStore.Get(settingId));
    }
#endif
}
