using UnityEngine;

/// <summary>
/// Optional per-world-object anchor for <see cref="PlayerWorldTargetIndicator"/>.
/// When absent, the indicator uses collider/sprite bounds or transform position.
/// </summary>
[DisallowMultipleComponent]
public class WorldTargetIndicatorAnchor : MonoBehaviour
{
    [Tooltip("World position follows this transform. When unset, uses this GameObject's transform.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Extra offset in anchor local space (e.g. raise above the head).")]
    [SerializeField] private Vector3 localOffset;

    public Vector3 LocalOffset
    {
        get => localOffset;
        set => localOffset = value;
    }

    public Vector3 GetWorldPosition()
    {
        Transform t = anchor != null ? anchor : transform;
        return t.position + t.TransformVector(localOffset);
    }
}
