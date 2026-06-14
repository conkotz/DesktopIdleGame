using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Keeps horizontal timeline scrollbar drags from falling through to the ScrollRect content behind it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class TimelineScrollbarInputGuard : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    IScrollHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public void OnPointerDown(PointerEventData eventData) => eventData.Use();
    public void OnPointerUp(PointerEventData eventData) => eventData.Use();
    public void OnPointerClick(PointerEventData eventData) => eventData.Use();
    public void OnScroll(PointerEventData eventData) => eventData.Use();
    public void OnBeginDrag(PointerEventData eventData) => eventData.Use();
    public void OnDrag(PointerEventData eventData) => eventData.Use();
    public void OnEndDrag(PointerEventData eventData) => eventData.Use();
}
