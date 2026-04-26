using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SharedTooltipUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text valueEachText;
    [SerializeField] private TMP_Text stackValueText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text customValueText; // optional now / not relied on

    [Header("Stats")]
    [SerializeField] private TMP_Text statsText;

    [Header("Rarity UI")]
    [SerializeField] private Image rarityBorder;
    [SerializeField] private Color defaultNameColor = Color.white;
    [SerializeField] private Color defaultBorderColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private FlipInsideBounds flipInsideBounds;

    [Tooltip("VLG + CSF tooltip box (e.g. child named Content). Rebuilt before flip positioning.")]
    [SerializeField] private RectTransform tooltipLayoutRoot;

    [Header("Scale")]
    [SerializeField] private Vector2 defaultTooltipScale = Vector2.one;
    [SerializeField] private Vector2 hudTooltipScale = new Vector2(0.8f, 0.8f);

    private RectTransform _defaultParent;
    private RectTransform _rt;

    private void Awake()
    {
        _rt = transform as RectTransform;
        _defaultParent = transform.parent as RectTransform;

        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

        if (!rarityBorder)
            rarityBorder = transform.Find("RarityBorder")?.GetComponent<Image>();

        if (!flipInsideBounds)
            flipInsideBounds = GetComponent<FlipInsideBounds>();

        if (!tooltipLayoutRoot)
        {
            Transform t = transform.Find("Content");
            if (t)
                tooltipLayoutRoot = t as RectTransform;
        }

        if (canvasGroup)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        Hide();
    }

    public void Show(
     ItemDefinition def,
     int stackAmount,
     int? valueOverride = null,
     string valueLabelOverride = null,
     string customValueOverride = null)
    {
        if (!def || !canvasGroup || !nameText)
            return;

        SetEquipmentCompactMode(false);

        var c = GetRarityColor(def.rarity);

        if (rarityBorder) rarityBorder.color = c;

        nameText.color = c;
        nameText.text = def.displayName;
        nameText.gameObject.SetActive(true);

        if (rarityText)
        {
            rarityText.text = def.rarity.ToString();
            rarityText.color = c;
            rarityText.gameObject.SetActive(true);
        }

        bool isEquip = def.equipSlot != EquipSlot.None;

        int each = Mathf.Max(0, valueOverride ?? def.value);
        string valueLabel = string.IsNullOrWhiteSpace(valueLabelOverride) ? "Value" : valueLabelOverride;

        string itemDescription = string.IsNullOrWhiteSpace(def.description) ? "" : def.description.Trim();
        string customBlock = string.IsNullOrWhiteSpace(customValueOverride) ? "" : customValueOverride.Trim();
        bool hasShopBlock = !string.IsNullOrWhiteSpace(customBlock);

        if (descriptionText)
        {
            descriptionText.text = itemDescription;
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(itemDescription));
        }

        if (statsText)
        {
            string stats = BuildTooltipStatsTextWithSupportRequirement(def);
            statsText.text = stats;
            statsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(stats));
        }

        if (hasShopBlock)
        {
            if (valueEachText)
            {
                valueEachText.text = "";
                valueEachText.gameObject.SetActive(false);
            }

            if (stackValueText)
            {
                stackValueText.text = "";
                stackValueText.gameObject.SetActive(false);
            }

            if (customValueText)
            {
                customValueText.text = customBlock;
                customValueText.enabled = true;
                customValueText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (customValueText)
            {
                customValueText.text = "";
                customValueText.enabled = false;
                customValueText.gameObject.SetActive(false);
            }

            if (valueEachText)
            {
                valueEachText.text = $"{valueLabel}: {FormatGold(each)}";
                valueEachText.gameObject.SetActive(true);
            }

            if (stackValueText)
            {
                if (!isEquip)
                {
                    stackAmount = Mathf.Max(0, stackAmount);
                    int stackValue = each * stackAmount;
                    stackValueText.text = $"Stack value: {FormatGold(stackValue)} ({stackAmount}×)";
                    stackValueText.gameObject.SetActive(true);
                }
                else
                {
                    stackValueText.text = "";
                    stackValueText.gameObject.SetActive(false);
                }
            }
        }

        if (_rt)
            _rt.localScale = new Vector3(defaultTooltipScale.x, defaultTooltipScale.y, 1f);

        RebuildTooltipLayoutNow();

        canvasGroup.alpha = 1f;
    }

    public void ShowForEquipment(ItemDefinition def)
    {
        if (!def || !canvasGroup || !nameText)
            return;

        SetEquipmentCompactMode(true);

        var c = GetRarityColor(def.rarity);

        if (rarityBorder) rarityBorder.color = c;

        nameText.color = c;
        nameText.text = def.displayName;
        nameText.gameObject.SetActive(true);

        if (rarityText)
        {
            rarityText.text = def.rarity.ToString();
            rarityText.color = c;
            rarityText.gameObject.SetActive(true);
        }

        if (statsText)
        {
            string stats = BuildTooltipStatsTextWithSupportRequirement(def);
            statsText.text = stats;
            statsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(stats));
        }

        if (descriptionText)
        {
            descriptionText.text = "";
            descriptionText.gameObject.SetActive(false);
        }

        if (customValueText)
        {
            customValueText.text = "";
            customValueText.enabled = false;
            customValueText.gameObject.SetActive(false);
        }

        if (_rt)
            _rt.localScale = new Vector3(defaultTooltipScale.x, defaultTooltipScale.y, 1f);

        RebuildTooltipLayoutNow();

        canvasGroup.alpha = 1f;
    }

    public void ShowText(string title, string body, Color? titleColor = null)
    {
        SetEquipmentCompactMode(false);

        if (nameText)
        {
            nameText.text = title ?? "";
            nameText.color = titleColor ?? defaultNameColor;
            nameText.gameObject.SetActive(true);
        }

        if (rarityText)
        {
            rarityText.text = "";
            rarityText.gameObject.SetActive(false);
        }

        if (valueEachText)
        {
            valueEachText.text = "";
            valueEachText.gameObject.SetActive(false);
        }

        if (stackValueText)
        {
            stackValueText.text = "";
            stackValueText.gameObject.SetActive(false);
        }

        if (descriptionText)
        {
            descriptionText.text = body ?? "";
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
        }

        if (statsText)
        {
            statsText.text = "";
            statsText.gameObject.SetActive(false);
        }

        if (customValueText)
        {
            customValueText.text = "";
            customValueText.enabled = false;
            customValueText.gameObject.SetActive(false);
        }

        if (rarityBorder)
            rarityBorder.color = defaultBorderColor;

        RebuildTooltipLayoutNow();

        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Ensures ContentSizeFitter / layout groups apply before <see cref="FlipInsideBounds"/> runs in LateUpdate.
    /// </summary>
    private void RebuildTooltipLayoutNow()
    {
        Canvas.ForceUpdateCanvases();
        if (tooltipLayoutRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipLayoutRoot);
        if (_rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        RestoreDefaultParent();

        if (_rt)
        {
            _rt.anchoredPosition = Vector2.zero;
            _rt.localScale = new Vector3(defaultTooltipScale.x, defaultTooltipScale.y, 1f);
        }

        if (canvasGroup)
            canvasGroup.alpha = 0f;

        if (nameText)
        {
            nameText.text = "";
            nameText.color = defaultNameColor;
            nameText.gameObject.SetActive(true);
        }

        if (rarityText)
        {
            rarityText.text = "";
            rarityText.gameObject.SetActive(false);
        }

        if (valueEachText)
        {
            valueEachText.text = "";
            valueEachText.gameObject.SetActive(false);
        }

        if (stackValueText)
        {
            stackValueText.text = "";
            stackValueText.gameObject.SetActive(false);
        }

        if (descriptionText)
        {
            descriptionText.text = "";
            descriptionText.gameObject.SetActive(false);
        }

        if (statsText)
        {
            statsText.text = "";
            statsText.gameObject.SetActive(false);
        }

        if (customValueText)
        {
            customValueText.text = "";
            customValueText.enabled = false;
            customValueText.gameObject.SetActive(false);
        }

        if (rarityBorder)
            rarityBorder.color = defaultBorderColor;
    }

    private void SetEquipmentCompactMode(bool compact)
    {
        if (valueEachText)
        {
            valueEachText.text = "";
            valueEachText.gameObject.SetActive(false);
        }

        if (stackValueText)
        {
            stackValueText.text = "";
            stackValueText.gameObject.SetActive(false);
        }

        if (descriptionText && compact)
        {
            descriptionText.text = "";
            descriptionText.gameObject.SetActive(false);
        }

        if (customValueText)
        {
            customValueText.text = "";
            customValueText.enabled = false;
            customValueText.gameObject.SetActive(false);
        }

        if (nameText) nameText.gameObject.SetActive(true);
        if (rarityText) rarityText.gameObject.SetActive(true);
    }

    private static string FormatGold(int g)
    {
        if (g >= 1000) return $"{(g / 1000f):0.#}k gold";
        return $"{g} gold";
    }

    private static Color GetRarityColor(ItemRarity r)
    {
        return r switch
        {
            ItemRarity.Common => new Color(0.85f, 0.85f, 0.85f),
            ItemRarity.Uncommon => new Color(0.45f, 0.9f, 0.55f),
            ItemRarity.Rare => new Color(0.45f, 0.7f, 1f),
            ItemRarity.Epic => new Color(0.75f, 0.5f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.75f, 0.25f),
            _ => Color.white
        };
    }

    private string BuildTooltipStatsTextWithSupportRequirement(ItemDefinition def)
    {
        if (!def)
            return "";

        string stats = def.BuildTooltipStatsText() ?? "";

        if (!def.RequiresOffhandSupport || def.RequiredSupportType == CombatSupportType.None)
            return stats;

        bool hasRequirementEquipped = HasRequiredSupportEquipped(def.RequiredSupportType);

        string colour = hasRequirementEquipped ? "#55DD55" : "#FF5555";
        string reqLine = $"<color={colour}>Requires: {def.RequiredSupportType}</color>";

        stats = ReplaceLineStartingWith(stats, "Requires:", reqLine, out bool replaced);
        if (replaced)
            return stats;

        if (string.IsNullOrWhiteSpace(stats))
            return reqLine;

        return stats.TrimEnd('\n') + "\n" + reqLine;
    }

    private static string ReplaceLineStartingWith(
        string block,
        string startsWith,
        string replacement,
        out bool replaced)
    {
        replaced = false;

        if (string.IsNullOrWhiteSpace(block) || string.IsNullOrWhiteSpace(startsWith))
            return block ?? "";

        string[] lines = block.Replace("\r\n", "\n").Split('\n');
        System.Text.StringBuilder sb = new System.Text.StringBuilder(block.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i] ?? "";
            if (line.TrimStart().StartsWith(startsWith))
            {
                line = replacement ?? "";
                replaced = true;
            }

            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append(line);
        }

        return sb.ToString();
    }

    private bool HasRequiredSupportEquipped(CombatSupportType requiredType)
    {
        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!player)
            return false;

        var equipment = player.GetComponent<EquipmentManager>();
        if (!equipment)
            return false;

        var offDef = equipment.GetOffHandDef();
        if (!offDef || !offDef.IsCombatSupport)
            return false;

        return offDef.SupportType == requiredType;
    }

    private void RestoreDefaultParent()
    {
        if (_rt == null || _defaultParent == null)
            return;

        if (_rt.parent != _defaultParent)
            _rt.SetParent(_defaultParent, false);
    }

    public void SetAnchor(Transform anchor)
    {
        if (!anchor || _rt == null)
            return;

        _rt.SetParent(anchor, false);
        _rt.anchoredPosition = Vector2.zero;
        _rt.localScale = new Vector3(defaultTooltipScale.x, defaultTooltipScale.y, 1f);
        _rt.SetAsLastSibling();
    }

    public void ShowAt(
        Transform anchor,
        ItemDefinition def,
        int amount,
        bool compact,
        int? valueOverride = null,
        string valueLabelOverride = null,
        string customValueOverride = null)
    {
        if (!anchor || !def)
        {
            Hide();
            return;
        }

        RestoreDefaultParent();
        transform.SetAsLastSibling();
        SetAnchor(anchor);

        if (compact)
            ShowForEquipment(def);
        else
            Show(def, amount, valueOverride, valueLabelOverride, customValueOverride);
    }

    private void BringToFront()
    {
        transform.SetAsLastSibling();
    }

    public void ConfigureDocking(
        RectTransform anchor,
        RectTransform heightRect = null,
        FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right)
    {
        if (!flipInsideBounds) return;

        flipInsideBounds.SetMeasureRect(anchor);
        flipInsideBounds.SetHeightRect(heightRect ? heightRect : anchor);
        flipInsideBounds.SetPreferredSide(preferredSide);
    }

    public void ShowTextAt(
        Transform anchor,
        string title,
        string body,
        RectTransform measureRect = null,
        RectTransform heightRect = null,
        FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right,
        Color? titleColor = null)
    {
        if (!anchor)
        {
            Hide();
            return;
        }

        RestoreDefaultParent();
        transform.SetAsLastSibling();
        SetAnchor(anchor);

        if (_rt != null)
            _rt.localScale = new Vector3(hudTooltipScale.x, hudTooltipScale.y, 1f);

        if (flipInsideBounds)
        {
            RectTransform anchorRect = anchor as RectTransform;
            flipInsideBounds.SetMeasureRect(measureRect ? measureRect : anchorRect);
            flipInsideBounds.SetHeightRect(heightRect ? heightRect : (measureRect ? measureRect : anchorRect));
            flipInsideBounds.SetPreferredSide(preferredSide);
        }

        ShowText(title, body, titleColor);

        if (_rt != null)
            _rt.localScale = new Vector3(hudTooltipScale.x, hudTooltipScale.y, 1f);
    }

}