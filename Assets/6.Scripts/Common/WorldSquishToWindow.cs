using UnityEngine;

[DefaultExecutionOrder(100)]
public class WorldSquishToWindow : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera stripCamera;
    [SerializeField] private Transform worldRoot;
    [SerializeField] private Transform leftAnchor; // child of worldRoot at the left edge you want pinned

    [Header("Behaviour")]
    [Tooltip("If off, uses the camera aspect at Start as the reference.")]
    [SerializeField] private bool useCustomReferenceAspect = false;

    [Tooltip("Reference aspect to treat as '1.0' scale. (Eg 1920/360 = 5.3333)")]
    [SerializeField] private float referenceAspect = 5.333333f;

    [SerializeField] private float minScaleX = 0.4f;
    [SerializeField] private float maxScaleX = 1.0f;

    private float _refAspect;
    private Vector3 _baseRootScale;
    private Vector3 _anchorWorldAtStart;

    private void Awake()
    {
        if (!stripCamera) stripCamera = Camera.main;
    }

    private void Start()
    {
        if (!stripCamera || !worldRoot || !leftAnchor) return;

        _baseRootScale = worldRoot.localScale;
        _anchorWorldAtStart = leftAnchor.position;

        _refAspect = useCustomReferenceAspect ? referenceAspect : stripCamera.aspect;
    }

    private void LateUpdate()
    {
        if (!stripCamera || !worldRoot || !leftAnchor) return;

        // Scale factor based on current aspect vs reference aspect
        float scaleFactor = stripCamera.aspect / _refAspect;
        scaleFactor = Mathf.Clamp(scaleFactor, minScaleX, maxScaleX);

        // Apply horizontal squish only
        worldRoot.localScale = new Vector3(_baseRootScale.x * scaleFactor, _baseRootScale.y, _baseRootScale.z);

        // Re-pin the left anchor so it doesn't drift when scaling
        Vector3 anchorNow = leftAnchor.position;
        Vector3 delta = _anchorWorldAtStart - anchorNow;
        worldRoot.position += delta;
    }
}