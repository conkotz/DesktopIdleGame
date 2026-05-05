using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Text;
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
    [SerializeField] private Button dpsOrDamageButton;
    [SerializeField] private TMP_Text dpsOrDamageButtonText;

    [Header("Outgoing")]
    [SerializeField] private TMP_Text outgoingTotalText;
    [SerializeField] private TMP_Text outgoingPhysicalText;
    [SerializeField] private TMP_Text outgoingMagicText;
    [SerializeField] private TMP_Text outgoingCorruptionText;
    [SerializeField] private TMP_Text outgoingMinionText;
    [SerializeField] private TMP_Text outgoingBleedText;
    [SerializeField] private TMP_Text outgoingPoisonText;
    [SerializeField] private TMP_Text outgoingBurnText;

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

    [Header("Refresh")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;
    [Header("Debug")]
    [SerializeField] private bool debugScrollDiagnostics = true;

    private float _nextRefreshTime;
    private MetricMode _mode = MetricMode.Dps;
    private Coroutine _lateWireRoutine;
    private bool _loggedScrollDiagnostics;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoAttach()
    {
        SceneManager.sceneLoaded += (_, _) => AutoAttachToTrackerWindows();
        AutoAttachToTrackerWindows();
    }

    private static void AutoAttachToTrackerWindows()
    {
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
        ResolveReferences();
        EnsureScrollViewMasking();
        WireButtons();
        if (_lateWireRoutine != null)
            StopCoroutine(_lateWireRoutine);
        _lateWireRoutine = StartCoroutine(CoWireButtonsAfterInitializers());
        ApplyModeToTracker();
        Refresh();
    }

    private void OnDisable()
    {
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
        MaybeLogScrollDiagnostics();
        Refresh();
    }

    private void Refresh()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        RefreshElapsedTime();
        RefreshIncomingDealerDamage(combat);

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
            return;
        }

        var entries = currentCombat.GetIncomingDamageByDealer();
        if (entries == null || entries.Count == 0)
        {
            individualDamageDealersText.text = dpsMode ? "No incoming DPS yet" : "No incoming damage yet";
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
    }

    private void EnsureScrollViewMasking()
    {
        if (!individualDamageDealersText)
            return;

        ScrollRect sr = individualDamageDealersText.GetComponentInParent<ScrollRect>(true);
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

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0f, 1f);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (viewport)
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
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
        float outgoingTotal = currentCombat ? currentCombat.GetCurrentDps() : 0f;
        float incomingTotal = currentCombat ? currentCombat.GetCurrentIncomingDps() : 0f;
        DpsDamageBreakdown outgoing = currentCombat ? currentCombat.GetOutgoingDpsBreakdown() : default;
        DpsDamageBreakdown incoming = currentCombat ? currentCombat.GetIncomingDpsBreakdown() : default;

        SetLine(outgoingTotalText, "TotalDPS", outgoingTotal, isDps: true);
        SetLine(outgoingPhysicalText, "Physical", outgoing.Physical, isDps: true);
        SetLine(outgoingMagicText, "Magic", outgoing.Magic, isDps: true);
        SetLine(outgoingCorruptionText, "Corruption", outgoing.Corruption, isDps: true);
        SetLine(outgoingMinionText, "Minion", outgoing.Minion, isDps: true);
        SetLine(outgoingBleedText, "Bleed", outgoing.Bleed, isDps: true);
        SetLine(outgoingPoisonText, "Poison", outgoing.Poison, isDps: true);
        SetLine(outgoingBurnText, "Burn", outgoing.Burn, isDps: true);

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
        float outgoingTotal = currentCombat ? currentCombat.GetOutgoingTotalDamage() : 0f;
        float incomingTotal = currentCombat ? currentCombat.GetIncomingTotalDamage() : 0f;
        DpsDamageBreakdown outgoing = currentCombat ? currentCombat.GetOutgoingTotalDamageBreakdown() : default;
        DpsDamageBreakdown incoming = currentCombat ? currentCombat.GetIncomingTotalDamageBreakdown() : default;

        SetLine(outgoingTotalText, "Total Damage", outgoingTotal, isDps: false);
        SetLine(outgoingPhysicalText, "Physical", outgoing.Physical, isDps: false);
        SetLine(outgoingMagicText, "Magic", outgoing.Magic, isDps: false);
        SetLine(outgoingCorruptionText, "Corruption", outgoing.Corruption, isDps: false);
        SetLine(outgoingMinionText, "Minion", outgoing.Minion, isDps: false);
        SetLine(outgoingBleedText, "Bleed", outgoing.Bleed, isDps: false);
        SetLine(outgoingPoisonText, "Poison", outgoing.Poison, isDps: false);
        SetLine(outgoingBurnText, "Burn", outgoing.Burn, isDps: false);

        SetLine(incomingTotalText, "Total Damage", incomingTotal, isDps: false);
        SetLine(incomingPhysicalText, "Physical", incoming.Physical, isDps: false);
        SetLine(incomingMagicText, "Magic", incoming.Magic, isDps: false);
        SetLine(incomingCorruptionText, "Corruption", incoming.Corruption, isDps: false);
        SetLine(incomingBleedText, "Bleed", incoming.Bleed, isDps: false);
        SetLine(incomingPoisonText, "Poison", incoming.Poison, isDps: false);
        SetLine(incomingBurnText, "Burn", incoming.Burn, isDps: false);
    }

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
        if (!outgoingBleedText)
            outgoingBleedText = FindText(texts, "Outgoing", "Bleed");
        if (!outgoingPoisonText)
            outgoingPoisonText = FindText(texts, "Outgoing", "Poison");
        if (!outgoingBurnText)
            outgoingBurnText = FindText(texts, "Outgoing", "Burn");

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
            individualDamageDealersText = FindText(texts, "Incoming", "IndividualDamageDealersText", "DamageDealersText", "Dealer");
        if (!resetButton)
            resetButton = FindButtonByName("Reset", "ResetButton");
        if (!dpsOrDamageButton)
            dpsOrDamageButton = FindButtonByName("DPSorDamageButton", "DpsOrDamageButton", "DmgButton", "DamageModeButton");
        if (!dpsOrDamageButtonText && dpsOrDamageButton)
            dpsOrDamageButtonText = dpsOrDamageButton.GetComponentInChildren<TMP_Text>(true);
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

        WindowToggleUI genericToggle = go.GetComponent<WindowToggleUI>();
        if (genericToggle) genericToggle.enabled = false;
    }

    private void OnResetClicked()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
        combat?.ResetDpsTrackerNow();
        Refresh();
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
        combat?.SetDpsAutoResetEnabled(dpsMode);

        if (outgoingHeaderText)
            outgoingHeaderText.text = dpsMode ? "Outgoing DPS" : "Outgoing Total Damage";
        if (incomingHeaderText)
            incomingHeaderText.text = dpsMode ? "Incoming DPS" : "Incoming Total Damage";
        if (dpsOrDamageButtonText)
            dpsOrDamageButtonText.text = dpsMode ? "DMG" : "DPS";
    }

    private Button FindButtonByName(params string[] nameCandidates)
    {
        if (nameCandidates == null || nameCandidates.Length == 0)
            return null;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (!b) continue;
            for (int n = 0; n < nameCandidates.Length; n++)
            {
                string candidate = nameCandidates[n];
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    b.name.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return b;
            }
        }

        return null;
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
    }
#endif
}
