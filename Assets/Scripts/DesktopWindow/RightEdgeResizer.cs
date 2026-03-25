using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RightEdgeResizer : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private StripViewport stripViewport;   // <-- assign your StripViewport script
    [SerializeField] private Camera stripCamera;            // optional; will auto find from StripViewport
    [SerializeField] private bool keepLeftAnchored = true;  // keeps x=0 while resizing

    [Header("Limits (pixels)")]
    [SerializeField] private float minWidthPx = 600f;

    [Tooltip("Extra pixels to subtract from screen width (breathing room).")]
    [SerializeField] private float screenPaddingPx = 0f;

    [Header("Hover (2px line)")]
    [SerializeField] private Image hoverLine;     // assign a 2px Image child (recommended)
    [SerializeField] private float hoverAlpha = 0.35f;

    private float _startMouseX;
    private float _startWidthPx;

    private void Awake()
    {
        if (!stripViewport) stripViewport = FindFirstObjectByType<StripViewport>();
        if (!stripCamera && stripViewport) stripCamera = stripViewport.GetComponent<Camera>();

        if (!hoverLine)
        {
            var t = transform.Find("HoverLine");
            if (t) hoverLine = t.GetComponent<Image>();
        }

        SetHover(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!stripViewport || !stripCamera) return;

        _startMouseX = eventData.position.x;

        // current strip width in pixels from camera viewport rect
        _startWidthPx = stripCamera.rect.width * Screen.width;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!stripViewport || !stripCamera) return;

        float deltaX = eventData.position.x - _startMouseX;

        float maxWidthPx = Mathf.Max(0f, Screen.width - screenPaddingPx);
        if (maxWidthPx <= 0f) maxWidthPx = Screen.width;

        float newWidthPx = Mathf.Clamp(_startWidthPx + deltaX, minWidthPx, maxWidthPx);

        float wNorm = Mathf.Clamp01(newWidthPx / Screen.width);

        // Keep it pinned left so it "shrinks from the right"
        if (keepLeftAnchored)
            stripViewport.SetLeftNormalized(0f);

        stripViewport.SetWidthNormalized(wNorm);
    }

    public void OnPointerEnter(PointerEventData eventData) => SetHover(true);
    public void OnPointerExit(PointerEventData eventData) => SetHover(false);

    private void SetHover(bool on)
    {
        if (!hoverLine) return;

        var c = hoverLine.color;
        c.a = on ? hoverAlpha : 0f;
        hoverLine.color = c;
    }
}