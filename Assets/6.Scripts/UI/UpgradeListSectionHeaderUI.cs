using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeListSectionHeaderUI : MonoBehaviour
{
    private static readonly Color HeaderTextColor = new(0.22745098f, 0.19607843f, 0.16470589f, 1f);

    [SerializeField] private TMP_Text titleText;

    private void Awake()
    {
        if (!titleText)
            titleText = GetComponentInChildren<TMP_Text>(true);
    }

    public void SetTitle(string title)
    {
        if (!titleText)
            titleText = GetComponentInChildren<TMP_Text>(true);
        if (titleText)
            titleText.text = title ?? string.Empty;
    }

    public static UpgradeListSectionHeaderUI Create(RectTransform parent, string title)
    {
        GameObject root = new GameObject("UpgradeListSectionHeader", typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 28f);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 28f;
        layout.flexibleWidth = 1f;

        GameObject textGo = new GameObject("TitleText", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-UpgradeListHeaderUI.ListRightPadding, 0f);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = title ?? string.Empty;
        tmp.fontSize = 15f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = HeaderTextColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;

        UpgradeListSectionHeaderUI header = root.AddComponent<UpgradeListSectionHeaderUI>();
        header.titleText = tmp;
        return header;
    }
}
