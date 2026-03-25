using UnityEngine;
using UnityEngine.EventSystems;

public class UIWindowFocus : MonoBehaviour, IPointerDownHandler
{
    private void OnEnable()
    {
        BringToFront();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BringToFront();
    }

    private void BringToFront()
    {
        if (transform.parent != null)
            transform.SetAsLastSibling();
    }
}