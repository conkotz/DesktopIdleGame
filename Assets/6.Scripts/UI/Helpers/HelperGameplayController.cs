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

    [Tooltip("Nested canvas sorting: glow layer between root (dimmer) and panel.")]
    [SerializeField] private int whitelistGlowSortDelta = 1;

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
    [SerializeField] private Color whitelistHelperTint = new Color(1f, 0.92f, 0.35f, 1f);

    [Tooltip("Alpha pulse extremes for the overlay glow (0 = fades to fully transparent).")]
    [SerializeField] [Range(0f, 1f)] private float whitelistGlowPulseAlphaMin;

    [SerializeField] [Range(0f, 1f)] private float whitelistGlowPulseAlphaMax = 0.55f;

    [SerializeField] private float whitelistGlowPulseSpeed = 2.8f;

    [Tooltip("When Glow Above Dimmer is off: tints world SpriteRenderers (mostly hidden under fullscreen dim).")]
    [SerializeField] [Range(0f, 1f)] private float whitelistHelperTintStrength = 0.42f;

    [SerializeField] private float whitelistTintLerpSpeed = 14f;

    [Header("Whitelist UI glow (toolbar / buttons)")]
    [Tooltip("Soft outer pad (each side) behind the icon clone so UI targets read like the NPC pulse, not a flat panel fill.")]
    [SerializeField] private float whitelistUiGlowHaloPadding = 12f;

    [SerializeField] [Range(1f, 1.25f)] private float whitelistUiGlowHaloUniformScale = 1.06f;

    [SerializeField] [Range(0.1f, 1f)] private float whitelistUiGlowHaloAlphaScale = 0.55f;

    [SerializeField] private Vector2 panelSize = new(560f, 280f);

    [SerializeField] private Vector2 panelAnchoredPosition = new(40f, -88f);

    [Tooltip(
        "How far above the helper front panel Canvas sort order to place nested canvases built on whitelist UI markers (toolbar buttons). Larger = safer over other overlays.")]
    [SerializeField] private int whitelistUiCanvasSortBeyondPanel = 125;

    [Header("Body typewriter")]
    [Tooltip(
        "How fast the helper body reveals (TMP visible characters per second; full paragraph layout while typing — same idea as NPCDialogueBoxUI). Higher = faster. 0 = one character per frame.")]
    [SerializeField] private float typewriterCharactersPerSecond = 48f;

    private Coroutine _bodyTypewriterCo;
    private string _bodyTypewriterFullPlain;

    private bool _activeUsesWorldWhitelistRouting;

    private HelperPopupDefinition _activeDefinition;

    private const float ExpandedHeaderStripHeight = 40f;

    private const float ExpandedFooterNavHeight = 34f;

    /// <summary>Last non–header-only helper panel height (preserves resize across minimize).</summary>
    private Vector2 _lastExpandedPanelSizeDelta;

    private bool HasActiveTutorialDefinitionPending() => _activeDefinition != null;

    private bool IsHelperExpandedPresentation() =>
        _expandedPanelRoot != null && _expandedPanelRoot.activeSelf;

    private bool IsActiveHelperExpandedWithModalGameplayLock() =>
        _activeDefinition != null &&
        _activeDefinition.darkenScreenAndLockGameplay &&
        IsHelperExpandedPresentation();

    private GameObject _expandedPanelRoot;

    private RectTransform _helperPanelRt;

    private Image _dimmerImage;

    private TMP_Text _chromeStripTitleText;

    private TMP_Text _minimizeExpandGlyphTmp;

    private Button _prevHistoryButton;

    private Button _nextHistoryButton;

    private struct HelperDisplayedMessageSnap
    {
        public string TitlePlain;
        public string BodyPlain;
    }

    private readonly List<HelperDisplayedMessageSnap> _sessionMessageHistory = new(16);

    private int _historyViewIndex;

    private PlayerController _player;

    private Inventory _inventoryForHelpers;

    private QuestProgressManager _questProgressForHelpers;

    private SkillsManager _skillsManagerForHelpers;

    /// <summary>Snapshot before each <see cref="Inventory.OnInventoryChanged"/> callback for trigger math.</summary>
    private int _lastSeenInventoryTotalUnitsBeforeChange;

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
    private Canvas _overlayCanvas;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;

    private Canvas _panelFrontCanvas;

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
        /// <summary>Optional soft rim drawn under <see cref="GlowImg"/> for UI sources only.</summary>
        public Image GlowHaloImg;
    }

    private struct WhitelistTintState
    {
        public SpriteRenderer Renderer;
        public Color SavedColor;
    }

    private readonly List<WhitelistTintState> _whitelistPresentationTints = new();

    /// <summary>Call from <see cref="WorldInputRouter2D"/> after routing a click that hit a whitelist collider while blocking.</summary>
    public static void NotifyWhitelistWorldRouteHandled()
    {
        if (Instance == null)
            return;

        Instance.TryDismiss(HelperDismissMode.InteractWhitelistDismiss);
    }

    /// <summary>
    /// Call from <see cref="HelperWhitelistUiInteractTarget"/> when the player activates a whitelist id (toolbar / UI).
    /// </summary>
    public static void NotifyWhitelistUiInteract(string interactionIdMarker)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(interactionIdMarker))
            return;

        Instance.TryDismiss(HelperDismissMode.InteractWhitelistDismiss, interactionIdMarker.Trim());
    }

    /// <summary>True while a scripted helper expects world / strip picks for whitelist dismiss routing.</summary>
    public static bool UsesWorldWhitelistRouting =>
        Instance != null && Instance._activeUsesWorldWhitelistRouting;

    /// <summary>Whitelist ids configured on active helper.</summary>
    public static bool ActiveHelperUsesWorldWhitelist =>
        Instance != null &&
        Instance._activeDefinition != null &&
        Instance._activeDefinition.whitelistedInteractionIds != null &&
        Instance._activeDefinition.whitelistedInteractionIds.Length > 0;

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
                (d.whitelistedInteractionIds == null || d.whitelistedInteractionIds.Length == 0))
            {
                Debug.LogWarning(
                    $"[HelperGameplayController] Helper '{d.helperId}' uses Interact Whitelist Dismiss but Whitelisted Interaction Ids is empty.",
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
            DismissSilent();
            Instance = null;
        }

        UnsubscribeInventoryHelpers();
        UnsubscribeQuestProgressHelpers();
        UnsubscribeSkillsHelpers();

        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= HandleLevelStarted;
    }

    private IEnumerator Start()
    {
        yield return HelperProgressStore.WaitUntilHydratedFromSave();

        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        SubscribeInventoryHelpers();
        SubscribeQuestProgressHelpers();
        SubscribeSkillsHelpers();

        GameplayLevelBootstrapper boots = GameplayLevelBootstrapper.Instance;
        if (boots == null)
        {
            Debug.LogWarning(
                "[HelperGameplayController] No GameplayLevelBootstrapper — First Visit helpers will never run.",
                this);
            yield break;
        }

        boots.OnLevelStarted += HandleLevelStarted;

        // Execution order: our Start runs after Bootstrapper.Start — replay the active map once.
        if (boots.ActiveDefinition != null)
            HandleLevelStarted(boots.ActiveDefinition);
    }

    private void RegisterProgressKeys()
    {
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
            HelperProgressStore.RegisterDefinition(definitions[i]);
    }

    private void HandleLevelStarted(MapNodeDefinition node)
    {
        if (_activeDefinition != null || node == null || definitions == null || definitions.Length == 0)
            return;

        StartCoroutine(EvaluateMapEntryNextFrame(node));
    }

    /// <summary>Close an open helper when the player disables &quot;Show help popups&quot; — does not mark the tip dismissed.</summary>
    private void OnToggleSettingsChanged(ToggleSettingId id, bool _)
    {
        if (id != ToggleSettingId.ShowHelpPopups)
            return;

        if ((_activeDefinition != null || (_overlayRoot && _overlayRoot.activeSelf)) &&
            !ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
            DismissSilent();
    }

    private IEnumerator EvaluateMapEntryNextFrame(MapNodeDefinition node)
    {
        yield return null;

        if (_activeDefinition != null)
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

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);

        if (_activeDefinition == null)
        {
            EvaluateInventoryItemCountReachedHelpers();
            EvaluateQuestGatherProgressHelpers();
            EvaluateQuestRewardClaimedHelpers();
            EvaluateQuestAcceptedHelpers();
        }
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
    }

    private void HandleInventoryChangedForHelpers()
    {
        if (_inventoryForHelpers == null || definitions == null || definitions.Length == 0)
            return;

        if (!HelperProgressStore.IsHydratedFromSave)
            return;

        int total = CountTotalInventoryUnits(_inventoryForHelpers);
        int prev = _lastSeenInventoryTotalUnitsBeforeChange;

        bool becameNonEmptyFromEmpty = prev <= 0 && total > 0;
        _lastSeenInventoryTotalUnitsBeforeChange = total;

        if (_activeDefinition != null)
            return;

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
        if (_activeDefinition != null)
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
        if (_activeDefinition != null)
            return;

        EvaluateSkillLevelReachedHelpers(skillType, newLevel);
    }

    private void EvaluateFirstBagGainFromZeroHelpers()
    {
        if (_activeDefinition != null || definitions == null || definitions.Length == 0)
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

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);
    }

    private void EvaluateInventoryItemCountReachedHelpers()
    {
        if (_activeDefinition != null || definitions == null || definitions.Length == 0 ||
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

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);
    }

    private void EvaluateQuestGatherProgressHelpers()
    {
        if (_activeDefinition != null || definitions == null || definitions.Length == 0)
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

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);
    }

    private void EvaluateSkillLevelReachedHelpers(SkillType firedSkill, int newLevel)
    {
        if (_activeDefinition != null || definitions == null || definitions.Length == 0)
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

            if (d.skillLevelTriggerSkill != firedSkill)
                continue;

            int minLv = Mathf.Max(2, d.skillLevelTriggerMinimumNewLevel);
            if (newLevel < minLv)
                continue;

            if (!MapNodeMatchesOptional(d, activeMap))
                continue;

            candidates.Add(d);
        }

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);
    }

    private void EvaluateQuestRewardClaimedHelpers()
    {
        if (_activeDefinition != null || definitions == null || definitions.Length == 0)
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

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);
    }

    private void EvaluateQuestAcceptedHelpers()
    {
        if (_activeDefinition != null || definitions == null || definitions.Length == 0)
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

        candidates.Sort(static (a, b) =>
        {
            int c = a.priority.CompareTo(b.priority);
            return c != 0 ? c : string.CompareOrdinal(a.helperId, b.helperId);
        });

        if (candidates.Count > 0)
            ShowPopup(candidates[0]);
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
            _activeDefinition.whitelistedInteractionIds == null ||
            _activeDefinition.whitelistedInteractionIds.Length == 0)
            return;

        int overlayTopSort = canvasSortOrder + panelSortDelta;
        if (_panelFrontCanvas)
            overlayTopSort = Mathf.Max(overlayTopSort, _panelFrontCanvas.sortingOrder);

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

    private void ShowPopup(HelperPopupDefinition def)
    {
        if (def == null ||
            !ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups))
            return;

        _activeDefinition = def;

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
            TitlePlain = substitutedTitle.Trim(),
            BodyPlain = substitutedBody,
        });
        _historyViewIndex = _sessionMessageHistory.Count - 1;

        TransitionToExpandedPresentationLayout();
        RaiseWhitelistUiTargetCanvasesForActiveOverlay();

        if (_helperCloseButtonRoot)
            _helperCloseButtonRoot.SetActive(def.showCloseButton);

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);

        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: true);

        RefreshWhitelistPresentationEmphasis();

        RefreshWorldWhitelistRoutingFlag();
        ApplyDarkenModalPresentation();
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
        if (!_dimmerImage)
            return;

        bool wants =
            IsActiveHelperExpandedWithModalGameplayLock() &&
            _overlayRoot != null &&
            _overlayRoot.activeSelf;

        _dimmerImage.enabled = wants;
        _dimmerImage.color = wants ? new Color(0f, 0f, 0f, 0.62f) : new Color(0f, 0f, 0f, 0f);
        _dimmerImage.raycastTarget = wants;

        SyncMovementLockFromSettings();
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
        ApplyDisplayedHistoryIndexToPanel(_historyViewIndex, startTypewriterFresh: false);

        RefreshWhitelistPresentationEmphasis();
        ApplyDarkenModalPresentation();
        RefreshWorldWhitelistRoutingFlag();
    }

    private void HideOverlayCompletely(bool clearMovementLock, bool purgeMessageHistory = true)
    {
        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();
        if (_bodyText)
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = string.Empty;
        }

        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        _lastExpandedPanelSizeDelta = Vector2.zero;

        if (_expandedPanelRoot)
            _expandedPanelRoot.SetActive(true);

        RefreshChromeCollapsedVisuals(false);

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

        if (_dimmerImage)
        {
            _dimmerImage.enabled = false;
            _dimmerImage.raycastTarget = false;
        }

        _activeDefinition = null;

        if (clearMovementLock)
            ResolvePlayerMovementLock(false);
    }

    private void LateUpdate()
    {
        SyncAndPulseWhitelistGlow();
    }

    private void Update()
    {
        LerpWhitelistPresentationTints();
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
            DismissMarked();
        else
            HideOverlayCompletely(true);
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
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            DialogueTextTypewriter.RestoreFullReveal(_bodyText);
            _bodyText.text = plainFull;
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
    }

    private void TryDismiss(HelperDismissMode modeReason, string interactWhitelistIdMarker = null)
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
            string.Equals(
                interactWhitelistIdMarker.Trim(),
                HelperWhitelistUiInteractTarget.SkillsAbilityToolbarWhitelistId,
                System.StringComparison.OrdinalIgnoreCase))
            s_skillsAbilityToolbarWhitelistUiDismissStampFrame = Time.frameCount;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            interactWhitelistIdMarker != null &&
            string.Equals(
                interactWhitelistIdMarker.Trim(),
                HelperWhitelistUiInteractTarget.LevelSelectToolbarWhitelistId,
                System.StringComparison.OrdinalIgnoreCase))
            s_levelSelectToolbarWhitelistUiDismissStampFrame = Time.frameCount;

        if (modeReason == HelperDismissMode.InteractWhitelistDismiss &&
            ToggleSettingsStore.Get(ToggleSettingId.ShowHelpPopups) &&
            IsHelperExpandedPresentation())
        {
            CompleteInteractWhitelistDismissByMinimize();
            return;
        }

        DismissMarked();
    }

    private void CompleteInteractWhitelistDismissByMinimize()
    {
        if (_activeDefinition == null)
            return;

        HelperProgressStore.MarkDismissed(_activeDefinition.helperId);

        RestoreWhitelistUiTargetCanvases();
        StopBodyTypewriterAndClear();

        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        _activeDefinition = null;
        _activeUsesWorldWhitelistRouting = false;

        if (!_helperPanelRt || !_expandedPanelRoot || _overlayRoot == null)
        {
            HideOverlayCompletely(true);
            return;
        }

        TransitionToCollapsedStripeLayout();

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);
        ApplyDarkenModalPresentation();
    }

    private void DismissMarked()
    {
        if (_activeDefinition == null)
            return;

        HelperProgressStore.MarkDismissed(_activeDefinition.helperId);
        HideOverlayCompletely(true);
    }

    /// <summary>Does not persist — used when controller is destroyed mid-session.</summary>
    private void DismissSilent()
    {
        bool hasSurface = (_overlayRoot && _overlayRoot.activeSelf) || _activeDefinition != null;
        if (!hasSurface)
            return;

        HideOverlayCompletely(true);
    }

    private void RefreshWhitelistPresentationEmphasis()
    {
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        if (_activeDefinition == null ||
            !IsHelperExpandedPresentation() ||
            !ViewingLatestHistoryEntry() ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            _activeDefinition.whitelistedInteractionIds == null ||
            _activeDefinition.whitelistedInteractionIds.Length == 0)
            return;

        if (whitelistGlowAboveDimmer)
            RebuildWhitelistGlowOverlays();
        else
            ApplyWhitelistPresentationTintsInner();
    }

    private void ApplyWhitelistPresentationTintsInner()
    {
        if (_activeDefinition == null ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            _activeDefinition.whitelistedInteractionIds == null ||
            _activeDefinition.whitelistedInteractionIds.Length == 0)
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

    private void EnsureViewBuilt()
    {
        if (_overlayRoot != null)
            return;

        RectTransform parentRt = ResolveOverlayParent();
        if (!parentRt)
        {
            Debug.LogError("[HelperGameplayController] No UI parent — UICanvas tag, or assign Ui Parent Override and turn Prefer Full Screen off.");
            return;
        }

        _overlayRoot = new GameObject("HelperSystemOverlay");
        _overlayRect = _overlayRoot.AddComponent<RectTransform>();
        _overlayRect.SetParent(parentRt, false);
        _overlayRect.anchorMin = Vector2.zero;
        _overlayRect.anchorMax = Vector2.one;
        _overlayRect.offsetMin = Vector2.zero;
        _overlayRect.offsetMax = Vector2.zero;
        _overlayRect.localScale = Vector3.one;

        _overlayCanvas = _overlayRoot.AddComponent<Canvas>();
        _overlayCanvas.overrideSorting = true;
        _overlayCanvas.sortingOrder = canvasSortOrder;
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayRoot.AddComponent<GraphicRaycaster>();

        GameObject dimGo = CreateChild(_overlayRoot.transform, "Dimmer");
        RectTransform dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        Image dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.62f);
        dimImg.raycastTarget = true;
        _dimmerImage = dimImg;

        GameObject glowLayerGo = CreateChild(_overlayRoot.transform, "WhitelistGlowOverlay");
        RectTransform glowLayerRt = glowLayerGo.GetComponent<RectTransform>();
        glowLayerRt.anchorMin = Vector2.zero;
        glowLayerRt.anchorMax = Vector2.one;
        glowLayerRt.offsetMin = Vector2.zero;
        glowLayerRt.offsetMax = Vector2.zero;

        Canvas glowCanvas = glowLayerGo.AddComponent<Canvas>();
        glowCanvas.overrideSorting = true;
        glowCanvas.sortingOrder = canvasSortOrder + whitelistGlowSortDelta;

        GameObject glowHolderGo = CreateChild(glowLayerGo.transform, "WhitelistGlowHolder");
        _whitelistGlowHolder = glowHolderGo.GetComponent<RectTransform>();
        _whitelistGlowHolder.anchorMin = Vector2.zero;
        _whitelistGlowHolder.anchorMax = Vector2.one;
        _whitelistGlowHolder.offsetMin = Vector2.zero;
        _whitelistGlowHolder.offsetMax = Vector2.zero;

        GameObject panelLayerGo = CreateChild(_overlayRoot.transform, "WelcomePanelOverlay");
        RectTransform panelLayerRt = panelLayerGo.GetComponent<RectTransform>();
        panelLayerRt.anchorMin = Vector2.zero;
        panelLayerRt.anchorMax = Vector2.one;
        panelLayerRt.offsetMin = Vector2.zero;
        panelLayerRt.offsetMax = Vector2.zero;

        _panelFrontCanvas = panelLayerGo.AddComponent<Canvas>();
        _panelFrontCanvas.overrideSorting = true;
        _panelFrontCanvas.sortingOrder = canvasSortOrder + panelSortDelta;
        panelLayerGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = CreateChild(panelLayerGo.transform, "HelperPanel");
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
        chromeDrag.AttachWindow(_helperPanelRt, omitTopCornerHandles: true, counterHudCanvasScale: true);

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

        GameObject bodyGo =
            CreateChild(_expandedPanelRoot.transform, "BodyText");
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0f, 1f);
        float bodyTopInset = titleTopInset + titleBlockH + 6f;
        bodyRt.offsetMin = new Vector2(16f, ExpandedFooterNavHeight + 8f);
        bodyRt.offsetMax = new Vector2(-16f, -bodyTopInset);

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        _bodyText.fontSize = 16f;
        _bodyText.color = new Color(0.93f, 0.86f, 0.72f, 1f);
        _bodyText.textWrappingMode = TextWrappingModes.Normal;
        _bodyText.alignment = TextAlignmentOptions.TopJustified;
        _bodyText.raycastTarget = true;

        HelperTypewriterPanelSkip clickSkip =
            bodyGo.GetComponent<HelperTypewriterPanelSkip>() ?? bodyGo.AddComponent<HelperTypewriterPanelSkip>();
        clickSkip.Init(this);

        dimGo.transform.SetSiblingIndex(0);
        glowLayerGo.transform.SetSiblingIndex(1);
        panelLayerGo.transform.SetSiblingIndex(2);

        _overlayRoot.SetActive(false);
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
            _activeDefinition.whitelistedInteractionIds == null ||
            _activeDefinition.whitelistedInteractionIds.Length == 0)
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
                    GlowHaloImg = null,
                });
            }
        }

        AppendWhitelistUiGlowOverlaysIntoHolder();
    }

    /// <remarks>Whitelist UI rects use each Canvas's projected screen bounds; no StripCamera required.</remarks>
    private void AppendWhitelistUiGlowOverlaysIntoHolder()
    {
        if (!IsHelperExpandedPresentation() ||
            !ViewingLatestHistoryEntry() ||
            _activeDefinition == null ||
            _whitelistGlowHolder == null ||
            !_activeDefinition.highlightWhitelistTargetsDuringHelper ||
            _activeDefinition.whitelistedInteractionIds == null ||
            _activeDefinition.whitelistedInteractionIds.Length == 0)
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
        for (int i = 0; i < _whitelistGlowLinks.Count; i++)
        {
            Image halo = _whitelistGlowLinks[i].GlowHaloImg;
            if (halo)
                Destroy(halo.gameObject);

            Image g = _whitelistGlowLinks[i].GlowImg;
            if (g)
                Destroy(g.gameObject);
        }

        _whitelistGlowLinks.Clear();
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
                haloC.a *= whitelistUiGlowHaloAlphaScale;
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
        tmp.enableWordWrapping = false;
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

    private RectTransform ResolveOverlayParent()
    {
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
    }
}
