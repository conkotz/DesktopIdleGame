using System;
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

    [Header("Quest pickup / turn-in marker")]
    [Tooltip("Optional prefab while a quest is available (!) or ready to turn in here (?). If empty, a yellow 3D TextMesh is created.")]
    [SerializeField] private GameObject exclamationMarkPrefab;
    [Tooltip("World offset from the top-center of this object's Collider2D bounds.")]
    [SerializeField] private Vector3 exclamationMarkLocalOffset = new(0f, 0.75f, 0f);
    [SerializeField] private float generatedExclamationMarkSize = 0.2f;
    [SerializeField] private Color exclamationMarkColor = Color.yellow;

    private GameObject _exclamationMarkInstance;
    private QuestProgressManager _manager;
    private readonly List<QuestDefinition> _scratchLocationQuests = new();
    private readonly List<QuestDefinition> _scratchClaimableQuests = new();
    private Inventory _subscribedInventory;
    private PlayerStorage _subscribedStorage;

    private void OnEnable()
    {
        TryBindManager();
        TrySubscribeInventoryAndStorage();
        RefreshExclamationMark();
    }

    private void OnDisable()
    {
        UnsubscribeInventoryAndStorage();
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

        if (TrySubscribeInventoryAndStorage())
            RefreshExclamationMark();
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

    /// <returns>True when the subscribed inventory or storage instance changed (same pattern as <see cref="QuestPageUI"/>).</returns>
    private bool TrySubscribeInventoryAndStorage()
    {
        bool changed = false;

        Inventory inv = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inv != _subscribedInventory)
        {
            if (_subscribedInventory)
                _subscribedInventory.OnInventoryChanged -= OnInventoryOrStorageChanged;
            _subscribedInventory = inv;
            if (_subscribedInventory)
                _subscribedInventory.OnInventoryChanged += OnInventoryOrStorageChanged;
            changed = true;
        }

        PlayerStorage st = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (st != _subscribedStorage)
        {
            if (_subscribedStorage)
                _subscribedStorage.OnStorageChanged -= OnInventoryOrStorageChanged;
            _subscribedStorage = st;
            if (_subscribedStorage)
                _subscribedStorage.OnStorageChanged += OnInventoryOrStorageChanged;
            changed = true;
        }

        return changed;
    }

    private void UnsubscribeInventoryAndStorage()
    {
        if (_subscribedInventory != null)
        {
            _subscribedInventory.OnInventoryChanged -= OnInventoryOrStorageChanged;
            _subscribedInventory = null;
        }

        if (_subscribedStorage != null)
        {
            _subscribedStorage.OnStorageChanged -= OnInventoryOrStorageChanged;
            _subscribedStorage = null;
        }
    }

    private void OnInventoryOrStorageChanged()
    {
        if (!isActiveAndEnabled)
            return;
        RefreshExclamationMark();
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

    /// <summary>
    /// First quest from this giver that matches the journal <c>Complete Quest</c> button (ready to claim at this obtain-location).
    /// When both a claimable and an available quest exist, prefer this for markers and <see cref="TryClaimFirstReadyQuestReward"/>.
    /// </summary>
    public QuestDefinition GetFirstClaimableQuestAtLocation()
    {
        TryBindManager();
        if (_manager == null)
            return null;

        QuestDefinition q = FindClaimableQuestById(questId);
        if (q)
            return q;

        if (additionalQuestIds != null)
        {
            for (int i = 0; i < additionalQuestIds.Count; i++)
            {
                q = FindClaimableQuestById(additionalQuestIds[i]);
                if (q)
                    return q;
            }
        }

        if (!string.IsNullOrWhiteSpace(locationId))
        {
            _manager.CollectClaimableQuestsAtLocation(locationId, _scratchClaimableQuests);
            if (_scratchClaimableQuests.Count > 0)
                return _scratchClaimableQuests[0];
        }

        return null;
    }

    /// <summary>
    /// Claims the first claimable quest at this giver (same as journal Complete Quest). Returns false if nothing to claim or claim blocked.
    /// </summary>
    public bool TryClaimFirstReadyQuestReward()
    {
        TryBindManager();
        if (_manager == null)
            return false;

        QuestDefinition q = GetFirstClaimableQuestAtLocation();
        if (!q)
            return false;

        if (!_manager.TryClaimQuestReward(q))
            return false;

        RefreshExclamationMark();
        return true;
    }

    /// <summary>
    /// Explicit quest ids first (primary, then additional), then any other acceptable quests at <see cref="locationId"/>.
    /// </summary>
    public List<QuestDefinition> GetAllAvailableQuests()
    {
        var list = new List<QuestDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void TryAdd(QuestDefinition q)
        {
            if (!q || string.IsNullOrWhiteSpace(q.questId))
                return;
            string id = q.questId.Trim();
            if (seen.Contains(id))
                return;
            seen.Add(id);
            list.Add(q);
        }

        TryBindManager();

        TryAdd(FindAvailableQuestById(questId));
        if (additionalQuestIds != null)
        {
            for (int i = 0; i < additionalQuestIds.Count; i++)
                TryAdd(FindAvailableQuestById(additionalQuestIds[i]));
        }

        if (_manager != null && !string.IsNullOrWhiteSpace(locationId))
        {
            _manager.CollectAcceptableQuestsAtLocation(locationId, _scratchLocationQuests);
            for (int i = 0; i < _scratchLocationQuests.Count; i++)
                TryAdd(_scratchLocationQuests[i]);
        }

        return list;
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

    private QuestDefinition FindClaimableQuestById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _manager == null)
            return null;

        QuestDefinition quest = _manager.GetQuestDefinition(id.Trim());
        if (!quest || !_manager.IsQuestReadyToClaimAtGiverLocation(quest, locationId))
            return null;

        return quest;
    }

    private void RefreshExclamationMark()
    {
        bool claimable = GetFirstClaimableQuestAtLocation() != null;
        bool available = GetFirstAvailableQuest() != null;
        bool show = claimable || available;

        EnsureExclamationMarkInstance();
        if (_exclamationMarkInstance)
        {
            PositionExclamationMark();
            ApplyMarkerGlyph(claimable);
            _exclamationMarkInstance.SetActive(show);
        }
    }

    private void ApplyMarkerGlyph(bool readyToClaim)
    {
        if (!_exclamationMarkInstance)
            return;

        TextMesh tm = _exclamationMarkInstance.GetComponent<TextMesh>();
        if (!tm)
            tm = _exclamationMarkInstance.GetComponentInChildren<TextMesh>(true);
        if (tm)
        {
            tm.text = readyToClaim ? "?" : "!";
            tm.color = exclamationMarkColor;
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
