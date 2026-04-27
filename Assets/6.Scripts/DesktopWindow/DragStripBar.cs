using UnityEngine;
using UnityEngine.EventSystems;

public class DragStripBar : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Refs")]
    [SerializeField] private StripCameraController stripController;

    [Header("Clamp")]
    [Tooltip("Extra normalized space to keep above the bottom edge.")]
    [Range(0f, 1f)]
    [SerializeField] private float bottomPaddingNormalized = 0f;

    private float _startYNorm;
    private float _startMouseY;

    private void Awake()
    {
        if (!stripController)
            stripController = FindFirstObjectByType<StripCameraController>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!stripController) return;

        _startMouseY = eventData.position.y;
        _startYNorm = stripController.BottomNormalized;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!stripController || Screen.height <= 0) return;

        // Pointer positions arrive in screen pixels, then immediately become normalized viewport movement.
        float deltaNormalized = (eventData.position.y - _startMouseY) / Screen.height;
        float minY = Mathf.Clamp01(bottomPaddingNormalized);
        float maxY = 1f - stripController.StripHeightPercent;
        float nextY = Mathf.Clamp(_startYNorm + deltaNormalized, minY, maxY);

        stripController.SetBottomNormalized(nextY);
    }
}