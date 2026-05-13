using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Brings this window (or <see cref="bringToFrontTransform"/>) to the end of its parent's child list so it
/// renders and receives input above sibling windows. Runs on enable and on pointer down on this GameObject
/// (put this component on a raycast-target area such as a title bar, or ensure the window root has a Graphic
/// with Raycast Target enabled where you want click-to-focus).
/// </summary>
public class UIWindowFocus : MonoBehaviour, IPointerDownHandler
{
    private const string LegacyRaycastSinkChildName = "__UIWindowFocus_RaycastSink";

    [Tooltip(
        "When set, reordering uses this transform (e.g. helper panel clicks bring the HelperPopupWindow root forward among WindowsArea siblings). " +
        "When null, this GameObject is moved — same as classic windows whose root has the raycast Image.")]
    [SerializeField]
    private Transform bringToFrontTransform;

    public void SetBringToFrontTransform(Transform root) => bringToFrontTransform = root;

    private void Awake()
    {
        DestroyLegacyRaycastSinkIfPresent();
    }

    private void OnEnable()
    {
        BringToFront();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BringToFront();
    }

    /// <summary>Reorder among parent siblings. Call after <c>SetActive(true)</c> if another script might reorder the same frame.</summary>
    public void BringToFrontNow() => BringToFront();

    private void BringToFront()
    {
        Transform t = bringToFrontTransform != null ? bringToFrontTransform : transform;
        if (t != null && t.parent != null)
            t.SetAsLastSibling();
    }

    /// <summary>Removes sinks from older versions of this component so they cannot block slot clicks.</summary>
    private void DestroyLegacyRaycastSinkIfPresent()
    {
        DestroySinkUnder(transform);
        if (bringToFrontTransform != null && bringToFrontTransform != transform)
            DestroySinkUnder(bringToFrontTransform);
    }

    private static void DestroySinkUnder(Transform root)
    {
        if (root == null)
            return;
        Transform sink = root.Find(LegacyRaycastSinkChildName);
        if (sink != null)
            Destroy(sink.gameObject);
    }
}
