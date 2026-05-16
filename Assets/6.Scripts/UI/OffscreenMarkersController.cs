using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns stacked <see cref="OffscreenMarkerView"/> rows for objects tagged Enemy, NPC, Resource, Storage, NoticeBoard, and Cave
/// that are outside the gameplay camera. One row per category with aggregated counts.
/// </summary>
[DisallowMultipleComponent]
public class OffscreenMarkersController : MonoBehaviour
{
    /// <summary>Gameplay singleton — controller may live on <c>WorldManager</c> instead of the player.</summary>
    public static OffscreenMarkersController Instance { get; private set; }

    private const string EnemyTag = "Enemy";
    private const string NpcTag = "NPC";
    private const string ResourceTag = "Resource";
    private const string StorageTag = "Storage";
    private const string NoticeBoardTag = "NoticeBoard";
    private const string CaveTag = "Cave";

    private enum OffscreenKind
    {
        Enemy,
        Npc,
        Resource,
        Storage,
        NoticeBoard,
        Cave
    }

    private struct Aggregate
    {
        public int LeftCount;
        public float LeftSumWorldX;
        public int RightCount;
        public float RightSumWorldX;
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
    [Tooltip(
        "When the HUD canvas is wider than the strip camera's on-screen rect (reference resolution / letterboxing), " +
        "left-docked markers can sit in dead space left of the gameplay view. Maps the strip camera pixelRect left edge " +
        "into this container and shifts left markers right so the arrow tip stays inside the viewable strip. Right-docked layout is unchanged.")]
    [SerializeField] private bool shiftLeftMarkersIntoStripView = true;
    [Tooltip("Added to pixelRect.xMin before mapping into UI (screen pixels). Use a small positive value if a reference/safe frame sits inside the camera rect.")]
    [SerializeField] [Min(0f)] private float leftClampScreenInsetPixels = 0f;
    [Header("Performance")]
    [Tooltip("How often to rescan the scene for markers (seconds).")]
    [SerializeField] private float refreshInterval = 0.08f;

    [Header("Palette (RGB only; alpha from OffscreenMarkerView)")]
    [SerializeField] private Color enemyColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color npcColor = new Color(0.35f, 0.6f, 1f, 1f);
    [SerializeField] private Color resourceColor = new Color(0.35f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color storageColor = new Color(0.95f, 0.72f, 0.2f, 1f);
    [SerializeField] private Color noticeBoardColor = new Color(0.85f, 0.5f, 1f, 1f);
    [SerializeField] private Color caveColor = new Color(0.78f, 0.65f, 0.46f, 1f);

    [Header("Marker label font sizes")]
    [Tooltip("When enabled, marker rows use the per-type font sizes below.")]
    [SerializeField] private bool overrideMarkerLabelFontSizes;
    [SerializeField] [Min(1f)] private float enemyLabelFontSize = 16f;
    [SerializeField] [Min(1f)] private float npcLabelFontSize = 16f;
    [SerializeField] [Min(1f)] private float resourceLabelFontSize = 16f;
    [SerializeField] [Min(1f)] private float storageLabelFontSize = 16f;
    [SerializeField] [Min(1f)] private float noticeBoardLabelFontSize = 16f;
    [SerializeField] [Min(1f)] private float caveLabelFontSize = 16f;

    [Header("Marker label names")]
    [SerializeField] private string enemyLabelName = "Enemy";
    [SerializeField] private string npcLabelName = "NPC";
    [SerializeField] private string resourceLabelName = "Resources";
    [SerializeField] private string storageLabelName = "Storage";
    [SerializeField] private string noticeBoardLabelName = "Notice board";
    [SerializeField] private string caveLabelName = "Cave";

    private readonly List<OffscreenMarkerView> _pool = new();

    private float _nextRefreshTime;
    private int _activeMarkerRows;
    private bool _markerLayoutDirty;

    private static readonly List<int> s_layoutLeftOrder = new();
    private static readonly List<int> s_layoutRightOrder = new();
    private static float[] s_layoutHeights = System.Array.Empty<float>();
    private static float[] s_layoutYByIndex = System.Array.Empty<float>();
    private static readonly HashSet<int> s_collectSeenIds = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[OffscreenMarkersController] Only one instance is supported — destroying duplicate component.",
                this);
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveMarkerContainer();
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// When the controller is on a non-UI object (e.g. WorldManager), assign <see cref="markerContainer"/> in the Inspector
    /// or add a child/descendant RectTransform named OffscreenMarkerContainer or MarkerContainer.
    /// </summary>
    private void ResolveMarkerContainer()
    {
        if (markerContainer)
            return;

        if (TryGetComponent(out RectTransform selfRt))
        {
            markerContainer = selfRt;
            return;
        }

        // Immediate children (common setup)
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (!c)
                continue;
            if (TryMatchMarkerContainerName(c.name) && c.TryGetComponent(out RectTransform rtChild))
            {
                markerContainer = rtChild;
                return;
            }
        }

        // Any descendant (controller under scene root; UI nested deeper)
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rt = rects[i];
            if (!rt || rt.gameObject == gameObject)
                continue;
            if (TryMatchMarkerContainerName(rt.name))
            {
                markerContainer = rt;
                return;
            }
        }

        Debug.LogWarning(
            "[OffscreenMarkersController] Marker Container is not set and no child named OffscreenMarkerContainer / MarkerContainer was found. " +
            "Assign a RectTransform (e.g. full-width strip under StripUICanvas).",
            this);
    }

    private static bool TryMatchMarkerContainerName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;
        string n = objectName.Trim();
        return string.Equals(n, "OffscreenMarkerContainer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(n, "MarkerContainer", StringComparison.OrdinalIgnoreCase);
    }

    private void OnEnable()
    {
        SliderSettingsStore.Changed += OnSliderSettingsChanged;
        ToggleSettingsStore.Changed += OnToggleSettingsChanged;
        ApplyMarkersIndependentOfHudResize();
        ApplyVisibilityFromSettings();
    }

    private void OnDisable()
    {
        SliderSettingsStore.Changed -= OnSliderSettingsChanged;
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;
    }

    public static void RefreshAllFromSettings()
    {
        if (Instance != null)
        {
            Instance.ApplyVisibilityFromSettings();
            return;
        }

        OffscreenMarkersController[] controllers =
            FindObjectsByType<OffscreenMarkersController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i])
                controllers[i].ApplyVisibilityFromSettings();
        }
    }

    private void OnToggleSettingsChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.ShowOffscreenMarkers)
            ApplyVisibilityFromSettings();
    }

    private void ApplyVisibilityFromSettings()
    {
        if (!ToggleSettingsStore.Get(ToggleSettingId.ShowOffscreenMarkers))
            HideAllMarkers();
    }

    private void HideAllMarkers()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i])
                _pool[i].gameObject.SetActive(false);
        }

        _activeMarkerRows = 0;
    }

    private void OnSliderSettingsChanged(SliderSettingId id, float _)
    {
        if (id == SliderSettingId.HudResize)
            ApplyMarkersIndependentOfHudResize();
    }

    /// <summary>
    /// <see cref="RuntimeCanvasScaleController"/> scales the whole HUD canvas via <see cref="SliderSettingId.HudResize"/>.
    /// Counter-scale the marker stack so arrows stay authoring size while strip HUD scales.
    /// </summary>
    public void ApplyMarkersIndependentOfHudResize()
    {
        if (!markerContainer)
            return;

        float hud = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.HudResize));
        markerContainer.localScale = Vector3.one / hud;
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
        if (!ToggleSettingsStore.Get(ToggleSettingId.ShowOffscreenMarkers))
        {
            if (_activeMarkerRows > 0)
                HideAllMarkers();
            return;
        }

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
            Aggregate storages = default;
            Aggregate noticeBoards = default;
            Aggregate caves = default;

            CollectEnemies(ref enemies);
            CollectNpcs(ref npcs);
            CollectResources(ref resources);
            CollectTaggedWorldObjects(StorageTag, ref storages);
            CollectTaggedWorldObjects(NoticeBoardTag, ref noticeBoards);
            CollectTaggedWorldObjects(CaveTag, ref caves);

            int need = 0;
            need += CountRowsForAggregate(enemies);
            need += CountRowsForAggregate(npcs);
            need += CountRowsForAggregate(resources);
            need += CountRowsForAggregate(storages);
            need += CountRowsForAggregate(noticeBoards);
            need += CountRowsForAggregate(caves);

            EnsurePoolSize(need);
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(i < need);

            int idx = 0;
            EmitRowsForAggregate(ref idx, OffscreenKind.Enemy, enemies);
            EmitRowsForAggregate(ref idx, OffscreenKind.Npc, npcs);
            EmitRowsForAggregate(ref idx, OffscreenKind.Resource, resources);
            EmitRowsForAggregate(ref idx, OffscreenKind.Storage, storages);
            EmitRowsForAggregate(ref idx, OffscreenKind.NoticeBoard, noticeBoards);
            EmitRowsForAggregate(ref idx, OffscreenKind.Cave, caves);

            _activeMarkerRows = need;
            _markerLayoutDirty = true;
        }

        if (_markerLayoutDirty && _activeMarkerRows > 0)
        {
            LayoutStack(_activeMarkerRows);
            _markerLayoutDirty = false;
        }
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
        float halfParentH = markerContainer ? markerContainer.rect.height * 0.5f : 0f;
        if (halfParentH <= 0.01f) halfParentH = 50f;

        EnsureLayoutScratchSize(activeCount);

        s_layoutLeftOrder.Clear();
        s_layoutRightOrder.Clear();
        for (int i = 0; i < activeCount; i++)
        {
            RectTransform rt = _pool[i].transform as RectTransform;
            s_layoutHeights[i] = rt ? rt.rect.height * Mathf.Abs(rt.lossyScale.y) : 0f;
            if (_pool[i].DockedLeft)
                s_layoutLeftOrder.Add(i);
            else
                s_layoutRightOrder.Add(i);
        }

        System.Array.Clear(s_layoutYByIndex, 0, activeCount);
        ComputeSideStackY(s_layoutLeftOrder, s_layoutHeights, halfParentH, s_layoutYByIndex);
        ComputeSideStackY(s_layoutRightOrder, s_layoutHeights, halfParentH, s_layoutYByIndex);

        float stripLeftLocalX = 0f;
        bool haveStripLeft =
            shiftLeftMarkersIntoStripView &&
            markerContainer &&
            TryGetStripLeftEdgeLocalInMarkerContainer(out stripLeftLocalX);
        float containerLeftLocalX = markerContainer ? markerContainer.rect.xMin : 0f;

        for (int i = 0; i < activeCount; i++)
        {
            OffscreenMarkerView view = _pool[i];
            RectTransform rt = view.transform as RectTransform;
            if (!rt) continue;

            bool dockLeft = view.DockedLeft;
            rt.anchorMin = dockLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.anchorMax = dockLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float h = s_layoutHeights[i];
            float halfToTip = HorizontalCenterToOuterTip(rt) + markerHorizontalBleedPadding;
            float x = dockLeft ? edgePaddingPixels + halfToTip : -(edgePaddingPixels + halfToTip);

            // Left anchors sit on the container's left edge at local x = rect.xMin (works for any pivot — e.g. scene pivot (1,0.5) => xMin = -width).
            // Child pivot x ≈ rect.xMin + anchoredPosition.x; outer left tip ≈ that minus halfToTip.
            if (dockLeft && haveStripLeft && markerContainer.rect.width > 1f)
            {
                float baseX = edgePaddingPixels + halfToTip;
                float minXFromLeftAnchor =
                    stripLeftLocalX - containerLeftLocalX + halfToTip + edgePaddingPixels;
                if (minXFromLeftAnchor > baseX)
                    x = minXFromLeftAnchor;
            }

            rt.anchoredPosition = new Vector2(x, s_layoutYByIndex[i] + stackVerticalOffsetPixels);
        }
    }

    private static void EnsureLayoutScratchSize(int activeCount)
    {
        if (s_layoutHeights.Length < activeCount)
            s_layoutHeights = new float[Mathf.NextPowerOfTwo(activeCount)];

        if (s_layoutYByIndex.Length < activeCount)
            s_layoutYByIndex = new float[Mathf.NextPowerOfTwo(activeCount)];
    }

    /// <summary>
    /// Left edge of the strip camera's <see cref="Camera.pixelRect"/> in <see cref="markerContainer"/> local space
    /// (origin at container pivot — same as <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle"/>).
    /// </summary>
    private bool TryGetStripLeftEdgeLocalInMarkerContainer(out float stripLeftLocalX)
    {
        stripLeftLocalX = 0f;
        if (!markerContainer || !worldCamera)
            return false;

        Rect pr = worldCamera.pixelRect;
        if (pr.width < 2f || pr.height < 2f)
            return false;

        float sy = pr.yMin + pr.height * 0.5f;
        float sx = Mathf.Clamp(
            pr.xMin + Mathf.Max(0f, leftClampScreenInsetPixels),
            pr.xMin,
            Mathf.Max(pr.xMin, pr.xMax - 0.01f));
        Canvas root = markerContainer.GetComponentInParent<Canvas>();
        if (!root)
            return false;

        Camera camForUi = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                markerContainer, new Vector2(sx, sy), camForUi, out Vector2 local))
            return false;

        stripLeftLocalX = local.x;
        return true;
    }

    /// <summary>Vertical positions for one edge only, so left/right stacks align row 0 at the same height.</summary>
    private void ComputeSideStackY(List<int> order, float[] heights, float halfParentH, float[] yOut)
    {
        int n = order.Count;
        if (n == 0)
            return;

        if (alignStackFromTop)
        {
            float y = halfParentH - firstRowInsetFromTopPixels;
            for (int k = 0; k < n; k++)
            {
                int idx = order[k];
                float h = heights[idx];
                if (k > 0)
                {
                    int prev = order[k - 1];
                    y -= heights[prev] * 0.5f + stackSpacingPixels + h * 0.5f;
                }

                yOut[idx] = y;
            }
        }
        else
        {
            float total = 0f;
            for (int k = 0; k < n; k++)
            {
                total += heights[order[k]];
                if (k < n - 1)
                    total += stackSpacingPixels;
            }

            float yCenter = total * 0.5f;
            for (int k = 0; k < n; k++)
            {
                int idx = order[k];
                float h = heights[idx];
                yCenter -= h * 0.5f;
                yOut[idx] = yCenter;
                yCenter -= h * 0.5f;
                if (k < n - 1)
                    yCenter -= stackSpacingPixels;
            }
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

    private int CountRowsForAggregate(Aggregate agg)
    {
        int rows = 0;
        bool hasLeft = agg.LeftCount > 0;
        bool hasRight = agg.RightCount > 0;
        if (hasLeft) rows++;
        if (hasRight) rows++;
        return rows;
    }

    private void EmitRowsForAggregate(ref int idx, OffscreenKind kind, Aggregate agg)
    {
        bool hasLeft = agg.LeftCount > 0;
        bool hasRight = agg.RightCount > 0;
        if (!hasLeft && !hasRight)
            return;

        if (hasLeft)
            ApplyRow(_pool[idx++], kind, agg.LeftCount, dockLeft: true);
        if (hasRight)
            ApplyRow(_pool[idx++], kind, agg.RightCount, dockLeft: false);
    }

    private void ApplyRow(OffscreenMarkerView row, OffscreenKind kind, int count, bool dockLeft)
    {
        switch (kind)
        {
            case OffscreenKind.Enemy:
                row.Apply(enemyColor, $"{ResolveLabelName(OffscreenKind.Enemy)} {count}x", dockLeft, ResolveLabelFontSizeOverride(OffscreenKind.Enemy));
                break;
            case OffscreenKind.Npc:
                row.Apply(npcColor, $"{ResolveLabelName(OffscreenKind.Npc)} {count}x", dockLeft, ResolveLabelFontSizeOverride(OffscreenKind.Npc));
                break;
            case OffscreenKind.Resource:
                row.Apply(resourceColor, $"{ResolveLabelName(OffscreenKind.Resource)} {count}x", dockLeft, ResolveLabelFontSizeOverride(OffscreenKind.Resource));
                break;
            case OffscreenKind.Storage:
                row.Apply(storageColor, $"{ResolveLabelName(OffscreenKind.Storage)} {count}x", dockLeft, ResolveLabelFontSizeOverride(OffscreenKind.Storage));
                break;
            case OffscreenKind.NoticeBoard:
                row.Apply(noticeBoardColor, $"{ResolveLabelName(OffscreenKind.NoticeBoard)} {count}x", dockLeft, ResolveLabelFontSizeOverride(OffscreenKind.NoticeBoard));
                break;
            case OffscreenKind.Cave:
                row.Apply(caveColor, $"{ResolveLabelName(OffscreenKind.Cave)} {count}x", dockLeft, ResolveLabelFontSizeOverride(OffscreenKind.Cave));
                break;
        }
    }

    private float ResolveLabelFontSizeOverride(OffscreenKind kind)
    {
        if (!overrideMarkerLabelFontSizes)
            return -1f;

        return kind switch
        {
            OffscreenKind.Enemy => enemyLabelFontSize,
            OffscreenKind.Npc => npcLabelFontSize,
            OffscreenKind.Resource => resourceLabelFontSize,
            OffscreenKind.Storage => storageLabelFontSize,
            OffscreenKind.NoticeBoard => noticeBoardLabelFontSize,
            OffscreenKind.Cave => caveLabelFontSize,
            _ => -1f
        };
    }

    private string ResolveLabelName(OffscreenKind kind)
    {
        string raw = kind switch
        {
            OffscreenKind.Enemy => enemyLabelName,
            OffscreenKind.Npc => npcLabelName,
            OffscreenKind.Resource => resourceLabelName,
            OffscreenKind.Storage => storageLabelName,
            OffscreenKind.NoticeBoard => noticeBoardLabelName,
            OffscreenKind.Cave => caveLabelName,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(raw))
            return raw.Trim();

        return kind switch
        {
            OffscreenKind.Enemy => "Enemy",
            OffscreenKind.Npc => "NPC",
            OffscreenKind.Resource => "Resources",
            OffscreenKind.Storage => "Storage",
            OffscreenKind.NoticeBoard => "Notice board",
            OffscreenKind.Cave => "Cave",
            _ => "Marker"
        };
    }

    private bool IsOffCamera(Vector3 worldPos)
    {
        Vector3 vp = worldCamera.WorldToViewportPoint(worldPos);
        if (vp.z < 0f)
            return true;

        float m = viewportMargin;
        return vp.x < m || vp.x > 1f - m || vp.y < m || vp.y > 1f - m;
    }

    private void Add(ref Aggregate a, float worldX)
    {
        float camX = worldCamera ? worldCamera.transform.position.x : 0f;
        if (worldX < camX)
        {
            a.LeftCount++;
            a.LeftSumWorldX += worldX;
        }
        else
        {
            a.RightCount++;
            a.RightSumWorldX += worldX;
        }
        a.Any = true;
    }

    private void CollectTaggedWorldObjects(string unityTag, ref Aggregate agg)
    {
        s_collectSeenIds.Clear();
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(unityTag);
        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;

            Vector3 p = go.transform.position;
            int id = go.GetInstanceID();
            if (!s_collectSeenIds.Add(id)) continue;
            if (!IsOffCamera(p)) continue;
            Add(ref agg, p.x);
        }
    }

    private void CollectEnemies(ref Aggregate agg)
    {
        s_collectSeenIds.Clear();
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(EnemyTag);

        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;

            EnemyBaseController ebc = go.GetComponentInParent<EnemyBaseController>();
            if (ebc && ebc.IsDead) continue;

            int dedupeId = ebc ? ebc.gameObject.GetInstanceID() : go.GetInstanceID();
            if (!s_collectSeenIds.Add(dedupeId)) continue;

            Vector3 p = ebc ? ebc.transform.position : go.transform.position;
            if (!IsOffCamera(p)) continue;
            Add(ref agg, p.x);
        }
    }

    private void CollectResources(ref Aggregate agg)
    {
        s_collectSeenIds.Clear();

        GameObject[] tagged = GameObject.FindGameObjectsWithTag(ResourceTag);
        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;

            ResourceNode node = go.GetComponentInParent<ResourceNode>();
            Vector3 p = node && node.workSpot ? node.workSpot.position : go.transform.position;
            int id = node ? node.gameObject.GetInstanceID() : go.GetInstanceID();
            TryAddResourceCandidate(ref agg, p, id);
        }

        ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            ResourceNode node = nodes[i];
            if (!node.gameObject.activeInHierarchy) continue;

            Vector3 p = node.workSpot ? node.workSpot.position : node.transform.position;
            TryAddResourceCandidate(ref agg, p, node.gameObject.GetInstanceID());
        }
    }

    private void TryAddResourceCandidate(ref Aggregate agg, Vector3 worldPos, int dedupeId)
    {
        if (!s_collectSeenIds.Add(dedupeId)) return;
        if (!IsOffCamera(worldPos)) return;
        Add(ref agg, worldPos.x);
    }

    private void CollectNpcs(ref Aggregate agg)
    {
        // Per-GameObject ids — do NOT use transform.root or every NPC under the same folder counts once.
        s_collectSeenIds.Clear();

        GameObject[] tagged = GameObject.FindGameObjectsWithTag(NpcTag);
        for (int i = 0; i < tagged.Length; i++)
        {
            GameObject go = tagged[i];
            if (!go.activeInHierarchy) continue;
            TryAddNpcCandidate(ref agg, go.transform.position, go.GetInstanceID());
        }

        Merchant[] merchants = FindObjectsByType<Merchant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < merchants.Length; i++)
        {
            Merchant m = merchants[i];
            if (!m || !m.gameObject.activeInHierarchy) continue;
            TryAddNpcCandidate(ref agg, m.transform.position, m.gameObject.GetInstanceID());
        }
    }

    private void TryAddNpcCandidate(ref Aggregate agg, Vector3 worldPos, int dedupeId)
    {
        if (!s_collectSeenIds.Add(dedupeId)) return;
        if (!IsOffCamera(worldPos)) return;
        Add(ref agg, worldPos.x);
    }
}
