using UnityEngine;

/// <summary>
/// Swaps the furnace world sprite between inactive, active smelting, and ready-to-collect states.
/// </summary>
[DisallowMultipleComponent]
public class FurnaceStageVisual : MonoBehaviour
{
    [SerializeField] private FurnaceSmelter smelter;
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
        if (!smelter)
            smelter = GetComponent<FurnaceSmelter>();
        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Subscribe()
    {
        if (smelter == null)
            return;

        smelter.StateChanged -= Refresh;
        smelter.StateChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (smelter != null)
            smelter.StateChanged -= Refresh;
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
        if (smelter != null)
        {
            if (smelter.IsSmelting)
                return activeSprite != null ? activeSprite : inactiveSprite;

            if (smelter.ReadyBarAmount > 0)
                return readySprite != null ? readySprite : inactiveSprite;
        }

        return inactiveSprite;
    }
}
