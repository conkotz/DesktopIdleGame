using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RightEdgeResizer : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private StripCameraController stripController;
    [SerializeField] private bool keepLeftAnchored = true;

    [Header("Limits")]
    [Tooltip("Strip cannot be resized narrower than this fraction of screen width.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float minWidthNormalized = 0.35f;

    [Tooltip("Extra normalized screen width to leave unused on the right edge.")]
    [Range(0f, 1f)]
    [SerializeField] private float screenPaddingNormalized = 0f;

    [Header("Hover (2px line)")]
    [SerializeField] private Image hoverLine;     // assign a 2px Image child (recommended)
    [SerializeField] private float hoverAlpha = 0.35f;

    private float _startMouseX;
    private float _startWidthNormalized;

    private void Awake()
    {
        if (!stripController)
            stripController = FindFirstObjectByType<StripCameraController>();

        if (!hoverLine)
        {
            var t = transform.Find("HoverLine");
            if (t) hoverLine = t.GetComponent<Image>();
        }

        SetHover(false);
    }

    private void OnEnable()
    {
        if (StripCameraController.IsStripLayoutLockedForExpandBackground)
            gameObject.SetActive(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (StripCameraController.IsStripLayoutLockedForExpandBackground)
            return;

        if (!stripController) return;

        _startMouseX = eventData.position.x;
        _startWidthNormalized = stripController.WidthNormalized;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (StripCameraController.IsStripLayoutLockedForExpandBackground)
            return;

        if (!stripController || Screen.width <= 0) return;

        float deltaNormalized = (eventData.position.x - _startMouseX) / Screen.width;
        float maxWidthNormalized = Mathf.Clamp01(1f - screenPaddingNormalized);
        float nextWidth = Mathf.Clamp(
            _startWidthNormalized + deltaNormalized,
            minWidthNormalized,
            maxWidthNormalized);

        // Keep it pinned left so it "shrinks from the right"
        if (keepLeftAnchored)
            stripController.SetLeftNormalized(0f);

        stripController.SetWidthNormalized(nextWidth);
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
