using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One item row in the database Items section.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseItemEntryRowUI : MonoBehaviour
{
    private const float RowHeight = 100f;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text locationsText;
    [SerializeField] private Image iconImage;

    private void Awake()
    {
        ResolveReferences();
        EnsureRowLayout();
    }

    public void Bind(ItemDefinition item, SharedTooltipUI tooltip)
    {
        ResolveReferences();
        EnsureRowLayout();

        if (nameText)
            nameText.text = item != null ? item.displayName : string.Empty;

        if (locationsText)
        {
            string locations = item != null ? DatabaseItemSourceCatalog.FormatObtainLocations(item) : string.Empty;
            locationsText.text = string.IsNullOrEmpty(locations) ? "—" : locations;
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

            if (!iconImage.TryGetComponent(out DatabaseLootTableEntryUI lootEntryUi))
                lootEntryUi = iconImage.gameObject.AddComponent<DatabaseLootTableEntryUI>();

            lootEntryUi.Bind(item, tooltip, dropChance: 0f, isEliteDrop: false, amountMin: 1, amountMax: 1);
        }
    }

    private void EnsureRowLayout()
    {
        RectTransform row = transform as RectTransform;
        if (!row)
            return;

        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, RowHeight);

        LayoutElement rowElement = GetComponent<LayoutElement>();
        if (!rowElement)
            rowElement = gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = RowHeight;
        rowElement.preferredHeight = RowHeight;
        rowElement.flexibleWidth = 1f;
    }

    private void ResolveReferences()
    {
        if (!nameText)
            nameText = transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (!locationsText)
            locationsText = transform.Find("LocationsRow/LocationsText")?.GetComponent<TMP_Text>();
        if (!iconImage)
            iconImage = transform.Find("IconBackground/LootTableEntryItem")?.GetComponent<Image>();
    }
}
