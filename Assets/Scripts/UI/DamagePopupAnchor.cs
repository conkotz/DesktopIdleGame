using UnityEngine;

[DisallowMultipleComponent]
public class DamagePopupAnchor : MonoBehaviour
{
    [Tooltip("If assigned, popups spawn here. If null, uses this transform.")]
    public Transform anchor;

    public Vector3 WorldPos => (anchor ? anchor.position : transform.position);
}