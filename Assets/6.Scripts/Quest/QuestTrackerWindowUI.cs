using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders tracked quests in QuestTrackerWindow/Content, preserving track order.
/// </summary>
public class QuestTrackerWindowUI : MonoBehaviour
{
    private const string TrackerWindowName = "QuestTrackerWindow";
    private const string TrackerContentName = "Content";
    private const string TrackerTitleName = "QuestsTrackLabel";
    private const string RowNameTextChild = "QuestTrackerListName";
    private const string RowProgressTextChild = "QuestTrackerProgress";
    private static readonly Color TrackerDefaultTextColor = new Color(0.16f, 0.13f, 0.1f, 1f);
    private static readonly Color TrackerDefaultRowColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TrackerObjectiveCompleteRowColor = new Color(0.82f, 0.96f, 0.82f, 1f);
    private static bool s_hasRememberedWindowActiveState;
    private static bool s_rememberedWindowActive = true;

    [SerializeField] private RectTransform trackerContentRoot;
    [SerializeField] private TMP_Text trackerTitleText;
    [SerializeField] private QuestDatabase questDatabase;
    [SerializeField] private GameObject questTrackerRowPrefab;

    private CanvasGroup _canvasGroup;
    private QuestProgressManager _questProgress;
    private Inventory _inventory;
    private PlayerStorage _storage;
    private bool _isRefreshingRows;

    private readonly List<GameObject> _spawnedRows = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToTrackerWindow()
    {
        GameObject window = FindSceneObjectByName(TrackerWindowName);
        if (!window)
            return;
        if (!window.GetComponent<QuestTrackerWindowUI>())
            window.AddComponent<QuestTrackerWindowUI>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureCanvasGroup();
        ApplyRememberedWindowActiveState();
    }

    private void OnEnable()
    {
        RememberWindowActiveState(true);
        ResolveReferences();
        QuestTrackerState.Changed += RefreshRows;

        if (_questProgress != null)
            _questProgress.ProgressChanged += RefreshRows;
        if (_inventory != null)
            _inventory.OnInventoryChanged += RefreshRows;
        if (_storage != null)
            _storage.OnStorageChanged += RefreshRows;

        RefreshRows();
    }

    private void OnDisable()
    {
        QuestTrackerState.Changed -= RefreshRows;

        if (_questProgress != null)
            _questProgress.ProgressChanged -= RefreshRows;
        if (_inventory != null)
            _inventory.OnInventoryChanged -= RefreshRows;
        if (_storage != null)
            _storage.OnStorageChanged -= RefreshRows;
    }

    private void ResolveReferences()
    {
        if (!trackerContentRoot)
        {
            Transform content = transform.Find(TrackerContentName);
            if (content)
                trackerContentRoot = content as RectTransform;
        }

        if (!trackerTitleText)
            trackerTitleText = FindChildByName(transform, TrackerTitleName)?.GetComponent<TMP_Text>();

        if (!questDatabase)
            questDatabase = Resources.Load<QuestDatabase>("Databases/QuestDatabase_Main");

        if (_questProgress == null)
            _questProgress = QuestProgressManager.Instance ?? FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (_inventory == null)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (_storage == null)
            _storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }

    private void RefreshRows()
    {
        if (_isRefreshingRows)
            return;
        _isRefreshingRows = true;

        ResolveReferences();
        ClearRows();

        if (!trackerContentRoot || !questDatabase)
        {
            SetTrackerVisible(false);
            _isRefreshingRows = false;
            return;
        }

        // Auto-untrack only missing or permanently completed (claimed) quests.
        QuestTrackerState.PruneMissing(id =>
        {
            QuestDefinition def = FindQuestDefinition(id);
            if (!def)
                return false;
            bool permanentlyDone = _questProgress != null && _questProgress.IsPermanentlyComplete(def);
            bool gated = _questProgress != null && _questProgress.IsQuestGatedByPrerequisites(def);
            return !permanentlyDone && !gated;
        });
        QuestTrackerState.PruneToMaxCount();

        IReadOnlyList<string> tracked = QuestTrackerState.OrderedTrackedQuestIds;
        int renderedCount = 0;
        for (int i = 0; i < tracked.Count; i++)
        {
            string questId = tracked[i];
            QuestDefinition q = FindQuestDefinition(questId);
            if (!q)
                continue;

            int current = _questProgress != null ? _questProgress.GetDisplayProgress(q) : 0;
            int target = Mathf.Max(1, q.targetCount);
            bool objectiveComplete = q.IsComplete(current);

            string progress = q.objectiveKind switch
            {
                QuestObjectiveKind.KillCount => $"{Mathf.Clamp(current, 0, target)}/{target} kills",
                QuestObjectiveKind.GatherItem => $"{Mathf.Clamp(current, 0, target)}/{target} gathered",
                _ => $"{Mathf.Clamp(current, 0, target)}/{target}"
            };

            CreateRow(q.questId, q.displayName, progress, objectiveComplete);
            renderedCount++;
        }

        RefreshTitle(renderedCount);
        SetTrackerVisible(renderedCount > 0);
        if (trackerContentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(trackerContentRoot);

        _isRefreshingRows = false;
    }

    private void ClearRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i])
                Destroy(_spawnedRows[i]);
        }
        _spawnedRows.Clear();
    }

    private void CreateRow(string questId, string questName, string progressText, bool objectiveComplete)
    {
        if (!questTrackerRowPrefab)
            return;

        GameObject row = Instantiate(questTrackerRowPrefab, trackerContentRoot);
        row.name = "TrackedQuestRow";
        _spawnedRows.Add(row);

        RectTransform rowRt = row.transform as RectTransform;
        if (rowRt)
        {
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = Vector2.zero;
            rowRt.sizeDelta = new Vector2(0f, rowRt.sizeDelta.y);
        }

        TMP_Text nameText = row.transform.Find(RowNameTextChild)?.GetComponent<TMP_Text>();
        TMP_Text progressDisplayText = row.transform.Find(RowProgressTextChild)?.GetComponent<TMP_Text>();
        Image rowBg = row.GetComponent<Image>();
        if (rowBg != null)
            rowBg.color = objectiveComplete ? TrackerObjectiveCompleteRowColor : TrackerDefaultRowColor;
        if (nameText)
        {
            nameText.text = questName;
            nameText.color = TrackerDefaultTextColor;
        }
        if (progressDisplayText)
        {
            progressDisplayText.text = progressText;
            progressDisplayText.color = TrackerDefaultTextColor;
        }

        Button rowButton = row.GetComponent<Button>();
        if (rowButton == null)
            rowButton = row.AddComponent<Button>();
        Image targetImage = row.GetComponent<Image>();
        if (targetImage != null)
            rowButton.targetGraphic = targetImage;
        rowButton.transition = Selectable.Transition.None;
        rowButton.onClick.RemoveAllListeners();
        rowButton.onClick.AddListener(() => OnTrackedQuestRowClicked(questId));
    }

    private QuestDefinition FindQuestDefinition(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId) || questDatabase == null || questDatabase.All == null)
            return null;

        string key = questId.Trim();
        IReadOnlyList<QuestDefinition> all = questDatabase.All;
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || string.IsNullOrWhiteSpace(q.questId))
                continue;
            if (string.Equals(q.questId.Trim(), key, System.StringComparison.Ordinal))
                return q;
        }

        return null;
    }

    private void RefreshTitle(int trackedCount)
    {
        if (!trackerTitleText)
            return;

        int current = Mathf.Clamp(trackedCount, 0, QuestTrackerState.MaxTrackedQuestCount);
        trackerTitleText.text = $"Quest Tracker ({current}/{QuestTrackerState.MaxTrackedQuestCount})";
    }

    private void OnTrackedQuestRowClicked(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        bool questPageAlreadyOpen =
            menu != null &&
            menu.IsOpen &&
            menu.CurrentPage != null &&
            string.Equals(menu.CurrentPage.name, "QuestPage", System.StringComparison.Ordinal);
        if (!questPageAlreadyOpen)
            menu?.OpenQuest();

        StartCoroutine(SelectQuestNextFrame(questId));
    }

    private static IEnumerator SelectQuestNextFrame(string questId)
    {
        yield return null;
        QuestPageUI questPage = FindFirstObjectByType<QuestPageUI>(FindObjectsInactive.Include);
        if (questPage != null)
            questPage.SelectQuestById(questId);
    }

    private void EnsureCanvasGroup()
    {
        if (!_canvasGroup)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (!_canvasGroup)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetTrackerVisible(bool visible)
    {
        EnsureCanvasGroup();
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private void ApplyRememberedWindowActiveState()
    {
        if (!s_hasRememberedWindowActiveState || s_rememberedWindowActive)
            return;

        gameObject.SetActive(false);
    }

    private void RememberWindowActiveState(bool active)
    {
        s_hasRememberedWindowActiveState = true;
        s_rememberedWindowActive = active;
    }

    public void RememberWindowClosedByUser()
    {
        RememberWindowActiveState(false);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (t.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
