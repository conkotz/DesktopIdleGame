using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quest availability marker/provider. NPCInteractionSettings handles click dialogue and accepting.
/// Configure a location id that matches QuestDefinition.obtainLocationId and one or more quest ids.
/// </summary>
public class QuestGiver : MonoBehaviour
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
    [Tooltip("World offset from the top-center of this object's Collider2D bounds.")]
    [SerializeField] private Vector3 exclamationMarkLocalOffset = new(0f, 0.75f, 0f);
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

    public QuestDefinition GetFirstAvailableQuest()
    {
        TryBindManager();
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

    public bool TryAcceptQuest(QuestDefinition quest)
    {
        TryBindManager();
        if (_manager == null || !quest)
            return false;

        bool accepted = _manager.TryAcceptQuest(quest, locationId);
        RefreshExclamationMark();
        return accepted;
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
        bool show = GetFirstAvailableQuest() != null;
        EnsureExclamationMarkInstance();
        if (_exclamationMarkInstance)
        {
            PositionExclamationMark();
            _exclamationMarkInstance.SetActive(show);
        }
    }

    private void EnsureExclamationMarkInstance()
    {
        if (_exclamationMarkInstance)
            return;

        if (exclamationMarkPrefab)
        {
            _exclamationMarkInstance = Instantiate(exclamationMarkPrefab, transform);
            return;
        }

        var go = new GameObject("AvailableQuestExclamationMark", typeof(TextMesh));
        go.transform.SetParent(transform, false);

        TextMesh text = go.GetComponent<TextMesh>();
        text.text = "!";
        text.color = exclamationMarkColor;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = generatedExclamationMarkSize;
        text.fontSize = 96;

        _exclamationMarkInstance = go;
    }

    private void PositionExclamationMark()
    {
        Vector3 anchor = GetColliderTopCenterWorld();
        _exclamationMarkInstance.transform.position = anchor + exclamationMarkLocalOffset;
        _exclamationMarkInstance.transform.rotation = Quaternion.identity;
    }

    private Vector3 GetColliderTopCenterWorld()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col)
            col = GetComponentInChildren<Collider2D>();
        if (!col)
            col = GetComponentInParent<Collider2D>();

        if (!col)
            return transform.position;

        Bounds b = col.bounds;
        return new Vector3(b.center.x, b.max.y, transform.position.z);
    }
}
