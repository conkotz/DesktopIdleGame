using UnityEngine;

public class VisualAlignToCollider2D : MonoBehaviour
{
    [SerializeField] private Collider2D targetCollider;     // player collider
    [SerializeField] private Renderer[] renderersToAlign;    // sprite renderers under Soldier
    [SerializeField] private bool alignX = false;
    [SerializeField] private bool alignY = true;

    [Tooltip("Extra lift (+) or drop (-) after alignment.")]
    [SerializeField] private float yNudge = 0f;

    private void Awake()
    {
        if (!targetCollider) targetCollider = GetComponentInParent<Collider2D>();
        if (renderersToAlign == null || renderersToAlign.Length == 0)
            renderersToAlign = GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        // Wait one frame so Animator applies its first pose, then align.
        StartCoroutine(AlignNextFrame());
    }

    private System.Collections.IEnumerator AlignNextFrame()
    {
        yield return null; // 1 frame

        if (!targetCollider) yield break;

        // Bounds of the "visuals"
        Bounds vb = renderersToAlign[0].bounds;
        for (int i = 1; i < renderersToAlign.Length; i++)
            vb.Encapsulate(renderersToAlign[i].bounds);

        // We want visuals bottom (min.y) to sit on collider bottom (bounds.min.y)
        Vector3 delta = Vector3.zero;

        if (alignY)
        {
            float colliderBottom = targetCollider.bounds.min.y;
            float visualsBottom = vb.min.y;
            delta.y = (colliderBottom - visualsBottom) + yNudge;
        }

        if (alignX)
        {
            float colliderCenterX = targetCollider.bounds.center.x;
            float visualsCenterX = vb.center.x;
            delta.x = (colliderCenterX - visualsCenterX);
        }

        transform.position += delta;
    }
}