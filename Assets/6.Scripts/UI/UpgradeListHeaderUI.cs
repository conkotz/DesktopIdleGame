using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeListHeaderUI : MonoBehaviour
{
    public const int ListRightPadding = 20;
    public const float MaterialsColumnWidth = 96f;

    private static readonly Color HeaderTextColor = new(0.22745098f, 0.19607843f, 0.16470589f, 1f);

    private void Awake()
    {
        BuildIfNeeded();
    }

    public void BuildIfNeeded()
    {
        if (transform.childCount > 0)
        {
            UpdateColumnLabel("ScrollHeader", "Has Materials");
            UpdateColumnLabel("HasscrollHeader", "Has Materials");
            UpdateColumnLabel("HasMaterialsHeader", "Has Materials");
            ApplyMaterialsColumnWidth();
            return;
        }

        RectTransform rowGroup = CreateRowGroup(transform);
        CreateHeaderCell(rowGroup, "Type", TextAlignmentOptions.Left, flexibleWidth: 1);
        CreateHeaderCell(rowGroup, "Value", TextAlignmentOptions.Left, preferredWidth: 150);
        CreateHeaderCell(rowGroup, "Has Materials", TextAlignmentOptions.Center, preferredWidth: MaterialsColumnWidth);

        LayoutElement layout = gameObject.GetComponent<LayoutElement>();
        if (!layout)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 30f;
        layout.flexibleWidth = 1f;
    }

    public static UpgradeListHeaderUI Create(RectTransform parent)
    {
        GameObject headerGo = new GameObject("UpgradeListColumnHeader", typeof(RectTransform), typeof(UpgradeListHeaderUI));
        headerGo.transform.SetParent(parent, false);

        RectTransform headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 30f);

        UpgradeListHeaderUI header = headerGo.GetComponent<UpgradeListHeaderUI>();
        header.BuildIfNeeded();
        return header;
    }

    private void ApplyMaterialsColumnWidth()
    {
        if (transform.childCount == 0)
            return;

        Transform rowGroup = transform.GetChild(0);
        ApplyMaterialsColumnWidth(rowGroup.Find("ScrollHeader"));
        ApplyMaterialsColumnWidth(rowGroup.Find("HasscrollHeader"));
        ApplyMaterialsColumnWidth(rowGroup.Find("HasMaterialsHeader"));
    }

    private static void ApplyMaterialsColumnWidth(Transform cell)
    {
        if (!cell)
            return;

        LayoutElement layoutElement = cell.GetComponent<LayoutElement>();
        if (layoutElement != null)
            layoutElement.preferredWidth = MaterialsColumnWidth;

        TextMeshProUGUI text = cell.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private static RectTransform CreateRowGroup(Transform parent)
    {
        GameObject rowGo = new GameObject("RowGroup", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = Vector2.zero;
        rowRect.anchorMax = Vector2.one;
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, ListRightPadding, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        return rowRect;
    }

    private void UpdateColumnLabel(string childName, string label)
    {
        Transform cell = null;
        if (transform.childCount > 0)
            cell = transform.GetChild(0).Find(childName);

        if (!cell)
            return;

        TextMeshProUGUI text = cell.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = label;
    }

    private static void CreateHeaderCell(
        RectTransform parent,
        string label,
        TextAlignmentOptions alignment,
        float flexibleWidth = -1f,
        float preferredWidth = -1f)
    {
        GameObject cellGo = new GameObject(label.Replace(" ", string.Empty) + "Header", typeof(RectTransform));
        cellGo.transform.SetParent(parent, false);

        LayoutElement layoutElement = cellGo.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 28f;
        if (preferredWidth > 0f)
            layoutElement.preferredWidth = preferredWidth;
        if (flexibleWidth > 0f)
            layoutElement.flexibleWidth = flexibleWidth;

        TextMeshProUGUI text = cellGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 14f;
        text.fontStyle = FontStyles.Bold;
        text.color = HeaderTextColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.margin = Vector4.zero;

        if (string.Equals(label, "Has Materials", System.StringComparison.Ordinal))
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
