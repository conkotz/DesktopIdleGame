using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shop-only: lets the full-width <see cref="UIDragWindow"/> header receive drags on empty space while passing
/// clicks through to buttons underneath. Wired from <see cref="ShopUI.EnsureShopDragHandleOnTop"/> only.
/// Uses rect overlap checks (never <see cref="EventSystem.RaycastAll"/> here — that re-enters
/// <see cref="ICanvasRaycastFilter"/> and overflows the stack).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class UIDragWindowRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField] private RectTransform windowRoot;

    private readonly List<RectTransform> _interactiveRects = new List<RectTransform>(32);
    private RectTransform _dragRoot;

    public static UIDragWindowRaycastFilter EnsureOn(GameObject dragHandle, RectTransform window)
    {
        if (!dragHandle)
            return null;

        if (!dragHandle.TryGetComponent(out UIDragWindowRaycastFilter filter))
            filter = dragHandle.AddComponent<UIDragWindowRaycastFilter>();

        if (filter == null)
            return null;

        filter._dragRoot = dragHandle.transform as RectTransform;
        filter.windowRoot = window != null ? window : filter._dragRoot;
        filter.RebuildInteractiveRects();
        return filter;
    }

    private void OnEnable()
    {
        if (!windowRoot)
            return;

        RebuildInteractiveRects();
    }

    public void RebuildInteractiveRects()
    {
        _interactiveRects.Clear();
        RectTransform window = windowRoot != null ? windowRoot : _dragRoot;
        if (!window)
            return;

        CollectComponentRects<Selectable>(window);
        CollectComponentRects<InventorySlotUI>(window);
        CollectComponentRects<StorageSlotUI>(window);
        CollectComponentRects<ShopSlotUI>(window);
        CollectComponentRects<ScrollRect>(window);
        CollectComponentRects<TMPro.TMP_InputField>(window);
        CollectComponentRects<UIWindowCloseButton>(window);
        CollectComponentRects<UIWindowResizeHandle>(window);
    }

    private void CollectComponentRects<T>(RectTransform window) where T : Component
    {
        T[] items = window.GetComponentsInChildren<T>(true);
        for (int i = 0; i < items.Length; i++)
        {
            T item = items[i];
            if (item == null)
                continue;

            RectTransform rt = item.transform as RectTransform;
            if (rt == null || rt == _dragRoot)
                continue;

            if (!_interactiveRects.Contains(rt))
                _interactiveRects.Add(rt);
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!_dragRoot)
            _dragRoot = transform as RectTransform;

        if (!_dragRoot || !RectTransformUtility.RectangleContainsScreenPoint(_dragRoot, screenPoint, eventCamera))
            return false;

        return !IsPointerOverCachedInteractive(screenPoint, eventCamera);
    }

    private bool IsPointerOverCachedInteractive(Vector2 screenPoint, Camera eventCamera)
    {
        for (int i = 0; i < _interactiveRects.Count; i++)
        {
            RectTransform rt = _interactiveRects[i];
            if (rt == null || !rt.gameObject.activeInHierarchy)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, eventCamera))
                continue;

            if (!IsRectRaycastable(rt))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsRectRaycastable(RectTransform rt)
    {
        Graphic graphic = rt.GetComponent<Graphic>();
        if (graphic != null && graphic.raycastTarget && graphic.enabled)
            return true;

        Selectable selectable = rt.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable();
    }
}
