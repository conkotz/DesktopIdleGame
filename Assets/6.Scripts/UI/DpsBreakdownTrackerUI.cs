using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DpsBreakdownTrackerUI : MonoBehaviour
{
    private enum MetricMode
    {
        Dps,
        TotalDamage
    }

    private static readonly string[] TrackerNameCandidates =
    {
        "DPS",
        "Dps",
        "DamageBreakdown",
        "Damage Breakdown",
        "DamageTracker",
        "Damage Tracker"
    };

    [Header("Refs")]
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button pausePlayButton;
    [SerializeField] private Image pausePlayButtonIcon;
    [SerializeField] private Button dpsOrDamageButton;
    [SerializeField] private TMP_Text dpsOrDamageButtonText;
    [SerializeField] private Sprite dpsTrackerPauseSprite;
    [SerializeField] private Sprite dpsTrackerPlaySprite;

    [Header("Outgoing")]
    [SerializeField] private TMP_Text outgoingTotalText;
    [SerializeField] private TMP_Text outgoingPhysicalText;
    [SerializeField] private TMP_Text outgoingMagicText;
    [SerializeField] private TMP_Text outgoingCorruptionText;
    [SerializeField] private TMP_Text outgoingMinionText;
    [FormerlySerializedAs("outgoingBleedText")]
    [SerializeField] private TMP_Text outgoingAilmentsText;

    [Header("Incoming")]
    [SerializeField] private TMP_Text incomingTotalText;
    [SerializeField] private TMP_Text incomingPhysicalText;
    [SerializeField] private TMP_Text incomingMagicText;
    [SerializeField] private TMP_Text incomingCorruptionText;
    [SerializeField] private TMP_Text incomingBleedText;
    [SerializeField] private TMP_Text incomingPoisonText;
    [SerializeField] private TMP_Text incomingBurnText;
    [SerializeField] private TMP_Text outgoingHeaderText;
    [SerializeField] private TMP_Text incomingHeaderText;
    [SerializeField] private TMP_Text elapsedTimeText;
    [SerializeField] private TMP_Text individualDamageDealersText;
    [SerializeField] private TMP_Text damageMitigatedSourcesText;
    [SerializeField] private TMP_Text individualOutgoingDamageSourcesText;
    [SerializeField] private TMP_Text individualIncomingHealingSourcesText;

    [Header("Refresh")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;
    [Header("Debug")]
    [SerializeField] private bool debugScrollDiagnostics = true;

    private float _nextRefreshTime;
    private MetricMode _mode = MetricMode.Dps;
    private bool _displayPaused;
    private Coroutine _lateWireRoutine;
    private bool _loggedScrollDiagnostics;
    private float _incomingPanelBasePreferredHeight = -1f;
    private float _dealerTextBaseHeight = -1f;
    private float _mitigationPanelBasePreferredHeight = -1f;
    private float _mitigationSourceTextBaseHeight = -1f;
    private float _outgoingPanelBasePreferredHeight = -1f;
    private float _outgoingSourceTextBaseHeight = -1f;
    private float _healingPanelBasePreferredHeight = -1f;
    private float _healingSourceTextBaseHeight = -1f;
    private float _scrollContentBaseHeight = -1f;
    private Transform _resolvedTrackerRoot;
    private static readonly Dictionary<int, DpsBreakdownTrackerUI> RootOwnerById = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoAttach()
    {
        SceneManager.sceneLoaded += (_, _) => AutoAttachToTrackerWindows();
        AutoAttachToTrackerWindows();
    }

    private static void AutoAttachToTrackerWindows()
    {
        // If a tracker already exists in the scene (manually placed), do not auto-attach extras.
        DpsBreakdownTrackerUI[] existing =
            FindObjectsByType<DpsBreakdownTrackerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing != null && existing.Length > 0)
            return;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (!IsSceneObject(candidate))
                continue;

            if (!LooksLikeTrackerRoot(candidate) && !ContainsTrackerTitle(candidate))
                continue;

            Transform root = ResolveTrackerRoot(candidate);
            if (!root || root.GetComponent<DpsBreakdownTrackerUI>())
                continue;

            root.gameObject.AddComponent<DpsBreakdownTrackerUI>();
        }
    }

    private static bool IsSceneObject(Transform transform)
    {
        return transform != null &&
               transform.hideFlags == HideFlags.None &&
               transform.gameObject.scene.IsValid();
    }

    private static bool LooksLikeTrackerRoot(Transform transform)
    {
        if (transform == null)
            return false;

        string objectName = transform.name;
        bool hasTrackerName = false;
        for (int i = 0; i < TrackerNameCandidates.Length; i++)
        {
            if (objectName.IndexOf(TrackerNameCandidates[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hasTrackerName = true;
                break;
            }
        }

        return hasTrackerName && HasOutgoingAndIncomingText(transform);
    }

    private static bool ContainsTrackerTitle(Transform transform)
    {
        if (!transform)
            return false;

        TMP_Text text = transform.GetComponent<TMP_Text>();
        if (!text)
            return false;

        string body = text.text;
        return !string.IsNullOrEmpty(body) &&
               body.IndexOf("Full Damage Breakdown", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Transform ResolveTrackerRoot(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (HasOutgoingAndIncomingText(current))
                return current;

            current = current.parent;
        }

        return candidate;
    }

    private static bool HasOutgoingAndIncomingText(Transform root)
    {
        if (!root)
            return false;

        bool hasOutgoing = false;
        bool hasIncoming = false;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!text)
                continue;

            string combined = text.name + " " + text.text;
            if (combined.IndexOf("Outgoing", System.StringComparison.OrdinalIgnoreCase) >= 0)
                hasOutgoing = true;
            if (combined.IndexOf("Incoming", System.StringComparison.OrdinalIgnoreCase) >= 0)
                hasIncoming = true;
        }

        return hasOutgoing && hasIncoming;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        _resolvedTrackerRoot = ResolveTrackerRoot(transform);
        int rootId = _resolvedTrackerRoot ? _resolvedTrackerRoot.GetInstanceID() : transform.GetInstanceID();
        if (RootOwnerById.TryGetValue(rootId, out DpsBreakdownTrackerUI owner) && owner != null && owner != this)
        {
            enabled = false;
            return;
        }
        RootOwnerById[rootId] = this;

        ResolveReferences();
        EnsureScrollViewMasking();
        WireButtons();
        if (_lateWireRoutine != null)
            StopCoroutine(_lateWireRoutine);
        _lateWireRoutine = StartCoroutine(CoWireButtonsAfterInitializers());
        ApplyModeToTracker();
        ApplyPlayPauseButtonVisual();
        Refresh();
    }

    private void OnDisable()
    {
        int rootId = _resolvedTrackerRoot ? _resolvedTrackerRoot.GetInstanceID() : transform.GetInstanceID();
        if (RootOwnerById.TryGetValue(rootId, out DpsBreakdownTrackerUI owner) && owner == this)
            RootOwnerById.Remove(rootId);

        if (_lateWireRoutine != null)
        {
            StopCoroutine(_lateWireRoutine);
            _lateWireRoutine = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + refreshInterval;
        ApplyPlayPauseButtonVisual();

        if (_displayPaused)
            return;

        MaybeLogScrollDiagnostics();
        Refresh();
    }

    private void Refresh()
    {
        if (!combat)
            ResolveReferences();

        RefreshElapsedTime();
        RefreshIncomingDealerDamage(combat);
        RefreshIncomingMitigationSources(combat);
        RefreshOutgoingDamageSources(combat);
        RefreshIncomingHealingSources(combat);

        if (_mode == MetricMode.TotalDamage)
            RefreshTotalDamage(combat);
        else
            RefreshDps(combat);
    }

    private void RefreshElapsedTime()
    {
        if (!elapsedTimeText)
            return;

        float elapsed = combat ? combat.GetDamageSessionElapsedSeconds() : 0f;
        int totalSeconds = Mathf.FloorToInt(elapsed);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        elapsedTimeText.text = hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    private void RefreshIncomingDealerDamage(PlayerCombatController currentCombat)
    {
        if (!individualDamageDealersText)
            return;

        bool dpsMode = _mode == MetricMode.Dps;

        if (!currentCombat)
        {
            individualDamageDealersText.text = dpsMode ? "No incoming DPS yet" : "No incoming damage yet";
            RefreshDealerPanelLayout();
            return;
        }

        var entries = currentCombat.GetIncomingDamageByDealer();
        if (entries == null || entries.Count == 0)
        {
            individualDamageDealersText.text = dpsMode ? "No incoming DPS yet" : "No incoming damage yet";
            RefreshDealerPanelLayout();
            return;
        }

        float elapsed = Mathf.Max(0f, currentCombat.GetDamageSessionElapsedSeconds());
        StringBuilder sb = new StringBuilder(128);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (i > 0)
                sb.AppendLine();
            sb.Append(e.dealerName);
            sb.Append(": ");
            if (dpsMode)
            {
                float perSecond = elapsed > 0.001f ? Mathf.Max(0f, e.totalDamage) / elapsed : 0f;
                sb.Append(perSecond.ToString("0.#"));
                sb.Append(" DPS");
            }
            else
            {
                sb.Append(Mathf.RoundToInt(Mathf.Max(0f, e.totalDamage)));
            }
        }

        individualDamageDealersText.text = sb.ToString();
        RefreshDealerPanelLayout();
    }

    private void RefreshIncomingMitigationSources(PlayerCombatController currentCombat)
    {
        if (!damageMitigatedSourcesText)
            return;

        bool dpsMode = _mode == MetricMode.Dps;
        string emptyMessage = dpsMode ? "No damage mitigated yet" : "No damage mitigated yet";

        if (!currentCombat)
        {
            damageMitigatedSourcesText.text = emptyMessage;
            RefreshMitigationPanelLayout();
            return;
        }

        DpsMitigationBreakdown totals = currentCombat.GetIncomingMitigationBreakdown();
        if (totals.Total <= 0f)
        {
            damageMitigatedSourcesText.text = emptyMessage;
            RefreshMitigationPanelLayout();
            return;
        }

        float elapsed = Mathf.Max(0f, currentCombat.GetDamageSessionElapsedSeconds());
        StringBuilder sb = new StringBuilder(128);
        AppendMitigationSourceLine(sb, "Armour", totals.Armour, elapsed, dpsMode);
        AppendMitigationSourceLine(sb, "Magic Resist", totals.MagicResist, elapsed, dpsMode);
        AppendMitigationSourceLine(sb, "Corruption Resist", totals.CorruptionResist, elapsed, dpsMode);
        AppendMitigationSourceLine(sb, "Blocked", totals.Blocked, elapsed, dpsMode);
        AppendMitigationSourceLine(sb, "Parry", totals.Parry, elapsed, dpsMode);

        damageMitigatedSourcesText.text = sb.Length > 0 ? sb.ToString() : emptyMessage;
        RefreshMitigationPanelLayout();
    }

    private static void AppendMitigationSourceLine(
        StringBuilder sb,
        string label,
        float amount,
        float elapsed,
        bool dpsMode)
    {
        if (amount <= 0f)
            return;

        if (sb.Length > 0)
            sb.AppendLine();

        sb.Append(label);
        sb.Append(": ");
        if (dpsMode)
        {
            float perSecond = elapsed > 0.001f ? amount / elapsed : 0f;
            sb.Append(perSecond.ToString("0.#"));
            sb.Append(" DPS");
        }
        else
        {
            sb.Append(Mathf.RoundToInt(amount));
        }
    }

    private void RefreshOutgoingDamageSources(PlayerCombatController currentCombat)
    {
        if (!individualOutgoingDamageSourcesText)
            return;

        bool dpsMode = _mode == MetricMode.Dps;

        if (!currentCombat)
        {
            individualOutgoingDamageSourcesText.text = dpsMode ? "No outgoing DPS yet" : "No outgoing damage yet";
            RefreshOutgoingPanelLayout();
            return;
        }

        List<PlayerCombatController.OutgoingDamageSourceEntry> entries = currentCombat.GetOutgoingDamageBySource();
        if (entries == null || entries.Count == 0)
        {
            individualOutgoingDamageSourcesText.text = dpsMode ? "No outgoing DPS yet" : "No outgoing damage yet";
            RefreshOutgoingPanelLayout();
            return;
        }

        float elapsed = Mathf.Max(0f, currentCombat.GetDamageSessionElapsedSeconds());
        StringBuilder sb = new StringBuilder(128);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (i > 0)
                sb.AppendLine();
            sb.Append(i + 1);
            sb.Append(". ");
            sb.Append(e.sourceName);
            sb.Append(": ");
            if (dpsMode)
            {
                float perSecond = elapsed > 0.001f ? Mathf.Max(0f, e.totalDamage) / elapsed : 0f;
                sb.Append(perSecond.ToString("0.#"));
                sb.Append(" DPS");
            }
            else
            {
                sb.Append(Mathf.RoundToInt(Mathf.Max(0f, e.totalDamage)));
            }

            sb.Append(FormatOutgoingSourceHitUseSuffix(e));
        }

        individualOutgoingDamageSourcesText.text = sb.ToString();
        RefreshOutgoingPanelLayout();
    }

    private void RefreshIncomingHealingSources(PlayerCombatController currentCombat)
    {
        if (!individualIncomingHealingSourcesText)
            return;

        bool dpsMode = _mode == MetricMode.Dps;

        if (!currentCombat)
        {
            individualIncomingHealingSourcesText.text = dpsMode ? "No incoming healing yet" : "No incoming healing yet";
            RefreshHealingPanelLayout();
            return;
        }

        List<PlayerCombatController.IncomingHealingSourceEntry> entries = currentCombat.GetIncomingHealingBySource();
        if (entries == null || entries.Count == 0)
        {
            individualIncomingHealingSourcesText.text = dpsMode ? "No incoming healing yet" : "No incoming healing yet";
            RefreshHealingPanelLayout();
            return;
        }

        float elapsed = Mathf.Max(0f, currentCombat.GetDamageSessionElapsedSeconds());
        StringBuilder sb = new StringBuilder(128);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (i > 0)
                sb.AppendLine();
            sb.Append(e.sourceName);
            sb.Append(": ");
            if (dpsMode)
            {
                float perSecond = elapsed > 0.001f ? Mathf.Max(0f, e.totalHealing) / elapsed : 0f;
                sb.Append(perSecond.ToString("0.#"));
                sb.Append(" HPS");
            }
            else
            {
                sb.Append(Mathf.RoundToInt(Mathf.Max(0f, e.totalHealing)));
            }
        }

        individualIncomingHealingSourcesText.text = sb.ToString();
        RefreshHealingPanelLayout();
    }

    private static string FormatOutgoingSourceHitUseSuffix(PlayerCombatController.OutgoingDamageSourceEntry entry)
    {
        if (entry.hitCount <= 0 && entry.useCount <= 0)
            return "";

        if (entry.tracksUses)
        {
            return entry.hitCount > 0
                ? $" ({entry.hitCount} hits, {entry.useCount} uses)"
                : $" ({entry.useCount} uses)";
        }

        return entry.hitCount > 0 ? $" ({entry.hitCount} hits)" : "";
    }

    private void EnsureScrollViewMasking()
    {
        TMP_Text layoutAnchorText = individualDamageDealersText != null
            ? individualDamageDealersText
            : individualOutgoingDamageSourcesText;
        if (!layoutAnchorText)
            return;

        ScrollRect sr = layoutAnchorText.GetComponentInParent<ScrollRect>(true);
        if (!sr)
            return;

        // Ensure the DPS details scroller behaves consistently even if scene wiring drifts.
        sr.vertical = true;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = sr.viewport;
        if (viewport)
        {
            RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
            if (!rectMask)
                viewport.gameObject.AddComponent<RectMask2D>();

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
        }

        RectTransform content = sr.content;
        if (!content)
            return;

        if (_scrollContentBaseHeight < 0f)
            _scrollContentBaseHeight = Mathf.Max(1f, content.rect.height);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0f, 1f);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        HorizontalLayoutGroup hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (hlg)
        {
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;
        }

        MaskableGraphic[] graphics = content.GetComponentsInChildren<MaskableGraphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            MaskableGraphic g = graphics[i];
            if (!g)
                continue;
            g.maskable = true;
        }

        CanvasGroup[] groups = content.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            if (!groups[i])
                continue;
            if (!groups[i].blocksRaycasts)
                continue;
            // Text-only panels inside this scroll should not trap pointer wheel/drag from ScrollRect.
            if (!groups[i].interactable)
                groups[i].blocksRaycasts = false;
        }

        if (individualDamageDealersText)
            individualDamageDealersText.raycastTarget = false;
        if (damageMitigatedSourcesText)
            damageMitigatedSourcesText.raycastTarget = false;
        if (individualOutgoingDamageSourcesText)
            individualOutgoingDamageSourcesText.raycastTarget = false;
        if (individualIncomingHealingSourcesText)
            individualIncomingHealingSourcesText.raycastTarget = false;

        RefreshDealerPanelLayout();
        RefreshMitigationPanelLayout();
        RefreshOutgoingPanelLayout();
        RefreshHealingPanelLayout();

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (viewport)
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
    }

    private void RefreshDealerPanelLayout()
    {
        RefreshDetailTextPanelLayout(
            individualDamageDealersText,
            ref _incomingPanelBasePreferredHeight,
            ref _dealerTextBaseHeight);
    }

    private void RefreshMitigationPanelLayout()
    {
        RefreshDetailTextPanelLayout(
            damageMitigatedSourcesText,
            ref _mitigationPanelBasePreferredHeight,
            ref _mitigationSourceTextBaseHeight);
    }

    private void RefreshOutgoingPanelLayout()
    {
        RefreshDetailTextPanelLayout(
            individualOutgoingDamageSourcesText,
            ref _outgoingPanelBasePreferredHeight,
            ref _outgoingSourceTextBaseHeight);
    }

    private void RefreshHealingPanelLayout()
    {
        RefreshDetailTextPanelLayout(
            individualIncomingHealingSourcesText,
            ref _healingPanelBasePreferredHeight,
            ref _healingSourceTextBaseHeight);
    }

    private void RefreshDetailTextPanelLayout(
        TMP_Text detailText,
        ref float panelBasePreferredHeight,
        ref float textBaseHeight)
    {
        if (!detailText)
            return;

        RectTransform panelRt = detailText.transform.parent as RectTransform;
        if (!panelRt)
            return;

        // Scene masks on these panels clip multi-line detail text.
        RectMask2D localMask = panelRt.GetComponent<RectMask2D>();
        if (localMask)
            localMask.enabled = false;

        RectTransform textRt = detailText.rectTransform;
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0f, 1f);
        textRt.anchoredPosition = new Vector2(textRt.anchoredPosition.x, 0f);

        ContentSizeFitter textFitter = textRt.GetComponent<ContentSizeFitter>();
        if (textFitter)
        {
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        LayoutElement panelLe = panelRt.GetComponent<LayoutElement>();
        if (panelLe && panelBasePreferredHeight < 0f)
            panelBasePreferredHeight = Mathf.Max(1f, panelLe.preferredHeight);
        if (panelBasePreferredHeight < 0f)
            panelBasePreferredHeight = Mathf.Max(1f, panelRt.rect.height);

        if (textBaseHeight < 0f)
            textBaseHeight = Mathf.Max(24f, textRt.rect.height);

        float availableWidth = Mathf.Max(120f, textRt.rect.width);
        detailText.ForceMeshUpdate();
        float detailPreferred = Mathf.Max(
            textBaseHeight,
            detailText.GetPreferredValues(detailText.text, availableWidth, 0f).y);
        float extraDetailHeight = Mathf.Max(0f, detailPreferred - textBaseHeight);
        float wantedPanelHeight = panelBasePreferredHeight + extraDetailHeight;

        if (panelLe)
            panelLe.preferredHeight = wantedPanelHeight;

        LayoutElement textLe = textRt.GetComponent<LayoutElement>();
        if (!textLe)
            textLe = textRt.gameObject.AddComponent<LayoutElement>();
        textLe.minHeight = textBaseHeight;
        textLe.preferredHeight = detailPreferred;

        textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, detailPreferred);

        LayoutRebuilder.ForceRebuildLayoutImmediate(textRt);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
        if (panelRt.parent is RectTransform parentRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);

        ScrollRect scrollRect = detailText.GetComponentInParent<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content is RectTransform contentRt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            if (scrollRect.viewport is RectTransform viewportRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRt);
        }
    }

    private void MaybeLogScrollDiagnostics()
    {
        if (!debugScrollDiagnostics || _loggedScrollDiagnostics)
            return;
        if (!isActiveAndEnabled)
            return;

        ScrollRect sr = individualDamageDealersText
            ? individualDamageDealersText.GetComponentInParent<ScrollRect>(true)
            : GetComponentInChildren<ScrollRect>(true);

        if (!sr)
        {
            Debug.Log("[FullDpsScrollDiag] No ScrollRect found.");
            _loggedScrollDiagnostics = true;
            return;
        }

        RectTransform viewport = sr.viewport;
        RectTransform content = sr.content;

        string viewportRect = viewport ? viewport.rect.ToString() : "null";
        string contentRect = content ? content.rect.ToString() : "null";
        string viewportAnchors = viewport ? $"min={viewport.anchorMin} max={viewport.anchorMax} pivot={viewport.pivot}" : "null";
        string contentAnchors = content ? $"min={content.anchorMin} max={content.anchorMax} pivot={content.pivot}" : "null";
        string vpSize = viewport ? $"w={viewport.rect.width:0.##},h={viewport.rect.height:0.##}" : "null";
        string contentSize = content ? $"w={content.rect.width:0.##},h={content.rect.height:0.##}" : "null";

        GameObject pointerOver = null;
        if (EventSystem.current != null && Input.mousePresent)
            pointerOver = EventSystem.current.currentSelectedGameObject;

        Debug.Log(
            $"[FullDpsScrollDiag] sr='{sr.name}' vertical={sr.vertical} horizontal={sr.horizontal} " +
            $"norm={sr.verticalNormalizedPosition:0.###} velocity={sr.velocity} " +
            $"viewport='{(viewport ? viewport.name : "null")}' {vpSize} anchors[{viewportAnchors}] rect={viewportRect} " +
            $"content='{(content ? content.name : "null")}' {contentSize} anchors[{contentAnchors}] rect={contentRect} " +
            $"children={(content ? content.childCount : -1)} pointerSelected='{(pointerOver ? pointerOver.name : "null")}'",
            this
        );

        _loggedScrollDiagnostics = true;
    }

    private void RefreshDps(PlayerCombatController currentCombat)
    {
        DpsDamageBreakdown outgoing = currentCombat ? currentCombat.GetOutgoingDpsBreakdown() : default;
        float outgoingTotal = outgoing.Total;
        float incomingTotal = currentCombat ? currentCombat.GetCurrentIncomingDps() : 0f;
        DpsDamageBreakdown incoming = currentCombat ? currentCombat.GetIncomingDpsBreakdown() : default;

        SetLine(outgoingTotalText, "TotalDPS", outgoingTotal, isDps: true);
        SetLine(outgoingPhysicalText, "Physical", outgoing.Physical, isDps: true);
        SetLine(outgoingMagicText, "Magic", outgoing.Magic, isDps: true);
        SetLine(outgoingCorruptionText, "Corruption", outgoing.Corruption, isDps: true);
        SetLine(outgoingMinionText, "Minion", outgoing.Minion, isDps: true);
        SetLine(outgoingAilmentsText, "Ailments", GetCombinedAilmentDamage(outgoing), isDps: true);

        SetLine(incomingTotalText, "TotalDPS", incomingTotal, isDps: true);
        SetLine(incomingPhysicalText, "Physical", incoming.Physical, isDps: true);
        SetLine(incomingMagicText, "Magic", incoming.Magic, isDps: true);
        SetLine(incomingCorruptionText, "Corruption", incoming.Corruption, isDps: true);
        SetLine(incomingBleedText, "Bleed", incoming.Bleed, isDps: true);
        SetLine(incomingPoisonText, "Poison", incoming.Poison, isDps: true);
        SetLine(incomingBurnText, "Burn", incoming.Burn, isDps: true);
    }

    private void RefreshTotalDamage(PlayerCombatController currentCombat)
    {
        DpsDamageBreakdown outgoing = currentCombat ? currentCombat.GetOutgoingTotalDamageBreakdown() : default;
        float outgoingTotal = outgoing.Total;
        float incomingTotal = currentCombat ? currentCombat.GetIncomingTotalDamage() : 0f;
        DpsDamageBreakdown incoming = currentCombat ? currentCombat.GetIncomingTotalDamageBreakdown() : default;

        SetLine(outgoingTotalText, "Total Damage", outgoingTotal, isDps: false);
        SetLine(outgoingPhysicalText, "Physical", outgoing.Physical, isDps: false);
        SetLine(outgoingMagicText, "Magic", outgoing.Magic, isDps: false);
        SetLine(outgoingCorruptionText, "Corruption", outgoing.Corruption, isDps: false);
        SetLine(outgoingMinionText, "Minion", outgoing.Minion, isDps: false);
        SetLine(outgoingAilmentsText, "Ailments", GetCombinedAilmentDamage(outgoing), isDps: false);

        SetLine(incomingTotalText, "Total Damage", incomingTotal, isDps: false);
        SetLine(incomingPhysicalText, "Physical", incoming.Physical, isDps: false);
        SetLine(incomingMagicText, "Magic", incoming.Magic, isDps: false);
        SetLine(incomingCorruptionText, "Corruption", incoming.Corruption, isDps: false);
        SetLine(incomingBleedText, "Bleed", incoming.Bleed, isDps: false);
        SetLine(incomingPoisonText, "Poison", incoming.Poison, isDps: false);
        SetLine(incomingBurnText, "Burn", incoming.Burn, isDps: false);
    }

    private static float GetCombinedAilmentDamage(DpsDamageBreakdown breakdown) =>
        Mathf.Max(0f, breakdown.Bleed + breakdown.Poison + breakdown.Burn);

    private static void SetLine(TMP_Text text, string label, float value, bool isDps)
    {
        if (!text)
            return;

        if (isDps)
            text.text = $"{label}: {value:0.#} DPS";
        else
            text.text = $"{label}: {Mathf.RoundToInt(Mathf.Max(0f, value))}";
    }

    private void ResolveReferences()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (!outgoingTotalText)
            outgoingTotalText = FindText(texts, "Outgoing", "TotalDPS", "Total DPS", "Total");
        if (!outgoingPhysicalText)
            outgoingPhysicalText = FindText(texts, "Outgoing", "Physical", "Phys");
        if (!outgoingMagicText)
            outgoingMagicText = FindText(texts, "Outgoing", "Magic");
        if (!outgoingCorruptionText)
            outgoingCorruptionText = FindText(texts, "Outgoing", "Corruption", "Corrupt");
        if (!outgoingMinionText)
            outgoingMinionText = FindText(texts, "Outgoing", "Minion");
        if (!outgoingAilmentsText)
            outgoingAilmentsText = FindText(texts, "Outgoing", "Ailments", "Bleed", "Poison", "Burn");

        if (!incomingTotalText)
            incomingTotalText = FindText(texts, "Incoming", "TotalDPS", "Total DPS", "Total");
        if (!incomingPhysicalText)
            incomingPhysicalText = FindText(texts, "Incoming", "Physical", "Phys");
        if (!incomingMagicText)
            incomingMagicText = FindText(texts, "Incoming", "Magic");
        if (!incomingCorruptionText)
            incomingCorruptionText = FindText(texts, "Incoming", "Corruption", "Corrupt");
        if (!incomingBleedText)
            incomingBleedText = FindText(texts, "Incoming", "Bleed");
        if (!incomingPoisonText)
            incomingPoisonText = FindText(texts, "Incoming", "Poison");
        if (!incomingBurnText)
            incomingBurnText = FindText(texts, "Incoming", "Burn");
        if (!outgoingHeaderText)
            outgoingHeaderText = FindText(texts, "Outgoing", "HeaderLabel", "DPS");
        if (!incomingHeaderText)
            incomingHeaderText = FindText(texts, "Incoming", "HeaderLabel", "DPS");
        if (!elapsedTimeText)
            elapsedTimeText = FindText(texts, "", "ElapsedTimeText", "Elapsed Time", "Time");
        if (!individualDamageDealersText)
            individualDamageDealersText = FindText(texts, "Incoming", "IndividualDamageDealersText");
        if (!individualDamageDealersText)
            individualDamageDealersText = FindText(texts, "Incoming", "IndividualDamage", "DealersText");
        if (individualDamageDealersText != null &&
            individualDamageDealersText.name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Never bind the header label as the dynamic dealer-value output field.
            individualDamageDealersText = null;
        }

        if (!damageMitigatedSourcesText)
            damageMitigatedSourcesText = FindText(texts, "Incoming", "DamageMitigatedSourcesText");
        if (!damageMitigatedSourcesText)
            damageMitigatedSourcesText = FindText(texts, "Incoming", "DamageMitigated", "MitigatedSourcesText");
        if (damageMitigatedSourcesText != null &&
            damageMitigatedSourcesText.name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            damageMitigatedSourcesText = null;
        }

        if (!individualOutgoingDamageSourcesText)
            individualOutgoingDamageSourcesText = FindText(texts, "Outgoing", "IndividualDamageSourceText", "DamageSource");
        if (!individualOutgoingDamageSourcesText)
            individualOutgoingDamageSourcesText = FindText(texts, "Outgoing", "IndividualDamageSource", "DamageSourceOutput");
        if (individualOutgoingDamageSourcesText != null &&
            individualOutgoingDamageSourcesText.name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            individualOutgoingDamageSourcesText = null;
        }

        if (!individualIncomingHealingSourcesText)
            individualIncomingHealingSourcesText = FindText(texts, "Healing", "IndividualHealingSourceText", "HealingSource");
        if (!individualIncomingHealingSourcesText)
            individualIncomingHealingSourcesText = FindText(texts, "", "IndividualHealingSourceText", "IndividualHealingSource");
        if (individualIncomingHealingSourcesText != null &&
            individualIncomingHealingSourcesText.name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            individualIncomingHealingSourcesText = null;
        }

        if (!resetButton)
            resetButton = FindButtonByObjectName("Reset", "ResetButton")
                ?? FindButtonByLabelContaining("reset");
        if (!pausePlayButton)
            pausePlayButton = FindButtonByObjectName(
                "PausePlayButton",
                "PagePlaybutton",
                "PagePlayButton",
                "PausePlay");
        if (pausePlayButton && !pausePlayButtonIcon)
            pausePlayButtonIcon = ResolvePausePlayButtonIcon(pausePlayButton);
        TryAssignDefaultPlayPauseSprites();
        if (!dpsOrDamageButton)
            dpsOrDamageButton = FindButtonByObjectName(
                    "DPSorDamageButton",
                    "DpsOrDamageButton",
                    "PerSecondOrTotalButton",
                    "DmgButton",
                    "DamageModeButton")
                ?? FindButtonByDpsDamageLabel();
        if (dpsOrDamageButton)
            dpsOrDamageButtonText = ResolveModeButtonLabel(dpsOrDamageButton);
    }

    private void WireButtons()
    {
        if (resetButton)
        {
            DisableConflictingButtonBehaviours(resetButton.gameObject);
            // Own this button behavior so legacy/template listeners do not close/toggle the window.
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnResetClicked);
        }

        if (dpsOrDamageButton)
        {
            DisableConflictingButtonBehaviours(dpsOrDamageButton.gameObject);
            // Own this button behavior so legacy/template listeners do not close/toggle the window.
            dpsOrDamageButton.onClick.RemoveAllListeners();
            dpsOrDamageButton.onClick.AddListener(OnToggleMetricModeClicked);
        }

        if (pausePlayButton)
        {
            DisableConflictingButtonBehaviours(pausePlayButton.gameObject);
            pausePlayButton.onClick.RemoveAllListeners();
            pausePlayButton.onClick.AddListener(OnPausePlayButtonClicked);
        }
    }

    /// <summary>
    /// Some UI scripts attach listeners in Start/late scene init, which can win on first open after map/login.
    /// Re-apply our wiring on the next frames so reset/mode buttons never act like close/toggle.
    /// </summary>
    private IEnumerator CoWireButtonsAfterInitializers()
    {
        yield return null;
        WireButtons();
        yield return null;
        WireButtons();
    }

    private static void DisableConflictingButtonBehaviours(GameObject go)
    {
        if (!go)
            return;

        UIWindowCloseButton close = go.GetComponent<UIWindowCloseButton>();
        if (close) close.enabled = false;

        FullDpsWindowToggleUI fullToggle = go.GetComponent<FullDpsWindowToggleUI>();
        if (fullToggle) fullToggle.enabled = false;

        ActivityWindowToggleUI activityToggle = go.GetComponent<ActivityWindowToggleUI>();
        if (activityToggle) activityToggle.enabled = false;

        TrackerWindowToggleUI trackerToggle = go.GetComponent<TrackerWindowToggleUI>();
        if (trackerToggle) trackerToggle.enabled = false;

        WindowToggleUI genericToggle = go.GetComponent<WindowToggleUI>();
        if (genericToggle) genericToggle.enabled = false;
    }

    private void OnResetClicked()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
        _displayPaused = false;
        combat?.ResetDpsTrackerNow();
        ApplyPlayPauseButtonVisual();
        Refresh();
    }

    private void OnPausePlayButtonClicked()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (_displayPaused)
        {
            _displayPaused = false;
            if (combat != null && !combat.IsDpsTrackerRunning())
                combat.StartDpsTrackerSession();
        }
        else
        {
            _displayPaused = true;
        }

        ApplyPlayPauseButtonVisual();
        if (!_displayPaused)
            Refresh();
    }

    private void ApplyPlayPauseButtonVisual()
    {
        if (!pausePlayButtonIcon)
            return;

        bool showPauseIcon = !_displayPaused && combat != null && combat.IsDpsTrackerRunning();
        Sprite sprite = showPauseIcon ? dpsTrackerPauseSprite : dpsTrackerPlaySprite;
        if (sprite != null)
            pausePlayButtonIcon.sprite = sprite;
    }

    private static Image ResolvePausePlayButtonIcon(Button button)
    {
        if (!button)
            return null;

        Transform pausePlayChild = button.transform.Find("PausePlay");
        if (pausePlayChild != null && pausePlayChild.TryGetComponent(out Image pausePlayImage))
            return pausePlayImage;

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;
            if (image.name.IndexOf("PausePlay", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return image;
        }

        return button.GetComponent<Image>();
    }

    private void TryAssignDefaultPlayPauseSprites()
    {
#if UNITY_EDITOR
        if (!dpsTrackerPauseSprite)
        {
            UnityEngine.Object[] pauseAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/5.Art/Sprites/UI/icons8-pause-50.png");
            for (int i = 0; i < pauseAssets.Length; i++)
            {
                if (pauseAssets[i] is Sprite pauseSprite)
                {
                    dpsTrackerPauseSprite = pauseSprite;
                    break;
                }
            }
        }

        if (!dpsTrackerPlaySprite)
        {
            UnityEngine.Object[] playAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/5.Art/Sprites/UI/icons8-play-50.png");
            for (int i = 0; i < playAssets.Length; i++)
            {
                if (playAssets[i] is Sprite playSprite)
                {
                    dpsTrackerPlaySprite = playSprite;
                    break;
                }
            }
        }
#endif

        if (!dpsTrackerPauseSprite)
            dpsTrackerPauseSprite = FindSpriteByName("icons8-pause-50_0", "icons8-pause-50");
        if (!dpsTrackerPlaySprite)
            dpsTrackerPlaySprite = FindSpriteByName("icons8-play-50_0", "icons8-play-50");
    }

    private static Sprite FindSpriteByName(params string[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
            return null;

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int c = 0; c < candidates.Length; c++)
        {
            string candidate = candidates[c];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite != null && string.Equals(sprite.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
        }

        return null;
    }

    private void OnToggleMetricModeClicked()
    {
        _mode = _mode == MetricMode.Dps ? MetricMode.TotalDamage : MetricMode.Dps;
        ApplyModeToTracker();
        Refresh();
    }

    private void ApplyModeToTracker()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        bool dpsMode = _mode == MetricMode.Dps;

        if (outgoingHeaderText)
            outgoingHeaderText.text = dpsMode ? "Outgoing DPS" : "Outgoing Total Damage";
        if (incomingHeaderText)
            incomingHeaderText.text = dpsMode ? "Incoming DPS" : "Incoming Total Damage";
        if (dpsOrDamageButtonText)
            dpsOrDamageButtonText.text = dpsMode ? "Total" : "Per second";
    }

    private Button FindButtonByObjectName(params string[] nameCandidates)
    {
        if (nameCandidates == null || nameCandidates.Length == 0)
            return null;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (!b)
                continue;

            for (int n = 0; n < nameCandidates.Length; n++)
            {
                string candidate = nameCandidates[n];
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    b.name.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return b;
                }
            }
        }

        return null;
    }

    private Button FindButtonByLabelContaining(string labelPart)
    {
        if (string.IsNullOrWhiteSpace(labelPart))
            return null;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (!b || IsPausePlayButton(b))
                continue;

            TMP_Text[] labels = b.GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < labels.Length; j++)
            {
                TMP_Text label = labels[j];
                if (!label || string.IsNullOrWhiteSpace(label.text))
                    continue;

                if (label.text.IndexOf(labelPart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return b;
            }
        }

        return null;
    }

    private Button FindButtonByDpsDamageLabel()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (!b || IsPausePlayButton(b))
                continue;

            TMP_Text[] labels = b.GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < labels.Length; j++)
            {
                TMP_Text label = labels[j];
                if (!label || string.IsNullOrWhiteSpace(label.text))
                    continue;

                string body = label.text;
                bool looksLikeDpsDamageToggle =
                    body.IndexOf("dps", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    body.IndexOf("damage", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (looksLikeDpsDamageToggle)
                    return b;
            }
        }

        return null;
    }

    private static bool IsPausePlayButton(Button button)
    {
        if (!button)
            return false;

        string name = button.name ?? string.Empty;
        return name.IndexOf("PausePlay", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("PagePlay", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static TMP_Text FindText(TMP_Text[] texts, string sectionName, params string[] candidates)
    {
        TMP_Text firstKeywordMatch = null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (!text)
                continue;

            if (!ContainsAny(text.name, candidates) && !ContainsAny(text.text, candidates))
                continue;

            if (HasAncestorName(text.transform, sectionName))
                return text;

            firstKeywordMatch ??= text;
        }

        return firstKeywordMatch;
    }

    private static TMP_Text ResolveModeButtonLabel(Button button)
    {
        if (!button)
            return null;

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text fallback = null;
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text t = labels[i];
            if (!t)
                continue;
            fallback ??= t;
            string n = t.name ?? string.Empty;
            if (n.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            return t;
        }

        return fallback;
    }

    private static bool ContainsAny(string value, string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(value) || candidates == null)
            return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (!string.IsNullOrWhiteSpace(candidate) &&
                value.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool HasAncestorName(Transform transform, string namePart)
    {
        if (transform == null || string.IsNullOrWhiteSpace(namePart))
            return false;

        Transform current = transform;
        while (current != null)
        {
            if (current.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            current = current.parent;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            ResolveReferences();
        TryAssignDefaultPlayPauseSprites();
    }
#endif
}
