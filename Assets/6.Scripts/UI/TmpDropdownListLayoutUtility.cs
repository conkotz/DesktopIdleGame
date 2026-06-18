using TMPro;
using UnityEngine;

/// <summary>Shared TMP dropdown list sizing for settings rows with long option labels.</summary>
public static class TmpDropdownListLayoutUtility
{
    public const float ItemHeight = 40f;
    public const float TemplateHeight = 220f;

    public static void ApplyTallListItems(TMP_Dropdown dropdown)
    {
        if (!dropdown || !dropdown.template)
            return;

        RectTransform templateRt = dropdown.template;
        templateRt.sizeDelta = new Vector2(templateRt.sizeDelta.x, TemplateHeight);

        Transform content = templateRt.Find("Viewport/Content");
        if (content is RectTransform contentRt)
            contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, ItemHeight);

        Transform item = content != null ? content.Find("Item") : null;
        if (item is RectTransform itemRt)
            itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, ItemHeight);

        TMP_Text itemLabel = dropdown.itemText;
        if (!itemLabel && item != null)
            itemLabel = item.Find("Item Label")?.GetComponent<TMP_Text>();

        if (itemLabel)
        {
            itemLabel.textWrappingMode = TextWrappingModes.Normal;
            itemLabel.overflowMode = TextOverflowModes.Overflow;
            itemLabel.enableAutoSizing = false;
            itemLabel.fontSize = Mathf.Min(itemLabel.fontSize, 14f);
        }
    }
}
