using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// First-pass quest giver: attach to an NPC, merchant, noticeboard, or clickable world/UI object.
/// Configure a location id that matches QuestDefinition.obtainLocationId and one or more quest ids.
/// </summary>
public class QuestGiver : MonoBehaviour, IPointerClickHandler
{
    [Header("Quest source")]
    [Tooltip("Must match QuestDefinition.obtainLocationId for quests offered here.")]
    [SerializeField] private string locationId = "";

    [Tooltip("Primary QuestDefinition.questId offered by this object.")]
    [SerializeField] private string questId = "";

    [Tooltip("Optional extra QuestDefinition.questIds offered by this same object.")]
    [SerializeField] private List<string> additionalQuestIds = new();

    [Header("Available quest marker")]
    [Tooltip("Optional prefab to show while a quest is available. If empty, a yellow 3D '!' is created.")]
    [SerializeField] private GameObject exclamationMarkPrefab;
    [SerializeField] private Vector3 exclamationMarkLocalOffset = new(0.2f, 1.5f, 0f);
    [SerializeField] private float generatedExclamationMarkSize = 0.2f;
    [SerializeField] private Color exclamationMarkColor = Color.yellow;

    private GameObject _exclamationMarkInstance;
    private QuestProgressManager _manager;

    private void OnEnable()
    {
        TryBindManager();
        RefreshExclamationMark();
    }

    private void OnDisable()
    {
        if (_manager != null)
        {
            _manager.ProgressChanged -= RefreshExclamationMark;
            _manager = null;
        }
    }

    private void Update()
    {
        if (_manager == null)
        {
            TryBindManager();
            RefreshExclamationMark();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryGrantAvailableQuest();
    }

    private void OnMouseDown()
    {
        TryGrantAvailableQuest();
    }

    public bool TryGrantAvailableQuest()
    {
        TryBindManager();
        if (_manager == null)
            return false;

        QuestDefinition quest = FindFirstAvailableQuest();
        if (!quest)
            return false;

        bool accepted = _manager.TryAcceptQuest(quest, locationId);
        RefreshExclamationMark();
        return accepted;
    }

    private void TryBindManager()
    {
        QuestProgressManager next = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);

        if (next == _manager)
            return;

        if (_manager != null)
            _manager.ProgressChanged -= RefreshExclamationMark;

        _manager = next;
        if (_manager != null)
            _manager.ProgressChanged += RefreshExclamationMark;
    }

    private QuestDefinition FindFirstAvailableQuest()
    {
        if (_manager == null)
            return null;

        QuestDefinition quest = FindAvailableQuestById(questId);
        if (quest)
            return quest;

        if (additionalQuestIds == null)
            return _manager.FindFirstAcceptableQuestAtLocation(locationId);

        for (int i = 0; i < additionalQuestIds.Count; i++)
        {
            quest = FindAvailableQuestById(additionalQuestIds[i]);
            if (quest)
                return quest;
        }

        return _manager.FindFirstAcceptableQuestAtLocation(locationId);
    }

    private QuestDefinition FindAvailableQuestById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _manager == null)
            return null;

        QuestDefinition quest = _manager.GetQuestDefinition(id.Trim());
        if (!quest || !_manager.CanAcceptQuest(quest, locationId))
            return null;

        return quest;
    }

    private void RefreshExclamationMark()
    {
        bool show = FindFirstAvailableQuest() != null;
        EnsureExclamationMarkInstance();
        if (_exclamationMarkInstance)
            _exclamationMarkInstance.SetActive(show);
    }

    private void EnsureExclamationMarkInstance()
    {
        if (_exclamationMarkInstance)
            return;

        if (exclamationMarkPrefab)
        {
            _exclamationMarkInstance = Instantiate(exclamationMarkPrefab, transform);
            _exclamationMarkInstance.transform.localPosition = exclamationMarkLocalOffset;
            return;
        }

        var go = new GameObject("AvailableQuestExclamationMark", typeof(TextMesh));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = exclamationMarkLocalOffset;

        TextMesh text = go.GetComponent<TextMesh>();
        text.text = "!";
        text.color = exclamationMarkColor;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = generatedExclamationMarkSize;
        text.fontSize = 96;

        _exclamationMarkInstance = go;
    }
}
