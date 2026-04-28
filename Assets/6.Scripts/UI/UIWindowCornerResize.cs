using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIWindowCornerResize : MonoBehaviour
{
    private static Sprite s_hitSprite;

    public enum ResizeCorner
    {
        BottomLeft,
        TopLeft,
        TopRight,
        BottomRight
    }

    private static readonly Dictionary<string, float> SavedScaleMultipliers = new();

    [SerializeField] private RectTransform targetWindow;
    [SerializeField] private float minScale = 0.75f;
    [SerializeField] private float maxScale = 1.25f;
    [SerializeField] private float handleSize = 28f;
    [SerializeField] private string memoryKey;

    private readonly Vector3[] _corners = new Vector3[4];
    private RectTransform _rect;
    private Vector3 _baseLocalScale = Vector3.one;
    private Vector2 _oppositeCornerScreenPoint;
    private float _resizeStartDistance;
    private float _resizeStartScaleMultiplier = 1f;

    public static UIWindowCornerResize EnsureOn(RectTransform window)
    {
        if (!window)
            return null;

        UIWindowCornerResize resize = window.GetComponent<UIWindowCornerResize>();
        if (!resize)
            resize = window.gameObject.AddComponent<UIWindowCornerResize>();

        resize.targetWindow = window;
        return resize;
    }

    public static void ResetAllScalesToDefault()
    {
        SavedScaleMultipliers.Clear();

        UIWindowCornerResize[] resizers = FindObjectsByType<UIWindowCornerResize>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < resizers.Length; i++)
        {
            if (resizers[i] != null)
                resizers[i].ResetScale();
        }
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
    }

    /// <summary>Apply saved toggle to corner hit targets (also called when settings change).</summary>
    public void RefreshHandlesActive()
    {
        if (!_rect)
            return;

        bool show = ToggleSettingsStore.Get(ToggleSettingId.ShowWindowResizeHandles);

        for (int i = 0; i < 4; i++)
        {
            var corner = (ResizeCorner)i;
            string handleName = $"ResizeHandle_{corner}";
            Transform t = _rect.Find(handleName);
            if (t)
                t.gameObject.SetActive(show);
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
        EnsureHandle(ResizeCorner.TopRight);
        EnsureHandle(ResizeCorner.BottomRight);
    }

    private void EnsureHandle(ResizeCorner corner)
    {
        string handleName = $"ResizeHandle_{corner}";
        Transform existing = _rect.Find(handleName);
        RectTransform handleRect;

        if (existing)
        {
            handleRect = existing as RectTransform;
        }
        else
        {
            GameObject handleObject = new GameObject(handleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(UIWindowResizeHandle));
            handleObject.layer = gameObject.layer;
            handleRect = handleObject.transform as RectTransform;
            handleRect.SetParent(_rect, false);

            Image image = handleObject.GetComponent<Image>();
            ConfigureHitImage(image);

            LayoutElement layoutElement = handleObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
        }

        ConfigureHandleRect(handleRect, corner);

        if (handleRect.TryGetComponent(out Image hitImage))
            ConfigureHitImage(hitImage);

        UIWindowResizeHandle resizeHandle = handleRect.GetComponent<UIWindowResizeHandle>();
        resizeHandle.Configure(this, corner);
        handleRect.SetAsLastSibling();
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
        image.color = new Color(1f, 1f, 1f, 0.02f);
        image.raycastTarget = true;
        image.maskable = true;
    }

    private void ConfigureHandleRect(RectTransform handleRect, ResizeCorner corner)
    {
        Vector2 anchor;
        Vector2 pivot;

        switch (corner)
        {
            case ResizeCorner.BottomLeft:
                anchor = new Vector2(0f, 0f);
                pivot = new Vector2(1f, 1f);
                break;
            case ResizeCorner.TopLeft:
                anchor = new Vector2(0f, 1f);
                pivot = new Vector2(1f, 0f);
                break;
            case ResizeCorner.TopRight:
                anchor = new Vector2(1f, 1f);
                pivot = new Vector2(0f, 0f);
                break;
            default:
                anchor = new Vector2(1f, 0f);
                pivot = new Vector2(0f, 1f);
                break;
        }

        handleRect.anchorMin = anchor;
        handleRect.anchorMax = anchor;
        handleRect.pivot = pivot;
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(handleSize, handleSize);
    }

    private void ApplyScale(float scaleMultiplier)
    {
        float clamped = Mathf.Clamp(scaleMultiplier, minScale, maxScale);
        targetWindow.localScale = new Vector3(
            _baseLocalScale.x * clamped,
            _baseLocalScale.y * clamped,
            _baseLocalScale.z);
    }

    private float GetCurrentScaleMultiplier()
    {
        if (!targetWindow || Mathf.Approximately(_baseLocalScale.x, 0f))
            return 1f;

        return Mathf.Clamp(targetWindow.localScale.x / _baseLocalScale.x, minScale, maxScale);
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
        if (!targetWindow)
            return;

        if (SavedScaleMultipliers.TryGetValue(memoryKey, out float scaleMultiplier))
            ApplyScale(scaleMultiplier);
    }

    private void RememberCurrentScale()
    {
        if (!targetWindow || string.IsNullOrWhiteSpace(memoryKey))
            return;

        SavedScaleMultipliers[memoryKey] = GetCurrentScaleMultiplier();
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
