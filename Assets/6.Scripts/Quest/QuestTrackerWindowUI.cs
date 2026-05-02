using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    private static bool s_hooksRegistered;

    [SerializeField] private RectTransform trackerContentRoot;
    [SerializeField] private TMP_Text trackerTitleText;
    [SerializeField] private QuestDatabase questDatabase;
    [SerializeField] private GameObject questTrackerRowPrefab;

    private CanvasGroup _canvasGroup;
    private QuestProgressManager _questProgress;
    private Inventory _inventory;
    private PlayerStorage _storage;
    private bool _isRefreshingRows;
    private Coroutine _deferredLayoutRebuild;

    private readonly List<GameObject> _spawnedRows = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToTrackerWindow()
    {
        EnsureSceneLoadedHook();
        TryRestoreTrackerForCurrentScene();
    }

    private static void EnsureSceneLoadedHook()
    {
        if (s_hooksRegistered)
            return;
        s_hooksRegistered = true;
        SceneManager.sceneLoaded -= OnSceneLoadedRestoreTracker;
        SceneManager.sceneLoaded += OnSceneLoadedRestoreTracker;
    }

    private static void OnSceneLoadedRestoreTracker(Scene scene, LoadSceneMode mode)
    {
        TryRestoreTrackerForCurrentScene();
    }

    /// <summary>Re-show the tracker after a scene change when the player still has tracked quests (state is static; the window is recreated per scene).</summary>
    private static void TryRestoreTrackerForCurrentScene()
    {
        if (QuestTrackerState.TrackedCount <= 0)
            return;

        GameObject window = FindSceneObjectByName(TrackerWindowName);
        if (!window)
            return;

        if (!window.GetComponent<QuestTrackerWindowUI>())
            window.AddComponent<QuestTrackerWindowUI>();

        if (!window.activeSelf)
            window.SetActive(true);
    }

    private void Awake()
    {
        EnsureSceneLoadedHook();
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
        if (_deferredLayoutRebuild != null)
        {
            StopCoroutine(_deferredLayoutRebuild);
            _deferredLayoutRebuild = null;
        }

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

        // Drop unknown ids and finished (reward claimed) non-repeatable quests only.
        // Do not prune "gated" / unavailable — map/skills briefly unset during loads and would empty the tracker.
        QuestTrackerState.PruneMissing(id =>
        {
            QuestDefinition def = FindQuestDefinition(id);
            if (!def)
                return false;
            bool permanentlyDone = _questProgress != null && _questProgress.IsPermanentlyComplete(def);
            return !permanentlyDone;
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

            string progress = FormatTrackerObjectiveProgress(q, current, target);

            CreateRow(q.questId, q.displayName, progress, objectiveComplete);
            renderedCount++;
        }

        RefreshTitle(renderedCount);
        SetTrackerVisible(renderedCount > 0);
        RebuildTrackerLayoutImmediate();

        if (renderedCount > 0)
            ScheduleDeferredLayoutRebuild();

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
            nameText.ForceMeshUpdate(true);
        }
        if (progressDisplayText)
        {
            progressDisplayText.text = progressText;
            progressDisplayText.color = TrackerDefaultTextColor;
            progressDisplayText.ForceMeshUpdate(true);
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

    private void RebuildTrackerLayoutImmediate()
    {
        if (!trackerContentRoot)
            return;

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (!_spawnedRows[i])
                continue;
            RectTransform rowRt = _spawnedRows[i].transform as RectTransform;
            if (rowRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(trackerContentRoot);

        RectTransform contentParent = trackerContentRoot.parent as RectTransform;
        if (contentParent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
    }

    private void ScheduleDeferredLayoutRebuild()
    {
        if (_deferredLayoutRebuild != null)
            StopCoroutine(_deferredLayoutRebuild);
        _deferredLayoutRebuild = StartCoroutine(DeferredLayoutRebuildRoutine());
    }

    private IEnumerator DeferredLayoutRebuildRoutine()
    {
        yield return null;
        _deferredLayoutRebuild = null;
        if (!this || !isActiveAndEnabled || !trackerContentRoot)
            yield break;

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (!_spawnedRows[i])
                continue;
            foreach (TMP_Text tmp in _spawnedRows[i].GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp)
                    tmp.ForceMeshUpdate(true);
            }
        }

        RebuildTrackerLayoutImmediate();
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
        if (QuestTrackerState.TrackedCount > 0)
        {
            gameObject.SetActive(true);
            return;
        }

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

    /// <summary>
    /// Shows the quest tracker window after <see cref="QuestTrackerState.TrackQuest"/> so <see cref="OnEnable"/> refresh includes the new quest.
    /// </summary>
    public static void EnsureWindowOpenAfterTrack()
    {
        QuestTrackerWindowUI ui =
            FindFirstObjectByType<QuestTrackerWindowUI>(FindObjectsInactive.Include);
        if (!ui)
        {
            GameObject window = FindSceneObjectByName(TrackerWindowName);
            if (window)
                ui = window.GetComponent<QuestTrackerWindowUI>();
        }

        if (!ui)
            return;

        if (!ui.gameObject.activeSelf)
            ui.gameObject.SetActive(true);
    }

    private static string ResolveSpecialObjectiveTextOrDefault(QuestDefinition q, string fallback)
    {
        if (q != null && !string.IsNullOrWhiteSpace(q.specialObjectiveListText))
            return q.specialObjectiveListText.Trim();
        return fallback;
    }

    private static string FormatTrackerObjectiveProgress(QuestDefinition q, int current, int target)
    {
        int c = Mathf.Clamp(current, 0, target);
        int t = Mathf.Max(1, target);

        return q.objectiveKind switch
        {
            QuestObjectiveKind.KillCount => FormatKillTrackerLine(q, c, t),
            QuestObjectiveKind.GatherItem => FormatGatherTrackerLine(q, c, t),
            QuestObjectiveKind.DieOnce => ResolveSpecialObjectiveTextOrDefault(q, "Die once"),
            _ => $"{c}/{t}"
        };
    }

    private static string FormatKillTrackerLine(QuestDefinition q, int c, int t)
    {
        string id = q.ResolveKillDisplayEnemyId();
        if (string.IsNullOrEmpty(id))
            return $"{c}/{t} kills";

        EnemyDatabase ed = Resources.Load<EnemyDatabase>("Databases/EnemyDatabase");
        EnemyDefinition def = ed ? ed.Get(id) : null;
        string singular = def && !string.IsNullOrWhiteSpace(def.displayName)
            ? def.displayName.Trim()
            : FormatTrackerEnemyIdFallback(id);
        return $"{c}/{t} {PluralizeTrackerUnit(singular, t)}";
    }

    private static string FormatGatherTrackerLine(QuestDefinition q, int c, int t)
    {
        if (string.IsNullOrWhiteSpace(q.objectiveId))
            return $"{c}/{t} gathered";

        ItemDatabase db = Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        ItemDefinition item = db ? db.Get(q.objectiveId.Trim()) : null;
        string singular = item && !string.IsNullOrWhiteSpace(item.displayName)
            ? item.displayName.Trim()
            : q.objectiveId.Trim();
        return $"{c}/{t} {PluralizeTrackerUnit(singular, t)}";
    }

    private static string FormatTrackerEnemyIdFallback(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        if (raw.StartsWith("enemy_", System.StringComparison.Ordinal))
            raw = raw.Substring("enemy_".Length);
        return raw.Replace('_', ' ');
    }

    private static string PluralizeTrackerUnit(string singular, int count)
    {
        if (string.IsNullOrEmpty(singular))
            return count == 1 ? "enemy" : "enemies";
        if (count == 1)
            return singular;
        if (string.Equals(singular, "enemy", System.StringComparison.OrdinalIgnoreCase))
            return "enemies";
        char last = singular[^1];
        if (char.ToLowerInvariant(last) == 's')
            return singular;
        return singular + "s";
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
