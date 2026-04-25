using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DpsBreakdownTrackerUI : MonoBehaviour
{
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

    [Header("Refresh")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;

    private float _nextRefreshTime;

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
        TMP_Text text = transform.GetComponent<TMP_Text>();
        return text && text.text.IndexOf("Full Damage Breakdown", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        float outgoingTotal = combat ? combat.GetCurrentDps() : 0f;
        float incomingTotal = combat ? combat.GetCurrentIncomingDps() : 0f;
        DpsDamageBreakdown outgoing = combat ? combat.GetOutgoingDpsBreakdown() : default;
        DpsDamageBreakdown incoming = combat ? combat.GetIncomingDpsBreakdown() : default;

        SetLine(outgoingTotalText, "TotalDPS", outgoingTotal);
        SetLine(outgoingPhysicalText, "Physical", outgoing.Physical);
        SetLine(outgoingMagicText, "Magic", outgoing.Magic);
        SetLine(outgoingCorruptionText, "Corruption", outgoing.Corruption);
        SetLine(outgoingMinionText, "Minion", outgoing.Minion);
        SetLine(outgoingBleedText, "Bleed", outgoing.Bleed);
        SetLine(outgoingPoisonText, "Poison", outgoing.Poison);
        SetLine(outgoingBurnText, "Burn", outgoing.Burn);

        SetLine(incomingTotalText, "TotalDPS", incomingTotal);
        SetLine(incomingPhysicalText, "Physical", incoming.Physical);
        SetLine(incomingMagicText, "Magic", incoming.Magic);
        SetLine(incomingCorruptionText, "Corruption", incoming.Corruption);
        SetLine(incomingBleedText, "Bleed", incoming.Bleed);
        SetLine(incomingPoisonText, "Poison", incoming.Poison);
        SetLine(incomingBurnText, "Burn", incoming.Burn);
    }

    private static void SetLine(TMP_Text text, string label, float dps)
    {
        if (!text)
            return;

        text.text = $"{label}: {dps:0.#} DPS";
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
