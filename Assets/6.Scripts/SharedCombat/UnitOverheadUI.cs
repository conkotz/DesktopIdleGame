using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UnitOverheadUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Optional. Shows combat profile label (e.g. Glass Cannon, Deadly); color is set from the profile. Assign in the Inspector.")]
    [SerializeField] private TMP_Text combatProfileText;
    [SerializeField] private Image hpFill;
    [Tooltip("Optional; same bar stack as player/enemy HP.")]
    [SerializeField] private Image guardFill;
    [Tooltip("Optional. Shows current guard / natural cap.")]
    [SerializeField] private TMP_Text guardValueText;
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private Transform debuffContainer;
    [SerializeField] private GameObject debuffIconPrefab;

    [Header("Colors")]
    [Tooltip("Applied only to enemy overhead HP bars. Player overhead bars keep their prefab color.")]
    [SerializeField] private Color enemyHpFillColor = new(1f, 0.42f, 0.2f, 1f);

    [Header("Debuff Sprites")]
    [SerializeField] private Sprite bleedIcon;
    [SerializeField] private Sprite poisonIcon;
    [SerializeField] private Sprite burnIcon;
    [SerializeField] private Sprite chillIcon;
    [SerializeField] private Sprite shockIcon;

    [Header("Auto Bind")]
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private EnemyBaseController enemy;
    [SerializeField] private AilmentController ailments;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Camera targetCamera;

    [Header("Overlap stack (enemy overhead only)")]
    [Tooltip("When multiple enemy overheads project to nearby X positions on the strip canvas, stack them vertically.")]
    [SerializeField] private bool enableOverlappingStack = true;
    [Tooltip(
        "Small fudge (canvas px) added when testing horizontal overlap. Keep low so stacks only form when UI actually overlaps.")]
    [FormerlySerializedAs("stackOverlapThresholdPx")]
    [SerializeField] private float stackHorizontalOverlapPaddingPx = 8f;
    [Tooltip("How many canvas pixels two overhead labels may overlap horizontally before they stack vertically.")]
    [SerializeField] private float stackAllowedOverlapBeforeStackPx = 20f;
    [SerializeField] private float stackVerticalSpacingPx = 56f;
    [Tooltip("When enemy overheads overlap the player's fixed HP baseline, lift them by this many canvas pixels.")]
    [SerializeField] private float stackPlayerBaselineLiftPx = 30f;
    [Tooltip(
        "Optional minimum half-width (canvas px) for overlap tests. 0 = use measured rect + TMP bounds only. " +
        "Increase slightly if very narrow layouts fail to stack when enemies stand on the same spot.")]
    [SerializeField] private float stackMinClusteringHalfWidthPx = 0f;

    private RectTransform canvasRect;
    private readonly List<GameObject> spawnedDebuffIcons = new();

    private Vector2 _stackBaseAnchored;
    private float _stackYOffset;
    private float _externalScale = 1f;

    /// <summary>Cached <see cref="SliderSettingId.OverheadHpBarResize"/>; multiplied into root scale alongside <see cref="_externalScale"/>.</summary>
    private float _overheadBarResizeSlider = 1f;
    private Vector3 _additionalWorldOffset = Vector3.zero;

    /// <summary>Cached <see cref="SliderSettingId.HudResize"/>; dividing undoes CanvasScaler HUD growth so overhead size follows overhead slider only.</summary>
    private float _hudResizeSlider = 1f;

    private static readonly List<UnitOverheadUI> s_instances = new();
    private static bool s_canvasCallbackSubscribed;
    private static int s_lastStackResolveFrame = -1;
    private static readonly Dictionary<int, int> s_lastAssignedStackLaneByUiId = new();

    private PlayerCombatController _playerCombatCache;
    private Image _clickBackingImage;

    private bool _hpBarOnlyLayout;
    private bool _vitalsVisible = true;
    private PlayerCombatState _playerCombatState;

    /// <summary>True when the follow target is in the strip camera band this frame; used for overlap stacking (same idea as when the whole object was deactivated off-screen).</summary>
    private bool _worldBandVisible;

    private void Awake()
    {
        if (!root) root = transform as RectTransform;

        // Old behaviour still works if this UI is placed under the enemy directly.
        if (!characterStats) characterStats = GetComponentInParent<CharacterStats>();
        if (!enemy) enemy = GetComponentInParent<EnemyBaseController>();
        if (!ailments) ailments = GetComponentInParent<AilmentController>();

        EnsureClickableBacking();
    }

    private void OnEnable()
    {
        if (!s_instances.Contains(this))
            s_instances.Add(this);
        EnsureCanvasStackCallback();
        SliderSettingsStore.Changed += HandleSliderSettingsChanged;
        RefreshSliderScaleCaches();
        Subscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        SliderSettingsStore.Changed -= HandleSliderSettingsChanged;
        s_instances.Remove(this);
        Unsubscribe();
    }

    private void LateUpdate()
    {
        ComputeBaseAnchoredAndVisibility();

        if (!ShouldUseOverlapStacking())
            ApplyDirectPosition();
        else
            ApplyStackedPosition();
    }

    public void Bind(
        CharacterStats stats,
        EnemyBaseController enemyController,
        AilmentController ailmentController,
        Transform target,
        Canvas canvas,
        Camera cam,
        bool hpBarOnly = false)
    {
        Unsubscribe();

        characterStats = stats;
        enemy = enemyController;
        ailments = ailmentController;
        followTarget = target;
        parentCanvas = canvas;
        targetCamera = cam;
        canvasRect = canvas ? canvas.transform as RectTransform : null;
        _hpBarOnlyLayout = hpBarOnly;
        ResolvePlayerCombatState();

        ApplyHpBarOnlyVisuals();
        Subscribe();
        RefreshAll();
        EnsureClickableBacking();
        ComputeBaseAnchoredAndVisibility();
        if (!ShouldUseOverlapStacking())
            ApplyDirectPosition();
        else
            ApplyStackedPosition();
    }

    public void SetAdditionalWorldOffset(Vector3 offset)
    {
        _additionalWorldOffset = offset;
    }

    private void ApplyHpBarOnlyVisuals()
    {
        bool showExtras = !_hpBarOnlyLayout;
        if (nameText)
            nameText.gameObject.SetActive(showExtras);
        if (combatProfileText)
            combatProfileText.gameObject.SetActive(showExtras);
        if (debuffContainer)
            debuffContainer.gameObject.SetActive(showExtras);
    }

    private static void EnsureCanvasStackCallback()
    {
        if (s_canvasCallbackSubscribed)
            return;
        s_canvasCallbackSubscribed = true;
        Canvas.willRenderCanvases += OnCanvasWillRenderResolveStack;
    }

    private static void OnCanvasWillRenderResolveStack()
    {
        int fc = Time.frameCount;
        if (fc == s_lastStackResolveFrame)
            return;
        s_lastStackResolveFrame = fc;
        ResolveEnemyOverheadStacking();
    }

    private static void ResolveEnemyOverheadStacking()
    {
        var byCanvas = new Dictionary<int, List<UnitOverheadUI>>();

        for (int i = 0; i < s_instances.Count; i++)
        {
            UnitOverheadUI ui = s_instances[i];
            if (ui == null || !ui.isActiveAndEnabled || !ui.gameObject.activeInHierarchy)
                continue;
            if (!ui._worldBandVisible)
                continue;
            if (!ui.ShouldUseOverlapStacking())
                continue;

            int canvasKey = ui.parentCanvas != null ? ui.parentCanvas.GetInstanceID() : 0;
            if (!byCanvas.TryGetValue(canvasKey, out List<UnitOverheadUI> list))
            {
                list = new List<UnitOverheadUI>();
                byCanvas[canvasKey] = list;
            }

            list.Add(ui);
        }

        foreach (KeyValuePair<int, List<UnitOverheadUI>> kv in byCanvas)
            ResolveStackingForCanvasGroup(kv.Value);
    }

    private static void ResolveStackingForCanvasGroup(List<UnitOverheadUI> candidates)
    {
        var activeIds = new HashSet<int>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            UnitOverheadUI ui = candidates[i];
            if (ui != null)
                activeIds.Add(ui.GetInstanceID());
        }

        if (s_lastAssignedStackLaneByUiId.Count > 0)
        {
            var stale = new List<int>();
            foreach (int id in s_lastAssignedStackLaneByUiId.Keys)
            {
                if (!activeIds.Contains(id))
                    stale.Add(id);
            }

            for (int i = 0; i < stale.Count; i++)
                s_lastAssignedStackLaneByUiId.Remove(stale[i]);
        }

        for (int i = 0; i < candidates.Count; i++)
            candidates[i]._stackYOffset = 0f;

        if (candidates.Count <= 1)
        {
            for (int i = 0; i < candidates.Count; i++)
                candidates[i].ApplyStackedPosition();
            return;
        }

        Canvas.ForceUpdateCanvases();

        float padding = Mathf.Max(0f, candidates[0].stackHorizontalOverlapPaddingPx);
        float allowedOverlap = Mathf.Max(0f, candidates[0].stackAllowedOverlapBeforeStackPx);
        float spacing = Mathf.Max(1f, candidates[0].stackVerticalSpacingPx);
        float baselineHysteresisPx = Mathf.Max(6f, allowedOverlap * 0.5f);

        float minHalfW = Mathf.Max(0f, candidates[0].stackMinClusteringHalfWidthPx);

        var spans = new List<(float minX, float maxX, UnitOverheadUI ui)>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            candidates[i].GetHorizontalSpanInCanvas(out float minX, out float maxX);
            WidenSpanForClusterMerge(candidates[i]._stackBaseAnchored.x, minHalfW, ref minX, ref maxX);
            spans.Add((minX, maxX, candidates[i]));
        }

        spans.Sort((a, b) => a.minX.CompareTo(b.minX));

        // Player compact HP bar(s) are fixed anchors: never move them.
        var fixedBaselineSpans = new List<(float minX, float maxX)>(2);
        for (int i = 0; i < spans.Count; i++)
        {
            if (IsFixedPlayerBaseline(spans[i].ui))
            {
                spans[i].ui._stackYOffset = 0f;
                fixedBaselineSpans.Add((spans[i].minX, spans[i].maxX));
            }
        }

        // Assign to the lowest available "lane" that does not horizontally overlap.
        // This avoids transitive chaining (A overlaps B, B overlaps C) from forcing C
        // onto higher rows when A and C could share the same baseline row.
        var laneLastMaxX = new List<float>(8);
        for (int i = 0; i < spans.Count; i++)
        {
            (float minX, float maxX, UnitOverheadUI ui) = spans[i];
            if (IsFixedPlayerBaseline(ui))
                continue;

            bool overlapsFixedBaseline = false;
            for (int f = 0; f < fixedBaselineSpans.Count; f++)
            {
                (float fixedMinX, float fixedMaxX) = fixedBaselineSpans[f];
                bool overlaps = minX <= fixedMaxX + padding - allowedOverlap &&
                                maxX >= fixedMinX - padding + allowedOverlap;
                if (overlaps)
                {
                    overlapsFixedBaseline = true;
                    break;
                }
            }

            int preferredLane = 0;
            int uiId = ui.GetInstanceID();
            if (s_lastAssignedStackLaneByUiId.TryGetValue(uiId, out int rememberedLane))
                preferredLane = Mathf.Max(0, rememberedLane);

            // Prevent rapid 0<->1 lane thrash when an enemy hovers on the player's baseline edge.
            // If this UI was already above baseline, keep it above until it's clearly separated.
            if (!overlapsFixedBaseline && preferredLane > 0 && fixedBaselineSpans.Count > 0)
            {
                for (int f = 0; f < fixedBaselineSpans.Count; f++)
                {
                    (float fixedMinX, float fixedMaxX) = fixedBaselineSpans[f];
                    bool nearBaseline =
                        minX <= fixedMaxX + padding - allowedOverlap + baselineHysteresisPx &&
                        maxX >= fixedMinX - padding + allowedOverlap - baselineHysteresisPx;
                    if (nearBaseline)
                    {
                        overlapsFixedBaseline = true;
                        break;
                    }
                }
            }

            bool IsLaneAvailable(int lane)
            {
                if (lane < 0 || lane >= laneLastMaxX.Count)
                    return true;
                bool laneOverlaps = minX <= laneLastMaxX[lane] + padding - allowedOverlap;
                return !laneOverlaps;
            }

            int laneIndex = -1;
            if (preferredLane < laneLastMaxX.Count && IsLaneAvailable(preferredLane))
            {
                laneIndex = preferredLane;
            }
            else
            {
                for (int lane = 0; lane < laneLastMaxX.Count; lane++)
                {
                    if (IsLaneAvailable(lane))
                    {
                        laneIndex = lane;
                        break;
                    }
                }
            }

            if (laneIndex < 0)
            {
                laneIndex = laneLastMaxX.Count;
                laneLastMaxX.Add(maxX);
            }
            else
            {
                laneLastMaxX[laneIndex] = maxX;
            }

            // Keep player baseline fixed at y=0; overlapping enemies get a smaller dedicated lift.
            // Store remembered lane without baseline lift so overlap-edge transitions do not thrash lane memory.
            int rememberedLaneToStore = laneIndex;
            float baselineLift = overlapsFixedBaseline ? Mathf.Max(0f, ui.stackPlayerBaselineLiftPx) : 0f;

            ui._stackYOffset = laneIndex * spacing + baselineLift;
            s_lastAssignedStackLaneByUiId[uiId] = rememberedLaneToStore;
        }

        for (int i = 0; i < candidates.Count; i++)
            candidates[i].ApplyStackedPosition();
    }

    private static void WidenSpanForClusterMerge(float anchorCanvasX, float minHalfWidth, ref float minX, ref float maxX)
    {
        float w = maxX - minX;
        float center = w > 0.001f ? (minX + maxX) * 0.5f : anchorCanvasX;
        float half = Mathf.Max(w * 0.5f, minHalfWidth);
        minX = center - half;
        maxX = center + half;
    }

    private static bool IsFixedPlayerBaseline(UnitOverheadUI ui)
    {
        return ui != null && ui._hpBarOnlyLayout && ui.enemy == null;
    }

    private bool ShouldUseOverlapStacking()
    {
        if (!enableOverlappingStack)
            return false;

        // Stack enemy bars as before, and include compact player/world HP-only bars.
        return enemy != null || _hpBarOnlyLayout;
    }

    /// <summary>
    /// Left/right in canvas-local space. Uses root rect <b>and</b> TMP mesh bounds — long names often draw wider than the root/bar rect.
    /// </summary>
    private void GetHorizontalSpanInCanvas(out float minX, out float maxX)
    {
        minX = maxX = _stackBaseAnchored.x;

        if (root == null)
            return;

        if (canvasRect == null && parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        minX = float.MaxValue;
        maxX = float.MinValue;

        root.GetWorldCorners(UnitOverheadUIWorkCorners);
        for (int c = 0; c < 4; c++)
        {
            Vector3 local = canvasRect.InverseTransformPoint(UnitOverheadUIWorkCorners[c]);
            if (local.x < minX) minX = local.x;
            if (local.x > maxX) maxX = local.x;
        }

        ExpandHorizontalSpanWithTmpMeshBounds(canvasRect, ref minX, ref maxX);
    }

    private float GetMeasuredCanvasHalfHeight()
    {
        if (root == null)
            return 24f;

        if (canvasRect == null && parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null)
            return 24f;

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        root.GetWorldCorners(UnitOverheadUIWorkCorners);
        for (int c = 0; c < 4; c++)
        {
            Vector3 local = canvasRect.InverseTransformPoint(UnitOverheadUIWorkCorners[c]);
            if (local.y < minY) minY = local.y;
            if (local.y > maxY) maxY = local.y;
        }

        ExpandVerticalSpanWithTmpMeshBounds(canvasRect, ref minY, ref maxY);

        if (minY > maxY)
            return 24f;

        return Mathf.Max(4f, (maxY - minY) * 0.5f);
    }

    /// <summary>
    /// Root rect can be bar-sized while TMP draws past it; <see cref="TMP_Text.textBounds"/> matches rendered glyphs.
    /// </summary>
    private void ExpandHorizontalSpanWithTmpMeshBounds(RectTransform canvasRt, ref float minX, ref float maxX)
    {
        TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text tmp = tmps[i];
            if (!tmp || !tmp.gameObject.activeInHierarchy)
                continue;

            tmp.ForceMeshUpdate();
            Bounds b = tmp.textBounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            if (e.x < 1e-6f && e.y < 1e-6f && e.z < 1e-6f)
                continue;

            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            for (int iz = -1; iz <= 1; iz += 2)
            {
                Vector3 localCorner = c + new Vector3(ix * e.x, iy * e.y, iz * e.z);
                Vector3 world = tmp.transform.TransformPoint(localCorner);
                Vector3 canvasLocal = canvasRt.InverseTransformPoint(world);
                if (canvasLocal.x < minX) minX = canvasLocal.x;
                if (canvasLocal.x > maxX) maxX = canvasLocal.x;
            }
        }
    }

    private void ExpandVerticalSpanWithTmpMeshBounds(RectTransform canvasRt, ref float minY, ref float maxY)
    {
        TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text tmp = tmps[i];
            if (!tmp || !tmp.gameObject.activeInHierarchy)
                continue;

            tmp.ForceMeshUpdate();
            Bounds b = tmp.textBounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            if (e.x < 1e-6f && e.y < 1e-6f && e.z < 1e-6f)
                continue;

            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            for (int iz = -1; iz <= 1; iz += 2)
            {
                Vector3 localCorner = c + new Vector3(ix * e.x, iy * e.y, iz * e.z);
                Vector3 world = tmp.transform.TransformPoint(localCorner);
                Vector3 canvasLocal = canvasRt.InverseTransformPoint(world);
                if (canvasLocal.y < minY) minY = canvasLocal.y;
                if (canvasLocal.y > maxY) maxY = canvasLocal.y;
            }
        }
    }

    private static readonly Vector3[] UnitOverheadUIWorkCorners = new Vector3[4];

    private void Subscribe()
    {
        if (characterStats != null)
        {
            characterStats.OnNameChanged += HandleNameChanged;
            characterStats.OnHPChanged += HandleCharacterHpChanged;
            characterStats.OnGuardChanged += HandleCharacterGuardChanged;
            characterStats.OnStatsChanged += HandleStatsChanged;
        }

        if (enemy != null)
        {
            enemy.OnNameChanged += HandleNameChanged;
            enemy.OnHealthChanged += HandleEnemyHpChanged;
        }

        if (ailments != null)
        {
            ailments.OnAilmentsChanged += RefreshDebuffIcons;
        }

        ToggleSettingsStore.Changed += HandleToggleSettingChanged;
    }

    private void Unsubscribe()
    {
        ToggleSettingsStore.Changed -= HandleToggleSettingChanged;

        if (characterStats != null)
        {
            characterStats.OnNameChanged -= HandleNameChanged;
            characterStats.OnHPChanged -= HandleCharacterHpChanged;
            characterStats.OnGuardChanged -= HandleCharacterGuardChanged;
            characterStats.OnStatsChanged -= HandleStatsChanged;
        }

        if (enemy != null)
        {
            enemy.OnNameChanged -= HandleNameChanged;
            enemy.OnHealthChanged -= HandleEnemyHpChanged;
        }

        if (ailments != null)
        {
            ailments.OnAilmentsChanged -= RefreshDebuffIcons;
        }
    }

    private void HandleToggleSettingChanged(ToggleSettingId setting, bool _)
    {
        if (setting == ToggleSettingId.ShowPlayerHealthBarOutOfCombat)
            ComputeBaseAnchoredAndVisibility();

        if (setting == ToggleSettingId.ShowOverheadHealthGuardNumbers)
            ApplyOverheadNumericLabelPreference();
    }

    private void ApplyOverheadNumericLabelPreference()
    {
        bool showNums = ToggleSettingsStore.Get(ToggleSettingId.ShowOverheadHealthGuardNumbers);

        if (hpValueText != null)
            hpValueText.gameObject.SetActive(showNums);

        if (guardValueText == null)
            return;

        if (!showNums)
        {
            guardValueText.gameObject.SetActive(false);
            return;
        }

        if (characterStats != null)
            HandleCharacterGuardChanged(characterStats.Guard, characterStats.NaturalGuardCap);
        else
            guardValueText.gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        ApplyHpFillColorByOwner();
        HandleNameChanged(string.Empty);

        if (characterStats != null)
        {
            HandleCharacterHpChanged(characterStats.HP, characterStats.MaxHP);
            HandleCharacterGuardChanged(characterStats.Guard, characterStats.NaturalGuardCap);
        }

        if (enemy != null)
            HandleEnemyHpChanged(enemy.HP, enemy.MaxHP);

        RefreshDebuffIcons();
        ApplyOverheadNumericLabelPreference();
    }

    /// <summary>
    /// Tiny anti-flicker tolerance (viewport units) so overhead UI doesn't pop on/off for floating-point jitter at the edge.
    /// Kept very small on purpose — anything larger leaks UI into the desktop area below/above the strip.
    /// </summary>
    private const float OverheadViewportEdgeAntiFlickerMargin = 0.01f;

    private static bool IsOverheadWorldPointVisible(Camera cam, Vector3 worldPos, Vector3 screenPos)
    {
        if (cam == null)
            return screenPos.z > 0f;

        // Strict-clip overhead UI to the strip camera's viewport so HP bars / nameplates can never draw outside the
        // strip (e.g. into the transparent desktop area when the window covers the whole monitor and the strip is in
        // the upper half of the screen). Previously this allowed a 20%-of-viewport overhang in every direction.
        if (cam.orthographic)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            if (vp.z < 0f)
                return false;

            float m = OverheadViewportEdgeAntiFlickerMargin;
            if (vp.x < -m || vp.x > 1f + m || vp.y < -m || vp.y > 1f + m)
                return false;
        }
        else if (screenPos.z <= 0f)
        {
            return false;
        }

        // Pixel-rect sanity check: the projected screen position must fall inside the strip camera's render area on
        // the screen. This catches setups where the strip camera has a custom viewport rect (most of this game) and
        // a tiny viewport-units margin would still land outside the strip in canvas pixels.
        Rect pr = cam.pixelRect;
        if (pr.width <= 0f || pr.height <= 0f)
            return true;

        float pixelMargin = Mathf.Max(pr.width, pr.height) * OverheadViewportEdgeAntiFlickerMargin;
        return screenPos.x >= pr.xMin - pixelMargin &&
               screenPos.x <= pr.xMax + pixelMargin &&
               screenPos.y >= pr.yMin - pixelMargin &&
               screenPos.y <= pr.yMax + pixelMargin;
    }

    private void ComputeBaseAnchoredAndVisibility()
    {
        if (root == null || followTarget == null || parentCanvas == null || targetCamera == null)
        {
            _worldBandVisible = false;
            return;
        }

        if (canvasRect == null)
            canvasRect = parentCanvas.transform as RectTransform;

        Vector3 worldPos = followTarget.position + worldOffset + _additionalWorldOffset;
        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);

        // WorldToScreenPoint z <= 0 often means "behind" the camera, but orthographic 2D setups can edge-case;
        // viewport test keeps nameplates on-screen when the point is in front of the camera frustum.
        bool visible = IsOverheadWorldPointVisible(targetCamera, worldPos, screenPos) &&
                       _vitalsVisible &&
                       ShouldShowPlayerOverheadByCombatSetting();
        _worldBandVisible = visible;

        // Toggle only the UI root, not this behaviour's GameObject — otherwise LateUpdate stops
        // and we never recover when the follow target later moves on-screen (player scene teleport).
        if (root != null && root.gameObject.activeSelf != visible)
            root.gameObject.SetActive(visible);

        if (!visible)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCamera,
            out _stackBaseAnchored
        );
    }

    private void ResolvePlayerCombatState()
    {
        _playerCombatState = null;
        if (!_hpBarOnlyLayout || enemy != null)
            return;

        if (characterStats != null)
            _playerCombatState = characterStats.GetComponentInParent<PlayerCombatState>();
        if (_playerCombatState == null && followTarget != null)
            _playerCombatState = followTarget.GetComponentInParent<PlayerCombatState>();
    }

    private bool ShouldShowPlayerOverheadByCombatSetting()
    {
        if (!_hpBarOnlyLayout || enemy != null)
            return true;

        if (ToggleSettingsStore.Get(ToggleSettingId.ShowPlayerHealthBarOutOfCombat))
            return true;

        if (_playerCombatState == null)
            ResolvePlayerCombatState();

        return _playerCombatState != null && _playerCombatState.InCombat;
    }

    private void ApplyDirectPosition()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        root.anchoredPosition = _stackBaseAnchored;
        ApplyCombinedRootScale();
    }

    private void ApplyStackedPosition()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        root.anchoredPosition = _stackBaseAnchored + new Vector2(0f, _stackYOffset);
        ApplyCombinedRootScale();
    }

    public void SetExternalScale(float scale)
    {
        _externalScale = Mathf.Max(0.01f, scale);
        ApplyCombinedRootScale();
    }

    private void HandleSliderSettingsChanged(SliderSettingId id, float _)
    {
        if (id == SliderSettingId.HudResize || id == SliderSettingId.OverheadHpBarResize)
            RefreshSliderScaleCaches();
    }

    private void RefreshSliderScaleCaches()
    {
        _overheadBarResizeSlider = SliderSettingsStore.Get(SliderSettingId.OverheadHpBarResize);
        _hudResizeSlider = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.HudResize));
        ApplyCombinedRootScale();
    }

    private void ApplyCombinedRootScale()
    {
        if (root == null)
            return;

        float s = Mathf.Max(0.01f, _externalScale) * _overheadBarResizeSlider / _hudResizeSlider;
        root.localScale = Vector3.one * s;
    }

    private void HandleStatsChanged()
    {
        HandleNameChanged(string.Empty);
        if (characterStats != null)
            HandleCharacterGuardChanged(characterStats.Guard, characterStats.NaturalGuardCap);
    }

    private void HandleNameChanged(string _)
    {
        RefreshNameCombatPowerAndProfile();
    }

    private void RefreshNameCombatPowerAndProfile()
    {
        string baseName = "Unit";

        if (enemy != null)
            baseName = enemy.GetRichTextDisplayNameForOverhead();
        else if (characterStats != null)
            baseName = characterStats.UnitDisplayName;

        if (nameText != null)
        {
            nameText.richText = true;
            if (characterStats == null)
                nameText.text = baseName;
            else
            {
                int cp = Mathf.RoundToInt(characterStats.GetCombatPowerBreakdown().TotalCombatPower);
                nameText.text = $"{baseName} <size=75%><color=#AAAAAA>CP {cp}</color></size>";
            }
        }

        if (combatProfileText != null)
        {
            if (characterStats == null)
            {
                combatProfileText.text = string.Empty;
                combatProfileText.color = Color.white;
            }
            else
            {
                CombatPowerBreakdown breakdown = characterStats.GetCombatPowerBreakdown();
                CombatProfileDefenseHints defenseHints = characterStats.GetCombatProfileDefenseHints();
                string profileLabel = CombatProfileClassifier.Classify(breakdown, defenseHints);
                combatProfileText.text = profileLabel;
                combatProfileText.color = CombatProfileClassifier.GetColorForLabel(profileLabel);
            }
        }
    }

    private void HandleCharacterHpChanged(float current, float max)
    {
        _vitalsVisible = characterStats == null || !characterStats.IsDead;
        float fill = max > 0f ? current / max : 0f;

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(fill);

        if (hpValueText != null)
        {
            hpValueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
            hpValueText.gameObject.SetActive(ToggleSettingsStore.Get(ToggleSettingId.ShowOverheadHealthGuardNumbers));
        }
    }

    private void HandleCharacterGuardChanged(float current, float naturalCap)
    {
        if (characterStats == null)
            return;

        if (guardFill != null)
        {
            if (naturalCap <= 0.0001f)
                guardFill.fillAmount = 0f;
            else
            {
                float hpD = Mathf.Max(1f, characterStats.MaxHP);
                float guardZone01 = Mathf.Clamp01(naturalCap / hpD);
                float guardFill01 = Mathf.Clamp01(current / naturalCap);
                guardFill.fillAmount = Mathf.Clamp01(guardFill01 * guardZone01);
            }
        }

        if (guardValueText)
        {
            if (!ToggleSettingsStore.Get(ToggleSettingId.ShowOverheadHealthGuardNumbers))
            {
                guardValueText.gameObject.SetActive(false);
                return;
            }

            if (current <= 0.0001f)
                guardValueText.gameObject.SetActive(false);
            else
            {
                guardValueText.gameObject.SetActive(true);
                guardValueText.text = $"{Mathf.CeilToInt(current)}";
            }
        }
    }

    private void HandleEnemyHpChanged(int current, int max)
    {
        ApplyHpFillColorByOwner();
        float fill = max > 0 ? (float)current / max : 0f;

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(fill);

        if (hpValueText != null)
        {
            hpValueText.text = $"{current}/{max}";
            hpValueText.gameObject.SetActive(ToggleSettingsStore.Get(ToggleSettingId.ShowOverheadHealthGuardNumbers));
        }
    }

    private void ApplyHpFillColorByOwner()
    {
        if (hpFill == null || enemy == null)
            return;
        hpFill.color = enemyHpFillColor;
    }

    public void RefreshDebuffIcons()
    {
        ClearDebuffIcons();

        if (_hpBarOnlyLayout)
            return;

        if (ailments == null || debuffContainer == null || debuffIconPrefab == null)
            return;

        if (ailments.HasBleed)
            SpawnDebuffIcon(bleedIcon, "Bleed", 1);

        if (ailments.HasPoison)
            SpawnDebuffIcon(poisonIcon, "Poison", ailments.PoisonStacks);

        if (ailments.HasBurn)
            SpawnDebuffIcon(burnIcon, "Burn", ailments.BurnStacks);

        if (ailments.HasChill)
            SpawnDebuffIcon(chillIcon, "Chill", ailments.ChillStacks);

        if (ailments.HasShock)
            SpawnDebuffIcon(shockIcon, "Shock", 1);
    }

    private void SpawnDebuffIcon(Sprite sprite, string iconName, int stacks)
    {
        if (sprite == null) return;

        GameObject icon = Instantiate(debuffIconPrefab, debuffContainer);
        icon.name = $"Debuff_{iconName}";

        DebuffIconUI iconUI = icon.GetComponent<DebuffIconUI>();
        if (iconUI != null)
        {
            iconUI.SetData(sprite, stacks);
        }
        else
        {
            Image image = icon.GetComponent<Image>();
            if (image == null)
                image = icon.GetComponentInChildren<Image>();

            if (image != null)
                image.sprite = sprite;
        }

        spawnedDebuffIcons.Add(icon);

        foreach (Graphic g in icon.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
    }

    private void EnsureClickableBacking()
    {
        if (root == null)
            return;

        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        _clickBackingImage = root.GetComponent<Image>();
        if (_clickBackingImage == null)
        {
            _clickBackingImage = root.gameObject.AddComponent<Image>();
            _clickBackingImage.color = new Color(1f, 1f, 1f, 0f);
        }

        _clickBackingImage.raycastTarget = !_hpBarOnlyLayout;

        OverheadClickRelay relay = root.GetComponent<OverheadClickRelay>();
        if (relay == null)
            relay = root.gameObject.AddComponent<OverheadClickRelay>();
        relay.Initialize(this);
    }

    internal void NotifyOverheadClicked(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        if (enemy == null || enemy.IsDead)
            return;

        if (_playerCombatCache == null)
            _playerCombatCache = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Exclude);

        if (_playerCombatCache == null)
            return;

        _playerCombatCache.SetTarget(enemy);
    }

    private void ClearDebuffIcons()
    {
        for (int i = 0; i < spawnedDebuffIcons.Count; i++)
        {
            if (spawnedDebuffIcons[i] != null)
                Destroy(spawnedDebuffIcons[i]);
        }

        spawnedDebuffIcons.Clear();
    }

    private sealed class OverheadClickRelay : MonoBehaviour, IPointerClickHandler
    {
        private UnitOverheadUI _owner;

        public void Initialize(UnitOverheadUI owner)
        {
            _owner = owner;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _owner?.NotifyOverheadClicked(eventData);
        }
    }
}