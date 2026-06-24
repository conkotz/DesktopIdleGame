using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fades the skill-level chrome when the cursor is near so timeline nodes underneath stay readable.
/// Does not block pointer input to nodes behind the panel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class SkillLevelPanelHoverDimUI : MonoBehaviour
{
    [SerializeField] private float nearPaddingPixels = 72f;
    [Tooltip("Alpha while the cursor is near the panel (0.15 = 85% dimmed).")]
    [SerializeField] private float dimmedAlpha = 0.15f;
    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float fadeSpeed = 14f;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Canvas _rootCanvas;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        _canvasGroup = GetComponent<CanvasGroup>();
        _rootCanvas = GetComponentInParent<Canvas>();

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        DisableChildRaycasts();
    }

    private void DisableChildRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void Update()
    {
        if (_canvasGroup == null || _rectTransform == null)
            return;

        float target = IsCursorNearPanel() ? dimmedAlpha : normalAlpha;
        _canvasGroup.alpha = Mathf.MoveTowards(
            _canvasGroup.alpha,
            target,
            fadeSpeed * Time.unscaledDeltaTime);
    }

    private bool IsCursorNearPanel()
    {
        Camera eventCamera = null;
        if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = _rootCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                Input.mousePosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect expanded = _rectTransform.rect;
        expanded.xMin -= nearPaddingPixels;
        expanded.yMin -= nearPaddingPixels;
        expanded.xMax += nearPaddingPixels;
        expanded.yMax += nearPaddingPixels;
        return expanded.Contains(localPoint);
    }
}
