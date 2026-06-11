using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One item row in the database Items section.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseItemEntryRowUI : MonoBehaviour
{
    private const string ObtainLocationPrefix = "Obtained location: ";

    private const float RowHeight = 114f;
    private const float RowPadding = 8f;
    private const float IconSize = 88f;
    private const float TextGap = 12f;
    private const float NameBandHeight = 38f;
    private const float BandGap = 4f;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text locationsText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Transform iconBackground;
    [SerializeField] private Transform locationsRow;

    public string ItemId { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        EnsureRowMetrics();
        ApplyChildLayout();
    }

    public void Bind(ItemDefinition item, SharedTooltipUI tooltip, DatabaseItemSubtab subtab)
    {
        ResolveReferences();
        EnsureRowMetrics();
        ApplyChildLayout();

        ItemId = item != null ? item.itemId : null;

        if (nameText)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = item != null ? DatabaseItemCatalog.FormatDatabaseDisplayName(item) : string.Empty;
        }

        if (locationsText)
        {
            string locations = item != null
                ? DatabaseItemCatalog.FormatDatabaseObtainLocations(item, subtab)
                : string.Empty;
            locationsText.text = string.IsNullOrEmpty(locations)
                ? $"{ObtainLocationPrefix}—"
                : $"{ObtainLocationPrefix}{locations}";
        }

        if (!iconImage)
            iconImage = transform.Find("IconBackground/LootTableEntryItem")?.GetComponent<Image>();

        if (iconImage)
        {
            Sprite sprite = item != null ? item.icon : null;
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            iconImage.raycastTarget = item != null;

            if (iconImage.TryGetComponent(out DatabaseLootTableEntryUI lootEntryUi))
                Destroy(lootEntryUi);

            if (!iconImage.TryGetComponent(out DatabaseItemIconTooltipUI tooltipUi))
                tooltipUi = iconImage.gameObject.AddComponent<DatabaseItemIconTooltipUI>();

            tooltipUi.Bind(item, tooltip);
        }

        if (iconBackground)
            iconBackground.SetSiblingIndex(0);
        if (locationsRow)
            locationsRow.SetSiblingIndex(1);
        if (nameText)
            nameText.transform.SetAsLastSibling();
    }

    private void EnsureRowMetrics()
    {
        if (TryGetComponent(out VerticalLayoutGroup rowLayout))
            rowLayout.enabled = false;

        LayoutElement rowElement = GetComponent<LayoutElement>();
        if (!rowElement)
            rowElement = gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = RowHeight;
        rowElement.preferredHeight = RowHeight;
        rowElement.flexibleWidth = 1f;
    }

    private void ApplyChildLayout()
    {
        float textLeft = RowPadding + IconSize + TextGap;
        float locationsTop = RowPadding + NameBandHeight + BandGap;
        float locationsHeight = RowHeight - locationsTop - RowPadding;

        if (nameText)
            ConfigureTopBand(nameText.rectTransform, textLeft, RowPadding, RowPadding, NameBandHeight);

        if (locationsRow is RectTransform locationsRect)
            ConfigureTopBand(locationsRect, textLeft, RowPadding, locationsTop, locationsHeight);

        if (locationsText)
            ConfigureStretchFill(locationsText.rectTransform);
    }

    private static void ConfigureTopBand(
        RectTransform band,
        float leftInset,
        float rightInset,
        float topInset,
        float height)
    {
        if (!band)
            return;

        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.pivot = new Vector2(0f, 1f);
        band.offsetMin = new Vector2(leftInset, -(topInset + height));
        band.offsetMax = new Vector2(-rightInset, -topInset);
    }

    private static void ConfigureStretchFill(RectTransform rect)
    {
        if (!rect)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ResolveReferences()
    {
        if (!nameText)
            nameText = transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (!locationsText)
        {
            locationsText = transform.Find("LocationsRow/LocationsourceText")?.GetComponent<TMP_Text>()
                ?? transform.Find("LocationsRow/LocationsText")?.GetComponent<TMP_Text>();
        }
        if (!iconBackground)
            iconBackground = transform.Find("IconBackground");
        if (!locationsRow)
            locationsRow = transform.Find("LocationsRow");
        if (!iconImage)
            iconImage = transform.Find("IconBackground/LootTableEntryItem")?.GetComponent<Image>();
    }
}

