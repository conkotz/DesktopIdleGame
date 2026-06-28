using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Dims overlapping ground-item stack labels so only the frontmost readable label stays bright.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemDropStackLabelFocus : MonoBehaviour
{
    private const float DimAlphaMultiplier = 0.35f;

    private static ItemDropStackLabelFocus _instance;
    private static readonly List<ItemDrop> Drops = new(64);
    private static readonly List<ItemDrop> SortedScratch = new(64);
    private static readonly List<ItemDrop> FullBrightScratch = new(16);
    private static readonly Collider2D[] HitScratch = new Collider2D[32];

    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask pickupMask = ~0;

    public static void Register(ItemDrop drop)
    {
        if (!drop)
            return;

        EnsureInstance();
        if (!Drops.Contains(drop))
            Drops.Add(drop);
    }

    public static void Unregister(ItemDrop drop)
    {
        if (!drop)
            return;

        Drops.Remove(drop);
    }

    public static float DimAlphaMultiplierForLabels => DimAlphaMultiplier;

    private static ItemDropStackLabelFocus EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var host = new GameObject(nameof(ItemDropStackLabelFocus));
        _instance = host.AddComponent<ItemDropStackLabelFocus>();
        DontDestroyOnLoad(host);
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (!worldCamera)
            worldCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (Drops.Count == 0)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (!cam)
            return;

        for (int i = Drops.Count - 1; i >= 0; i--)
        {
            if (!Drops[i])
                Drops.RemoveAt(i);
        }

        if (Drops.Count == 0)
            return;

        SortedScratch.Clear();
        for (int i = 0; i < Drops.Count; i++)
        {
            ItemDrop drop = Drops[i];
            if (drop != null && drop.HasVisibleStackLabel)
                SortedScratch.Add(drop);
        }

        if (SortedScratch.Count == 0)
            return;

        SortedScratch.Sort(CompareDropFrontToBack);

        FullBrightScratch.Clear();
        ItemDrop mouseDrop = TryPickDropAtMouse(cam);
        if (mouseDrop != null && mouseDrop.HasVisibleStackLabel)
            FullBrightScratch.Add(mouseDrop);

        for (int i = 0; i < SortedScratch.Count; i++)
        {
            ItemDrop candidate = SortedScratch[i];
            if (FullBrightScratch.Contains(candidate))
                continue;

            if (!candidate.TryGetStackLabelScreenRect(cam, out Rect candidateRect))
                continue;

            bool overlapsBrighter = false;
            for (int j = 0; j < FullBrightScratch.Count; j++)
            {
                ItemDrop brighter = FullBrightScratch[j];
                if (!brighter.TryGetStackLabelScreenRect(cam, out Rect brighterRect))
                    continue;

                if (candidateRect.Overlaps(brighterRect, true))
                {
                    overlapsBrighter = true;
                    break;
                }
            }

            if (!overlapsBrighter)
                FullBrightScratch.Add(candidate);
        }

        for (int i = 0; i < SortedScratch.Count; i++)
        {
            ItemDrop drop = SortedScratch[i];
            drop.SetStackLabelDimState(!FullBrightScratch.Contains(drop));
        }
    }

    private ItemDrop TryPickDropAtMouse(Camera cam)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return null;

        Vector3 screen = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(screen);
        var point = new Vector2(world.x, world.y);

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = pickupMask,
            useTriggers = true
        };

        int count = Physics2D.OverlapPoint(point, filter, HitScratch);
        if (count <= 0)
            return null;

        ItemDrop best = null;
        int bestDropOrder = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = HitScratch[i];
            if (!col)
                continue;

            ItemDrop drop = col.GetComponentInParent<ItemDrop>();
            if (!drop)
                continue;

            if (drop.DropOrder > bestDropOrder)
            {
                bestDropOrder = drop.DropOrder;
                best = drop;
            }
        }

        return best;
    }

    private static int CompareDropFrontToBack(ItemDrop a, ItemDrop b)
    {
        if (!a && !b) return 0;
        if (!a) return 1;
        if (!b) return -1;
        return b.DropOrder.CompareTo(a.DropOrder);
    }
}
