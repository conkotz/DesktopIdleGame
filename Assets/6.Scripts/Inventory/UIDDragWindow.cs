using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform window;        // panel to move
   // [SerializeField] private float minVisiblePixels = 24f;
    [SerializeField] private bool clampOnEnable = true;
    [SerializeField] private string memoryKey;

    private RectTransform _parent;
    private Vector2 _pointerOffsetLocal;
    private Vector2 _anchorPoint;
    private bool _isDragging;
    private bool _recordAfterClamp;
    private Coroutine _deferredClamp;
    private bool _windowWasAutoAssigned;

    private string _prefsPosKeyX;
    private string _prefsPosKeyY;

    private void Awake()
    {
        if (!window)
        {
            window = transform as RectTransform;
            _windowWasAutoAssigned = true;
        }
        _parent = window.parent as RectTransform;
        _anchorPoint = window ? window.anchoredPosition : Vector2.zero;
        if (string.IsNullOrWhiteSpace(memoryKey))
            memoryKey = ResolveMemoryKey();

        // Keep resize handles working for windows that assign "window" in the inspector.
        // For runtime-created drag strips (like the Helper chrome), "window" is auto-assigned to the strip itself,
        // so we must NOT auto-create resize handles on the strip.
        if (!_windowWasAutoAssigned && window)
            UIWindowCornerResize.EnsureOn(window);
    }

    /// <summary>Override auto key (e.g. runtime-built drag strips call <see cref="AttachWindow"/> after Awake).</summary>
    public void SetRuntimeMemoryKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            memoryKey = key.Trim();
    }

    /// <summary>Persist <see cref="RectTransform.anchoredPosition"/> to <see cref="PlayerPrefs"/> (machine-local).</summary>
    public void UsePlayerPrefsForAnchoredPosition(string prefsKeyX, string prefsKeyY)
    {
        _prefsPosKeyX = prefsKeyX;
        _prefsPosKeyY = prefsKeyY;
    }

    /// <summary>
    /// Use when adding <see cref="UIDragWindow"/> from code — sets the movable panel (e.g. handle on header, window is outer panel).
    /// </summary>
    public void AttachWindow(RectTransform targetWindow) =>
        AttachWindow(targetWindow, false, false, false);

    /// <param name="omitTopCornerHandles">Forwarded to <see cref="UIWindowCornerResize"/> on the movable window.</param>
    /// <param name="counterHudCanvasScale">Keeps authored size stable when HUD canvas applies <see cref="SliderSettingId.HudResize"/>.</param>
    /// <param name="omitBottomRightCornerHandle">Forwarded to <see cref="UIWindowCornerResize"/> (e.g. helper chrome strip).</param>
    public void AttachWindow(
        RectTransform targetWindow,
        bool omitTopCornerHandles,
        bool counterHudCanvasScale,
        bool omitBottomRightCornerHandle = false)
    {
        window = targetWindow ? targetWindow : transform as RectTransform;
        UIWindowCornerResize.EnsureOn(window, omitTopCornerHandles, counterHudCanvasScale, omitBottomRightCornerHandle);
    }

    private void OnEnable()
    {
        if (!MovePivotsModeController.IsTestViewActive)
            RestoreRememberedPosition();
        if (clampOnEnable && !MovePivotsModeController.IsTestViewActive)
            ClampNow();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (MovePivotsModeController.IsTestViewActive)
            return;

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
        if (MovePivotsModeController.IsTestViewActive)
            return;

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
        _recordAfterClamp = true;
        ClampNow();
    }

    public void ResetToAnchorPoint()
    {
        if (!window) return;

        if (_deferredClamp != null)
        {
            StopCoroutine(_deferredClamp);
            _deferredClamp = null;
        }

        window.anchoredPosition = _anchorPoint;
        _recordAfterClamp = false;
    }

    /// <summary>
    /// Updates the position used by <see cref="ResetToAnchorPoint"/> (e.g. after Settings reset all windows so the helper
    /// uses <see cref="HelperGameplayController"/> inspector defaults instead of an Awake-captured top-left value).
    /// </summary>
    public void SetResetAnchorPoint(Vector2 anchoredPosition)
    {
        _anchorPoint = anchoredPosition;
        if (window)
            window.anchoredPosition = anchoredPosition;
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
        if (_recordAfterClamp)
        {
            _recordAfterClamp = false;
            RememberCurrentPosition();
        }
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

    private void RestoreRememberedPosition()
    {
        if (!window)
            return;

        // Session drag/position wins during play. Pivot layout is applied on session start (see UIWindowLayoutBinding).
        if (UIWindowLayoutBinding.IsKnownPivotWindow(memoryKey) &&
            UIWindowSessionLayoutMemory.TryGet(memoryKey, out UIWindowLayoutPrefs.Snapshot sessionLayout))
        {
            UIWindowLayoutPrefs.Apply(window, sessionLayout);
            if (UIWindowLayoutBinding.IsQuestTrackerWindow(memoryKey))
                UIWindowLayoutBinding.EnsureQuestTrackerTopAnchoredLayout(window);
            return;
        }

        if (UIWindowPositionMemory.TryGet(memoryKey, out Vector2 sessionPosition))
        {
            window.anchoredPosition = sessionPosition;
            if (UIWindowLayoutBinding.IsQuestTrackerWindow(memoryKey))
                UIWindowLayoutBinding.EnsureQuestTrackerTopAnchoredLayout(window);
            return;
        }

        if (MovePivotsModeController.IsPivotModeActive || MovePivotsModeController.IsTestViewActive)
            return;

        if (UIWindowLayoutPrefs.HasSaved(memoryKey))
        {
            UIWindowLayoutPrefs.TryLoadAndApply(window, memoryKey);
            UIWindowPositionMemory.Save(memoryKey, window.anchoredPosition);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_prefsPosKeyX) &&
            !string.IsNullOrWhiteSpace(_prefsPosKeyY) &&
            PlayerPrefs.HasKey(_prefsPosKeyX) &&
            PlayerPrefs.HasKey(_prefsPosKeyY))
        {
            var fromDisk = new Vector2(PlayerPrefs.GetFloat(_prefsPosKeyX), PlayerPrefs.GetFloat(_prefsPosKeyY));
            window.anchoredPosition = fromDisk;
            UIWindowPositionMemory.Save(memoryKey, fromDisk);
        }
    }

    private void RememberCurrentPosition()
    {
        if (!window)
            return;

        // Session-only: pivot layout is saved only via Move Pivots mode (UIWindowLayoutBinding.SaveCurrentLayout).
        UIWindowPositionMemory.Save(memoryKey, window.anchoredPosition);
        if (UIWindowLayoutBinding.IsKnownPivotWindow(memoryKey))
            UIWindowSessionLayoutMemory.Capture(window, memoryKey);

        if (!string.IsNullOrWhiteSpace(_prefsPosKeyX) && !string.IsNullOrWhiteSpace(_prefsPosKeyY))
        {
            PlayerPrefs.SetFloat(_prefsPosKeyX, window.anchoredPosition.x);
            PlayerPrefs.SetFloat(_prefsPosKeyY, window.anchoredPosition.y);
            PlayerPrefs.Save();
        }
    }

    private string ResolveMemoryKey()
    {
        if (window != null && !string.IsNullOrWhiteSpace(window.name))
            return window.name;

        return gameObject.name;
    }
}