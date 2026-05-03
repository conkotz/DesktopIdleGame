using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum NpcDialogueConditionKind
{
    [Tooltip("True after the player has claimed this quest's reward (quest fully finished).")]
    CompletedQuest = 0,

    [Tooltip(
        "After the player dies and respawns (scene reload), or when loading a save with this flag still set. " +
        "When After Death Respawn Map Node Id is set, it must match the map where the player died (not the map you are on when the NPC speaks). Cleared when this dialogue is shown.")]
    AfterDeathAndRespawn = 1,
}

public enum NpcDialogueOutcomeKind
{
    None = 0,
    [Tooltip("Loads GamePlay with the chosen map node (same flow as quest reward teleport).")]
    TeleportToMapNode = 1,
}

[Serializable]
public class NpcConditionalDialogueEntry
{
    public NpcDialogueConditionKind condition = NpcDialogueConditionKind.CompletedQuest;

    [Tooltip("Quest id (QuestDefinition.questId). Must match after reward is claimed.")]
    public string completedQuestId = "";

    [Tooltip(
        "For After Death And Respawn only: require this MapNodeDefinition.nodeId for the map where the player **died** " +
        "(e.g. tutorial_3). Empty = any map. Legacy saves with pending but no stored death node fall back to current map for this check.")]
    public string afterDeathRespawnMapNodeId = "";

    [TextArea(2, 6)]
    public string dialogue = "";

    [Header("On Accept (optional)")]
    [Tooltip("When not None, the dialogue box shows an Accept button and runs this after click.")]
    public NpcDialogueOutcomeKind onAcceptOutcome = NpcDialogueOutcomeKind.None;

    [Tooltip("e.g. drag MapNode_town_duskwood, or leave empty and set Teleport Map Node Id.")]
    public MapNodeDefinition teleportTargetNode;

    [Tooltip("Used when Teleport Target Node is empty. Must match MapNodeDefinition.nodeId (e.g. duskwood).")]
    public string teleportMapNodeId = "";
}

public class NPCInteractionSettings : MonoBehaviour
{
    private const string GameplaySceneName = "GamePlay";

    [Header("Dialogue")]
    [TextArea(2, 6)]
    [SerializeField] private string dialogue = "howdy";
    [SerializeField] private NPCDialogueBoxUI dialogueBoxPrefab;
    [Tooltip("World offset from the top-right of this object's Collider2D bounds.")]
    [SerializeField] private Vector3 dialogueLocalOffset = new(1.95f, 0.5f, 0f);
    [SerializeField] private float nonQuestAutoCloseSeconds = 5f;
    [Tooltip("When enabled, shows dialogue the first time this NPC is on-screen, then re-opens automatically when conditional dialogue changes (e.g. after a quest completes).")]
    [SerializeField] private bool openDialogueOnFirstSighting;

    [Tooltip(
        "When enabled, opens this NPC's resolved plain dialogue when the quest below becomes accepted (edge only — " +
        "does not fire on load if it was already accepted). For quests with no obtain location, the game treats them as always accepted; use a giver-based quest id.")]
    [SerializeField] private bool openDialogueAfterQuestAccepted;

    [Tooltip("QuestDefinition.questId (must match the quest asset). Shown only when the toggle above is on.")]
    [ShowWhenTrue(nameof(openDialogueAfterQuestAccepted))]
    [SerializeField] private string openDialogueAfterQuestAcceptedQuestId = "";

    [Tooltip(
        "When on, the first time any Additional Conditional Dialogue row is shown, the base Dialogue line is never used again for this NPC — only matching conditionals (e.g. after-death, then completed-quest). Persists when One Way Dialogue Queue Save Id is set.")]
    [SerializeField] private bool oneWayDialogueQueue;

    [ShowWhenTrue(nameof(oneWayDialogueQueue))]
    [Tooltip(
        "Unique id on this save slot (e.g. npc_tutorial_3_merlin). Empty = progress resets when you leave play mode or load without this component re-running; set an id for shipped saves.")]
    [SerializeField] private string oneWayDialogueQueueSaveId = "";

    [Header("Additional dialogues (conditional)")]
    [Tooltip("Evaluated top to bottom; later rows override earlier ones when their condition is met. Falls back to Dialogue above.")]
    [SerializeField] private List<NpcConditionalDialogueEntry> additionalConditionalDialogues = new();

    [Header("Quest integration")]
    [SerializeField] private QuestGiver questGiver;

    private NPCDialogueBoxUI _activeDialogue;
    private bool _hasOpenedOnFirstSighting;
    private string _lastAutoPlainDialogueSignature = "";
    private QuestProgressManager _boundQuestProgress;

    private bool _watchQuestAcceptedBaselineReady;
    private bool _watchQuestAcceptedWasAccepted;

    private Coroutine _deferredQuestAcceptedPlainDialogueRoutine;

    /// <summary>When <see cref="oneWayDialogueQueueSaveId"/> is empty, one-way progress is kept in memory for this run only.</summary>
    private bool _sessionOneWayConditionalConsumed;

    /// <summary>Highest conditional row index (0-based) presented this session when <see cref="oneWayDialogueQueueSaveId"/> is empty.</summary>
    private int _sessionHighestOneWayConditionalPresented = -1;

    private void Awake()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _watchQuestAcceptedBaselineReady = false;
    }
#endif

    private void OnEnable()
    {
        TrySubscribeQuestProgressForAutoDialogue();
    }

    private void OnDisable()
    {
        StopDeferredQuestAcceptedPlainDialogue();
        TryUnsubscribeQuestProgressForAutoDialogue();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (NeedsQuestProgressSubscription() && _boundQuestProgress == null)
            TrySubscribeQuestProgressForAutoDialogue();

        if (!openDialogueOnFirstSighting || _hasOpenedOnFirstSighting)
            return;

        Camera cam = Camera.main;
        if (!cam || !IsVisibleInCameraViewport(cam))
            return;

        // Do not set _hasOpenedOnFirstSighting here — only after a successful ShowAt inside ShowNormalDialogueOnly,
        // otherwise one empty resolve (e.g. save / death-pending not hydrated yet) permanently skips auto dialogue.
        ShowNormalDialogueOnly(false);
    }

    private bool HasConditionalDialogues() =>
        additionalConditionalDialogues != null && additionalConditionalDialogues.Count > 0;

    private bool IsOneWayBaseDialogueConsumed()
    {
        if (!oneWayDialogueQueue)
            return false;

        if (!string.IsNullOrWhiteSpace(oneWayDialogueQueueSaveId))
            return NpcOneWayDialogueQueueStore.IsConsumed(oneWayDialogueQueueSaveId.Trim());

        return _sessionOneWayConditionalConsumed;
    }

    private void MaybeMarkOneWayBaseDialogueConsumed(NpcConditionalDialogueEntry winning)
    {
        if (!oneWayDialogueQueue || winning == null)
            return;

        if (!string.IsNullOrWhiteSpace(oneWayDialogueQueueSaveId))
            NpcOneWayDialogueQueueStore.MarkConsumedAndSave(oneWayDialogueQueueSaveId.Trim());
        else
            _sessionOneWayConditionalConsumed = true;
    }

    /// <summary>
    /// After any conditional row has been shown, never evaluate earlier rows again. Later rows can still win;
    /// <see cref="Interact"/> may fall back to base dialogue when no row matches.
    /// </summary>
    private int GetHighestOneWayConditionalPresented()
    {
        if (!oneWayDialogueQueue)
            return -1;

        if (!string.IsNullOrWhiteSpace(oneWayDialogueQueueSaveId))
            return NpcOneWayDialogueQueueStore.GetHighestConditionalIndexPresented(oneWayDialogueQueueSaveId.Trim());

        if (!_sessionOneWayConditionalConsumed)
            return -1;

        return _sessionHighestOneWayConditionalPresented < 0 ? 0 : _sessionHighestOneWayConditionalPresented;
    }

    private void RecordOneWayConditionalPresented(int conditionalIndex)
    {
        if (!oneWayDialogueQueue || conditionalIndex < 0)
            return;

        if (!string.IsNullOrWhiteSpace(oneWayDialogueQueueSaveId))
            NpcOneWayDialogueQueueStore.RecordHighestConditionalIndexPresented(
                oneWayDialogueQueueSaveId.Trim(),
                conditionalIndex);
        else
            _sessionHighestOneWayConditionalPresented =
                Mathf.Max(_sessionHighestOneWayConditionalPresented, conditionalIndex);
    }

    private void NotifyOneWayConditionalPresented(NpcConditionalDialogueEntry winning)
    {
        if (winning == null || additionalConditionalDialogues == null)
            return;

        for (int i = 0; i < additionalConditionalDialogues.Count; i++)
        {
            if (!ReferenceEquals(additionalConditionalDialogues[i], winning))
                continue;
            RecordOneWayConditionalPresented(i);
            return;
        }
    }

    /// <summary>First conditional index to evaluate (skips rows already superseded by the one-way chain).</summary>
    private int GetOneWayChainMinimumConditionalIndexForEvaluation()
    {
        if (additionalConditionalDialogues == null || additionalConditionalDialogues.Count == 0)
            return 0;
        if (!oneWayDialogueQueue || !IsOneWayBaseDialogueConsumed())
            return 0;

        int highest = GetHighestOneWayConditionalPresented();
        return Mathf.Min(highest + 1, additionalConditionalDialogues.Count);
    }

    private bool NeedsQuestProgressSubscription() =>
        (openDialogueOnFirstSighting && HasConditionalDialogues()) ||
        (oneWayDialogueQueue && HasConditionalDialogues()) ||
        (openDialogueAfterQuestAccepted && !string.IsNullOrWhiteSpace(openDialogueAfterQuestAcceptedQuestId));

    /// <summary>
    /// Quest progress should re-resolve conditional plain dialogue when first-sighting auto-dialogue has run, or when
    /// one-way queue has advanced (so e.g. Completed Quest lines still open after the death line was shown without first sighting).
    /// </summary>
    private bool ShouldReactToQuestProgressWithConditionalDialogue()
    {
        if (!HasConditionalDialogues())
            return false;

        if (openDialogueOnFirstSighting && _hasOpenedOnFirstSighting)
            return true;

        if (oneWayDialogueQueue && IsOneWayBaseDialogueConsumed())
            return true;

        return false;
    }

    /// <summary>Used by world click routing: merchants open the shop unless a quest offer should appear instead.</summary>
    public bool HasAvailableQuestOffers()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();

        List<QuestDefinition> quests =
            questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>();

        return quests.Count > 0;
    }

    public void Interact()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();

        if (questGiver != null && questGiver.TryClaimFirstReadyQuestReward())
            return;

        List<QuestDefinition> quests =
            questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>();

        TryGetResolvedPlainDialogue(
            out string interactResolved,
            out _,
            out _,
            out _,
            allowBaseWhenOneWayHasNoMatchingConditional: true);
        EnsurePlainDialogueNeverEmpty(ref interactResolved);
        if (quests.Count == 0 && string.IsNullOrWhiteSpace(interactResolved))
        {
            if (_activeDialogue)
                _activeDialogue.Hide();
            return;
        }

        if (quests.Count > 0 && NPCDialogueBoxUI.TryReshowCollapsedPlainDialogueForNpc(transform))
            return;

        if (quests.Count > 0 &&
            NPCDialogueBoxUI.HasPinnedPlainQuestSpreadForNpc(transform) &&
            !NPCDialogueBoxUI.IsPlainHostCollapsedForActiveSpread())
            return;

        if (quests.Count > 0 &&
            _activeDialogue &&
            _activeDialogue.gameObject.activeInHierarchy &&
            NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform) &&
            NPCDialogueBoxUI.IsEligiblePlainHostForStackedQuestOffers(_activeDialogue))
        {
            _activeDialogue.StackQuestOffersBesidePlainDialogue(
                quests,
                () => questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>(),
                q => questGiver != null && questGiver.TryAcceptQuest(q),
                autoCloseSeconds: 0f);
            return;
        }

        if (NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform))
            return;

        NPCDialogueBoxUI box = GetOrCreateDialogueBox();
        if (!box)
            return;

        if (quests.Count > 0)
        {
            box.ShowQuestOffersAt(
                transform,
                transform,
                Vector3.zero,
                quests,
                () => questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>(),
                q => questGiver != null && questGiver.TryAcceptQuest(q),
                autoCloseSeconds: 0f);
            return;
        }

        TryGetResolvedPlainDialogue(
            out string text,
            out bool showAccept,
            out Action onAccept,
            out NpcConditionalDialogueEntry winning,
            allowBaseWhenOneWayHasNoMatchingConditional: true);
        EnsurePlainDialogueNeverEmpty(ref text);
        float autoClose = showAccept ? 0f : nonQuestAutoCloseSeconds;
        box.ShowAt(transform, transform, Vector3.zero, text, showAccept, onAccept, autoClose);
        RememberAutoPlainDialogueSignatureIfNeeded(text, showAccept);
        NotifyPresentedDeathRespawnDialogueIfNeeded(winning);
        MaybeMarkOneWayBaseDialogueConsumed(winning);
        NotifyOneWayConditionalPresented(winning);
    }

    private void ShowNormalDialogueOnly(bool replaceExistingThisNpcDialogue)
    {
        TryGetResolvedPlainDialogue(
            out string text,
            out bool showAccept,
            out Action onAccept,
            out NpcConditionalDialogueEntry winning,
            allowBaseWhenOneWayHasNoMatchingConditional: true);
        EnsurePlainDialogueNeverEmpty(ref text);
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform))
        {
            if (!replaceExistingThisNpcDialogue)
                return;
            if (_activeDialogue)
                _activeDialogue.Hide();
        }

        NPCDialogueBoxUI box = GetOrCreateDialogueBox();
        if (!box)
            return;

        float autoClose = showAccept ? 0f : nonQuestAutoCloseSeconds;
        box.ShowAt(
            transform,
            transform,
            Vector3.zero,
            text,
            showAccept,
            onAccept,
            autoClose);
        if (openDialogueOnFirstSighting)
            _hasOpenedOnFirstSighting = true;
        RememberAutoPlainDialogueSignatureIfNeeded(text, showAccept);
        NotifyPresentedDeathRespawnDialogueIfNeeded(winning);
        MaybeMarkOneWayBaseDialogueConsumed(winning);
        NotifyOneWayConditionalPresented(winning);
    }

    private void NotifyPresentedDeathRespawnDialogueIfNeeded(NpcConditionalDialogueEntry winning)
    {
        if (!NpcPostDeathRespawnDialogueStore.IsPending || winning == null)
            return;
        if (winning.condition == NpcDialogueConditionKind.AfterDeathAndRespawn)
        {
            NpcPostDeathRespawnDialogueStore.ClearPendingAndSave();
            return;
        }

        if (winning.condition == NpcDialogueConditionKind.CompletedQuest && HasAfterDeathConditionalEntry())
            NpcPostDeathRespawnDialogueStore.ClearPendingAndSave();
    }

    private bool HasAfterDeathConditionalEntry()
    {
        if (additionalConditionalDialogues == null)
            return false;
        for (int i = 0; i < additionalConditionalDialogues.Count; i++)
        {
            NpcConditionalDialogueEntry e = additionalConditionalDialogues[i];
            if (e != null && e.condition == NpcDialogueConditionKind.AfterDeathAndRespawn)
                return true;
        }

        return false;
    }

    private void RememberAutoPlainDialogueSignatureIfNeeded(string text, bool showAccept)
    {
        if (!openDialogueOnFirstSighting && !oneWayDialogueQueue)
            return;
        _lastAutoPlainDialogueSignature = BuildPlainDialogueSignature(text, showAccept);
    }

    private static string BuildPlainDialogueSignature(string text, bool showAccept) =>
        $"{showAccept}\u001f{text ?? ""}";

    private void TrySubscribeQuestProgressForAutoDialogue()
    {
        if (!NeedsQuestProgressSubscription())
            return;

        QuestProgressManager mgr = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (!mgr || mgr == _boundQuestProgress)
            return;

        TryUnsubscribeQuestProgressForAutoDialogue();
        _boundQuestProgress = mgr;
        _boundQuestProgress.ProgressChanged += HandleQuestProgressChangedForAutoDialogue;

        if (openDialogueAfterQuestAccepted && !string.IsNullOrWhiteSpace(openDialogueAfterQuestAcceptedQuestId))
        {
            _watchQuestAcceptedWasAccepted = IsWatchedQuestAcceptedNow(mgr);
            _watchQuestAcceptedBaselineReady = true;
        }
    }

    private void TryUnsubscribeQuestProgressForAutoDialogue()
    {
        if (_boundQuestProgress == null)
            return;
        _boundQuestProgress.ProgressChanged -= HandleQuestProgressChangedForAutoDialogue;
        _boundQuestProgress = null;
    }

    private void HandleQuestProgressChangedForAutoDialogue()
    {
        if (!Application.isPlaying)
            return;

        HandleAfterQuestAcceptedDialogue();

        if (!ShouldReactToQuestProgressWithConditionalDialogue())
            return;

        TryGetResolvedPlainDialogue(out string text, out bool showAccept, out Action onAccept, out _);
        if (string.IsNullOrWhiteSpace(text))
            return;

        string sig = BuildPlainDialogueSignature(text, showAccept);
        if (sig == _lastAutoPlainDialogueSignature)
            return;

        ShowNormalDialogueOnly(true);
    }

    private bool IsWatchedQuestAcceptedNow(QuestProgressManager mgr)
    {
        if (mgr == null || string.IsNullOrWhiteSpace(openDialogueAfterQuestAcceptedQuestId))
            return false;

        QuestDefinition q = mgr.GetQuestDefinition(openDialogueAfterQuestAcceptedQuestId.Trim());
        return q != null && mgr.IsQuestAccepted(q);
    }

    private void HandleAfterQuestAcceptedDialogue()
    {
        if (!openDialogueAfterQuestAccepted || string.IsNullOrWhiteSpace(openDialogueAfterQuestAcceptedQuestId))
            return;
        if (_boundQuestProgress == null)
            return;

        if (!_watchQuestAcceptedBaselineReady)
        {
            _watchQuestAcceptedWasAccepted = IsWatchedQuestAcceptedNow(_boundQuestProgress);
            _watchQuestAcceptedBaselineReady = true;
            return;
        }

        bool now = IsWatchedQuestAcceptedNow(_boundQuestProgress);
        if (now && !_watchQuestAcceptedWasAccepted)
        {
            // Quest offer Accept runs TryAcceptQuest inside NPCDialogueBoxUI.HandleAcceptClicked, then always calls Hide()
            // on the same box. Showing plain dialogue synchronously would be closed immediately — wait one frame.
            StopDeferredQuestAcceptedPlainDialogue();
            _deferredQuestAcceptedPlainDialogueRoutine =
                StartCoroutine(DeferredShowPlainDialogueAfterQuestAccept());
        }

        _watchQuestAcceptedWasAccepted = now;
    }

    private void StopDeferredQuestAcceptedPlainDialogue()
    {
        if (_deferredQuestAcceptedPlainDialogueRoutine == null)
            return;
        StopCoroutine(_deferredQuestAcceptedPlainDialogueRoutine);
        _deferredQuestAcceptedPlainDialogueRoutine = null;
    }

    private IEnumerator DeferredShowPlainDialogueAfterQuestAccept()
    {
        yield return null;
        _deferredQuestAcceptedPlainDialogueRoutine = null;
        ShowNormalDialogueOnly(true);
    }

    /// <param name="allowBaseWhenOneWayHasNoMatchingConditional">
    /// When true (manual <see cref="Interact"/> only): if one-way mode has suppressed the base line but no conditional row
    /// matches right now, fall back to the base Dialogue field so the box can still open. Auto dialogue paths pass false.
    /// </param>
    private void TryGetResolvedPlainDialogue(
        out string text,
        out bool showAccept,
        out Action onAccept,
        out NpcConditionalDialogueEntry winningEntry,
        bool allowBaseWhenOneWayHasNoMatchingConditional = false)
    {
        bool skipBase = IsOneWayBaseDialogueConsumed();
        text = skipBase ? "" : (dialogue != null ? dialogue.Trim() : "");
        showAccept = false;
        onAccept = null;
        winningEntry = null;

        QuestProgressManager mgr = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);

        if (additionalConditionalDialogues == null || additionalConditionalDialogues.Count == 0)
        {
            ApplyOneWayManualInteractBaseFallback(
                allowBaseWhenOneWayHasNoMatchingConditional,
                skipBase,
                ref text);
            return;
        }

        int minIndex = GetOneWayChainMinimumConditionalIndexForEvaluation();
        for (int i = minIndex; i < additionalConditionalDialogues.Count; i++)
        {
            NpcConditionalDialogueEntry e = additionalConditionalDialogues[i];
            if (e == null || string.IsNullOrWhiteSpace(e.dialogue))
                continue;
            if (!EvaluateConditionalEntry(e, mgr))
                continue;

            text = e.dialogue.Trim();
            Action built = BuildAcceptActionOrNull(e);
            showAccept = built != null;
            onAccept = built;
            winningEntry = e;
        }

        ApplyOneWayManualInteractBaseFallback(
            allowBaseWhenOneWayHasNoMatchingConditional,
            skipBase,
            ref text);
    }

    private void ApplyOneWayManualInteractBaseFallback(
        bool allowBaseWhenOneWayHasNoMatchingConditional,
        bool skipBase,
        ref string text)
    {
        if (!allowBaseWhenOneWayHasNoMatchingConditional || !skipBase || !string.IsNullOrWhiteSpace(text))
            return;
        if (string.IsNullOrWhiteSpace(dialogue))
            return;
        text = dialogue.Trim();
    }

    /// <summary>Last resort when base <see cref="dialogue"/> exists but resolution returned empty (interact + auto-open).</summary>
    private void EnsurePlainDialogueNeverEmpty(ref string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            return;
        if (string.IsNullOrWhiteSpace(dialogue))
            return;
        text = dialogue.Trim();
    }

    private static Action BuildAcceptActionOrNull(NpcConditionalDialogueEntry e)
    {
        switch (e.onAcceptOutcome)
        {
            case NpcDialogueOutcomeKind.TeleportToMapNode:
            {
                MapNodeDefinition node = ResolveTeleportMapNode(e);
                if (!node)
                    return null;
                MapNodeDefinition captured = node;
                return () => TeleportPlayerToMapNode(captured);
            }
            default:
                return null;
        }
    }

    private static MapNodeDefinition ResolveTeleportMapNode(NpcConditionalDialogueEntry e)
    {
        if (e.teleportTargetNode)
            return e.teleportTargetNode;
        if (!string.IsNullOrWhiteSpace(e.teleportMapNodeId))
            return FindMapNodeById(e.teleportMapNodeId.Trim());
        return null;
    }

    private static MapNodeDefinition FindMapNodeById(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition map = wmp ? wmp.WorldMap : null;
        if (!map)
            map = Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        if (!map)
            return null;

        return map.FindNodeById(nodeId);
    }

    private static void TeleportPlayerToMapNode(MapNodeDefinition node)
    {
        if (!node)
            return;

        ActiveLevelContext.SetPendingLevel(node, logToConsole: false);
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(GameplaySceneName);
    }

    private static bool EvaluateConditionalEntry(NpcConditionalDialogueEntry e, QuestProgressManager mgr)
    {
        switch (e.condition)
        {
            case NpcDialogueConditionKind.CompletedQuest:
                if (string.IsNullOrWhiteSpace(e.completedQuestId) || mgr == null)
                    return false;
                return mgr.IsRewardClaimed(e.completedQuestId.Trim());
            case NpcDialogueConditionKind.AfterDeathAndRespawn:
                if (!NpcPostDeathRespawnDialogueStore.IsPending)
                    return false;
                if (string.IsNullOrWhiteSpace(e.afterDeathRespawnMapNodeId))
                    return true;
                string need = e.afterDeathRespawnMapNodeId.Trim();
                string deathNode = NpcPostDeathRespawnDialogueStore.DeathOccurredOnMapNodeId;
                if (!string.IsNullOrEmpty(deathNode))
                    return string.Equals(deathNode, need, StringComparison.OrdinalIgnoreCase);
                // Legacy save: pending was set before we stored death node — fall back to old "current map" rule.
                string cur = ResolveActiveMapNodeIdForNpcConditions();
                return !string.IsNullOrEmpty(cur) &&
                       string.Equals(cur, need, StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    private static string ResolveActiveMapNodeIdForNpcConditions() =>
        NpcPostDeathRespawnDialogueStore.ResolveCurrentGameplayMapNodeId();

    /// <summary>World point for dialogue follow each frame — uses collider bounds + <see cref="dialogueLocalOffset"/> so NPC hover scale cannot drift the pivot.</summary>
    public Vector3 GetDialogueFollowWorldPoint()
    {
        Collider2D col = ResolveInteractCollider2D();
        Vector3 pivot = dialogueLocalOffset;
        if (!col)
            return transform.position + pivot;

        Bounds b = col.bounds;
        return new Vector3(b.max.x + pivot.x, b.max.y + pivot.y, transform.position.z + pivot.z);
    }

    private Collider2D ResolveInteractCollider2D()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col)
            col = GetComponentInChildren<Collider2D>();
        if (!col)
            col = GetComponentInParent<Collider2D>();
        return col;
    }

    private bool IsVisibleInCameraViewport(Camera cam)
    {
        if (!cam)
            return false;

        Bounds b;
        Collider2D col = ResolveInteractCollider2D();
        if (col != null)
            b = col.bounds;
        else
        {
            Renderer r = GetComponentInChildren<Renderer>();
            b = r != null ? r.bounds : new Bounds(transform.position, Vector3.one * 0.25f);
        }

        Vector3 min = b.min;
        Vector3 max = b.max;
        var points = new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            b.center
        };

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 vp = cam.WorldToViewportPoint(points[i]);
            if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                return true;
        }

        return false;
    }

    private NPCDialogueBoxUI GetOrCreateDialogueBox()
    {
        if (_activeDialogue)
            return _activeDialogue;

        if (dialogueBoxPrefab)
            _activeDialogue = Instantiate(dialogueBoxPrefab);
        else
        {
            GameObject go = new("NPCDialogueBox", typeof(RectTransform), typeof(NPCDialogueBoxUI));
            _activeDialogue = go.GetComponent<NPCDialogueBoxUI>();
        }

        return _activeDialogue;
    }

    public static string BuildQuestOfferTitleHtml(QuestDefinition quest)
    {
        string questName = string.IsNullOrWhiteSpace(quest.displayName) ? "Quest" : quest.displayName.Trim();
        return $"<size=115%><b><color=#FFD66B>{questName}</color></b></size>";
    }

    public static string GetQuestOfferDescriptionPlain(QuestDefinition quest)
    {
        return quest != null && !string.IsNullOrWhiteSpace(quest.description)
            ? quest.description.Trim()
            : "";
    }

    public static string BuildQuestOfferRewardHtml(QuestDefinition quest)
    {
        return $"<size=95%><b><color=#B8E6A1>Reward:</color></b> {FormatQuestRewards(quest)}</size>";
    }

    public static string BuildQuestOfferText(QuestDefinition quest)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BuildQuestOfferTitleHtml(quest));
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(quest.description))
        {
            sb.AppendLine(quest.description.Trim());
            sb.AppendLine();
        }

        sb.Append(BuildQuestOfferRewardHtml(quest));
        return sb.ToString();
    }

    public static string FormatQuestRewards(QuestDefinition quest)
    {
        var parts = new List<string>();

        if (quest.rewardGold > 0)
            parts.Add($"{quest.rewardGold}g");

        if (quest.rewardItem)
            parts.Add(FormatRewardItem(quest.rewardItem.displayName, quest.rewardItemQuantity));
        else if (!string.IsNullOrWhiteSpace(quest.rewardItemId))
            parts.Add(FormatRewardItem(FormatItemIdAsName(quest.rewardItemId), quest.rewardItemQuantity));

        if (quest.additionalItemRewards != null)
        {
            for (int i = 0; i < quest.additionalItemRewards.Count; i++)
            {
                QuestItemReward reward = quest.additionalItemRewards[i];
                if (reward == null)
                    continue;

                string displayName = reward.item
                    ? reward.item.displayName
                    : FormatItemIdAsName(reward.itemId);
                parts.Add(FormatRewardItem(displayName, reward.quantity));
            }
        }

        if (!string.IsNullOrWhiteSpace(quest.rewardNotes))
            parts.Add(quest.rewardNotes.Trim());

        if (quest.grantIdleCombatUnlockOnRewardClaim)
            parts.Add("Unlocks Auto Battle");

        return parts.Count > 0 ? string.Join(" / ", parts) : "-";
    }

    private static string FormatRewardItem(string displayName, int quantity)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "";
        return $"{displayName.Trim()} x{Mathf.Max(1, quantity)}";
    }

    private static string FormatItemIdAsName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "";

        string[] parts = itemId.Trim().Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
                continue;
            parts[i] = char.ToUpperInvariant(parts[i][0]) +
                       (parts[i].Length > 1 ? parts[i][1..].ToLowerInvariant() : "");
        }

        return string.Join(" ", parts);
    }
}
