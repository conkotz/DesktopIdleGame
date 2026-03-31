using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform window;        // panel to move
   // [SerializeField] private float minVisiblePixels = 24f;
    [SerializeField] private bool clampOnEnable = true;

    private RectTransform _parent;
    private Vector2 _pointerOffsetLocal;
    private bool _isDragging;
    private Coroutine _deferredClamp;

    private void Awake()
    {
        if (!window) window = transform as RectTransform;
        _parent = window.parent as RectTransform;
    }

    private void OnEnable()
    {
        if (clampOnEnable)
            ClampNow();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;

        _parent = window.parent as RectTransform;
        if (!_parent) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera, // null for Screen Space Overlay (fine)
            out var pointerLocal
        );

        _pointerOffsetLocal = pointerLocal - window.anchoredPosition;

        if (_deferredClamp != null) { StopCoroutine(_deferredClamp); _deferredClamp = null; }
    }

    public void OnDrag(PointerEventData eventData)
    {
        _parent = window.parent as RectTransform;
        if (!_parent) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out var pointerLocal
        );

        window.anchoredPosition = pointerLocal - _pointerOffsetLocal;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        ClampNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isDragging) return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

        ClampNow();
    }

    public void ClampNow()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

        if (_deferredClamp != null) StopCoroutine(_deferredClamp);
        _deferredClamp = StartCoroutine(ClampNextFrame());
    }

    private IEnumerator ClampNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        _deferredClamp = null;

        if (_isDragging) yield break;
        ClampToScreenEdges();
    }

    [SerializeField] private float edgePadding = 0f; // set to 0 or e.g. 6f if you want a small inset

    private void ClampToScreenEdges()
    {
        if (!window) return;

        // Get window corners in SCREEN space
        Vector3[] wc = new Vector3[4];
        window.GetWorldCorners(wc);

        Vector2 bl = RectTransformUtility.WorldToScreenPoint(null, wc[0]); // bottom-left
        Vector2 tl = RectTransformUtility.WorldToScreenPoint(null, wc[1]); // top-left
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(null, wc[2]); // top-right
        Vector2 br = RectTransformUtility.WorldToScreenPoint(null, wc[3]); // bottom-right

        float left = Mathf.Min(bl.x, tl.x);
        float right = Mathf.Max(tr.x, br.x);
        float bottom = Mathf.Min(bl.y, br.y);
        float top = Mathf.Max(tl.y, tr.y);

        // Hard bounds (inside screen)
        float minLeft = edgePadding;
        float maxRight = Screen.width - edgePadding;
        float minBottom = edgePadding;
        float maxTop = Screen.height - edgePadding;

        Vector2 deltaScreen = Vector2.zero;

        if (left < minLeft) deltaScreen.x += (minLeft - left);
        if (right > maxRight) deltaScreen.x += (maxRight - right);

        if (bottom < minBottom) deltaScreen.y += (minBottom - bottom);
        if (top > maxTop) deltaScreen.y += (maxTop - top);

        if (deltaScreen == Vector2.zero) return;

        // Convert screen delta into parent-local anchoredPosition delta
        var parent = window.parent as RectTransform;
        if (!parent) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, null, out var local0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, deltaScreen, null, out var localDelta);

        window.anchoredPosition += (localDelta - local0);
    }
}