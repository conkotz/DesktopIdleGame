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
        "After the player dies and respawns (scene reload), or when loading a save with this flag still set — " +
        "if the current map matches After Death Respawn Map Node Id when that field is set. Cleared when this dialogue is shown.")]
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
        "For After Death And Respawn only: require this MapNodeDefinition.nodeId (e.g. tutorial_3). Empty = any map.")]
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

    [Header("Additional dialogues (conditional)")]
    [Tooltip("Evaluated top to bottom; later rows override earlier ones when their condition is met. Falls back to Dialogue above.")]
    [SerializeField] private List<NpcConditionalDialogueEntry> additionalConditionalDialogues = new();

    [Header("Quest integration")]
    [SerializeField] private QuestGiver questGiver;

    private NPCDialogueBoxUI _activeDialogue;
    private bool _hasOpenedOnFirstSighting;
    private string _lastAutoPlainDialogueSignature;
    private QuestProgressManager _boundQuestProgress;

    private bool _watchQuestAcceptedBaselineReady;
    private bool _watchQuestAcceptedWasAccepted;

    private Coroutine _deferredQuestAcceptedPlainDialogueRoutine;

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

        _hasOpenedOnFirstSighting = true;
        ShowNormalDialogueOnly(false);
    }

    private bool HasConditionalDialogues() =>
        additionalConditionalDialogues != null && additionalConditionalDialogues.Count > 0;

    private bool NeedsQuestProgressSubscription() =>
        (openDialogueOnFirstSighting && HasConditionalDialogues()) ||
        (openDialogueAfterQuestAccepted && !string.IsNullOrWhiteSpace(openDialogueAfterQuestAcceptedQuestId));

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

        List<QuestDefinition> quests =
            questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>();

        if (quests.Count == 0 && string.IsNullOrWhiteSpace(GetResolvedDialogueText()))
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

        TryGetResolvedPlainDialogue(out string text, out bool showAccept, out Action onAccept, out NpcConditionalDialogueEntry winning);
        float autoClose = showAccept ? 0f : nonQuestAutoCloseSeconds;
        box.ShowAt(transform, transform, Vector3.zero, text, showAccept, onAccept, autoClose);
        RememberAutoPlainDialogueSignatureIfNeeded(text, showAccept);
        NotifyPresentedDeathRespawnDialogueIfNeeded(winning);
    }

    private void ShowNormalDialogueOnly(bool replaceExistingThisNpcDialogue)
    {
        TryGetResolvedPlainDialogue(out string text, out bool showAccept, out Action onAccept, out NpcConditionalDialogueEntry winning);
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
        RememberAutoPlainDialogueSignatureIfNeeded(text, showAccept);
        NotifyPresentedDeathRespawnDialogueIfNeeded(winning);
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
        if (!openDialogueOnFirstSighting)
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

        if (!openDialogueOnFirstSighting || !_hasOpenedOnFirstSighting)
            return;
        if (additionalConditionalDialogues == null || additionalConditionalDialogues.Count == 0)
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

    private string GetResolvedDialogueText()
    {
        TryGetResolvedPlainDialogue(out string resolved, out _, out _, out _);
        return resolved;
    }

    private void TryGetResolvedPlainDialogue(
        out string text,
        out bool showAccept,
        out Action onAccept,
        out NpcConditionalDialogueEntry winningEntry)
    {
        text = dialogue != null ? dialogue.Trim() : "";
        showAccept = false;
        onAccept = null;
        winningEntry = null;

        QuestProgressManager mgr = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);

        if (additionalConditionalDialogues == null || additionalConditionalDialogues.Count == 0)
            return;

        for (int i = 0; i < additionalConditionalDialogues.Count; i++)
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
                string cur = ResolveActiveMapNodeIdForNpcConditions();
                return !string.IsNullOrEmpty(cur) &&
                       string.Equals(cur, need, StringComparison.Ordinal);
            default:
                return false;
        }
    }

    private static string ResolveActiveMapNodeIdForNpcConditions()
    {
        if (ActiveLevelContext.Current != null && !string.IsNullOrWhiteSpace(ActiveLevelContext.Current.nodeId))
            return ActiveLevelContext.Current.nodeId.Trim();

        GameplayLevelBootstrapper boot = GameplayLevelBootstrapper.Instance;
        if (boot != null && boot.ActiveDefinition != null && !string.IsNullOrWhiteSpace(boot.ActiveDefinition.nodeId))
            return boot.ActiveDefinition.nodeId.Trim();

        return "";
    }

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
