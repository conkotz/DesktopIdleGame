using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestListRowUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Button trackQuestButton;
    [SerializeField] private Button abandonQuestButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text trackQuestButtonLabel;
    [SerializeField] private TMP_Text abandonQuestButtonLabel;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private CanvasGroup rowCanvasGroup;

    public QuestDefinition BoundQuest { get; private set; }

    private void Awake()
    {
        if (!button)
            button = GetComponent<Button>();
        if (!titleText)
            titleText = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!subtitleText)
            subtitleText = transform.Find("Subtitle")?.GetComponent<TMP_Text>();
        if (!statusText)
            statusText = transform.Find("Status")?.GetComponent<TMP_Text>();
        if (!trackQuestButton)
            trackQuestButton = transform.Find("TrackQuestButton")?.GetComponent<Button>();
        if (!abandonQuestButton)
            abandonQuestButton = transform.Find("TrackAbandonButton")?.GetComponent<Button>();
        if (!trackQuestButtonLabel)
            trackQuestButtonLabel = transform.Find("TrackQuestButton")?.GetComponentInChildren<TMP_Text>(true);
        if (!abandonQuestButtonLabel && abandonQuestButton)
            abandonQuestButtonLabel = abandonQuestButton.GetComponentInChildren<TMP_Text>(true);
        if (!selectedHighlight)
        {
            Transform t = transform.Find("SelectedBorder");
            if (t) selectedHighlight = t.gameObject;
        }

        if (!rowCanvasGroup)
            rowCanvasGroup = GetComponent<CanvasGroup>();

        if (titleText)
            titleText.raycastTarget = false;
        if (subtitleText)
            subtitleText.raycastTarget = false;
        if (statusText)
            statusText.raycastTarget = false;
    }

    /// <param name="rowCanvasAlpha"><see cref="CanvasGroup.alpha"/> for the row (1 = opaque; lower = washed / dimmed).</param>
    public void Bind(
        QuestDefinition quest,
        string subtitle,
        string statusLine,
        bool selected,
        float rowCanvasAlpha,
        Action<QuestDefinition> onClicked,
        bool tracked,
        Action<QuestDefinition> onTrackClicked,
        bool showTrackToggle,
        Action<QuestDefinition> onAbandonClicked,
        bool showAbandonButton)
    {
        BoundQuest = quest;

        if (titleText)
            titleText.text = quest ? quest.displayName : "";

        if (subtitleText)
        {
            bool has = !string.IsNullOrEmpty(subtitle);
            subtitleText.gameObject.SetActive(has);
            subtitleText.text = has ? subtitle : "";
        }

        if (statusText)
        {
            bool has = !string.IsNullOrEmpty(statusLine);
            statusText.gameObject.SetActive(has);
            statusText.text = has ? statusLine : "";
        }

        SetSelected(selected);

        if (rowCanvasGroup)
            rowCanvasGroup.alpha = Mathf.Clamp01(rowCanvasAlpha);

        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (quest != null && onClicked != null)
                button.onClick.AddListener(() => onClicked(quest));
        }

        if (trackQuestButton)
        {
            trackQuestButton.gameObject.SetActive(showTrackToggle);
            trackQuestButton.onClick.RemoveAllListeners();
            if (showTrackToggle && quest != null && onTrackClicked != null)
                trackQuestButton.onClick.AddListener(() => onTrackClicked(quest));
        }

        if (trackQuestButtonLabel && showTrackToggle)
            trackQuestButtonLabel.text = tracked ? "Untrack" : "Track";

        if (abandonQuestButton)
        {
            abandonQuestButton.gameObject.SetActive(showAbandonButton);
            abandonQuestButton.onClick.RemoveAllListeners();
            if (showAbandonButton && quest != null && onAbandonClicked != null)
                abandonQuestButton.onClick.AddListener(() => onAbandonClicked(quest));
        }

        if (abandonQuestButtonLabel && showAbandonButton)
            abandonQuestButtonLabel.text = "Abandon";
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight)
            selectedHighlight.SetActive(selected);
    }
}
