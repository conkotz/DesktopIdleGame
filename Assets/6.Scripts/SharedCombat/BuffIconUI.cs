using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuffIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private TMP_Text timerText;

    [Header("Active Overlay")]
    [Tooltip("Optional overlay Image shown on top of the icon while a finite-duration buff is active. " +
             "Set Image Type = Filled (e.g. Radial 360) on this Image and the script will drive " +
             "fillAmount = remaining / total so the wedge sweeps down as the buff expires. " +
             "Hidden for indefinite buffs and when duration has ended.")]
    [SerializeField] private Image activeOverlay;

    [Header("Tooltip")]
    [SerializeField] private SharedTooltipUI tooltip;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private RectTransform tooltipHeightRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;
    [SerializeField] private Color titleColor = new Color(0.45f, 1f, 0.55f);

    private string _title;
    private string _body;
    private float _remainingSeconds;
    private float _totalDurationSeconds;
    private bool _persistActiveOverlay;
    private bool _isPointerOver;
    private bool _hasValidData;
    private bool _dismissOnRightClick;
    private Action _onRightClickDismiss;

    public void SetData(
        Sprite sprite,
        string valueLabel,
        float remainingSeconds,
        string title,
        string body,
        SharedTooltipUI sharedTooltip = null,
        RectTransform measureRect = null,
        RectTransform heightRect = null,
        FlipInsideBounds.PreferredSide side = FlipInsideBounds.PreferredSide.Right,
        int stacks = 0,
        bool showStacks = false,
        float totalDurationSeconds = 0f,
        bool persistActiveOverlay = false)
    {
        _hasValidData = sprite != null;
        _totalDurationSeconds = Mathf.Max(0f, totalDurationSeconds);
        _persistActiveOverlay = persistActiveOverlay;

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.raycastTarget = true;
        }

        if (stackText != null)
        {
            stackText.raycastTarget = false;

            // Single-instance buffs (Lumber Frenzy, Cleaving Chop, Spectral Axe, Soulforged Weapon, …)
            // pass stacks=1 just to keep the slot active. We only surface the count when it actually
            // conveys information — i.e. multiple stacks like Cleaving Strikes' swing charges.
            bool shouldShowStacks = showStacks && stacks > 1;
            stackText.gameObject.SetActive(shouldShowStacks);

            if (shouldShowStacks)
                stackText.text = stacks.ToString();
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

    public void SetRightClickDismissHandler(Action onDismiss)
    {
        _onRightClickDismiss = onDismiss;
        _dismissOnRightClick = onDismiss != null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;

        if (!_dismissOnRightClick)
            return;

        _onRightClickDismiss?.Invoke();
    }

    public void UpdateTimer(float remainingSeconds)
    {
        _remainingSeconds = remainingSeconds;

        if (timerText != null)
        {
            bool showTimer = !_persistActiveOverlay && remainingSeconds > 0f;
            timerText.gameObject.SetActive(showTimer);

            if (showTimer)
                timerText.text = Mathf.CeilToInt(remainingSeconds).ToString();
        }

        RefreshActiveOverlay();
        RefreshHoveredTooltip();
    }

    public void UpdateStacksAndValue(int stacks, bool showStacks, string valueLabel)
    {
        if (stackText != null)
        {
            bool shouldShowStacks = showStacks && stacks > 1;
            stackText.gameObject.SetActive(shouldShowStacks);

            if (shouldShowStacks)
                stackText.text = stacks.ToString();
        }

        if (valueText != null)
        {
            bool showValue = !string.IsNullOrWhiteSpace(valueLabel);
            valueText.gameObject.SetActive(showValue);

            if (showValue)
                valueText.text = valueLabel;
        }
    }

    private void RefreshActiveOverlay()
    {
        if (activeOverlay == null)
            return;

        bool showOverlay = !_persistActiveOverlay && _totalDurationSeconds > 0f && _remainingSeconds > 0f;
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

    /// <summary>Omit "Remaining: …" when duration is over (e.g. Cleaving Strikes hit-charge tail); timer text already hidden in that case.</summary>
    private string BuildTooltipBodyWithOptionalRemaining()
    {
        string remainingLine = null;
        if (_persistActiveOverlay)
            remainingLine = "Remaining: Until dismissed";
        else if (_remainingSeconds > 0f)
            remainingLine = $"Remaining: {Mathf.CeilToInt(_remainingSeconds)}s";

        if (string.IsNullOrWhiteSpace(_body))
            return remainingLine ?? "";

        if (string.IsNullOrEmpty(remainingLine))
            return _body;

        return $"{_body}\n{remainingLine}";
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

        string body = BuildTooltipBodyWithOptionalRemaining();

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