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

    [SerializeField] private TMP_Text requiredLevelText;

    [SerializeField] private GameObject selectAbilityRoot;

    [SerializeField] private Button selectButton;

    [Tooltip("Yellow ! — enhancement not chosen yet (matches skill tree NotSelected).")]

    [SerializeField] private GameObject notSelectedIndicatorRoot;

    [Tooltip("Green + — open enhancement branch (matches skill tree Enhance button).")]

    [SerializeField] private GameObject enhanceIndicatorRoot;

    [SerializeField] private Button enhanceIndicatorButton;

    [SerializeField] private CanvasGroup canvasGroup;



    [Header("Style")]

    [SerializeField] private Color normalRowColor = new Color(0.16862746f, 0.12941177f, 0.09411765f, 0.85f);

    [SerializeField] private Color capstoneRowColor = new Color(0.38f, 0.24f, 0.1f, 0.95f);

    [Tooltip("TMP rich-text hex for committed enhancement name beside the passive title.")]
    [SerializeField] private string enhancementNameColorHex = "#55DD55";



    private SkillDefinition _skill;

    private SkillUnlockDefinition _unlock;

    private int _treeScrollLevel;

    private bool _isAvailablePlaceholder;

    private bool _isCapstoneStyle;



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

    }



    private void AutoWireChildRefs()

    {

        if (nameText == null)

        {

            Transform t = transform.Find("RowGroup/NameText") ?? transform.Find("NameText");

            if (t != null)

                nameText = t.GetComponent<TMP_Text>();

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



        string displayName = unlock != null

            ? SkillsAbilityPresentationResolver.ResolveUnlockTitle(unlock)

            : "—";

        if (string.IsNullOrWhiteSpace(displayName))

            displayName = "Major Passive";



        if (nameText)

        {

            nameText.richText = true;

            string enhancementTitle = SkillUnlockPanelTooltipBuilder.TryResolveCommittedEnhancementTitle(

                skill, unlock, SkillsManager.Instance);

            nameText.text = SkillUnlockPanelTooltipBuilder.FormatPassiveNameWithEnhancement(

                displayName, enhancementTitle, enhancementNameColorHex);

        }



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



        if (nameText)

            nameText.text = "Major Passive Available";



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

        if (requiredLevelText)

            requiredLevelText.raycastTarget = false;

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


