using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One cell in <see cref="UndoShopWindowUI"/> — shows a recently sold stack; click selects for the bar Undo action.
/// </summary>
public class UndoShopSlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button button;
    [SerializeField] private Image slotBackground;

    [Header("Selection")]
    [SerializeField] private Color normalBackground = new Color(0.12f, 0.14f, 0.17f, 1f);
    [SerializeField] private Color selectedBackground = new Color(0.28f, 0.22f, 0.1f, 1f);

    private int _entryId = -1;
    private Action<int> _onSelected;

    public int BoundEntryId => _entryId;

    private void Awake()
    {
        if (!slotBackground)
            slotBackground = GetComponent<Image>();

        EnsureMaskableGraphics();

        if (icon) icon.raycastTarget = false;
        if (stockText) stockText.raycastTarget = false;
        if (priceText) priceText.raycastTarget = false;

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    private void EnsureMaskableGraphics()
    {
        if (slotBackground) slotBackground.maskable = true;
        if (icon) icon.maskable = true;
        if (stockText) stockText.maskable = true;
        if (priceText) priceText.maskable = true;
    }

    public void Bind(int entryId, Sprite itemIcon, int amount, int gold, Action<int> onSelected)
    {
        _entryId = entryId;
        _onSelected = onSelected;

        if (icon)
        {
            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null;
            icon.preserveAspect = true;
        }

        if (stockText)
            stockText.text = amount > 0 ? $"x{amount}" : "";

        if (priceText)
        {
            priceText.gameObject.SetActive(true);
            priceText.text = gold > 0 ? $"{gold}g" : "";
        }

        if (button)
            button.interactable = entryId > 0;

        ApplySelectionVisual(false);
    }

    public void SetSelected(bool selected)
    {
        ApplySelectionVisual(selected);
    }

    private void ApplySelectionVisual(bool selected)
    {
        if (slotBackground)
            slotBackground.color = selected ? selectedBackground : normalBackground;
    }

    private void OnClicked()
    {
        if (_entryId > 0)
            _onSelected?.Invoke(_entryId);
    }
}
