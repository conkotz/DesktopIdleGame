using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag empty space on furnace / cooking panels during gameplay; syncs the shared processing layout proxy.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Graphic))]
public sealed class ProcessingSkillsPanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _panel;
    private Canvas _panelCanvas;
    private RectTransform _parent;
    private Vector2 _pointerOffsetLocal;
    private bool _isDragging;
    private bool _recordAfterClamp;
    private Coroutine _deferredClamp;

    public void Initialize(RectTransform panel, Canvas panelCanvas)
    {
        _panel = panel;
        _panelCanvas = panelCanvas;
        if (_panel)
            UIDragWindowRaycastFilter.EnsureOn(gameObject, _panel);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (MovePivotsModeController.IsPivotModeActive || MovePivotsModeController.IsTestViewActive)
            return;
        if (!_panel)
            return;

        _isDragging = true;
        _parent = _panel.parent as RectTransform;
        if (!_parent)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocal);

        _pointerOffsetLocal = pointerLocal - _panel.anchoredPosition;

        if (_deferredClamp != null)
        {
            StopCoroutine(_deferredClamp);
            _deferredClamp = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (MovePivotsModeController.IsPivotModeActive || MovePivotsModeController.IsTestViewActive)
            return;
        if (!_panel || !_parent)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocal);

        _panel.anchoredPosition = pointerLocal - _pointerOffsetLocal;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        _recordAfterClamp = true;
        ClampNow();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isDragging || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        ClampNow();
    }

    private void ClampNow()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (_deferredClamp != null)
            StopCoroutine(_deferredClamp);
        _deferredClamp = StartCoroutine(ClampNextFrame());
    }

    private IEnumerator ClampNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        _deferredClamp = null;

        if (_isDragging || !_panel)
            yield break;

        ClampToScreenEdges();
        if (_recordAfterClamp)
        {
            _recordAfterClamp = false;
            ProcessingSkillsWindowLayout.RecordSessionFromPanel(_panel, _panelCanvas);
        }
    }

    private void ClampToScreenEdges()
    {
        if (!_panel)
            return;

        Vector3[] wc = new Vector3[4];
        _panel.GetWorldCorners(wc);

        Vector2 bl = RectTransformUtility.WorldToScreenPoint(null, wc[0]);
        Vector2 tl = RectTransformUtility.WorldToScreenPoint(null, wc[1]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(null, wc[2]);
        Vector2 br = RectTransformUtility.WorldToScreenPoint(null, wc[3]);

        float left = Mathf.Min(bl.x, tl.x);
        float right = Mathf.Max(tr.x, br.x);
        float bottom = Mathf.Min(bl.y, br.y);
        float top = Mathf.Max(tl.y, tr.y);

        Vector2 deltaScreen = Vector2.zero;
        if (left < 0f)
            deltaScreen.x += -left;
        if (right > Screen.width)
            deltaScreen.x += Screen.width - right;
        if (bottom < 0f)
            deltaScreen.y += -bottom;
        if (top > Screen.height)
            deltaScreen.y += Screen.height - top;

        if (deltaScreen == Vector2.zero)
            return;

        RectTransform parent = _panel.parent as RectTransform;
        if (!parent)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, null, out Vector2 local0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, deltaScreen, null, out Vector2 localDelta);
        _panel.anchoredPosition += localDelta - local0;
    }
}
