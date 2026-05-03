using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Strip dialogue: <see cref="RenderMode.ScreenSpaceOverlay"/> on <c>StripUICanvas</c> (same parent as <see cref="EnemyOverheadUISpawner"/> overheads),
/// screen→local each frame. Optional <see cref="pinDialogueInWorldWhenStripPresent"/> uses a world-space canvas under the NPC instead.
/// </summary>
[DefaultExecutionOrder(120)]
public class NPCDialogueBoxUI : MonoBehaviour
{
    private static NPCDialogueBoxUI _activeBox;
    private static readonly List<NPCDialogueBoxUI> ActiveMultiOfferBoxes = new();
    private static bool BulkClosingMultiOfferGroup;

    private static NPCDialogueBoxUI SpreadTemplate;
    private static Transform SpreadParent;
    private static Transform SpreadAnchor;
    private static Vector3 SpreadBaseOffset;
    private static Func<List<QuestDefinition>> SpreadRefreshQuests;
    private static Func<QuestDefinition, bool> SpreadTryAcceptQuest;
    private static float SpreadAutoCloseSeconds;

    private bool _spawnedAsOfferClone;

    /// <summary>True while this box shows plain NPC lines (not a quest-offer card). Used to stack quest panels beside an open greeting.</summary>
    private bool _plainDialogueMode;

    /// <summary>
    /// When set, invoked once when plain dialogue is hidden (X, auto-close, replaced by another box, or plain host collapsed in quest spread).
    /// Used so death/respawn NPC lines can persist <see cref="NpcPostDeathRespawnDialogueStore"/> until the player dismisses the box, not on first paint.
    /// </summary>
    private Action _plainHideOnceCallback;

    /// <summary>When true, <see cref="CloseAllMultiOfferBoxesTogether"/> removes quest clones but keeps this box open (plain host + quest row).</summary>
    private bool _pinnedPlainHostForQuestSpread;

    /// <summary>Plain host was closed (X / auto-close) while quest cards stay open — layout collapses to quest-only columns until the NPC is clicked again.</summary>
    private bool _plainHostCollapsedLeavingQuestsOpen;

    private Transform _interactionOwnerTransform;

    private Transform _stripFollowAnchor;
    private Vector3 _stripFollowWorldOffset;
    /// <summary>When set (NPC dialogue), pivot is recomputed each frame from collider bounds so parent hover scale cannot move the anchored point.</summary>
    private NPCInteractionSettings _npcStripFollowPivotSource;
    private Vector2 _stripAnchoredSpreadOffset;
    private Canvas _resolvedStripPresentationCanvas;
    /// <summary><see cref="RectTransform"/> passed to Screen→local conversions (strip canvas root).</summary>
    private RectTransform _stripProjectionRectRt;

    /// <summary>Resolved <c>UI_Frame</c>; when non-null, clamps this box inside strip layout (see <see cref="StripUIViewportFollower"/>).</summary>
    private RectTransform _stripUiClampFrameRt;

    private bool _stripWorldFollowActive;

    private static bool s_deferredStripMultiOpening;

    private static RectTransform s_sharedHudBarForFloorClamp;
    private static Camera s_stripCamForHudBarClampSample;

    private static readonly Vector3[] sHudClampCornerScratch = new Vector3[4];

    /// <summary>True when this instance is the active dialogue and is nested under <paramref name="ancestor"/> (e.g. NPC hover scale should not move the box).</summary>
    public static bool ActiveDialogueIsDescendantOf(Transform ancestor)
    {
        if (!ancestor)
            return false;

        static bool Matches(NPCDialogueBoxUI box, Transform anc)
        {
            if (box == null || !box.gameObject.activeInHierarchy)
                return false;

            if (box.transform.IsChildOf(anc))
                return true;

            Transform owner = box._interactionOwnerTransform;
            return owner && (owner == anc || owner.IsChildOf(anc));
        }

        if (_activeBox != null && Matches(_activeBox, ancestor))
            return true;

        for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
        {
            if (Matches(ActiveMultiOfferBoxes[i], ancestor))
                return true;
        }

        return false;
    }

    /// <summary>True when this instance is showing plain dialogue (eligible to stack quest offer cards beside it on re-click).</summary>
    public static bool IsEligiblePlainHostForStackedQuestOffers(NPCDialogueBoxUI box) =>
        box &&
        box.gameObject.activeInHierarchy &&
        box._plainDialogueMode &&
        (!ActiveMultiOfferBoxes.Contains(box) || box._pinnedPlainHostForQuestSpread);

    /// <summary>True when this NPC still has an active plain+quest spread session (expanded or collapsed dialogue host).</summary>
    public static bool HasPinnedPlainQuestSpreadForNpc(Transform npcRoot)
    {
        if (!npcRoot || SpreadTemplate == null)
            return false;
        if (!SpreadTemplate._pinnedPlainHostForQuestSpread)
            return false;
        return ActiveDialogueIsDescendantOf(npcRoot);
    }

    /// <summary>Plain dialogue panel is hidden but quest offer clones from the same session are still open.</summary>
    public static bool IsPlainHostCollapsedForActiveSpread() =>
        SpreadTemplate && SpreadTemplate._plainHostCollapsedLeavingQuestsOpen;

    /// <summary>Restores the plain dialogue column beside existing quest cards after the host was collapsed.</summary>
    public static bool TryReshowCollapsedPlainDialogueForNpc(Transform npcRoot)
    {
        if (!npcRoot || SpreadTemplate == null || !SpreadTemplate._plainHostCollapsedLeavingQuestsOpen)
            return false;
        if (!SpreadTemplate._pinnedPlainHostForQuestSpread)
            return false;
        if (!ActiveDialogueIsDescendantOf(npcRoot))
            return false;

        SpreadTemplate._plainHostCollapsedLeavingQuestsOpen = false;
        if (!SpreadTemplate.gameObject.activeSelf)
            SpreadTemplate.gameObject.SetActive(true);

        ActiveMultiOfferBoxes.Add(SpreadTemplate);
        SortActiveMultiOfferBoxesLeftToRight();

        float step = SpreadTemplate.fixedSize.x + SpreadTemplate.questOfferCardSpacing;
        SpreadTemplate._stripAnchoredSpreadOffset = Vector2.zero;

        int col = 0;
        for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
        {
            NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
            if (!b || ReferenceEquals(b, SpreadTemplate))
                continue;
            if (b._spawnedAsOfferClone)
            {
                col++;
                b._stripAnchoredSpreadOffset = new Vector2(step * col, 0f);
            }
        }

        SortActiveMultiOfferBoxesLeftToRight();
        s_deferredStripMultiOpening = true;
        _activeBox = SpreadTemplate;
        return true;
    }

    [Header("Layout")]
    [SerializeField] private Vector2 fixedSize = new(250f, 250f);
    [SerializeField] private float worldScale = 0.015f;
    [SerializeField, Range(0f, 0.1f)] private float viewportPadding = 0.02f;

    [Header("Multi-quest offers")]
    [SerializeField] private float questOfferCardSpacing = 10f;

    [Tooltip("Shown above the quest name when this box is used as a quest offer (plain NPC dialogue hides this row).")]
    [SerializeField] private string questDialogueHeaderText = "Available Quest";

    [Header("Typewriter")]
    [Tooltip("How fast body / quest description reveals (visible characters per second). Uses full-paragraph layout while typing. Higher = snappier. 0 = one character per frame.")]
    [SerializeField] private float typewriterCharactersPerSecond = 48f;

    [Header("Optional refs")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button acceptButton;

    [Header("Strip UI")]
    [Tooltip(
        "If on, parents under the NPC with a World Space Canvas (strip camera). If off (default), parents under StripUICanvas like overhead HP bars — readable at all zoom levels and draws on top via sibling order.")]
    [SerializeField] private bool pinDialogueInWorldWhenStripPresent = false;

    [Tooltip("World-space path only: sorting order on the dialogue Canvas.")]
    [SerializeField] private int stripWorldDialogueSortingOrder = 800;

    [Tooltip("World-space path only: sorting layer name, or empty for default.")]
    [SerializeField] private string stripWorldDialogueSortingLayer = "";

    [Tooltip(
        "If off, uses Camera.ViewportToScreenPoint with viewport XY clamped to [0,1]. If on, clamped viewport × Camera.pixelRect.")]
    [SerializeField] private bool deriveScreenViaViewportTimesPixelRect = false;

    [Tooltip(
        "Sample the bottom HUD bar top in screen space (same source as WorldFloorToUIEdge) so dialogue cannot sit over the desktop bar across zoom/layout.")]
    [SerializeField] private bool clampAboveBottomHudBar = true;

    [Tooltip("Added above the HUD top edge in screen pixels (parity with WorldFloorToUIEdge source pixel offset ~6).")]
    [SerializeField, Min(0f)] private float bottomHudClearanceScreenPx = 6f;

    [Tooltip("Optional. If unset, resolves from WorldFloorToUIEdge.HudBarRect or a GameObject named BotomGameBar.")]
    [SerializeField] private RectTransform bottomHudBarOverride;

    [Header("Debug (dialogue anchoredPosition)")]
    [SerializeField] private bool logStripPresentationDiagnostics;
    [SerializeField, Min(1)] private int diagnosticsLogThrottleFrames = 45;

    private Action _onAccept;
    private RectTransform _rectTransform;
    private Coroutine _autoCloseRoutine;

    private GameObject _singleModeScrollRoot;
    private TMP_Text _questOfferHeaderText;
    private TMP_Text _singleTitleText;
    private TMP_Text _singleRewardText;

    private sealed class ActiveTypewriter
    {
        public TMP_Text Tmp;
        public string FullPlain;
        public Coroutine Co;
        public Action OnComplete;
    }

    private readonly List<ActiveTypewriter> _activeTypewriters = new();

    private void Awake()
    {
        EnsureBuilt();
        gameObject.SetActive(false);
    }

    /// <summary>Shows remaining typewriter text immediately (click anywhere on the box).</summary>
    public void CompleteAllTypewriters()
    {
        for (int i = 0; i < _activeTypewriters.Count; i++)
        {
            ActiveTypewriter a = _activeTypewriters[i];
            if (a.Co != null)
                StopCoroutine(a.Co);
            if (a.Tmp)
            {
                DialogueTextTypewriter.RestoreFullReveal(a.Tmp);
                a.Tmp.text = a.FullPlain;
            }
            a.OnComplete?.Invoke();
        }

        _activeTypewriters.Clear();
    }

    private void AttachClickForward(GameObject go)
    {
        if (!go)
            return;
        NpcDialogueTypewriterClickForward f = go.GetComponent<NpcDialogueTypewriterClickForward>();
        if (!f)
            f = go.AddComponent<NpcDialogueTypewriterClickForward>();
        f.Init(this);
    }

    private void StartTypewriter(TMP_Text tmp, string plainFull, Action onComplete = null)
    {
        if (!tmp)
        {
            onComplete?.Invoke();
            return;
        }

        plainFull ??= "";
        if (plainFull.Length == 0)
        {
            DialogueTextTypewriter.RestoreFullReveal(tmp);
            tmp.text = "";
            onComplete?.Invoke();
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            DialogueTextTypewriter.RestoreFullReveal(tmp);
            tmp.text = plainFull;
            onComplete?.Invoke();
            return;
        }

        DialogueTextTypewriter.RestoreFullReveal(tmp);
        tmp.text = "";
        var entry = new ActiveTypewriter { Tmp = tmp, FullPlain = plainFull, OnComplete = onComplete };
        entry.Co = StartCoroutine(RunTypewriter(entry));
        _activeTypewriters.Add(entry);
    }

    private IEnumerator RunTypewriter(ActiveTypewriter entry)
    {
        yield return DialogueTextTypewriter.RevealFlowingCharacters(entry.Tmp, entry.FullPlain, typewriterCharactersPerSecond);

        Action done = entry.OnComplete;
        _activeTypewriters.Remove(entry);
        done?.Invoke();
    }

    private void ApplyCommonShowTransforms(
        Transform interactionOwner,
        Transform anchor,
        Vector3 worldOffsetFromAnchor)
    {
        if (!interactionOwner)
            return;

        _interactionOwnerTransform = interactionOwner;
        _npcStripFollowPivotSource = interactionOwner
            ? interactionOwner.GetComponent<NPCInteractionSettings>()
            : null;

        Canvas strip = ResolveStripOverlayCanvas();
        if (!strip)
        {
            TeardownNpcDialogueRootCanvasComponents();
            _stripWorldFollowActive = false;
            transform.SetParent(interactionOwner, false);
            if (_npcStripFollowPivotSource)
                transform.position = _npcStripFollowPivotSource.GetDialogueFollowWorldPoint();
            else
            {
                Vector3 anchorWorld = anchor ? anchor.position : interactionOwner.position;
                transform.position = anchorWorld + worldOffsetFromAnchor;
            }
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * worldScale;
            _stripFollowAnchor = null;
            _resolvedStripPresentationCanvas = null;
            _stripProjectionRectRt = null;
            _stripUiClampFrameRt = null;
            return;
        }

        Camera stripGameplayCam = ResolveStripGameplayCamera(strip);

        // Default: World Space Canvas under the NPC — same idea as strip-off mode, drawn by StripCamera — no HUD/overlay pixel remap.
        if (pinDialogueInWorldWhenStripPresent && stripGameplayCam)
        {
            TeardownNpcDialogueRootCanvasComponents();

            _stripWorldFollowActive = true;
            _resolvedStripPresentationCanvas = strip;
            _stripProjectionRectRt = null;
            _stripUiClampFrameRt = null;
            _stripFollowAnchor = anchor ? anchor : interactionOwner;
            _stripFollowWorldOffset = worldOffsetFromAnchor;

            transform.SetParent(interactionOwner, false);
            ConfigureStripWorldFollowCanvas(stripGameplayCam);
            _rectTransform.anchorMin = _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ApplyStripWorldFollowScale();
            RefreshStripWorldFollowWorldPosition();

            transform.SetAsLastSibling();
            return;
        }

        _stripWorldFollowActive = false;
        TeardownNpcDialogueRootCanvasComponents();

        _resolvedStripPresentationCanvas = strip;
        RectTransform canvasRt = strip.transform as RectTransform;
        _stripFollowAnchor = anchor ? anchor : interactionOwner;
        _stripFollowWorldOffset = worldOffsetFromAnchor;

        // Canvas root projection + sibling order match EnemyOverheadUISpawner; UI_Frame clamps layout to strip Camera.rect band.
        _stripProjectionRectRt = canvasRt;
        _stripUiClampFrameRt = TryResolveViewportAlignedFrame(strip);
        transform.SetParent(canvasRt, false);
        transform.localRotation = Quaternion.identity;
        ApplyStripPresentationLocalScale();

        _rectTransform.anchorMin = _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        // Strip position + clamps run after activation and content/layout in FinalizeStripOverlayOpen (same frame/strip order as LateUpdate).
    }

    /// <summary>
    /// Runs after <see cref="StripUIViewportFollower"/> would have aligned UI_Frame — avoids clamping twice with stale anchors (open jitter).
    /// </summary>
    private void SyncStripViewportFollowerImmediate()
    {
        if (_stripWorldFollowActive || !_resolvedStripPresentationCanvas || !_stripProjectionRectRt)
            return;

        StripUIViewportFollower follower =
            _resolvedStripPresentationCanvas.GetComponentInChildren<StripUIViewportFollower>(true);
        follower?.ForceApplyViewportAnchorsNow();
    }

    private void FinalizeStripOverlayOpen(bool clampToUiFrameBand, bool syncViewportAnchorsFirst)
    {
        if (_stripWorldFollowActive || _stripProjectionRectRt == null || _rectTransform == null)
            return;

        if (syncViewportAnchorsFirst)
            SyncStripViewportFollowerImmediate();

        Canvas.ForceUpdateCanvases();

        ApplyStripPresentationLocalScale();
        UpdateStripPresentationTransform();
        transform.SetAsLastSibling();

        if (clampToUiFrameBand && _stripUiClampFrameRt)
            ClampAnchoredRectInsideUiFrame();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (_stripWorldFollowActive && _stripFollowAnchor)
        {
            RefreshStripWorldFollowWorldPosition();
            ApplyStripWorldFollowScale();
            return;
        }

        if (_stripFollowAnchor == null || _stripProjectionRectRt == null)
            return;

        // Multi-offer overlay row: one driver updates every box using the same screen→anchor math so horizontal
        // spread stays fixed; group UI_Frame clamp applies one delta so cards do not stack.
        if (ActiveMultiOfferBoxes.Count > 1 && ActiveMultiOfferBoxes.Contains(this))
        {
            NPCDialogueBoxUI stripDriver = ResolveMultiOfferStripDriver();
            if (!ReferenceEquals(this, stripDriver))
                return;

            // First LateUpdate after open: lane + UI_Frame are already synced (follower 110 < this 120).
            if (s_deferredStripMultiOpening)
            {
                s_deferredStripMultiOpening = false;
                SyncStripViewportFollowerImmediate();
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
                {
                    NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
                    if (b && b.isActiveAndEnabled && b._stripProjectionRectRt)
                        b.RefreshStripOverlayLayoutForFrame();
                }

                SortActiveMultiOfferBoxesLeftToRight();
                for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
                {
                    NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
                    if (b)
                        b.transform.SetAsLastSibling();
                }

                ClampMultiOfferBoxesToViewport();
                return;
            }

            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
                if (b && b.isActiveAndEnabled && b._stripProjectionRectRt)
                    b.RefreshStripOverlayLayoutForFrame();
            }

            SortActiveMultiOfferBoxesLeftToRight();
            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
                if (b)
                    b.transform.SetAsLastSibling();
            }

            ClampMultiOfferRowInsideUiFrame();
            return;
        }

        ApplyStripPresentationLocalScale();

        UpdateStripPresentationTransform();
        transform.SetAsLastSibling();
        ClampAnchoredRectInsideUiFrame();
    }

    private static NPCDialogueBoxUI ResolveMultiOfferStripDriver()
    {
        if (SpreadTemplate != null &&
            SpreadTemplate._pinnedPlainHostForQuestSpread &&
            SpreadTemplate._plainHostCollapsedLeavingQuestsOpen)
        {
            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
                if (b && b._spawnedAsOfferClone)
                    return b;
            }
        }

        return SpreadTemplate;
    }

    private void RefreshStripOverlayLayoutForFrame()
    {
        ApplyStripPresentationLocalScale();
        UpdateStripPresentationTransform();
    }

    private void UpdateStripPresentationTransform()
    {
        if (_stripFollowAnchor == null || _stripProjectionRectRt == null || _rectTransform == null)
            return;

        Vector3 worldPt = ResolveStripFollowWorldPoint();
        Camera cam = ResolveWorldToScreenCamera();
        if (!cam)
            return;

        Vector3 vpRaw = cam.WorldToViewportPoint(worldPt);
        if (cam.orthographic ? vpRaw.z < 0f : vpRaw.z <= 0f)
            return;

        // Keep screen pixels inside Camera.pixelRect — exclamation / collider top can sit above ortho frustum (vp.y > 1).
        Vector3 vpClamp = new Vector3(Mathf.Clamp01(vpRaw.x), Mathf.Clamp01(vpRaw.y), vpRaw.z);

        bool usedVpTimesPr = deriveScreenViaViewportTimesPixelRect;
        Vector2 screenPx;
        if (usedVpTimesPr)
        {
            if (!TryClampedViewportTimesPixelRect(cam, vpClamp, out screenPx))
                return;
        }
        else
        {
            Vector3 ss = cam.ViewportToScreenPoint(vpClamp);
            screenPx = new Vector2(ss.x, ss.y);
        }

        Canvas canvas = _resolvedStripPresentationCanvas;
        Camera screenToLocalCam =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? cam : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _stripProjectionRectRt,
            screenPx,
            screenToLocalCam,
            out Vector2 local);

        _rectTransform.anchoredPosition = local + _stripAnchoredSpreadOffset;

        if (logStripPresentationDiagnostics && diagnosticsLogThrottleFrames > 0 &&
            Time.frameCount % diagnosticsLogThrottleFrames == 0)
            LogStripPresentationDiagnostics(cam, worldPt, vpRaw, vpClamp, screenPx, usedVpTimesPr, screenToLocalCam, local);
    }

    /// <summary>
    /// Same idea as <see cref="UnitOverheadUI.ApplyCombinedRootScale"/> / <see cref="OffscreenMarkersController.ApplyMarkersIndependentOfHudResize"/>:
    /// <see cref="RuntimeCanvasScaleController"/> boosts the strip canvas via <see cref="SliderSettingId.HudResize"/>; dividing keeps this box’s authored <see cref="worldScale"/>
    /// visually stable and aligned with overhead screen→local math.
    /// </summary>
    private void ApplyStripPresentationLocalScale()
    {
        float hud = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.HudResize));
        float overheadBar = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.OverheadHpBarResize));
        float s = Mathf.Max(0.01f, worldScale / 0.015f) * overheadBar / hud;

        transform.localScale = Vector3.one * s;
    }

    /// <summary>Clamped viewport (0–1) → screen pixels inside <see cref="Camera.pixelRect"/>.</summary>
    private static bool TryClampedViewportTimesPixelRect(Camera cam, Vector3 vpClamped, out Vector2 screenPx)
    {
        screenPx = default;
        if (!cam)
            return false;

        Rect pr = cam.pixelRect;
        if (pr.width > 1f && pr.height > 1f)
        {
            screenPx.x = pr.xMin + vpClamped.x * pr.width;
            screenPx.y = pr.yMin + vpClamped.y * pr.height;
            return true;
        }

        Vector3 ss = cam.ViewportToScreenPoint(vpClamped);
        screenPx = new Vector2(ss.x, ss.y);
        return true;
    }

    private void LogStripPresentationDiagnostics(
        Camera cam,
        Vector3 worldPt,
        Vector3 viewportRaw,
        Vector3 viewportClamped,
        Vector2 screenPxUsed,
        bool usedVpTimesPixelRectProjection,
        Camera screenToLocalCamera,
        Vector2 anchoredResult)
    {
        Vector3 wsFallback = cam.WorldToScreenPoint(worldPt);
        Vector2 screenVpPr = Vector2.zero;
        bool vpPrOk = TryClampedViewportTimesPixelRect(cam, viewportClamped, out screenVpPr);

        Canvas canvasDiag = _resolvedStripPresentationCanvas;
        string canvasMode =
            canvasDiag == null ? "none" :
            canvasDiag.renderMode == RenderMode.ScreenSpaceOverlay ? "Overlay" : canvasDiag.renderMode.ToString();

        string projRtName =
            _stripProjectionRectRt ? _stripProjectionRectRt.name : "?";
        string hierarchyParentName = transform.parent ? transform.parent.name : "?";
        Vector3 ls = _stripProjectionRectRt ? _stripProjectionRectRt.lossyScale : default;
        StripCameraController stripCtl =
            FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
        float pct = stripCtl != null && stripCtl.DefaultOrthoBaseline > 0.01f
            ? Mathf.Round(stripCtl.GetComponent<Camera>().orthographicSize / stripCtl.DefaultOrthoBaseline * 1000f)
            / 10f : -1f;

        float hud = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.HudResize));
        float ohBar = Mathf.Max(0.05f, SliderSettingsStore.Get(SliderSettingId.OverheadHpBarResize));

        Debug.Log(
            $"[NPCDialogueBoxUI] worldFollow={_stripWorldFollowActive} projRt='{projRtName}' hierarchyParent='{hierarchyParentName}' projRt.lossyScale=({ls.x:F5},{ls.y:F5},{ls.z:F5}) " +
            $"hudResize×={hud:F3} overheadBar×={ohBar:F3} boxScale={transform.localScale.x:F4} " +
            $"cam='{cam.name}' tag={cam.tag} rect={cam.rect} pixelRect={cam.pixelRect} orthoSize={cam.orthographicSize:F3} " +
            $"zoomVsPrefab~{pct:F1}% canvas={canvasMode} " +
            $"worldPt={worldPt} vpRaw={viewportRaw} vpClamp={viewportClamped} screenUsed=({screenPxUsed.x:F1},{screenPxUsed.y:F1}) via={(usedVpTimesPixelRectProjection ? "clamp×pixelRect" : "Viewport→Screen clamped")} " +
            $"WorldToScreenAlt=({wsFallback.x:F1},{wsFallback.y:F1}) vpPrAltOk={vpPrOk} vpPrAlt=({screenVpPr.x:F1},{screenVpPr.y:F1}) " +
            $"ScreenToLocalCam={(screenToLocalCamera ? screenToLocalCamera.name : "null")} anchored=({anchoredResult.x:F1},{anchoredResult.y:F1}) " +
            $"anchor='{(_stripFollowAnchor ? _stripFollowAnchor.name : "?")}'",
            this);
    }


    /// <summary>Gameplay camera used to draw lane + world-space HUD (StripCamera via <see cref="Camera.main"/> when overlay).</summary>
    private Camera ResolveStripGameplayCamera(Canvas stripUi)
    {
        if (stripUi != null && stripUi.renderMode == RenderMode.ScreenSpaceCamera && stripUi.worldCamera != null &&
            stripUi.worldCamera.isActiveAndEnabled)
            return stripUi.worldCamera;

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        return FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private void TeardownNpcDialogueRootCanvasComponents()
    {
        GraphicRaycaster[] rays = GetComponents<GraphicRaycaster>();
        for (int i = rays.Length - 1; i >= 0; i--)
        {
            if (rays[i])
                DestroyImmediate(rays[i]);
        }

        Canvas[] canvases = GetComponents<Canvas>();
        for (int i = canvases.Length - 1; i >= 0; i--)
        {
            if (canvases[i])
                DestroyImmediate(canvases[i]);
        }
    }

    private void ConfigureStripWorldFollowCanvas(Camera worldCam)
    {
        if (!worldCam)
            return;

        Canvas c = GetComponent<Canvas>();
        if (!c)
            c = gameObject.AddComponent<Canvas>();
        if (!c)
            return;

        c.renderMode = RenderMode.WorldSpace;
        c.worldCamera = worldCam;
        c.overrideSorting = true;
        c.sortingOrder = stripWorldDialogueSortingOrder;
        if (!string.IsNullOrWhiteSpace(stripWorldDialogueSortingLayer))
        {
            int lid = SortingLayer.NameToID(stripWorldDialogueSortingLayer);
            if (lid >= 0)
                c.sortingLayerID = lid;
        }

        if (!GetComponent<GraphicRaycaster>())
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private Vector3 ResolveStripFollowWorldPoint()
    {
        if (_npcStripFollowPivotSource)
            return _npcStripFollowPivotSource.GetDialogueFollowWorldPoint();
        return _stripFollowAnchor.position + _stripFollowWorldOffset;
    }

    private void RefreshStripWorldFollowWorldPosition()
    {
        if (_stripFollowAnchor == null || _rectTransform == null)
            return;

        transform.position = ResolveStripFollowWorldPoint();
        transform.rotation = Quaternion.identity;
    }

    private void ApplyStripWorldFollowScale()
    {
        float s = Mathf.Max(0.0001f, worldScale);
        transform.localScale = Vector3.one * s;
    }

    private Camera ResolveWorldToScreenCamera()
    {
        // Mirror EnemyOverheadUISpawner.ResolveStripContext so World→screen shares the overhead pixel basis.
        Canvas c = _resolvedStripPresentationCanvas;
        if (!c && _stripProjectionRectRt != null)
            _resolvedStripPresentationCanvas = c = _stripProjectionRectRt.GetComponentInParent<Canvas>();

        if (c != null)
        {
            if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null &&
                c.worldCamera.isActiveAndEnabled)
                return c.worldCamera;

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;

            return FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        }

        return Camera.main != null && Camera.main.isActiveAndEnabled
            ? Camera.main
            : FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
    }

    private static Canvas ResolveStripOverlayCanvas()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("UICanvas");
        if (tagged)
        {
            Canvas c = tagged.GetComponent<Canvas>();
            if (c)
                return c;
        }

        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.hideFlags != HideFlags.None || !canvas.gameObject.scene.IsValid())
                continue;

            if (canvas.CompareTag("UICanvas") || string.Equals(canvas.name, "StripUICanvas", StringComparison.Ordinal))
                return canvas;
        }

        return null;
    }

    /// <summary>Strip <c>UI_Frame</c> tracked by <see cref="StripUIViewportFollower"/> (strip <see cref="Camera.rect"/> band).</summary>
    private static RectTransform TryResolveViewportAlignedFrame(Canvas strip)
    {
        if (!strip)
            return null;

        StripUIViewportFollower follower = strip.GetComponentInChildren<StripUIViewportFollower>(true);
        RectTransform frameRt = follower != null ? follower.ViewportAlignedRect : null;
        if (frameRt)
            return frameRt;

        Transform named = strip.transform.Find("UI_Frame");
        return named ? named as RectTransform : null;
    }

    private static void InvalidateSharedHudBarClampRefsIfDestroyed()
    {
        if (!s_sharedHudBarForFloorClamp || !s_sharedHudBarForFloorClamp.gameObject)
            s_sharedHudBarForFloorClamp = null;

        if (!s_stripCamForHudBarClampSample)
            s_stripCamForHudBarClampSample = null;
    }

    private static void EnsureSharedHudBarClampReferences()
    {
        InvalidateSharedHudBarClampRefsIfDestroyed();

        bool haveBar = s_sharedHudBarForFloorClamp;
        bool haveCam = s_stripCamForHudBarClampSample && s_stripCamForHudBarClampSample.isActiveAndEnabled;
        if (haveBar && haveCam)
            return;

        s_sharedHudBarForFloorClamp = null;
        s_stripCamForHudBarClampSample = null;

        WorldFloorToUIEdge[] edges =
            FindObjectsByType<WorldFloorToUIEdge>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < edges.Length; i++)
        {
            WorldFloorToUIEdge e = edges[i];
            if (!e || !e.HudBarRect)
                continue;

            s_sharedHudBarForFloorClamp = e.HudBarRect;
            if (e.AlignmentStripCamera && e.AlignmentStripCamera.isActiveAndEnabled)
                s_stripCamForHudBarClampSample = e.AlignmentStripCamera;
            break;
        }

        if (!s_stripCamForHudBarClampSample)
        {
            StripCameraController ctl = FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
            if (ctl && ctl.TryGetComponent(out Camera cam))
                s_stripCamForHudBarClampSample = cam;
        }

        if (!s_sharedHudBarForFloorClamp)
        {
            GameObject named = GameObject.Find("BotomGameBar");
            if (named)
                s_sharedHudBarForFloorClamp = named.transform as RectTransform;
        }
    }

    private static Camera ResolveStripCameraFromController()
    {
        StripCameraController ctl = FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Exclude);
        return ctl ? ctl.GetComponent<Camera>() : null;
    }

    private RectTransform ResolveHudBarRectForClamp()
    {
        if (bottomHudBarOverride)
            return bottomHudBarOverride;

        EnsureSharedHudBarClampReferences();
        return s_sharedHudBarForFloorClamp;
    }

    /// <summary>WorldFloorToUIEdge parity: top of bottom HUD in screen space → strip-canvas local Y floor for dialogue.</summary>
    private bool TryGetBottomHudTopAsMinLocalY(RectTransform projectionRoot, out float minLocalY)
    {
        minLocalY = float.NegativeInfinity;
        if (!clampAboveBottomHudBar || !projectionRoot)
            return false;

        RectTransform hudBar = ResolveHudBarRectForClamp();
        if (!hudBar)
            return false;

        EnsureSharedHudBarClampReferences();

        Camera stripCam = s_stripCamForHudBarClampSample && s_stripCamForHudBarClampSample.isActiveAndEnabled
            ? s_stripCamForHudBarClampSample
            : null;
        if (!stripCam)
            stripCam = ResolveStripCameraFromController();

        hudBar.GetWorldCorners(sHudClampCornerScratch);

        float screenTopY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, sHudClampCornerScratch[i]);
            screenTopY = Mathf.Max(screenTopY, sp.y);
        }

        screenTopY = Mathf.Round(screenTopY + bottomHudClearanceScreenPx);

        float screenX;
        if (stripCam != null && stripCam.pixelRect.width > 1e-3f)
        {
            Rect pr = stripCam.pixelRect;
            screenX = Mathf.Clamp(pr.center.x, pr.xMin, pr.xMax);
        }
        else
            screenX = Screen.width * 0.5f;

        Canvas projCanvas = projectionRoot.GetComponentInParent<Canvas>();
        Camera overlayEventCam =
            projCanvas != null && projCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? projCanvas.worldCamera
                : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                projectionRoot, new Vector2(screenX, screenTopY), overlayEventCam, out Vector2 local))
            return false;

        minLocalY = local.y;
        return true;
    }

    private static NPCDialogueBoxUI PickHudClampSettingsSourceForMultiSpread()
    {
        if (SpreadTemplate)
            return SpreadTemplate;

        return ActiveMultiOfferBoxes.Count > 0 ? ActiveMultiOfferBoxes[0] : null;
    }

    private static bool TryGetBottomHudTopAsMinLocalYStatic(RectTransform projectionRoot, out float minLocalY)
    {
        minLocalY = float.NegativeInfinity;
        NPCDialogueBoxUI src = PickHudClampSettingsSourceForMultiSpread();
        return src != null && src.TryGetBottomHudTopAsMinLocalY(projectionRoot, out minLocalY);
    }

    /// <summary>
    /// Viewport deltas (<see cref="Camera.WorldToViewportPoint"/> on clamp corners) → anchored deltas in the strip projection rect,
    /// same projection basis as <see cref="UnitOverheadUI"/> / <see cref="UpdateStripPresentationTransform"/>.
    /// </summary>
    private Vector2 ViewportDeltaToAnchoredDeltaInStripParent(
        Camera cam,
        float dxViewport,
        float dyViewport)
    {
        Rect pr = cam.pixelRect;
        Vector2 screenDelta = new Vector2(dxViewport * pr.width, dyViewport * pr.height);
        Vector2 refScreen = new Vector2(pr.xMin + pr.width * 0.5f, pr.yMin + pr.height * 0.5f);

        Camera eventCam =
            _resolvedStripPresentationCanvas != null &&
            _resolvedStripPresentationCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cam : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _stripProjectionRectRt, refScreen, eventCam, out Vector2 lf0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _stripProjectionRectRt, refScreen + screenDelta, eventCam, out Vector2 lf1);

        return lf1 - lf0;
    }

    private static Vector2 ViewportDeltaToAnchoredDeltaInStripParentStatic(
        RectTransform stripParentRt,
        Canvas stripCanvas,
        Camera cam,
        float dxViewport,
        float dyViewport)
    {
        Rect pr = cam.pixelRect;
        Vector2 screenDelta = new Vector2(dxViewport * pr.width, dyViewport * pr.height);
        Vector2 refScreen = new Vector2(pr.xMin + pr.width * 0.5f, pr.yMin + pr.height * 0.5f);

        Camera eventCam =
            stripCanvas && stripCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cam : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            stripParentRt, refScreen, eventCam, out Vector2 lf0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            stripParentRt, refScreen + screenDelta, eventCam, out Vector2 lf1);

        return lf1 - lf0;
    }

    public void Show(Transform owner, Vector3 localOffset, string message, bool showAccept, Action onAccept, float autoCloseSeconds = 0f)
    {
        ShowAt(owner, owner, localOffset, message, showAccept, onAccept, autoCloseSeconds);
    }

    public void ShowAt(Transform parent, Transform anchor, Vector3 localOffset, string message, bool showAccept, Action onAccept, float autoCloseSeconds = 0f)
    {
        EnsureBuilt();
        CloseAllMultiOfferBoxesTogether();
        CompleteAllTypewriters();
        CleanupMultiOfferUi();
        SetOfferModeMulti(false);

        if (_activeBox != null && _activeBox != this)
            _activeBox.Hide(suppressPlainDismissCallback: true);
        _activeBox = this;

        _stripAnchoredSpreadOffset = Vector2.zero;

        if (parent)
            ApplyCommonShowTransforms(parent, anchor, localOffset);

        if (_rectTransform)
            _rectTransform.sizeDelta = fixedSize;

        gameObject.SetActive(true);

        ConfigureSingleScrollTextsForPlainDialogue();
        StartTypewriter(dialogueText, message ?? "");
        _plainDialogueMode = true;

        _onAccept = onAccept;
        if (acceptButton)
        {
            acceptButton.gameObject.SetActive(showAccept);
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);
            acceptButton.onClick.AddListener(HandleAcceptClicked);
        }

        if (!_stripWorldFollowActive && _stripProjectionRectRt != null)
            FinalizeStripOverlayOpen(clampToUiFrameBand: true, syncViewportAnchorsFirst: true);
        else
            ClampInsideScreen();

        StartAutoClose(autoCloseSeconds);
    }

    /// <summary>
    /// Keeps this plain-dialogue box open and opens quest offer clone(s) to the right, using the same horizontal spread as multi-quest.
    /// </summary>
    public void StackQuestOffersBesidePlainDialogue(
        List<QuestDefinition> quests,
        Func<List<QuestDefinition>> refreshQuests,
        Func<QuestDefinition, bool> tryAcceptQuest,
        float autoCloseSeconds)
    {
        if (quests == null || quests.Count == 0 || !gameObject.activeSelf)
            return;

        EnsureBuilt();

        CloseAllMultiOfferBoxesTogether();

        SpreadTemplate = this;
        SpreadParent = _interactionOwnerTransform ? _interactionOwnerTransform : transform.parent;
        SpreadAnchor = _stripFollowAnchor ? _stripFollowAnchor : SpreadParent;
        SpreadBaseOffset = _stripFollowWorldOffset;
        SpreadRefreshQuests = refreshQuests;
        SpreadTryAcceptQuest = tryAcceptQuest;
        SpreadAutoCloseSeconds = autoCloseSeconds;

        ActiveMultiOfferBoxes.Clear();

        _stripAnchoredSpreadOffset = Vector2.zero;
        _pinnedPlainHostForQuestSpread = true;
        _plainHostCollapsedLeavingQuestsOpen = false;
        ActiveMultiOfferBoxes.Add(this);

        float spreadStepPx = fixedSize.x + questOfferCardSpacing;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinition q = quests[i];
            NPCDialogueBoxUI inst = Instantiate(gameObject).GetComponent<NPCDialogueBoxUI>();
            inst._spawnedAsOfferClone = true;
            ActiveMultiOfferBoxes.Add(inst);

            QuestDefinition captured = q;
            inst.ShowQuestOfferSingle(
                SpreadParent,
                SpreadAnchor,
                SpreadBaseOffset,
                captured,
                showAccept: true,
                () => HandleSpreadQuestAccepted(captured),
                autoCloseSeconds,
                partOfMultiSpread: true,
                stripAnchoredSpreadOffset: new Vector2(spreadStepPx * (i + 1), 0f));
        }

        s_deferredStripMultiOpening = true;

        SortActiveMultiOfferBoxesLeftToRight();
    }

    /// <param name="suppressPlainDismissCallback">
    /// When true, the plain-dialogue hide-once callback (e.g. post-death NPC save) is not invoked — use when this box
    /// is hidden because another <see cref="NPCDialogueBoxUI"/> replaced <see cref="_activeBox"/>, not because the player dismissed it.
    /// </param>
    public void Hide(bool suppressPlainDismissCallback = false)
    {
        if (!BulkClosingMultiOfferGroup && ActiveMultiOfferBoxes.Contains(this))
        {
            if (_pinnedPlainHostForQuestSpread &&
                ReferenceEquals(this, SpreadTemplate) &&
                ActiveMultiOfferBoxes.Count > 1)
            {
                CollapsePlainHostLeavingQuestOffers();
                return;
            }

            bool dismissPlainHostEntirely =
                _pinnedPlainHostForQuestSpread &&
                ReferenceEquals(this, SpreadTemplate);

            CloseAllMultiOfferBoxesTogether();

            if (dismissPlainHostEntirely)
                HideSolo(invokePlainDismissCallback: !suppressPlainDismissCallback);

            return;
        }

        HideSolo(invokePlainDismissCallback: !suppressPlainDismissCallback);
    }

    private void CollapsePlainHostLeavingQuestOffers()
    {
        if (!ReferenceEquals(this, SpreadTemplate) || !_pinnedPlainHostForQuestSpread)
        {
            HideSolo(invokePlainDismissCallback: true);
            return;
        }

        if (_autoCloseRoutine != null)
        {
            StopCoroutine(_autoCloseRoutine);
            _autoCloseRoutine = null;
        }

        ActiveMultiOfferBoxes.Remove(this);
        _plainHostCollapsedLeavingQuestsOpen = true;
        // Collapsing the plain host to show quest cards only is not a full dismiss for post-death / one-way callbacks.
        _plainHideOnceCallback = null;
        gameObject.SetActive(false);

        float step = fixedSize.x + questOfferCardSpacing;
        int j = 0;
        SortActiveMultiOfferBoxesLeftToRight();
        for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
        {
            NPCDialogueBoxUI b = ActiveMultiOfferBoxes[i];
            if (b && b._spawnedAsOfferClone)
                b._stripAnchoredSpreadOffset = new Vector2(step * j++, 0f);
        }

        s_deferredStripMultiOpening = true;
    }

    private void HideSolo(bool invokePlainDismissCallback = true)
    {
        ActiveMultiOfferBoxes.Remove(this);

        if (_autoCloseRoutine != null)
        {
            StopCoroutine(_autoCloseRoutine);
            _autoCloseRoutine = null;
        }

        if (_activeBox == this)
            _activeBox = null;

        CompleteAllTypewriters();
        CleanupMultiOfferUi();
        SetOfferModeMulti(false);

        _stripWorldFollowActive = false;
        TeardownNpcDialogueRootCanvasComponents();

        _stripFollowAnchor = null;
        _resolvedStripPresentationCanvas = null;
        _stripProjectionRectRt = null;
        _stripAnchoredSpreadOffset = Vector2.zero;
        _stripUiClampFrameRt = null;

        _interactionOwnerTransform = null;
        _npcStripFollowPivotSource = null;

        _plainDialogueMode = false;
        _pinnedPlainHostForQuestSpread = false;
        _plainHostCollapsedLeavingQuestsOpen = false;

        gameObject.SetActive(false);
        if (invokePlainDismissCallback)
            InvokeAndClearPlainHideOnceCallback();
        else
            _plainHideOnceCallback = null;
    }

    /// <summary>Registers a callback fired once when this plain dialogue instance is hidden; cleared on the next <see cref="ShowAt"/>.</summary>
    public void SetPlainDialogueHideOnceCallback(Action callback) => _plainHideOnceCallback = callback;

    private void InvokeAndClearPlainHideOnceCallback()
    {
        Action cb = _plainHideOnceCallback;
        _plainHideOnceCallback = null;
        cb?.Invoke();
    }

    private void OnDestroy()
    {
        _plainHideOnceCallback = null;
        ActiveMultiOfferBoxes.Remove(this);
        if (_activeBox == this)
            _activeBox = null;
        if (ReferenceEquals(SpreadTemplate, this))
            SpreadTemplate = null;
    }

    /// <summary>
    /// Keeps <see cref="ActiveMultiOfferBoxes"/> ordered by horizontal spread (left → right). Plain host wins ties so it stays the left column.
    /// </summary>
    private static void SortActiveMultiOfferBoxesLeftToRight()
    {
        if (ActiveMultiOfferBoxes.Count < 2)
            return;

        ActiveMultiOfferBoxes.Sort(static (a, b) =>
        {
            if (!a && !b)
                return 0;
            if (!a)
                return 1;
            if (!b)
                return -1;

            int cmp = a._stripAnchoredSpreadOffset.x.CompareTo(b._stripAnchoredSpreadOffset.x);
            if (cmp != 0)
                return cmp;

            if (a._pinnedPlainHostForQuestSpread && !b._pinnedPlainHostForQuestSpread)
                return -1;
            if (!a._pinnedPlainHostForQuestSpread && b._pinnedPlainHostForQuestSpread)
                return 1;

            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });
    }

    /// <summary>
    /// When the quest giver reports no quests but a multi-offer spread is still registered for this NPC (stale session),
    /// tear it down so <see cref="ActiveDialogueIsDescendantOf"/> does not block plain dialogue.
    /// </summary>
    public static bool TryDismissStaleMultiOfferSpreadForZeroQuests(Transform npcRoot, int availableQuestCount)
    {
        if (!npcRoot || availableQuestCount != 0)
            return false;
        if (ActiveMultiOfferBoxes.Count == 0)
            return false;
        if (!ActiveDialogueIsDescendantOf(npcRoot))
            return false;

        CloseAllMultiOfferBoxesTogether();
        EndMultiOfferSpreadSession(suppressPlainDismissCallback: true);
        return true;
    }

    /// <summary>
    /// After the last quest in a spread is accepted, <see cref="CloseAllMultiOfferBoxesTogether"/> removes clones and unpins
    /// the plain host but does not hide it — callers must invoke this so <see cref="ActiveDialogueIsDescendantOf"/> does not
    /// block the next NPC click with an invisible/stale host.
    /// </summary>
    private static void EndMultiOfferSpreadSession(bool suppressPlainDismissCallback)
    {
        NPCDialogueBoxUI tpl = SpreadTemplate;
        if (tpl)
            tpl.Hide(suppressPlainDismissCallback);

        SpreadTemplate = null;
        SpreadParent = null;
        SpreadAnchor = null;
        SpreadBaseOffset = Vector3.zero;
        SpreadRefreshQuests = null;
        SpreadTryAcceptQuest = null;
        SpreadAutoCloseSeconds = 0f;
        s_deferredStripMultiOpening = false;
    }

    private static void CloseAllMultiOfferBoxesTogether()
    {
        if (ActiveMultiOfferBoxes.Count == 0)
            return;

        s_deferredStripMultiOpening = false;

        BulkClosingMultiOfferGroup = true;
        List<NPCDialogueBoxUI> snapshot = new(ActiveMultiOfferBoxes);
        ActiveMultiOfferBoxes.Clear();

        for (int i = 0; i < snapshot.Count; i++)
        {
            NPCDialogueBoxUI b = snapshot[i];
            if (!b)
                continue;

            if (b._spawnedAsOfferClone)
                Destroy(b.gameObject);
            else if (b._pinnedPlainHostForQuestSpread)
            {
                b._pinnedPlainHostForQuestSpread = false;
                b._plainHostCollapsedLeavingQuestsOpen = false;
                b._stripAnchoredSpreadOffset = Vector2.zero;
                if (!b.gameObject.activeSelf)
                    b.gameObject.SetActive(true);
            }
            else
                b.HideSolo(invokePlainDismissCallback: false);
        }

        BulkClosingMultiOfferGroup = false;
    }

    /// <summary>Single quest offer: title + reward static; description typewrites.</summary>
    public void ShowQuestOfferSingle(
        Transform parent,
        Transform anchor,
        Vector3 localOffset,
        QuestDefinition quest,
        bool showAccept,
        Action onAccept,
        float autoCloseSeconds,
        bool partOfMultiSpread = false,
        Vector2 stripAnchoredSpreadOffset = default)
    {
        EnsureBuilt();
        _stripAnchoredSpreadOffset = stripAnchoredSpreadOffset;
        if (!partOfMultiSpread)
        {
            CloseAllMultiOfferBoxesTogether();
            CompleteAllTypewriters();
            if (_activeBox != null && _activeBox != this)
                _activeBox.Hide(suppressPlainDismissCallback: true);
            _activeBox = this;
        }
        else
        {
            CompleteAllTypewriters();
        }

        CleanupMultiOfferUi();
        SetOfferModeMulti(false);

        ApplyCommonShowTransforms(parent, anchor, localOffset);

        if (_rectTransform)
            _rectTransform.sizeDelta = fixedSize;

        gameObject.SetActive(true);

        ConfigureSingleScrollTextsForQuestOffer(quest);

        _onAccept = onAccept;
        if (acceptButton)
        {
            acceptButton.gameObject.SetActive(showAccept);
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);
            acceptButton.onClick.AddListener(HandleAcceptClicked);
        }

        // Overlay: single finalize here (sync follower + clamps); multi-offer batches in SpreadTemplate LateUpdate.
        if (!_stripWorldFollowActive && _stripProjectionRectRt != null)
        {
            if (!partOfMultiSpread)
                FinalizeStripOverlayOpen(clampToUiFrameBand: true, syncViewportAnchorsFirst: true);
        }
        else if (!partOfMultiSpread)
            ClampInsideScreen();

        StartAutoClose(autoCloseSeconds);
    }

    private void ConfigureSingleScrollTextsForPlainDialogue()
    {
        if (_questOfferHeaderText)
        {
            _questOfferHeaderText.text = "";
            _questOfferHeaderText.gameObject.SetActive(false);
        }

        if (_singleTitleText)
        {
            _singleTitleText.text = "";
            _singleTitleText.gameObject.SetActive(false);
        }

        if (_singleRewardText)
        {
            _singleRewardText.text = "";
            _singleRewardText.gameObject.SetActive(false);
        }

        if (dialogueText)
            dialogueText.gameObject.SetActive(true);
    }

    private void ConfigureSingleScrollTextsForQuestOffer(QuestDefinition quest)
    {
        if (!quest)
            return;

        _plainDialogueMode = false;

        if (_questOfferHeaderText)
        {
            _questOfferHeaderText.gameObject.SetActive(true);
            _questOfferHeaderText.text = string.IsNullOrWhiteSpace(questDialogueHeaderText)
                ? "Available Quest"
                : questDialogueHeaderText.Trim();
        }

        if (_singleTitleText)
        {
            _singleTitleText.gameObject.SetActive(true);
            _singleTitleText.text = NPCInteractionSettings.BuildQuestOfferTitleHtml(quest);
        }

        string rewardHtml = NPCInteractionSettings.BuildQuestOfferRewardHtml(quest);
        if (_singleRewardText)
        {
            _singleRewardText.text = "";
            _singleRewardText.gameObject.SetActive(false);
        }

        if (dialogueText)
            dialogueText.gameObject.SetActive(true);

        string descPlain = NPCInteractionSettings.GetQuestOfferDescriptionPlain(quest);
        void RevealReward()
        {
            if (!_singleRewardText)
                return;
            _singleRewardText.text = rewardHtml;
            _singleRewardText.gameObject.SetActive(true);
        }

        if (string.IsNullOrEmpty(descPlain))
            RevealReward();
        else
            StartTypewriter(dialogueText, descPlain, RevealReward);
    }

    /// <summary>Show one or more quest offers. Multiple quests use separate full dialogue boxes laid out horizontally.</summary>
    public void ShowQuestOffersAt(
        Transform parent,
        Transform anchor,
        Vector3 localOffset,
        List<QuestDefinition> quests,
        Func<List<QuestDefinition>> refreshQuests,
        Func<QuestDefinition, bool> tryAcceptQuest,
        float autoCloseSeconds = 0f)
    {
        EnsureBuilt();

        if (quests == null || quests.Count == 0)
        {
            Hide();
            return;
        }

        if (quests.Count == 1)
        {
            QuestDefinition only = quests[0];
            ShowQuestOfferSingle(
                parent,
                anchor,
                localOffset,
                only,
                showAccept: true,
                () => tryAcceptQuest?.Invoke(only),
                autoCloseSeconds);
            return;
        }

        OpenMultipleQuestOffersAsSeparateBoxes(
            parent, anchor, localOffset, quests, refreshQuests, tryAcceptQuest, autoCloseSeconds);
    }

    private void OpenMultipleQuestOffersAsSeparateBoxes(
        Transform parent,
        Transform anchor,
        Vector3 baseOffset,
        List<QuestDefinition> quests,
        Func<List<QuestDefinition>> refreshQuests,
        Func<QuestDefinition, bool> tryAcceptQuest,
        float autoCloseSeconds)
    {
        SpreadTemplate = this;
        SpreadParent = parent;
        SpreadAnchor = anchor;
        SpreadBaseOffset = baseOffset;
        SpreadRefreshQuests = refreshQuests;
        SpreadTryAcceptQuest = tryAcceptQuest;
        SpreadAutoCloseSeconds = autoCloseSeconds;

        CloseAllMultiOfferBoxesTogether();

        if (_activeBox != null && _activeBox != this)
            _activeBox.Hide(suppressPlainDismissCallback: true);
        _activeBox = null;

        if (gameObject.activeSelf)
            HideSolo(invokePlainDismissCallback: false);

        float spreadStepPx = fixedSize.x + questOfferCardSpacing;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinition q = quests[i];
            NPCDialogueBoxUI inst = i == 0 ? this : Instantiate(gameObject).GetComponent<NPCDialogueBoxUI>();
            if (i > 0)
                inst._spawnedAsOfferClone = true;

            ActiveMultiOfferBoxes.Add(inst);

            QuestDefinition captured = q;
            inst.ShowQuestOfferSingle(
                parent,
                anchor,
                baseOffset,
                captured,
                showAccept: true,
                () => HandleSpreadQuestAccepted(captured),
                autoCloseSeconds,
                partOfMultiSpread: true,
                stripAnchoredSpreadOffset: new Vector2(spreadStepPx * i, 0f));
        }

        s_deferredStripMultiOpening = true;

        SortActiveMultiOfferBoxesLeftToRight();
    }

    private static void HandleSpreadQuestAccepted(QuestDefinition quest)
    {
        if (SpreadTryAcceptQuest == null || !SpreadTryAcceptQuest(quest))
            return;

        List<QuestDefinition> next = SpreadRefreshQuests != null ? SpreadRefreshQuests() : null;
        CloseAllMultiOfferBoxesTogether();

        if (next == null || next.Count == 0)
        {
            EndMultiOfferSpreadSession(suppressPlainDismissCallback: true);
            return;
        }

        if (SpreadTemplate != null && IsEligiblePlainHostForStackedQuestOffers(SpreadTemplate))
        {
            SpreadTemplate.StackQuestOffersBesidePlainDialogue(
                next,
                SpreadRefreshQuests,
                SpreadTryAcceptQuest,
                SpreadAutoCloseSeconds);
            return;
        }

        if (next.Count == 1)
        {
            QuestDefinition only = next[0];
            SpreadTemplate.ShowQuestOfferSingle(
                SpreadParent,
                SpreadAnchor,
                SpreadBaseOffset,
                only,
                showAccept: true,
                () =>
                {
                    SpreadTryAcceptQuest?.Invoke(only);
                    SpreadTemplate.Hide();
                },
                SpreadAutoCloseSeconds);
            return;
        }

        SpreadTemplate.OpenMultipleQuestOffersAsSeparateBoxes(
            SpreadParent,
            SpreadAnchor,
            SpreadBaseOffset,
            next,
            SpreadRefreshQuests,
            SpreadTryAcceptQuest,
            SpreadAutoCloseSeconds);
    }

    private void StartAutoClose(float seconds)
    {
        if (_autoCloseRoutine != null)
            StopCoroutine(_autoCloseRoutine);

        _autoCloseRoutine = seconds > 0f ? StartCoroutine(AutoCloseAfter(seconds)) : null;
    }

    private IEnumerator AutoCloseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _autoCloseRoutine = null;
        Hide();
    }

    private void HandleAcceptClicked()
    {
        _onAccept?.Invoke();
        Hide();
    }

    /// <summary>Keeps dialogue rect inside <see cref="_stripUiClampFrameRt"/> (UI strip band) using canvas-root-relative bounds.</summary>
    private void ClampAnchoredRectInsideUiFrame()
    {
        if (!_stripUiClampFrameRt || !_rectTransform || !_stripProjectionRectRt)
            return;

        Transform canvasRoot = _stripProjectionRectRt;

        for (int pass = 0; pass < 4; pass++)
        {
            Canvas.ForceUpdateCanvases();

            Bounds box = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRoot, _rectTransform);
            Bounds frame = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRoot, _stripUiClampFrameRt);

            float padFrac = Mathf.Clamp01(viewportPadding);
            float inset = Mathf.Max(4f, padFrac * 0.5f * Mathf.Min(frame.size.x, frame.size.y));

            float minX = frame.min.x + inset;
            float maxX = frame.max.x - inset;
            float minY = frame.min.y + inset;
            if (TryGetBottomHudTopAsMinLocalY(_stripProjectionRectRt, out float hudMinFloorY))
                minY = Mathf.Max(minY, hudMinFloorY);
            float maxY = frame.max.y - inset;
            if (minX >= maxX || minY >= maxY)
                return;

            float dx = 0f;
            float dy = 0f;
            if (box.min.x < minX)
                dx = minX - box.min.x;
            else if (box.max.x > maxX)
                dx = maxX - box.max.x;
            if (box.min.y < minY)
                dy = minY - box.min.y;
            else if (box.max.y > maxY)
                dy = maxY - box.max.y;

            if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
                return;

            _rectTransform.anchoredPosition += new Vector2(dx, dy);
        }
    }

    private void ClampInsideScreen()
    {
        if (!_rectTransform)
            return;

        if (_stripWorldFollowActive)
            return;

        if (_stripUiClampFrameRt)
        {
            Canvas.ForceUpdateCanvases();
            ClampAnchoredRectInsideUiFrame();
            return;
        }

        Canvas.ForceUpdateCanvases();

        Camera cam = ResolveWorldToScreenCamera();
        if (!cam)
            return;

        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 viewportPoint = cam.WorldToViewportPoint(corners[i]);
            if (viewportPoint.z < 0f)
                return;

            minX = Mathf.Min(minX, viewportPoint.x);
            minY = Mathf.Min(minY, viewportPoint.y);
            maxX = Mathf.Max(maxX, viewportPoint.x);
            maxY = Mathf.Max(maxY, viewportPoint.y);
        }

        float pad = Mathf.Clamp01(viewportPadding);
        float dx = 0f;
        float dy = 0f;

        if (minX < pad)
            dx = pad - minX;
        else if (maxX > 1f - pad)
            dx = (1f - pad) - maxX;

        if (minY < pad)
            dy = pad - minY;
        else if (maxY > 1f - pad)
            dy = (1f - pad) - maxY;

        if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
            return;

        if (_stripProjectionRectRt != null)
        {
            _rectTransform.anchoredPosition +=
                ViewportDeltaToAnchoredDeltaInStripParent(cam, dx, dy);
            return;
        }

        float depth = cam.WorldToViewportPoint(transform.position).z;
        Vector3 worldOrigin = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 worldDelta = cam.ViewportToWorldPoint(new Vector3(dx, dy, depth)) - worldOrigin;
        transform.position += worldDelta;
    }

    /// <summary>Moves every open multi-offer box by the same delta so the group stays inside the viewport (shift inward when overlapping an edge).</summary>
    private void ClampMultiOfferBoxesToViewport()
    {
        if (ActiveMultiOfferBoxes.Count == 0)
            return;

        SortActiveMultiOfferBoxesLeftToRight();

        Camera cam = null;
        for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
        {
            NPCDialogueBoxUI box = ActiveMultiOfferBoxes[i];
            if (!box || !box.isActiveAndEnabled)
                continue;

            cam = box.ResolveWorldToScreenCamera();
            break;
        }

        if (!cam)
            cam = Camera.main;
        if (!cam)
            return;

        RectTransform stripParent = null;
        Canvas stripCanvas = null;
        for (int si = 0; si < ActiveMultiOfferBoxes.Count; si++)
        {
            NPCDialogueBoxUI b = ActiveMultiOfferBoxes[si];
            if (b != null && b._stripProjectionRectRt != null)
            {
                stripParent = b._stripProjectionRectRt;
                stripCanvas = b._resolvedStripPresentationCanvas;
                break;
            }
        }

        float pad = Mathf.Clamp01(viewportPadding);
        Vector3[] corners = new Vector3[4];

        for (int iter = 0; iter < 4; iter++)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                NPCDialogueBoxUI box = ActiveMultiOfferBoxes[i];
                if (!box || !box._rectTransform)
                    continue;

                box._rectTransform.GetWorldCorners(corners);
                for (int j = 0; j < corners.Length; j++)
                {
                    Vector3 vp = cam.WorldToViewportPoint(corners[j]);
                    if (vp.z < 0f)
                        return;

                    minX = Mathf.Min(minX, vp.x);
                    minY = Mathf.Min(minY, vp.y);
                    maxX = Mathf.Max(maxX, vp.x);
                    maxY = Mathf.Max(maxY, vp.y);
                }
            }

            float dx = 0f;
            float dy = 0f;
            if (maxX > 1f - pad)
                dx = (1f - pad) - maxX;
            else if (minX < pad)
                dx = pad - minX;

            if (maxY > 1f - pad)
                dy = (1f - pad) - maxY;
            else if (minY < pad)
                dy = pad - minY;

            if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
                break;

            if (stripParent != null)
            {
                Vector2 nudge =
                    ViewportDeltaToAnchoredDeltaInStripParentStatic(
                        stripParent, stripCanvas, cam, dx, dy);
                for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
                {
                    if (ActiveMultiOfferBoxes[i] && ActiveMultiOfferBoxes[i]._rectTransform)
                        ActiveMultiOfferBoxes[i]._rectTransform.anchoredPosition += nudge;
                }
                continue;
            }

            float refDepth = cam.WorldToViewportPoint(ActiveMultiOfferBoxes[0].transform.position).z;
            Vector3 worldOrigin = cam.ViewportToWorldPoint(new Vector3(0f, 0f, refDepth));
            Vector3 worldDelta = cam.ViewportToWorldPoint(new Vector3(dx, dy, refDepth)) - worldOrigin;

            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                if (ActiveMultiOfferBoxes[i])
                    ActiveMultiOfferBoxes[i].transform.position += worldDelta;
            }
        }

        Canvas.ForceUpdateCanvases();
        ClampMultiOfferRowInsideUiFrame();
    }

    /// <summary>Single delta vs <see cref="_stripUiClampFrameRt"/> using union bounds — keeps horizontal spacing from <see cref="_stripAnchoredSpreadOffset"/>.</summary>
    private static void ClampMultiOfferRowInsideUiFrame()
    {
        if (ActiveMultiOfferBoxes.Count < 2)
            return;

        SortActiveMultiOfferBoxesLeftToRight();

        RectTransform canvasRoot = null;
        RectTransform frameRt = null;

        for (int si = 0; si < ActiveMultiOfferBoxes.Count; si++)
        {
            NPCDialogueBoxUI b = ActiveMultiOfferBoxes[si];
            if (!b || !b._stripProjectionRectRt || !b._stripUiClampFrameRt)
                continue;
            canvasRoot = b._stripProjectionRectRt;
            frameRt = b._stripUiClampFrameRt;
            break;
        }

        if (!canvasRoot || !frameRt)
            return;

        NPCDialogueBoxUI padSrc = SpreadTemplate ? SpreadTemplate : ActiveMultiOfferBoxes[0];
        float padFrac = padSrc ? Mathf.Clamp01(padSrc.viewportPadding) : 0f;

        for (int pass = 0; pass < 4; pass++)
        {
            Canvas.ForceUpdateCanvases();

            bool hasUnion = false;
            Bounds union = default;

            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                NPCDialogueBoxUI box = ActiveMultiOfferBoxes[i];
                if (!box || !box._rectTransform || box._stripProjectionRectRt != canvasRoot)
                    continue;

                Bounds boxB = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRoot, box._rectTransform);
                if (!hasUnion)
                {
                    union = boxB;
                    hasUnion = true;
                }
                else
                    union.Encapsulate(boxB);
            }

            if (!hasUnion)
                return;

            Bounds frame = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRoot, frameRt);
            float inset = Mathf.Max(4f, padFrac * 0.5f * Mathf.Min(frame.size.x, frame.size.y));

            float minX = frame.min.x + inset;
            float maxX = frame.max.x - inset;
            float minY = frame.min.y + inset;
            if (TryGetBottomHudTopAsMinLocalYStatic(canvasRoot, out float hudMinFloorY))
                minY = Mathf.Max(minY, hudMinFloorY);
            float maxY = frame.max.y - inset;
            if (minX >= maxX || minY >= maxY)
                return;

            float dx = 0f;
            float dy = 0f;
            if (union.min.x < minX)
                dx = minX - union.min.x;
            else if (union.max.x > maxX)
                dx = maxX - union.max.x;
            if (union.min.y < minY)
                dy = minY - union.min.y;
            else if (union.max.y > maxY)
                dy = maxY - union.max.y;

            if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
                return;

            Vector2 delta = new Vector2(dx, dy);

            for (int i = 0; i < ActiveMultiOfferBoxes.Count; i++)
            {
                NPCDialogueBoxUI box = ActiveMultiOfferBoxes[i];
                if (box && box._stripProjectionRectRt == canvasRoot && box._rectTransform)
                    box._rectTransform.anchoredPosition += delta;
            }
        }
    }

    /// <summary>
    /// Unity does not copy non-serialized refs on Instantiate; bind to the cloned hierarchy so we do not spawn duplicate UI
    /// (which caused overlapping text and wrong quest data on 2nd+ boxes).
    /// </summary>
    private void BindSingleModeRefsFromHierarchy()
    {
        if (!_singleModeScrollRoot)
        {
            Transform scroll = transform.Find("DialogueScrollView");
            if (scroll)
                _singleModeScrollRoot = scroll.gameObject;
        }

        if (_singleModeScrollRoot)
        {
            Transform content = _singleModeScrollRoot.transform.Find("Viewport/Content");
            if (content)
            {
                if (!_questOfferHeaderText)
                {
                    Transform h = content.Find("QuestOfferHeader");
                    if (h)
                        _questOfferHeaderText = h.GetComponent<TMP_Text>();
                }

                if (!_singleTitleText)
                {
                    Transform t = content.Find("QuestTitle");
                    if (t)
                        _singleTitleText = t.GetComponent<TMP_Text>();
                }

                if (!dialogueText)
                {
                    Transform t = content.Find("QuestBody");
                    if (t)
                        dialogueText = t.GetComponent<TMP_Text>();
                }

                if (!_singleRewardText)
                {
                    Transform t = content.Find("QuestReward");
                    if (t)
                        _singleRewardText = t.GetComponent<TMP_Text>();
                }
            }
        }

        if (!acceptButton)
        {
            Transform t = transform.Find("AcceptButton");
            if (t)
                acceptButton = t.GetComponent<Button>();
        }
    }

    /// <summary>Upgrades older prefabs: inserts the quest header row above <see cref="QuestTitle"/> when missing.</summary>
    private void EnsureQuestOfferHeaderInsertedIfMissing()
    {
        if (_questOfferHeaderText)
            return;

        Transform content = null;
        if (_singleModeScrollRoot)
            content = _singleModeScrollRoot.transform.Find("Viewport/Content");
        if (!content)
        {
            Transform scroll = transform.Find("DialogueScrollView");
            if (scroll)
                content = scroll.Find("Viewport/Content");
        }
        if (!content)
            return;

        _questOfferHeaderText = CreateScrollLineTMP("QuestOfferHeader", content, DialogueQuestHeaderFontSize, FontStyles.Bold, bodyFlexible: false);
        _questOfferHeaderText.color = new Color(0.88f, 0.91f, 0.96f, 1f);
        _questOfferHeaderText.gameObject.SetActive(false);
        _questOfferHeaderText.rectTransform.SetSiblingIndex(0);
    }

    private static readonly Color DialoguePanelBackdrop = new Color(0.05f, 0.05f, 0.08f, 0.94f);
    /// <summary>Unity stencil <see cref="Mask"/> derives visibility from graphic alpha — 0 hides all masked children.</summary>
    private const float DialogueViewportMaskMinAlpha = 0.02f;

    private const float DialogueQuestHeaderFontSize = 15f;
    private const float DialogueQuestTitleFontSize = 18f;
    private const float DialogueBodyFontSize = 16f;
    private const float DialogueQuestRewardFontSize = 14f;
    private const float DialogueChromeButtonLabelFontSize = 15f;

    private void ApplyDialogueScrollTypography()
    {
        if (_questOfferHeaderText)
            _questOfferHeaderText.fontSize = DialogueQuestHeaderFontSize;
        if (_singleTitleText)
            _singleTitleText.fontSize = DialogueQuestTitleFontSize;
        if (dialogueText)
            dialogueText.fontSize = DialogueBodyFontSize;
        if (_singleRewardText)
            _singleRewardText.fontSize = DialogueQuestRewardFontSize;
    }

    /// <summary>Single flat panel read: outer root carries the tint; scroll/image fills stay visually flat; viewport keeps low alpha only for masking.</summary>
    private void NormalizeDialoguePanelChrome()
    {
        Image rootImg = GetComponent<Image>();
        if (rootImg)
            rootImg.color = DialoguePanelBackdrop;

        Transform scrollTr = transform.Find("DialogueScrollView");
        if (scrollTr && scrollTr.TryGetComponent<Image>(out Image scrollBg))
            scrollBg.color = new Color(DialoguePanelBackdrop.r, DialoguePanelBackdrop.g, DialoguePanelBackdrop.b, 0f);

        if (!scrollTr)
            return;

        Transform vpTr = scrollTr.Find("Viewport");
        if (vpTr && vpTr.TryGetComponent<Image>(out Image vpImg))
            vpImg.color = new Color(1f, 1f, 1f, DialogueViewportMaskMinAlpha);
    }

    private void EnsureBuilt()
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;
        if (_rectTransform == null)
            _rectTransform = gameObject.AddComponent<RectTransform>();

        _rectTransform.sizeDelta = fixedSize;

        BindSingleModeRefsFromHierarchy();
        EnsureQuestOfferHeaderInsertedIfMissing();
        NormalizeDialoguePanelChrome();
        ApplyDialogueScrollTypography();

        if (dialogueText != null && acceptButton != null)
        {
            WireCloseButtonDismissesViaHide();
            return;
        }

        Image bg = GetComponent<Image>();
        if (!bg)
            bg = gameObject.AddComponent<Image>();
        bg.color = DialoguePanelBackdrop;
        bg.raycastTarget = true;
        AttachClickForward(gameObject);

        if (dialogueText != null && acceptButton != null)
        {
            WireCloseButtonDismissesViaHide();
            return;
        }

        RectTransform closeRt = CreateButton("CloseButton", "X", _rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(24f, 24f), out Button closeButton);
        WireCloseButton(closeButton);

        GameObject scrollGo = new GameObject("DialogueScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(_rectTransform, false);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(10f, 45f);
        scrollRt.offsetMax = new Vector2(-10f, -35f);
        Image scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.color =
            new Color(DialoguePanelBackdrop.r, DialoguePanelBackdrop.g, DialoguePanelBackdrop.b, 0f);
        scrollBg.raycastTarget = true;
        AttachClickForward(scrollGo);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollRt, false);
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        Image viewportImg = viewportGo.GetComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, DialogueViewportMaskMinAlpha);
        viewportImg.raycastTarget = true;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;
        AttachClickForward(viewportGo);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportRt, false);
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = new Vector2(6f, 0f);
        contentRt.offsetMax = new Vector2(-6f, 0f);

        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentGo.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _questOfferHeaderText = CreateScrollLineTMP("QuestOfferHeader", contentGo.transform, DialogueQuestHeaderFontSize, FontStyles.Bold, bodyFlexible: false);
        _questOfferHeaderText.color = new Color(0.88f, 0.91f, 0.96f, 1f);
        _questOfferHeaderText.gameObject.SetActive(false);

        _singleTitleText = CreateScrollLineTMP("QuestTitle", contentGo.transform, DialogueQuestTitleFontSize, FontStyles.Bold, bodyFlexible: false);
        dialogueText = CreateScrollLineTMP("QuestBody", contentGo.transform, DialogueBodyFontSize, FontStyles.Normal, bodyFlexible: true);
        _singleRewardText = CreateScrollLineTMP("QuestReward", contentGo.transform, DialogueQuestRewardFontSize, FontStyles.Normal, bodyFlexible: false);

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        _singleModeScrollRoot = scrollGo;

        RectTransform acceptRt = CreateButton("AcceptButton", "Accept", _rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 22f), new Vector2(80f, 30f), out acceptButton);
        acceptRt.gameObject.SetActive(false);

        ApplyDialogueScrollTypography();
    }

    /// <summary>
    /// <see cref="UIWindowCloseButton"/> uses <c>SetActive(false)</c>, which skips <see cref="Hide"/> collapse / spread bookkeeping for plain+quest sessions.
    /// </summary>
    private void WireCloseButtonDismissesViaHide()
    {
        Transform closeT = transform.Find("CloseButton");
        if (!closeT)
            return;
        Button b = closeT.GetComponent<Button>();
        if (!b)
            return;

        UIWindowCloseButton winClose = closeT.GetComponent<UIWindowCloseButton>();
        if (winClose)
            Destroy(winClose);

        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => Hide());
    }

    private void WireCloseButton(Button closeButton)
    {
        if (!closeButton)
            return;

        UIWindowCloseButton winClose = closeButton.GetComponent<UIWindowCloseButton>();
        if (winClose)
            Destroy(winClose);

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => Hide());
    }

    private TMP_Text CreateScrollLineTMP(string name, Transform parent, float fontSize, FontStyles style, bool bodyFlexible)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        var sz = go.GetComponent<ContentSizeFitter>();
        sz.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sz.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (bodyFlexible)
        {
            le.flexibleHeight = 1f;
            le.minHeight = 28f;
        }
        else
        {
            le.flexibleHeight = 0f;
            le.minHeight = 0f;
        }

        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.93f, 0.86f, 0.72f, 1f);
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.fontStyle = style;
        tmp.raycastTarget = true;

        AttachClickForward(go);
        return tmp;
    }

    private void SetOfferModeMulti(bool multi)
    {
        if (_singleModeScrollRoot)
            _singleModeScrollRoot.SetActive(!multi);
    }

    private void CleanupMultiOfferUi()
    {
    }

    private static RectTransform CreateButton(
        string name,
        string label,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        out Button button)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.78f, 0.68f, 0.46f, 1f);
        button = go.GetComponent<Button>();

        GameObject textGo = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rt, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TMP_Text tmp = textGo.GetComponent<TMP_Text>();
        tmp.text = label;
        tmp.fontSize = DialogueChromeButtonLabelFontSize;
        tmp.color = new Color(0.12f, 0.1f, 0.08f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;

        return rt;
    }
}
