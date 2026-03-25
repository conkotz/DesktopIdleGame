using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DebuffIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;
    [SerializeField] private Color titleColor = new Color(1f, 0.45f, 0.45f);

    private string _title;
    private string _body;
    private bool _isPointerOver;
    private bool _hasValidData;

    public void SetData(
        Sprite sprite,
        int stacks,
        bool showOne = true,
        string title = "",
        string body = "",
        SharedTooltipUI sharedTooltip = null,
        RectTransform measureRect = null,
        RectTransform heightRect = null,
        FlipInsideBounds.PreferredSide side = FlipInsideBounds.PreferredSide.Right)
    {
        _hasValidData = sprite != null && stacks > 0;

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.raycastTarget = true;
        }

        if (stackText != null)
        {
            stackText.raycastTarget = false;

            bool shouldShow = stacks > 1 || (showOne && stacks == 1);
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