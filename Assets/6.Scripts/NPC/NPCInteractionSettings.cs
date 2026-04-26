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
    [SerializeField] private Vector3 dialogueLocalOffset = new(1.25f, -0.35f, 0f);
    [SerializeField] private float nonQuestAutoCloseSeconds = 5f;

    [Header("Quest integration")]
    [SerializeField] private QuestGiver questGiver;

    private NPCDialogueBoxUI _activeDialogue;
    private Transform _dialogueAnchor;

    private void Awake()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();
    }

    public void Interact()
    {
        if (!questGiver)
            questGiver = GetComponent<QuestGiver>();

        QuestDefinition availableQuest = questGiver ? questGiver.GetFirstAvailableQuest() : null;
        string message = availableQuest
            ? BuildQuestOfferText(availableQuest)
            : dialogue;

        bool showAccept = availableQuest != null;
        NPCDialogueBoxUI box = GetOrCreateDialogueBox();
        if (!box)
            return;

        float autoCloseSeconds = showAccept ? 0f : nonQuestAutoCloseSeconds;
        box.ShowAt(transform, GetDialogueAnchor(), dialogueLocalOffset, message, showAccept, () =>
        {
            if (questGiver && availableQuest)
                questGiver.TryAcceptQuest(availableQuest);
        }, autoCloseSeconds);
    }

    private Transform GetDialogueAnchor()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col)
            col = GetComponentInChildren<Collider2D>();
        if (!col)
            col = GetComponentInParent<Collider2D>();

        if (!col)
            return transform;

        if (!_dialogueAnchor)
        {
            GameObject anchor = new("NPCDialogueColliderAnchor");
            anchor.transform.SetParent(transform, false);
            _dialogueAnchor = anchor.transform;
        }

        Bounds b = col.bounds;
        _dialogueAnchor.position = new Vector3(b.max.x, b.max.y, transform.position.z);
        return _dialogueAnchor;
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

    private static string BuildQuestOfferText(QuestDefinition quest)
    {
        var sb = new StringBuilder();
        string questName = string.IsNullOrWhiteSpace(quest.displayName) ? "Quest" : quest.displayName.Trim();
        sb.AppendLine($"<size=115%><b><color=#FFD66B>{questName}</color></b></size>");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(quest.description))
        {
            sb.AppendLine(quest.description.Trim());
            sb.AppendLine();
        }

        sb.Append("<size=95%><b><color=#B8E6A1>Reward:</color></b> ");
        sb.Append(FormatQuestRewards(quest));
        sb.Append("</size>");
        return sb.ToString();
    }

    private static string FormatQuestRewards(QuestDefinition quest)
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
