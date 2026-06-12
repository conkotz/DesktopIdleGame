using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Pulsing load labels parented to the gameplay black load fader.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelLoadScreenUI : MonoBehaviour
{
    private const string SettingsIconPath = "Assets/5.Art/Sprites/Basic Icons/SettingsIcon.png";
    private static readonly Color LabelColor = new(0.96862745f, 0.88235295f, 0.74509805f, 1f);
    private static readonly Color MapNameColor = new(0.82f, 0.76f, 0.66f, 1f);

    private static Sprite s_cogSprite;

    [SerializeField] private float pulseCycleSeconds = 1.2f;
    [SerializeField] private float pulseMinAlpha = 0.42f;
    [SerializeField] private float pulseMaxAlpha = 1f;

    private Image _cogImage;
    private TMP_Text _loadingText;
    private TMP_Text _mapNameText;
    private CanvasGroup _contentGroup;
    private Color _cogBaseColor;
    private Color _loadingBaseColor;
    private Color _mapNameBaseColor;
    private bool _pulseActive;

    public static LevelLoadScreenUI EnsureOn(CanvasGroup fader)
    {
        if (!fader)
            return null;

        LevelLoadScreenUI existing = fader.GetComponentInChildren<LevelLoadScreenUI>(true);
        if (existing)
        {
            existing.SetVisible(true);
            existing._pulseActive = true;
            return existing;
        }

        var host = new GameObject(nameof(LevelLoadScreenUI), typeof(RectTransform));
        host.transform.SetParent(fader.transform, false);
        var ui = host.AddComponent<LevelLoadScreenUI>();
        ui.Build((RectTransform)host.transform);
        return ui;
    }

    private void Build(RectTransform root)
    {
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup), typeof(VerticalLayoutGroup));
        content.transform.SetParent(root, false);
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(520f, 220f);

        _contentGroup = content.GetComponent<CanvasGroup>();
        _contentGroup.alpha = 1f;
        _contentGroup.interactable = false;
        _contentGroup.blocksRaycasts = false;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        _cogImage = CreateCogImage(contentRect);
        _cogBaseColor = LabelColor;
        _loadingText = CreateLabel(contentRect, "LoadingText", "Loading", 28f, FontStyles.Bold, LabelColor);
        _loadingBaseColor = LabelColor;
        _mapNameText = CreateLabel(contentRect, "MapNameText", string.Empty, 22f, FontStyles.Normal, MapNameColor);
        _mapNameBaseColor = MapNameColor;
        _pulseActive = true;
    }

    private Image CreateCogImage(RectTransform parent)
    {
        var go = new GameObject("Cog", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 64f;
        layout.preferredHeight = 64f;

        Image image = go.GetComponent<Image>();
        image.sprite = ResolveCogSprite();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = LabelColor;

        return image;
    }

    private static TMP_Text CreateLabel(
        RectTransform parent,
        string objectName,
        string text,
        float fontSize,
        FontStyles style,
        Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = fontSize + 12f;
        layout.minWidth = 280f;

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private void Update()
    {
        if (!_pulseActive || !isActiveAndEnabled)
            return;

        float cycle = Mathf.Max(0.1f, pulseCycleSeconds);
        float wave = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / cycle)) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, wave);
        ApplySynchronizedPulse(alpha);
    }

    private void ApplySynchronizedPulse(float alphaMultiplier)
    {
        if (_cogImage)
        {
            Color c = _cogBaseColor;
            c.a = _cogBaseColor.a * alphaMultiplier;
            _cogImage.color = c;
        }

        if (_loadingText)
        {
            Color c = _loadingBaseColor;
            c.a = _loadingBaseColor.a * alphaMultiplier;
            _loadingText.color = c;
        }

        if (_mapNameText && _mapNameText.gameObject.activeSelf)
        {
            Color c = _mapNameBaseColor;
            c.a = _mapNameBaseColor.a * alphaMultiplier;
            _mapNameText.color = c;
        }
    }

    public void SetMapName(string mapDisplayName)
    {
        if (!_mapNameText)
            return;

        string value = string.IsNullOrWhiteSpace(mapDisplayName) ? string.Empty : mapDisplayName.Trim();
        _mapNameText.text = value;
        _mapNameText.gameObject.SetActive(!string.IsNullOrEmpty(value));
    }

    public void SetVisible(bool visible)
    {
        _pulseActive = visible;

        if (_contentGroup)
            _contentGroup.alpha = visible ? 1f : 0f;
        else
            gameObject.SetActive(visible);

        if (visible)
            ApplySynchronizedPulse(pulseMaxAlpha);
    }

    private static Sprite ResolveCogSprite()
    {
        if (s_cogSprite)
            return s_cogSprite;

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SettingsIconPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == "SettingsIcon_0")
            {
                s_cogSprite = sprite;
                return s_cogSprite;
            }
        }

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                s_cogSprite = sprite;
                return s_cogSprite;
            }
        }
#endif

        s_cogSprite = UILoadingSpriteRefs.LoadingCogSprite;
        if (!s_cogSprite)
            s_cogSprite = UILoadingSpriteRefs.LoadingSpinnerSprite;

        return s_cogSprite;
    }
}
