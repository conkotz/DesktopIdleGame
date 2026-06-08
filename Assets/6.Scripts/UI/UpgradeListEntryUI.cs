using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeListEntryUI : MonoBehaviour
{
    private static readonly Color NormalRowColor = new(0.16862746f, 0.12941177f, 0.09411765f, 0.8509804f);
    private static readonly Color SelectedRowColor = new(0.32f, 0.26f, 0.18f, 0.95f);
    private static readonly Color NormalTextColor = new(0.96862745f, 0.88235295f, 0.74509805f, 1f);
    private static readonly Color DimTextColor = new(0.96862745f, 0.88235295f, 0.74509805f, 0.65f);

    [SerializeField] private Button button;
    [SerializeField] private Image rowBackground;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text upgradeValueText;
    [SerializeField] private Image hasScrollImage;
    [SerializeField] private CanvasGroup rowCanvasGroup;

    public ItemDefinition BoundScroll { get; private set; }

    private void Awake()
    {
        if (!button)
            button = GetComponent<Button>();
        if (!rowBackground)
            rowBackground = GetComponent<Image>();

        if (!nameText)
            nameText = transform.Find("RowGroup/NameText")?.GetComponent<TMP_Text>();
        if (!upgradeValueText)
            upgradeValueText = transform.Find("RowGroup/UpgradeValueText")?.GetComponent<TMP_Text>();
        if (!hasScrollImage)
            hasScrollImage = transform.Find("RowGroup/HasScroll")?.GetComponent<Image>();

        if (nameText)
            nameText.raycastTarget = false;
        if (upgradeValueText)
            upgradeValueText.raycastTarget = false;
        if (hasScrollImage)
            hasScrollImage.raycastTarget = false;
    }

    public void BindOption(
        EnhancementOptionEntry option,
        bool canPay,
        bool selected,
        bool unavailableForSelectedGear,
        Sprite hasScrollSprite,
        Sprite missingScrollSprite,
        Action<EnhancementOptionEntry> onClicked)
    {
        BoundScroll = null;
        bool dimRow = unavailableForSelectedGear;

        if (nameText)
        {
            nameText.text = option != null ? UpgradeOptionDisplay.FormatOptionType(option) : string.Empty;
            nameText.color = dimRow ? DimTextColor : NormalTextColor;
        }

        if (upgradeValueText)
        {
            upgradeValueText.text = option != null ? UpgradeOptionDisplay.FormatOptionValue(option) : string.Empty;
            upgradeValueText.color = dimRow ? DimTextColor : NormalTextColor;
        }

        if (hasScrollImage)
        {
            hasScrollImage.enabled = true;
            hasScrollImage.sprite = canPay ? hasScrollSprite : missingScrollSprite;
            hasScrollImage.preserveAspect = true;
            hasScrollImage.color = dimRow ? DimTextColor : Color.white;
        }

        if (rowBackground)
            rowBackground.color = selected ? SelectedRowColor : NormalRowColor;

        EnsureRowCanvasGroup();
        rowCanvasGroup.alpha = dimRow ? 0.72f : 1f;

        if (!button)
            return;

        button.interactable = true;
        button.onClick.RemoveAllListeners();
        if (option != null && onClicked != null)
            button.onClick.AddListener(() => onClicked(option));
    }

    public void Bind(
        ItemDefinition scroll,
        bool playerHasScroll,
        bool selected,
        bool unavailableForSelectedGear,
        Sprite hasScrollSprite,
        Sprite missingScrollSprite,
        Action<ItemDefinition> onClicked)
    {
        BoundScroll = scroll;
        bool dimRow = unavailableForSelectedGear;

        if (nameText)
        {
            nameText.text = scroll != null ? UpgradeScrollDisplay.FormatOptionName(scroll) : string.Empty;
            nameText.color = dimRow ? DimTextColor : NormalTextColor;
        }

        if (upgradeValueText)
        {
            upgradeValueText.text = scroll != null ? UpgradeScrollDisplay.FormatOptionValueDescription(scroll) : string.Empty;
            upgradeValueText.color = dimRow ? DimTextColor : NormalTextColor;
        }

        if (hasScrollImage)
        {
            hasScrollImage.enabled = true;
            hasScrollImage.sprite = playerHasScroll ? hasScrollSprite : missingScrollSprite;
            hasScrollImage.preserveAspect = true;
            hasScrollImage.color = dimRow ? DimTextColor : Color.white;
        }

        if (rowBackground)
            rowBackground.color = selected ? SelectedRowColor : NormalRowColor;

        EnsureRowCanvasGroup();
        rowCanvasGroup.alpha = dimRow ? 0.72f : 1f;

        if (!button)
            return;

        button.interactable = true;
        button.onClick.RemoveAllListeners();
        if (scroll != null && onClicked != null)
            button.onClick.AddListener(() => onClicked(scroll));
    }

    private void EnsureRowCanvasGroup()
    {
        if (rowCanvasGroup)
            return;

        rowCanvasGroup = GetComponent<CanvasGroup>();
        if (!rowCanvasGroup)
            rowCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
