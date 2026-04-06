using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Optional standalone hover highlighter. If a <see cref="WorldInputRouter2D"/> is present with hover enabled,
/// this component does nothing so hover and clicks always use the same <see cref="WorldClickPicker2D"/> winner.
/// </summary>
public class HoverPicker2D : MonoBehaviour
{
    [SerializeField] private LayerMask pickMask = ~0;

    [Tooltip("Leave empty to use Camera.main — should match WorldInputRouter2D's camera for consistent picks.")]
    [SerializeField] private Camera cam;

    private SimpleHoverHighlight2D _currentHovered;
    private WorldInputRouter2D _router;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        _router = FindFirstObjectByType<WorldInputRouter2D>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (ShouldDeferToRouter())
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        if (!cam) cam = Camera.main;
        if (!cam) return;

        Vector3 w3 = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 wp = new Vector2(w3.x, w3.y);

        Collider2D winner = WorldClickPicker2D.PickTopmostAtPoint(wp, pickMask);
        var highlight = winner ? winner.GetComponentInParent<SimpleHoverHighlight2D>() : null;

        if (_currentHovered != highlight)
        {
            if (_currentHovered) _currentHovered.SetHovered(false);
            _currentHovered = highlight;
            if (_currentHovered) _currentHovered.SetHovered(true);
        }
    }

    private bool ShouldDeferToRouter()
    {
        if (_router == null)
            _router = FindFirstObjectByType<WorldInputRouter2D>(FindObjectsInactive.Include);
        return _router != null && _router.isActiveAndEnabled && _router.HoverHighlightEnabled;
    }

    private void ClearHover()
    {
        if (_currentHovered)
        {
            _currentHovered.SetHovered(false);
            _currentHovered = null;
        }
    }

    /// <summary>Same winner as <see cref="WorldClickPicker2D"/>; kept for any external callers.</summary>
    public SimpleHoverHighlight2D PickTopmostUnderMouse()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return null;

        Vector3 w3 = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 wp = new Vector2(w3.x, w3.y);

        Collider2D winner = WorldClickPicker2D.PickTopmostAtPoint(wp, pickMask);
        return winner ? winner.GetComponentInParent<SimpleHoverHighlight2D>() : null;
    }
}
