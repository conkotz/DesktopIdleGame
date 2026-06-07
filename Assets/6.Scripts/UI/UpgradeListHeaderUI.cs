using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeListHeaderUI : MonoBehaviour
{
    public const int ListRightPadding = 20;

    private static readonly Color HeaderTextColor = new(0.22745098f, 0.19607843f, 0.16470589f, 1f);

    private void Awake()
    {
        BuildIfNeeded();
    }

    public void BuildIfNeeded()
    {
        if (transform.childCount > 0)
            return;

        RectTransform rowGroup = CreateRowGroup(transform);
        CreateHeaderCell(rowGroup, "Type", TextAlignmentOptions.Left, flexibleWidth: 1);
        CreateHeaderCell(rowGroup, "Value", TextAlignmentOptions.Left, preferredWidth: 150);
        CreateHeaderCell(rowGroup, "Has scroll", TextAlignmentOptions.Center, preferredWidth: 28);

        LayoutElement layout = gameObject.GetComponent<LayoutElement>();
        if (!layout)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 30f;
        layout.flexibleWidth = 1f;
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
    }
}
