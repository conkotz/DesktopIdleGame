using TMPro;

using UnityEngine;

using UnityEngine.EventSystems;

using UnityEngine.UI;



/// <summary>

/// Right-panel row for a committed major passive or capstone (no icon; scrolls to tree tier on click).

/// </summary>

public class MajorPassiveListEntryUI : MonoBehaviour,

    IPointerEnterHandler, IPointerExitHandler

{

    private static Sprite _sharedNotSelectedSprite;

    private static Sprite _sharedEnhanceSprite;



    [SerializeField] private Image rowBackground;

    [SerializeField] private TMP_Text nameText;

    [SerializeField] private TMP_Text enhancementText;

    [SerializeField] private TMP_Text requiredLevelText;

    [SerializeField] private GameObject selectAbilityRoot;

    [SerializeField] private Button selectButton;

    [SerializeField] private GameObject iconBackgroundRoot;

    [SerializeField] private Image iconImage;

    [Tooltip("Yellow ! — enhancement not chosen yet (matches skill tree NotSelected).")]

    [SerializeField] private GameObject notSelectedIndicatorRoot;

    [Tooltip("Green + — open enhancement branch (matches skill tree Enhance button).")]

    [SerializeField] private GameObject enhanceIndicatorRoot;

    [SerializeField] private Button enhanceIndicatorButton;

    [SerializeField] private CanvasGroup canvasGroup;



    [Header("Style")]

    [SerializeField] private Color normalRowColor = new Color(0.16862746f, 0.12941177f, 0.09411765f, 0.85f);

    [SerializeField] private Color capstoneRowColor = new Color(0.62f, 0.48f, 0.16f, 0.96f);



    private SkillDefinition _skill;

    private SkillUnlockDefinition _unlock;

    private int _treeScrollLevel;

    private bool _isAvailablePlaceholder;

    private bool _isCapstoneStyle;

    private bool _showsNoEnhancementPlaceholder;

    private Color _enhancementTextBaseColor = Color.white;

    private bool _enhancementTextBaseColorCached;

    private static readonly Color NoEnhancementPlaceholderColor = new(0.62f, 0.56f, 0.46f, 0.92f);

    private const string NoEnhancementPlaceholderText = "No enhancement selected";

    public bool ShowsNoEnhancementPlaceholder => _showsNoEnhancementPlaceholder;

    private SharedTooltipUI _tooltip;

    private Canvas _rootCanvas;

    private RectTransform _tooltipBoundsRect;

    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Right;



    private Button _rowButton;

    private string _tooltipTitle;

    private string _tooltipBody;



    public static void SetSharedIndicatorSprites(Sprite notSelected, Sprite enhance)

    {

        _sharedNotSelectedSprite = notSelected;

        _sharedEnhanceSprite = enhance;

    }



    private void Awake()

    {

        _rowButton = GetComponent<Button>();

        if (_rowButton != null)

        {

            Navigation n = _rowButton.navigation;

            n.mode = Navigation.Mode.None;

            _rowButton.navigation = n;

        }



        if (rowBackground == null)

            rowBackground = GetComponent<Image>();



        AutoWireChildRefs();

        EnsureChildGraphicsIgnoreRaycasts();
        EnsureTextGroupLayout();

    }



    private void AutoWireChildRefs()

    {

        if (nameText == null)

        {

            Transform t = transform.Find("RowGroup/TextGroup/NameText")
                ?? transform.Find("RowGroup/NameText")
                ?? transform.Find("NameText");

            if (t != null)

                nameText = t.GetComponent<TMP_Text>();

        }



        if (enhancementText == null)

        {

            Transform t = transform.Find("RowGroup/TextGroup/EnhancementText")
                ?? transform.Find("EnhancementText");

            if (t != null)

                enhancementText = t.GetComponent<TMP_Text>();

        }



        if (requiredLevelText == null)

        {

            Transform t = transform.Find("RequiredLevelText");

            if (t != null)

                requiredLevelText = t.GetComponent<TMP_Text>();

        }



        if (selectAbilityRoot == null)

        {

            Transform t = transform.Find("RowGroup/SelectAbility") ?? transform.Find("SelectAbility");

            if (t != null)

            {

                selectAbilityRoot = t.gameObject;

                if (selectButton == null)

                    selectButton = t.GetComponent<Button>();

            }

        }



        if (iconBackgroundRoot == null)

        {

            Transform t = transform.Find("RowGroup/IconBackground") ?? transform.Find("IconBackground");

            if (t != null)

                iconBackgroundRoot = t.gameObject;

        }



        if (iconImage == null && iconBackgroundRoot != null)

        {

            Transform t = iconBackgroundRoot.transform.Find("Icon");

            if (t != null)

                iconImage = t.GetComponent<Image>();

        }



        if (notSelectedIndicatorRoot == null)

        {

            Transform t = transform.Find("RowGroup/NotSelectedIndicator") ?? transform.Find("NotSelectedIndicator");

            if (t != null)

                notSelectedIndicatorRoot = t.gameObject;

        }



        if (enhanceIndicatorRoot == null)

        {

            Transform t = transform.Find("RowGroup/EnhanceIndicator") ?? transform.Find("EnhanceIndicator");

            if (t != null)

            {

                enhanceIndicatorRoot = t.gameObject;

                if (enhanceIndicatorButton == null)

                    enhanceIndicatorButton = t.GetComponent<Button>();

            }

        }

    }



    public void SetTreeStatusIndicators(bool showNotSelectedPrompt, bool showEnhanceButton, System.Action onEnhanceClicked = null)

    {

        EnsureRuntimeIndicatorObjects();



        if (notSelectedIndicatorRoot)

            notSelectedIndicatorRoot.SetActive(showNotSelectedPrompt);



        if (enhanceIndicatorRoot)

            enhanceIndicatorRoot.SetActive(showEnhanceButton);



        if (enhanceIndicatorButton != null)

        {

            enhanceIndicatorButton.onClick.RemoveAllListeners();

            if (showEnhanceButton && onEnhanceClicked != null)

                enhanceIndicatorButton.onClick.AddListener(() => onEnhanceClicked());

        }

    }



    private void EnsureRuntimeIndicatorObjects()

    {

        Transform rowGroup = transform.Find("RowGroup");

        if (rowGroup == null)

            return;



        if (notSelectedIndicatorRoot == null && _sharedNotSelectedSprite != null)

        {

            notSelectedIndicatorRoot = CreateIndicatorObject(

                rowGroup,

                "NotSelectedIndicator",

                _sharedNotSelectedSprite,

                new Vector2(2f, -2f),

                new Vector2(20f, 20f),

                new Vector2(0f, 1f),

                addButton: false,

                out _);

        }



        if (enhanceIndicatorRoot == null && _sharedEnhanceSprite != null)

        {

            enhanceIndicatorRoot = CreateIndicatorObject(

                rowGroup,

                "EnhanceIndicator",

                _sharedEnhanceSprite,

                new Vector2(2f, -24f),

                new Vector2(22f, 22f),

                new Vector2(0f, 1f),

                addButton: true,

                out Button enhanceBtn);

            enhanceIndicatorButton = enhanceBtn;

        }

    }



    private static GameObject CreateIndicatorObject(

        Transform parent,

        string objectName,

        Sprite sprite,

        Vector2 anchoredPosition,

        Vector2 size,

        Vector2 anchorMin,

        bool addButton,

        out Button button)

    {

        button = null;

        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        go.transform.SetParent(parent, false);



        RectTransform rt = go.GetComponent<RectTransform>();

        rt.anchorMin = anchorMin;

        rt.anchorMax = anchorMin;

        rt.pivot = anchorMin;

        rt.anchoredPosition = anchoredPosition;

        rt.sizeDelta = size;



        Image image = go.GetComponent<Image>();

        image.sprite = sprite;

        image.preserveAspect = true;

        image.raycastTarget = addButton;



        if (addButton)

        {

            button = go.AddComponent<Button>();

            button.targetGraphic = image;

            Navigation nav = button.navigation;

            nav.mode = Navigation.Mode.None;

            button.navigation = nav;

        }



        go.SetActive(false);

        return go;

    }



    public void Bind(

        SkillDefinition skill,

        SkillUnlockDefinition unlock,

        bool capstoneStyle,

        SharedTooltipUI tooltip,

        Canvas rootCanvas,

        System.Action onRowClickScrollToTree)

    {

        ClearRowClickListeners();

        _isAvailablePlaceholder = false;

        _skill = skill;

        _unlock = unlock;

        _isCapstoneStyle = capstoneStyle;

        _treeScrollLevel = unlock != null ? Mathf.Max(1, unlock.requiredLevel) : 1;

        _tooltip = tooltip;

        _rootCanvas = rootCanvas;

        if (_rootCanvas == null)

            _rootCanvas = GetComponentInParent<Canvas>();



        if (selectAbilityRoot)

            selectAbilityRoot.SetActive(false);



        SetIconDisplay(false);



        string displayName = unlock != null

            ? SkillsAbilityPresentationResolver.ResolveUnlockTitle(unlock)

            : "—";

        if (string.IsNullOrWhiteSpace(displayName))

            displayName = "Major Passive";



        string enhancementTitle = SkillUnlockPanelTooltipBuilder.TryResolveCommittedEnhancementTitle(

            skill, unlock, SkillsManager.Instance);

        bool showNoEnhancementPlaceholder = false;
        if (string.IsNullOrWhiteSpace(enhancementTitle) && skill != null && unlock != null)
        {
            SkillTreeMajorPassiveRowIndicators.TryGet(
                skill, unlock, SkillsManager.Instance, out showNoEnhancementPlaceholder, out _);
        }

        ApplyPassiveNameAndEnhancement(displayName, enhancementTitle, showNoEnhancementPlaceholder);



        if (requiredLevelText)

            requiredLevelText.text = unlock != null ? $"Lv {_treeScrollLevel}" : string.Empty;



        ApplyRowBackgroundColor();



        if (!canvasGroup)

            canvasGroup = GetComponent<CanvasGroup>();

        if (!canvasGroup)

            canvasGroup = gameObject.AddComponent<CanvasGroup>();



        canvasGroup.alpha = 1f;

        canvasGroup.blocksRaycasts = true;



        SkillUnlockPanelTooltipBuilder.TryBuildListEntryTooltip(skill, unlock, SkillsManager.Instance, out _tooltipTitle, out _tooltipBody);

        RegisterRootRowScrollClick(onRowClickScrollToTree);

    }



    public void BindAvailableMajorPassiveTier(

        int rowLevel,

        SharedTooltipUI tooltip,

        Canvas rootCanvas,

        System.Action onSelectScrollTree)

    {

        ClearRowClickListeners();

        _isAvailablePlaceholder = true;

        _skill = null;

        _unlock = null;

        _isCapstoneStyle = false;

        _treeScrollLevel = rowLevel;

        _tooltip = tooltip;

        _rootCanvas = rootCanvas;

        if (_rootCanvas == null)

            _rootCanvas = GetComponentInParent<Canvas>();



        _tooltipTitle = null;

        _tooltipBody = null;



        if (selectAbilityRoot)

            selectAbilityRoot.SetActive(selectButton != null);



        SetIconDisplay(false);



        ApplyPassiveNameAndEnhancement("Major Passive Available", null);



        if (requiredLevelText)

            requiredLevelText.text = $"Lv {rowLevel}";



        ApplyRowBackgroundColor();



        if (!canvasGroup)

            canvasGroup = GetComponent<CanvasGroup>();

        if (!canvasGroup)

            canvasGroup = gameObject.AddComponent<CanvasGroup>();



        canvasGroup.alpha = 1f;

        canvasGroup.blocksRaycasts = true;



        if (selectButton != null && onSelectScrollTree != null)

            selectButton.onClick.AddListener(() => onSelectScrollTree());



        RegisterRootRowScrollClick(onSelectScrollTree);

        SetTreeStatusIndicators(showNotSelectedPrompt: true, showEnhanceButton: false);

    }



    public void BindGeneralUnlock(

        SkillDefinition skill,

        SkillUnlockDefinition unlock,

        SharedTooltipUI tooltip,

        Canvas rootCanvas,

        System.Action onRowClickScrollToTree)

    {

        ClearRowClickListeners();

        _isAvailablePlaceholder = false;

        _skill = skill;

        _unlock = unlock;

        _isCapstoneStyle = false;

        _treeScrollLevel = unlock != null ? Mathf.Max(1, unlock.requiredLevel) : 1;

        _tooltip = tooltip;

        _rootCanvas = rootCanvas;

        if (_rootCanvas == null)

            _rootCanvas = GetComponentInParent<Canvas>();



        if (selectAbilityRoot)

            selectAbilityRoot.SetActive(false);



        SetTreeStatusIndicators(showNotSelectedPrompt: false, showEnhanceButton: false);



        string displayName = unlock != null

            ? SkillsAbilityPresentationResolver.ResolveUnlockTitle(unlock)

            : "Unlock";

        if (string.IsNullOrWhiteSpace(displayName))

            displayName = "Unlock";



        ApplyPassiveNameAndEnhancement(displayName, null);



        if (requiredLevelText)

            requiredLevelText.text = $"Lv {_treeScrollLevel}";



        Sprite unlockIcon = unlock != null ? unlock.icon : null;

        SetIconDisplay(unlockIcon != null, unlockIcon);



        ApplyRowBackgroundColor();



        if (!canvasGroup)

            canvasGroup = GetComponent<CanvasGroup>();

        if (!canvasGroup)

            canvasGroup = gameObject.AddComponent<CanvasGroup>();



        canvasGroup.alpha = 1f;

        canvasGroup.blocksRaycasts = true;



        SkillUnlockPanelTooltipBuilder.TryBuildListEntryTooltip(skill, unlock, SkillsManager.Instance, out _tooltipTitle, out _tooltipBody);

        RegisterRootRowScrollClick(onRowClickScrollToTree);

    }



    public void SetTooltipDocking(RectTransform tooltipBoundsRect, FlipInsideBounds.PreferredSide preferredSide)

    {

        _tooltipBoundsRect = tooltipBoundsRect;

        _preferredSide = preferredSide;

    }



    public void OnPointerEnter(PointerEventData eventData)

    {

        if (_isAvailablePlaceholder || _tooltip == null || string.IsNullOrWhiteSpace(_tooltipBody))

            return;



        RectTransform rowRect = transform as RectTransform;

        RectTransform measure = rowRect != null ? rowRect : (_tooltipBoundsRect ? _tooltipBoundsRect : transform.root as RectTransform);

        _tooltip.ShowTextAt(

            measure != null ? measure : transform,

            _tooltipTitle,

            _tooltipBody,

            measureRect: measure,

            heightRect: measure,

            preferredSide: _preferredSide,

            useHudTooltipScale: false);

    }



    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide();



    private void ApplyRowBackgroundColor()

    {

        if (rowBackground == null)

            return;



        rowBackground.color = _isCapstoneStyle ? capstoneRowColor : normalRowColor;

    }



    private void EnsureChildGraphicsIgnoreRaycasts()

    {

        if (nameText)

            nameText.raycastTarget = false;

        if (enhancementText)

            enhancementText.raycastTarget = false;

        if (requiredLevelText)

            requiredLevelText.raycastTarget = false;

        if (iconImage)

            iconImage.raycastTarget = false;

    }



    private void SetIconDisplay(bool visible, Sprite sprite = null)

    {

        if (iconBackgroundRoot != null)

            iconBackgroundRoot.SetActive(visible);



        if (iconImage != null)

        {

            iconImage.enabled = visible && sprite != null;

            iconImage.sprite = sprite;

            iconImage.preserveAspect = true;

        }

    }



    private void ApplyPassiveNameAndEnhancement(
        string displayName,
        string enhancementTitle,
        bool showNoEnhancementPlaceholder = false)

    {
        _showsNoEnhancementPlaceholder = false;

        if (nameText != null)

        {

            nameText.richText = false;

            nameText.textWrappingMode = TextWrappingModes.Normal;

            nameText.overflowMode = TextOverflowModes.Overflow;

            nameText.horizontalAlignment = HorizontalAlignmentOptions.Left;

            nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Major Passive" : displayName.Trim();

        }



        if (enhancementText != null)

        {
            if (!_enhancementTextBaseColorCached)
            {
                _enhancementTextBaseColor = enhancementText.color;
                _enhancementTextBaseColorCached = true;
            }

            bool hasEnhancement = !string.IsNullOrWhiteSpace(enhancementTitle);

            enhancementText.richText = false;

            enhancementText.textWrappingMode = TextWrappingModes.Normal;

            enhancementText.horizontalAlignment = HorizontalAlignmentOptions.Left;

            LayoutElement enhancementLayout = enhancementText.GetComponent<LayoutElement>();
            if (enhancementLayout == null)
                enhancementLayout = enhancementText.gameObject.AddComponent<LayoutElement>();

            if (hasEnhancement)

            {
                enhancementText.gameObject.SetActive(true);
                enhancementText.fontStyle = FontStyles.Normal;
                enhancementText.color = _enhancementTextBaseColor;
                enhancementText.text = enhancementTitle.Trim();
                enhancementLayout.minHeight = 16f;
            }
            else if (showNoEnhancementPlaceholder)
            {
                _showsNoEnhancementPlaceholder = true;
                enhancementText.gameObject.SetActive(true);
                enhancementText.fontStyle = FontStyles.Italic;
                enhancementText.color = NoEnhancementPlaceholderColor;
                enhancementText.text = NoEnhancementPlaceholderText;
                enhancementLayout.minHeight = 16f;
            }
            else

            {
                enhancementText.gameObject.SetActive(false);
                enhancementText.text = string.Empty;
                enhancementLayout.minHeight = 0f;
            }

        }



        EnsureTextGroupLayout();

        RebuildTextGroupLayout();

    }



    private void EnsureTextGroupLayout()

    {

        Transform textGroup = transform.Find("RowGroup/TextGroup");

        if (textGroup == null)

            return;



        VerticalLayoutGroup vlg = textGroup.GetComponent<VerticalLayoutGroup>();

        if (vlg == null)

            vlg = textGroup.gameObject.AddComponent<VerticalLayoutGroup>();



        vlg.spacing = 2f;

        vlg.padding = new RectOffset(0, 8, 0, 0);

        vlg.childAlignment = TextAnchor.UpperLeft;

        vlg.childControlWidth = true;

        vlg.childControlHeight = true;

        vlg.childForceExpandWidth = false;

        vlg.childForceExpandHeight = false;



        ConfigureTextForVerticalLayout(nameText);

        ConfigureTextForVerticalLayout(enhancementText);

    }



    private static void ConfigureTextForVerticalLayout(TMP_Text text)

    {

        if (text == null)

            return;



        RectTransform rt = text.rectTransform;

        rt.anchorMin = new Vector2(0f, 1f);

        rt.anchorMax = new Vector2(1f, 1f);

        rt.pivot = new Vector2(0f, 1f);

        rt.anchoredPosition = Vector2.zero;

        rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);



        ContentSizeFitter fitter = rt.GetComponent<ContentSizeFitter>();

        if (fitter == null)

            fitter = rt.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;



        LayoutElement layout = rt.GetComponent<LayoutElement>();

        if (layout == null)

            layout = rt.gameObject.AddComponent<LayoutElement>();

        layout.minHeight = Mathf.Max(16f, text.fontSize + 2f);

        layout.preferredHeight = -1f;

        layout.flexibleHeight = 0f;

    }



    private void RebuildTextGroupLayout()

    {

        Transform textGroup = transform.Find("RowGroup/TextGroup");

        if (textGroup is RectTransform rt)

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

    }



    private void ClearRowClickListeners()

    {

        if (selectButton != null)

            selectButton.onClick.RemoveAllListeners();

        if (_rowButton != null)

            _rowButton.onClick.RemoveAllListeners();

        if (enhanceIndicatorButton != null)

            enhanceIndicatorButton.onClick.RemoveAllListeners();

    }



    private void RegisterRootRowScrollClick(System.Action scrollAction)

    {

        if (_rowButton == null)

            _rowButton = GetComponent<Button>();

        if (_rowButton == null || scrollAction == null)

            return;



        _rowButton.onClick.AddListener(() => scrollAction());

    }

}


