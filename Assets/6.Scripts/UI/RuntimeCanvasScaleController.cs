using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class RuntimeCanvasScaleController : MonoBehaviour
{
    [SerializeField] private SliderSettingId settingId = SliderSettingId.HudResize;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private Vector2 baseReferenceResolution = new Vector2(2560f, 1440f);

    private float _scaleMultiplier;

    private void Awake()
    {
        if (!canvasScaler)
            canvasScaler = GetComponent<CanvasScaler>();

        if (baseReferenceResolution.x <= 0f || baseReferenceResolution.y <= 0f)
            baseReferenceResolution = canvasScaler ? canvasScaler.referenceResolution : new Vector2(2560f, 1440f);

        if (!SliderSettingsStore.IsVisible(settingId))
        {
            enabled = false;
            return;
        }

        _scaleMultiplier = SliderSettingsStore.Get(settingId);
        ApplyScale();
    }

    private void OnEnable()
    {
        SliderSettingsStore.Changed += OnSliderSettingChanged;
    }

    private void OnDisable()
    {
        SliderSettingsStore.Changed -= OnSliderSettingChanged;
    }

    private void OnSliderSettingChanged(SliderSettingId setting, float value)
    {
        if (setting != settingId)
            return;

        _scaleMultiplier = value;
        ApplyScale();
    }

    private void ApplyScale()
    {
        if (!canvasScaler)
            return;

        float scale = Mathf.Max(0.01f, _scaleMultiplier);
        canvasScaler.referenceResolution = baseReferenceResolution / scale;
    }
}
