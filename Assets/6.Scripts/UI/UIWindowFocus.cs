using UnityEngine;
using UnityEngine.EventSystems;

public class UIWindowFocus : MonoBehaviour, IPointerDownHandler
{
    [Tooltip(
        "When set, reordering uses this transform (e.g. helper panel clicks bring the HelperPopupWindow root forward among WindowsArea siblings). " +
        "When null, this GameObject is moved — same as classic windows whose root has the raycast Image.")]
    [SerializeField]
    private Transform bringToFrontTransform;

    public void SetBringToFrontTransform(Transform root) => bringToFrontTransform = root;

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
        Transform t = bringToFrontTransform != null ? bringToFrontTransform : transform;
        if (t != null && t.parent != null)
            t.SetAsLastSibling();
    }
}