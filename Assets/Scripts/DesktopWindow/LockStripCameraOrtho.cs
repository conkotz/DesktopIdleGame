using UnityEngine;

[DefaultExecutionOrder(-200)]
public class LockStripCameraOrtho : MonoBehaviour
{
    [SerializeField] private Camera stripCamera;

    [Tooltip("Set this to the exact Orthographic Size that looks correct in your 1920x360 editor preset.")]
    [SerializeField] private float fixedOrthoSize = 6.0f;

    private void OnEnable()
    {
        if (!stripCamera) stripCamera = GetComponent<Camera>();
        Apply();
    }

    private void LateUpdate()
    {
        // Re-apply in case any other component (PixelPerfect, scripts, UniWin) modifies it.
        Apply();
    }

    private void Apply()
    {
        if (!stripCamera) return;
        if (!stripCamera.orthographic) return;

        if (!Mathf.Approximately(stripCamera.orthographicSize, fixedOrthoSize))
            stripCamera.orthographicSize = fixedOrthoSize;
    }
}