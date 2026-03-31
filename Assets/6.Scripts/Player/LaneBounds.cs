using UnityEngine;

public class LaneBounds : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float paddingWorld = 0.3f;

    public float MinX { get; private set; }
    public float MaxX { get; private set; }

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        Recalculate();
    }

    private void LateUpdate()
    {
        // Aspect changes when window resizes, so update continuously (cheap).
        Recalculate();
    }

    private void Recalculate()
    {
        if (!cam || !cam.orthographic) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        MinX = cam.transform.position.x - camW * 0.5f + paddingWorld;
        MaxX = cam.transform.position.x + camW * 0.5f - paddingWorld;
    }
}
