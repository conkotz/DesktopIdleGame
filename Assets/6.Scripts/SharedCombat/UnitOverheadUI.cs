using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UnitOverheadUI : MonoBehaviour
{
    private const string StripUiFrameObjectName = "UI_Frame";

    [Header("UI Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Optional. Shows combat profile label (e.g. Glass Cannon, Deadly); color is set from the profile. Assign in the Inspector.")]
    [SerializeField] private TMP_Text combatProfileText;
    [SerializeField] private Image hpFill;
    [Tooltip("Player overhead only — mana bar fill under HPBar/MpFill.")]
    [SerializeField] private Image mpFill;
    [Tooltip("Player overhead only — energy bar fill under HPBar/EnergyFill.")]
    [SerializeField] private Image energyFill;
    [Tooltip("Optional; same bar stack as player/enemy HP.")]
    [SerializeField] private Image guardFill;
    [Tooltip("Optional. Shows current guard / natural cap.")]
    [SerializeField] private TMP_Text guardValueText;
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private Transform debuffContainer;
    [SerializeField] private GameObject debuffIconPrefab;

    [Header("Debuff Icon Size")]
    [Tooltip("Uniform scale applied to each spawned debuff icon (1 = prefab default, 0.5 = half). " +
             "Drives transform.localScale on the icon root (which scales the inner Icon / overlay / stack text) " +
             "and a LayoutElement so the container's Horizontal/Vertical Layout Group packs the scaled icons tightly. " +
             "Use < 1 to shrink enemy overhead debuffs while the player's debuff bar keeps the prefab default.")]
    [SerializeField, Min(0.05f)] private float debuffIconScale = 1f;

    [Header("Debuff stack text (overhead)")]
    [Tooltip("When the icon root is scaled down, counter-scale the stack TMP so it does not shrink with the icon.")]
    [SerializeField] private bool debuffStackCompensateIconScale = true;
    [Tooltip("Extra multiplier on stack text size after compensation (1 = match prefab readability when compensated).")]
    [SerializeField, Min(0.01f)] private float debuffStackTextScale = 1f;
    [Tooltip("Added to the StackText RectTransform anchoredPosition from the prefab (e.g. nudge away from the icon).")]
    [SerializeField] private Vector2 debuffStackTextAnchoredPositionOffset = Vector2.zero;
    [Tooltip("Enemy overhead HP fill tint. Player overhead uses prefab fill until an ailment overrides it (see code).")]
    [SerializeField] private Color enemyHpFillColor = new(1f, 0.42f, 0.2f, 1f);

    [Header("HP segment marks")]
    [SerializeField] private bool hpSegmentMarksEnabled = true;
    [Tooltip("Ideal spacing between tick marks in HP (e.g. 50). On high max HP the interval auto-increases so the bar stays readable.")]
    [SerializeField, Min(0)] private int hpSegmentHpInterval = 50;
    [Tooltip("Never draw more than this many ticks on one bar (prevents 5000 HP / 50 = solid white bar).")]
    [SerializeField, Range(4, 32)] private int hpSegmentMaxMarkCount = 12;
    [Tooltip("Minimum canvas pixels between ticks; bumps interval when the bar is narrow.")]
    [SerializeField, Min(0f)] private float hpSegmentMinSpacingPx = 8f;
    [SerializeField, Min(0.5f)] private float hpSegmentLineWidthPx = 1f;
    [SerializeField] private Color hpSegmentLineColor = new(1f, 1f, 1f, 0.22f);

    [Header("Debuff Sprites")]
    [SerializeField] private Sprite bleedIcon;
    [SerializeField] private Sprite poisonIcon;
    [SerializeField] private Sprite burnIcon;
    [SerializeField] private Sprite chillIcon;
    [SerializeField] private Sprite shockIcon;
    [Tooltip("Shadow Strike — Lethal Mark (enhancement 1). Assign in inspector.")]
    [SerializeField] private Sprite shadowStrikeLethalMarkIcon;
    [Tooltip("Shadow Strike — Shadow Execution mark (enhancement 2). Assign in inspector.")]
    [SerializeField] private Sprite shadowStrikeExecutionMarkIcon;

    [Header("Auto Bind")]
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private EnemyBaseController enemy;
    [SerializeField] private AilmentController ailments;
    private EnemyShadowStrikeMarks _shadowStrikeMarks;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Camera targetCamera;

    [Header("Target marker (enemy)")]
    [Tooltip("Optional anchor at the top of the overhead strip. When unset, one is created on OverheadUIRoot at runtime.")]
    [SerializeField] private RectTransform targetMarkerAnchor;
    [Tooltip("UIImage on the anchor (or a child). Auto-created at runtime if missing.")]
    [SerializeField] private Image targetMarkerImage;
    [Tooltip("Canvas pixels above the root top for the runtime anchor (only when Target Marker Anchor is auto-created).")]
    [SerializeField] private float targetMarkerAnchorPaddingPx = 8f;
    [SerializeField] private Vector2 targetMarkerImageSize = new Vector2(44f, 44f);
    [Tooltip("Alpha range for the combat target marker breathe cycle.")]
    [SerializeField, Range(0f, 1f)] private float targetMarkerPulseMinAlpha = 0.05f;
    [SerializeField, Range(0f, 1f)] private float targetMarkerPulseMaxAlpha = 1f;
    [SerializeField, Min(0.05f)] private float targetMarkerFadeSeconds = 0.75f;
    [SerializeField, Min(0f)] private float targetMarkerHoldAtPeakSeconds = 1.5f;

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
    [Tooltip(
        "Extra canvas pixels added only to the first enemy row stacked above the player (not other enemies or higher rows).")]
    [SerializeField] private float stackFirstEnemyAbovePlayerExtraPx = 18f;
    [Tooltip(
        "Optional minimum half-width (canvas px) for overlap tests. 0 = use measured rect + TMP bounds only. " +
        "Increase slightly if very narrow layouts fail to stack when enemies stand on the same spot.")]
    [SerializeField] private float stackMinClusteringHalfWidthPx = 0f;
    [Tooltip(
        "When two overheads are this close in canvas X, keep the previous left/right order instead of re-sorting every frame.")]
    [SerializeField] private float stackSortHysteresisPx = 16f;
    [Tooltip("Minimum time a new vertical stack lane must stay valid before the bar moves to it (reduces swap flicker).")]
    [SerializeField] private float stackLaneChangeCooldownSeconds = 0.4f;

    private RectTransform canvasRect;
    private readonly List<GameObject> spawnedDebuffIcons = new();
    private readonly List<Image> _hpSegmentLineImages = new();
    private RectTransform _hpSegmentContainer;
    private int _cachedHpSegmentMaxHp = -1;
    private int _cachedHpSegmentEffectiveInterval = -1;
    private static Sprite s_hpSegmentWhiteSprite;

    private Vector2 _stackBaseAnchored;
    private float _stackYOffset;
    private float _externalScale = 1f;

    /// <summary>Cached <see cref="SliderSettingId.OverheadHpBarResize"/>; multiplied into root scale alongside <see cref="_externalScale"/>.</summary>
    private float _overheadBarResizeSlider = 1f;
    private Vector3 _additionalWorldOffset = Vector3.zero;

    /// <summary>Cached <see cref="SliderSettingId.HudResize"/>; dividing undoes CanvasScaler HUD growth so overhead size follows overhead slider only.</summary>
    private float _hudResizeSlider = 1f;

    private static readonly List<UnitOverheadUI> s_instances = new();
    private static readonly Dictionary<int, List<UnitOverheadUI>> s_byCanvasScratch = new();
    private static readonly List<List<UnitOverheadUI>> s_canvasListPool = new();
    private static bool s_canvasCallbackSubscribed;
    private static int s_lastStackResolveFrame = -1;
    private static readonly Dictionary<int, int> s_lastAssignedStackLaneByUiId = new();
    private static readonly Dictionary<int, int> s_lastSortRankByUiId = new();
    private static readonly Dictionary<int, PendingStackLane> s_pendingStackLaneByUiId = new();

    private struct PendingStackLane
    {
        public int lane;
        public float sinceUnscaledTime;
    }

    private PlayerCombatController _playerCombatCache;
    private Image _clickBackingImage;

    private bool _hpBarOnlyLayout;
    private bool _vitalsVisible = true;
    private PlayerCombatState _playerCombatState;

    private Color _playerHpFillCapturedBase = Color.white;
    private bool _playerHpFillBaseCaptured;
    private bool _wasPoisonForStatusPopup;
    private bool _wasBleedForStatusPopup;
    private bool _wasBurnForStatusPopup;
    private bool _wasShockForStatusPopup;
    private bool _wasChillForStatusPopup;

    /// <summary>True when the follow target is in the strip camera band this frame; used for overlap stacking (same idea as when the whole object was deactivated off-screen).</summary>
    private bool _worldBandVisible;

    private Vector3 _targetMarkerWorldTop;
    private bool _targetMarkerWorldTopValid;
    private Color _targetMarkerBaseColor = Color.white;

    private static UnitOverheadUI s_activeCombatTargetMarkerUi;

    private void Awake()
    {
        if (!root) root = transform as RectTransform;

        // Old behaviour still works if this UI is placed under the enemy directly.
        if (!characterStats) characterStats = GetComponentInParent<CharacterStats>();
        if (!enemy) enemy = GetComponentInParent<EnemyBaseController>();
        if (!ailments) ailments = GetComponentInParent<AilmentController>();

        ResolveResourceFillRefs();
        ApplyPlayerResourceBarVisibility();
        EnsureClickableBacking();
        EnsureTargetMarkerAnchor();
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
        if (s_activeCombatTargetMarkerUi == this)
            s_activeCombatTargetMarkerUi = null;

        SetTargetMarkerVisible(false, null);
        SliderSettingsStore.Changed -= HandleSliderSettingsChanged;
        s_instances.Remove(this);
        Unsubscribe();
        ResetPlayerAilmentStatusPopupLatches();
    }

    private void LateUpdate()
    {
        ComputeBaseAnchoredAndVisibility();

        if (!ShouldUseOverlapStacking())
            ApplyDirectPosition();
        else
            ApplyStackedPosition();

        EnsurePlayerOverheadDrawsAboveEnemyOverheads();
        TickTargetMarkerPulse();
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

        ResetPlayerAilmentStatusPopupLatches();
        bool isPlayerOverhead =
            stats != null &&
            enemyController == null &&
            stats.GetComponentInParent<PlayerController>() != null;
        if (hpFill != null && isPlayerOverhead)
        {
            _playerHpFillCapturedBase = hpFill.color;
            _playerHpFillBaseCaptured = true;
        }
        else
            _playerHpFillBaseCaptured = false;

        ApplyHpBarOnlyVisuals();
        ApplyPlayerResourceBarVisibility();
        EnsureTargetMarkerAnchor();
        if (enemy != null)
            EnsureTargetMarkerGraphic();
        Subscribe();
        RefreshAll();
        EnsureClickableBacking();
        ComputeBaseAnchoredAndVisibility();
        if (!ShouldUseOverlapStacking())
            ApplyDirectPosition();
        else
            ApplyStackedPosition();

        EnsureDrawsBehindStripUiFrame();
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

    private void ResolveResourceFillRefs()
    {
        Transform searchRoot = root != null ? root : transform;
        if (!mpFill)
        {
            Transform t = searchRoot.Find("OverheadUIRoot/HPBar/MpFill");
            if (!t) t = searchRoot.Find("HPBar/MpFill");
            if (!t) t = searchRoot.Find("MpFill");
            if (t) mpFill = t.GetComponent<Image>();
        }

        if (!energyFill)
        {
            Transform t = searchRoot.Find("OverheadUIRoot/HPBar/EnergyFill");
            if (!t) t = searchRoot.Find("HPBar/EnergyFill");
            if (!t) t = searchRoot.Find("EnergyFill");
            if (t) energyFill = t.GetComponent<Image>();
        }

        if (!guardFill)
        {
            Transform t = searchRoot.Find("OverheadUIRoot/HPBar/GuardFill");
            if (!t) t = searchRoot.Find("HPBar/GuardFill");
            if (!t) t = searchRoot.Find("GuardFill");
            if (t) guardFill = t.GetComponent<Image>();
        }

        if (!guardValueText)
        {
            Transform t = searchRoot.Find("OverheadUIRoot/HPBar/GuardValueText");
            if (!t) t = searchRoot.Find("HPBar/GuardValueText");
            if (!t) t = searchRoot.Find("GuardValueText");
            if (t) guardValueText = t.GetComponent<TMP_Text>();
        }
    }

    private bool IsPlayerOverhead() =>
        enemy == null &&
        characterStats != null &&
        characterStats.GetComponentInParent<PlayerController>() != null;

    private void ApplyPlayerResourceBarVisibility()
    {
        bool show = IsPlayerOverhead();
        if (mpFill != null)
            mpFill.gameObject.SetActive(show);
        if (energyFill != null)
            energyFill.gameObject.SetActive(show);
    }

    private void RefreshPlayerResourceBarFills()
    {
        if (!IsPlayerOverhead() || characterStats == null)
            return;

        HandleCharacterManaChanged(characterStats.Mana, characterStats.MaxMana);
        HandleCharacterEnergyChanged(characterStats.Energy, characterStats.MaxEnergy);
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
        for (int i = s_instances.Count - 1; i >= 0; i--)
        {
            if (!s_instances[i])
                s_instances.RemoveAt(i);
        }

        for (int i = 0; i < s_canvasListPool.Count; i++)
            s_canvasListPool[i].Clear();
        s_byCanvasScratch.Clear();

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
            if (!s_byCanvasScratch.TryGetValue(canvasKey, out List<UnitOverheadUI> list))
            {
                list = RentCanvasList();
                s_byCanvasScratch[canvasKey] = list;
            }

            list.Add(ui);
        }

        foreach (List<UnitOverheadUI> list in s_byCanvasScratch.Values)
            ResolveStackingForCanvasGroup(list);
    }

    private static List<UnitOverheadUI> RentCanvasList()
    {
        for (int i = 0; i < s_canvasListPool.Count; i++)
        {
            List<UnitOverheadUI> list = s_canvasListPool[i];
            if (list.Count == 0)
                return list;
        }

        List<UnitOverheadUI> created = new List<UnitOverheadUI>(16);
        s_canvasListPool.Add(created);
        return created;
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

        PruneStaleStackState(activeIds);

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
        float firstAbovePlayerExtra = Mathf.Max(0f, candidates[0].stackFirstEnemyAbovePlayerExtraPx);

        float minHalfW = Mathf.Max(0f, candidates[0].stackMinClusteringHalfWidthPx);

        var spans = new List<(float minX, float maxX, UnitOverheadUI ui)>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            candidates[i].GetHorizontalSpanInCanvas(out float minX, out float maxX);
            WidenSpanForClusterMerge(candidates[i]._stackBaseAnchored.x, minHalfW, ref minX, ref maxX);
            spans.Add((minX, maxX, candidates[i]));
        }

        float sortHysteresisPx = Mathf.Max(0f, candidates[0].stackSortHysteresisPx);
        spans.Sort((a, b) => CompareSpansForStableSort(a.minX, b.minX, a.ui, b.ui, sortHysteresisPx));
        for (int i = 0; i < spans.Count; i++)
            s_lastSortRankByUiId[spans[i].ui.GetInstanceID()] = i;

        float playerSpanMinX = float.PositiveInfinity;
        float playerSpanMaxX = float.NegativeInfinity;
        bool hasPlayerBaseline = false;
        for (int i = 0; i < spans.Count; i++)
        {
            if (!IsFixedPlayerBaseline(spans[i].ui))
                continue;

            hasPlayerBaseline = true;
            spans[i].ui._stackYOffset = 0f;
            playerSpanMinX = Mathf.Min(playerSpanMinX, spans[i].minX);
            playerSpanMaxX = Mathf.Max(playerSpanMaxX, spans[i].maxX);
        }

        // Assign each enemy bar to the lowest available "lane" that does not horizontally overlap
        // any other enemy bar. This avoids transitive chaining (A overlaps B, B overlaps C) from
        // forcing C onto higher rows when A and C could share the same baseline row.
        var laneLastMaxX = new List<float>(8);
        for (int i = 0; i < spans.Count; i++)
        {
            (float minX, float maxX, UnitOverheadUI ui) = spans[i];
            if (IsFixedPlayerBaseline(ui))
                continue;

            int preferredLane = 0;
            int uiId = ui.GetInstanceID();
            if (s_lastAssignedStackLaneByUiId.TryGetValue(uiId, out int rememberedLane))
                preferredLane = Mathf.Max(0, rememberedLane);

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
                laneIndex = laneLastMaxX.Count;

            int committedLane = CommitStackLaneWithCooldown(
                ui,
                uiId,
                laneIndex,
                minX,
                maxX,
                laneLastMaxX,
                padding,
                allowedOverlap);

            bool overlapsPlayer = hasPlayerBaseline &&
                HorizontalSpansOverlap(minX, maxX, playerSpanMinX, playerSpanMaxX, padding, allowedOverlap);

            int displayLane = committedLane;
            if (overlapsPlayer)
                displayLane = committedLane + 1;

            float yOffset = displayLane * spacing;
            if (overlapsPlayer && committedLane == 0 && firstAbovePlayerExtra > 0f)
                yOffset += firstAbovePlayerExtra;

            ui._stackYOffset = yOffset;
            s_lastAssignedStackLaneByUiId[uiId] = committedLane;

            if (committedLane >= laneLastMaxX.Count)
                laneLastMaxX.Add(maxX);
            else
                laneLastMaxX[committedLane] = Mathf.Max(laneLastMaxX[committedLane], maxX);
        }

        for (int i = 0; i < candidates.Count; i++)
            candidates[i].ApplyStackedPosition();
    }

    private static void PruneStaleStackState(HashSet<int> activeIds)
    {
        if (s_lastAssignedStackLaneByUiId.Count == 0 &&
            s_lastSortRankByUiId.Count == 0 &&
            s_pendingStackLaneByUiId.Count == 0)
            return;

        var stale = new List<int>();
        foreach (int id in s_lastAssignedStackLaneByUiId.Keys)
        {
            if (!activeIds.Contains(id))
                stale.Add(id);
        }

        for (int i = 0; i < stale.Count; i++)
        {
            int id = stale[i];
            s_lastAssignedStackLaneByUiId.Remove(id);
            s_lastSortRankByUiId.Remove(id);
            s_pendingStackLaneByUiId.Remove(id);
        }
    }

    private static int CompareSpansForStableSort(
        float minXA,
        float minXB,
        UnitOverheadUI uiA,
        UnitOverheadUI uiB,
        float hysteresisPx)
    {
        float dx = minXA - minXB;
        if (Mathf.Abs(dx) <= hysteresisPx)
        {
            int idA = uiA.GetInstanceID();
            int idB = uiB.GetInstanceID();
            bool hasA = s_lastSortRankByUiId.TryGetValue(idA, out int rankA);
            bool hasB = s_lastSortRankByUiId.TryGetValue(idB, out int rankB);
            if (hasA && hasB)
                return rankA.CompareTo(rankB);
            return idA.CompareTo(idB);
        }

        return dx < 0f ? -1 : (dx > 0f ? 1 : 0);
    }

    private static int CommitStackLaneWithCooldown(
        UnitOverheadUI ui,
        int uiId,
        int computedLane,
        float minX,
        float maxX,
        List<float> laneLastMaxX,
        float padding,
        float allowedOverlap)
    {
        if (!s_lastAssignedStackLaneByUiId.TryGetValue(uiId, out int previousLane) || previousLane == computedLane)
        {
            s_pendingStackLaneByUiId.Remove(uiId);
            return computedLane;
        }

        float cooldown = Mathf.Max(0f, ui.stackLaneChangeCooldownSeconds);
        if (cooldown <= 0f)
        {
            s_pendingStackLaneByUiId.Remove(uiId);
            return computedLane;
        }

        if (!s_pendingStackLaneByUiId.TryGetValue(uiId, out PendingStackLane pending) || pending.lane != computedLane)
            pending = new PendingStackLane { lane = computedLane, sinceUnscaledTime = Time.unscaledTime };

        s_pendingStackLaneByUiId[uiId] = pending;

        bool previousLaneStillFits = IsStackLaneAvailable(
            previousLane,
            minX,
            maxX,
            laneLastMaxX,
            padding,
            allowedOverlap);

        if (!previousLaneStillFits)
        {
            s_pendingStackLaneByUiId.Remove(uiId);
            return computedLane;
        }

        if (Time.unscaledTime - pending.sinceUnscaledTime >= cooldown)
        {
            s_pendingStackLaneByUiId.Remove(uiId);
            return computedLane;
        }

        return previousLane;
    }

    private static bool IsStackLaneAvailable(
        int lane,
        float minX,
        float maxX,
        List<float> laneLastMaxX,
        float padding,
        float allowedOverlap)
    {
        if (lane < 0 || lane >= laneLastMaxX.Count)
            return true;

        return minX > laneLastMaxX[lane] + padding - allowedOverlap;
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

    private static bool HorizontalSpansOverlap(
        float minX,
        float maxX,
        float otherMinX,
        float otherMaxX,
        float padding,
        float allowedOverlap)
    {
        return minX <= otherMaxX + padding - allowedOverlap &&
               otherMinX <= maxX + padding - allowedOverlap;
    }

    /// <summary>
    /// Player overheads share the strip canvas with enemies. Keep the player instance later in the sibling list so it
    /// draws on top when overlapping (enemy UI otherwise wins by spawn order), but still before <see cref="StripUiFrameObjectName"/>.
    /// </summary>
    private void EnsurePlayerOverheadDrawsAboveEnemyOverheads()
    {
        if (enemy != null || !gameObject.activeSelf)
            return;
        if (characterStats == null || characterStats.GetComponentInParent<PlayerController>() == null)
            return;
        if (parentCanvas == null)
            return;

        Transform strip = parentCanvas.transform;
        int uiFrameSibling = TryGetStripUiFrameSiblingIndex(parentCanvas, out int uiFrameIndex)
            ? uiFrameIndex
            : strip.childCount;

        int lastEnemyOverheadSibling = -1;
        int scanCount = Mathf.Min(strip.childCount, uiFrameSibling);
        for (int i = 0; i < scanCount; i++)
        {
            UnitOverheadUI childUi = strip.GetChild(i).GetComponent<UnitOverheadUI>();
            if (childUi != null && childUi.enemy != null)
                lastEnemyOverheadSibling = i;
        }

        if (lastEnemyOverheadSibling < 0)
            return;

        int want = Mathf.Min(lastEnemyOverheadSibling + 1, uiFrameSibling);
        int cur = transform.GetSiblingIndex();
        if (cur < want)
            transform.SetSiblingIndex(want);
    }

    /// <summary>
    /// Overheads are parented to <c>StripUICanvas</c>. Keep them before <see cref="StripUiFrameObjectName"/> so HUD panels
    /// (quick menu, windows, etc.) draw on top.
    /// </summary>
    private void EnsureDrawsBehindStripUiFrame()
    {
        if (parentCanvas == null)
            return;

        if (!TryGetStripUiFrameSiblingIndex(parentCanvas, out int uiFrameIndex))
            return;

        int selfIndex = transform.GetSiblingIndex();
        if (selfIndex >= uiFrameIndex)
            transform.SetSiblingIndex(uiFrameIndex);
    }

    private static bool TryGetStripUiFrameSiblingIndex(Canvas stripCanvas, out int uiFrameSiblingIndex)
    {
        uiFrameSiblingIndex = -1;
        if (stripCanvas == null)
            return false;

        Transform uiFrame = stripCanvas.transform.Find(StripUiFrameObjectName);
        if (uiFrame == null)
            return false;

        uiFrameSiblingIndex = uiFrame.GetSiblingIndex();
        return true;
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

    /// <summary>
    /// World position above the top of this enemy's strip overhead UI (name + HP bar), for world-space target markers.
    /// Uses the same follow anchor + camera as the overhead bar, not UI rect world corners (those are off the gameplay plane).
    /// </summary>
    /// <summary>Shows the combat target marker on this enemy's overhead strip (canvas UI — moves with stack offset).</summary>
    public static void SetCombatTargetMarkerForEnemy(EnemyBaseController enemyController, Sprite sprite, bool show)
    {
        if (!show || enemyController == null || !IsEnemyOverheadTarget(enemyController))
        {
            ClearCombatTargetMarker();
            return;
        }

        if (s_activeCombatTargetMarkerUi != null && s_activeCombatTargetMarkerUi.enemy != enemyController)
            s_activeCombatTargetMarkerUi.SetTargetMarkerVisible(false, null);

        UnitOverheadUI match = FindOverheadForEnemy(enemyController);
        if (match == null)
            return;

        match.SetTargetMarkerVisible(true, sprite);
        s_activeCombatTargetMarkerUi = match;
    }

    public static void ClearCombatTargetMarker()
    {
        if (s_activeCombatTargetMarkerUi != null)
        {
            s_activeCombatTargetMarkerUi.SetTargetMarkerVisible(false, null);
            s_activeCombatTargetMarkerUi = null;
        }
    }

    public static bool TryGetWorldPointAboveEnemyOverhead(
        EnemyBaseController enemyController,
        float extraWorldUp,
        out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!enemyController)
            return false;

        UnitOverheadUI match = FindOverheadForEnemy(enemyController);
        if (match != null && match.TryComputeWorldPointAboveOverhead(extraWorldUp, out worldPosition))
            return true;

        Collider2D col = enemyController.GetComponentInChildren<Collider2D>();
        if (!col)
            return false;

        Bounds b = col.bounds;
        worldPosition = new Vector3(
            b.center.x,
            b.max.y + 1.2f + extraWorldUp,
            enemyController.transform.position.z);
        return true;
    }

    public static void RefreshShadowStrikeMarksForEnemy(EnemyBaseController enemyController)
    {
        UnitOverheadUI match = FindOverheadForEnemy(enemyController);
        if (match == null)
            return;

        match.EnsureShadowStrikeMarksSubscription();
        match.RefreshDebuffIcons();
    }

    private void EnsureShadowStrikeMarksSubscription()
    {
        if (enemy == null)
            return;

        EnemyShadowStrikeMarks marks = enemy.GetComponent<EnemyShadowStrikeMarks>();
        if (marks == _shadowStrikeMarks)
            return;

        if (_shadowStrikeMarks != null)
            _shadowStrikeMarks.OnMarksChanged -= RefreshDebuffIcons;

        _shadowStrikeMarks = marks;
        if (_shadowStrikeMarks != null)
            _shadowStrikeMarks.OnMarksChanged += RefreshDebuffIcons;
    }

    private static UnitOverheadUI FindOverheadForEnemy(EnemyBaseController enemyController)
    {
        if (!enemyController)
            return null;

        for (int i = 0; i < s_instances.Count; i++)
        {
            UnitOverheadUI ui = s_instances[i];
            if (ui != null && ui.enemy == enemyController && OverheadFollowsEnemy(ui, enemyController))
                return ui;
        }

        return null;
    }

    private static bool IsEnemyOverheadTarget(EnemyBaseController enemyController)
    {
        if (!enemyController)
            return false;

        if (enemyController.GetComponent<PlayerController>() != null)
            return false;

        if (enemyController.GetComponentInParent<PlayerController>() != null)
            return false;

        return true;
    }

    private static bool OverheadFollowsEnemy(UnitOverheadUI ui, EnemyBaseController enemyController)
    {
        if (ui == null || !enemyController)
            return false;

        Transform ft = ui.followTarget != null ? ui.followTarget : ui.enemy != null ? ui.enemy.transform : null;
        if (!ft)
            return false;

        return ft == enemyController.transform || ft.IsChildOf(enemyController.transform);
    }

    private bool TryComputeWorldPointAboveOverhead(float extraWorldUp, out Vector3 worldPosition)
    {
        worldPosition = default;
        RefreshTargetMarkerWorldCache();

        if (!_targetMarkerWorldTopValid)
            return false;

        worldPosition = _targetMarkerWorldTop + Vector3.up * extraWorldUp;
        return true;
    }

    private void SetTargetMarkerVisible(bool visible, Sprite sprite)
    {
        if (enemy == null)
        {
            if (targetMarkerImage != null)
                targetMarkerImage.enabled = false;
            return;
        }

        EnsureTargetMarkerAnchor();
        EnsureTargetMarkerGraphic();

        if (targetMarkerImage == null)
            return;

        if (sprite != null)
            targetMarkerImage.sprite = sprite;

        bool show = visible && targetMarkerImage.sprite != null;
        targetMarkerImage.enabled = show;

        if (show)
        {
            _targetMarkerBaseColor = targetMarkerImage.color;
            _targetMarkerBaseColor.a = 1f;
            if (targetMarkerAnchor != null)
                targetMarkerAnchor.SetAsLastSibling();
        }
        else
        {
            Color c = targetMarkerImage.color;
            c.a = 1f;
            targetMarkerImage.color = c;
        }
    }

    private void TickTargetMarkerPulse()
    {
        if (targetMarkerImage == null || !targetMarkerImage.enabled)
            return;

        float alpha = EvaluateTargetMarkerBreatheAlpha(
            Time.time,
            targetMarkerPulseMinAlpha,
            targetMarkerPulseMaxAlpha,
            targetMarkerFadeSeconds,
            targetMarkerHoldAtPeakSeconds);

        Color c = _targetMarkerBaseColor;
        c.a = alpha;
        targetMarkerImage.color = c;
    }

    private static float EvaluateTargetMarkerBreatheAlpha(
        float time,
        float minAlpha,
        float maxAlpha,
        float fadeSeconds,
        float holdAtPeakSeconds)
    {
        float minA = Mathf.Clamp01(minAlpha);
        float maxA = Mathf.Clamp01(maxAlpha);
        if (maxA < minA)
            (minA, maxA) = (maxA, minA);

        float fade = Mathf.Max(0.05f, fadeSeconds);
        float hold = Mathf.Max(0f, holdAtPeakSeconds);
        float cycle = fade + hold + fade;
        float t = cycle > 0f ? time % cycle : 0f;

        if (t < fade)
            return Mathf.Lerp(minA, maxA, t / fade);

        t -= fade;
        if (t < hold)
            return maxA;

        t -= hold;
        return Mathf.Lerp(maxA, minA, t / fade);
    }

    /// <summary>
    /// Caches world-space top via strip camera screen projection (fallback if canvas marker is unused).
    /// </summary>
    private void RefreshTargetMarkerWorldCache()
    {
        _targetMarkerWorldTopValid = false;
        if (root == null || !root.gameObject.activeInHierarchy || !root.gameObject.activeSelf)
            return;

        if (targetCamera == null)
            return;

        Transform ft = followTarget != null ? followTarget : enemy != null ? enemy.transform : null;
        if (ft == null)
            return;

        if (!TryGetTargetMarkerScreenPoint(out Vector3 screenPoint))
            return;

        Vector3 world = targetCamera.ScreenToWorldPoint(screenPoint);
        _targetMarkerWorldTop = new Vector3(world.x, world.y, ft.position.z);
        _targetMarkerWorldTopValid = true;
    }

    private bool TryGetTargetMarkerScreenPoint(out Vector3 screenPoint)
    {
        screenPoint = default;
        Transform ft = followTarget != null ? followTarget : enemy != null ? enemy.transform : null;
        if (ft == null || targetCamera == null)
            return false;

        Canvas.ForceUpdateCanvases();

        Vector3 baseWorld = ft.position + worldOffset + _additionalWorldOffset;
        Vector3 baseScreen = targetCamera.WorldToScreenPoint(baseWorld);

        float screenX = baseScreen.x;
        float screenY = baseScreen.y;

        if (targetMarkerAnchor != null && targetMarkerAnchor.gameObject.activeInHierarchy)
        {
            Vector3 anchorScreen = targetCamera.WorldToScreenPoint(targetMarkerAnchor.position);
            screenX = anchorScreen.x;
            screenY = anchorScreen.y;
        }
        else if (canvasRect != null)
        {
            root.GetWorldCorners(UnitOverheadUIWorkCorners);
            float topY = Mathf.Max(UnitOverheadUIWorkCorners[1].y, UnitOverheadUIWorkCorners[2].y);
            float centerX = (UnitOverheadUIWorkCorners[1].x + UnitOverheadUIWorkCorners[2].x) * 0.5f;
            RefineTargetMarkerTopWithTmpWorld(nameText, ref topY);
            RefineTargetMarkerTopWithTmpWorld(combatProfileText, ref topY);
            RefineTargetMarkerTopWithTmpWorld(hpValueText, ref topY);
            Vector3 topScreen = targetCamera.WorldToScreenPoint(new Vector3(centerX, topY, 0f));
            screenX = topScreen.x;
            screenY = topScreen.y;
        }
        else
            return false;

        screenPoint = new Vector3(screenX, screenY, baseScreen.z);
        return true;
    }

    private static void RefineTargetMarkerTopWithTmpWorld(TMP_Text tmp, ref float topY)
    {
        if (tmp == null || !tmp.gameObject.activeInHierarchy)
            return;

        tmp.ForceMeshUpdate();
        Bounds bounds = tmp.textBounds;
        if (bounds.size.sqrMagnitude < 0.0001f)
            return;

        Vector3 localTop = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        float worldY = tmp.transform.TransformPoint(localTop).y;
        if (worldY > topY)
            topY = worldY;
    }

    private void EnsureTargetMarkerAnchor()
    {
        if (root == null)
            return;

        if (targetMarkerAnchor == null)
        {
            Transform existing = root.Find("TargetMarkerAnchor");
            if (existing != null)
                targetMarkerAnchor = existing as RectTransform;
        }

        if (enemy == null || targetMarkerAnchor != null)
            return;

        var go = new GameObject("TargetMarkerAnchor", typeof(RectTransform));
        targetMarkerAnchor = go.GetComponent<RectTransform>();
        targetMarkerAnchor.SetParent(root, false);
        targetMarkerAnchor.anchorMin = new Vector2(0.5f, 1f);
        targetMarkerAnchor.anchorMax = new Vector2(0.5f, 1f);
        targetMarkerAnchor.pivot = new Vector2(0.5f, 0f);
        targetMarkerAnchor.anchoredPosition = new Vector2(0f, targetMarkerAnchorPaddingPx);
        targetMarkerAnchor.sizeDelta = Vector2.zero;
    }

    private void EnsureTargetMarkerGraphic()
    {
        if (enemy == null || targetMarkerAnchor == null)
            return;

        if (targetMarkerImage == null)
            targetMarkerImage = targetMarkerAnchor.GetComponent<Image>();

        if (targetMarkerImage == null)
            targetMarkerImage = targetMarkerAnchor.GetComponentInChildren<Image>(true);

        if (targetMarkerImage != null)
        {
            targetMarkerImage.raycastTarget = false;
            return;
        }

        var go = new GameObject("TargetMarkerImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(targetMarkerAnchor, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = targetMarkerImageSize;

        targetMarkerImage = go.GetComponent<Image>();
        targetMarkerImage.raycastTarget = false;
        targetMarkerImage.preserveAspect = true;
        targetMarkerImage.enabled = false;
    }

    private void ExpandTopWithTmpMeshBounds(RectTransform canvasRt, ref float topLocalY)
    {
        if (nameText != null && nameText.rectTransform != null)
            ExpandTmpTop(canvasRt, nameText, ref topLocalY);
        if (combatProfileText != null && combatProfileText.rectTransform != null)
            ExpandTmpTop(canvasRt, combatProfileText, ref topLocalY);
    }

    private static void ExpandTmpTop(RectTransform canvasRt, TMP_Text tmp, ref float topLocalY)
    {
        tmp.ForceMeshUpdate();
        Bounds bounds = tmp.mesh.bounds;
        if (bounds.size.sqrMagnitude < 0.0001f)
            return;

        Vector3 localCenter = tmp.rectTransform.localPosition + bounds.center;
        Vector3 worldCenter = tmp.rectTransform.TransformPoint(localCenter);
        Vector3 canvasLocal = canvasRt.InverseTransformPoint(worldCenter);
        float halfH = bounds.extents.y * tmp.rectTransform.lossyScale.y;
        float candidate = canvasLocal.y + halfH;
        if (candidate > topLocalY)
            topLocalY = candidate;
    }

    private void Subscribe()
    {
        if (characterStats != null)
        {
            characterStats.OnNameChanged += HandleNameChanged;
            characterStats.OnHPChanged += HandleCharacterHpChanged;
            characterStats.OnGuardChanged += HandleCharacterGuardChanged;
            characterStats.OnManaChanged += HandleCharacterManaChanged;
            characterStats.OnEnergyChanged += HandleCharacterEnergyChanged;
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

        _shadowStrikeMarks = enemy != null ? enemy.GetComponent<EnemyShadowStrikeMarks>() : null;
        if (_shadowStrikeMarks != null)
            _shadowStrikeMarks.OnMarksChanged += RefreshDebuffIcons;

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
            characterStats.OnManaChanged -= HandleCharacterManaChanged;
            characterStats.OnEnergyChanged -= HandleCharacterEnergyChanged;
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

        if (_shadowStrikeMarks != null)
            _shadowStrikeMarks.OnMarksChanged -= RefreshDebuffIcons;
        _shadowStrikeMarks = null;
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
        ApplyPlayerResourceBarVisibility();
        RefreshPlayerResourceBarFills();
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
        {
            _targetMarkerWorldTopValid = false;
            return;
        }

        root.anchoredPosition = _stackBaseAnchored;
        ApplyCombinedRootScale();
        RefreshTargetMarkerWorldCache();
    }

    private void ApplyStackedPosition()
    {
        if (root == null || !root.gameObject.activeSelf)
        {
            _targetMarkerWorldTopValid = false;
            return;
        }

        root.anchoredPosition = _stackBaseAnchored + new Vector2(0f, _stackYOffset);
        ApplyCombinedRootScale();
        RefreshTargetMarkerWorldCache();
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
        {
            HandleCharacterGuardChanged(characterStats.Guard, characterStats.NaturalGuardCap);
            RefreshPlayerResourceBarFills();
            RefreshHpSegmentMarks(Mathf.RoundToInt(characterStats.MaxHP));
        }
    }

    private void HandleCharacterManaChanged(float current, float max)
    {
        if (!IsPlayerOverhead() || mpFill == null)
            return;

        mpFill.fillAmount = max <= 0f ? 0f : Mathf.Clamp01(current / max);
    }

    private void HandleCharacterEnergyChanged(float current, float max)
    {
        if (!IsPlayerOverhead() || energyFill == null)
            return;

        energyFill.fillAmount = max <= 0f ? 0f : Mathf.Clamp01(current / max);
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

        RefreshHpSegmentMarks(Mathf.RoundToInt(max));

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
            bool showGuard = current > 0.0001f;
            guardFill.gameObject.SetActive(showGuard);
            guardFill.fillAmount = showGuard
                ? ComputeGuardFillAmount(current, naturalCap, characterStats.MaxHP)
                : 0f;
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

    /// <summary>
    /// Guard shares the HP bar width; zone size follows natural cap, expanding when bonus guard exceeds it.
    /// </summary>
    private static float ComputeGuardFillAmount(float current, float naturalCap, float maxHp)
    {
        if (current <= 0.0001f)
            return 0f;

        float displayCap = naturalCap > 0.0001f ? naturalCap : current;
        if (current > naturalCap)
            displayCap = current;

        float hpD = Mathf.Max(1f, maxHp);
        float guardZone01 = Mathf.Clamp01(displayCap / hpD);
        float guardFill01 = Mathf.Clamp01(current / displayCap);
        return Mathf.Clamp01(guardFill01 * guardZone01);
    }

    private void HandleEnemyHpChanged(int current, int max)
    {
        ApplyHpFillColorByOwner();
        float fill = max > 0 ? (float)current / max : 0f;

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(fill);

        RefreshHpSegmentMarks(max);

        if (hpValueText != null)
        {
            hpValueText.text = $"{current}/{max}";
            hpValueText.gameObject.SetActive(ToggleSettingsStore.Get(ToggleSettingId.ShowOverheadHealthGuardNumbers));
        }
    }

    private static int CountHpSegmentMarks(int maxHp, int hpPerSegment)
    {
        if (maxHp <= 0 || hpPerSegment <= 0)
            return 0;

        int count = 0;
        for (int hp = hpPerSegment; hp < maxHp; hp += hpPerSegment)
            count++;
        return count;
    }

    private int ResolveEffectiveHpSegmentInterval(int maxHp)
    {
        if (hpSegmentHpInterval <= 0 || maxHp <= 0)
            return 0;

        int interval = hpSegmentHpInterval;
        int markCount = CountHpSegmentMarks(maxHp, interval);

        while (markCount > hpSegmentMaxMarkCount && interval < maxHp)
        {
            interval += hpSegmentHpInterval;
            markCount = CountHpSegmentMarks(maxHp, interval);
        }

        float barWidthPx = hpFill != null ? hpFill.rectTransform.rect.width : 0f;
        if (barWidthPx > 1f && hpSegmentMinSpacingPx > 0f && markCount > 0)
        {
            float spacingPx = interval / (float)maxHp * barWidthPx;
            while (spacingPx < hpSegmentMinSpacingPx && interval < maxHp)
            {
                interval += hpSegmentHpInterval;
                markCount = CountHpSegmentMarks(maxHp, interval);
                if (markCount <= 0)
                    break;

                spacingPx = interval / (float)maxHp * barWidthPx;
            }
        }

        interval = SnapHpSegmentIntervalUp(interval);
        while (CountHpSegmentMarks(maxHp, interval) > hpSegmentMaxMarkCount && interval < maxHp)
            interval += hpSegmentHpInterval;

        return interval;
    }

    /// <summary>Rounds up to a readable tick step without shrinking the interval (which would add more lines).</summary>
    private static int SnapHpSegmentIntervalUp(int interval)
    {
        if (interval <= 50) return 50;
        if (interval <= 100) return 100;
        if (interval <= 250) return 250;
        if (interval <= 500) return 500;
        if (interval <= 1000) return 1000;
        return Mathf.CeilToInt(interval / 1000f) * 1000;
    }

    private void RefreshHpSegmentMarks(int maxHp)
    {
        if (!hpSegmentMarksEnabled)
        {
            SetHpSegmentMarksActive(false);
            return;
        }

        if (hpFill == null || hpSegmentHpInterval <= 0)
        {
            SetHpSegmentMarksActive(false);
            return;
        }

        EnsureHpSegmentContainer();
        if (_hpSegmentContainer == null)
            return;

        int effectiveInterval = ResolveEffectiveHpSegmentInterval(maxHp);
        if (effectiveInterval <= 0)
        {
            SetHpSegmentMarksActive(false);
            return;
        }

        int markCount = CountHpSegmentMarks(maxHp, effectiveInterval);
        if (markCount > hpSegmentMaxMarkCount)
        {
            SetHpSegmentMarksActive(false);
            return;
        }

        bool layoutChanged = _cachedHpSegmentMaxHp != maxHp || _cachedHpSegmentEffectiveInterval != effectiveInterval;
        if (layoutChanged)
        {
            _cachedHpSegmentMaxHp = maxHp;
            _cachedHpSegmentEffectiveInterval = effectiveInterval;
            while (_hpSegmentLineImages.Count < markCount)
                _hpSegmentLineImages.Add(CreateHpSegmentLine());
        }

        _hpSegmentContainer.gameObject.SetActive(true);

        for (int i = 0; i < _hpSegmentLineImages.Count; i++)
        {
            Image line = _hpSegmentLineImages[i];
            bool active = i < markCount;
            line.gameObject.SetActive(active);
            if (!active)
                continue;

            int hpAt = effectiveInterval * (i + 1);
            float fraction = maxHp > 0 ? hpAt / (float)maxHp : 0f;
            PositionHpSegmentLine(line.rectTransform, _hpSegmentContainer, fraction, hpSegmentLineWidthPx);
        }
    }

    private void SetHpSegmentMarksActive(bool active)
    {
        if (_hpSegmentContainer != null)
            _hpSegmentContainer.gameObject.SetActive(active);

        if (!active)
        {
            for (int i = 0; i < _hpSegmentLineImages.Count; i++)
                _hpSegmentLineImages[i].gameObject.SetActive(false);
        }
    }

    private void EnsureHpSegmentContainer()
    {
        if (_hpSegmentContainer != null)
        {
            PlaceHpSegmentContainerInBarStack();
            return;
        }

        RectTransform fillRt = hpFill.rectTransform;
        RectTransform track = fillRt.parent as RectTransform;
        if (track == null)
            return;

        var go = new GameObject("HpSegmentMarks", typeof(RectTransform), typeof(CanvasRenderer));
        _hpSegmentContainer = go.GetComponent<RectTransform>();
        _hpSegmentContainer.SetParent(track, false);
        _hpSegmentContainer.anchorMin = fillRt.anchorMin;
        _hpSegmentContainer.anchorMax = fillRt.anchorMax;
        _hpSegmentContainer.anchoredPosition = fillRt.anchoredPosition;
        _hpSegmentContainer.sizeDelta = fillRt.sizeDelta;
        _hpSegmentContainer.pivot = fillRt.pivot;
        _hpSegmentContainer.localScale = Vector3.one;

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        PlaceHpSegmentContainerInBarStack();
    }

    /// <summary>Draw ticks on top of the fill, under HP value text and other overlays.</summary>
    private void PlaceHpSegmentContainerInBarStack()
    {
        if (_hpSegmentContainer == null || hpFill == null)
            return;

        int index = hpFill.rectTransform.GetSiblingIndex() + 1;
        if (hpValueText != null)
            index = Mathf.Min(index, hpValueText.rectTransform.GetSiblingIndex());

        _hpSegmentContainer.SetSiblingIndex(index);
    }

    private Image CreateHpSegmentLine()
    {
        var go = new GameObject("Mark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = GetHpSegmentWhiteSprite();
        img.color = hpSegmentLineColor;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;

        RectTransform rt = img.rectTransform;
        rt.SetParent(_hpSegmentContainer, false);
        rt.localScale = Vector3.one;

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        return img;
    }

    private static void PositionHpSegmentLine(
        RectTransform line,
        RectTransform barRect,
        float fraction01,
        float lineWidthPx)
    {
        fraction01 = Mathf.Clamp01(fraction01);

        if (barRect != null && barRect.rect.width > 0.001f)
        {
            Canvas canvas = line.GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            float localX = fraction01 * barRect.rect.width;
            float snappedLocalX = Mathf.Round(localX * scale) / scale;
            fraction01 = Mathf.Clamp01(snappedLocalX / barRect.rect.width);
        }

        int widthPx = Mathf.Max(1, Mathf.RoundToInt(lineWidthPx));

        line.anchorMin = new Vector2(fraction01, 0f);
        line.anchorMax = new Vector2(fraction01, 1f);
        line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(widthPx, 0f);
    }

    private static Sprite GetHpSegmentWhiteSprite()
    {
        if (s_hpSegmentWhiteSprite != null)
            return s_hpSegmentWhiteSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        s_hpSegmentWhiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        return s_hpSegmentWhiteSprite;
    }

    private void ApplyHpFillColorByOwner()
    {
        if (hpFill == null)
            return;

        if (enemy != null)
        {
            hpFill.color = enemyHpFillColor;
            return;
        }

        // Player: tint is driven by ailments in RefreshPlayerOverheadAilmentPresentation().
        if (characterStats != null)
            RefreshPlayerOverheadAilmentPresentation();
    }

    private void ResetPlayerAilmentStatusPopupLatches()
    {
        _wasPoisonForStatusPopup = false;
        _wasBleedForStatusPopup = false;
        _wasBurnForStatusPopup = false;
        _wasShockForStatusPopup = false;
        _wasChillForStatusPopup = false;
    }

    private static void GetAilmentPresentationColors(
        out Color poison,
        out Color bleed,
        out Color burn,
        out Color shock,
        out Color chill)
    {
        poison = new Color32(85, 200, 90, 255);
        bleed = new Color32(170, 35, 35, 255);
        burn = new Color32(255, 140, 40, 255);
        shock = new Color32(255, 190, 70, 255);
        chill = new Color32(90, 160, 255, 255);

        FloatingDamageTextUI p = DamagePopupSystem.Instance != null ? DamagePopupSystem.Instance.PopupPrefab : null;
        if (p == null)
            return;

        poison = p.PoisonDamageColor;
        bleed = p.BleedDamageColor;
        burn = p.BurnPresentationColor;
        shock = p.ShockPresentationColor;
        chill = p.ChillPresentationColor;
    }

    private void RefreshPlayerOverheadAilmentPresentation()
    {
        if (hpFill == null || enemy != null || characterStats == null)
            return;

        if (characterStats.GetComponentInParent<PlayerController>() == null)
            return;

        GetAilmentPresentationColors(
            out Color poisonColor,
            out Color bleedColor,
            out Color burnColor,
            out Color shockColor,
            out Color chillColor);

        Color fill = _playerHpFillBaseCaptured ? _playerHpFillCapturedBase : hpFill.color;
        if (ailments != null)
        {
            if (ailments.HasPoison)
                fill = poisonColor;
            else if (ailments.HasBleed)
                fill = bleedColor;
            else if (ailments.HasBurn)
                fill = burnColor;
            else if (ailments.HasShock)
                fill = shockColor;
            else if (ailments.HasChill)
                fill = chillColor;
        }

        hpFill.color = fill;

        if (ailments == null || DamagePopupSystem.Instance == null)
            return;

        TryPlayPlayerAilmentStatusAcquisition(
            poisonColor,
            bleedColor,
            burnColor,
            shockColor,
            chillColor);
    }

    private static Vector3 ResolvePlayerAilmentStatusPopupWorldPos(
        Vector3 anchorPos,
        PlayerController pc,
        AilmentController ailments,
        string statusMessage)
    {
        if (ailments != null && ailments.TryGetStatusPopupDealerWorld(statusMessage, out Vector3 dealerWorld))
            return DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerWorld);

        if (pc != null)
        {
            PlayerCombatController combat = pc.GetComponent<PlayerCombatController>();
            if (combat != null && combat.TryGetRecentIncomingDamageDealerWorld(8f, out dealerWorld))
                return DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerWorld);
        }

        return anchorPos + Vector3.up * 0.12f;
    }

    private void TryPlayPlayerAilmentStatusAcquisition(
        Color poisonColor,
        Color bleedColor,
        Color burnColor,
        Color shockColor,
        Color chillColor)
    {
        Transform victim = followTarget != null ? followTarget : characterStats.transform;
        PlayerController pc = characterStats.GetComponentInParent<PlayerController>();
        DamagePopupAnchor anchor = victim.GetComponentInChildren<DamagePopupAnchor>(true);
        Vector3 anchorPos = anchor ? anchor.WorldPos : victim.position;

        void SpawnIfAcquired(bool activeNow, ref bool wasActive, string message, Color color)
        {
            if (activeNow && !wasActive)
            {
                Vector3 pos = ResolvePlayerAilmentStatusPopupWorldPos(
                    anchorPos,
                    pc,
                    ailments,
                    message);

                DamagePopupSystem.Instance.SpawnAilmentStatus(pos, message, color, victim);
            }

            wasActive = activeNow;
        }

        SpawnIfAcquired(ailments.HasPoison, ref _wasPoisonForStatusPopup, "Poisoned", poisonColor);
        SpawnIfAcquired(ailments.HasBleed, ref _wasBleedForStatusPopup, "bleeding", bleedColor);
        SpawnIfAcquired(ailments.HasBurn, ref _wasBurnForStatusPopup, "Burnt", burnColor);
        SpawnIfAcquired(ailments.HasShock, ref _wasShockForStatusPopup, "Shocked", shockColor);
        SpawnIfAcquired(ailments.HasChill, ref _wasChillForStatusPopup, "Chilled", chillColor);
    }

    public void RefreshDebuffIcons()
    {
        RefreshPlayerOverheadAilmentPresentation();
        EnsureShadowStrikeMarksSubscription();

        ClearDebuffIcons();

        if (_hpBarOnlyLayout)
            return;

        if (ailments == null || debuffContainer == null || debuffIconPrefab == null)
            return;

        if (ailments.HasBleed)
            SpawnDebuffIcon(bleedIcon, "Bleed", Mathf.Max(1, ailments.BleedStacks));

        if (ailments.HasPoison)
            SpawnDebuffIcon(poisonIcon, "Poison", ailments.PoisonStacks);

        if (ailments.HasBurn)
            SpawnDebuffIcon(burnIcon, "Burn", ailments.BurnStacks);

        if (ailments.HasChill)
            SpawnDebuffIcon(chillIcon, "Chill", ailments.ChillStacks);

        if (ailments.HasShock)
            SpawnDebuffIcon(shockIcon, "Shock", 1);

        EnemyShadowStrikeMarks marks = _shadowStrikeMarks != null
            ? _shadowStrikeMarks
            : enemy != null ? enemy.GetComponent<EnemyShadowStrikeMarks>() : null;
        if (marks != null)
        {
            if (marks.HasLethalCritMark)
                SpawnDebuffIcon(shadowStrikeLethalMarkIcon, "ShadowMarkLethal", 1);
            if (marks.HasExecutionMark)
                SpawnDebuffIcon(shadowStrikeExecutionMarkIcon, "ShadowMarkExecution", 1);
        }
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

        ApplyDebuffIconSize(icon);

        spawnedDebuffIcons.Add(icon);

        foreach (Graphic g in icon.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
    }

    private void ApplyDebuffIconSize(GameObject icon)
    {
        if (icon == null)
            return;

        float s = Mathf.Max(0.05f, debuffIconScale);

        if (Mathf.Approximately(s, 1f))
            icon.transform.localScale = Vector3.one;
        else
            icon.transform.localScale = new Vector3(s, s, 1f);

        Vector2 baseSize = Vector2.zero;
        if (icon.transform is RectTransform rt)
        {
            baseSize = rt.rect.size;
            if (baseSize.x <= 0f && rt.sizeDelta.x > 0f) baseSize.x = rt.sizeDelta.x;
            if (baseSize.y <= 0f && rt.sizeDelta.y > 0f) baseSize.y = rt.sizeDelta.y;
        }

        if (!Mathf.Approximately(s, 1f) && baseSize.x > 0f && baseSize.y > 0f)
        {
            LayoutElement le = icon.GetComponent<LayoutElement>();
            if (le == null)
                le = icon.AddComponent<LayoutElement>();

            le.minWidth = baseSize.x * s;
            le.minHeight = baseSize.y * s;
            le.preferredWidth = baseSize.x * s;
            le.preferredHeight = baseSize.y * s;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
        }

        ApplyDebuffStackPresentation(icon, s);
    }

    private void ApplyDebuffStackPresentation(GameObject icon, float appliedIconUniformScale)
    {
        DebuffIconUI iconUi = icon.GetComponent<DebuffIconUI>();
        if (iconUi == null)
            return;

        iconUi.ApplyOverheadStackPresentation(
            appliedIconUniformScale,
            debuffStackCompensateIconScale,
            debuffStackTextScale,
            debuffStackTextAnchoredPositionOffset);
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

        if (!IsEnemyOverheadTarget(enemy))
            return;

        if (_playerCombatCache == null)
            _playerCombatCache = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Exclude);

        if (_playerCombatCache == null)
            return;

        _playerCombatCache.EngageTargetFromPlayerInput(enemy);
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