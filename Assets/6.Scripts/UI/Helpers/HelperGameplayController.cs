using System.Collections;
using System.Collections.Generic;
using System.Text;
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

    /// <summary>True while a helper popup is modal over the strip — blocks clicks, zoom hotkeys, and action bar polling.</summary>
    public static bool BlocksStripGameplay => Instance != null && Instance._blockActive;

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

    [SerializeField] private Vector2 panelSize = new(560f, 280f);

    [SerializeField] private Vector2 panelAnchoredPosition = new(40f, -88f);

    [Header("Body typewriter")]
    [Tooltip("Delay between each revealed word on the helper body (same pacing model as NPCDialogueBoxUI). Title shows immediately.")]
    [SerializeField] private float typewriterSecondsPerWord = 1f;

    private Coroutine _bodyTypewriterCo;
    private string _bodyTypewriterFullPlain;

    private bool _blockActive;
    private HelperPopupDefinition _activeDefinition;
    private PlayerController _player;

    private GameObject _overlayRoot;
    private RectTransform _overlayRect;
    private Canvas _overlayCanvas;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;

    private Canvas _panelFrontCanvas;

    /// <summary>Glow overlays above dimmer.</summary>
    private RectTransform _whitelistGlowHolder;

    private readonly List<WhitelistGlowLink> _whitelistGlowLinks = new();

    private struct WhitelistGlowLink
    {
        public SpriteRenderer Source;
        public Image GlowImg;
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

    /// <summary>True while blocking and the active helper lists at least one whitelist world interaction id.</summary>
    public static bool ActiveHelperUsesWorldWhitelist =>
        Instance != null &&
        Instance._blockActive &&
        Instance._activeDefinition != null &&
        Instance._activeDefinition.whitelistedInteractionIds != null &&
        Instance._activeDefinition.whitelistedInteractionIds.Length > 0;

    /// <summary>True when <paramref name="winnerCol"/> hits a subtree that carries a whitelist id listed on the active helper.</summary>
    public static bool IsWhitelistedWorldPick(Collider2D winnerCol)
    {
        if (!winnerCol || Instance == null || !Instance._blockActive || Instance._activeDefinition == null)
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

        for (int i = 0; i < definitions.Length; i++)
        {
            HelperPopupDefinition d = definitions[i];
            if (!d || string.IsNullOrWhiteSpace(d.helperId))
                continue;

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
        }
    }
#else
    private static void LogMisconfiguredDefinitions() { }
#endif

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DismissSilent();
            Instance = null;
        }

        if (GameplayLevelBootstrapper.Instance != null)
            GameplayLevelBootstrapper.Instance.OnLevelStarted -= HandleLevelStarted;
    }

    private IEnumerator Start()
    {
        yield return HelperProgressStore.WaitUntilHydratedFromSave();

        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        GameplayLevelBootstrapper boots = GameplayLevelBootstrapper.Instance;
        if (boots == null)
        {
            Debug.LogWarning("[HelperGameplayController] No GameplayLevelBootstrapper — map-entry helpers will never run.", this);
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

    /// <remarks>Call from MainMenu when the Character (inventory/equipment) tab opens.</remarks>
    public static void NotifyCharacterMenuOpened()
    {
        Instance?.TryDismiss(HelperDismissMode.CharacterPageOpened);
    }

    private void HandleLevelStarted(MapNodeDefinition node)
    {
        if (_blockActive || node == null || definitions == null || definitions.Length == 0)
            return;

        StartCoroutine(EvaluateMapEntryNextFrame(node));
    }

    private IEnumerator EvaluateMapEntryNextFrame(MapNodeDefinition node)
    {
        yield return null;

        if (_blockActive)
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
    }

    private void ShowPopup(HelperPopupDefinition def)
    {
        _activeDefinition = def;
        _blockActive = true;

        ResolvePlayerMovementLock(true);
        EnsureViewBuilt();
        if (_overlayRoot == null)
        {
            _blockActive = false;
            _activeDefinition = null;
            ResolvePlayerMovementLock(false);
            return;
        }

        bool hasTitle = def.title != null && def.title.Trim().Length > 0;
        _titleText.gameObject.SetActive(hasTitle);
        if (hasTitle)
            _titleText.text = def.title.Trim();

        _overlayRoot.transform.SetAsLastSibling();
        _overlayRoot.SetActive(true);

        StartBodyTypewriter(def.bodyText ?? string.Empty);

        RefreshWhitelistPresentationEmphasis();
    }

    private void LateUpdate()
    {
        SyncAndPulseWhitelistGlow();
    }

    private void Update()
    {
        LerpWhitelistPresentationTints();
    }

    private void ResolvePlayerMovementLock(bool locked)
    {
        _player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (_player != null)
            _player.SetMovementLocked(locked);
    }

    public void CloseFromUIButton()
    {
        TryDismiss(HelperDismissMode.CloseButton);
    }

    /// <summary>Pointer-down on the helper panel reveals the full body text immediately (matches NPC dialogue box).</summary>
    public void CompleteBodyTypewriter()
    {
        if (_bodyTypewriterCo == null)
            return;

        StopCoroutine(_bodyTypewriterCo);
        _bodyTypewriterCo = null;
        if (_bodyText && _bodyTypewriterFullPlain != null)
            _bodyText.text = _bodyTypewriterFullPlain;
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
            _bodyText.text = "";
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            _bodyText.text = plainFull;
            return;
        }

        _bodyTypewriterFullPlain = plainFull;
        _bodyText.text = "";
        _bodyTypewriterCo = StartCoroutine(RunBodyTypewriterCoroutine());
    }

    private IEnumerator RunBodyTypewriterCoroutine()
    {
        string full = _bodyTypewriterFullPlain ?? "";
        List<(string word, string trailingWs)> tokens = TokenizeWordsWithWhitespace(full);
        var sb = new StringBuilder();

        for (int i = 0; i < tokens.Count; i++)
        {
            sb.Append(tokens[i].word);
            sb.Append(tokens[i].trailingWs);
            _bodyText.text = sb.ToString();
            if (i < tokens.Count - 1)
                yield return new WaitForSeconds(typewriterSecondsPerWord);
        }

        _bodyTypewriterCo = null;
        _bodyTypewriterFullPlain = null;
    }

    private static List<(string word, string trailingWs)> TokenizeWordsWithWhitespace(string s)
    {
        var list = new List<(string, string)>();
        if (string.IsNullOrEmpty(s))
            return list;

        int i = 0;
        int len = s.Length;
        while (i < len)
        {
            while (i < len && char.IsWhiteSpace(s[i]))
                i++;
            if (i >= len)
                break;

            int w0 = i;
            while (i < len && !char.IsWhiteSpace(s[i]))
                i++;
            string word = s.Substring(w0, i - w0);

            int ws0 = i;
            while (i < len && char.IsWhiteSpace(s[i]))
                i++;
            string ws = s.Substring(ws0, i - ws0);
            list.Add((word, ws));
        }

        return list;
    }

    private void TryDismiss(HelperDismissMode modeReason)
    {
        if (!_blockActive || _activeDefinition == null)
            return;

        if ((_activeDefinition.dismissModes & modeReason) == 0)
            return;

        DismissMarked();
    }

    private void DismissMarked()
    {
        if (!_blockActive || _activeDefinition == null)
            return;

        string id = _activeDefinition.helperId;
        HelperProgressStore.MarkDismissed(id);

        HideUi();
        _activeDefinition = null;
        _blockActive = false;
        ResolvePlayerMovementLock(false);
    }

    /// <summary>Does not persist — used when controller is destroyed mid-session.</summary>
    private void DismissSilent()
    {
        if (!_blockActive)
            return;

        HideUi();
        _activeDefinition = null;
        _blockActive = false;
        ResolvePlayerMovementLock(false);
    }

    private void HideUi()
    {
        StopBodyTypewriterAndClear();
        if (_bodyText)
            _bodyText.text = string.Empty;

        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        if (_overlayRoot)
            _overlayRoot.SetActive(false);
    }

    private void RefreshWhitelistPresentationEmphasis()
    {
        ClearWhitelistGlowOverlays();
        ClearWhitelistPresentationTints();

        if (_activeDefinition == null ||
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
        if (!_blockActive ||
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
        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = panelAnchoredPosition;
        panelRt.sizeDelta = panelSize;

        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        panelBg.raycastTarget = true;

        GameObject titleGo = CreateChild(panelGo.transform, "TitleText");
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.sizeDelta = new Vector2(panelSize.x - 48f, 36f);
        titleRt.anchoredPosition = new Vector2(16f, -12f);

        _titleText = titleGo.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize = 21f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.color = new Color(0.88f, 0.91f, 0.96f, 1f);
        _titleText.alignment = TextAlignmentOptions.TopLeft;
        _titleText.textWrappingMode = TextWrappingModes.Normal;
        _titleText.raycastTarget = false;

        GameObject bodyGo = CreateChild(panelGo.transform, "BodyText");
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0f, 1f);
        bodyRt.offsetMin = new Vector2(16f, 20f);
        bodyRt.offsetMax = new Vector2(-16f, -52f);

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        _bodyText.fontSize = 18f;
        _bodyText.color = new Color(0.93f, 0.86f, 0.72f, 1f);
        _bodyText.textWrappingMode = TextWrappingModes.Normal;
        _bodyText.alignment = TextAlignmentOptions.TopJustified;
        _bodyText.raycastTarget = false;

        CreateCloseButton(panelGo.transform);

        HelperTypewriterPanelSkip clickSkip = panelGo.GetComponent<HelperTypewriterPanelSkip>();
        if (!clickSkip)
            clickSkip = panelGo.AddComponent<HelperTypewriterPanelSkip>();
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
            !_blockActive ||
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
        if (!cam)
            return;

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

            _whitelistGlowLinks.Add(new WhitelistGlowLink { Source = sr, GlowImg = img });
        }
    }

    private void ClearWhitelistGlowOverlays()
    {
        for (int i = 0; i < _whitelistGlowLinks.Count; i++)
        {
            Image g = _whitelistGlowLinks[i].GlowImg;
            if (g)
                Destroy(g.gameObject);
        }

        _whitelistGlowLinks.Clear();
    }

    private void SyncAndPulseWhitelistGlow()
    {
        if (!_blockActive ||
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

        Camera cam = worldCameraForWhitelistGlow;
        if (!cam)
            return;

        float pulseT = (Mathf.Sin(Time.time * whitelistGlowPulseSpeed) + 1f) * 0.5f;
        Color glowCol = PulsedWhitelistGlowColor(pulseT);

        for (int i = _whitelistGlowLinks.Count - 1; i >= 0; i--)
        {
            WhitelistGlowLink link = _whitelistGlowLinks[i];
            if (!link.Source || link.Source.sprite == null || !link.GlowImg)
            {
                if (link.GlowImg)
                    Destroy(link.GlowImg.gameObject);
                _whitelistGlowLinks.RemoveAt(i);
                continue;
            }

            FitSpriteRendererOverlayRect(link.Source, link.GlowImg.rectTransform, _whitelistGlowHolder, cam);
            link.GlowImg.color = glowCol;
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

    private void CreateCloseButton(Transform panel)
    {
        GameObject btGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btGo.transform.SetParent(panel, false);
        RectTransform btRt = btGo.GetComponent<RectTransform>();
        btRt.anchorMin = new Vector2(1f, 1f);
        btRt.anchorMax = new Vector2(1f, 1f);
        btRt.pivot = new Vector2(1f, 1f);
        btRt.anchoredPosition = new Vector2(-14f, -12f);
        btRt.sizeDelta = new Vector2(30f, 30f);

        Image img = btGo.GetComponent<Image>();
        img.color = new Color(0.35f, 0.08f, 0.08f, 1f);
        img.raycastTarget = true;

        GameObject lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(btGo.transform, false);
        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = "X";
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        RectTransform lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        Button b = btGo.GetComponent<Button>();
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(CloseFromUIButton);
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
