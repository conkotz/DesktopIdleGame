using UnityEngine;

/// <summary>
/// Applies <see cref="SliderSettingId.HudLeftResize"/> as a uniform multiplier on this transform's authored
/// <see cref="Transform.localScale"/> (from the prefab / scene). Stacks with strip <see cref="RuntimeCanvasScaleController"/> HUD scaling.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class HudLeftPanelScaleController : MonoBehaviour
{
    private Vector3 _authoredLocalScale;
    private RectTransform _rect;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _authoredLocalScale = _rect.localScale;
        ApplyScale();
    }

    private void OnEnable()
    {
        SliderSettingsStore.Changed += OnSliderChanged;
        ApplyScale();
    }

    private void OnDisable()
    {
        SliderSettingsStore.Changed -= OnSliderChanged;
    }

    private void OnSliderChanged(SliderSettingId id, float _)
    {
        if (id != SliderSettingId.HudLeftResize)
            return;
        ApplyScale();
    }

    private void ApplyScale()
    {
        if (!_rect)
            _rect = (RectTransform)transform;

        float m = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.HudLeftResize));
        _rect.localScale = _authoredLocalScale * m;
    }
}
