using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestListRowUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private CanvasGroup rowCanvasGroup;

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
        if (!selectedHighlight)
        {
            Transform t = transform.Find("SelectedBorder");
            if (t) selectedHighlight = t.gameObject;
        }

        if (!rowCanvasGroup)
            rowCanvasGroup = GetComponent<CanvasGroup>();
    }

    public void Bind(
        QuestDefinition quest,
        string subtitle,
        string statusLine,
        bool selected,
        bool completed,
        float completedAlpha,
        Action<QuestDefinition> onClicked)
    {
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
            rowCanvasGroup.alpha = completed ? Mathf.Clamp01(completedAlpha) : 1f;

        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (quest != null && onClicked != null)
                button.onClick.AddListener(() => onClicked(quest));
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight)
            selectedHighlight.SetActive(selected);
    }
}
