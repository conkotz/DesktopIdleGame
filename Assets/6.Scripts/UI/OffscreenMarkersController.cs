using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns stacked <see cref="OffscreenMarkerView"/> rows for objects tagged Enemy, NPC, and Resource
/// that are outside the gameplay camera. One row per category with aggregated counts.
/// </summary>
[DisallowMultipleComponent]
public class OffscreenMarkersController : MonoBehaviour
{
    private const string EnemyTag = "Enemy";
    private const string NpcTag = "NPC";
    private const string ResourceTag = "Resource";

    private enum OffscreenKind
    {
        Enemy,
        Npc,
        Resource
    }

    private struct Aggregate
    {
        public int Count;
        public float SumWorldX;
        public bool Any;
    }

    [Header("Prefab")]
    [SerializeField] private OffscreenMarkerView markerPrefab;
    [SerializeField] private RectTransform markerContainer;

    [Header("Camera")]
    [Tooltip("Viewport tests use this camera. Leave empty to use StripCameraController's Camera (if present), then Camera.main.")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Viewport margin (0–0.5). Slightly inside 0/1 avoids flicker at the screen edge.")]
    [SerializeField] private float viewportMargin = 0.02f;

    [Header("Layout")]
    [SerializeField] private float stackSpacingPixels = 6f;
    [Tooltip("Horizontal gap from screen edge to the marker's outer tip. 0 = flush (arrow touches the strip edge). LateUpdate repositions rows every frame, so change this here—not on marker children.")]
    [SerializeField] private float edgePaddingPixels = 0f;
    [Tooltip("Extra horizontal inset beyond the outer-tip distance. Use only if the sprite still clips; otherwise keep at 0 for flush arrows.")]
    [SerializeField] private float markerHorizontalBleedPadding = 0f;
    [Tooltip("Approximate distance from marker center to farthest pixel (arrow tip) in prefab space before scale. Lower pulls the tip closer to the screen edge.")]
    [SerializeField] private float centerToOuterTipReferencePixels = 20f;
    [Tooltip("If true, the first row is placed below the top of this container instead of vertically centered (good for pinned top-right markers).")]
    [SerializeField] private bool alignStackFromTop = true;
    [Tooltip("When Align Stack From Top is on: distance from the container's top edge down to the first row's center.")]
    [SerializeField] private float firstRowInsetFromTopPixels = 8f;
    [Tooltip("Extra vertical nudge applied to every row (+ = up on standard canvases). Layout runs every frame so this always applies.")]
    [SerializeField] private float stackVerticalOffsetPixels = 0f;
    [Tooltip("If true, this controller's RectTransform is stretched between the strip's left and right edges so row anchors align with the screen. Turn off if you already stretch it in the editor.")]
    [SerializeField] private bool stretchContainerHorizontally = true;

    [Header("Performance")]
    [Tooltip("How often to rescan the scene for markers (seconds).")]
    [SerializeField] private float refreshInterval = 0.08f;

    [Header("Palette (RGB only; alpha from OffscreenMarkerView)")]
    [SerializeField] private Color enemyColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color npcColor = new Color(0.35f, 0.6f, 1f, 1f);
    [SerializeField] private Color resourceColor = new Color(0.35f, 0.95f, 0.45f, 1f);

    private readonly List<OffscreenMarkerView> _pool = new();

    private float _nextRefreshTime;
    private int _activeMarkerRows;

    private void Awake()
    {
        if (!markerContainer) markerContainer = GetComponent<RectTransform>();
        ResolveWorldCamera();

        if (markerContainer && stretchContainerHorizontally)
        {
            markerContainer.anchorMin = new Vector2(0f, markerContainer.anchorMin.y);
            markerContainer.anchorMax = new Vector2(1f, markerContainer.anchorMax.y);
            float yMin = markerContainer.offsetMin.y;
            float yMax = markerContainer.offsetMax.y;
            markerContainer.offsetMin = new Vector2(0f, yMin);
            markerContainer.offsetMax = new Vector2(0f, yMax);
        }
    }

    private void ResolveWorldCamera()
    {
        if (worldCamera)
            return;

        StripCameraController strip = FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
        if (strip && strip.TryGetComponent<Camera>(out Camera stripCam))
            worldCamera = stripCam;

        if (!worldCamera)
            worldCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (!markerPrefab || !markerContainer)
            return;

        ResolveWorldCamera();
        if (!worldCamera)
            return;

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

            Aggregate enemies = default;
            Aggregate npcs = default;
            Aggregate resources = default;

            CollectEnemies(ref enemies);
            CollectNpcs(ref npcs);
            CollectResources(ref resources);

            int need = 0;
            if (enemies.Any) need++;
            if (npcs.Any) need++;
            if (resources.Any) need++;

            EnsurePoolSize(need);
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(i < need);

            int idx = 0;
            if (enemies.Any)
                ApplyRow(_pool[idx++], OffscreenKind.Enemy, enemies);
            if (npcs.Any)
                ApplyRow(_pool[idx++], OffscreenKind.Npc, npcs);
            if (resources.Any)
                ApplyRow(_pool[idx++], OffscreenKind.Resource, resources);

            _activeMarkerRows = need;
        }

        if (_activeMarkerRows > 0)
            LayoutStack(_activeMarkerRows);
    }

    private void EnsurePoolSize(int need)
    {
        while (_pool.Count < need)
        {
            OffscreenMarkerView row = Instantiate(markerPrefab, markerContainer);
            row.gameObject.SetActive(false);
            _pool.Add(row);
        }
    }

    private void LayoutStack(int activeCount)
    {
        float total = 0f;
        var heights = new float[activeCount];
        for (int i = 0; i < activeCount; i++)
        {
            RectTransform rt = _pool[i].transform as RectTransform;
            float h = rt ? rt.rect.height * Mathf.Abs(rt.lossyScale.y) : 0f;
            heights[i] = h;
            total += h;
            if (i < activeCount - 1) total += stackSpacingPixels;
        }

        float halfParentH = markerContainer ? markerContainer.rect.height * 0.5f : 0f;
        if (halfParentH <= 0.01f) halfParentH = 50f;

        for (int i = 0; i < activeCount; i++)
        {
            OffscreenMarkerView view = _pool[i];
            RectTransform rt = view.transform as RectTransform;
            if (!rt) continue;

            bool dockLeft = view.DockedLeft;
            rt.anchorMin = dockLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.anchorMax = dockLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float h = heights[i];
            float halfToTip = HorizontalCenterToOuterTip(rt) + markerHorizontalBleedPadding;
            float x = dockLeft ? edgePaddingPixels + halfToTip : -(edgePaddingPixels + halfToTip);

            float yCenter;
            if (alignStackFromTop)
            {
                yCenter = halfParentH - firstRowInsetFromTopPixels;
                for (int j = 0; j < i; j++)
                    yCenter -= heights[j] * 0.5f + stackSpacingPixels + heights[j + 1] * 0.5f;
            }
            else
            {
                yCenter = total * 0.5f;
                for (int j = 0; j < i; j++)
                {
                    yCenter -= heights[j];
                    yCenter -= stackSpacingPixels;
                }
                yCenter -= heights[i] * 0.5f;
            }

            rt.anchoredPosition = new Vector2(x, yCenter + stackVerticalOffsetPixels);
        }
    }

    /// <summary>Distance from marker center to outermost tip (arrow), after scale, so tip can sit on the strip edge when edge padding is 0.</summary>
    private float HorizontalCenterToOuterTip(RectTransform rt)
    {
        if (!rt) return centerToOuterTipReferencePixels;

        float s = Mathf.Max(Mathf.Abs(rt.lossyScale.x), Mathf.Abs(rt.lossyScale.y));
        float halfBox = Mathf.Max(Mathf.Abs(rt.rect.width), Mathf.Abs(rt.rect.height)) * 0.5f * s;
        float fromRef = Mathf.Max(0.01f, centerToOuterTipReferencePixels) * s;
        return Mathf.Max(halfBox, fromRef);
    }

    private void ApplyRow(OffscreenMarkerView row, OffscreenKind kind, Aggregate agg)
    {
        float camX = worldCamera.transform.position.x;
        float avgX = agg.SumWorldX / Mathf.Max(1, agg.Count);
        bool flipX = avgX < camX;

        switch (kind)
        {
            case OffscreenKind.Enemy:
                row.Apply(enemyColor, $"Enemy {agg.Count}x", flipX);
                break;
            case OffscreenKind.Npc:
                row.Apply(npcColor, $"NPC {agg.Count}x", flipX);
                break;
            default:
                row.Apply(resourceColor, $"Resources {agg.Count}x", flipX);
                break;
        }
    }

    private bool IsOffCamera(Vector3 worldPos)
    {
        Vector3 vp = worldCamera.WorldToViewportPoint(worldPos);
        if (vp.z < 0f)
            return true;

        float m = viewportMargin;
        return vp.x < m || vp.x > 1f - m || vp.y < m || vp.y > 1f - m;
    }

    private static void Add(ref Aggregate a, float worldX)
    {
        a.Count++;
        a.SumWorldX += worldX;
        a.Any = true;
    }

    private void CollectEnemies(ref Aggregate agg)
    {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(EnemyTag);

        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;

            EnemyBaseController ebc = go.GetComponentInParent<EnemyBaseController>();
            if (ebc && ebc.IsDead) continue;

            Vector3 p = go.transform.position;
            if (!IsOffCamera(p)) continue;
            Add(ref agg, p.x);
        }
    }

    private void CollectResources(ref Aggregate agg)
    {
        HashSet<int> seen = new HashSet<int>();

        GameObject[] tagged = GameObject.FindGameObjectsWithTag(ResourceTag);
        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;

            ResourceNode node = go.GetComponentInParent<ResourceNode>();
            Vector3 p = node && node.workSpot ? node.workSpot.position : go.transform.position;
            int id = node ? node.gameObject.GetInstanceID() : go.GetInstanceID();
            TryAddResourceCandidate(ref agg, seen, p, id);
        }

        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            ResourceNode node = nodes[i];
            if (!node.gameObject.activeInHierarchy) continue;

            Vector3 p = node.workSpot ? node.workSpot.position : node.transform.position;
            TryAddResourceCandidate(ref agg, seen, p, node.gameObject.GetInstanceID());
        }
    }

    private void TryAddResourceCandidate(ref Aggregate agg, HashSet<int> seen, Vector3 worldPos, int dedupeId)
    {
        if (!seen.Add(dedupeId)) return;
        if (!IsOffCamera(worldPos)) return;
        Add(ref agg, worldPos.x);
    }

    private void CollectNpcs(ref Aggregate agg)
    {
        // Per-GameObject ids — do NOT use transform.root or every NPC under the same folder counts once.
        HashSet<int> seenNpcIds = new HashSet<int>();

        GameObject[] tagged = GameObject.FindGameObjectsWithTag(NpcTag);
        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;
            TryAddNpcCandidate(ref agg, seenNpcIds, go.transform.position, go.GetInstanceID());
        }

        NPCInteractionSettings[] dialogue =
            FindObjectsByType<NPCInteractionSettings>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < dialogue.Length; i++)
        {
            NPCInteractionSettings npc = dialogue[i];
            if (!npc || !npc.gameObject.activeInHierarchy) continue;
            TryAddNpcCandidate(ref agg, seenNpcIds, npc.transform.position, npc.gameObject.GetInstanceID());
        }

        Merchant[] merchants = FindObjectsByType<Merchant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < merchants.Length; i++)
        {
            Merchant m = merchants[i];
            if (!m || !m.gameObject.activeInHierarchy) continue;
            TryAddNpcCandidate(ref agg, seenNpcIds, m.transform.position, m.gameObject.GetInstanceID());
        }
    }

    private void TryAddNpcCandidate(ref Aggregate agg, HashSet<int> seenNpcIds, Vector3 worldPos, int dedupeId)
    {
        if (!seenNpcIds.Add(dedupeId)) return;
        if (!IsOffCamera(worldPos)) return;
        Add(ref agg, worldPos.x);
    }
}
