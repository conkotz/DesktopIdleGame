using UnityEngine;
using UnityEngine.EventSystems;

public class DragStripBar : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Refs")]
    [SerializeField] private Camera stripCamera;

    [Header("Strip size (pixels)")]
    [SerializeField] private float stripHeightPx = 360f;

    [Header("Clamp")]
    [Tooltip("Extra pixels to keep above bottom edge (optional).")]
    [SerializeField] private float bottomPaddingPx = 0f;

    private float _startYNorm;
    private float _startMouseY;

    private void Awake()
    {
        if (!stripCamera) stripCamera = GameObject.Find("StripCamera")?.GetComponent<Camera>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!stripCamera) return;

        _startMouseY = eventData.position.y;
        _startYNorm = stripCamera.rect.y;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!stripCamera) return;

        float dyPx = eventData.position.y - _startMouseY;
        float dyNorm = dyPx / Screen.height;

        Rect r = stripCamera.rect;

        // Keep height fixed, only move Y
        float stripHeightNorm = stripHeightPx / Screen.height;

        float minY = bottomPaddingPx / Screen.height;
        float maxY = 1f - stripHeightNorm;

        r.y = Mathf.Clamp(_startYNorm + dyNorm, minY, maxY);
        r.height = stripHeightNorm;

        stripCamera.rect = r;
    }
}