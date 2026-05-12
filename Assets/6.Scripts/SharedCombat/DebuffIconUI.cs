using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DebuffIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    [Header("Active Overlay")]
    [Tooltip("Optional overlay Image shown on top of the icon while the debuff is active. " +
             "Set Image Type = Filled (e.g. Radial 360) on this Image and the script will drive " +
             "fillAmount = remaining / total so the wedge sweeps down as the debuff expires. " +
             "Hidden automatically when no duration is provided.")]
    [SerializeField] private Image activeOverlay;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;
    [SerializeField] private Color titleColor = new Color(1f, 0.45f, 0.45f);

    private string _title;
    private string _body;
    private float _remainingSeconds;
    private float _totalDurationSeconds;
    private bool _isPointerOver;
    private bool _hasValidData;

    public void SetData(
        Sprite sprite,
        int stacks,
        string title = "",
        string body = "",
        SharedTooltipUI sharedTooltip = null,
        RectTransform measureRect = null,
        RectTransform heightRect = null,
        FlipInsideBounds.PreferredSide side = FlipInsideBounds.PreferredSide.Right,
        float remainingSeconds = 0f,
        float totalDurationSeconds = 0f)
    {
        _hasValidData = sprite != null && stacks > 0;
        _remainingSeconds = Mathf.Max(0f, remainingSeconds);
        _totalDurationSeconds = Mathf.Max(0f, totalDurationSeconds);

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.raycastTarget = true;
        }

        if (stackText != null)
        {
            stackText.raycastTarget = false;

            bool shouldShow = stacks > 0;
            stackText.gameObject.SetActive(shouldShow);

            if (shouldShow)
                stackText.text = stacks.ToString();
        }

        _title = title ?? "";
        _body = body ?? "";

        tooltip = sharedTooltip;
        tooltipMeasureRect = measureRect;
        tooltipHeightRect = heightRect;
        preferredSide = side;

        RefreshActiveOverlay();
        RefreshHoveredTooltip();
    }

    /// <summary>
    /// Update the remaining duration after the icon has been spawned (e.g. from a tick loop).
    /// </summary>
    public void UpdateTimer(float remainingSeconds)
    {
        _remainingSeconds = Mathf.Max(0f, remainingSeconds);
        RefreshActiveOverlay();
    }

    private void RefreshActiveOverlay()
    {
        if (activeOverlay == null)
            return;

        bool showOverlay = _totalDurationSeconds > 0f && _remainingSeconds > 0f;
        activeOverlay.gameObject.SetActive(showOverlay);

        if (!showOverlay)
            return;

        activeOverlay.raycastTarget = false;
        float fill = Mathf.Clamp01(_remainingSeconds / _totalDurationSeconds);
        if (activeOverlay.type == Image.Type.Filled)
            activeOverlay.fillAmount = fill;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        RefreshHoveredTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;
        tooltip?.Hide();
    }

    private bool CanShowTooltip()
    {
        return isActiveAndEnabled &&
               _hasValidData &&
               tooltip != null &&
               !string.IsNullOrWhiteSpace(_title);
    }

    private void RefreshHoveredTooltip()
    {
        if (!_isPointerOver)
            return;

        if (!CanShowTooltip())
        {
            tooltip?.Hide();
            return;
        }

        tooltip.ShowTextAt(
            transform,
            _title,
            _body,
            tooltipMeasureRect,
            tooltipHeightRect,
            preferredSide,
            titleColor);
    }

    private void OnDisable()
    {
        _isPointerOver = false;
        tooltip?.Hide();
    }

    private void OnDestroy()
    {
        if (_isPointerOver)
            tooltip?.Hide();
    }
}