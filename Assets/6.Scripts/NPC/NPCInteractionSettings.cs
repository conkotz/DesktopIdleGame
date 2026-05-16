using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("Removes all of the configured item id from player inventory and grants gold per item.")]
    BuyAllItems = 2,
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

    [Tooltip("For Buy All Items: ItemDefinition.itemId to purchase from player inventory.")]
    public string buyAllItemId = "";

    [Min(1)]
    [Tooltip("For Buy All Items: gold paid per item removed from inventory.")]
    public int buyAllGoldPerItem = 50;
}

public class NPCInteractionSettings : MonoBehaviour
{
    private const string GameplaySceneName = "GamePlay";

    [Header("Dialogue")]
    [TextArea(2, 6)]
    [SerializeField] private string dialogue = "howdy";
    [SerializeField] private NPCDialogueBoxUI dialogueBoxPrefab;
    [Tooltip("When enabled, this NPC uses a custom dialogue follow offset instead of the Dialogue Box default offset.")]
    [SerializeField] private bool useDialogueLocalOffsetOverride = false;
    [Tooltip("Per-NPC override for dialogue follow offset from collider top-right.")]
    [ShowWhenTrue(nameof(useDialogueLocalOffsetOverride))]
    [FormerlySerializedAs("dialogueLocalOffset")]
    [SerializeField] private Vector3 dialogueLocalOffsetOverride = new(1.95f, 0.5f, 0f);
    [Tooltip(
        "Optional child Transform (e.g. empty at speech bubble corner). When set, the bottom-left corner of the dialogue box is placed at this world point (strip + world-pin paths). Stays aligned with the sprite when zooming; no offset required.")]
    [SerializeField] private Transform dialogueFollowWorldAnchor;
    [Tooltip(
        "Optional extra nudge after viewport projection (0–1 per axis). Usually leave at zero when using Dialogue Follow World Anchor.")]
    [SerializeField] private Vector2 dialogueFollowViewportOffset = Vector2.zero;
    [SerializeField] private float nonQuestAutoCloseSeconds = 5f;
    [Tooltip("When enabled, shows dialogue the first time this NPC is on-screen, then re-opens automatically when conditional dialogue changes (e.g. after a quest completes).")]
    [SerializeField] private bool openDialogueOnFirstSighting;

    [Header("Click-to-walk arrival (dialogue + quest box)")]
    [Tooltip(
        "When on, clicking this NPC moves the player to it first (same as Merchant Click). " +
        "Dialogue and quest offers only appear once the player is within Open When Within X Distance of the NPC's collider.")]
    [SerializeField] private bool walkPlayerToNpcOnClick = true;

    [Tooltip("Extra padding added on top of the NPC collider half-width when checking arrival on click (mirrors MerchantClick).")]
    [ShowWhenTrue(nameof(walkPlayerToNpcOnClick))]
    [SerializeField, Min(0f)] private float openWhenWithinXDistance = 0.15f;

    [Header("Auto-reopen proximity gate (does NOT affect first sighting)")]
    [Tooltip(
        "When on, dialogues that auto-open due to quest progress changes or after-quest-accepted require the player to be within Auto Reopen Range Units before they appear. " +
        "First-sighting auto-open is unaffected.")]
    [SerializeField] private bool requirePlayerProximityForAutoReopen = true;

    [Tooltip("Maximum 2D distance (world units) from this NPC at which auto-reopen dialogues will appear. Used only when Require Player Proximity For Auto Reopen is on.")]
    [ShowWhenTrue(nameof(requirePlayerProximityForAutoReopen))]
    [SerializeField, Min(0.1f)] private float autoReopenRangeUnits = 2.5f;

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

    [Header("Debug")]
    [Tooltip("Logs one line when a click would open no plain dialogue (pending / one-way / chain index).")]
    [SerializeField] private bool debugLogPlainDialogueResolution;

    private NPCDialogueBoxUI _activeDialogue;
    private bool _hasOpenedOnFirstSighting;
    private string _lastAutoPlainDialogueSignature = "";
    private QuestProgressManager _boundQuestProgress;

    /// <summary>True when a quest-driven auto-reopen wanted to fire but the player was out of range; Update polls until proximity is satisfied.</summary>
    private bool _pendingProximityAutoReopen;
    private static PlayerController s_cachedPlayerForProximity;

    private Coroutine _interactWhenArrivedRoutine;
    private static NPCInteractionSettings _pendingInteract;

    private bool _watchQuestAcceptedBaselineReady;
    private bool _watchQuestAcceptedWasAccepted;

    private Coroutine _deferredQuestAcceptedPlainDialogueRoutine;

    /// <summary>While set, <see cref="HandleQuestProgressChangedForAutoDialogue"/> skips — <see cref="InteractNow"/> presents lines after claim on the same click.</summary>
    private bool _presentDialogueAfterClaimOnThisInteract;

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

        if (_pendingInteract == this)
            CancelPendingInteract();
        else if (_interactWhenArrivedRoutine != null)
        {
            StopCoroutine(_interactWhenArrivedRoutine);
            _interactWhenArrivedRoutine = null;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (NeedsQuestProgressSubscription() && _boundQuestProgress == null)
            TrySubscribeQuestProgressForAutoDialogue();

        // First-sighting path is unaffected by the proximity gate (per design).
        if (openDialogueOnFirstSighting && !_hasOpenedOnFirstSighting)
        {
            Camera cam = Camera.main;
            if (cam && IsVisibleInCameraViewport(cam))
            {
                // Do not set _hasOpenedOnFirstSighting here — only after a successful ShowAt inside ShowNormalDialogueOnly,
                // otherwise one empty resolve (e.g. save / death-pending not hydrated yet) permanently skips auto dialogue.
                ShowNormalDialogueOnly(false);
            }
        }

        // Quest-driven auto-reopen that was deferred because the player was too far: retry once they enter range.
        if (_pendingProximityAutoReopen && IsPlayerWithinAutoReopenRange())
            TryFlushPendingProximityAutoReopen();
    }

    private void TryFlushPendingProximityAutoReopen()
    {
        _pendingProximityAutoReopen = false;

        TryGetResolvedPlainDialogue(
            out string text,
            out bool showAccept,
            out _,
            out _,
            out _,
            allowBaseWhenOneWayHasNoMatchingConditional: false);
        if (string.IsNullOrWhiteSpace(text))
            return;

        string sig = BuildPlainDialogueSignature(text, showAccept);
        if (sig == _lastAutoPlainDialogueSignature)
            return;

        ShowNormalDialogueOnly(true);
    }

    /// <summary>True when the proximity gate is satisfied (or disabled). Cached player ref re-resolves on demand.</summary>
    private bool IsPlayerWithinAutoReopenRange()
    {
        if (!requirePlayerProximityForAutoReopen)
            return true;

        if (s_cachedPlayerForProximity == null)
            s_cachedPlayerForProximity = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (s_cachedPlayerForProximity == null)
            return false;

        Vector3 npcPos = transform.position;
        Collider2D col = ResolveInteractCollider2D();
        if (col != null)
            npcPos = col.bounds.center;

        Vector3 playerPos = s_cachedPlayerForProximity.transform.position;
        float dx = playerPos.x - npcPos.x;
        float dy = playerPos.y - npcPos.y;
        float radius = Mathf.Max(0.1f, autoReopenRangeUnits);
        return dx * dx + dy * dy <= radius * radius;
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
    /// After any conditional row has been shown, never evaluate earlier rows again. Later rows can still win.
    /// When one-way base is consumed and no row matches, dialogue stays empty (no base fallback).
    /// </summary>
    private int GetHighestOneWayConditionalPresented()
    {
        if (!oneWayDialogueQueue)
            return -1;

        if (!string.IsNullOrWhiteSpace(oneWayDialogueQueueSaveId))
            return NpcOneWayDialogueQueueStore.GetHighestConditionalIndexPresented(oneWayDialogueQueueSaveId.Trim());

        if (!_sessionOneWayConditionalConsumed)
            return -1;

        return _sessionHighestOneWayConditionalPresented;
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

    /// <param name="winningConditionalIndex">Index in <see cref="additionalConditionalDialogues"/> when <paramref name="winning"/> came from that loop; otherwise -1.</param>
    private void NotifyOneWayConditionalPresented(NpcConditionalDialogueEntry winning, int winningConditionalIndex)
    {
        if (winning == null || winningConditionalIndex < 0)
            return;
        RecordOneWayConditionalPresented(winningConditionalIndex);
    }

    /// <summary>First conditional index to evaluate (skips rows already superseded by the one-way chain).</summary>
    private int GetOneWayChainMinimumConditionalIndexForEvaluation()
    {
        if (additionalConditionalDialogues == null || additionalConditionalDialogues.Count == 0)
            return 0;
        if (!oneWayDialogueQueue || !IsOneWayBaseDialogueConsumed())
            return 0;

        // While death/respawn dialogue is still pending, the After Death row must stay in the evaluation window even
        // if chain progress was written by an older build or the box was dismissed before pending was cleared correctly.
        // Skip that reset when a turn-in is ready — claim runs first and advances the queue to the next row.
        if (NpcPostDeathRespawnDialogueStore.IsPending &&
            HasAfterDeathConditionalEntry() &&
            !HasClaimableQuestAtGiver())
            return 0;

        int highest = GetHighestOneWayConditionalPresented();
        if (highest < 0)
            return 0;

        int count = additionalConditionalDialogues.Count;
        // Next row after the furthest-shown index — but when that index is already the last row (highest == count - 1),
        // highest + 1 == count and the for-loop never runs, stranding e.g. "Accept → teleport" after closing the box.
        int start = highest + 1;
        if (start >= count)
            start = Mathf.Max(0, count - 1);

        return start;
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

    /// <summary>
    /// True while this NPC's dialogue/quest box is open, or this object's merchant shop is open.
    /// Used by <see cref="FacePlayerSpriteFlip"/> in <see cref="FacePlayerMode.WhenEngaged"/> mode.
    /// </summary>
    public bool IsEngagedWithPlayer()
    {
        if (NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform))
            return true;

        MerchantClick merchant = GetComponent<MerchantClick>();
        if (!merchant)
            merchant = GetComponentInChildren<MerchantClick>(true);

        return merchant != null && merchant.IsShopEngagedWithPlayer();
    }

    public void Interact()
    {
        if (!walkPlayerToNpcOnClick)
        {
            InteractNow();
            return;
        }

        PlayerController player = ResolveCachedPlayer();
        if (player == null || player.IsDead)
        {
            InteractNow();
            return;
        }

        if (IsPlayerWithinNpcArrivalRange(player))
        {
            InteractNow();
            return;
        }

        // Walk to the NPC's nearest collider edge (offset back by the player's own half-width + padding) instead of the
        // NPC's transform.position.x — otherwise the player walks through the NPC sprite before the arrival check fires
        // because MoveToPointX would keep targeting the NPC center.
        CancelPendingInteract();
        _pendingInteract = this;
        player.MoveToPointX(ComputeApproachTargetX(player));
        _interactWhenArrivedRoutine = StartCoroutine(CoInteractWhenArrived(player));
    }

    /// <summary>
    /// World-X the player should walk to so they stop at the nearest edge of the NPC's collider (not its center).
    /// Side is chosen by the player's current X relative to the NPC center, then offset outward by player half-width + <see cref="openWhenWithinXDistance"/>.
    /// Falls back to <see cref="Transform.position"/> when no interact collider can be resolved.
    /// </summary>
    private float ComputeApproachTargetX(PlayerController player)
    {
        Collider2D npcCol = ResolveInteractCollider2D();
        if (npcCol == null || player == null)
            return transform.position.x;

        Bounds b = npcCol.bounds;
        float playerX = player.transform.position.x;
        bool approachFromLeft = playerX <= b.center.x;
        float edgeX = approachFromLeft ? b.min.x : b.max.x;
        float sign = approachFromLeft ? -1f : 1f;

        // Walk the player's near edge right up to the NPC's near edge (no `openWhenWithinXDistance` gap baked in).
        // The arrival check below still treats openWhenWithinXDistance as the tolerance — keeping it out of the walk
        // target means the player can't undershoot past it (MoveToPointX has its own clickArriveThreshold snap),
        // which previously left the player parked a few px short of the collider so the dialogue never opened.
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(player);
        return edgeX + sign * playerHalfWidth;
    }

    private static float ResolvePlayerColliderHalfWidth(PlayerController player)
    {
        if (player == null)
            return 0f;
        Collider2D pcol = player.GetComponent<Collider2D>();
        if (!pcol)
            pcol = player.GetComponentInChildren<Collider2D>(true);
        return pcol != null ? pcol.bounds.extents.x : 0f;
    }

    private IEnumerator CoInteractWhenArrived(PlayerController player)
    {
        while (_pendingInteract == this)
        {
            if (player == null || player.IsDead)
            {
                _interactWhenArrivedRoutine = null;
                if (_pendingInteract == this)
                    _pendingInteract = null;
                yield break;
            }

            if (IsPlayerWithinNpcArrivalRange(player))
            {
                _interactWhenArrivedRoutine = null;
                if (_pendingInteract == this)
                    _pendingInteract = null;
                InteractNow();
                yield break;
            }

            yield return null;
        }

        _interactWhenArrivedRoutine = null;
    }

    private bool IsPlayerWithinNpcArrivalRange(PlayerController player)
    {
        if (player == null)
            return false;

        // Arrived = player's nearest body edge is within `openWhenWithinXDistance` of the NPC's nearest collider edge.
        // Measured edge-to-edge (not center-to-center) so the player no longer needs to overlap the NPC for the dialogue to open,
        // matching the approach point used by walk-to logic above.
        Collider2D col = ResolveInteractCollider2D();
        if (col == null)
        {
            float dxCenter = Mathf.Abs(player.transform.position.x - transform.position.x);
            return dxCenter <= Mathf.Max(0.01f, openWhenWithinXDistance);
        }

        Bounds b = col.bounds;
        float playerX = player.transform.position.x;
        float edgeX = playerX <= b.center.x ? b.min.x : b.max.x;
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(player);
        float gapBetweenBodies = Mathf.Abs(playerX - edgeX) - playerHalfWidth;
        return gapBetweenBodies <= Mathf.Max(0.01f, openWhenWithinXDistance);
    }

    private static PlayerController ResolveCachedPlayer()
    {
        if (s_cachedPlayerForProximity == null)
            s_cachedPlayerForProximity = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        return s_cachedPlayerForProximity;
    }

    /// <summary>Cancel a pending walk-then-interact (e.g. when the player clicks elsewhere). Mirrors <see cref="MerchantClick.CancelPendingOpen"/>.</summary>
    public static void CancelPendingInteract()
    {
        if (_pendingInteract == null)
            return;

        if (_pendingInteract._interactWhenArrivedRoutine != null)
        {
            _pendingInteract.StopCoroutine(_pendingInteract._interactWhenArrivedRoutine);
            _pendingInteract._interactWhenArrivedRoutine = null;
        }

        _pendingInteract = null;
    }

    private void InteractNow()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();

        // Player has arrived (or walk was disabled): drop any pending proximity-gated auto-reopen.
        _pendingProximityAutoReopen = false;

        InvokeInteractionEffects();

        bool dialogueAlreadyOpenForThisNpc = NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform);

        _presentDialogueAfterClaimOnThisInteract = true;
        try
        {
            InteractNowAfterClaimGate(dialogueAlreadyOpenForThisNpc);
        }
        finally
        {
            _presentDialogueAfterClaimOnThisInteract = false;
        }
    }

    private void InteractNowAfterClaimGate(bool dialogueAlreadyOpenForThisNpc)
    {
        bool claimedRewardThisClick = false;
        if (TryClaimReadyQuestRewardBeforeDialogue(dialogueAlreadyOpenForThisNpc, out bool claimed))
        {
            claimedRewardThisClick = claimed;
            if (IsQuestClaimStartingMapTravel())
                return;
        }

        List<QuestDefinition> quests =
            questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>();

        NPCDialogueBoxUI.TryDismissStaleMultiOfferSpreadForZeroQuests(transform, quests.Count);

        TryGetResolvedPlainDialogue(
            out string interactResolved,
            out _,
            out _,
            out _,
            out _,
            allowBaseWhenOneWayHasNoMatchingConditional: true);
        EnsurePlainDialogueNeverEmpty(ref interactResolved);
        if (quests.Count == 0 && string.IsNullOrWhiteSpace(interactResolved))
        {
            MaybeLogPlainDialogueTapDiagnostics(quests.Count, "empty plain resolve (early exit)");
            if (_activeDialogue && !claimedRewardThisClick)
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
            NPCDialogueBoxUI.TryGetPlainDialogueHostForNpc(transform, out NPCDialogueBoxUI plainHost))
        {
            plainHost.StackQuestOffersBesidePlainDialogue(
                quests,
                () => questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>(),
                q => questGiver != null && questGiver.TryAcceptQuest(q),
                autoCloseSeconds: 0f);
            return;
        }

        if (dialogueAlreadyOpenForThisNpc)
        {
            if (!claimedRewardThisClick &&
                questGiver != null &&
                questGiver.TryClaimFirstReadyQuestReward())
            {
                claimedRewardThisClick = true;
                if (IsQuestClaimStartingMapTravel())
                    return;
            }

            if (!claimedRewardThisClick)
            {
                MaybeLogPlainDialogueTapDiagnostics(quests.Count, "blocked: ActiveDialogueIsDescendantOf(this NPC)");
                return;
            }

            if (_activeDialogue)
                _activeDialogue.Hide(suppressPlainDismissCallback: true);
            dialogueAlreadyOpenForThisNpc = false;
            quests = questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>();
        }

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
            out int winningConditionalIndex,
            allowBaseWhenOneWayHasNoMatchingConditional: true);
        EnsurePlainDialogueNeverEmpty(ref text);
        float autoClose = ResolvePlainAutoCloseSeconds(showAccept, winning);
        box.ShowAt(transform, transform, Vector3.zero, text, showAccept, onAccept, autoClose);
        RememberAutoPlainDialogueSignatureIfNeeded(text, showAccept);
        ApplyPlainDialoguePresentedSideEffects(box, winning, winningConditionalIndex);
    }

    private void InvokeInteractionEffects()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is INPCInteractionEffect effect)
                effect.OnNpcInteract(this);
        }
    }

    private void ShowNormalDialogueOnly(bool replaceExistingThisNpcDialogue)
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();

        bool dialogueOpen = NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform);
        _presentDialogueAfterClaimOnThisInteract = true;
        try
        {
            if (TryClaimReadyQuestRewardBeforeDialogue(dialogueOpen, out bool claimed) && claimed)
            {
                if (IsQuestClaimStartingMapTravel())
                    return;
                replaceExistingThisNpcDialogue = true;
            }

            ShowNormalDialogueOnlyCore(replaceExistingThisNpcDialogue);
        }
        finally
        {
            _presentDialogueAfterClaimOnThisInteract = false;
        }
    }

    private void ShowNormalDialogueOnlyCore(bool replaceExistingThisNpcDialogue)
    {
        TryGetResolvedPlainDialogue(
            out string text,
            out bool showAccept,
            out Action onAccept,
            out NpcConditionalDialogueEntry winning,
            out int winningConditionalIndex,
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

        float autoClose = ResolvePlainAutoCloseSeconds(showAccept, winning);
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
        _pendingProximityAutoReopen = false;
        RememberAutoPlainDialogueSignatureIfNeeded(text, showAccept);
        ApplyPlainDialoguePresentedSideEffects(box, winning, winningConditionalIndex);
    }

    /// <summary>Auto-close delay for plain lines; after-death rows stay open until dismissed when post-death is pending.</summary>
    private float ResolvePlainAutoCloseSeconds(bool showAccept, NpcConditionalDialogueEntry winning)
    {
        if (showAccept)
            return 0f;
        if (winning != null &&
            winning.condition == NpcDialogueConditionKind.AfterDeathAndRespawn &&
            NpcPostDeathRespawnDialogueStore.IsPending)
            return 0f;
        return nonQuestAutoCloseSeconds;
    }

    private void ApplyPlainDialoguePresentedSideEffects(
        NPCDialogueBoxUI box,
        NpcConditionalDialogueEntry winning,
        int winningConditionalIndex)
    {
        box.SetPlainDialogueHideOnceCallback(null);
        MaybeMarkOneWayBaseDialogueConsumed(winning);

        if (winning != null &&
            winning.condition == NpcDialogueConditionKind.AfterDeathAndRespawn &&
            NpcPostDeathRespawnDialogueStore.IsPending)
        {
            box.SetPlainDialogueHideOnceCallback(() =>
            {
                NpcPostDeathRespawnDialogueStore.ClearPendingAndSave();
                NotifyOneWayConditionalPresented(winning, winningConditionalIndex);
            });
            return;
        }

        NotifyPresentedDeathRespawnDialogueIfNeeded(winning);
        NotifyOneWayConditionalPresented(winning, winningConditionalIndex);
    }

    private void NotifyPresentedDeathRespawnDialogueIfNeeded(NpcConditionalDialogueEntry winning)
    {
        if (!NpcPostDeathRespawnDialogueStore.IsPending || winning == null)
            return;

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

    private static bool IsQuestClaimStartingMapTravel()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!player)
            return false;

        PlayerLevelTransition transition = player.GetComponent<PlayerLevelTransition>();
        return transition != null && transition.PendingScaleRestore;
    }

    private bool HasClaimableQuestAtGiver()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();
        return questGiver != null && questGiver.GetFirstClaimableQuestAtLocation() != null;
    }

    /// <summary>
    /// When a turn-in is ready, claim before any plain dialogue (walk-to-arrive, first sighting, or click).
    /// Post-death lines are only prioritized when there is nothing to claim.
    /// </summary>
    private bool ShouldDeferRewardClaimForPostDeathDialogue(bool dialogueAlreadyOpenForThisNpc)
    {
        if (dialogueAlreadyOpenForThisNpc || HasClaimableQuestAtGiver())
            return false;

        return NpcPostDeathRespawnDialogueStore.IsPending && HasAfterDeathConditionalEntry();
    }

    private bool TryClaimReadyQuestRewardBeforeDialogue(bool dialogueAlreadyOpenForThisNpc, out bool claimed)
    {
        claimed = false;
        if (ShouldDeferRewardClaimForPostDeathDialogue(dialogueAlreadyOpenForThisNpc))
            return false;

        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();
        if (questGiver == null || !questGiver.TryClaimFirstReadyQuestReward())
            return false;

        claimed = true;
        SupersedePostDeathDialogueAfterQuestTurnIn();
        return true;
    }

    /// <summary>
    /// After a turn-in at this NPC, do not replay the after-death row — advance to the next one-way conditional.
    /// </summary>
    private void SupersedePostDeathDialogueAfterQuestTurnIn()
    {
        if (!HasAfterDeathConditionalEntry() || !NpcPostDeathRespawnDialogueStore.IsPending)
            return;

        NpcPostDeathRespawnDialogueStore.ClearPendingAndSave();
    }

    private void MaybeLogPlainDialogueTapDiagnostics(int questOfferCount, string reason)
    {
        if (!debugLogPlainDialogueResolution)
            return;

        int hi = GetHighestOneWayConditionalPresented();
        int minIdx = GetOneWayChainMinimumConditionalIndexForEvaluation();
        Debug.Log(
            $"[NPCInteraction '{name}' ({gameObject.name})] {reason}\n" +
            $"  postDeathPending={NpcPostDeathRespawnDialogueStore.IsPending} deathNodeId='{NpcPostDeathRespawnDialogueStore.DeathOccurredOnMapNodeId}' " +
            $"curMapId='{NpcPostDeathRespawnDialogueStore.ResolveCurrentGameplayMapNodeId()}'\n" +
            $"  oneWay={oneWayDialogueQueue} baseConsumed={IsOneWayBaseDialogueConsumed()} chainHigh={hi} evalMin={minIdx} " +
            $"saveId='{oneWayDialogueQueueSaveId}'\n" +
            $"  questOffers={questOfferCount} hasAfterDeathRow={HasAfterDeathConditionalEntry()}",
            this);
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

        if (_presentDialogueAfterClaimOnThisInteract)
            return;

        HandleAfterQuestAcceptedDialogue();

        if (!ShouldReactToQuestProgressWithConditionalDialogue())
            return;

        TryGetResolvedPlainDialogue(
            out string text,
            out bool showAccept,
            out Action onAccept,
            out _,
            out _,
            allowBaseWhenOneWayHasNoMatchingConditional: false);
        if (string.IsNullOrWhiteSpace(text))
            return;

        string sig = BuildPlainDialogueSignature(text, showAccept);
        if (sig == _lastAutoPlainDialogueSignature)
            return;

        if (!IsPlayerWithinAutoReopenRange())
        {
            // Defer until the player walks up to this NPC; Update will retry each frame.
            _pendingProximityAutoReopen = true;
            return;
        }

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

        if (!IsPlayerWithinAutoReopenRange())
        {
            // The accepted quest could be sourced from a different NPC; wait until the player reaches *this* NPC.
            _pendingProximityAutoReopen = true;
            yield break;
        }

        ShowNormalDialogueOnly(true);
    }

    /// <param name="allowBaseWhenOneWayHasNoMatchingConditional">
    /// When true (manual <see cref="Interact"/> only): if non-one-way base was suppressed but no conditional matches,
    /// fall back to the base Dialogue field. Ignored when <see cref="oneWayDialogueQueue"/> is on (base never returns).
    /// </param>
    /// <param name="winningConditionalIndex">0-based index of <paramref name="winningEntry"/> in <see cref="additionalConditionalDialogues"/>; -1 if resolution used base dialogue or none.</param>
    private void TryGetResolvedPlainDialogue(
        out string text,
        out bool showAccept,
        out Action onAccept,
        out NpcConditionalDialogueEntry winningEntry,
        out int winningConditionalIndex,
        bool allowBaseWhenOneWayHasNoMatchingConditional = false)
    {
        bool skipBase = IsOneWayBaseDialogueConsumed();
        text = skipBase ? "" : (dialogue != null ? dialogue.Trim() : "");
        showAccept = false;
        onAccept = null;
        winningEntry = null;
        winningConditionalIndex = -1;

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
        bool skipAfterDeathWhileClaimReady = HasClaimableQuestAtGiver();
        for (int i = minIndex; i < additionalConditionalDialogues.Count; i++)
        {
            NpcConditionalDialogueEntry e = additionalConditionalDialogues[i];
            if (e == null || string.IsNullOrWhiteSpace(e.dialogue))
                continue;
            if (skipAfterDeathWhileClaimReady &&
                e.condition == NpcDialogueConditionKind.AfterDeathAndRespawn)
                continue;
            if (!EvaluateConditionalEntry(e, mgr))
                continue;

            text = e.dialogue.Trim();
            Action built = BuildAcceptActionOrNull(e);
            showAccept = built != null;
            onAccept = built;
            winningEntry = e;
            winningConditionalIndex = i;
            if (oneWayDialogueQueue)
                break;
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
        // One-way mode after a conditional has been shown: do not resurrect base dialogue.
        // Manual click fallback should replay the last shown one-way conditional line if possible.
        if (oneWayDialogueQueue)
        {
            int replayIdx = GetHighestOneWayConditionalPresented();
            if (additionalConditionalDialogues != null &&
                replayIdx >= 0 &&
                replayIdx < additionalConditionalDialogues.Count)
            {
                NpcConditionalDialogueEntry replay = additionalConditionalDialogues[replayIdx];
                if (replay != null && !string.IsNullOrWhiteSpace(replay.dialogue))
                    text = replay.dialogue.Trim();
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(dialogue))
            return;
        text = dialogue.Trim();
    }

    /// <summary>Last resort when base <see cref="dialogue"/> exists but resolution returned empty (interact + auto-open).</summary>
    private void EnsurePlainDialogueNeverEmpty(ref string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            return;
        if (oneWayDialogueQueue && IsOneWayBaseDialogueConsumed())
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
            case NpcDialogueOutcomeKind.BuyAllItems:
            {
                string itemId = string.IsNullOrWhiteSpace(e.buyAllItemId) ? "" : e.buyAllItemId.Trim();
                int goldPerItem = Mathf.Max(1, e.buyAllGoldPerItem);
                if (string.IsNullOrWhiteSpace(itemId))
                    return null;
                return () => BuyAllItemsFromPlayer(itemId, goldPerItem);
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

        MapTravelSession.BeginTravel(node, MapTravelSession.EntryMethod.InWorldEntrance, logPendingLevel: false);
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

    private static Inventory ResolvePlayerInventoryForNpcConditions()
    {
        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv != null)
            return inv;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer)
            return taggedPlayer.GetComponentInChildren<Inventory>(true);

        return null;
    }

    private static CurrencyWallet ResolveCurrencyWalletForNpcConditions() =>
        FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

    private static void BuyAllItemsFromPlayer(string itemId, int goldPerItem)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        Inventory inv = ResolvePlayerInventoryForNpcConditions();
        CurrencyWallet wallet = ResolveCurrencyWalletForNpcConditions();
        if (inv == null || wallet == null)
            return;

        int itemCount = inv.GetTotalAmount(itemId);
        if (itemCount <= 0)
            return;

        if (!inv.Remove(itemId, itemCount))
            return;

        int goldEarned = itemCount * Mathf.Max(1, goldPerItem);
        wallet.AddGold(goldEarned);

        ItemDefinition def = inv.GetItemDef(itemId);
        string itemName = def && !string.IsNullOrWhiteSpace(def.displayName)
            ? def.displayName.Trim()
            : FormatItemIdAsName(itemId);
        GameLog.SoldItem(itemName, itemCount, goldEarned);
    }

    private static string ResolveActiveMapNodeIdForNpcConditions() =>
        NpcPostDeathRespawnDialogueStore.ResolveCurrentGameplayMapNodeId();

    /// <summary>World point for dialogue follow each frame — uses collider bounds + resolved dialogue offset so NPC hover scale cannot drift the pivot.</summary>
    public Vector3 GetDialogueFollowWorldPoint()
    {
        if (dialogueFollowWorldAnchor != null)
        {
            if (useDialogueLocalOffsetOverride)
                return dialogueFollowWorldAnchor.position +
                       dialogueFollowWorldAnchor.TransformVector(dialogueLocalOffsetOverride);
            return dialogueFollowWorldAnchor.position;
        }

        Collider2D col = ResolveInteractCollider2D();
        Vector3 pivot = ResolveDialogueLocalOffset();
        if (!col)
            return transform.position + pivot;

        Bounds b = col.bounds;
        return new Vector3(b.max.x + pivot.x, b.max.y + pivot.y, transform.position.z + pivot.z);
    }

    /// <summary>Viewport-space nudge applied in <see cref="NPCDialogueBoxUI"/> after <see cref="Camera.WorldToViewportPoint"/> — stable across ortho zoom.</summary>
    public Vector2 GetDialogueFollowViewportOffset() => dialogueFollowViewportOffset;

    /// <summary>When set, <see cref="NPCDialogueBoxUI"/> uses pivot bottom-left so the box attaches at the follow point.</summary>
    public bool HasDialogueFollowWorldAnchor => dialogueFollowWorldAnchor;

    private Vector3 ResolveDialogueLocalOffset()
    {
        if (useDialogueLocalOffsetOverride)
            return dialogueLocalOffsetOverride;
        if (_activeDialogue != null)
            return _activeDialogue.GetNpcDialogueLocalOffset();
        if (dialogueBoxPrefab != null)
            return dialogueBoxPrefab.GetNpcDialogueLocalOffset();
        return NPCDialogueBoxUI.GetDefaultNpcDialogueLocalOffset();
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
        return $"<size=100%><b><color=#FFD66B>{questName}</color></b></size>";
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
        if (quest.grantAdditionalInventorySlotsOnRewardClaim > 0)
            parts.Add($"+{quest.grantAdditionalInventorySlotsOnRewardClaim} Inventory Slots");

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
