using UnityEngine;

/// <summary>
/// Swaps the cooking range world sprite between inactive, active cooking, and ready-to-collect states.
/// </summary>
[DisallowMultipleComponent]
public class CookingStageVisual : MonoBehaviour
{
    [SerializeField] private CookingStation station;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite readySprite;

    private void Awake()
    {
        CacheRefs();
        if (inactiveSprite == null && spriteRenderer != null)
            inactiveSprite = spriteRenderer.sprite;
    }

    private void OnEnable()
    {
        CacheRefs();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void CacheRefs()
    {
        if (!station)
            station = GetComponent<CookingStation>();
        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Subscribe()
    {
        if (station == null)
            return;

        station.StateChanged -= Refresh;
        station.StateChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (station != null)
            station.StateChanged -= Refresh;
    }

    private void Refresh()
    {
        if (spriteRenderer == null)
            return;

        Sprite target = ResolveStageSprite();
        if (target != null && spriteRenderer.sprite != target)
            spriteRenderer.sprite = target;
    }

    private Sprite ResolveStageSprite()
    {
        if (station != null)
        {
            if (station.IsCooking)
                return activeSprite != null ? activeSprite : inactiveSprite;

            if (station.ReadyCookedAmount > 0)
                return readySprite != null ? readySprite : inactiveSprite;
        }

        return inactiveSprite;
    }
}
