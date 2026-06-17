using UnityEngine;

/// <summary>
/// Deprecated: assign <see cref="WorldVisuals"/> on <see cref="WorldFloorToUIEdge.additionalYRootsToMoveWithLane"/>
/// instead. This component is kept so existing scenes do not break, but it no longer moves transforms.
/// </summary>
[DefaultExecutionOrder(125)]
public sealed class FloorVisualZoomNudger : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isActiveAndEnabled)
            Debug.LogWarning(
                "[FloorVisualZoomNudger] Disabled — use WorldFloorToUIEdge ▸ Synced Visual Roots (WorldVisuals) instead.",
                this);
    }
#endif
}
