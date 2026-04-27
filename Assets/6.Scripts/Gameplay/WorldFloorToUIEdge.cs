using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class WorldFloorToUIEdge : MonoBehaviour
{
    private enum RectEdge
    {
        Top,
        Bottom
    }

    [Header("UI Source")]
    [SerializeField] private RectTransform sourceRect;
    [SerializeField] private RectEdge sourceEdge = RectEdge.Top;

    [Tooltip("Positive values place the world floor above the selected UI edge in screen pixels.")]
    [SerializeField] private float sourcePixelOffset;

    [Header("World Target")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform worldRoot;
    [SerializeField] private BoxCollider2D floorCollider;
    [SerializeField] private float worldYOffset;

    [Header("Runtime Followers")]
    [SerializeField] private bool moveRuntimeActorsWithFloor = true;
    [SerializeField] private bool moveItemDropsWithFloor = true;

    [Header("Behaviour")]
    [SerializeField] private bool forceCanvasUpdate = true;
    [SerializeField] private bool updateContinuously = true;
    [SerializeField] private float minMoveDelta = 0.0001f;

    private readonly Vector3[] _corners = new Vector3[4];

    private void OnEnable()
    {
        CacheReferences();
        Apply(force: true);
    }

    private void OnValidate()
    {
        CacheReferences();
        Apply(force: true);
    }

    private void LateUpdate()
    {
        if (updateContinuously)
            Apply(force: false);
    }

    private void CacheReferences()
    {
        if (!worldRoot)
            worldRoot = transform;

        if (!worldCamera)
            worldCamera = Camera.main;

        if (!floorCollider)
            floorCollider = GetComponentInChildren<BoxCollider2D>(true);
    }

    private void Apply(bool force)
    {
        CacheReferences();

        if (!sourceRect || !worldCamera || !worldRoot || !floorCollider)
            return;

        if (forceCanvasUpdate)
            Canvas.ForceUpdateCanvases();

        float targetFloorTopY = GetSourceEdgeWorldY() + worldYOffset;
        float deltaY = targetFloorTopY - floorCollider.bounds.max.y;
        if (!force && Mathf.Abs(deltaY) < minMoveDelta)
            return;

        MoveTransformY(worldRoot, deltaY);

        if (Application.isPlaying && moveRuntimeActorsWithFloor)
            MoveRuntimeFollowers(deltaY);
    }

    private float GetSourceEdgeWorldY()
    {
        sourceRect.GetWorldCorners(_corners);

        float screenX = Mathf.Clamp(
            worldCamera.pixelRect.center.x,
            worldCamera.pixelRect.xMin,
            worldCamera.pixelRect.xMax);

        float screenY = sourceEdge == RectEdge.Top
            ? float.MinValue
            : float.MaxValue;

        for (int i = 0; i < _corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, _corners[i]);
            screenY = sourceEdge == RectEdge.Top
                ? Mathf.Max(screenY, screenPoint.y)
                : Mathf.Min(screenY, screenPoint.y);
        }

        screenY += sourcePixelOffset;

        float planeDistance = Mathf.Abs(worldCamera.transform.position.z - floorCollider.transform.position.z);
        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenX, screenY, planeDistance));
        return worldPoint.y;
    }

    private void MoveRuntimeFollowers(float deltaY)
    {
        MoveAllByDelta(FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);
        MoveAllByDelta(FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);

        if (moveItemDropsWithFloor)
            MoveAllByDelta(FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None), deltaY);
    }

    private void MoveAllByDelta<T>(T[] components, float deltaY) where T : Component
    {
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (!component)
                continue;

            Transform t = component.transform;
            if (t == worldRoot || t.IsChildOf(worldRoot))
                continue;

            MoveTransformY(t, deltaY);
        }
    }

    private static void MoveTransformY(Transform target, float deltaY)
    {
        if (!target)
            return;

        Vector3 position = target.position;
        position.y += deltaY;
        target.position = position;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.position = position;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
    }
}
