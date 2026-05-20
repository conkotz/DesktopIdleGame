using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum SkillTreeTooltipChrome
{
    None,
    /// <summary>Matches ability / enhancement tooltips: dark panel, visible frame, TMP rich text for orange value lines.</summary>
    MajorPassivePanel
}

public class SharedTooltipUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;

    [Header("Enhancement (gear upgrade stars)")]
    [Tooltip("Parent for a row of star Images (e.g. Content/EnhancementSlots). Add a HorizontalLayoutGroup in the editor.")]
    [SerializeField] private RectTransform enhancementIconsRow;

    [Tooltip("Optional: legacy single-star or extra UI under Content/EnhancementSuccess — hidden when the star row is used.")]
    [SerializeField] private GameObject enhancementLegacySuccessRoot;

    [SerializeField] private Sprite enhancementStarEmptySprite;
    [SerializeField] private Sprite enhancementStarFilledSprite;

    [SerializeField] private Color enhancementStarColor = Color.white;

    [SerializeField] private float enhancementStarCellSize = 18f;

    [Tooltip("Smaller title for plain-text tooltips (e.g. stats panel hovers). When used, NameText is hidden.")]
    [SerializeField] private TMP_Text statsOnlyNameText;

    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text valueEachText;
    [SerializeField] private TMP_Text stackValueText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text customValueText; // optional now / not relied on

    [Header("Misc stats (tier, requirements, type, tool line…)")]
    [FormerlySerializedAs("statsText")]
    [SerializeField] private TMP_Text miscStatsText;

    [Header("Main stats (damage, gather rates, armor, support bonuses…)")]
    [SerializeField] private TMP_Text mainStatsText;

    [Header("Rarity UI")]
    [SerializeField] private Image rarityBorder;
    [SerializeField] private Color defaultNameColor = Color.white;
    [SerializeField] private Color defaultBorderColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private FlipInsideBounds flipInsideBounds;

    [Header("Skill tree (major passive presentation)")]
    [Tooltip("Optional root panel fill. If empty, uses an Image on this tooltip root when it is not RarityBorder.")]
    [SerializeField] private Image skillTreePanelBackdrop;
    [SerializeField] private Color skillTreeMajorBackdropColor = new Color(0.07f, 0.09f, 0.14f, 0.98f);
    [SerializeField] private Color skillTreeMajorBorderColor = new Color(0.88f, 0.9f, 0.94f, 0.55f);

    [Tooltip("VLG + CSF tooltip box (e.g. child named Content). Rebuilt before flip positioning.")]
    [SerializeField] private RectTransform tooltipLayoutRoot;

    [Header("Content width (TMP)")]
    [Tooltip("Minimum total width of the Content rect (short lines still get this width).")]
    [SerializeField] private float tooltipContentMinWidth = 220f;

    [Tooltip("Maximum total width before TMP wraps and the panel grows vertically.")]
    [SerializeField] private float tooltipContentMaxWidth = 400f;

    [Header("Section spacing")]
    [Tooltip("Extra space below the description block (TMP margin). Newlines alone often do not change layout height with VLG/CSF.")]
    [SerializeField] private float spacingAfterDescriptionPixels = 8f;

    [Tooltip("Extra space below main stats, or below the combined stats block when MainStatsText is not used.")]
    [SerializeField] private float spacingAfterMainStatsPixels = 8f;

    [Header("Scale")]
    [SerializeField] private Vector2 defaultTooltipScale = Vector2.one;
    [SerializeField] private Vector2 hudTooltipScale = new Vector2(0.8f, 0.8f);

    private RectTransform _defaultParent;
    private RectTransform _rt;
    private bool _restoreParentQueued;

    /// <summary>When parented to a slot/window, used to cancel that hierarchy's lossy scale so tooltip size follows <see cref="SliderSettingId.TooltipResize"/> only.</summary>
    private Transform _scaleAnchor;

    private bool _useHudTooltipScalePath;

    private LayoutElement _tooltipContentLayoutElement;

    private Vector4 _marginBaseDescription;
    private Vector4 _marginBaseMisc;
    private Vector4 _marginBaseMain;
    private bool _tooltipMarginBasesCaptured;

    private SkillTreeTooltipChrome _activeSkillTreeChrome;
    private Color _storedBorderColorForChrome;
    private bool _capturedBorderColorForChrome;
    private Color _storedBackdropColorForChrome;
    private bool _capturedBackdropColorForChrome;

    private void Awake()
    {
        _rt = transform as RectTransform;
        _defaultParent = transform.parent as RectTransform;

        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

        if (!rarityBorder)
            rarityBorder = transform.Find("RarityBorder")?.GetComponent<Image>();

        if (!flipInsideBounds)
            flipInsideBounds = GetComponent<FlipInsideBounds>();

        if (!skillTreePanelBackdrop)
        {
            var rootImg = GetComponent<Image>();
            if (rootImg && rootImg != rarityBorder)
                skillTreePanelBackdrop = rootImg;
        }

        if (!tooltipLayoutRoot)
        {
            Transform t = transform.Find("Content");
            if (t)
                tooltipLayoutRoot = t as RectTransform;
        }

        ConfigureTooltipContentForDynamicWidth();
        ResolveOptionalSplitStatsTextRefs();
        ResolveEnhancementRowRefs();

        if (canvasGroup)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        Hide();
    }

    private void OnEnable()
    {
        SliderSettingsStore.Changed += OnSliderSettingsChanged;
    }

    private void OnDisable()
    {
        SliderSettingsStore.Changed -= OnSliderSettingsChanged;
    }

    private void LateUpdate()
    {
        if (!canvasGroup || canvasGroup.alpha < 0.01f || !_scaleAnchor)
            return;

        ApplyDockedTooltipScale();
    }

    private void OnSliderSettingsChanged(SliderSettingId id, float _)
    {
        if (id != SliderSettingId.TooltipResize)
            return;

        if (canvasGroup && canvasGroup.alpha > 0.01f)
            ApplyDockedTooltipScale();
    }

    /// <summary>
    /// World-space size ≈ <paramref name="baseScale"/> × tooltip slider; parent chain scale (e.g. per-window resize) is divided out when docked.
    /// </summary>
    private void ApplyDockedTooltipScale()
    {
        if (!_rt)
            return;

        Vector2 baseScale = _useHudTooltipScalePath ? hudTooltipScale : defaultTooltipScale;
        float ax = _scaleAnchor ? Mathf.Max(0.001f, _scaleAnchor.lossyScale.x) : 1f;
        float tipMul = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.TooltipResize));
        float f = tipMul / ax;
        _rt.localScale = new Vector3(baseScale.x * f, baseScale.y * f, 1f);
    }

    private void ResetTooltipLocalScaleToDefaultAuthored()
    {
        if (!_rt)
            return;

        _rt.localScale = new Vector3(defaultTooltipScale.x, defaultTooltipScale.y, 1f);
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

        RestoreSkillTreeChromeIfNeeded();
        ResetTooltipSectionMargins();

        SetEquipmentCompactMode(false);

        var c = GetRarityColor(def.rarity);

        if (rarityBorder) rarityBorder.color = c;

        HideStatsOnlyNameHeader();

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
        bool showMaxStackSize =
            def.itemKind != ItemKind.Weapon &&
            def.itemKind != ItemKind.Armor &&
            def.itemKind != ItemKind.CombatSupport &&
            def.itemKind != ItemKind.Jewelry;

        int each = Mathf.Max(0, valueOverride ?? def.value);
        string valueLabel = string.IsNullOrWhiteSpace(valueLabelOverride) ? "Value" : valueLabelOverride;

        string itemDescription = string.IsNullOrWhiteSpace(def.description) ? "" : def.description.Trim();
        string customBlock = string.IsNullOrWhiteSpace(customValueOverride) ? "" : customValueOverride.Trim();
        bool hasShopBlock = !string.IsNullOrWhiteSpace(customBlock);

        if (descriptionText)
        {
            descriptionText.text = itemDescription;
            bool hasDesc = !string.IsNullOrWhiteSpace(itemDescription);
            descriptionText.margin = hasDesc
                ? WithExtraBottomMargin(_marginBaseDescription, spacingAfterDescriptionPixels)
                : _marginBaseDescription;
            descriptionText.gameObject.SetActive(hasDesc);
        }

        BindTooltipStats(def);
        BindEnhancementDisplay(def);

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
                customValueText.text = FormatShopValueBlock(customBlock);
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
                    if (showMaxStackSize)
                        stackValueText.text += $"\nMax stack size: {Mathf.Max(1, def.maxStack)}";
                    stackValueText.gameObject.SetActive(true);
                }
                else
                {
                    stackValueText.text = "";
                    stackValueText.gameObject.SetActive(false);
                }
            }
        }

        ApplyDockedTooltipScale();

        RebuildTooltipLayoutNow();

        canvasGroup.alpha = 1f;
    }

    public void ShowForEquipment(ItemDefinition def)
    {
        if (!def || !canvasGroup || !nameText)
            return;

        RestoreSkillTreeChromeIfNeeded();
        ResetTooltipSectionMargins();

        SetEquipmentCompactMode(true);

        var c = GetRarityColor(def.rarity);

        if (rarityBorder) rarityBorder.color = c;

        HideStatsOnlyNameHeader();

        nameText.color = c;
        nameText.text = def.displayName;
        nameText.gameObject.SetActive(true);

        if (rarityText)
        {
            rarityText.text = def.rarity.ToString();
            rarityText.color = c;
            rarityText.gameObject.SetActive(true);
        }

        BindTooltipStats(def);
        BindEnhancementDisplay(def);

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

        ApplyDockedTooltipScale();

        RebuildTooltipLayoutNow();

        canvasGroup.alpha = 1f;
    }

    /// <param name="useStatsDisplayHeader">
    /// When true and <see cref="statsOnlyNameText"/> is assigned, title goes there and <see cref="nameText"/> is hidden (stats / help hovers).
    /// </param>
    public void ShowText(
        string title,
        string body,
        Color? titleColor = null,
        bool useStatsDisplayHeader = false,
        SkillTreeTooltipChrome skillTreeChrome = SkillTreeTooltipChrome.None)
    {
        ResetTooltipSectionMargins();

        RestoreSkillTreeChromeIfNeeded();
        SetEquipmentCompactMode(false);

        bool statsHeader = useStatsDisplayHeader && statsOnlyNameText;
        if (statsHeader)
        {
            if (nameText)
            {
                nameText.text = "";
                nameText.gameObject.SetActive(false);
            }

            statsOnlyNameText.text = title ?? "";
            statsOnlyNameText.color = titleColor ?? defaultNameColor;
            statsOnlyNameText.gameObject.SetActive(true);
        }
        else
        {
            if (!nameText)
                return;

            HideStatsOnlyNameHeader();

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
            string b = body ?? "";
            descriptionText.text = b;
            bool hasBody = !string.IsNullOrWhiteSpace(b);
            descriptionText.margin = hasBody
                ? WithExtraBottomMargin(_marginBaseDescription, spacingAfterDescriptionPixels)
                : _marginBaseDescription;
            descriptionText.gameObject.SetActive(hasBody);
            if (hasBody && skillTreeChrome == SkillTreeTooltipChrome.MajorPassivePanel)
                descriptionText.richText = true;
        }

        ClearTooltipStatsFields();
        ClearEnhancementDisplay();

        if (customValueText)
        {
            customValueText.text = "";
            customValueText.enabled = false;
            customValueText.gameObject.SetActive(false);
        }

        if (rarityBorder)
            rarityBorder.color = defaultBorderColor;

        if (skillTreeChrome == SkillTreeTooltipChrome.MajorPassivePanel)
            ApplySkillTreeMajorPassiveChrome();

        RebuildTooltipLayoutNow();

        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Ensures ContentSizeFitter / layout groups apply before <see cref="FlipInsideBounds"/> runs in LateUpdate.
    /// </summary>
    private void RebuildTooltipLayoutNow()
    {
        ApplyDynamicTooltipContentWidth();

        Canvas.ForceUpdateCanvases();
        if (tooltipLayoutRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipLayoutRoot);
        if (_rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
        Canvas.ForceUpdateCanvases();
    }

    private void ResolveOptionalSplitStatsTextRefs()
    {
        if (!miscStatsText && tooltipLayoutRoot)
        {
            miscStatsText = tooltipLayoutRoot.Find("MiscStatsText")?.GetComponent<TMP_Text>()
                ?? tooltipLayoutRoot.Find("StatsText")?.GetComponent<TMP_Text>();
        }

        if (!mainStatsText && tooltipLayoutRoot)
            mainStatsText = tooltipLayoutRoot.Find("MainStatsText")?.GetComponent<TMP_Text>();

        if (!statsOnlyNameText && tooltipLayoutRoot)
            statsOnlyNameText = tooltipLayoutRoot.Find("StatsOnlyNameText")?.GetComponent<TMP_Text>();
    }

    private void HideStatsOnlyNameHeader()
    {
        if (!statsOnlyNameText)
            return;

        statsOnlyNameText.text = "";
        statsOnlyNameText.gameObject.SetActive(false);
    }

    private void ConfigureTooltipContentForDynamicWidth()
    {
        if (!tooltipLayoutRoot)
            return;

        var vlg = tooltipLayoutRoot.GetComponent<VerticalLayoutGroup>();
        if (vlg)
        {
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
        }

        var csf = tooltipLayoutRoot.GetComponent<ContentSizeFitter>();
        if (csf && csf.horizontalFit != ContentSizeFitter.FitMode.Unconstrained)
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    /// <summary>
    /// Width = clamp(max(active TMP natural width) + horizontal padding, min, max); TMP wrap past max so height grows.
    /// </summary>
    private void ApplyDynamicTooltipContentWidth()
    {
        if (!tooltipLayoutRoot)
            return;

        float minW = Mathf.Max(1f, tooltipContentMinWidth);
        float maxW = Mathf.Max(minW, tooltipContentMaxWidth);

        float hPad = 0f;
        var vlg = tooltipLayoutRoot.GetComponent<VerticalLayoutGroup>();
        if (vlg)
            hPad += vlg.padding.left + vlg.padding.right;
        var hlg = tooltipLayoutRoot.GetComponent<HorizontalLayoutGroup>();
        if (hlg)
            hPad += hlg.padding.left + hlg.padding.right;

        float maxPreferred = 0f;
        TMP_Text[] tmps = tooltipLayoutRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text tmp = tmps[i];
            if (!tmp || !tmp.gameObject.activeInHierarchy)
                continue;

            tmp.textWrappingMode = TextWrappingModes.Normal;

            string s = tmp.text ?? string.Empty;
            if (string.IsNullOrEmpty(s))
                continue;

            Vector2 pref = tmp.GetPreferredValues(s, float.PositiveInfinity, float.PositiveInfinity);
            maxPreferred = Mathf.Max(maxPreferred, pref.x);
        }

        float naturalTotal = maxPreferred + hPad;
        float targetWidth = Mathf.Clamp(Mathf.Max(naturalTotal, minW), minW, maxW);

        if (!_tooltipContentLayoutElement)
            _tooltipContentLayoutElement = tooltipLayoutRoot.GetComponent<LayoutElement>();
        if (!_tooltipContentLayoutElement)
            _tooltipContentLayoutElement = tooltipLayoutRoot.gameObject.AddComponent<LayoutElement>();

        _tooltipContentLayoutElement.preferredWidth = targetWidth;

        // Content has no parent LayoutGroup; LayoutElement alone does not resize the RectTransform.
        // Horizontal CSF is Unconstrained so we assign width explicitly; vertical CSF still grows height.
        tooltipLayoutRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    public void Hide()
    {
        RestoreSkillTreeChromeIfNeeded();
        ResetTooltipSectionMargins();

        RestoreDefaultParent();
        _scaleAnchor = null;
        _useHudTooltipScalePath = false;

        if (_rt)
        {
            _rt.anchoredPosition = Vector2.zero;
            ResetTooltipLocalScaleToDefaultAuthored();
        }

        if (canvasGroup)
            canvasGroup.alpha = 0f;

        if (nameText)
        {
            nameText.text = "";
            nameText.color = defaultNameColor;
            nameText.gameObject.SetActive(true);
        }

        HideStatsOnlyNameHeader();

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

        ClearTooltipStatsFields();

        if (customValueText)
        {
            customValueText.text = "";
            customValueText.enabled = false;
            customValueText.gameObject.SetActive(false);
        }

        if (rarityBorder)
            rarityBorder.color = defaultBorderColor;

        ClearEnhancementDisplay();
    }

    private void ApplySkillTreeMajorPassiveChrome()
    {
        _activeSkillTreeChrome = SkillTreeTooltipChrome.MajorPassivePanel;
        if (rarityBorder)
        {
            _storedBorderColorForChrome = rarityBorder.color;
            _capturedBorderColorForChrome = true;
            rarityBorder.color = skillTreeMajorBorderColor;
        }

        if (skillTreePanelBackdrop)
        {
            _storedBackdropColorForChrome = skillTreePanelBackdrop.color;
            _capturedBackdropColorForChrome = true;
            skillTreePanelBackdrop.color = skillTreeMajorBackdropColor;
        }
    }

    private void RestoreSkillTreeChromeIfNeeded()
    {
        if (_activeSkillTreeChrome != SkillTreeTooltipChrome.MajorPassivePanel)
            return;

        if (rarityBorder && _capturedBorderColorForChrome)
            rarityBorder.color = _storedBorderColorForChrome;
        if (skillTreePanelBackdrop && _capturedBackdropColorForChrome)
            skillTreePanelBackdrop.color = _storedBackdropColorForChrome;

        _activeSkillTreeChrome = SkillTreeTooltipChrome.None;
        _capturedBorderColorForChrome = false;
        _capturedBackdropColorForChrome = false;
    }

    private void ResolveEnhancementRowRefs()
    {
        if (!tooltipLayoutRoot)
            return;

        if (!enhancementIconsRow)
            enhancementIconsRow = tooltipLayoutRoot.Find("EnhancementSlots") as RectTransform;

        if (!enhancementLegacySuccessRoot)
        {
            Transform t = tooltipLayoutRoot.Find("EnhancementSuccess");
            if (t)
                enhancementLegacySuccessRoot = t.gameObject;
        }
    }

    private void BindEnhancementDisplay(ItemDefinition def)
    {
        ResolveEnhancementRowRefs();

        if (enhancementLegacySuccessRoot)
            enhancementLegacySuccessRoot.SetActive(false);

        if (!enhancementIconsRow)
            return;

        if (!def || !def.HasUpgradeSlots || def.MaxUpgradeSlots <= 0)
        {
            enhancementIconsRow.gameObject.SetActive(false);
            return;
        }

        if (!enhancementStarEmptySprite || !enhancementStarFilledSprite)
        {
            enhancementIconsRow.gameObject.SetActive(false);
            return;
        }

        int max = def.MaxUpgradeSlots;
        int filled = Mathf.Clamp(def.SuccessfulEnhancements, 0, max);

        EnsureEnhancementStarChildCount(max);
        for (int i = 0; i < max; i++)
        {
            Transform star = enhancementIconsRow.GetChild(i);
            star.gameObject.SetActive(true);
            Image img = star.GetComponent<Image>();
            if (!img)
                img = star.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = i < filled ? enhancementStarFilledSprite : enhancementStarEmptySprite;
            img.color = enhancementStarColor;
        }

        for (int i = max; i < enhancementIconsRow.childCount; i++)
            enhancementIconsRow.GetChild(i).gameObject.SetActive(false);

        enhancementIconsRow.gameObject.SetActive(true);
    }

    private void EnsureEnhancementStarChildCount(int count)
    {
        if (!enhancementIconsRow || count <= 0)
            return;

        float size = Mathf.Max(4f, enhancementStarCellSize);

        while (enhancementIconsRow.childCount < count)
        {
            GameObject go = new GameObject("EnhancementStar", typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(enhancementIconsRow, false);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = size;
            le.minHeight = size;
            le.preferredWidth = size;
            le.preferredHeight = size;
        }

        for (int i = 0; i < count; i++)
        {
            Transform t = enhancementIconsRow.GetChild(i);
            LayoutElement le = t.GetComponent<LayoutElement>();
            if (!le)
                le = t.gameObject.AddComponent<LayoutElement>();
            le.minWidth = size;
            le.minHeight = size;
            le.preferredWidth = size;
            le.preferredHeight = size;
        }
    }

    private void ClearEnhancementDisplay()
    {
        if (enhancementIconsRow)
            enhancementIconsRow.gameObject.SetActive(false);

        if (enhancementLegacySuccessRoot)
            enhancementLegacySuccessRoot.SetActive(false);
    }

    private void ClearTooltipStatsFields()
    {
        CaptureTooltipMarginBasesOnce();

        if (miscStatsText)
        {
            miscStatsText.text = "";
            miscStatsText.margin = _marginBaseMisc;
            miscStatsText.gameObject.SetActive(false);
        }

        if (mainStatsText)
        {
            mainStatsText.text = "";
            mainStatsText.margin = _marginBaseMain;
            mainStatsText.gameObject.SetActive(false);
        }
    }

    private void BindTooltipStats(ItemDefinition def)
    {
        if (!def)
        {
            ClearTooltipStatsFields();
            return;
        }

        CaptureTooltipMarginBasesOnce();

        if (mainStatsText == null)
        {
            string combined = BuildTooltipStatsTextWithSupportRequirement(def);
            if (miscStatsText)
            {
                miscStatsText.text = combined;
                bool has = !string.IsNullOrWhiteSpace(combined);
                miscStatsText.margin = has
                    ? WithExtraBottomMargin(_marginBaseMisc, spacingAfterMainStatsPixels)
                    : _marginBaseMisc;
                miscStatsText.gameObject.SetActive(has);
            }

            return;
        }

        string misc = def.BuildTooltipMiscStatsText() ?? "";
        string main = BuildTooltipMainStatsTextWithSupportRequirement(def);

        if (miscStatsText)
        {
            miscStatsText.text = misc;
            miscStatsText.margin = _marginBaseMisc;
            miscStatsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(misc));
        }

        bool hasMain = !string.IsNullOrWhiteSpace(main);
        mainStatsText.text = main;
        mainStatsText.margin = hasMain
            ? WithExtraBottomMargin(_marginBaseMain, spacingAfterMainStatsPixels)
            : _marginBaseMain;
        mainStatsText.gameObject.SetActive(hasMain);
    }

    private void CaptureTooltipMarginBasesOnce()
    {
        if (_tooltipMarginBasesCaptured)
            return;

        if (descriptionText)
            _marginBaseDescription = descriptionText.margin;
        if (miscStatsText)
            _marginBaseMisc = miscStatsText.margin;
        if (mainStatsText)
            _marginBaseMain = mainStatsText.margin;

        _tooltipMarginBasesCaptured = true;
    }

    private void ResetTooltipSectionMargins()
    {
        CaptureTooltipMarginBasesOnce();

        if (descriptionText)
            descriptionText.margin = _marginBaseDescription;
        if (miscStatsText)
            miscStatsText.margin = _marginBaseMisc;
        if (mainStatsText)
            mainStatsText.margin = _marginBaseMain;
    }

    private static Vector4 WithExtraBottomMargin(Vector4 baseMargin, float extraBottom)
    {
        if (extraBottom <= 0f)
            return baseMargin;

        return new Vector4(baseMargin.x, baseMargin.y, baseMargin.z, baseMargin.w + extraBottom);
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

    private static string FormatShopValueBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
            return "";

        return $"\n<size=115%>{block.Trim()}</size>";
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

        return ApplyOffhandSupportRequirementColoring(stats, def);
    }

    private string BuildTooltipMainStatsTextWithSupportRequirement(ItemDefinition def)
    {
        if (!def)
            return "";

        string main = def.BuildTooltipMainStatsText() ?? "";
        return ApplyOffhandSupportRequirementColoring(main, def);
    }

    private string ApplyOffhandSupportRequirementColoring(string block, ItemDefinition def)
    {
        if (!def)
            return block ?? "";

        if (!def.RequiresOffhandSupport || def.RequiredSupportType == CombatSupportType.None)
            return block ?? "";

        bool hasRequirementEquipped = HasRequiredSupportEquipped(def.RequiredSupportType);

        string colour = hasRequirementEquipped ? "#55DD55" : "#FF5555";
        string reqLine = $"<color={colour}>Requires: {def.RequiredSupportType}</color>";

        if (string.IsNullOrWhiteSpace(block))
            return reqLine;

        block = ReplaceLineStartingWith(block, "Requires:", reqLine, out bool replaced);
        if (replaced)
            return block;

        return block.TrimEnd('\n') + "\n" + reqLine;
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

        if (_rt.parent == _defaultParent)
        {
            _restoreParentQueued = false;
            return;
        }

        if (CanReparentTooltipNow())
        {
            _restoreParentQueued = false;
            _rt.SetParent(_defaultParent, false);
            return;
        }

        if (!_restoreParentQueued)
        {
            _restoreParentQueued = true;
            SharedTooltipParentRestoreRunner.Enqueue(this);
        }
    }

    /// <summary>Called next frame when the docked slot is no longer in OnDisable (avoids SetParent during deactivate).</summary>
    internal void RestoreDefaultParentDeferred()
    {
        _restoreParentQueued = false;
        if (_rt == null || _defaultParent == null)
            return;

        if (_rt.parent != _defaultParent)
            _rt.SetParent(_defaultParent, false);
    }

    private bool CanReparentTooltipNow()
    {
        Transform walk = _rt.parent;
        while (walk != null)
        {
            if (walk == _defaultParent)
                return true;

            if (!walk.gameObject.activeInHierarchy)
                return false;

            walk = walk.parent;
        }

        return true;
    }

    public void SetAnchor(Transform anchor)
    {
        if (!anchor || _rt == null)
            return;

        _scaleAnchor = anchor;
        _rt.SetParent(anchor, false);
        _rt.anchoredPosition = Vector2.zero;
        _rt.SetAsLastSibling();
        ApplyDockedTooltipScale();
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
        _useHudTooltipScalePath = false;
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
        Color? titleColor = null,
        bool useStatsDisplayHeader = false,
        SkillTreeTooltipChrome skillTreeChrome = SkillTreeTooltipChrome.None,
        bool useHudTooltipScale = true)
    {
        if (!anchor)
        {
            Hide();
            return;
        }

        RestoreDefaultParent();
        transform.SetAsLastSibling();
        _useHudTooltipScalePath = useHudTooltipScale;
        SetAnchor(anchor);

        if (flipInsideBounds)
        {
            RectTransform anchorRect = anchor as RectTransform;
            flipInsideBounds.SetMeasureRect(measureRect ? measureRect : anchorRect);
            flipInsideBounds.SetHeightRect(heightRect ? heightRect : (measureRect ? measureRect : anchorRect));
            flipInsideBounds.SetPreferredSide(preferredSide);
        }

        ShowText(title, body, titleColor, useStatsDisplayHeader, skillTreeChrome);
        ApplyDockedTooltipScale();
    }
}

/// <summary>Flushes tooltip reparent after slot OnDisable completes (Unity forbids SetParent during deactivate).</summary>
internal sealed class SharedTooltipParentRestoreRunner : MonoBehaviour
{
    private static SharedTooltipParentRestoreRunner _instance;
    private static readonly List<SharedTooltipUI> Queue = new();
    private static bool _flushScheduled;

    public static void Enqueue(SharedTooltipUI tooltip)
    {
        if (!tooltip || Queue.Contains(tooltip))
            return;

        Queue.Add(tooltip);
        EnsureInstance();

        if (!_flushScheduled && _instance != null)
        {
            _flushScheduled = true;
            _instance.StartCoroutine(_instance.FlushNextFrame());
        }
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        var go = new GameObject("[SharedTooltipParentRestore]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SharedTooltipParentRestoreRunner>();
    }

    private IEnumerator FlushNextFrame()
    {
        yield return null;
        _flushScheduled = false;

        for (int i = 0; i < Queue.Count; i++)
        {
            SharedTooltipUI tip = Queue[i];
            if (tip)
                tip.RestoreDefaultParentDeferred();
        }

        Queue.Clear();
    }
}