using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIWindowCornerResize : MonoBehaviour
{
    private static Sprite s_hitSprite;
    private const float HandleVisibleAlpha = 0.35f;
    private const float HandleHiddenAlpha = 0.001f;

    private const string ScalePrefsPrefix = "UI.WindowScale.";

    public enum ResizeCorner
    {
        BottomLeft,
        TopLeft,
        TopRight,
        BottomRight
    }

    [SerializeField] private RectTransform targetWindow;
    [SerializeField] private float minScale = 0.75f;
    [SerializeField] private float maxScale = 1.25f;
    [SerializeField] private float handleSize = 28f;
    [SerializeField] private string memoryKey;

    [Tooltip("When true, TopLeft hit target is not created (resize from bottom corners only). TopRight is always omitted globally.")]
    [SerializeField] private bool omitTopCornerHandles;

    [Tooltip("When true, BottomRight hit target is not created (e.g. helper title bar: avoid handle over close/minimize).")]
    [SerializeField] private bool omitBottomRightCornerHandle;

    [Tooltip(
        "When set, BottomLeft/BottomRight handles are parented under this rect (anchors stay bottom-left / bottom-right of it). " +
        "Use when the window root rect does not match the visual bottom of scroll/list content (e.g. quest tracker with preferred-height body). " +
        "Resize still scales the target window.")]
    [SerializeField] private RectTransform bottomResizeHandleParent;

    /// <summary>
    /// Divides authored local scale by <see cref="SliderSettingId.HudResize"/> each frame-ish so apparent size stays constant while the HUD canvas scaler changes.
    /// </summary>
    [SerializeField] private bool counterHudCanvasScale;

    private bool _subscribedHudSlider;

    private readonly Vector3[] _corners = new Vector3[4];
    private RectTransform _rect;
    private Vector3 _baseLocalScale = Vector3.one;
    private Vector2 _oppositeCornerScreenPoint;
    private float _resizeStartDistance;
    private float _resizeStartScaleMultiplier = 1f;

    public static UIWindowCornerResize EnsureOn(RectTransform window) =>
        EnsureOn(window, false, false, false);

    public static UIWindowCornerResize EnsureOn(
        RectTransform window,
        bool omitTopCornerHandles,
        bool counterHudCanvasScale,
        bool omitBottomRightCornerHandle = false)
    {
        if (!window)
            return null;

        UIWindowCornerResize resize = window.GetComponent<UIWindowCornerResize>();
        if (!resize)
            resize = window.gameObject.AddComponent<UIWindowCornerResize>();

        resize.targetWindow = window;
        resize.omitTopCornerHandles = omitTopCornerHandles;
        resize.counterHudCanvasScale = counterHudCanvasScale;
        resize.omitBottomRightCornerHandle = omitBottomRightCornerHandle;
        resize.ResolveTarget();
        resize.EnsureHandles();
        resize.RefreshHandlesActive();

        float remembered = resize.GetPersistedCornerScaleMultiplier();

        resize.ApplyScale(remembered);
        return resize;
    }

    /// <summary>
    /// Parents bottom corner hit targets under <paramref name="parent"/> so they track that rect's bottom edge (see <see cref="bottomResizeHandleParent"/>).
    /// Pass null to parent them back on <see cref="targetWindow"/>.
    /// </summary>
    public void SetBottomResizeHandleParent(RectTransform parent)
    {
        bottomResizeHandleParent = parent;
        EnsureHandles();
        RefreshHandlesActive();
    }

    public static void ResetAllScalesToDefault()
    {
        UIWindowCornerResize[] resizers = FindObjectsByType<UIWindowCornerResize>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < resizers.Length; i++)
        {
            if (resizers[i] == null)
                continue;

            resizers[i].DeletePersistedScale();
            resizers[i].ResetScale();
        }

        PlayerPrefs.Save();
    }

    private void Awake()
    {
        ResolveTarget();
        _baseLocalScale = targetWindow ? targetWindow.localScale : Vector3.one;

        if (string.IsNullOrWhiteSpace(memoryKey))
            memoryKey = ResolveMemoryKey();

        EnsureHandles();
    }

    private void OnEnable()
    {
        ResolveTarget();
        RestoreRememberedScale();
        EnsureHandles();
        RefreshHandlesActive();
        SubscribeHudResizeIfNeeded();
    }

    private void OnDisable() => UnsubscribeHudResize();

    private void SubscribeHudResizeIfNeeded()
    {
        if (!counterHudCanvasScale || _subscribedHudSlider)
            return;

        SliderSettingsStore.Changed += OnSliderHudResizeChanged;
        _subscribedHudSlider = true;
    }

    private void UnsubscribeHudResize()
    {
        if (!_subscribedHudSlider)
            return;

        SliderSettingsStore.Changed -= OnSliderHudResizeChanged;
        _subscribedHudSlider = false;
    }

    private void OnSliderHudResizeChanged(SliderSettingId id, float _)
    {
        if (id != SliderSettingId.HudResize || !counterHudCanvasScale || !enabled)
            return;

        ApplyScale(GetCurrentScaleMultiplier());
    }

    /// <summary>Apply saved toggle to corner hit targets (also called when settings change).</summary>
    public void RefreshHandlesActive()
    {
        if (!_rect)
            return;

        bool showVisual = ToggleSettingsStore.Get(ToggleSettingId.ShowWindowResizeHandles);

        for (int i = 0; i < 4; i++)
        {
            var corner = (ResizeCorner)i;
            if (corner == ResizeCorner.TopRight)
                continue;
            if (omitTopCornerHandles && corner == ResizeCorner.TopLeft)
                continue;
            if (omitBottomRightCornerHandle && corner == ResizeCorner.BottomRight)
                continue;

            string handleName = $"ResizeHandle_{corner}";
            Transform t = FindResizeHandleTransform(handleName);
            if (t)
            {
                t.gameObject.SetActive(true); // Always interactive; toggle controls visual hint only.
                if (t.TryGetComponent(out Image img))
                    ApplyHandleVisual(img, showVisual);
            }
        }
    }

    public static void RefreshAllHandlesVisibility()
    {
        UIWindowCornerResize[] resizers = FindObjectsByType<UIWindowCornerResize>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < resizers.Length; i++)
        {
            if (resizers[i] != null)
                resizers[i].RefreshHandlesActive();
        }
    }

    public void BeginResize(UIWindowResizeHandle handle, PointerEventData eventData)
    {
        if (!targetWindow || handle == null)
            return;

        targetWindow.GetWorldCorners(_corners);
        _oppositeCornerScreenPoint = RectTransformUtility.WorldToScreenPoint(
            eventData.pressEventCamera,
            _corners[GetOppositeCornerIndex(handle.Corner)]);

        _resizeStartDistance = Mathf.Max(
            1f,
            Vector2.Distance(eventData.position, _oppositeCornerScreenPoint));
        _resizeStartScaleMultiplier = GetCurrentScaleMultiplier();
    }

    public void Resize(PointerEventData eventData)
    {
        if (!targetWindow)
            return;

        float distance = Mathf.Max(
            1f,
            Vector2.Distance(eventData.position, _oppositeCornerScreenPoint));
        float scaleMultiplier = _resizeStartScaleMultiplier * (distance / _resizeStartDistance);

        ApplyScale(scaleMultiplier);
    }

    public void EndResize()
    {
        RememberCurrentScale();
        ClampDragWindows();
    }

    public void ResetScale()
    {
        if (!targetWindow)
            return;

        targetWindow.localScale = _baseLocalScale;
    }

    /// <summary>Clears saved corner-scale prefs and applies a neutral multiplier (for per-window factory reset).</summary>
    public void ForgetPersistedScaleAndResetToBase()
    {
        DeletePersistedScale();
        ApplyScale(1f);
    }

    private void ResolveTarget()
    {
        if (!targetWindow)
            targetWindow = transform as RectTransform;

        _rect = targetWindow;
    }

    private void EnsureHandles()
    {
        if (!_rect)
            return;

        EnsureHandle(ResizeCorner.BottomLeft);
        EnsureHandle(ResizeCorner.TopLeft);
        EnsureHandle(ResizeCorner.BottomRight);
    }

    private void EnsureHandle(ResizeCorner corner)
    {
        if (corner == ResizeCorner.TopRight)
        {
            string topRightName = $"ResizeHandle_{corner}";
            Transform oldTopRight = FindResizeHandleTransform(topRightName);
            if (oldTopRight)
                Destroy(oldTopRight.gameObject);
            return;
        }

        if (omitTopCornerHandles && corner == ResizeCorner.TopLeft)
        {
            string killName = $"ResizeHandle_{corner}";
            Transform old = FindResizeHandleTransform(killName);
            if (old)
                Destroy(old.gameObject);

            return;
        }

        if (omitBottomRightCornerHandle && corner == ResizeCorner.BottomRight)
        {
            string killName = $"ResizeHandle_{corner}";
            Transform old = FindResizeHandleTransform(killName);
            if (old)
                Destroy(old.gameObject);

            return;
        }

        string handleName = $"ResizeHandle_{corner}";
        Transform designatedParent = GetResizeHandleParent(corner);
        Transform existing = FindResizeHandleTransform(handleName);
        RectTransform handleRect;

        if (existing)
        {
            handleRect = existing as RectTransform;
            if (handleRect && handleRect.parent != designatedParent)
                handleRect.SetParent(designatedParent, false);
        }
        else
        {
            GameObject handleObject = new GameObject(handleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(UIWindowResizeHandle));
            handleObject.layer = gameObject.layer;
            handleRect = handleObject.transform as RectTransform;
            handleRect.SetParent(designatedParent, false);

            Image image = handleObject.GetComponent<Image>();
            ConfigureHitImage(image);

            LayoutElement layoutElement = handleObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
        }

        ConfigureHandleRect(handleRect, corner);

        if (handleRect.TryGetComponent(out Image hitImage))
        {
            ConfigureHitImage(hitImage);
            ApplyHandleVisual(hitImage, ToggleSettingsStore.Get(ToggleSettingId.ShowWindowResizeHandles));
        }

        UIWindowResizeHandle resizeHandle = handleRect.GetComponent<UIWindowResizeHandle>();
        resizeHandle.Configure(this, corner);
        handleRect.SetAsLastSibling();
    }

    private Transform GetResizeHandleParent(ResizeCorner corner)
    {
        bool bottom = corner is ResizeCorner.BottomLeft or ResizeCorner.BottomRight;
        if (bottom && bottomResizeHandleParent)
            return bottomResizeHandleParent;
        return _rect;
    }

    private Transform FindResizeHandleTransform(string handleName)
    {
        if (_rect)
        {
            Transform t = _rect.Find(handleName);
            if (t)
                return t;
        }

        if (bottomResizeHandleParent && bottomResizeHandleParent != _rect)
        {
            Transform t = bottomResizeHandleParent.Find(handleName);
            if (t)
                return t;
        }

        return null;
    }

    private static Sprite GetOrCreateHitSprite()
    {
        if (s_hitSprite)
            return s_hitSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_hitSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_hitSprite;
    }

    private static void ConfigureHitImage(Image image)
    {
        if (!image)
            return;

        image.sprite = GetOrCreateHitSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = new Color(1f, 1f, 1f, HandleHiddenAlpha);
        image.raycastTarget = true;
        image.maskable = true;
    }

    private static void ApplyHandleVisual(Image image, bool showVisual)
    {
        if (!image)
            return;

        Color c = image.color;
        c.a = showVisual ? HandleVisibleAlpha : HandleHiddenAlpha;
        image.color = c;
        image.raycastTarget = true;
    }

    private void ConfigureHandleRect(RectTransform handleRect, ResizeCorner corner)
    {
        Vector2 anchor;
        Vector2 pivot;

        switch (corner)
        {
            case ResizeCorner.BottomLeft:
                anchor = new Vector2(0f, 0f);
                pivot = new Vector2(0f, 0f);
                break;
            case ResizeCorner.TopLeft:
                anchor = new Vector2(0f, 1f);
                pivot = new Vector2(0f, 1f);
                break;
            case ResizeCorner.TopRight:
                anchor = new Vector2(1f, 1f);
                pivot = new Vector2(1f, 1f);
                break;
            default:
                anchor = new Vector2(1f, 0f);
                pivot = new Vector2(1f, 0f);
                break;
        }

        handleRect.anchorMin = anchor;
        handleRect.anchorMax = anchor;
        handleRect.pivot = pivot;
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(handleSize, handleSize);
    }

    private float GetHudScaleCorrection() =>
        counterHudCanvasScale ? Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.HudResize)) : 1f;

    private void ApplyScale(float scaleMultiplier)
    {
        float clamped = Mathf.Clamp(scaleMultiplier, minScale, maxScale);
        float h = GetHudScaleCorrection();
        float v = clamped / h;

        targetWindow.localScale = new Vector3(
            _baseLocalScale.x * v,
            _baseLocalScale.y * v,
            _baseLocalScale.z);
    }

    private float GetCurrentScaleMultiplier()
    {
        if (!targetWindow || Mathf.Approximately(_baseLocalScale.x, 0f))
            return 1f;

        float h = GetHudScaleCorrection();
        return Mathf.Clamp((targetWindow.localScale.x / _baseLocalScale.x) * h, minScale, maxScale);
    }

    private int GetOppositeCornerIndex(ResizeCorner corner)
    {
        switch (corner)
        {
            case ResizeCorner.BottomLeft:
                return 2;
            case ResizeCorner.TopLeft:
                return 3;
            case ResizeCorner.TopRight:
                return 0;
            default:
                return 1;
        }
    }

    private void RestoreRememberedScale()
    {
        ApplyScale(GetPersistedCornerScaleMultiplier());
    }

    private float GetPersistedCornerScaleMultiplier()
    {
        if (!targetWindow || string.IsNullOrWhiteSpace(memoryKey))
            return 1f;

        float multiplier = 1f;
        string prefsKey = GetScalePrefsKey(memoryKey);

        if (PlayerPrefs.HasKey(prefsKey))
            multiplier = PlayerPrefs.GetFloat(prefsKey, 1f);

        return Mathf.Clamp(multiplier, minScale, maxScale);
    }

    private void RememberCurrentScale()
    {
        if (!targetWindow || string.IsNullOrWhiteSpace(memoryKey))
            return;

        float mult = GetCurrentScaleMultiplier();
        PlayerPrefs.SetFloat(GetScalePrefsKey(memoryKey), mult);
        PlayerPrefs.Save();
    }

    private void DeletePersistedScale()
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return;

        PlayerPrefs.DeleteKey(GetScalePrefsKey(memoryKey));
    }

    private static string GetScalePrefsKey(string key)
    {
        return ScalePrefsPrefix + key.Trim();
    }

    private void ClampDragWindows()
    {
        UIDragWindow[] dragWindows = GetComponentsInChildren<UIDragWindow>(true);
        for (int i = 0; i < dragWindows.Length; i++)
        {
            if (dragWindows[i] != null)
                dragWindows[i].ClampNow();
        }
    }

    private string ResolveMemoryKey()
    {
        if (targetWindow != null && !string.IsNullOrWhiteSpace(targetWindow.name))
            return targetWindow.name;

        return gameObject.name;
    }
}

public sealed class UIWindowResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private UIWindowCornerResize _owner;
    private UIWindowCornerResize.ResizeCorner _corner;

    public UIWindowCornerResize.ResizeCorner Corner => _corner;

    public void Configure(UIWindowCornerResize owner, UIWindowCornerResize.ResizeCorner corner)
    {
        _owner = owner;
        _corner = corner;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _owner?.BeginResize(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _owner?.Resize(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _owner?.EndResize();
    }
}
