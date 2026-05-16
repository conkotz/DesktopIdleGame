using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runs helper tips in GamePlay (modal overlay, movement lock). Put one instance in the GamePlay scene (e.g. on <c>WorldManager</c> or <c>GameplayBootstrap</c>) and assign definitions.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Helper Gameplay Controller")]
[DefaultExecutionOrder(20)]
public sealed class HelperGameplayController : MonoBehaviour
{
    public static HelperGameplayController Instance { get; private set; }

    /// <summary>True while this helper uses modal dim + gameplay lock (see <see cref="HelperPopupDefinition.darkenScreenAndLockGameplay"/>).</summary>
    public static bool BlocksStripGameplay =>
        Instance != null &&
        Instance.IsActiveHelperExpandedWithModalGameplayLock();

    /// <summary>
    /// <see cref="Time.frameCount"/> when the Character-toolbar whitelist UI path dismissed the helper —
    /// paired Toolbar <c>ToggleCharacter</c> must not <see cref="MainMenuWindowUI.Close"/> if already on Character.
    /// </summary>
    private static int s_characterToolbarWhitelistUiDismissStampFrame = -1;

    /// <seealso cref="MainMenuWindowUI"/>
    internal static bool IsCharacterWhitelistToolbarDismissOnThisFrame() =>
        s_characterToolbarWhitelistUiDismissStampFrame == Time.frameCount;

    private static int s_questToolbarWhitelistUiDismissStampFrame = -1;

    internal static bool IsQuestWhitelistToolbarDismissOnThisFrame() =>
        s_questToolbarWhitelistUiDismissStampFrame == Time.frameCount;

    private static int s_skillsAbilityToolbarWhitelistUiDismissStampFrame = -1;

    internal static bool IsSkillsAbilityWhitelistToolbarDismissOnThisFrame() =>
        s_skillsAbilityToolbarWhitelistUiDismissStampFrame == Time.frameCount;

    private static int s_levelSelectToolbarWhitelistUiDismissStampFrame = -1;

    internal static bool IsLevelSelectWhitelistToolbarDismissOnThisFrame() =>
        s_levelSelectToolbarWhitelistUiDismissStampFrame == Time.frameCount;

    /// <summary>When respawning with <see cref="GameplayRespawnHelperPersistence"/>, old controller stores this so the new instance can restore <see cref="_activeDefinition"/>.</summary>
    private static string s_resumeActiveHelperIdAfterSceneReload;

    /// <summary>
    /// When the menu is already on a toolbar tab and the player taps that tab again, normally <see cref="MainMenuWindowUI.Close"/> runs.
    /// During an active helper that whitelist-dismisses that same toolbar id, we keep the menu open (helper dismisses separately).
    /// Uses definition match so script order vs. <see cref="NotifyWhitelistUiInteract"/> does not matter; frame stamps still apply for edge cases.
    /// </summary>
    internal static bool KeepMainMenuOpenWhenRepeatingToolbarTap(string toolbarWhitelistId)
    {
        if (BlocksStripGameplay)
            return true;

        if (string.IsNullOrWhiteSpace(toolbarWhitelistId))
            return false;

        if (Instance != null &&
            Instance._activeDefinition != null &&
            (Instance._activeDefinition.dismissModes & HelperDismissMode.InteractWhitelistDismiss) != 0)
        {
            if (Instance._activeDefinition.MatchesWhitelistId(toolbarWhitelistId))
                return true;

            if (HelperWhitelistUiInteractTarget.IsSkillsAbilityToolbarWhitelistMarker(toolbarWhitelistId) &&
                Instance._activeDefinition.MatchesWhitelistId(
                    HelperWhitelistUiInteractTarget.SkillsAbilityToolbarWhitelistIdLegacy))
                return true;
        }

        string t = toolbarWhitelistId.Trim();
        if (string.Equals(t, HelperWhitelistUiInteractTarget.CharacterToolbarWhitelistId, StringComparison.OrdinalIgnoreCase))
            return IsCharacterWhitelistToolbarDismissOnThisFrame();
        if (string.Equals(t, HelperWhitelistUiInteractTarget.QuestToolbarWhitelistId, StringComparison.OrdinalIgnoreCase))
            return IsQuestWhitelistToolbarDismissOnThisFrame();
        if (HelperWhitelistUiInteractTarget.IsSkillsAbilityToolbarWhitelistMarker(t))
            return IsSkillsAbilityWhitelistToolbarDismissOnThisFrame();
        if (string.Equals(t, HelperWhitelistUiInteractTarget.LevelSelectToolbarWhitelistId, StringComparison.OrdinalIgnoreCase))
            return IsLevelSelectWhitelistToolbarDismissOnThisFrame();

        return false;
    }

    [Header("Data")]
    [Tooltip("Assign helper assets here. First-visit triggers only run when Required Map Node Id matches the active level's MapNodeDefinition.nodeId (e.g. tutorial_1).")]
    [SerializeField] private HelperPopupDefinition[] definitions;

    [Header("Presentation")]
    [Tooltip("Rect parent for the helper overlay when Prefer Full Screen UI Canvas is off. Assign the strip UI_Frame so layout tracks the strip, or leave empty.")]
    [SerializeField] private RectTransform uiParentOverride;

    [Tooltip(
        "When on (default): parent the helper overlay to the tagged UICanvas for a true full‑screen modal, working raycasts on nested panels, and correct dimming. When off: uses Ui Parent Override.")]
    [SerializeField] private bool preferFullscreenUiCanvasForHelper = true;

    /// <summary>Falls above maps/menus canvases (~10k); lower values let FullWindow raycasts steal clicks.</summary>
    [SerializeField] private int canvasSortOrder = 15000;

    [Tooltip("Nested panel canvas — must sort above glow so text/close raycasts hit first.")]
    [SerializeField] private int panelSortDelta = 2;

    [Tooltip(
        "When any ancestor of the whitelist marker has a direct child with this name (e.g. Visuals), " +
        "helper tint applies only to sprite renderers under it so a legacy root SpriteRenderer is skipped. " +
        "Leave empty to tint all renderers under the marker (simple props / buttons).")]
    [SerializeField] private string visualsSubtreeChildName = "Visuals";

    [Header("Whitelist presentation")]
    [Tooltip(
        "When on, clones whitelist presentation sprites into UI above the dimmer with a yellow pulsing glow (visible over the blackout). Uses the Visuals subtree rule when set.")]
    [SerializeField] private bool whitelistGlowAboveDimmer = true;

    [Tooltip("Camera for mapping sprite bounds into overlay space (fallback: GameObject named StripCamera).")]
    [SerializeField] private Camera worldCameraForWhitelistGlow;

    [Tooltip("Glow colour; alpha is driven by the pulse (min/max below).")]
    [SerializeField] private Color whitelistHelperTint = new Color(1f, 0.9f, 0.28f, 1f);

    [Tooltip("Alpha pulse extremes for the overlay glow (0 = fades to fully transparent).")]
    [SerializeField] [Range(0f, 1f)] private float whitelistGlowPulseAlphaMin = 0.32f;

    [SerializeField] [Range(0f, 1f)] private float whitelistGlowPulseAlphaMax = 0.92f;

    [SerializeField] private float whitelistGlowPulseSpeed = 3.4f;

    [Tooltip(
        "World whitelist glow only: scales the duplicate silhouette under the main glow (>1 adds a larger rim). " +
        "Set to 1 to disable the extra layer.")]
    [SerializeField] [Range(1f, 1.35f)]
    private float whitelistWorldGlowHaloUniformScale = 1.14f;

    [Tooltip("World halo alpha multiplier vs. main pulsing glow.")]
    [SerializeField] [Range(0f, 1f)]
    private float whitelistWorldGlowHaloAlphaScale = 0.62f;

    [Tooltip("When Glow Above Dimmer is off: tints world SpriteRenderers (mostly hidden under fullscreen dim).")]
    [SerializeField] [Range(0f, 1f)] private float whitelistHelperTintStrength = 0.42f;

    [SerializeField] private float whitelistTintLerpSpeed = 14f;

    [Header("Whitelist UI glow (toolbar / buttons)")]
    [Tooltip("Soft outer pad (each side) behind the icon clone so UI targets read like the NPC pulse, not a flat panel fill.")]
    [SerializeField] private float whitelistUiGlowHaloPadding = 18f;

    [SerializeField] [Range(1f, 1.35f)] private float whitelistUiGlowHaloUniformScale = 1.14f;

    [SerializeField] [Range(0.1f, 1f)] private float whitelistUiGlowHaloAlphaScale = 0.86f;

    /// <summary>Single source for script default, reset-all-windows fallback, and new-game layout when no controller.</summary>
    private static readonly Vector2 kDefaultHelperPanelSize = new(800f, 800f);

    private static readonly Vector2 kDefaultHelperPanelAnchoredPosition = new(60f, -500f);

    [SerializeField] private Vector2 panelSize = kDefaultHelperPanelSize;

    [SerializeField] private Vector2 panelAnchoredPosition = kDefaultHelperPanelAnchoredPosition;

    public Vector2 DefaultHelperPanelAnchoredPosition => panelAnchoredPosition;

    public Vector2 DefaultHelperPanelSize => panelSize;

    [Tooltip(
        "How far above the helper front panel Canvas sort order to place nested canvases built on whitelist UI markers (toolbar buttons). Larger = safer over other overlays.")]
    [SerializeField] private int whitelistUiCanvasSortBeyondPanel = 125;

    [Header("Body typewriter")]
    [Tooltip(
        "How fast the helper body reveals (TMP visible characters per second; full paragraph layout while typing — same idea as NPCDialogueBoxUI). Higher = faster. 0 = one character per frame.")]
    [SerializeField] private float typewriterCharactersPerSecond = 48f;

    [Tooltip(
        "Scales title, body, chrome strip title, and footer/chrome button TMP font sizes (1 = default authored sizes). Designer-only — not a player setting.")]
    [SerializeField] [Range(0.5f, 2f)]
    private float helperTipTextSizeMultiplier = 1f;

    [Header("Helper \"new\" badge")]
    [Tooltip("Alpha pulse speed when a fresh helper tip opens (unscaled time).")]
    [SerializeField]
    private float helperNewBadgePulseSpeed = 1.65f;

    [SerializeField] [Range(0.15f, 1f)]
    private float helperNewBadgePulseAlphaMin = 0.4f;

    [SerializeField] [Range(0.15f, 1f)]
    private float helperNewBadgePulseAlphaMax = 1f;

    [Header("Helper priority queue")]
    [Tooltip(
        "When several helpers are eligible in the same evaluation pass, only the highest-priority one shows first; the rest queue and open immediately after the current one clears (whitelist dismiss/minimize, X close, or the next popup in the chain). " +
        "If > 0 and the player leaves the whitelist-dismiss step idle while more helpers are still queued, the current tip is marked completed after this delay so the next queued helper can open. Set to 0 to disable timed skip.")]
    [SerializeField]
    private float autoSkipStuckHelperWhenQueueHasMoreSeconds = 22f;

    private Coroutine _bodyTypewriterCo;
    private string _bodyTypewriterFullPlain;

    private bool _activeUsesWorldWhitelistRouting;

    /// <summary>
    /// While a staged whitelist helper is active: until the first valid whitelist interact, modal dim + movement stay on.
    /// That interact then clears emphasis for <b>every</b> whitelisted id and completes the scripted dismiss.
    /// </summary>
    private bool _whitelistStagedModalReleased;

    private HelperPopupDefinition _activeDefinition;

    private const float ExpandedHeaderStripHeight = 40f;

    private const string HelperNewBadgeChildName = "HelperNewBadge";

    private const float HelperNewBadgeFontSize = 31.5f;

    private static readonly Vector2 kHelperNewBadgeSizeDelta = new(201.6f, 50.4f);

    private static readonly Vector2 kHelperNewBadgeAnchoredPosition = new(21f, 25.2f);

    private const float HelperBodyScrollbarWidth = 14f;

    private const float HelperBodyScrollHorizontalPadding = 8f;

    private const float ExpandedFooterNavHeight = 34f;

    [Header("Footer nav layout")]
    [Tooltip("How far the helper history footer nav buttons are inset from the left/right edges.")]
    [SerializeField] private float historyFooterButtonEdgeInset = 18f;

    /// <summary>Last non–header-only helper panel height (preserves resize across minimize).</summary>
    private Vector2 _lastExpandedPanelSizeDelta;

    private Vector2 _layoutTrackSize;

    private bool HasActiveTutorialDefinitionPending() => _activeDefinition != null;

    private bool IsHelperExpandedPresentation() =>
        _expandedPanelRoot != null && _expandedPanelRoot.activeSelf;

    private bool IsActiveHelperExpandedWithModalGameplayLock() =>
        _activeDefinition != null &&
        _activeDefinition.darkenScreenAndLockGameplay &&
        IsHelperExpandedPresentation() &&
        !_whitelistStagedModalReleased;

    private GameObject _expandedPanelRoot;

    private RectTransform _helperPanelRt;

    /// <summary>True when we added a runtime <see cref="Canvas"/> on <see cref="_helperPanelRt"/> for modal stacking (safe to remove with its <see cref="GraphicRaycaster"/>).</summary>
    private bool _helperPanelModalBreakoutOwned;

    private Image _dimmerImage;

    private TMP_Text _chromeStripTitleText;

    private GameObject _helperNewBadgeRoot;

    private CanvasGroup _helperNewBadgeCanvasGroup;

    private int _helperFontBasesOverlayInstanceId = int.MinValue;

    /// <summary>Last <see cref="HelperTipTextSizeMultiplierClamped"/> applied to TMP font sizes (drives base-font recomputation when it changes).</summary>
    private float _lastAppliedHelperTipMul;

    private float _baseFontChromeStripTitle;
    private float _baseFontTitle;
    private float _baseFontBody;
    private float _baseFontMinimizeGlyph;
    private float _baseFontClose;
    private float _baseFontPrevNav;
    private float _baseFontNextNav;

    private TMP_Text _minimizeExpandGlyphTmp;

    private Button _prevHistoryButton;

    private Button _nextHistoryButton;

    private struct HelperDisplayedMessageSnap
    {
        public string HelperId;

        public string TitlePlain;

        public string BodyPlain;
    }

    private const int MaxPersistedHelperMessages = 32;

    private const string PersistedHelperHistoryKey = "HelperSessionMessageHistory.v2";

    [Serializable]
    private sealed class PersistedHelperSnapDto
    {
        public string helperId;

        public string title;

        public string body;
    }

    [Serializable]
    private sealed class PersistedHelperHistoryDto
    {
        public PersistedHelperSnapDto[] items;
    }

    private readonly List<HelperDisplayedMessageSnap> _sessionMessageHistory = new(16);

    private readonly Queue<HelperPopupDefinition> _pendingHelperQueue = new();

    private Coroutine _stuckQueuedAdvanceCo;

    private int _historyViewIndex;

    private PlayerController _player;

    private Inventory _inventoryForHelpers;

    private QuestProgressManager _questProgressForHelpers;

    private SkillsManager _skillsManagerForHelpers;

    /// <summary>Snapshot before each <see cref="Inventory.OnInventoryChanged"/> callback for trigger math.</summary>
    private int _lastSeenInventoryTotalUnitsBeforeChange;

    private bool _inventoryHelperEvalDeferred;

    private readonly List<SavedWhitelistUiElevation> _whitelistUiElevations = new(4);

    private readonly Vector3[] _uiWorldCornersScratch = new Vector3[4];

    private struct SavedWhitelistUiElevation
    {
        public Canvas CanvasComp;
        public bool CanvasCreatedDuringHelperOverlay;
        public bool GraphicRaycasterCreatedDuringHelperOverlay;
        public bool BackupHadOverrideSorting;
        public int BackupSortOrder;
    }

    private GameObject _overlayRoot;
    private RectTransform _overlayRect;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;

    private ScrollRect _helperBodyScrollRect;
    private RectTransform _helperBodyScrollContent;
    private Scrollbar _helperBodyScrollbar;

    /// <summary>Runtime-only close control (optional per-definition).</summary>
    private GameObject _helperCloseButtonRoot;

    /// <summary>Glow overlays above dimmer.</summary>
    private RectTransform _whitelistGlowHolder;

    private readonly List<WhitelistGlowLink> _whitelistGlowLinks = new();

    private struct WhitelistGlowLink
    {
        public SpriteRenderer SourceSprite;
        public Graphic SourceGraphic;
        /// <summary>Foreground silhouette (matches NPC glow Image clone).</summary>
        public Image GlowImg;
        /// <summary>Optional soft rim under <see cref="GlowImg"/> (UI) or scaled duplicate (world sprites).</summary>
        public Image GlowHaloImg;
    }

    private struct WhitelistTintState
    {
        public SpriteRenderer Renderer;
        public Color SavedColor;
    }

    private readonly List<WhitelistTintState> _whitelistPresentationTints = new();

    /// <summary>Call from <see cref="WorldInputRouter2D"/> after routing a click that hit a whitelist collider while blocking.</summary>
    /// <param name="worldWinnerCollider">Hit collider (staged whitelist dismiss clears glow/sort emphasis for <b>all</b> configured ids).</param>
    public static void NotifyWhitelistWorldRouteHandled(Collider2D worldWinnerCollider = null)
    {
        if (Instance == null || !ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
            return;

        if (Instance._activeDefinition != null)
            Instance.DismissHelperNewBadgeFromPanelPointer();

        Instance.TryDismiss(
            HelperDismissMode.InteractWhitelistDismiss,
            interactWhitelistIdMarker: null,
            whitelistUiGlowSource: null,
            whitelistWorldWinnerCollider: worldWinnerCollider);
    }

    /// <summary>
    /// Call from <see cref="HelperWhitelistUiInteractTarget"/> when the player activates a whitelist id (toolbar / UI).
    /// </summary>
    /// <param name="whitelistUiGlowSource">Graphic used when building this target's glow overlay — clears the correct instance when ids repeat.</param>
    public static void NotifyWhitelistUiInteract(string interactionIdMarker, Graphic whitelistUiGlowSource = null)
    {
        if (Instance == null ||
            string.IsNullOrWhiteSpace(interactionIdMarker) ||
            !ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
            return;

        string trimmed = interactionIdMarker.Trim();
        if (Instance._activeDefinition != null && Instance._activeDefinition.MatchesWhitelistId(trimmed))
            Instance.DismissHelperNewBadgeFromPanelPointer();

        Instance.TryDismiss(
            HelperDismissMode.InteractWhitelistDismiss,
            trimmed,
            whitelistUiGlowSource,
            whitelistWorldWinnerCollider: null);
    }

    /// <summary>
    /// Removes the pulsing glow overlay tied to <paramref name="sourceGraphic"/> (must match the graphic used when the glow was built).
    /// Called from <see cref="HelperWhitelistUiInteractTarget"/> when clear-glow-on-hover is enabled (component or
    /// <see cref="HelperPopupDefinition.whitelistInteractEntries"/> when that id is listed there).
    /// </summary>
    public static void NotifyWhitelistUiGlowClearedByPointerEnter(Graphic sourceGraphic)
    {
        if (Instance == null || !sourceGraphic || !ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
            return;

        Instance.RemoveWhitelistGlowLinkForSourceGraphic(sourceGraphic);
    }

    /// <summary>
    /// Resolves whether pointer-enter should clear this UI target's glow: if the id matches a
    /// <see cref="HelperPopupDefinition.whitelistInteractEntries"/> row, that row's flag is used; otherwise the component default
    /// (ids whitelisted only via <see cref="HelperPopupDefinition.whitelistedInteractionIds"/>).
    /// </summary>
    public static bool ShouldClearWhitelistUiGlowOnPointerEnter(string interactionId, bool componentDefault)
    {
        if (string.IsNullOrWhiteSpace(interactionId))
            return false;

        HelperGameplayController inst = Instance;
        if (inst == null || inst._activeDefinition == null)
            return componentDefault;

        HelperPopupDefinition def = inst._activeDefinition;
        if (!def.MatchesWhitelistId(interactionId))
            return false;

        if (def.TryGetStructuredEntryClearGlowOnPointerEnter(interactionId, out bool clearGlow))
            return clearGlow;

        return componentDefault;
    }

    private void RemoveWhitelistGlowLinkForSourceGraphic(Graphic sourceGraphic)
    {
        if (!sourceGraphic || _whitelistGlowLinks.Count == 0)
            return;

        for (int i = _whitelistGlowLinks.Count - 1; i >= 0; i--)
        {
            WhitelistGlowLink link = _whitelistGlowLinks[i];
            if (link.SourceGraphic != sourceGraphic)
                continue;

            if (link.GlowHaloImg)
                Destroy(link.GlowHaloImg.gameObject);

            if (link.GlowImg)
                Destroy(link.GlowImg.gameObject);

            _whitelistGlowLinks.RemoveAt(i);
            MaybeCompleteStagedWhitelistEmphasisDismiss();
            return;
        }
    }

    /// <summary>
    /// True when the active helper uses modal dim + glow-above-dimmer whitelist emphasis (staged release on first valid whitelist interact).
    /// </summary>
    private bool ActiveHelperUsesStagedWhitelistEmphasisRelease()
    {
        if (_activeDefinition == null)
            return false;

        if ((_activeDefinition.dismissModes & HelperDismissMode.InteractWhitelistDismiss) == 0)
            return false;

        if (!_activeDefinition.darkenScreenAndLockGameplay ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            !whitelistGlowAboveDimmer ||
            !_activeDefinition.HasConfiguredWhitelistInteractIds())
            return false;

        return true;
    }

    /// <summary>
    /// Staged whitelist: first valid whitelist interact lifts modal + unlock, clears <b>all</b> whitelist glow/tint
    /// emphasis (every configured id counts for visuals), then scripted dismiss completes.
    /// </summary>
    private bool TryProgressStagedWhitelistInteractDismiss(
        string interactWhitelistIdMarker,
        Graphic whitelistUiGlowSource,
        Collider2D whitelistWorldWinnerCollider)
    {
        if (!ActiveHelperUsesStagedWhitelistEmphasisRelease())
            return false;

        if (!_whitelistStagedModalReleased && _activeDefinition.darkenScreenAndLockGameplay)
        {
            _whitelistStagedModalReleased = true;
            ClearModalDimmerAndUnlockPlayer();
            ApplyDarkenModalPresentation();
            EnsureHelperModalOverlayDrawOrder();
        }

        // Any completed whitelist interact removes emphasis from every configured target (not only the one clicked).
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        MaybeCompleteStagedWhitelistEmphasisDismiss();
        return true;
    }

    private void MaybeCompleteStagedWhitelistEmphasisDismiss()
    {
        if (_activeDefinition == null)
            return;

        if (!ActiveHelperUsesStagedWhitelistEmphasisRelease())
            return;

        bool presentationTintsBlockCompletion =
            !whitelistGlowAboveDimmer && _whitelistPresentationTints.Count > 0;

        if (_whitelistGlowLinks.Count > 0 || presentationTintsBlockCompletion)
            return;

        CompleteScriptedDismissLeaveExpanded();
    }

    private void RemoveWhitelistGlowLinksForWorldCollider(Collider2D winnerCol)
    {
        if (!winnerCol || _activeDefinition == null || _whitelistGlowLinks.Count == 0)
            return;

        HelperWhitelistInteractTarget marker =
            winnerCol.GetComponentInParent<HelperWhitelistInteractTarget>(true);
        if (!marker || !_activeDefinition.MatchesWhitelistId(marker.InteractionId))
            return;

        var scratch = new List<SpriteRenderer>(24);
        var seen = new HashSet<SpriteRenderer>();
        CollectWhitelistPresentationSprites(marker, visualsSubtreeChildName, scratch, seen);
        if (scratch.Count == 0)
            return;

        var spriteSet = new HashSet<SpriteRenderer>(scratch);
        for (int i = _whitelistGlowLinks.Count - 1; i >= 0; i--)
        {
            WhitelistGlowLink link = _whitelistGlowLinks[i];
            SpriteRenderer spr = link.SourceSprite;
            if (!spr || !spriteSet.Contains(spr))
                continue;

            if (link.GlowHaloImg)
                Destroy(link.GlowHaloImg.gameObject);

            if (link.GlowImg)
                Destroy(link.GlowImg.gameObject);

            _whitelistGlowLinks.RemoveAt(i);
        }

        NormalizeWhitelistMarkerStripDrawOrder(marker);

        MaybeCompleteStagedWhitelistEmphasisDismiss();
    }

    /// <summary>
    /// After Settings "reset all windows": restores helper panel to serialized defaults and fixes
    /// <see cref="UIDragWindow"/> reset anchor (global reset clears prefs/memory and re-runs <see cref="UIDragWindow.ResetToAnchorPoint"/> first).
    /// </summary>
    public static void ResetHelperPanelLayoutToInspectorDefaultsAfterGlobalWindowReset()
    {
        HelperGameplayController ctrl =
            Instance ?? FindFirstObjectByType<HelperGameplayController>(FindObjectsInactive.Include);
        if (ctrl != null)
        {
            ctrl.ResetHelperPanelLayoutToInspectorDefaults();
            return;
        }

        Vector2 pos = kDefaultHelperPanelAnchoredPosition;
        Vector2 sz = kDefaultHelperPanelSize;
        HelperPopupWindow host = HelperPopupWindow.FindExisting();
        RectTransform panel = host ? host.transform.Find("HelperPanel") as RectTransform : null;
        if (!panel)
            return;

        panel.anchoredPosition = pos;
        panel.sizeDelta = sz;

        Transform chrome = panel.Find("TopChromeStrip");
        if (chrome && chrome.TryGetComponent(out UIDragWindow dw))
            dw.SetResetAnchorPoint(pos);

        if (panel.TryGetComponent(out UIWindowCornerResize rz))
            rz.ForgetPersistedScaleAndResetToBase();

        // Match "help enabled" minimized chrome — not an empty expanded panel (see ResetHelperPanelLayoutToInspectorDefaults).
        Transform expandedTf = panel.Find("ExpandedPresentation");
        if (expandedTf)
            expandedTf.gameObject.SetActive(false);
        panel.sizeDelta = new Vector2(sz.x, ExpandedHeaderStripHeight);
        Transform titleGo = chrome ? chrome.Find("ChromeTitle") : null;
        if (titleGo)
            titleGo.gameObject.SetActive(true);
        Transform glyphTr = chrome ? chrome.Find("MinimizeStripeButton/Label") : null;
        if (glyphTr && glyphTr.TryGetComponent(out TMP_Text gTmp))
            gTmp.text = "+";

        HelperPopupLayoutPrefs.Save(pos, sz, headerOnlyLayout: true);
        UIWindowPositionMemory.Save("HelperPopupWindow.Panel", pos);
    }

    private void ResetHelperPanelLayoutToInspectorDefaults()
    {
        if (!_helperPanelRt)
            return;

        Vector2 pos = DefaultHelperPanelAnchoredPosition;
        Vector2 sz = DefaultHelperPanelSize;

        _helperPanelRt.anchoredPosition = pos;
        _helperPanelRt.sizeDelta = sz;

        Transform chrome = _helperPanelRt.Find("TopChromeStrip");
        if (chrome && chrome.TryGetComponent(out UIDragWindow dw))
            dw.SetResetAnchorPoint(pos);

        if (_helperPanelRt.TryGetComponent(out UIWindowCornerResize rz))
            rz.ForgetPersistedScaleAndResetToBase();

        // Global settings reset runs after toggle prefs reload; leaving ExpandedPresentation active with no
        // active helper looks like an empty full window. Use the same collapsed stripe as PresentMinimizedAwaitingFuturePopups.
        if (_expandedPanelRoot)
        {
            if (HelpersPermittedBySettings())
                TransitionToCollapsedStripeLayout();
            else
            {
                _expandedPanelRoot.SetActive(false);
                RefreshChromeCollapsedVisuals(true);
                _helperPanelRt.sizeDelta = new Vector2(sz.x, ExpandedHeaderStripHeight);
                SaveHelperLayoutSnapshot();
            }
        }
        else
        {
            HelperPopupLayoutPrefs.Save(pos, sz, headerOnlyLayout: true);
            UIWindowPositionMemory.Save("HelperPopupWindow.Panel", pos);
        }

        _layoutTrackSize = _helperPanelRt.sizeDelta;
    }

    /// <summary>
    /// New save slot only: clears persisted helper layout (other windows unchanged). Called from <see cref="SaveManager"/>.
    /// </summary>
    public static void ResetHelperWindowLayoutForNewGame()
    {
        HelperPopupLayoutPrefs.Clear();
        UIWindowPositionMemory.ForgetKey("HelperPopupWindow.Panel");

        HelperGameplayController ctrl =
            Instance ?? FindFirstObjectByType<HelperGameplayController>(FindObjectsInactive.Include);

        Vector2 pos = ctrl != null ? ctrl.DefaultHelperPanelAnchoredPosition : kDefaultHelperPanelAnchoredPosition;
        Vector2 sz = ctrl != null ? ctrl.DefaultHelperPanelSize : kDefaultHelperPanelSize;

        HelperPopupWindow host = HelperPopupWindow.FindExisting();
        RectTransform panel = host ? host.transform.Find("HelperPanel") as RectTransform : null;
        if (panel != null)
        {
            panel.anchoredPosition = pos;
            panel.sizeDelta = sz;

            Transform expanded = panel.Find("ExpandedPresentation");
            if (expanded)
                expanded.gameObject.SetActive(true);

            Transform chrome = panel.Find("TopChromeStrip");
            if (chrome)
            {
                if (chrome.TryGetComponent(out UIDragWindow dw))
                    dw.SetResetAnchorPoint(pos);

                Transform titleGo = chrome.Find("ChromeTitle");
                if (titleGo)
                    titleGo.gameObject.SetActive(false);

                Transform glyphTr = chrome.Find("MinimizeStripeButton/Label");
                if (glyphTr && glyphTr.TryGetComponent(out TMP_Text gTmp))
                    gTmp.text = "-";
            }

            if (panel.TryGetComponent(out UIWindowCornerResize rz))
                rz.ForgetPersistedScaleAndResetToBase();
        }

        UIWindowPositionMemory.Save("HelperPopupWindow.Panel", pos);

        ClearPersistedHelperMessageHistoryKey();

        if (ctrl != null)
        {
            ctrl.StopStuckQueuedAdvanceCoroutine();
            ctrl._pendingHelperQueue.Clear();

            ctrl._sessionMessageHistory.Clear();
            ctrl._historyViewIndex = 0;
            ctrl._whitelistStagedModalReleased = false;
            ctrl._activeDefinition = null;
            ctrl._activeUsesWorldWhitelistRouting = false;

            if (ctrl._overlayRoot && ctrl._overlayRoot.activeSelf)
                ctrl.HideOverlayCompletely(true, purgeMessageHistory: false);

            ctrl.SyncHelperInternalStateAfterNewGameLayout(pos, sz);
        }
    }

    /// <summary>
    /// Call before loading Bootstrap when leaving gameplay (logout / save-slot menu). Death-respawn can set a flag so
    /// <see cref="OnDestroy"/> keeps the DontDestroyOnLoad helper shell visible for the next GamePlay load; if the player
    /// goes to Bootstrap instead, that path skips <see cref="HideOverlayCompletely"/> and the dimmer then intercepts all UI clicks.
    /// </summary>
    public static void ForceHidePersistentOverlayForMenuNavigation()
    {
        GameplayRespawnHelperPersistence.ClearStaleKeepOverlayFlagIfPresent();

        HelperGameplayController inst =
            Instance ?? FindFirstObjectByType<HelperGameplayController>(FindObjectsInactive.Include);
        if (inst != null)
            inst.HideOverlayCompletely(true, purgeMessageHistory: false, drainPendingQueue: true);

        // Logout / Bootstrap: destroy the DDOL helper shell entirely. SetActive(false) still leaves whitelist-elevated
        // nested canvases + sorting state that can win raycasts over the save-slot menu after a death-resume session.
        // Death→GamePlay reload never calls this method, so the overlay can still be preserved across respawn loads.
        HelperPopupWindow[] shells =
            FindObjectsByType<HelperPopupWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shells.Length; i++)
        {
            if (shells[i])
                UnityEngine.Object.Destroy(shells[i].gameObject);
        }
    }

    /// <summary>True while a scripted helper expects world / strip picks for whitelist dismiss routing.</summary>
    public static bool UsesWorldWhitelistRouting =>
        Instance != null && Instance._activeUsesWorldWhitelistRouting;

    /// <summary>Whitelist ids configured on active helper.</summary>
    public static bool ActiveHelperUsesWorldWhitelist =>
        Instance != null &&
        Instance._activeDefinition != null &&
        Instance._activeDefinition.HasConfiguredWhitelistInteractIds();

    /// <summary>True when <paramref name="winnerCol"/> hits a subtree that carries a whitelist id listed on the active helper.</summary>
    public static bool IsWhitelistedWorldPick(Collider2D winnerCol)
    {
        if (!winnerCol || Instance == null || Instance._activeDefinition == null)
            return false;

        return Instance._activeDefinition.IsWhitelistedCollider(winnerCol);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HelperGameplayController] Duplicate instance — destroying this.", this);
            Destroy(this);
            return;
        }

        Instance = this;

        ToggleSettingsStore.Changed += OnToggleSettingsChanged;

        RegisterProgressKeys();
        LogMisconfiguredDefinitions();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LogMisconfiguredDefinitions()
    {
        if (definitions == null || definitions.Length == 0)
        {
            Debug.LogWarning(
                "[HelperGameplayController] No Helper Popup definitions assigned — map-entry helpers will not run. " +
                "Assign assets under Definitions on GameplayBootstrap (or wire your helper assets here).",
                this);
            return;
        }

        var seenIds = new HashSet<string>(System.StringComparer.Ordinal);

        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d || string.IsNullOrWhiteSpace(d.helperId))
                continue;

            string idTrimmed = d.helperId.Trim();

            if (!seenIds.Add(idTrimmed))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Duplicate helperId '{idTrimmed}' across definitions — dismissal will behave incorrectly.",
                    d);
            }

            if (d.activationTrigger == HelperActivationTrigger.FirstVisitMapNode &&
                string.IsNullOrWhiteSpace(d.requiredMapNodeId))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses First Visit Map Node but Required Map Node Id " +
                    $"is empty — set it to e.g. tutorial_1 or green_fields so it can ever match the active level.",
                    d);
            }

            if ((d.dismissModes & HelperDismissMode.InteractWhitelistDismiss) != 0 &&
                !d.HasConfiguredWhitelistInteractIds())
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses Interact Whitelist Dismiss but no whitelist ids are configured (entries or legacy list).",
                    d);
            }

            if (d.activationTrigger == HelperActivationTrigger.InventoryItemCountReached)
            {
                if (string.IsNullOrWhiteSpace(d.inventoryTriggerItemId))
                {
                    Debug.LogWarning(
                        $"[HelperGameplayController] Helper '{d.helperId}' uses Inventory Item Count Reached but Inventory Trigger Item Id is empty.",
                        d);
                }

                if (d.inventoryTriggerItemCount < 1)
                {
                    Debug.LogWarning(
                        $"[HelperGameplayController] Helper '{d.helperId}' uses Inventory Item Count Reached but count is below 1 — set Inventory Trigger Item Count (e.g. 3).",
                        d);
                }
            }

            if (d.activationTrigger == HelperActivationTrigger.QuestGatherObjectiveReady &&
                string.IsNullOrWhiteSpace(d.questGatherTriggerQuestId))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses Quest Gather Objective Ready but Quest Gather Trigger Quest Id is empty.",
                    d);
            }

            if (d.activationTrigger == HelperActivationTrigger.QuestRewardClaimed &&
                string.IsNullOrWhiteSpace(d.questRewardClaimedTriggerQuestId))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses Quest Reward Claimed but Quest Reward Claimed Trigger Quest Id is empty.",
                    d);
            }

            if (d.activationTrigger == HelperActivationTrigger.QuestAccepted &&
                string.IsNullOrWhiteSpace(d.questAcceptedTriggerQuestId))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses Quest Accepted but Quest Accepted Trigger Quest Id is empty.",
                    d);
            }

            if (d.activationTrigger == HelperActivationTrigger.SkillLevelReached &&
                d.skillLevelTriggerMinimumNewLevel < 2)
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses Skill Level Reached with Minimum New Level below 2 — clamp in inspector.",
                    d);
            }

            if (d.activationTrigger == HelperActivationTrigger.WorldItemDropped &&
                string.IsNullOrWhiteSpace(d.GetResolvedWorldDropTriggerItemId()))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses World Item Dropped but no item is set (assign World Drop Trigger Item or Item Id).",
                    d);
            }
        }
    }
#else
    private static void LogMisconfiguredDefinitions() { }
#endif

    private void OnDestroy()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;

        if (Instance == this)
        {
            StopStuckQueuedAdvanceCoroutine();
            _pendingHelperQueue.Clear();

            bool keepForRespawn =
                GameplayRespawnHelperPersistence.IsKeepHelperOverlayAcrossNextGameplayLoadFlagSet();

            bool hasSurface = (_overlayRoot && _overlayRoot.activeSelf) || _activeDefinition != null;
            if (hasSurface)
            {
                if (keepForRespawn)
                {
                    SaveMessageHistoryToPlayerPrefs();
                    if (_activeDefinition != null && !string.IsNullOrWhiteSpace(_activeDefinition.helperId))
                        s_resumeActiveHelperIdAfterSceneReload = _activeDefinition.helperId.Trim();
                }
                else
                    HideOverlayCompletely(true, purgeMessageHistory: false);
            }

            Instance = null;
        }

        UnsubscribeInventoryHelpers();
        UnsubscribeWorldDropHelpers();
        UnsubscribeQuestProgressHelpers();
        UnsubscribeSkillsHelpers();

        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= HandleLevelStarted;
    }

    private IEnumerator Start()
    {
        yield return HelperProgressStore.WaitUntilHydratedFromSave();

        EnsureViewBuilt();

        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        SubscribeInventoryHelpers();
        SubscribeWorldDropHelpers();
        SubscribeQuestProgressHelpers();
        SubscribeSkillsHelpers();

        if (HelpersPermittedBySettings())
            LoadMessageHistoryFromPlayerPrefs();

        bool resumedFromDeath =
            GameplayRespawnHelperPersistence.ConsumeKeepHelperOverlayAcrossNextGameplayLoad();

        GameplayLevelBootstrapper boots = GameplayLevelBootstrapper.Instance;
        if (boots == null)
        {
            Debug.LogWarning(
                "[HelperGameplayController] No GameplayLevelBootstrapper — First Visit helpers will never run.",
                this);
            if (resumedFromDeath)
                TryResumeHelperAfterDeathRespawnSceneReload();
            yield return null;
            TryPresentSessionHistoryWhenOverlayHidden();
            yield break;
        }

        boots.OnLevelStarted += HandleLevelStarted;

        // Execution order: our Start runs after Bootstrapper.Start — replay the active map once.
        if (boots.ActiveDefinition != null)
            HandleLevelStarted(boots.ActiveDefinition);

        if (resumedFromDeath)
            TryResumeHelperAfterDeathRespawnSceneReload();

        yield return null;
        TryPresentSessionHistoryWhenOverlayHidden();
    }

    private void RegisterProgressKeys()
    {
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
            HelperProgressStore.RegisterDefinition(definitions[i]);
    }

    private static bool HelpersPermittedBySettings() =>
        ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups);

    private static void ClearPersistedHelperMessageHistoryKey()
    {
        PlayerPrefs.DeleteKey(PersistedHelperHistoryKey);
        PlayerPrefs.Save();
    }

    private void SaveMessageHistoryToPlayerPrefs()
    {
        if (!HelpersPermittedBySettings())
            return;

        int n = _sessionMessageHistory.Count;
        if (n == 0)
        {
            ClearPersistedHelperMessageHistoryKey();
            return;
        }

        int cap = Mathf.Min(n, MaxPersistedHelperMessages);
        int start = n - cap;

        var dto = new PersistedHelperHistoryDto { items = new PersistedHelperSnapDto[cap] };

        for (int i = 0; i < cap; i++)
        {
            HelperDisplayedMessageSnap s = _sessionMessageHistory[start + i];
            dto.items[i] = new PersistedHelperSnapDto
            {
                helperId = s.HelperId ?? string.Empty,
                title = s.TitlePlain ?? string.Empty,
                body = s.BodyPlain ?? string.Empty,
            };
        }

        PlayerPrefs.SetString(PersistedHelperHistoryKey, JsonUtility.ToJson(dto));
        PlayerPrefs.Save();
    }

    private void LoadMessageHistoryFromPlayerPrefs()
    {
        _sessionMessageHistory.Clear();
        _historyViewIndex = 0;

        if (!HelpersPermittedBySettings() || !PlayerPrefs.HasKey(PersistedHelperHistoryKey))
            return;

        string json = PlayerPrefs.GetString(PersistedHelperHistoryKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        PersistedHelperHistoryDto dto = JsonUtility.FromJson<PersistedHelperHistoryDto>(json);
        if (dto?.items == null)
            return;

        for (int i = 0; i < dto.items.Length; i++)
        {
            PersistedHelperSnapDto row = dto.items[i];
            if (row == null)
                continue;

            string hid = row.helperId?.Trim() ?? string.Empty;

            _sessionMessageHistory.Add(new HelperDisplayedMessageSnap
            {
                HelperId = hid,
                TitlePlain = row.title ?? string.Empty,
                BodyPlain = row.body ?? string.Empty,
            });
        }

        if (_sessionMessageHistory.Count > 0)
            _historyViewIndex = _sessionMessageHistory.Count - 1;
    }

    /// <summary>
    /// When help was off (or history was cleared), the expanded panel has nothing to render.
    /// Seed a single history entry from <see cref="definitions"/>[0] (first assigned slot) so reopen/expand always shows content.
    /// Does not call <see cref="HelperProgressStore.MarkDismissed"/> — gameplay triggers for that helper id can still run later.
    /// </summary>
    private bool TrySeedSessionHistoryFromFirstDefinition()
    {
        if (_sessionMessageHistory.Count > 0)
            return false;

        HelperPopupDefinition def = GetWelcomeSeedDefinition();
        if (!def)
            return false;

        CharacterStats stats = ResolvePlayerCharacterStats();
        string substitutedTitle =
            HelperPopupDefinition.ApplyRuntimeSubstitutions(def.title ?? string.Empty, stats);
        string substitutedBody =
            HelperPopupDefinition.ApplyRuntimeSubstitutions(def.bodyText ?? string.Empty, stats);

        if (string.IsNullOrWhiteSpace(substitutedTitle) && string.IsNullOrWhiteSpace(substitutedBody))
            return false;

        _sessionMessageHistory.Add(new HelperDisplayedMessageSnap
        {
            HelperId = string.IsNullOrWhiteSpace(def.helperId) ? string.Empty : def.helperId.Trim(),
            TitlePlain = substitutedTitle.Trim(),
            BodyPlain = substitutedBody,
        });
        _historyViewIndex = _sessionMessageHistory.Count - 1;
        SaveMessageHistoryToPlayerPrefs();
        return true;
    }

    /// <summary>Prefer index 0; if unassigned, use the first non-null definition in the array.</summary>
    private HelperPopupDefinition GetWelcomeSeedDefinition()
    {
        if (definitions == null || definitions.Length == 0)
            return null;
        if (definitions[0])
            return definitions[0];
        for (int i = 1; i < definitions.Length; i++)
        {
            if (definitions[i])
                return definitions[i];
        }

        return null;
    }

    private void TryPresentSessionHistoryWhenOverlayHidden()
    {
        if (!HelpersPermittedBySettings() || _sessionMessageHistory.Count == 0)
            return;

        if (_overlayRoot != null && _overlayRoot.activeSelf)
            return;

        if (_activeDefinition != null)
            return;

        if (!ShouldAutoPresentLatestHistoryEntry())
            return;

        PresentSessionHistoryOverlayExpanded();
    }

    /// <summary>
    /// Do not reopen the overlay for a session-history tip the player already dismissed (same helper id).
    /// </summary>
    private bool ShouldAutoPresentLatestHistoryEntry()
    {
        if (_sessionMessageHistory.Count == 0)
            return false;

        // If this save has never seen/dismissed any helper yet, treat history as stale/noise.
        if (!HelperProgressStore.HasAnyDismissed())
            return false;

        int idx = _sessionMessageHistory.Count - 1;
        HelperDisplayedMessageSnap snap = _sessionMessageHistory[idx];
        if (!string.IsNullOrWhiteSpace(snap.HelperId) && HelperProgressStore.WasDismissed(snap.HelperId))
            return false;

        return true;
    }

    private void PresentSessionHistoryOverlayExpanded()
    {
        EnsureViewBuilt();
        if (_overlayRoot == null || _sessionMessageHistory.Count == 0)
            return;

        _whitelistStagedModalReleased = false;
        _activeDefinition = null;
        _activeUsesWorldWhitelistRouting = false;

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();

        ApplyLoadedHelperLayout();
        TransitionToExpandedPresentationLayout();
        RaiseWhitelistUiTargetCanvasesForActiveOverlay();

        if (_helperCloseButtonRoot)
            _helperCloseButtonRoot.SetActive(true);

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);

        _historyViewIndex = Mathf.Clamp(_sessionMessageHistory.Count - 1, 0, _sessionMessageHistory.Count - 1);
        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: false);

        RefreshWhitelistPresentationEmphasis();
        RefreshWorldWhitelistRoutingFlag();
        ApplyDarkenModalPresentation();
        ApplyHelperTipTextScale();
    }

    private HelperPopupDefinition FindDefinitionByHelperId(string helperId)
    {
        if (string.IsNullOrWhiteSpace(helperId) || definitions == null)
            return null;

        string t = helperId.Trim();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (d && string.Equals(d.helperId?.Trim(), t, StringComparison.OrdinalIgnoreCase))
                return d;
        }

        return null;
    }

    private void TryResumeHelperAfterDeathRespawnSceneReload()
    {
        string hid = s_resumeActiveHelperIdAfterSceneReload;
        s_resumeActiveHelperIdAfterSceneReload = null;

        if (!HelpersPermittedBySettings())
        {
            if (_overlayRoot && _overlayRoot.activeSelf)
                HideOverlayCompletely(true, purgeMessageHistory: false);
            return;
        }

        if (_overlayRoot == null)
            return;

        HelperPopupWindow host = _overlayRoot.GetComponent<HelperPopupWindow>();
        RectTransform parentRt = ResolveOverlayParent();
        if (host && parentRt)
            host.EnsureUnderWindowsArea(parentRt);

        EnsureHelperWindowFocus();

        if (!_overlayRoot.activeSelf)
        {
            if (_sessionMessageHistory.Count > 0)
                PresentSessionHistoryOverlayExpanded();
            else
                HideOverlayCompletely(true, purgeMessageHistory: false);

            ApplyHelperTipTextScale();
            return;
        }

        HelperPopupDefinition def =
            !string.IsNullOrWhiteSpace(hid) ? FindDefinitionByHelperId(hid) : null;

        int idx = -1;
        if (!string.IsNullOrWhiteSpace(hid))
        {
            for (int i = _sessionMessageHistory.Count - 1; i >= 0; i--)
            {
                if (string.Equals(
                        _sessionMessageHistory[i].HelperId?.Trim(),
                        hid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
        }

        if (def != null && idx >= 0)
        {
            _activeDefinition = def;
            _activeUsesWorldWhitelistRouting = false;
            _historyViewIndex = idx;

            RestoreWhitelistUiTargetCanvases();
            StopBodyTypewriterAndClear();

            ApplyLoadedHelperLayout();
            TransitionToExpandedPresentationLayout();
            RaiseWhitelistUiTargetCanvasesForActiveOverlay();

            if (_helperCloseButtonRoot)
                _helperCloseButtonRoot.SetActive(def.showCloseButton);

            _overlayRoot.transform.SetAsLastSibling();
            _overlayRoot.SetActive(true);

            ApplyDisplayedHistoryIndexToPanel(idx, startTypewriterFresh: false);

            RefreshWhitelistPresentationEmphasis();
            ApplyInventoryLootHighlightFromActiveDefinition();
            RefreshWorldWhitelistRoutingFlag();
            ApplyDarkenModalPresentation();
            MaybeStartStuckQueuedAdvanceWatcher();
            HideHelperNewBadge();
        }
        else if (_sessionMessageHistory.Count > 0)
        {
            PresentSessionHistoryOverlayExpanded();
        }
        else
        {
            HideOverlayCompletely(true, purgeMessageHistory: false);
        }

        ApplyHelperTipTextScale();
    }

    private float HelperTipTextSizeMultiplierClamped() =>
        Mathf.Clamp(helperTipTextSizeMultiplier, 0.5f, 2f);

    private void EnsureHelperAuthoredFontBasesForCurrentOverlay()
    {
        if (_overlayRoot == null)
            return;

        int id = _overlayRoot.GetInstanceID();
        float tip = HelperTipTextSizeMultiplierClamped();

        if (_helperFontBasesOverlayInstanceId == id && Mathf.Approximately(_lastAppliedHelperTipMul, tip))
            return;

        float prevTip = 1f;
        if (_helperFontBasesOverlayInstanceId == id && _lastAppliedHelperTipMul > 0.01f)
            prevTip = _lastAppliedHelperTipMul;

        float invMul = 1f / Mathf.Max(0.01f, prevTip);

        if (_chromeStripTitleText)
            _baseFontChromeStripTitle = _chromeStripTitleText.fontSize * invMul;
        else
            _baseFontChromeStripTitle = 16f;

        if (_titleText)
            _baseFontTitle = _titleText.fontSize * invMul;
        else
            _baseFontTitle = 19f;

        if (_bodyText)
            _baseFontBody = _bodyText.fontSize * invMul;
        else
            _baseFontBody = 16f;
        if (_minimizeExpandGlyphTmp)
            _baseFontMinimizeGlyph = _minimizeExpandGlyphTmp.fontSize * invMul;
        else
            _baseFontMinimizeGlyph = 18f;

        TMP_Text closeTmp = _helperCloseButtonRoot ? FindTmpOnButton(_helperCloseButtonRoot) : null;
        _baseFontClose = closeTmp ? closeTmp.fontSize * invMul : 16f;

        if (_prevHistoryButton)
        {
            TMP_Text pt = FindTmpOnButton(_prevHistoryButton.gameObject);
            _baseFontPrevNav = pt ? pt.fontSize * invMul : 18f;
        }
        else
            _baseFontPrevNav = 18f;

        if (_nextHistoryButton)
        {
            TMP_Text nt = FindTmpOnButton(_nextHistoryButton.gameObject);
            _baseFontNextNav = nt ? nt.fontSize * invMul : 18f;
        }
        else
            _baseFontNextNav = 18f;

        _helperFontBasesOverlayInstanceId = id;
    }

    private void ApplyHelperTipTextScale()
    {
        if (_overlayRoot == null)
            return;

        EnsureHelperAuthoredFontBasesForCurrentOverlay();

        float mul = HelperTipTextSizeMultiplierClamped();

        if (_chromeStripTitleText)
            _chromeStripTitleText.fontSize = _baseFontChromeStripTitle * mul;
        if (_titleText)
            _titleText.fontSize = _baseFontTitle * mul;
        if (_bodyText)
            _bodyText.fontSize = _baseFontBody * mul;
        if (_minimizeExpandGlyphTmp)
            _minimizeExpandGlyphTmp.fontSize = _baseFontMinimizeGlyph * mul;

        if (_helperCloseButtonRoot)
        {
            TMP_Text closeTmp = FindTmpOnButton(_helperCloseButtonRoot);
            if (closeTmp)
                closeTmp.fontSize = _baseFontClose * mul;
        }

        if (_prevHistoryButton)
        {
            TMP_Text pt = FindTmpOnButton(_prevHistoryButton.gameObject);
            if (pt)
                pt.fontSize = _baseFontPrevNav * mul;
        }

        if (_nextHistoryButton)
        {
            TMP_Text nt = FindTmpOnButton(_nextHistoryButton.gameObject);
            if (nt)
                nt.fontSize = _baseFontNextNav * mul;
        }

        RefreshHelperBodyScrollLayout(scrollToTop: false);

        _lastAppliedHelperTipMul = mul;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        helperTipTextSizeMultiplier = Mathf.Clamp(helperTipTextSizeMultiplier, 0.5f, 2f);
        if (Application.isPlaying && Instance == this && _overlayRoot != null)
            ApplyHelperTipTextScale();
    }
#endif

    private void StopStuckQueuedAdvanceCoroutine()
    {
        if (_stuckQueuedAdvanceCo == null)
            return;

        StopCoroutine(_stuckQueuedAdvanceCo);
        _stuckQueuedAdvanceCo = null;
    }

    private bool IsHelperIdAlreadyPendingOrActive(string helperId)
    {
        if (string.IsNullOrWhiteSpace(helperId))
            return false;

        string id = helperId.Trim();
        if (_activeDefinition &&
            !string.IsNullOrWhiteSpace(_activeDefinition.helperId) &&
            string.Equals(_activeDefinition.helperId.Trim(), id, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (HelperPopupDefinition q in _pendingHelperQueue)
        {
            if (q &&
                !string.IsNullOrWhiteSpace(q.helperId) &&
                string.Equals(q.helperId.Trim(), id, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int CompareHelperOrder(HelperPopupDefinition a, HelperPopupDefinition b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int c = a.priority.CompareTo(b.priority);
        return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
    }

    private void EnqueueDeferredSortedCandidates(List<HelperPopupDefinition> sorted, int skipFirstSorted = 1)
    {
        if (sorted == null || sorted.Count <= skipFirstSorted)
            return;

        for (int i = skipFirstSorted; i < sorted.Count; i++)
        {
            HelperPopupDefinition d = sorted[i];
            if (!d || string.IsNullOrWhiteSpace(d.helperId))
                continue;

            if (HelperProgressStore.WasDismissed(d.helperId))
                continue;

            if (IsHelperIdAlreadyPendingOrActive(d.helperId))
                continue;

            _pendingHelperQueue.Enqueue(d);
        }
    }

    private void PrunePendingQueueToTrigger(HelperActivationTrigger trigger)
    {
        if (_pendingHelperQueue.Count == 0)
            return;

        var kept = new Queue<HelperPopupDefinition>(_pendingHelperQueue.Count);
        while (_pendingHelperQueue.Count > 0)
        {
            HelperPopupDefinition q = _pendingHelperQueue.Dequeue();
            if (!q)
                continue;
            if (q.activationTrigger != trigger)
                continue;
            kept.Enqueue(q);
        }

        while (kept.Count > 0)
            _pendingHelperQueue.Enqueue(kept.Dequeue());
    }

    private void TryDrainPendingHelperQueue()
    {
        if (!HelpersPermittedBySettings() || _activeDefinition != null)
            return;

        StopStuckQueuedAdvanceCoroutine();

        while (_pendingHelperQueue.Count > 0)
        {
            HelperPopupDefinition next = _pendingHelperQueue.Dequeue();
            if (!next || string.IsNullOrWhiteSpace(next.helperId))
                continue;

            if (HelperProgressStore.WasDismissed(next.helperId))
                continue;

            ShowPopup(next);
            return;
        }
    }

    private void MaybeStartStuckQueuedAdvanceWatcher()
    {
        StopStuckQueuedAdvanceCoroutine();

        if (autoSkipStuckHelperWhenQueueHasMoreSeconds <= 0f ||
            _pendingHelperQueue.Count == 0 ||
            _activeDefinition == null ||
            !gameObject.activeInHierarchy)
            return;

        _stuckQueuedAdvanceCo = StartCoroutine(CoAdvanceQueuedIfStuck(_activeDefinition));
    }

    private IEnumerator CoAdvanceQueuedIfStuck(HelperPopupDefinition startDef)
    {
        float wait = Mathf.Max(0.01f, autoSkipStuckHelperWhenQueueHasMoreSeconds);
        yield return new WaitForSeconds(wait);

        _stuckQueuedAdvanceCo = null;

        if (!HelpersPermittedBySettings())
            yield break;

        if (_activeDefinition != startDef || startDef == null)
            yield break;

        if (_pendingHelperQueue.Count == 0)
            yield break;

        HelperProgressStore.MarkDismissed(startDef.helperId);

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        _whitelistStagedModalReleased = false;
        _activeDefinition = null;
        _activeUsesWorldWhitelistRouting = false;

        TryDrainPendingHelperQueue();

        if (_activeDefinition == null && _overlayRoot && _overlayRoot.activeSelf)
            HideOverlayCompletely(true, purgeMessageHistory: false);
    }

    private void HandleLevelStarted(MapNodeDefinition node)
    {
        if (!HelpersPermittedBySettings() ||
            node == null ||
            definitions == null ||
            definitions.Length == 0)
            return;

        bool isFirstVisitThisEntry = GameplayLevelBootstrapper.Instance != null &&
            GameplayLevelBootstrapper.Instance.ActiveDefinition == node &&
            GameplayLevelBootstrapper.Instance.ActiveLevelWasFirstVisit;

        StartCoroutine(EvaluateMapEntryNextFrame(node, isFirstVisitThisEntry));
    }

    /// <summary>
    /// When &quot;Show help popups&quot; is turned off: hide any overlay and stop helper logic.
    /// When turned back on after being off: clear per-save dismissed helpers so tips can show again,
    /// then show history (if any) even when previously closed via X.
    /// </summary>
    private void OnToggleSettingsChanged(ToggleSettingId id, bool enabled)
    {
        if (id == ToggleSettingId.ExpandStripBackground)
        {
            RefreshDimmerLayoutForExpandSetting();
            return;
        }

        if (id != ToggleSettingId.ShowHelpPopups)
            return;

        if (enabled)
        {
            LoadMessageHistoryFromPlayerPrefs();
            if (!HelperProgressStore.HasAnyDismissed())
            {
                // No known previously seen helpers for this save: avoid reviving stale history text.
                _sessionMessageHistory.Clear();
                _historyViewIndex = 0;
                ClearPersistedHelperMessageHistoryKey();
                TrySeedSessionHistoryFromFirstDefinition();
                if (_sessionMessageHistory.Count > 0)
                    PresentSessionHistoryOverlayExpanded();
                else
                    PresentMinimizedAwaitingFuturePopups();
                return;
            }
            // Re-enabling help should not resurrect stale history popups.
            // If there is no saved history yet, seed element 0 so expand/minimize is never an empty panel.
            if (_sessionMessageHistory.Count == 0)
                TrySeedSessionHistoryFromFirstDefinition();

            if ((_overlayRoot == null || !_overlayRoot.activeSelf) &&
                _activeDefinition == null)
                PresentMinimizedAwaitingFuturePopups();

            return;
        }

        StopStuckQueuedAdvanceCoroutine();
        _pendingHelperQueue.Clear();

        bool hasSurface = (_overlayRoot && _overlayRoot.activeSelf) || _activeDefinition != null;
        if (hasSurface)
            HideOverlayCompletely(true, purgeMessageHistory: false);
        else
            SyncMovementLockFromSettings();
    }

    private void PresentMinimizedAwaitingFuturePopups()
    {
        EnsureViewBuilt();
        if (_overlayRoot == null)
            return;

        _activeDefinition = null;
        _activeUsesWorldWhitelistRouting = false;
        _whitelistStagedModalReleased = false;

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        ApplyLoadedHelperLayout();
        TransitionToCollapsedStripeLayout();
        if (_helperCloseButtonRoot)
            _helperCloseButtonRoot.SetActive(true);

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);
        ApplyDarkenModalPresentation();
        ApplyHelperTipTextScale();
    }

    private IEnumerator EvaluateMapEntryNextFrame(MapNodeDefinition node, bool isFirstVisitThisEntry)
    {
        yield return null;

        if (!HelpersPermittedBySettings())
            yield break;

        string enteredId = string.IsNullOrWhiteSpace(node.nodeId) ? null : node.nodeId.Trim();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d || string.IsNullOrWhiteSpace(d.helperId))
                continue;

            switch (d.activationTrigger)
            {
                case HelperActivationTrigger.FirstVisitMapNode when enteredId != null &&
                    isFirstVisitThisEntry &&
                    !string.IsNullOrWhiteSpace(d.requiredMapNodeId) &&
                    string.Equals(d.requiredMapNodeId.Trim(), enteredId,
                        System.StringComparison.OrdinalIgnoreCase):

                    if (!HelperProgressStore.WasDismissed(d.helperId))
                        candidates.Add(d);
                    break;

                case HelperActivationTrigger.None:
                default:
                    break;
            }
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }

        EvaluateInventoryItemCountReachedHelpers();
        EvaluateQuestGatherProgressHelpers();
        EvaluateQuestRewardClaimedHelpers();
        EvaluateQuestAcceptedHelpers();

        TryPresentSessionHistoryWhenOverlayHidden();
    }

    private void SubscribeInventoryHelpers()
    {
        UnsubscribeInventoryHelpers();

        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        Inventory inv = _player ? _player.GetComponent<Inventory>() : null;
        _inventoryForHelpers = inv;
        if (_inventoryForHelpers == null)
            return;

        _lastSeenInventoryTotalUnitsBeforeChange = CountTotalInventoryUnits(_inventoryForHelpers);

        _inventoryForHelpers.OnInventoryChanged += HandleInventoryChangedForHelpers;
    }

    private void UnsubscribeInventoryHelpers()
    {
        if (_inventoryForHelpers == null)
            return;

        _inventoryForHelpers.OnInventoryChanged -= HandleInventoryChangedForHelpers;
        _inventoryForHelpers = null;
        _inventoryHelperEvalDeferred = false;
    }

    private void SubscribeWorldDropHelpers()
    {
        UnsubscribeWorldDropHelpers();
        ItemDrop.OnWorldPickupSpawned += HandleWorldPickupSpawnedForHelpers;
    }

    private void UnsubscribeWorldDropHelpers()
    {
        ItemDrop.OnWorldPickupSpawned -= HandleWorldPickupSpawnedForHelpers;
    }

    private void HandleWorldPickupSpawnedForHelpers(string spawnedItemId)
    {
        EvaluateWorldItemDroppedHelpers(spawnedItemId);
    }

    private void EvaluateWorldItemDroppedHelpers(string spawnedItemId)
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        if (string.IsNullOrWhiteSpace(spawnedItemId))
            return;

        string remappedSpawned = Inventory.RemapLegacyItemId(spawnedItemId.Trim());
        MapNodeDefinition activeMap = ResolveActiveMapForHelpers();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d ||
                string.IsNullOrWhiteSpace(d.helperId) ||
                d.activationTrigger != HelperActivationTrigger.WorldItemDropped ||
                HelperProgressStore.WasDismissed(d.helperId))
                continue;

            string want = d.GetResolvedWorldDropTriggerItemId();
            if (string.IsNullOrWhiteSpace(want))
                continue;

            want = Inventory.RemapLegacyItemId(want);
            if (!string.Equals(want, remappedSpawned, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            candidates.Add(d);
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private void HandleInventoryChangedForHelpers()
    {
        _inventoryHelperEvalDeferred = true;
    }

    private void RunDeferredInventoryHelperEvaluation()
    {
        if (!_inventoryHelperEvalDeferred)
            return;
        _inventoryHelperEvalDeferred = false;

        if (!HelpersPermittedBySettings() ||
            _inventoryForHelpers == null ||
            definitions == null ||
            definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        int total = CountTotalInventoryUnits(_inventoryForHelpers);
        int prev = _lastSeenInventoryTotalUnitsBeforeChange;

        bool becameNonEmptyFromEmpty = prev <= 0 && total > 0;
        _lastSeenInventoryTotalUnitsBeforeChange = total;

        if (becameNonEmptyFromEmpty)
            EvaluateFirstBagGainFromZeroHelpers();

        EvaluateInventoryItemCountReachedHelpers();
        EvaluateQuestGatherProgressHelpers();
        EvaluateQuestRewardClaimedHelpers();
        EvaluateQuestAcceptedHelpers();
    }

    private void SubscribeQuestProgressHelpers()
    {
        UnsubscribeQuestProgressHelpers();

        _questProgressForHelpers =
            QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);

        if (_questProgressForHelpers == null)
            return;

        _questProgressForHelpers.ProgressChanged += HandleQuestProgressForHelpers;
    }

    private void UnsubscribeQuestProgressHelpers()
    {
        if (_questProgressForHelpers == null)
            return;

        _questProgressForHelpers.ProgressChanged -= HandleQuestProgressForHelpers;
        _questProgressForHelpers = null;
    }

    private void HandleQuestProgressForHelpers()
    {
        if (!HelpersPermittedBySettings())
            return;

        EvaluateQuestGatherProgressHelpers();
        EvaluateQuestRewardClaimedHelpers();
        EvaluateQuestAcceptedHelpers();
    }

    private void SubscribeSkillsHelpers()
    {
        UnsubscribeSkillsHelpers();

        _skillsManagerForHelpers =
            SkillsManager.Instance ??
            FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

        if (_skillsManagerForHelpers == null)
            return;

        _skillsManagerForHelpers.OnLevelUp += HandleSkillLevelUpForHelpers;
    }

    private void UnsubscribeSkillsHelpers()
    {
        if (_skillsManagerForHelpers == null)
            return;

        _skillsManagerForHelpers.OnLevelUp -= HandleSkillLevelUpForHelpers;
        _skillsManagerForHelpers = null;
    }

    private void HandleSkillLevelUpForHelpers(SkillType skillType, int newLevel)
    {
        if (!HelpersPermittedBySettings())
            return;

        EvaluateSkillLevelReachedHelpers(skillType, newLevel);
    }

    private void EvaluateFirstBagGainFromZeroHelpers()
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0)
            return;

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d || string.IsNullOrWhiteSpace(d.helperId))
                continue;

            switch (d.activationTrigger)
            {
                case HelperActivationTrigger.FirstBagGainFromZero
                    when !HelperProgressStore.WasDismissed(d.helperId):

                    candidates.Add(d);
                    break;

                default:
                    break;
            }
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private void EvaluateInventoryItemCountReachedHelpers()
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0 ||
            _inventoryForHelpers == null)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        MapNodeDefinition activeMap = ResolveActiveMapForHelpers();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d ||
                string.IsNullOrWhiteSpace(d.helperId) ||
                d.activationTrigger != HelperActivationTrigger.InventoryItemCountReached ||
                HelperProgressStore.WasDismissed(d.helperId))
                continue;

            if (string.IsNullOrWhiteSpace(d.inventoryTriggerItemId) || d.inventoryTriggerItemCount < 1)
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            int have = ResolveLiveGatherItemAmount(d.inventoryTriggerItemId);
            if (have < d.inventoryTriggerItemCount)
                continue;

            candidates.Add(d);
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private void EvaluateQuestGatherProgressHelpers()
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        QuestProgressManager qm =
            QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qm == null)
            return;

        MapNodeDefinition activeMap = ResolveActiveMapForHelpers();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d ||
                string.IsNullOrWhiteSpace(d.helperId) ||
                d.activationTrigger != HelperActivationTrigger.QuestGatherObjectiveReady ||
                HelperProgressStore.WasDismissed(d.helperId))
                continue;

            if (string.IsNullOrWhiteSpace(d.questGatherTriggerQuestId))
                continue;

            QuestDefinition q = qm.GetQuestDefinition(d.questGatherTriggerQuestId.Trim());
            if (!q || q.objectiveKind != QuestObjectiveKind.GatherItem || q.targetCount <= 0)
                continue;

            if (!qm.IsQuestAccepted(q))
                continue;

            if (qm.IsRewardClaimed(q.questId))
                continue;

            if (qm.GetDisplayProgress(q) < q.targetCount)
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            candidates.Add(d);
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private void EvaluateSkillLevelReachedHelpers(SkillType firedSkill, int newLevel)
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        MapNodeDefinition activeMap = ResolveActiveMapForHelpers();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d ||
                string.IsNullOrWhiteSpace(d.helperId) ||
                d.activationTrigger != HelperActivationTrigger.SkillLevelReached ||
                HelperProgressStore.WasDismissed(d.helperId))
                continue;

            if (d.skillLevelTriggerSkill != HelperSkillLevelTriggerOption.AnySkill &&
                (SkillType)d.skillLevelTriggerSkill != firedSkill)
                continue;

            int minLv = Mathf.Max(2, d.skillLevelTriggerMinimumNewLevel);
            if (newLevel < minLv)
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            candidates.Add(d);
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private void EvaluateQuestRewardClaimedHelpers()
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        QuestProgressManager qm =
            QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qm == null)
            return;

        MapNodeDefinition activeMap = ResolveActiveMapForHelpers();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d ||
                string.IsNullOrWhiteSpace(d.helperId) ||
                d.activationTrigger != HelperActivationTrigger.QuestRewardClaimed ||
                HelperProgressStore.WasDismissed(d.helperId))
                continue;

            if (string.IsNullOrWhiteSpace(d.questRewardClaimedTriggerQuestId))
                continue;

            string qid = d.questRewardClaimedTriggerQuestId.Trim();
            if (!qm.IsRewardClaimed(qid))
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            candidates.Add(d);
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            PrunePendingQueueToTrigger(candidates[0].activationTrigger);
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private void EvaluateQuestAcceptedHelpers()
    {
        if (!HelpersPermittedBySettings() ||
            definitions == null ||
            definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        QuestProgressManager qm =
            QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qm == null)
            return;

        MapNodeDefinition activeMap = ResolveActiveMapForHelpers();

        var candidates = new List<HelperPopupDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d ||
                string.IsNullOrWhiteSpace(d.helperId) ||
                d.activationTrigger != HelperActivationTrigger.QuestAccepted ||
                HelperProgressStore.WasDismissed(d.helperId))
                continue;

            if (string.IsNullOrWhiteSpace(d.questAcceptedTriggerQuestId))
                continue;

            string qid = d.questAcceptedTriggerQuestId.Trim();
            QuestDefinition qdef = qm.GetQuestDefinition(qid);
            if (qdef == null || !qm.IsQuestAccepted(qdef))
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            candidates.Add(d);
        }

        candidates.Sort(CompareHelperOrder);

        if (candidates.Count > 0)
        {
            EnqueueDeferredSortedCandidates(candidates);
            ShowPopup(candidates[0]);
        }
    }

    private int ResolveLiveGatherItemAmount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        string id = itemId.Trim();
        QuestProgressManager qm =
            QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qm != null)
            return qm.GetGatherItemCountLive(id);

        return _inventoryForHelpers ? _inventoryForHelpers.GetTotalAmount(id) : 0;
    }

    private static MapNodeDefinition ResolveActiveMapForHelpers()
    {
        GameplayLevelBootstrapper boots = GameplayLevelBootstrapper.Instance;
        if (boots != null && boots.ActiveDefinition != null)
            return boots.ActiveDefinition;

        return ActiveLevelContext.Current;
    }

    private static bool MapNodeMatchesOptional(HelperPopupDefinition d, MapNodeDefinition activeMap)
    {
        if (string.IsNullOrWhiteSpace(d.requiredMapNodeId))
            return true;

        string required = d.requiredMapNodeId.Trim();
        string activeId = activeMap != null && !string.IsNullOrWhiteSpace(activeMap.nodeId)
            ? activeMap.nodeId.Trim()
            : null;

        return activeId != null &&
               string.Equals(activeId, required, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int CountTotalInventoryUnits(Inventory inv)
    {
        if (!inv || inv.SlotCount <= 0)
            return 0;

        int total = 0;
        int n = inv.SlotCount;

        for (int i = 0; i < n; i++)
        {
            Inventory.Slot slot = inv.GetSlot(i);

            if (slot.IsEmpty)
                continue;

            total += slot.amount;
        }

        return total;
    }

    private void RaiseWhitelistUiTargetCanvasesForActiveOverlay()
    {
        RestoreWhitelistUiTargetCanvases();

        if (_activeDefinition == null ||
            !IsHelperExpandedPresentation() ||
            !_activeDefinition.HasConfiguredWhitelistInteractIds())
            return;

        int overlayTopSort = canvasSortOrder + panelSortDelta;

        int whitelistSort =
            Mathf.Clamp(overlayTopSort + Mathf.Max(1, whitelistUiCanvasSortBeyondPanel), -30000, 32760);

        HelperWhitelistUiInteractTarget[] markers =
            FindObjectsByType<HelperWhitelistUiInteractTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int bump = 0;
        for (int i = 0; i < markers.Length; i++)
        {
            HelperWhitelistUiInteractTarget marker = markers[i];
            if (!marker || !_activeDefinition.MatchesWhitelistId(marker.InteractionId))
                continue;

            ElevateWhitelistUiInteractTarget(marker, whitelistSort + bump);
            bump++;
        }
    }

    /// <summary>
    /// Puts a nested Canvas + raycaster on the whitelist marker so it sorts above the helper modal without retargeting the whole strip root.
    /// </summary>
    private void ElevateWhitelistUiInteractTarget(HelperWhitelistUiInteractTarget marker, int sortingOrderValue)
    {
        Canvas c = marker.GetComponent<Canvas>();
        bool createdCanvas = false;

        if (!c)
        {
            c = marker.gameObject.AddComponent<Canvas>();
            createdCanvas = true;
        }

        GraphicRaycaster gr = marker.GetComponent<GraphicRaycaster>();
        bool createdGr = false;

        if (!gr)
        {
            gr = marker.gameObject.AddComponent<GraphicRaycaster>();
            createdGr = true;
        }

        _whitelistUiElevations.Add(new SavedWhitelistUiElevation
        {
            CanvasComp = c,
            CanvasCreatedDuringHelperOverlay = createdCanvas,
            GraphicRaycasterCreatedDuringHelperOverlay = createdGr,
            BackupHadOverrideSorting = c.overrideSorting,
            BackupSortOrder = c.sortingOrder,
        });

        c.overrideSorting = true;
        c.sortingOrder = sortingOrderValue;
    }

    private void RestoreWhitelistUiTargetCanvases()
    {
        for (int i = 0; i < _whitelistUiElevations.Count; i++)
        {
            SavedWhitelistUiElevation snap = _whitelistUiElevations[i];
            Canvas c = snap.CanvasComp;
            if (!c)
                continue;

            if (snap.GraphicRaycasterCreatedDuringHelperOverlay)
            {
                GraphicRaycaster gr = c.gameObject.GetComponent<GraphicRaycaster>();
                if (gr)
                    Destroy(gr);
            }

            if (snap.CanvasCreatedDuringHelperOverlay)
            {
                Destroy(c);
            }
            else
            {
                c.overrideSorting = snap.BackupHadOverrideSorting;
                c.sortingOrder = snap.BackupSortOrder;
            }
        }

        _whitelistUiElevations.Clear();
    }

    /// <summary>
    /// After helper whitelist emphasis, snap strip draw state back to normal (see
    /// <see cref="NormalizeAllWhitelistMarkersStripDrawOrder"/>). NPC markers only get canvases reset when
    /// clearly inflated; gatherables get a full world-canvas + main-sprite reset to match prefab defaults.
    /// </summary>
    private const int WorldWhitelistMarkerCanvasInflatedSortThreshold = 9000;

    private static void NormalizeWhitelistMarkerStripDrawOrder(HelperWhitelistInteractTarget marker)
    {
        if (!marker)
            return;

        int uiLayerId = SortingLayer.NameToID("UI");
        int interactablesLayerId = SortingLayer.NameToID("Interactables");
        bool isResourceGatherable = marker.TryGetComponent(out ResourceNode _);

        Canvas[] canvases = marker.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (!c || c.renderMode != RenderMode.WorldSpace)
                continue;

            if (isResourceGatherable)
            {
                c.overrideSorting = false;
                c.sortingLayerID = 0;
                c.sortingOrder = 0;
                continue;
            }

            bool inflatedSort = c.overrideSorting && c.sortingOrder >= WorldWhitelistMarkerCanvasInflatedSortThreshold;
            bool uiSortingLayer = c.sortingLayerID == uiLayerId;
            if (!inflatedSort && !uiSortingLayer)
                continue;

            c.overrideSorting = false;
            c.sortingLayerID = 0;
            c.sortingOrder = 0;
        }

        if (!isResourceGatherable)
            return;

        SpriteRenderer[] srs = marker.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (!sr ||
                sr.gameObject.name.StartsWith(ResourceNode.RuntimeDepletionOverlayPrefix, StringComparison.Ordinal))
                continue;

            sr.sortingLayerID = interactablesLayerId;
            sr.sortingOrder = 0;
        }
    }

    /// <summary>
    /// Clears whitelist glow-related draw/sort drift on every world marker — must not depend on
    /// <see cref="_activeDefinition"/> (history overlay and other paths clear glow after def is already null).
    /// </summary>
    private static void NormalizeAllWhitelistMarkersStripDrawOrder()
    {
        HelperWhitelistInteractTarget[] markers =
            FindObjectsByType<HelperWhitelistInteractTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i])
                NormalizeWhitelistMarkerStripDrawOrder(markers[i]);
        }
    }

    /// <summary>
    /// When a new helper fires while another is still active (player has not finished the whitelist step),
    /// mark the outgoing tip dismissed, clear its presentation, and drop same-pass queue so the newest trigger wins.
    /// Prior messages stay in session history for the footer history buttons.
    /// </summary>
    private void SupersedeActiveHelperForIncoming(HelperPopupDefinition incoming)
    {
        HelperPopupDefinition outgoing = _activeDefinition;
        if (outgoing == null || incoming == null || outgoing == incoming)
            return;

        StopStuckQueuedAdvanceCoroutine();
        _pendingHelperQueue.Clear();

        if (!string.IsNullOrWhiteSpace(outgoing.helperId))
            HelperProgressStore.MarkDismissed(outgoing.helperId.Trim());

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();
        _activeUsesWorldWhitelistRouting = false;
        _whitelistStagedModalReleased = false;
    }

    private void ShowPopup(HelperPopupDefinition def)
    {
        if (def == null ||
            !ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
            return;

        if (_activeDefinition == def)
            return;

        if (_activeDefinition != null)
            SupersedeActiveHelperForIncoming(def);

        // Fail-safe: once a helper has been shown, treat it as consumed for this save
        // so repeating gameplay conditions cannot retrigger the same helper id.
        if (!string.IsNullOrWhiteSpace(def.helperId))
            HelperProgressStore.MarkDismissed(def.helperId.Trim());

        _activeDefinition = def;
        _whitelistStagedModalReleased = false;

        EnsureViewBuilt();
        if (_overlayRoot == null)
        {
            _activeDefinition = null;
            SyncMovementLockFromSettings();
            return;
        }

        CharacterStats stats = ResolvePlayerCharacterStats();
        string substitutedTitle = HelperPopupDefinition.ApplyRuntimeSubstitutions(def.title ?? string.Empty, stats);
        string substitutedBody = HelperPopupDefinition.ApplyRuntimeSubstitutions(def.bodyText ?? string.Empty, stats);

        _sessionMessageHistory.Add(new HelperDisplayedMessageSnap
        {
            HelperId = string.IsNullOrWhiteSpace(def.helperId) ? string.Empty : def.helperId.Trim(),
            TitlePlain = substitutedTitle.Trim(),
            BodyPlain = substitutedBody,
        });
        _historyViewIndex = _sessionMessageHistory.Count - 1;

        // Keep saved position/size, but always open expanded for a brand-new helper event.
        ApplyLoadedHelperLayout();
        TransitionToExpandedPresentationLayout();
        RaiseWhitelistUiTargetCanvasesForActiveOverlay();

        if (_helperCloseButtonRoot)
            _helperCloseButtonRoot.SetActive(def.showCloseButton);

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);

        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: true);

        RefreshWhitelistPresentationEmphasis();
        ApplyInventoryLootHighlightFromActiveDefinition();

        RefreshWorldWhitelistRoutingFlag();
        ApplyDarkenModalPresentation();

        SaveMessageHistoryToPlayerPrefs();

        ApplyHelperTipTextScale();

        MaybeStartStuckQueuedAdvanceWatcher();

        string hidForBadge = string.IsNullOrWhiteSpace(def.helperId) ? null : def.helperId.Trim();
        if (string.IsNullOrEmpty(hidForBadge) || !HelperProgressStore.WasNewBadgeSuppressed(hidForBadge))
            ShowHelperNewBadge();
        if (!string.IsNullOrEmpty(hidForBadge))
            HelperProgressStore.MarkNewBadgeSuppressed(hidForBadge);
    }

    private void RefreshChromeCollapsedVisuals(bool headerOnlyCollapsed)
    {
        if (_chromeStripTitleText)
        {
            _chromeStripTitleText.gameObject.SetActive(headerOnlyCollapsed);
            if (headerOnlyCollapsed)
                _chromeStripTitleText.text = "Helper Window";
        }

        if (_minimizeExpandGlyphTmp)
            _minimizeExpandGlyphTmp.text = headerOnlyCollapsed ? "+" : "-";
    }

    private void TransitionToExpandedPresentationLayout()
    {
        if (!_helperPanelRt || !_expandedPanelRoot)
            return;

        _expandedPanelRoot.SetActive(true);
        RefreshChromeCollapsedVisuals(false);

        Vector2 fallback = panelSize;
        float w =
            _lastExpandedPanelSizeDelta.x > 1f ? _lastExpandedPanelSizeDelta.x : fallback.x;
        float h =
            _lastExpandedPanelSizeDelta.y > ExpandedHeaderStripHeight + 24f
                ? _lastExpandedPanelSizeDelta.y
                : fallback.y;

        _helperPanelRt.sizeDelta = new Vector2(w, h);

        if (_prevHistoryButton && _nextHistoryButton)
        {
            bool showNav = _sessionMessageHistory.Count > 1;
            _prevHistoryButton.gameObject.SetActive(showNav);
            _nextHistoryButton.gameObject.SetActive(showNav);
        }

        SaveHelperLayoutSnapshot();
    }

    private void TransitionToCollapsedStripeLayout()
    {
        if (!_helperPanelRt || !_expandedPanelRoot)
            return;

        _lastExpandedPanelSizeDelta = _helperPanelRt.sizeDelta;

        _expandedPanelRoot.SetActive(false);

        Vector2 stripeSize = _lastExpandedPanelSizeDelta.x > 1f ? _lastExpandedPanelSizeDelta : panelSize;

        _helperPanelRt.sizeDelta = new Vector2(stripeSize.x, ExpandedHeaderStripHeight);
        RefreshChromeCollapsedVisuals(true);

        if (_prevHistoryButton)
            _prevHistoryButton.gameObject.SetActive(false);
        if (_nextHistoryButton)
            _nextHistoryButton.gameObject.SetActive(false);

        SaveHelperLayoutSnapshot();
    }

    private void RefreshWorldWhitelistRoutingFlag()
    {
        if (_activeDefinition == null ||
            (_activeDefinition.dismissModes & HelperDismissMode.InteractWhitelistDismiss) == 0)
        {
            _activeUsesWorldWhitelistRouting = false;
            return;
        }

        HelperWhitelistInteractTarget[] markers =
            FindObjectsByType<HelperWhitelistInteractTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < markers.Length; i++)
        {
            HelperWhitelistInteractTarget m = markers[i];
            if (m && _activeDefinition.MatchesWhitelistId(m.InteractionId))
            {
                _activeUsesWorldWhitelistRouting = true;
                return;
            }
        }

        _activeUsesWorldWhitelistRouting = false;
    }

    private void ApplyDarkenModalPresentation()
    {
        bool wants =
            IsActiveHelperExpandedWithModalGameplayLock() &&
            _overlayRoot != null &&
            _overlayRoot.activeSelf;

        if (_dimmerImage)
        {
            _dimmerImage.enabled = wants;
            _dimmerImage.color = wants ? new Color(0f, 0f, 0f, 0.62f) : new Color(0f, 0f, 0f, 0f);
            _dimmerImage.raycastTarget = wants;
        }

        SyncMovementLockFromSettings();
        EnsureHelperModalOverlayDrawOrder();
    }

    /// <summary>Forces modal visuals off and unlocks movement — used after scripted dismiss so state cannot get stuck if dimmer reference or EventSystem routing differs.</summary>
    private void ClearModalDimmerAndUnlockPlayer()
    {
        if (_dimmerImage)
        {
            _dimmerImage.enabled = false;
            _dimmerImage.raycastTarget = false;
            _dimmerImage.color = new Color(0f, 0f, 0f, 0f);
        }

        ResolvePlayerMovementLock(false);
        EnsureHelperModalOverlayDrawOrder();
    }

    private void SyncMovementLockFromSettings()
    {
        bool want =
            IsActiveHelperExpandedWithModalGameplayLock();

        ResolvePlayerMovementLock(want, preserveGatherFreezeForHelper: true);
    }

    private bool ViewingLatestHistoryEntry() =>
        _sessionMessageHistory.Count == 0 ||
        _historyViewIndex == _sessionMessageHistory.Count - 1;

    private void RefreshHistoryNavButtonInteractable()
    {
        if (!_prevHistoryButton || !_nextHistoryButton)
            return;

        int max = _sessionMessageHistory.Count - 1;
        if (max <= 0)
            return;

        _prevHistoryButton.interactable = _historyViewIndex > 0;
        _nextHistoryButton.interactable = _historyViewIndex < max;
    }

    private void ApplyDisplayedHistoryIndexToPanel(int index, bool startTypewriterFresh)
    {
        if (_sessionMessageHistory.Count == 0 || index < 0 || index >= _sessionMessageHistory.Count)
            return;

        HelperDisplayedMessageSnap snap = _sessionMessageHistory[index];
        bool hasTitle = snap.TitlePlain != null && snap.TitlePlain.Length > 0;
        if (_titleText)
        {
            _titleText.gameObject.SetActive(hasTitle);
            if (hasTitle)
                _titleText.text = snap.TitlePlain ?? string.Empty;
        }

        if (!_bodyText)
            return;

        if (startTypewriterFresh && index == _sessionMessageHistory.Count - 1 &&
            _activeDefinition != null)
        {
            StartBodyTypewriter(snap.BodyPlain ?? string.Empty);
        }
        else
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = snap.BodyPlain ?? string.Empty;
        }

        RefreshHistoryNavButtonInteractable();
        RefreshHelperBodyScrollLayout(scrollToTop: true);
    }

    public void OnHistoryNavClicked(int delta)
    {
        if (_sessionMessageHistory.Count == 0 || !IsHelperExpandedPresentation())
            return;

        CompleteBodyTypewriter();

        int next = Mathf.Clamp(_historyViewIndex + delta, 0, _sessionMessageHistory.Count - 1);
        if (next == _historyViewIndex)
            return;

        _historyViewIndex = next;
        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: false);

        if (ViewingLatestHistoryEntry() &&
            _activeDefinition != null &&
            _activeDefinition.highlightWhitelistTargetsDuringHelper)
            RefreshWhitelistPresentationEmphasis();
        else
            ClearWhitelistGlowOverlaysSafelyForHistoryBrowsing();
    }

    private void ClearWhitelistGlowOverlaysSafelyForHistoryBrowsing()
    {
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();
    }

    public void ToggleMinimizeStripe()
    {
        if (_overlayRoot == null || !_overlayRoot.activeSelf || !_expandedPanelRoot)
            return;

        if (IsHelperExpandedPresentation())
        {
            RestoreWhitelistUiTargetCanvases();
            TransitionToCollapsedStripeLayout();
            ApplyDarkenModalPresentation();
            RefreshWorldWhitelistRoutingFlag();

            CompleteBodyTypewriter();
            return;
        }

        TransitionToExpandedPresentationLayout();
        RaiseWhitelistUiTargetCanvasesForActiveOverlay();
        if (_sessionMessageHistory.Count == 0)
            TrySeedSessionHistoryFromFirstDefinition();
        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: false);

        RefreshWhitelistPresentationEmphasis();
        ApplyDarkenModalPresentation();
        RefreshWorldWhitelistRoutingFlag();
    }

    private void HideOverlayCompletely(bool clearMovementLock, bool purgeMessageHistory = true, bool drainPendingQueue = true)
    {
        StopStuckQueuedAdvanceCoroutine();

        SaveHelperLayoutSnapshot();

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();
        if (_bodyText)
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = string.Empty;
        }

        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        if (_expandedPanelRoot)
            RefreshChromeCollapsedVisuals(!_expandedPanelRoot.activeSelf);

        if (_helperPanelRt)
            _helperPanelRt.sizeDelta = panelSize;

        _activeUsesWorldWhitelistRouting = false;

        if (purgeMessageHistory)
        {
            _sessionMessageHistory.Clear();
            _historyViewIndex = 0;
        }

        if (_overlayRoot)
            _overlayRoot.SetActive(false);

        HideHelperNewBadge();

        if (_dimmerImage)
        {
            _dimmerImage.enabled = false;
            _dimmerImage.raycastTarget = false;
        }

        _whitelistStagedModalReleased = false;
        _activeDefinition = null;

        if (clearMovementLock)
            ResolvePlayerMovementLock(false);

        if (drainPendingQueue)
            TryDrainPendingHelperQueue();

        EnsureHelperModalOverlayDrawOrder();
    }

    private void LateUpdate()
    {
        RunDeferredInventoryHelperEvaluation();
        SyncAndPulseWhitelistGlow();
        TrackHelperLayoutSave();
        PulseHelperNewBadgeAlpha();
    }

    private void Update()
    {
        LerpWhitelistPresentationTints();
        PollAnyPlayerActionDismiss();
    }

    private void PollAnyPlayerActionDismiss()
    {
        if (_activeDefinition == null ||
            !IsHelperExpandedPresentation() ||
            !HelpersPermittedBySettings())
            return;

        if ((_activeDefinition.dismissModes & HelperDismissMode.AnyPlayerActionDismiss) == 0)
            return;

        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
            Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f)
        {
            TryDismiss(HelperDismissMode.AnyPlayerActionDismiss);
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                TryDismiss(HelperDismissMode.AnyPlayerActionDismiss);
                return;
            }

            if (IsTopUiRaycastHitHelperModalDimmer())
                TryDismiss(HelperDismissMode.AnyPlayerActionDismiss);
        }
    }

    /// <summary>
    /// True when the topmost graphic hit is the helper fullscreen dimmer (dark area), not the helper panel / other UI.
    /// Clicks there must count as "any player action" — otherwise <see cref="EventSystem.IsPointerOverGameObject"/> blocks dismiss.
    /// </summary>
    private bool IsTopUiRaycastHitHelperModalDimmer()
    {
        if (_dimmerImage == null || !_dimmerImage.isActiveAndEnabled)
            return false;

        if (EventSystem.current == null)
            return false;

        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        if (results.Count == 0)
            return false;

        Transform top = results[0].gameObject.transform;
        Transform dim = _dimmerImage.transform;
        return top == dim || top.IsChildOf(dim);
    }

    private CharacterStats ResolvePlayerCharacterStats()
    {
        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        return _player ? _player.GetComponent<CharacterStats>() : null;
    }

    private void ResolvePlayerMovementLock(bool locked, bool preserveGatherFreezeForHelper = false)
    {
        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (_player != null)
            _player.SetMovementLocked(locked, preserveGatherStateForUiModal: preserveGatherFreezeForHelper);
    }

    public void CloseFromUIButton()
    {
        if (_overlayRoot == null || !_overlayRoot.activeSelf)
            return;

        if (_activeDefinition != null)
            DismissMarked(drainPendingQueue: false);
        else
            HideOverlayCompletely(true, purgeMessageHistory: false, drainPendingQueue: false);
    }

    /// <remarks>Same as <see cref="CloseFromUIButton"/> (shared header close control).</remarks>
    public void CloseFromCollapsedStripeButton() => CloseFromUIButton();

    /// <summary>Pointer-down on the helper panel reveals the full body text immediately (matches NPC dialogue box).</summary>
    public void CompleteBodyTypewriter()
    {
        if (_bodyTypewriterCo == null)
            return;

        StopCoroutine(_bodyTypewriterCo);
        _bodyTypewriterCo = null;
        if (_bodyText && _bodyTypewriterFullPlain != null)
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = _bodyTypewriterFullPlain;
        }
        _bodyTypewriterFullPlain = null;
        RefreshHelperBodyScrollLayout(scrollToTop: true);
    }

    private void StopBodyTypewriterAndClear()
    {
        if (_bodyTypewriterCo != null)
        {
            StopCoroutine(_bodyTypewriterCo);
            _bodyTypewriterCo = null;
        }

        _bodyTypewriterFullPlain = null;
    }

    private void StartBodyTypewriter(string plainFull)
    {
        StopBodyTypewriterAndClear();
        plainFull ??= "";

        if (!_bodyText)
            return;

        if (plainFull.Length == 0)
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = "";
            RefreshHelperBodyScrollLayout(scrollToTop: true);
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = plainFull;
            RefreshHelperBodyScrollLayout(scrollToTop: true);
            return;
        }

        DialogueTextTypewriter.RestoreFullReveal(_bodyText);
        _bodyTypewriterFullPlain = plainFull;
        _bodyText.text = "";
        _bodyTypewriterCo = StartCoroutine(RunBodyTypewriterCoroutine());
    }

    private IEnumerator RunBodyTypewriterCoroutine()
    {
        string full = _bodyTypewriterFullPlain ?? "";
        yield return DialogueTextTypewriter.RevealFlowingCharacters(_bodyText, full, typewriterCharactersPerSecond);
        _bodyTypewriterCo = null;
        _bodyTypewriterFullPlain = null;
        RefreshHelperBodyScrollLayout(scrollToTop: true);
    }

    private void TryDismiss(
        HelperDismissMode modeReason,
        string interactWhitelistIdMarker = null,
        Graphic whitelistUiGlowSource = null,
        Collider2D whitelistWorldWinnerCollider = null)
    {
        if (_activeDefinition == null)
            return;

        if ((_activeDefinition.dismissModes & modeReason) == 0)
            return;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            interactWhitelistIdMarker != null &&
            !_activeDefinition.MatchesWhitelistId(interactWhitelistIdMarker))
            return;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            interactWhitelistIdMarker != null &&
            string.Equals(
                interactWhitelistIdMarker.Trim(),
                HelperWhitelistUiInteractTarget.CharacterToolbarWhitelistId,
                System.StringComparison.OrdinalIgnoreCase))
            s_characterToolbarWhitelistUiDismissStampFrame = Time.frameCount;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            interactWhitelistIdMarker != null &&
            string.Equals(
                interactWhitelistIdMarker.Trim(),
                HelperWhitelistUiInteractTarget.QuestToolbarWhitelistId,
                System.StringComparison.OrdinalIgnoreCase))
            s_questToolbarWhitelistUiDismissStampFrame = Time.frameCount;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            interactWhitelistIdMarker != null &&
            HelperWhitelistUiInteractTarget.IsSkillsAbilityToolbarWhitelistMarker(interactWhitelistIdMarker))
            s_skillsAbilityToolbarWhitelistUiDismissStampFrame = Time.frameCount;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            interactWhitelistIdMarker != null &&
            string.Equals(
                interactWhitelistIdMarker.Trim(),
                HelperWhitelistUiInteractTarget.LevelSelectToolbarWhitelistId,
                System.StringComparison.OrdinalIgnoreCase))
            s_levelSelectToolbarWhitelistUiDismissStampFrame = Time.frameCount;

        // Stamps above: MainMenuWindowUI treats same-frame toolbar clicks as "don't Close() the menu
        // when already on that tab" — helper still completes scripted dismiss below (stays expanded).

        if ((modeReason == HelperDismissMode.InteractWhitelistDismiss ||
             modeReason == HelperDismissMode.AnyPlayerActionDismiss) &&
            ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
        {
            if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
                TryProgressStagedWhitelistInteractDismiss(
                    interactWhitelistIdMarker,
                    whitelistUiGlowSource,
                    whitelistWorldWinnerCollider))
                return;

            CompleteScriptedDismissLeaveExpanded();
            return;
        }

        DismissMarked();
    }

    /// <summary>
    /// Marks the active tip dismissed, clears whitelist presentation, unlocks gameplay, and keeps the helper panel expanded (history + nav).
    /// The player can still minimize via the chrome control.
    /// </summary>
    private void CompleteScriptedDismissLeaveExpanded()
    {
        if (_activeDefinition == null)
            return;

        _whitelistStagedModalReleased = false;

        HelperProgressStore.MarkDismissed(_activeDefinition.helperId);

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();

        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        _activeDefinition = null;
        _activeUsesWorldWhitelistRouting = false;

        ClearModalDimmerAndUnlockPlayer();

        if (!_helperPanelRt || !_expandedPanelRoot || _overlayRoot == null)
        {
            HideOverlayCompletely(true, purgeMessageHistory: false);
            return;
        }

        if (!IsHelperExpandedPresentation())
            TransitionToExpandedPresentationLayout();

        if (_helperCloseButtonRoot)
            _helperCloseButtonRoot.SetActive(true);

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);

        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: false);
        ApplyDarkenModalPresentation();

        TryDrainPendingHelperQueue();
    }

    private void DismissMarked(bool drainPendingQueue = true)
    {
        if (_activeDefinition == null)
            return;

        HelperProgressStore.MarkDismissed(_activeDefinition.helperId);

        HideOverlayCompletely(true, purgeMessageHistory: false, drainPendingQueue: drainPendingQueue);
    }

    private void RefreshWhitelistPresentationEmphasis()
    {
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        if (_activeDefinition == null ||
            !IsHelperExpandedPresentation() ||
            !ViewingLatestHistoryEntry() ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            !_activeDefinition.HasConfiguredWhitelistInteractIds())
        {
            EnsureHelperModalOverlayDrawOrder();
            return;
        }

        if (whitelistGlowAboveDimmer)
            RebuildWhitelistGlowOverlays();
        else
            ApplyWhitelistPresentationTintsInner();

        EnsureHelperModalOverlayDrawOrder();
    }

    private void ApplyInventoryLootHighlightFromActiveDefinition()
    {
        if (_activeDefinition == null)
            return;

        ItemDefinition item = _activeDefinition.highlightInventorySlotsForItem;
        if (!item || string.IsNullOrWhiteSpace(item.itemId))
            return;

        AutoBattleLootHighlight.MarkSlotsContainingItem(item.itemId);
        AutoBattleLootHighlight.RefreshLootHighlightUIs();
    }

    private void ApplyWhitelistPresentationTintsInner()
    {
        if (_activeDefinition == null ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            !_activeDefinition.HasConfiguredWhitelistInteractIds())
            return;

        HelperWhitelistInteractTarget[] markers =
            FindObjectsByType<HelperWhitelistInteractTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        var scratch = new List<SpriteRenderer>(24);
        var seen = new HashSet<SpriteRenderer>();

        for (int i = 0; i < markers.Length; i++)
        {
            HelperWhitelistInteractTarget marker = markers[i];
            if (!marker || !_activeDefinition.MatchesWhitelistId(marker.InteractionId))
                continue;

            CollectWhitelistPresentationSprites(marker, visualsSubtreeChildName, scratch, seen);
        }

        for (int i = 0; i < scratch.Count; i++)
        {
            SpriteRenderer sr = scratch[i];
            if (!sr)
                continue;

            _whitelistPresentationTints.Add(new WhitelistTintState { Renderer = sr, SavedColor = sr.color });
        }
    }

    private void LerpWhitelistPresentationTints()
    {
        if (!IsHelperExpandedPresentation() ||
            _activeDefinition == null ||
            !ViewingLatestHistoryEntry() ||
            _whitelistPresentationTints.Count == 0 ||
            _whitelistGlowLinks.Count != 0)
            return;

        float dt = Time.deltaTime * whitelistTintLerpSpeed;
        for (int i = 0; i < _whitelistPresentationTints.Count; i++)
        {
            WhitelistTintState row = _whitelistPresentationTints[i];
            SpriteRenderer r = row.Renderer;
            if (!r)
                continue;

            Color target =
                Color.Lerp(row.SavedColor, whitelistHelperTint, whitelistHelperTintStrength);
            r.color = Color.Lerp(r.color, target, dt);
        }
    }

    private void ClearWhitelistPresentationTints()
    {
        for (int i = 0; i < _whitelistPresentationTints.Count; i++)
        {
            WhitelistTintState row = _whitelistPresentationTints[i];
            if (row.Renderer)
                row.Renderer.color = row.SavedColor;
        }

        _whitelistPresentationTints.Clear();
    }

    private void SyncHelperInternalStateAfterNewGameLayout(Vector2 pos, Vector2 size)
    {
        _lastExpandedPanelSizeDelta = Vector2.zero;

        if (_helperPanelRt)
        {
            _helperPanelRt.anchoredPosition = pos;
            _helperPanelRt.sizeDelta = size;
        }

        if (_expandedPanelRoot)
        {
            _expandedPanelRoot.SetActive(true);
            RefreshChromeCollapsedVisuals(false);
        }

        _layoutTrackSize = size;
    }

    private void SaveHelperLayoutSnapshot()
    {
        if (!_helperPanelRt)
            return;

        bool headerOnly = _expandedPanelRoot && !_expandedPanelRoot.activeSelf;
        Vector2 lastExp = headerOnly ? _lastExpandedPanelSizeDelta : _helperPanelRt.sizeDelta;
        if (lastExp.x < 1f || lastExp.y < 1f)
            lastExp = panelSize;

        HelperPopupLayoutPrefs.Save(_helperPanelRt.anchoredPosition, lastExp, headerOnly);
        UIWindowPositionMemory.Save("HelperPopupWindow.Panel", _helperPanelRt.anchoredPosition);
    }

    private void ApplyLoadedHelperLayout()
    {
        if (!_helperPanelRt || !_expandedPanelRoot)
            return;

        if (!HelperPopupLayoutPrefs.TryLoad(out Vector2 pos, out Vector2 lastExp, out bool headerOnly))
        {
            _layoutTrackSize = _helperPanelRt.sizeDelta;
            return;
        }

        _lastExpandedPanelSizeDelta = lastExp;
        _helperPanelRt.anchoredPosition = pos;
        UIWindowPositionMemory.Save("HelperPopupWindow.Panel", pos);

        if (headerOnly)
        {
            _expandedPanelRoot.SetActive(false);
            RefreshChromeCollapsedVisuals(true);
            float w = lastExp.x > 1f ? lastExp.x : panelSize.x;
            _helperPanelRt.sizeDelta = new Vector2(w, ExpandedHeaderStripHeight);
        }
        else
        {
            _expandedPanelRoot.SetActive(true);
            RefreshChromeCollapsedVisuals(false);
            float w = lastExp.x > 1f ? lastExp.x : panelSize.x;
            float h = lastExp.y > ExpandedHeaderStripHeight + 24f ? lastExp.y : panelSize.y;
            _helperPanelRt.sizeDelta = new Vector2(w, h);
        }

        _layoutTrackSize = _helperPanelRt.sizeDelta;
    }

    private void TrackHelperLayoutSave()
    {
        if (_helperPanelRt == null || _overlayRoot == null || !_overlayRoot.activeSelf)
            return;

        Vector2 sz = _helperPanelRt.sizeDelta;
        if (sz != _layoutTrackSize)
        {
            _layoutTrackSize = sz;
            SaveHelperLayoutSnapshot();
        }
    }

    private static Transform FindHelperBodyUnderExpanded(Transform expandedTf)
    {
        if (!expandedTf)
            return null;
        Transform direct = expandedTf.Find("BodyText");
        if (direct)
            return direct;
        return expandedTf.Find("BodyScrollView/Viewport/Content/BodyText");
    }

    /// <summary>Upgrades scroll <c>Content</c> from older builds (CSF-only) so TMP preferred height drives scroll range.</summary>
    private static void EnsureHelperScrollContentHasVerticalLayout(RectTransform contentRt)
    {
        if (!contentRt)
            return;

        if (contentRt.GetComponent<VerticalLayoutGroup>())
            return;

        VerticalLayoutGroup vlg = contentRt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 0;
        vlg.padding = new RectOffset(
            Mathf.RoundToInt(HelperBodyScrollHorizontalPadding),
            Mathf.RoundToInt(HelperBodyScrollHorizontalPadding),
            0,
            0);
    }

    /// <summary>
    /// Body paragraphs sit inside the scroll view margin plus <see cref="VerticalLayoutGroup.padding"/> on content —
    /// match the title's left edge to that same X so the header lines up with the text block.
    /// </summary>
    private void ApplyHelperTitleLeftInsetFromBodyLayout()
    {
        if (!_titleText || !_helperBodyScrollRect)
            return;

        RectTransform scrollRt = _helperBodyScrollRect.transform as RectTransform;
        if (!scrollRt)
            return;

        int padLeft = Mathf.RoundToInt(HelperBodyScrollHorizontalPadding);
        if (_helperBodyScrollContent &&
            _helperBodyScrollContent.TryGetComponent(out VerticalLayoutGroup bodyContentVlg))
            padLeft = bodyContentVlg.padding.left;

        float alignedLeft = scrollRt.offsetMin.x + padLeft;
        RectTransform titleRt = _titleText.rectTransform;
        titleRt.offsetMin = new Vector2(alignedLeft, titleRt.offsetMin.y);
    }

    private void BuildHelperBodyScrollInternals(
        RectTransform scrollRootRt,
        out ScrollRect scrollRect,
        out RectTransform viewportRt,
        out RectTransform contentRt,
        out Scrollbar verticalSb)
    {
        ScrollRect sr = scrollRootRt.GetComponent<ScrollRect>();
        if (!sr)
            sr = scrollRootRt.gameObject.AddComponent<ScrollRect>();
        scrollRect = sr;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 22f;
        sr.inertia = true;
        sr.decelerationRate = 0.135f;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        GameObject vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportRt = vpGo.GetComponent<RectTransform>();
        viewportRt.SetParent(scrollRootRt, false);
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.pivot = new Vector2(0f, 1f);
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = new Vector2(-HelperBodyScrollbarWidth, 0f);
        Image vpImg = vpGo.GetComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);
        vpImg.raycastTarget = true;

        GameObject sbGo = new GameObject("ScrollbarVertical", typeof(RectTransform), typeof(Scrollbar));
        RectTransform sbRt = sbGo.GetComponent<RectTransform>();
        sbRt.SetParent(scrollRootRt, false);
        sbRt.anchorMin = new Vector2(1f, 0f);
        sbRt.anchorMax = new Vector2(1f, 1f);
        sbRt.pivot = new Vector2(1f, 0.5f);
        sbRt.anchoredPosition = Vector2.zero;
        sbRt.sizeDelta = new Vector2(HelperBodyScrollbarWidth, 0f);

        verticalSb = sbGo.GetComponent<Scrollbar>();
        verticalSb.direction = Scrollbar.Direction.BottomToTop;
        verticalSb.transition = Selectable.Transition.ColorTint;

        GameObject slide = new GameObject("SlidingArea", typeof(RectTransform));
        slide.transform.SetParent(sbGo.transform, false);
        RectTransform slideRt = slide.GetComponent<RectTransform>();
        slideRt.anchorMin = new Vector2(0.08f, 0.02f);
        slideRt.anchorMax = new Vector2(0.92f, 0.98f);
        slideRt.offsetMin = Vector2.zero;
        slideRt.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slide.transform, false);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = Vector2.zero;

        Image hImg = handle.GetComponent<Image>();
        hImg.color = new Color(0.48f, 0.5f, 0.56f, 0.92f);
        hImg.raycastTarget = true;

        verticalSb.targetGraphic = hImg;
        verticalSb.handleRect = handleRt;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
        contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.SetParent(viewportRt, false);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 0;
        vlg.padding = new RectOffset(
            Mathf.RoundToInt(HelperBodyScrollHorizontalPadding),
            Mathf.RoundToInt(HelperBodyScrollHorizontalPadding),
            0,
            0);

        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewportRt;
        sr.content = contentRt;
        sr.verticalScrollbar = verticalSb;
    }

    private void EnsureHelperBodyScrollPresentation()
    {
        if (_bodyText == null)
            return;

        ScrollRect existing = _bodyText.GetComponentInParent<ScrollRect>();
        if (existing != null)
        {
            _helperBodyScrollRect = existing;
            _helperBodyScrollContent = existing.content;
            _helperBodyScrollbar = existing.verticalScrollbar;
            EnsureHelperScrollContentHasVerticalLayout(_helperBodyScrollContent);
            ApplyHelperBodyTextScrollLayoutDefaults(_bodyText);
            WireHelperBodyViewportTypewriterSkip();
            RefreshHelperBodyScrollLayout(scrollToTop: false);
            return;
        }

        RectTransform bodyRt = _bodyText.rectTransform;
        Transform parent = bodyRt.parent;
        if (!parent)
            return;

        int idx = bodyRt.GetSiblingIndex();

        GameObject scrollGo = new GameObject("BodyScrollView", typeof(RectTransform));
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(parent, false);
        scrollRt.SetSiblingIndex(idx);
        scrollRt.localScale = Vector3.one;
        scrollRt.anchorMin = bodyRt.anchorMin;
        scrollRt.anchorMax = bodyRt.anchorMax;
        scrollRt.pivot = bodyRt.pivot;
        scrollRt.anchoredPosition = bodyRt.anchoredPosition;
        scrollRt.sizeDelta = bodyRt.sizeDelta;
        scrollRt.offsetMin = bodyRt.offsetMin;
        scrollRt.offsetMax = bodyRt.offsetMax;

        BuildHelperBodyScrollInternals(scrollRt, out ScrollRect sr, out RectTransform viewportRt, out RectTransform contentRt,
            out Scrollbar sb);
        _helperBodyScrollRect = sr;
        _helperBodyScrollContent = contentRt;
        _helperBodyScrollbar = sb;

        _bodyText.transform.SetParent(contentRt, false);
        RectTransform brt = _bodyText.rectTransform;
        brt.localScale = Vector3.one;
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = Vector2.zero;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        ApplyHelperBodyTextScrollLayoutDefaults(_bodyText);

        WireHelperBodyViewportTypewriterSkip();
        RefreshHelperBodyScrollLayout(scrollToTop: true);
    }

    /// <summary>
    /// Body TMP must not raycast — it would steal drags/wheel from <see cref="ScrollRect"/>. Typewriter skip lives on the viewport instead.
    /// </summary>
    private static void ApplyHelperBodyTextScrollLayoutDefaults(TMP_Text body)
    {
        if (!body)
            return;

        body.raycastTarget = false;
        ContentSizeFitter fit = body.GetComponent<ContentSizeFitter>();
        if (!fit)
            fit = body.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void WireHelperBodyViewportTypewriterSkip()
    {
        if (!_helperBodyScrollRect || !_helperBodyScrollRect.viewport)
            return;

        GameObject vp = _helperBodyScrollRect.viewport.gameObject;
        if (_bodyText && _bodyText.TryGetComponent(out HelperTypewriterPanelSkip oldSkip))
            Destroy(oldSkip);

        HelperTypewriterPanelSkip s = vp.GetComponent<HelperTypewriterPanelSkip>() ?? vp.AddComponent<HelperTypewriterPanelSkip>();
        s.Init(this);
    }

    private void RefreshHelperBodyScrollLayout(bool scrollToTop)
    {
        if (!_helperBodyScrollRect || !_helperBodyScrollContent)
            return;

        if (_bodyText)
        {
            _bodyText.ForceMeshUpdate(true);
            ApplyHelperBodyTextScrollLayoutDefaults(_bodyText);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_helperBodyScrollContent);
        if (_bodyText)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bodyText.rectTransform);

        if (scrollToTop)
            _helperBodyScrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// Hides the flashing &quot;! new&quot; badge when the player clicks the helper panel (chrome, title, body, etc.)
    /// or activates a UI id that matches the active helper whitelist (see <see cref="NotifyWhitelistUiInteract"/>).
    /// </summary>
    public void DismissHelperNewBadgeFromPanelPointer()
    {
        if (_activeDefinition != null && !string.IsNullOrWhiteSpace(_activeDefinition.helperId))
            HelperProgressStore.MarkNewBadgeSuppressed(_activeDefinition.helperId.Trim());
        HideHelperNewBadge();
    }

    private void ShowHelperNewBadge()
    {
        EnsureHelperNewBadgeBuilt();
        EnsureHelperNewBadgeDismissTargetsWired();
        if (_helperNewBadgeRoot == null)
            return;

        _helperNewBadgeRoot.SetActive(true);
        if (_helperNewBadgeCanvasGroup)
            _helperNewBadgeCanvasGroup.alpha = helperNewBadgePulseAlphaMax;
    }

    private void HideHelperNewBadge()
    {
        if (_helperNewBadgeRoot != null && _helperNewBadgeRoot.activeSelf)
            _helperNewBadgeRoot.SetActive(false);
    }

    private void PulseHelperNewBadgeAlpha()
    {
        if (_helperNewBadgeRoot == null || !_helperNewBadgeRoot.activeSelf || _helperNewBadgeCanvasGroup == null)
            return;

        float w = Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, helperNewBadgePulseSpeed));
        float n = (w + 1f) * 0.5f;
        float min = Mathf.Clamp01(helperNewBadgePulseAlphaMin);
        float max = Mathf.Clamp01(helperNewBadgePulseAlphaMax);
        if (max < min)
        {
            float t = min;
            min = max;
            max = t;
        }

        _helperNewBadgeCanvasGroup.alpha = Mathf.Lerp(min, max, n);
    }

    private void EnsureHelperNewBadgeBuilt()
    {
        if (_helperPanelRt == null)
            return;

        Transform existing = _helperPanelRt.Find(HelperNewBadgeChildName);
        if (existing != null)
        {
            _helperNewBadgeRoot = existing.gameObject;
            _helperNewBadgeCanvasGroup = _helperNewBadgeRoot.GetComponent<CanvasGroup>();
            if (!_helperNewBadgeCanvasGroup)
            {
                _helperNewBadgeCanvasGroup = _helperNewBadgeRoot.AddComponent<CanvasGroup>();
                _helperNewBadgeCanvasGroup.blocksRaycasts = false;
                _helperNewBadgeCanvasGroup.interactable = false;
            }

            ApplyHelperNewBadgeLayout(_helperNewBadgeRoot);
        }
        else
        {
            GameObject badgeGo = new GameObject(HelperNewBadgeChildName, typeof(RectTransform));
            badgeGo.transform.SetParent(_helperPanelRt, false);
            RectTransform brt = badgeGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);

            CanvasGroup cg = badgeGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            _helperNewBadgeCanvasGroup = cg;

            TMP_Text tmp = badgeGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "! new";
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.92f, 0.18f, 1f);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            _helperNewBadgeRoot = badgeGo;
            badgeGo.transform.SetAsLastSibling();
            ApplyHelperNewBadgeLayout(_helperNewBadgeRoot);
        }

        // Default hidden until <see cref="ShowHelperNewBadge"/>; avoids "! new" flashing after respawn when UI is rebuilt.
        HideHelperNewBadge();
    }

    private static void ApplyHelperNewBadgeLayout(GameObject badgeRoot)
    {
        if (!badgeRoot)
            return;

        if (badgeRoot.TryGetComponent(out RectTransform brt))
        {
            brt.sizeDelta = kHelperNewBadgeSizeDelta;
            brt.anchoredPosition = kHelperNewBadgeAnchoredPosition;
        }

        TMP_Text tmp = badgeRoot.GetComponent<TMP_Text>();
        if (!tmp)
            tmp = badgeRoot.GetComponentInChildren<TMP_Text>(true);
        if (tmp)
            tmp.fontSize = HelperNewBadgeFontSize;
    }

    private void EnsureHelperNewBadgeDismissTargetsWired()
    {
        if (_helperPanelRt == null)
            return;

        Transform chrome = _helperPanelRt.Find("TopChromeStrip");
        if (chrome)
        {
            WireHelperNewBadgeDismissPointer(chrome);
            Transform minBt = chrome.Find("MinimizeStripeButton");
            if (minBt)
                WireHelperNewBadgeDismissPointer(minBt);
        }

        if (_helperCloseButtonRoot)
            WireHelperNewBadgeDismissPointer(_helperCloseButtonRoot.transform);

        if (_titleText)
        {
            _titleText.raycastTarget = true;
            WireHelperNewBadgeDismissPointer(_titleText.transform);
        }

        if (_expandedPanelRoot)
        {
            Transform footer = _expandedPanelRoot.transform.Find("FooterNav");
            if (footer && footer.TryGetComponent(out Image footImg))
            {
                footImg.raycastTarget = true;
                WireHelperNewBadgeDismissPointer(footer);
            }
        }

        if (_prevHistoryButton)
            WireHelperNewBadgeDismissPointer(_prevHistoryButton.transform);
        if (_nextHistoryButton)
            WireHelperNewBadgeDismissPointer(_nextHistoryButton.transform);

        // Body viewport uses <see cref="HelperTypewriterPanelSkip"/> (also dismisses the new badge).

        if (_helperBodyScrollbar)
            WireHelperNewBadgeDismissPointer(_helperBodyScrollbar.transform);

        WireHelperNewBadgeDismissPointer(_helperPanelRt);
    }

    private void WireHelperNewBadgeDismissPointer(Transform t)
    {
        if (!t)
            return;

        HelperPanelDismissNewBadgeOnPointerDown w =
            t.GetComponent<HelperPanelDismissNewBadgeOnPointerDown>() ??
            t.gameObject.AddComponent<HelperPanelDismissNewBadgeOnPointerDown>();
        w.Init(this);
    }

    private bool TryWireExistingHelperPopupWindow()
    {
        HelperPopupWindow host = HelperPopupWindow.FindExisting();
        if (!host)
            return false;

        Transform rootTf = host.transform;
        Transform panelTf = rootTf.Find("HelperPanel");
        if (!panelTf)
            return false;

        Transform chrome = panelTf.Find("TopChromeStrip");
        if (!chrome)
            return false;

        Transform expandedTf = panelTf.Find("ExpandedPresentation");
        if (!expandedTf || !FindHelperBodyUnderExpanded(expandedTf))
            return false;

        _overlayRoot = host.gameObject;
        _overlayRect = _overlayRoot.transform as RectTransform;

        Transform dim = _overlayRoot.transform.Find("Dimmer");
        _dimmerImage = dim ? dim.GetComponent<Image>() : null;
        if (dim)
            ConstrainDimmerToStripViewport(dim.gameObject);

        Transform glowHolderTf = _overlayRoot.transform.Find("WhitelistGlowOverlay/WhitelistGlowHolder");
        _whitelistGlowHolder = glowHolderTf as RectTransform;

        _helperPanelRt = panelTf as RectTransform;

        _chromeStripTitleText = chrome.Find("ChromeTitle")?.GetComponent<TMP_Text>();
        _helperCloseButtonRoot = chrome.Find("CloseButton")?.gameObject;

        GameObject minimizeGo = chrome.Find("MinimizeStripeButton")?.gameObject;
        _minimizeExpandGlyphTmp = FindTmpOnButton(minimizeGo);

        UIDragWindow drag = chrome.GetComponent<UIDragWindow>();
        if (drag)
        {
            drag.SetRuntimeMemoryKey("HelperPopupWindow.Panel");
            drag.UsePlayerPrefsForAnchoredPosition(HelperPopupLayoutPrefs.PosX, HelperPopupLayoutPrefs.PosY);
        }

        _expandedPanelRoot = expandedTf.gameObject;

        _prevHistoryButton = _expandedPanelRoot.transform.Find("FooterNav/HistoryPrevButton")?.GetComponent<Button>();
        _nextHistoryButton = _expandedPanelRoot.transform.Find("FooterNav/HistoryNextButton")?.GetComponent<Button>();
        ApplyHistoryFooterButtonInsets();
        _titleText = _expandedPanelRoot.transform.Find("TitleText")?.GetComponent<TMP_Text>();
        _bodyText = FindHelperBodyUnderExpanded(expandedTf)?.GetComponent<TMP_Text>();

        EnsureHelperBodyScrollPresentation();
        ApplyHelperTitleLeftInsetFromBodyLayout();

        RectTransform parentRt = ResolveOverlayParent();
        if (parentRt)
            host.EnsureUnderWindowsArea(parentRt);

        ApplyLoadedHelperLayout();
        EnsureHelperWindowFocus();
        ApplyHelperTipTextScale();
        EnsureHelperNewBadgeBuilt();
        EnsureHelperNewBadgeDismissTargetsWired();
        return true;
    }

    private void EnsureViewBuilt()
    {
        if (_overlayRoot != null)
        {
            ApplyHistoryFooterButtonInsets();
            ApplyHelperTitleLeftInsetFromBodyLayout();
            EnsureHelperWindowFocus();
            ApplyHelperTipTextScale();
            EnsureHelperNewBadgeBuilt();
            EnsureHelperNewBadgeDismissTargetsWired();
            return;
        }

        if (TryWireExistingHelperPopupWindow())
            return;

        RectTransform parentRt = ResolveOverlayParent();
        if (!parentRt)
        {
            Debug.LogError("[HelperGameplayController] No UI parent — assign FullWindowCanvas tag + WindowsArea child, UICanvas tag, or Ui Parent Override.");
            return;
        }

        _overlayRoot = new GameObject("HelperPopupWindow");
        _overlayRoot.AddComponent<HelperPopupWindow>();
        _overlayRect = _overlayRoot.AddComponent<RectTransform>();
        _overlayRect.SetParent(parentRt, false);
        _overlayRect.anchorMin = Vector2.zero;
        _overlayRect.anchorMax = Vector2.one;
        _overlayRect.offsetMin = Vector2.zero;
        _overlayRect.offsetMax = Vector2.zero;
        _overlayRect.localScale = Vector3.one;

        GameObject dimGo = CreateChild(_overlayRoot.transform, "Dimmer");
        RectTransform dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        Image dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0f);
        dimImg.raycastTarget = true;
        _dimmerImage = dimImg;

        // Helper modal lives under FullWindowCanvas/WindowsArea (covers the full monitor) so the dim Image with 0..1 anchors
        // above would darken (and gobble clicks for) the entire screen. We only want the gameplay strip darkened/blocked,
        // so retarget anchors to the strip camera's normalized viewport rect (unless expand-background is on).
        ApplyDimmerLayoutForExpandSetting(dimGo);

        GameObject glowLayerGo = CreateChild(_overlayRoot.transform, "WhitelistGlowOverlay");
        RectTransform glowLayerRt = glowLayerGo.GetComponent<RectTransform>();
        glowLayerRt.anchorMin = Vector2.zero;
        glowLayerRt.anchorMax = Vector2.one;
        glowLayerRt.offsetMin = Vector2.zero;
        glowLayerRt.offsetMax = Vector2.zero;

        GameObject glowHolderGo = CreateChild(glowLayerGo.transform, "WhitelistGlowHolder");
        _whitelistGlowHolder = glowHolderGo.GetComponent<RectTransform>();
        _whitelistGlowHolder.anchorMin = Vector2.zero;
        _whitelistGlowHolder.anchorMax = Vector2.one;
        _whitelistGlowHolder.offsetMin = Vector2.zero;
        _whitelistGlowHolder.offsetMax = Vector2.zero;

        GameObject panelGo = CreateChild(_overlayRoot.transform, "HelperPanel");
        _helperPanelRt = panelGo.GetComponent<RectTransform>();
        _helperPanelRt.anchorMin = new Vector2(0f, 1f);
        _helperPanelRt.anchorMax = new Vector2(0f, 1f);
        _helperPanelRt.pivot = new Vector2(0f, 1f);
        _helperPanelRt.anchoredPosition = panelAnchoredPosition;
        _helperPanelRt.sizeDelta = panelSize;

        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        panelBg.raycastTarget = true;

        GameObject chromeGo = CreateChild(panelGo.transform, "TopChromeStrip");

        chromeGo.transform.SetAsFirstSibling();

        RectTransform chromeRt = chromeGo.GetComponent<RectTransform>();
        chromeRt.anchorMin = new Vector2(0f, 1f);
        chromeRt.anchorMax = new Vector2(1f, 1f);
        chromeRt.pivot = new Vector2(0.5f, 1f);
        chromeRt.sizeDelta = new Vector2(0f, ExpandedHeaderStripHeight);
        chromeRt.anchoredPosition = Vector2.zero;

        Image chromeTint = chromeGo.AddComponent<Image>();
        chromeTint.color = new Color(0.09f, 0.09f, 0.17f, 0.94f);
        chromeTint.raycastTarget = true;

        UIDragWindow chromeDrag = chromeGo.AddComponent<UIDragWindow>();
        // Bottom-right scale handle sits on the title strip beside +/- / close; omit so it does not cover those controls.
        chromeDrag.AttachWindow(_helperPanelRt, omitTopCornerHandles: false, counterHudCanvasScale: true, omitBottomRightCornerHandle: true);
        chromeDrag.SetRuntimeMemoryKey("HelperPopupWindow.Panel");
        chromeDrag.UsePlayerPrefsForAnchoredPosition(HelperPopupLayoutPrefs.PosX, HelperPopupLayoutPrefs.PosY);

        GameObject chromeTitleGo = CreateChild(chromeGo.transform, "ChromeTitle");
        RectTransform chromeTitleRt = chromeTitleGo.GetComponent<RectTransform>();
        chromeTitleRt.anchorMin = new Vector2(0f, 0f);
        chromeTitleRt.anchorMax = new Vector2(1f, 1f);
        chromeTitleRt.pivot = new Vector2(0f, 0.5f);
        chromeTitleRt.offsetMin = new Vector2(12f, 0f);
        chromeTitleRt.offsetMax = new Vector2(-112f, 0f);

        _chromeStripTitleText = chromeTitleGo.AddComponent<TextMeshProUGUI>();
        _chromeStripTitleText.text = "Helper Window";
        _chromeStripTitleText.fontSize = 16f;
        _chromeStripTitleText.fontStyle = FontStyles.Bold;
        _chromeStripTitleText.color = new Color(0.88f, 0.91f, 0.96f, 1f);
        _chromeStripTitleText.alignment = TextAlignmentOptions.Left;
        _chromeStripTitleText.enableAutoSizing = false;
        _chromeStripTitleText.raycastTarget = false;
        _chromeStripTitleText.gameObject.SetActive(false);

        _helperCloseButtonRoot = CreateStripeChromeButton(chromeGo.transform, "CloseButton",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f),
            new Vector2(30f, 30f),
            new Color(0.35f, 0.08f, 0.08f, 1f));

        Button closeBt = _helperCloseButtonRoot.GetComponent<Button>();
        closeBt.onClick.RemoveAllListeners();
        closeBt.onClick.AddListener(CloseFromUIButton);

        TMP_Text closeLbl = FindTmpOnButton(_helperCloseButtonRoot);
        if (closeLbl)
        {
            closeLbl.text = "X";
            closeLbl.fontSize = 16f;
        }

        GameObject minimizeBtGo = CreateStripeChromeButton(chromeGo.transform, "MinimizeStripeButton",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -8f),
            new Vector2(30f, 30f),
            new Color(0.22f, 0.24f, 0.32f, 1f));

        Button minBt = minimizeBtGo.GetComponent<Button>();
        minBt.onClick.RemoveAllListeners();
        minBt.onClick.AddListener(ToggleMinimizeStripe);
        _minimizeExpandGlyphTmp = FindTmpOnButton(minimizeBtGo);
        if (_minimizeExpandGlyphTmp)
        {
            _minimizeExpandGlyphTmp.text = "-";
            _minimizeExpandGlyphTmp.fontSize = 18f;
        }

        _expandedPanelRoot = CreateChild(panelGo.transform, "ExpandedPresentation");
        RectTransform expRootRt = _expandedPanelRoot.GetComponent<RectTransform>();
        expRootRt.anchorMin = Vector2.zero;
        expRootRt.anchorMax = Vector2.one;
        expRootRt.offsetMin = Vector2.zero;
        expRootRt.offsetMax = new Vector2(0f, -ExpandedHeaderStripHeight);

        GameObject footerGo =
            CreateChild(_expandedPanelRoot.transform, "FooterNav");
        RectTransform footerRt = footerGo.GetComponent<RectTransform>();
        footerRt.anchorMin = new Vector2(0f, 0f);
        footerRt.anchorMax = new Vector2(1f, 0f);
        footerRt.pivot = new Vector2(0.5f, 0f);
        footerRt.sizeDelta = new Vector2(0f, ExpandedFooterNavHeight);
        footerRt.anchoredPosition = Vector2.zero;
        Image footerBg = footerGo.AddComponent<Image>();
        footerBg.color = new Color(0f, 0f, 0f, 0f);
        footerBg.raycastTarget = false;

        GameObject prevBtGo =
            CreateStripeChromeButton(footerGo.transform, "HistoryPrevButton",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 8f),
                new Vector2(28f, 26f),
                new Color(0.16f, 0.18f, 0.26f, 1f));

        Button prevBt = prevBtGo.GetComponent<Button>();
        prevBt.onClick.RemoveAllListeners();
        prevBt.onClick.AddListener(() => OnHistoryNavClicked(-1));

        TMP_Text pt = FindTmpOnButton(prevBtGo);
        if (pt)
        {
            pt.text = "<";
            pt.fontSize = 18f;
        }

        GameObject nextBtGo =
            CreateStripeChromeButton(footerGo.transform, "HistoryNextButton",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f),
                new Vector2(28f, 26f),
                new Color(0.16f, 0.18f, 0.26f, 1f));

        Button nextBt = nextBtGo.GetComponent<Button>();
        nextBt.onClick.RemoveAllListeners();
        nextBt.onClick.AddListener(() => OnHistoryNavClicked(+1));

        TMP_Text nt = FindTmpOnButton(nextBtGo);
        if (nt)
        {
            nt.text = ">";
            nt.fontSize = 18f;
        }

        _prevHistoryButton = prevBt;
        _nextHistoryButton = nextBt;
        ApplyHistoryFooterButtonInsets();
        prevBt.gameObject.SetActive(false);
        nextBt.gameObject.SetActive(false);

        GameObject titleGo =
            CreateChild(_expandedPanelRoot.transform, "TitleText");
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        float titleTopInset = 12f;
        float titleBlockH = 36f;
        titleRt.offsetMax = new Vector2(-16f, -titleTopInset);
        titleRt.offsetMin = new Vector2(16f, -(titleTopInset + titleBlockH));

        _titleText = titleGo.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize = 19f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.color = new Color(0.88f, 0.91f, 0.96f, 1f);
        _titleText.alignment = TextAlignmentOptions.TopLeft;
        _titleText.textWrappingMode = TextWrappingModes.Normal;
        _titleText.raycastTarget = false;

        GameObject scrollGo = CreateChild(_expandedPanelRoot.transform, "BodyScrollView");
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.pivot = new Vector2(0f, 1f);
        float bodyTopInset = titleTopInset + titleBlockH + 6f;
        scrollRt.offsetMin = new Vector2(16f, ExpandedFooterNavHeight + 8f);
        scrollRt.offsetMax = new Vector2(-16f, -bodyTopInset);

        BuildHelperBodyScrollInternals(scrollRt, out ScrollRect bodySr, out RectTransform viewportRt, out RectTransform contentRt,
            out Scrollbar bodyVBar);
        _helperBodyScrollRect = bodySr;
        _helperBodyScrollContent = contentRt;
        _helperBodyScrollbar = bodyVBar;

        GameObject bodyGo = CreateChild(contentRt.transform, "BodyText");
        RectTransform bodyInnerRt = bodyGo.GetComponent<RectTransform>();
        bodyInnerRt.anchorMin = new Vector2(0f, 1f);
        bodyInnerRt.anchorMax = new Vector2(1f, 1f);
        bodyInnerRt.pivot = new Vector2(0.5f, 1f);
        bodyInnerRt.anchoredPosition = Vector2.zero;
        bodyInnerRt.offsetMin = Vector2.zero;
        bodyInnerRt.offsetMax = Vector2.zero;

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        _bodyText.fontSize = 16f;
        _bodyText.color = new Color(0.93f, 0.86f, 0.72f, 1f);
        _bodyText.textWrappingMode = TextWrappingModes.Normal;
        _bodyText.alignment = TextAlignmentOptions.TopJustified;
        ApplyHelperBodyTextScrollLayoutDefaults(_bodyText);

        WireHelperBodyViewportTypewriterSkip();

        dimGo.transform.SetSiblingIndex(0);
        glowLayerGo.transform.SetSiblingIndex(1);
        panelGo.transform.SetSiblingIndex(2);

        HelperPopupWindow hostComp = _overlayRoot.GetComponent<HelperPopupWindow>();
        hostComp.MarkPersistentRoot();
        hostComp.EnsureUnderWindowsArea(parentRt);

        ApplyLoadedHelperLayout();
        ApplyHelperTitleLeftInsetFromBodyLayout();
        EnsureHelperWindowFocus();
        ApplyHelperTipTextScale();

        EnsureHelperNewBadgeBuilt();
        EnsureHelperNewBadgeDismissTargetsWired();

        _overlayRoot.SetActive(false);
    }

    /// <summary>
    /// Same stacking behavior as other <see cref="UIWindowFocus"/> windows: bring <see cref="_overlayRoot"/> above
    /// WindowsArea siblings when the helper opens or the player clicks the panel, chrome, body, or dimmer.
    /// </summary>
    private void EnsureHelperWindowFocus()
    {
        if (_overlayRoot == null || _helperPanelRt == null)
            return;

        Transform overlayTf = _overlayRoot.transform;

        void WireFocusTarget(GameObject go)
        {
            if (!go)
                return;

            UIWindowFocus f = go.GetComponent<UIWindowFocus>();
            if (!f)
                f = go.AddComponent<UIWindowFocus>();

            f.SetBringToFrontTransform(overlayTf);
        }

        if (!_overlayRoot.TryGetComponent(out UIWindowFocus _))
            _overlayRoot.AddComponent<UIWindowFocus>();

        WireFocusTarget(_helperPanelRt.gameObject);

        Transform chrome = _helperPanelRt.Find("TopChromeStrip");
        if (chrome)
            WireFocusTarget(chrome.gameObject);

        if (_bodyText)
            WireFocusTarget(_bodyText.gameObject);

        if (_helperBodyScrollRect)
            WireFocusTarget(_helperBodyScrollRect.gameObject);

        Transform dim = _overlayRoot.transform.Find("Dimmer");
        if (dim)
            WireFocusTarget(dim.gameObject);

        EnsureHelperModalOverlayDrawOrder();
    }

    /// <summary>
    /// Restores Dimmer → WhitelistGlowOverlay → HelperPanel sibling order, then (while the modal dimmer is visible) gives
    /// <b>only</b> <see cref="_helperPanelRt"/> a nested canvas so it draws above the blackout. Glow stays on the root canvas
    /// (above the dimmer via hierarchy) — we do not add a Canvas on <c>WhitelistGlowOverlay</c> (avoids MissingComponent /
    /// RequireComponent issues with raycasters and keeps glow logic unchanged).
    /// </summary>
    private void EnsureHelperModalOverlayDrawOrder()
    {
        if (_overlayRoot == null || _helperPanelRt == null)
            return;

        Transform rootTf = _overlayRoot.transform;
        Transform dimTf = rootTf.Find("Dimmer");
        Transform glowLayerTf = rootTf.Find("WhitelistGlowOverlay");

        if (dimTf)
            dimTf.SetSiblingIndex(0);
        if (glowLayerTf)
            glowLayerTf.SetSiblingIndex(dimTf != null ? 1 : 0);

        _helperPanelRt.SetAsLastSibling();

        bool modalDimmerVisible =
            _overlayRoot.activeSelf &&
            _dimmerImage &&
            _dimmerImage.isActiveAndEnabled &&
            _dimmerImage.enabled &&
            _dimmerImage.color.a > 0.05f;

        if (!modalDimmerVisible)
        {
            ClearHelperModalNestedCanvasOverrides();
            return;
        }

        int panelOrder = Mathf.Clamp(canvasSortOrder + Mathf.Max(panelSortDelta, 2), -30000, 32760);

        if (!_helperPanelRt.TryGetComponent(out Canvas _) &&
            _helperPanelRt.TryGetComponent(out GraphicRaycaster orphanRay))
        {
            Destroy(orphanRay);
        }

        Canvas pc = _helperPanelRt.GetComponent<Canvas>();
        if (!pc)
        {
            pc = _helperPanelRt.gameObject.AddComponent<Canvas>();
            _helperPanelModalBreakoutOwned = true;
        }

        pc.overrideSorting = true;
        pc.sortingOrder = panelOrder;
        if (!_helperPanelRt.TryGetComponent(out GraphicRaycaster _))
            _helperPanelRt.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void ClearHelperModalNestedCanvasOverrides()
    {
        if (_overlayRoot)
        {
            Transform glowLayerTf = _overlayRoot.transform.Find("WhitelistGlowOverlay");
            if (glowLayerTf)
            {
                if (glowLayerTf.TryGetComponent(out GraphicRaycaster gr))
                    Destroy(gr);

                if (glowLayerTf.TryGetComponent(out Canvas gc))
                    Destroy(gc);
            }
        }

        if (!_helperPanelRt)
            return;

        if (_helperPanelModalBreakoutOwned)
        {
            if (_helperPanelRt.TryGetComponent(out GraphicRaycaster pr))
                Destroy(pr);

            if (_helperPanelRt.TryGetComponent(out Canvas ownedCanvas))
                Destroy(ownedCanvas);

            _helperPanelModalBreakoutOwned = false;
            return;
        }

        if (_helperPanelRt.TryGetComponent(out Canvas pc) && pc.overrideSorting)
        {
            pc.overrideSorting = false;
            pc.sortingOrder = 0;
        }
    }

    private void RebuildWhitelistGlowOverlays()
    {
        ClearWhitelistGlowOverlays();

        if (!whitelistGlowAboveDimmer ||
            !IsHelperExpandedPresentation() ||
            !ViewingLatestHistoryEntry() ||
            _activeDefinition == null ||
            _whitelistGlowHolder == null ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            !_activeDefinition.HasConfiguredWhitelistInteractIds())
            return;

        if (!worldCameraForWhitelistGlow)
        {
            GameObject camGo = GameObject.Find("StripCamera");
            if (camGo)
                worldCameraForWhitelistGlow = camGo.GetComponent<Camera>();
        }

        Camera cam = worldCameraForWhitelistGlow;

        if (cam)
        {
            HelperWhitelistInteractTarget[] markers =
                FindObjectsByType<HelperWhitelistInteractTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var sources = new List<SpriteRenderer>(24);
            var seen = new HashSet<SpriteRenderer>();

            for (int i = 0; i < markers.Length; i++)
            {
                HelperWhitelistInteractTarget marker = markers[i];
                if (!marker || !_activeDefinition.MatchesWhitelistId(marker.InteractionId))
                    continue;

                CollectWhitelistPresentationSprites(marker, visualsSubtreeChildName, sources, seen);
            }

            sources.Sort(static (a, b) =>
            {
                int layerCmp = SortingLayer.GetLayerValueFromID(a.sortingLayerID)
                    .CompareTo(SortingLayer.GetLayerValueFromID(b.sortingLayerID));
                if (layerCmp != 0)
                    return layerCmp;
                int orderCmp = a.sortingOrder.CompareTo(b.sortingOrder);
                return orderCmp != 0 ? orderCmp : string.CompareOrdinal(a.name, b.name);
            });

            for (int i = 0; i < sources.Count; i++)
            {
                SpriteRenderer sr = sources[i];
                if (!sr || !sr.sprite)
                    continue;

                Image haloImg = null;
                RectTransform haloRt = null;
                if (whitelistWorldGlowHaloUniformScale > 1.005f)
                {
                    GameObject haloGo = new GameObject("WhitelistGlowWorldHalo", typeof(RectTransform));
                    haloRt = haloGo.GetComponent<RectTransform>();
                    haloRt.SetParent(_whitelistGlowHolder, false);
                    haloImg = haloGo.AddComponent<Image>();
                    haloImg.sprite = sr.sprite;
                    haloImg.raycastTarget = false;
                    haloImg.preserveAspect = false;
                    FitSpriteRendererOverlayRect(sr, haloRt, _whitelistGlowHolder, cam);
                    Vector2 hs = haloRt.sizeDelta;
                    haloRt.sizeDelta = hs * whitelistWorldGlowHaloUniformScale;
                    Color hc0 = PulsedWhitelistGlowColor(0f);
                    hc0.a *= whitelistWorldGlowHaloAlphaScale;
                    haloImg.color = hc0;
                }

                GameObject go = new GameObject("WhitelistGlowSprite", typeof(RectTransform));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(_whitelistGlowHolder, false);

                Image img = go.AddComponent<Image>();
                img.sprite = sr.sprite;
                img.raycastTarget = false;
                img.preserveAspect = false;

                FitSpriteRendererOverlayRect(sr, rt, _whitelistGlowHolder, cam);
                img.color = PulsedWhitelistGlowColor(0f);

                _whitelistGlowLinks.Add(new WhitelistGlowLink
                {
                    SourceSprite = sr,
                    SourceGraphic = null,
                    GlowImg = img,
                    GlowHaloImg = haloImg,
                });
            }
        }

        AppendWhitelistUiGlowOverlaysIntoHolder();
        EnsureHelperModalOverlayDrawOrder();
    }

    /// <remarks>Whitelist UI rects use each Canvas's projected screen bounds; no StripCamera required.</remarks>
    private void AppendWhitelistUiGlowOverlaysIntoHolder()
    {
        if (!IsHelperExpandedPresentation() ||
            !ViewingLatestHistoryEntry() ||
            _activeDefinition == null ||
            _whitelistGlowHolder == null ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            !_activeDefinition.HasConfiguredWhitelistInteractIds())
            return;

        HelperWhitelistUiInteractTarget[] uis =
            FindObjectsByType<HelperWhitelistUiInteractTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < uis.Length; i++)
        {
            HelperWhitelistUiInteractTarget row = uis[i];
            if (!row || !_activeDefinition.MatchesWhitelistId(row.InteractionId))
                continue;

            Graphic gfx = row.GlowSourceGraphic;
            if (!gfx || !gfx.isActiveAndEnabled)
                continue;

            Image haloImg = null;
            RectTransform haloRt = null;

            if (whitelistUiGlowHaloPadding > 0f)
            {
                GameObject haloGo = new GameObject("WhitelistGlowUiHalo", typeof(RectTransform));
                haloRt = haloGo.GetComponent<RectTransform>();
                haloRt.SetParent(_whitelistGlowHolder, false);
                haloImg = haloGo.AddComponent<Image>();
                haloImg.sprite = GetOrCreateWhitelistGlowUiFallbackSprite();
                haloImg.type = Image.Type.Simple;
                haloImg.raycastTarget = false;
                haloImg.preserveAspect = false;
                haloImg.color = PulsedWhitelistGlowColor(0f);
            }

            GameObject go = new GameObject("WhitelistGlowUiGraphic", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(_whitelistGlowHolder, false);

            Image img = go.AddComponent<Image>();
            if (gfx is Image uiImage && uiImage.sprite)
            {
                img.sprite = uiImage.sprite;
                CopyImagePresentationForGlowClone(img, uiImage);
            }
            else if (gfx is RawImage)
            {
                img.sprite = GetOrCreateWhitelistGlowUiFallbackSprite();
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
            }
            else
            {
                img.sprite = GetOrCreateWhitelistGlowUiFallbackSprite();
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
            }

            img.raycastTarget = false;

            FitUiGraphicOverlayRect(gfx, rt, _whitelistGlowHolder, _uiWorldCornersScratch);

            if (haloImg && haloRt != null)
                SyncUiGlowHaloUnderCore(haloRt, rt, adjustSiblingOrder: true);

            img.color = PulsedWhitelistGlowColor(0f);

            if (haloImg)
            {
                Color hc = PulsedWhitelistGlowColor(0f);
                hc.a *= whitelistUiGlowHaloAlphaScale;
                haloImg.color = hc;
            }

            _whitelistGlowLinks.Add(new WhitelistGlowLink
            {
                SourceSprite = null,
                SourceGraphic = gfx,
                GlowImg = img,
                GlowHaloImg = haloImg,
            });
        }
    }

    private void SyncUiGlowHaloUnderCore(RectTransform haloRt, RectTransform coreRt, bool adjustSiblingOrder)
    {
        if (!haloRt || !coreRt)
            return;

        haloRt.anchorMin = haloRt.anchorMax = new Vector2(0.5f, 0.5f);
        haloRt.pivot = new Vector2(0.5f, 0.5f);
        haloRt.anchoredPosition = coreRt.anchoredPosition;
        haloRt.localRotation = coreRt.localRotation;
        float haloScale = Mathf.Max(1f, whitelistUiGlowHaloUniformScale);
        haloRt.localScale = coreRt.localScale * haloScale;

        Vector2 pad = Vector2.one * (whitelistUiGlowHaloPadding * 2f);
        haloRt.sizeDelta = coreRt.sizeDelta + pad;

        if (adjustSiblingOrder)
            haloRt.SetSiblingIndex(Mathf.Max(0, coreRt.GetSiblingIndex() - 1));
    }

    private static void CopyImagePresentationForGlowClone(Image dst, Image src)
    {
        if (!dst || !src)
            return;

        dst.type = src.type;
        dst.preserveAspect = src.preserveAspect;
        dst.fillCenter = src.fillCenter;
        dst.fillMethod = src.fillMethod;
        dst.fillOrigin = src.fillOrigin;
        dst.fillAmount = src.fillAmount;
        dst.fillClockwise = src.fillClockwise;
        dst.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        dst.useSpriteMesh = src.useSpriteMesh;

        if (src.sprite)
            dst.sprite = src.sprite;
    }

    private static Sprite _whitelistGlowUiFallbackSprite;

    private static Sprite GetOrCreateWhitelistGlowUiFallbackSprite()
    {
        if (_whitelistGlowUiFallbackSprite)
            return _whitelistGlowUiFallbackSprite;

        Texture2D t = Texture2D.whiteTexture;
        float w = Mathf.Max(1f, t.width);
        float h = Mathf.Max(1f, t.height);

        _whitelistGlowUiFallbackSprite = Sprite.Create(
            t,
            new Rect(0f, 0f, w, h),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);

        return _whitelistGlowUiFallbackSprite;
    }

    /// <summary>Projects a UI <see cref="Graphic"/> onto the whitelist glow holder.</summary>
    private static void FitUiGraphicOverlayRect(
        Graphic gfx,
        RectTransform imgRt,
        RectTransform holder,
        Vector3[] worldCornersScratch)
    {
        if (!gfx || worldCornersScratch == null || worldCornersScratch.Length < 4)
            return;

        gfx.rectTransform.GetWorldCorners(worldCornersScratch);

        Camera projCam = GetCanvasProjectionCamera(gfx.canvas);

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        bool anyOk = false;

        for (int i = 0; i < 4; i++)
        {
            Vector3 sp = RectTransformUtility.WorldToScreenPoint(projCam, worldCornersScratch[i]);
            if (sp.z < 0f)
                continue;

            anyOk = true;
            minX = Mathf.Min(minX, sp.x);
            maxX = Mathf.Max(maxX, sp.x);
            minY = Mathf.Min(minY, sp.y);
            maxY = Mathf.Max(maxY, sp.y);
        }

        if (!anyOk || minX >= maxX || minY >= maxY)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(holder, new Vector2(minX, minY), null, out Vector2 localBl);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(holder, new Vector2(maxX, maxY), null, out Vector2 localTr);

        imgRt.anchorMin = imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.sizeDelta = new Vector2(Mathf.Abs(localTr.x - localBl.x), Mathf.Abs(localTr.y - localBl.y));
        imgRt.anchoredPosition = (localBl + localTr) * 0.5f;

        Quaternion worldRot = gfx.rectTransform.rotation;
        imgRt.localRotation = Quaternion.Euler(0f, 0f, worldRot.eulerAngles.z);

        Vector3 lossy = gfx.rectTransform.lossyScale;
        imgRt.localScale = new Vector3(Mathf.Sign(lossy.x), Mathf.Sign(lossy.y), 1f);
    }

    private static Camera GetCanvasProjectionCamera(Canvas wc)
    {
        if (!wc)
            return null;

        RenderMode rm = wc.renderMode;
        return rm switch
        {
            RenderMode.ScreenSpaceOverlay => null,
            RenderMode.ScreenSpaceCamera => wc.worldCamera,
            RenderMode.WorldSpace => wc.worldCamera,
            _ => null,
        };
    }

    private void ClearWhitelistGlowOverlays()
    {
        _whitelistGlowLinks.Clear();
        if (_whitelistGlowHolder)
        {
            for (int i = _whitelistGlowHolder.childCount - 1; i >= 0; i--)
                Destroy(_whitelistGlowHolder.GetChild(i).gameObject);
        }

        NormalizeAllWhitelistMarkersStripDrawOrder();
    }

    private void SyncAndPulseWhitelistGlow()
    {
        if (!IsHelperExpandedPresentation() ||
            !ViewingLatestHistoryEntry() ||
            !whitelistGlowAboveDimmer ||
            _whitelistGlowLinks.Count == 0 ||
            !_whitelistGlowHolder)
            return;

        if (!worldCameraForWhitelistGlow)
        {
            GameObject camGo = GameObject.Find("StripCamera");
            if (camGo)
                worldCameraForWhitelistGlow = camGo.GetComponent<Camera>();
        }

        Camera stripCam = worldCameraForWhitelistGlow;

        float pulseT = (Mathf.Sin(Time.time * whitelistGlowPulseSpeed) + 1f) * 0.5f;
        Color glowCol = PulsedWhitelistGlowColor(pulseT);

        for (int i = _whitelistGlowLinks.Count - 1; i >= 0; i--)
        {
            WhitelistGlowLink link = _whitelistGlowLinks[i];

            if (!link.GlowImg)
            {
                if (link.GlowHaloImg)
                    Destroy(link.GlowHaloImg.gameObject);

                _whitelistGlowLinks.RemoveAt(i);
                continue;
            }

            if (link.SourceSprite)
            {
                if (!stripCam ||
                    link.SourceSprite.sprite == null)
                {
                    if (link.GlowHaloImg)
                        Destroy(link.GlowHaloImg.gameObject);
                    Destroy(link.GlowImg.gameObject);
                    _whitelistGlowLinks.RemoveAt(i);
                    continue;
                }

                FitSpriteRendererOverlayRect(link.SourceSprite, link.GlowImg.rectTransform, _whitelistGlowHolder, stripCam);

                if (link.GlowHaloImg)
                {
                    RectTransform haloRt = link.GlowHaloImg.rectTransform;
                    FitSpriteRendererOverlayRect(link.SourceSprite, haloRt, _whitelistGlowHolder, stripCam);
                    Vector2 hs = haloRt.sizeDelta;
                    haloRt.sizeDelta = hs * Mathf.Max(1f, whitelistWorldGlowHaloUniformScale);
                    link.GlowHaloImg.sprite = link.SourceSprite.sprite;
                }
            }
            else if (link.SourceGraphic)
            {
                if (!link.SourceGraphic.isActiveAndEnabled || !link.SourceGraphic.canvas)
                {
                    if (link.GlowHaloImg)
                        Destroy(link.GlowHaloImg.gameObject);
                    Destroy(link.GlowImg.gameObject);
                    _whitelistGlowLinks.RemoveAt(i);
                    continue;
                }

                FitUiGraphicOverlayRect(link.SourceGraphic, link.GlowImg.rectTransform, _whitelistGlowHolder, _uiWorldCornersScratch);

                if (link.SourceGraphic is Image srcGlow && srcGlow.sprite)
                {
                    link.GlowImg.sprite = srcGlow.sprite;
                    CopyImagePresentationForGlowClone(link.GlowImg, srcGlow);
                }

                if (!link.GlowImg.sprite)
                    link.GlowImg.sprite = GetOrCreateWhitelistGlowUiFallbackSprite();

                if (link.GlowHaloImg)
                    SyncUiGlowHaloUnderCore(link.GlowHaloImg.rectTransform, link.GlowImg.rectTransform,
                        adjustSiblingOrder: false);
            }
            else
            {
                if (link.GlowHaloImg)
                    Destroy(link.GlowHaloImg.gameObject);
                Destroy(link.GlowImg.gameObject);
                _whitelistGlowLinks.RemoveAt(i);
                continue;
            }

            link.GlowImg.color = glowCol;

            if (link.GlowHaloImg)
            {
                Color haloC = glowCol;
                haloC.a *= link.SourceSprite
                    ? whitelistWorldGlowHaloAlphaScale
                    : whitelistUiGlowHaloAlphaScale;
                link.GlowHaloImg.color = haloC;
            }
        }
    }

    private Color PulsedWhitelistGlowColor(float pulseT01)
    {
        float lo = Mathf.Min(whitelistGlowPulseAlphaMin, whitelistGlowPulseAlphaMax);
        float hi = Mathf.Max(whitelistGlowPulseAlphaMin, whitelistGlowPulseAlphaMax);
        Color c = whitelistHelperTint;
        c.a = Mathf.Lerp(lo, hi, Mathf.Clamp01(pulseT01));
        return c;
    }

    private static void FitSpriteRendererOverlayRect(
        SpriteRenderer sr,
        RectTransform imgRt,
        RectTransform holder,
        Camera cam)
    {
        Bounds b = sr.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        bool anyInFront = false;

        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 world = c + new Vector3(e.x * ix, e.y * iy, e.z * iz);
                    Vector3 sp = cam.WorldToScreenPoint(world);
                    if (sp.z < 0f)
                        continue;
                    anyInFront = true;
                    minX = Mathf.Min(minX, sp.x);
                    maxX = Mathf.Max(maxX, sp.x);
                    minY = Mathf.Min(minY, sp.y);
                    maxY = Mathf.Max(maxY, sp.y);
                }
            }
        }

        if (!anyInFront || minX >= maxX || minY >= maxY)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(holder, new Vector2(minX, minY), null, out Vector2 localBl);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(holder, new Vector2(maxX, maxY), null, out Vector2 localTr);

        imgRt.anchorMin = imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.sizeDelta = new Vector2(Mathf.Abs(localTr.x - localBl.x), Mathf.Abs(localTr.y - localBl.y));
        imgRt.anchoredPosition = (localBl + localTr) * 0.5f;

        imgRt.localRotation = Quaternion.Euler(0f, 0f, sr.transform.eulerAngles.z);

        Vector3 lossy = sr.transform.lossyScale;
        imgRt.localScale = new Vector3(Mathf.Sign(lossy.x), Mathf.Sign(lossy.y), 1f);
    }

    /// <remarks>
    /// Prefers sprites under <paramref name="visualsSubtreeChildName"/> when present on any ancestor walk from the marker,
    /// so complex NPCs can keep a dormant root <see cref="SpriteRenderer"/>.
    /// </remarks>
    private static void CollectWhitelistPresentationSprites(
        HelperWhitelistInteractTarget marker,
        string visualsSubtreeChildName,
        List<SpriteRenderer> sources,
        HashSet<SpriteRenderer> seen)
    {
        Transform subtree = null;

        if (!string.IsNullOrWhiteSpace(visualsSubtreeChildName))
        {
            string trimmedName = visualsSubtreeChildName.Trim();
            subtree = FindDirectChildAmongAncestors(marker.transform, trimmedName);
        }

        SpriteRenderer[] chunk = subtree != null
            ? subtree.GetComponentsInChildren<SpriteRenderer>(true)
            : marker.GetComponentsInChildren<SpriteRenderer>(true);

        for (int c = 0; c < chunk.Length; c++)
        {
            SpriteRenderer sr = chunk[c];
            if (!sr || !sr.sprite || !seen.Add(sr))
                continue;
            sources.Add(sr);
        }
    }

    /// <summary>Returns the first Transform <c>t.Find(<paramref name="childName"/>)</c> walking from <paramref name="start"/> up through parents.</summary>
    private static Transform FindDirectChildAmongAncestors(Transform start, string childName)
    {
        if (!start || string.IsNullOrEmpty(childName))
            return null;

        for (Transform walk = start; walk != null; walk = walk.parent)
        {
            Transform child = walk.Find(childName);
            if (child != null)
                return child;
        }

        return null;
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        return go;
    }

    private GameObject CreateStripeChromeButton(
        Transform parent,
        string objName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color graphicColor)
    {
        GameObject btGo =
            new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));

        btGo.transform.SetParent(parent, false);

        RectTransform btRt = btGo.GetComponent<RectTransform>();
        btRt.anchorMin = anchorMin;
        btRt.anchorMax = anchorMax;
        btRt.pivot = pivot;
        btRt.anchoredPosition = anchoredPosition;
        btRt.sizeDelta = sizeDelta;
        btRt.localScale = Vector3.one;

        Image img = btGo.GetComponent<Image>();
        img.color = graphicColor;
        img.raycastTarget = true;

        GameObject lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(btGo.transform, false);

        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        RectTransform lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        Button btn = btGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        return btGo;
    }

    private static TMP_Text FindTmpOnButton(GameObject buttonRoot)
    {
        Transform t = buttonRoot ? buttonRoot.transform.Find("Label") : null;
        return t ? t.GetComponent<TMP_Text>() : null;
    }

    private void ApplyHistoryFooterButtonInsets()
    {
        float inset = Mathf.Max(0f, historyFooterButtonEdgeInset);

        if (_prevHistoryButton)
        {
            RectTransform prevRt = _prevHistoryButton.transform as RectTransform;
            if (prevRt)
            {
                Vector2 p = prevRt.anchoredPosition;
                p.x = inset;
                prevRt.anchoredPosition = p;
            }
        }

        if (_nextHistoryButton)
        {
            RectTransform nextRt = _nextHistoryButton.transform as RectTransform;
            if (nextRt)
            {
                Vector2 p = nextRt.anchoredPosition;
                p.x = -inset;
                nextRt.anchoredPosition = p;
            }
        }
    }

    private RectTransform ResolveOverlayParent()
    {
        // Helper popup should behave like a regular window, living under FullWindowCanvas/WindowsArea.
        // This also prevents the HUD resize slider (Strip canvas) from scaling the helper.
        RectTransform windowsArea = HelperPopupWindow.ResolveWindowsArea();
        if (windowsArea)
            return windowsArea;

        // Fallback to legacy overlay parenting when FullWindowCanvas is not present (e.g. template scenes).
        if (preferFullscreenUiCanvasForHelper)
        {
            GameObject taggedFullscreen = GameObject.FindGameObjectWithTag("UICanvas");
            if (taggedFullscreen != null)
                return taggedFullscreen.transform as RectTransform;
        }

        return ResolveUiParent();
    }

    private RectTransform ResolveUiParent()
    {
        if (uiParentOverride)
            return uiParentOverride;

        GameObject tagged = GameObject.FindGameObjectWithTag("UICanvas");
        return tagged ? tagged.transform as RectTransform : null;
    }

    /// <summary>When expand-background is on, helper dim covers the full window; otherwise only the strip viewport.</summary>
    public static void RefreshDimmerLayoutForExpandSetting()
    {
        if (Instance == null)
            return;

        if (Application.isPlaying)
            Instance.StartCoroutine(Instance.CoRefreshDimmerLayoutNextFrame());
        else
            Instance.ApplyDimmerLayoutForExpandSetting();
    }

    private IEnumerator CoRefreshDimmerLayoutNextFrame()
    {
        yield return null;
        ApplyDimmerLayoutForExpandSetting();
    }

    private void ApplyDimmerLayoutForExpandSetting()
    {
        if (!_dimmerImage)
            return;

        ApplyDimmerLayoutForExpandSetting(_dimmerImage.gameObject);
    }

    private static void ApplyDimmerLayoutForExpandSetting(GameObject dimGo)
    {
        if (!dimGo)
            return;

        RectTransform dimRt = dimGo.GetComponent<RectTransform>();
        if (!dimRt)
            return;

        StripUIViewportFollower follower = dimGo.GetComponent<StripUIViewportFollower>();

        if (ToggleSettingsStore.Get(ToggleSettingId.ExpandStripBackground))
        {
            if (follower)
                follower.enabled = false;

            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            return;
        }

        if (follower)
            follower.enabled = true;
        else
            follower = dimGo.AddComponent<StripUIViewportFollower>();

        ConstrainDimmerToStripViewport(dimGo);
    }

    /// <summary>
    /// Attaches (or reuses) a <see cref="StripUIViewportFollower"/> on the dimmer GameObject so its anchors track the
    /// strip camera's normalized viewport rect — keeps the helper modal dim limited to the gameplay strip area instead
    /// of blacking out the entire monitor (and intercepting clicks on floating windows / panels above the strip).
    /// </summary>
    private static void ConstrainDimmerToStripViewport(GameObject dimGo)
    {
        if (!dimGo)
            return;

        StripCameraController ctrl = UnityEngine.Object.FindFirstObjectByType<StripCameraController>(FindObjectsInactive.Include);
        Camera stripCam = ctrl ? ctrl.GetComponent<Camera>() : null;
        if (!stripCam)
            return;

        StripUIViewportFollower follower = dimGo.GetComponent<StripUIViewportFollower>();
        if (!follower)
            follower = dimGo.AddComponent<StripUIViewportFollower>();
        follower.enabled = true;
        follower.Bind(stripCam);
    }
}

/// <summary>Forwards panel pointer-down so in-progress helper body text finishes immediately (parity with NPCDialogueBoxUI).</summary>
[DisallowMultipleComponent]
public sealed class HelperTypewriterPanelSkip : MonoBehaviour, IPointerDownHandler
{
    private HelperGameplayController _host;

    public void Init(HelperGameplayController host) => _host = host;

    public void OnPointerDown(PointerEventData eventData)
    {
        _host?.CompleteBodyTypewriter();
        _host?.DismissHelperNewBadgeFromPanelPointer();
    }
}

/// <summary>Forwards pointer-down on helper chrome / title / panel to hide the &quot;! new&quot; badge.</summary>
[DisallowMultipleComponent]
public sealed class HelperPanelDismissNewBadgeOnPointerDown : MonoBehaviour, IPointerDownHandler
{
    private HelperGameplayController _host;

    public void Init(HelperGameplayController host) => _host = host;

    public void OnPointerDown(PointerEventData eventData) =>
        _host?.DismissHelperNewBadgeFromPanelPointer();
}
