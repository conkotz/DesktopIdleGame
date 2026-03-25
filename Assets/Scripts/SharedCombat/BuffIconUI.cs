using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuffIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text timerText;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;
    [SerializeField] private Color titleColor = new Color(0.45f, 1f, 0.55f);

    private string _title;
    private string _body;
    private float _remainingSeconds;
    private bool _isPointerOver;
    private bool _hasValidData;

    public void SetData(
        Sprite sprite,
        string valueLabel,
        float remainingSeconds,
        string title,
        string body,
        SharedTooltipUI sharedTooltip = null,
        RectTransform measureRect = null,
        RectTransform heightRect = null,
        FlipInsideBounds.PreferredSide side = FlipInsideBounds.PreferredSide.Right)
    {
        _hasValidData = sprite != null;

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.raycastTarget = true;
        }

        if (valueText != null)
        {
            valueText.raycastTarget = false;

            bool showValue = !string.IsNullOrWhiteSpace(valueLabel);
            valueText.gameObject.SetActive(showValue);

            if (showValue)
                valueText.text = valueLabel;
        }

        if (timerText != null)
            timerText.raycastTarget = false;

        _title = title ?? "";
        _body = body ?? "";

        tooltip = sharedTooltip;
        tooltipMeasureRect = measureRect;
        tooltipHeightRect = heightRect;
        preferredSide = side;

        UpdateTimer(remainingSeconds);
        RefreshHoveredTooltip();
    }

    public void UpdateTimer(float remainingSeconds)
    {
        _remainingSeconds = remainingSeconds;

        if (timerText != null)
        {
            bool showTimer = remainingSeconds > 0f;
            timerText.gameObject.SetActive(showTimer);

            if (showTimer)
                timerText.text = Mathf.CeilToInt(remainingSeconds).ToString();
        }

        RefreshHoveredTooltip();
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

        string body = string.IsNullOrWhiteSpace(_body)
            ? $"Remaining: {Mathf.CeilToInt(_remainingSeconds)}s"
            : $"{_body}\nRemaining: {Mathf.CeilToInt(_remainingSeconds)}s";

        tooltip.ShowTextAt(
            transform,
            _title,
            body,
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