using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single off-screen indicator row (circle + arrow + TMP label). Driven by <see cref="OffscreenMarkersController"/>.
/// </summary>
[DisallowMultipleComponent]
public class OffscreenMarkerView : MonoBehaviour
{
    public const float DefaultAlpha = 175f / 255f;

    [SerializeField] private Image circleImage;
    [SerializeField] private Image arrowImage;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private RectTransform labelRect;

    private Vector3 _initialLocalScale = Vector3.one;

    /// <summary>Set from <see cref="Apply"/>; used to pin this row to the left or right screen edge.</summary>
    public bool DockedLeft { get; private set; }

    private void Awake()
    {
        if (!circleImage) circleImage = GetComponent<Image>();
        if (!arrowImage) arrowImage = transform.Find("Arrow")?.GetComponent<Image>();
        if (!label) label = transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (!labelRect && label) labelRect = label.rectTransform;

        _initialLocalScale = transform.localScale;
        ApplyNonInteractiveDefaults();
    }

    private void ApplyNonInteractiveDefaults()
    {
        if (circleImage) circleImage.raycastTarget = false;
        if (arrowImage) arrowImage.raycastTarget = false;
        if (label) label.raycastTarget = false;
    }

    /// <param name="rgb">Color without alpha; alpha is forced to <see cref="DefaultAlpha"/> on images.</param>
    /// <param name="flipX">True when the group is off-screen to the left (mirror on X).</param>
    public void Apply(Color rgb, string text, bool flipX)
    {
        DockedLeft = flipX;
        ApplyNonInteractiveDefaults();

        Color c = rgb;
        c.a = DefaultAlpha;
        if (circleImage) circleImage.color = c;
        if (arrowImage) arrowImage.color = c;

        if (label) label.text = text;

        float mag = Mathf.Max(0.01f, Mathf.Abs(_initialLocalScale.x));
        float sx = flipX ? -mag : mag;
        transform.localScale = new Vector3(sx, _initialLocalScale.y, _initialLocalScale.z);

        // Counter parent X flip so TMP stays readable (uniform undo of parent scale).
        if (labelRect)
        {
            float inv = 1f / mag;
            labelRect.localScale = new Vector3(flipX ? -inv : inv, inv, inv);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!labelRect && label) labelRect = label.rectTransform;
    }
#endif
}
