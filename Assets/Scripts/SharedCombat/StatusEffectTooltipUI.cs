using TMPro;
using UnityEngine;

public class StatusEffectTooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Vector2 offset = new Vector2(16f, -16f);

    private Canvas _canvas;

    private void Awake()
    {
        if (!root) root = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();

        HideImmediate();
    }

    private void Update()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        FollowMouse();
    }

    public void Show(string title, string body)
    {
        if (titleText) titleText.text = title ?? "";
        if (bodyText) bodyText.text = body ?? "";

        if (root) root.gameObject.SetActive(true);
        FollowMouse();
    }

    public void Hide()
    {
        if (root) root.gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        if (root) root.gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (root == null || _canvas == null)
            return;

        RectTransform canvasRect = _canvas.transform as RectTransform;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out localPoint
        );

        root.anchoredPosition = localPoint + offset;
    }
}