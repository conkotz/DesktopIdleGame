using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class NPCInteractionSettings : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(2, 6)]
    [SerializeField] private string dialogue = "howdy";
    [SerializeField] private NPCDialogueBoxUI dialogueBoxPrefab;
    [Tooltip("World offset from the top-right of this object's Collider2D bounds.")]
    [SerializeField] private Vector3 dialogueLocalOffset = new(1.95f, 0.5f, 0f);
    [SerializeField] private float nonQuestAutoCloseSeconds = 5f;
    [SerializeField] private bool openDialogueOnFirstSighting;

    [Header("Quest integration")]
    [SerializeField] private QuestGiver questGiver;

    private NPCDialogueBoxUI _activeDialogue;
    private bool _hasOpenedOnFirstSighting;

    private void Awake()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();
    }

    private void Update()
    {
        if (!openDialogueOnFirstSighting || _hasOpenedOnFirstSighting || !Application.isPlaying)
            return;

        Camera cam = Camera.main;
        if (!cam || !IsVisibleInCameraViewport(cam))
            return;

        _hasOpenedOnFirstSighting = true;
        Interact();
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

        List<QuestDefinition> quests =
            questGiver ? questGiver.GetAllAvailableQuests() : new List<QuestDefinition>();

        if (quests.Count == 0 && string.IsNullOrWhiteSpace(dialogue))
        {
            if (_activeDialogue)
                _activeDialogue.Hide();
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

        box.ShowAt(transform, transform, Vector3.zero, dialogue, showAccept: false,
            onAccept: null, nonQuestAutoCloseSeconds);
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
