using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Death respawn card UI. Dim uses <see cref="GameplayScreenOverlay"/> (strip vs full window per expand-background).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Desktop Idle Game/UI/Death Respawn Popup UI")]
public class DeathRespawnPopupUI : MonoBehaviour
{
    private const string ContentRootChildName = "ContentRoot";
    private const string PanelChildName = "DeathPanel";
    private const string SubtitleChildName = "SubtitleText";

    [Header("Copy")]
    [SerializeField] private string titleText = "You Have Died";
    [SerializeField] private string subtitleRespawnInTown = "You will return to the nearest town.";
    [SerializeField] private string buttonRespawnInTown = "Respawn in Town";
    [SerializeField] private string buttonRespawnHere = "Respawn Here";

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(660f, 372f);
    [SerializeField] private float panelBorderOutsetPixels = 6f;
    [SerializeField] private float titleFontSize = 57f;
    [SerializeField] private float subtitleFontSize = 27f;
    [SerializeField] private float buttonFontSize = 33f;
    [SerializeField] private Vector2 buttonSize = new Vector2(360f, 69f);
    [SerializeField] private float buttonAnchoredYOffset = -108f;
    [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.62f;

    [Header("Colors")]
    [SerializeField] private Color panelColor = new Color32(28, 24, 22, 240);
    [SerializeField] private Color panelBorderColor = new Color32(196, 92, 48, 255);
    [SerializeField] private Color titleColor = new Color32(232, 93, 74, 255);
    [SerializeField] private Color subtitleColor = new Color32(196, 184, 168, 255);
    [SerializeField] private Color buttonNormalColor = new Color32(196, 92, 48, 255);
    [SerializeField] private Color buttonLabelColor = new Color32(255, 244, 228, 255);

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.4f;

    private static Sprite s_uiWhiteSprite;

    private bool _respawnHere;
    private bool _dimVisible;
    private CanvasGroup _canvasGroup;
    private RectTransform _contentRoot;
    private RectTransform _panelRt;
    private Coroutine _fadeRoutine;
    private TMP_Text _title;
    private TMP_Text _subtitle;
    private Button _respawnButton;
    private TMP_Text _respawnButtonLabel;

    private static Color DeathDimColor(float alpha) => new Color(0f, 0f, 0f, alpha);

    private static GameplayScreenOverlay.Spec DeathDimSpec => new GameplayScreenOverlay.Spec
    {
        blocksRaycasts = true,
        raycastTarget = true,
        stripSortingOrder = 32750,
        viewportBleedPixels = GameplayScreenOverlayLayout.DefaultViewportBleedPixels,
    };

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleSettingsChanged;
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;
        HideDim();
    }

    private void OnToggleSettingsChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.ExpandStripBackground && _dimVisible)
            ShowDim();
    }

    private void Awake()
    {
        EnsureLayout();
    }

    public void PrepareForShow(bool respawnHere)
    {
        _respawnHere = respawnHere;
        EnsureLayout();
        ApplyPanelMetrics();
        EnsureFullscreenPresentation();
        ApplyCopy();
        ShowDim();

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(CoFadeIn());
    }

    private void ShowDim()
    {
        GameplayScreenOverlay.Show(
            GameplayScreenOverlay.DeathRespawnDimId,
            DeathDimColor(backdropAlpha),
            DeathDimSpec);
        _dimVisible = true;
    }

    private void HideDim()
    {
        if (!_dimVisible)
            return;

        GameplayScreenOverlay.Hide(GameplayScreenOverlay.DeathRespawnDimId);
        _dimVisible = false;
    }

    /// <summary>
    /// Death card stays centered on the full monitor; only the dim overlay follows strip vs extended layout.
    /// </summary>
    private void EnsureFullscreenPresentation()
    {
        RectTransform root = transform as RectTransform;
        if (!root)
            return;

        RectTransform windowsArea = HelperPopupWindow.ResolveWindowsArea();
        if (windowsArea != null && root.parent != windowsArea)
            root.SetParent(windowsArea, false);

        root.SetAsLastSibling();
        GameplayScreenOverlayLayout.ApplyFullWindowRect(root);
        DisableStripViewportFollower(root.gameObject);

        if (_contentRoot)
        {
            GameplayScreenOverlayLayout.ApplyFullWindowRect(_contentRoot);
            DisableStripViewportFollower(_contentRoot.gameObject);
        }
    }

    private static void DisableStripViewportFollower(GameObject go)
    {
        if (!go)
            return;

        StripUIViewportFollower follower = go.GetComponent<StripUIViewportFollower>();
        if (follower != null)
            follower.enabled = false;
    }

    private void EnsureLayout()
    {
        RectTransform root = transform as RectTransform;
        if (!root)
            return;

        StretchToParent(root);

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.raycastTarget = false;
            rootImage.enabled = false;
        }

        Transform legacyDimmer = root.Find("Dimmer");
        if (legacyDimmer != null)
            Destroy(legacyDimmer.gameObject);

        _contentRoot = EnsureContentRoot(root);
        _panelRt = EnsurePanel(_contentRoot);
        StretchCentered(_panelRt, panelSize);

        Image panelBorder = EnsurePanelBorder(_panelRt, panelSize, panelBorderOutsetPixels);
        Image panelFill = EnsurePanelFill(_panelRt, panelBorder, panelSize);
        panelFill.color = panelColor;
        panelBorder.color = panelBorderColor;

        ReparentSceneContentUnderPanel(root, _panelRt);
        ResolveContentRefs(root, _panelRt);

        StyleTitle(_title);
        StyleSubtitle(_subtitle);
        StyleButton(_respawnButton, _respawnButtonLabel);
        EnsureContentDrawOrder();
    }

    private void ApplyPanelMetrics()
    {
        if (!_panelRt)
            return;

        StretchCentered(_panelRt, panelSize);

        Transform borderT = _panelRt.Find("PanelBorder");
        if (borderT is RectTransform borderRt)
            StretchCentered(borderRt, panelSize + Vector2.one * panelBorderOutsetPixels);

        Transform fillT = _panelRt.Find("PanelFill");
        if (fillT is RectTransform fillRt)
            StretchCentered(fillRt, panelSize);

        StyleTitle(_title);
        StyleSubtitle(_subtitle);
        StyleButton(_respawnButton, _respawnButtonLabel);
    }

    private void ReparentSceneContentUnderPanel(RectTransform root, RectTransform panelRt)
    {
        TMP_Text title = FindChildComponent<TMP_Text>(root, "NotificationText");
        if (title != null && title.transform.parent != panelRt)
            title.transform.SetParent(panelRt, false);

        Button button = FindChildComponent<Button>(root, "Button");
        if (button != null && button.transform.parent != panelRt)
            button.transform.SetParent(panelRt, false);
    }

    private void ResolveContentRefs(RectTransform root, RectTransform panelRt)
    {
        _title = FindChildTmp(panelRt, "NotificationText") ?? FindChildTmp(root, "NotificationText");
        _respawnButton = FindChildComponent<Button>(panelRt, "Button") ?? FindChildComponent<Button>(root, "Button");
        _respawnButtonLabel = _respawnButton != null
            ? _respawnButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        _subtitle = EnsureSubtitle(panelRt, _title);
    }

    private void EnsureContentDrawOrder()
    {
        if (_title)
            _title.transform.SetAsLastSibling();
        if (_subtitle)
            _subtitle.transform.SetAsLastSibling();
        if (_respawnButton)
            _respawnButton.transform.SetAsLastSibling();
    }

    private void ApplyCopy()
    {
        if (_title)
        {
            _title.text = titleText;
            _title.enabled = true;
            _title.gameObject.SetActive(true);

            RectTransform titleRt = _title.rectTransform;
            if (_respawnHere)
            {
                titleRt.anchorMin = new Vector2(0.08f, 0.42f);
                titleRt.anchorMax = new Vector2(0.92f, 0.78f);
            }
            else
            {
                titleRt.anchorMin = new Vector2(0.08f, 0.52f);
                titleRt.anchorMax = new Vector2(0.92f, 0.88f);
            }
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
        }

        if (_subtitle)
        {
            if (_respawnHere)
            {
                _subtitle.gameObject.SetActive(false);
            }
            else
            {
                _subtitle.gameObject.SetActive(true);
                _subtitle.text = subtitleRespawnInTown;
            }
        }

        if (_respawnButtonLabel)
            _respawnButtonLabel.text = _respawnHere ? buttonRespawnHere : buttonRespawnInTown;

        if (_respawnButton)
        {
            _respawnButton.gameObject.SetActive(true);
            _respawnButton.interactable = true;
        }
    }

    private IEnumerator CoFadeIn()
    {
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;

        float dur = Mathf.Max(0.01f, fadeInSeconds);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _fadeRoutine = null;
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static RectTransform EnsureContentRoot(RectTransform root)
    {
        Transform existing = root.Find(ContentRootChildName);
        if (existing != null)
            return existing as RectTransform;

        var contentGo = new GameObject(ContentRootChildName, typeof(RectTransform));
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(root, false);
        StretchToParent(contentRt);
        return contentRt;
    }

    private static RectTransform EnsurePanel(RectTransform contentRoot)
    {
        Transform existing = contentRoot.Find(PanelChildName);
        if (existing != null)
            return existing as RectTransform;

        var panelGo = new GameObject(PanelChildName, typeof(RectTransform));
        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.SetParent(contentRoot, false);
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        return panelRt;
    }

    private static Image EnsurePanelBorder(RectTransform panelRt, Vector2 size, float borderOutsetPixels)
    {
        Transform borderT = panelRt.Find("PanelBorder");
        if (borderT != null)
            return borderT.GetComponent<Image>();

        var borderGo = new GameObject("PanelBorder", typeof(RectTransform), typeof(Image));
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.SetParent(panelRt, false);
        borderRt.SetAsFirstSibling();
        StretchCentered(borderRt, size + Vector2.one * borderOutsetPixels);

        Image border = borderGo.GetComponent<Image>();
        border.sprite = GetUiWhiteSprite();
        border.type = Image.Type.Simple;
        border.raycastTarget = false;
        return border;
    }

    private static Image EnsurePanelFill(RectTransform panelRt, Image panelBorder, Vector2 size)
    {
        Transform fillT = panelRt.Find("PanelFill");
        if (fillT != null)
            return fillT.GetComponent<Image>();

        var fillGo = new GameObject("PanelFill", typeof(RectTransform), typeof(Image));
        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.SetParent(panelRt, false);
        fillRt.SetSiblingIndex(panelBorder != null ? panelBorder.transform.GetSiblingIndex() + 1 : 0);
        StretchCentered(fillRt, size);

        Image fill = fillGo.GetComponent<Image>();
        fill.sprite = GetUiWhiteSprite();
        fill.type = Image.Type.Simple;
        fill.raycastTarget = false;
        return fill;
    }

    private void StyleTitle(TMP_Text title)
    {
        if (!title)
            return;

        RectTransform rt = title.rectTransform;
        rt.anchorMin = new Vector2(0.08f, 0.52f);
        rt.anchorMax = new Vector2(0.92f, 0.88f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.fontSize = titleFontSize;
        title.color = titleColor;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
        title.raycastTarget = false;
    }

    private TMP_Text EnsureSubtitle(RectTransform panelRt, TMP_Text title)
    {
        TMP_Text subtitle = FindChildTmp(panelRt, SubtitleChildName);
        if (subtitle)
            return subtitle;

        if (!title)
            return null;

        var subtitleGo = new GameObject(SubtitleChildName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = subtitleGo.GetComponent<RectTransform>();
        rt.SetParent(panelRt, false);
        rt.anchorMin = new Vector2(0.1f, 0.34f);
        rt.anchorMax = new Vector2(0.9f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        subtitle = subtitleGo.GetComponent<TextMeshProUGUI>();
        if (title.font != null)
            subtitle.font = title.font;
        subtitle.raycastTarget = false;
        return subtitle;
    }

    private void StyleSubtitle(TMP_Text subtitle)
    {
        if (!subtitle)
            return;

        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.fontSize = subtitleFontSize;
        subtitle.color = subtitleColor;
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        subtitle.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void StyleButton(Button button, TMP_Text label)
    {
        if (!button)
            return;

        RectTransform rt = button.transform as RectTransform;
        if (rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, buttonAnchoredYOffset);
            rt.sizeDelta = buttonSize;
        }

        Image graphic = button.targetGraphic as Image;
        if (graphic == null)
            graphic = button.GetComponent<Image>();
        if (graphic != null)
        {
            graphic.sprite = GetUiWhiteSprite();
            graphic.type = Image.Type.Simple;
            graphic.color = buttonNormalColor;
            button.targetGraphic = graphic;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.55f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (label)
        {
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = buttonFontSize;
            label.color = buttonLabelColor;
            label.raycastTarget = false;
        }
    }

    private static Sprite GetUiWhiteSprite()
    {
        if (s_uiWhiteSprite)
            return s_uiWhiteSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_uiWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return s_uiWhiteSprite;
    }

    private static void StretchCentered(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    private static TMP_Text FindChildTmp(Transform root, string name) =>
        FindChildComponent<TMP_Text>(root, name);

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        if (!root)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
            {
                T match = child.GetComponent<T>();
                if (match != null)
                    return match;
            }

            T nested = FindChildComponent<T>(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
